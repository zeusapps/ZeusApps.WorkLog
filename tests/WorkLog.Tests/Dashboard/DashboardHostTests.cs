using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using WorkLog.Configuration;
using WorkLog.Dashboard;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Dashboard;

public sealed class DashboardHostTests
{
    [Fact]
    public async Task EmbeddedBrowserAssetsHaveCorrectRoutesAndContentTypes()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "assets", new DashboardAuthorization(box.Data), new DashboardTests.FixtureCommand(box.Repo));
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(DashboardHost.Address(app)) };
        var html = await client.GetStringAsync("/history");
        Assert.Contains("<worklog-app>", html);
        Assert.DoesNotContain("onload=", html);
        Assert.Contains("href=\"/favicon.svg\"", html);
        var favicon = await client.GetAsync("/favicon.svg");
        Assert.Equal(HttpStatusCode.OK, favicon.StatusCode);
        Assert.Equal("image/svg+xml", favicon.Content.Headers.ContentType?.MediaType);
        var assets = Regex.Matches(html, "(?:src|href)=\"([^\"]+\\.(?:js|css))\"").Select(m => m.Groups[1].Value).ToArray();
        Assert.True(assets.Length >= 2);
        foreach (var asset in assets)
        {
            var response = await client.GetAsync("/" + asset.TrimStart('/'));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(asset.EndsWith(".css", StringComparison.Ordinal) ? "text/css" : "text/javascript", response.Content.Headers.ContentType?.MediaType);
            Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 100);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/missing.js")).StatusCode);
        await app.StopAsync();
    }

    [Fact]
    public async Task AuthorizationProtectsPrivateDataSurvivesRestartAndCanBeRevoked()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        var authorization = new DashboardAuthorization(box.Data);
        var instance = "authorization";
        await using var app = DashboardHost.Create(settings, box.Repo, 0, instance, authorization, new DashboardTests.FixtureCommand(box.Repo));
        await app.StartAsync();
        var address = DashboardHost.Address(app);
        using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/state")).StatusCode);
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/state"))
        {
            request.Headers.Add("Origin", "https://other.example");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/state"))
        {
            request.Headers.Host = "untrusted.example:" + new Uri(address).Port;
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
        }
        var token = authorization.Bootstrap();
        Assert.True((await client.PostAsJsonAsync("/api/bootstrap", new { token })).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/bootstrap", new { token })).StatusCode);
        Assert.True((await client.GetAsync("/api/state")).IsSuccessStatusCode);
        Assert.True(handler.CookieContainer.GetCookies(new Uri(address))[authorization.CookieName]!.HttpOnly);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/launch"))
        {
            request.Headers.Add("X-WorkLog-Launch", authorization.Sign("launch:" + instance));
            Assert.True((await client.SendAsync(request)).IsSuccessStatusCode);
        }
        await app.StopAsync();
        await using var restarted = DashboardHost.Create(settings, box.Repo, new Uri(address).Port, "restart", new DashboardAuthorization(box.Data), new DashboardTests.FixtureCommand(box.Repo));
        await restarted.StartAsync();
        Assert.True((await client.GetAsync("/api/state")).IsSuccessStatusCode);
        File.Delete(Path.Combine(box.Data, "dashboard.secret"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/state")).StatusCode);
        await restarted.StopAsync();
        Assert.False(File.Exists(Path.Combine(box.Data, "journal.db")));
        Assert.False(File.Exists(Path.Combine(box.Data, "config.json")));
    }

    [Fact]
    public async Task AuthorizedDocumentWritesRetainEarlierEditsAndRejectStaleBlocks()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var path = DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n\nReview this.\n");
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "documents", authorization, new DashboardTests.FixtureCommand(box.Repo));
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(DashboardHost.Address(app)) };
        await client.PostAsJsonAsync("/api/bootstrap", new { token = authorization.Bootstrap() });
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/catalog");
        var source = catalog.GetProperty("sources")[0].GetProperty("id").GetString()!;
        var response = await client.PostAsJsonAsync("/api/document", new DocumentRequest(source, "demo", "design.md"));
        var document = (await response.Content.ReadFromJsonAsync<DocumentContent>())!;
        File.WriteAllText(path, "# Design\n\nAgent changed this.\n");
        var stale = await client.PostAsJsonAsync("/api/comments", new CommentRequest(source, "demo", "design.md", document.Revision, 2, 3, "Review this.", "Keep my comment"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.DoesNotContain("REVIEW:", File.ReadAllText(path));
        var outside = await client.PostAsJsonAsync("/api/document", new DocumentRequest(source, "demo", "../other.md"));
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        var saved = await client.PostAsJsonAsync("/api/comments", new CommentRequest(source, "demo", "design.md", document.Revision, 2, 3, "Agent changed this.", "Now review"));
        Assert.True(saved.IsSuccessStatusCode);
        var note = Assert.Single(ReviewMarker.Parse(File.ReadAllText(path)));
        Assert.Equal("Now review", note.Comment);
        var edit = await client.PostAsJsonAsync("/api/comments/edit", new ReviewMutationRequest(source, "demo", "design.md", document.Revision,
            note.Line, note.Raw, "Edited through HTTP", note.Id));
        Assert.True(edit.IsSuccessStatusCode);
        note = Assert.Single(ReviewMarker.Parse(File.ReadAllText(path)));
        Assert.Equal("Edited through HTTP", note.Comment);
        var deleted = await client.PostAsJsonAsync("/api/comments/delete", new ReviewMutationRequest(source, "demo", "design.md", document.Revision,
            note.Line, note.Raw, Id: note.Id));
        Assert.True(deleted.IsSuccessStatusCode);
        Assert.Empty(ReviewMarker.Parse(File.ReadAllText(path)));
        await app.StopAsync();
    }
}
