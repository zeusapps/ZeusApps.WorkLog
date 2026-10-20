using System.Net;
using System.Text.Json;
using WorkLog.Configuration;
using WorkLog.Infrastructure;
using WorkLog.Journaling;
using WorkLog.Mcp;
using WorkLog.Reporting;
using WorkLog.Tests.Testing;
using WorkLog.Updates;
using WorkJson = WorkLog.Infrastructure.Json;
using Xunit;

namespace WorkLog.Tests.Updates;

public sealed class DiscoveryTests
{
    public static ReleaseInfo Release(string version) => new(version, ReleaseInfo.ReleasesUrl + "/tag/v" + version,
        ReleaseInfo.DownloadUrl(version), ReleaseInfo.DownloadUrl(version) + ".sha256");

    public static string Metadata(string version = "0.2.0", bool draft = false, bool prerelease = false, bool assets = true) =>
        JsonSerializer.Serialize(new
        {
            tag_name = "v" + version, html_url = ReleaseInfo.ReleasesUrl + "/tag/v" + version, draft, prerelease,
            assets = assets ? new[]
            {
                new { name = ReleaseInfo.PackageName(version), browser_download_url = ReleaseInfo.DownloadUrl(version) },
                new { name = ReleaseInfo.PackageName(version) + ".sha256", browser_download_url = ReleaseInfo.DownloadUrl(version) + ".sha256" }
            } : []
        });

    [Theory]
    [InlineData("0.2.0", true)]
    [InlineData("0.1.0", false)]
    [InlineData("0.0.9", false)]
    [InlineData("0.10.0", true)]
    [InlineData("01.2.0", false)]
    [InlineData("1.0.0-beta", false)]
    [InlineData("broken", false)]
    public void VersionsUseStableSemanticComparison(string version, bool newer) => Assert.Equal(newer, Release(version).IsNewerThan("0.1.0"));

    [Fact]
    public void OldConfigurationAndOptOutRemainValid()
    {
        Assert.True(WorkJson.Decode<WorkLogConfiguration>("{}").Updates.Enabled);
        var config = WorkJson.Decode<WorkLogConfiguration>("""{"updates":{"enabled":false},"branchClassifications":{"feature":"feature"}}""");
        Assert.False(config.Updates.Enabled);
        Assert.Equal("feature", config.BranchClassifications["feature"]);
    }

    [Theory]
    [InlineData(false, false, true, "0.2.0", true)]
    [InlineData(true, false, true, "0.2.0", false)]
    [InlineData(false, true, true, "0.2.0", false)]
    [InlineData(false, false, false, "0.2.0", false)]
    [InlineData(false, false, true, "0.2.0-beta", false)]
    [InlineData(false, false, true, "bad", false)]
    public async Task ValidatesReleaseAndMakesOnlyPublicMetadataRequest(bool draft, bool prerelease, bool assets, string version, bool offered)
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal(ReleaseInfo.Endpoint, request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Null(request.Content);
            Assert.Null(request.Headers.Authorization);
            Assert.Contains("WorkLog/", request.Headers.UserAgent.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Metadata(version, draft, prerelease, assets)) });
        });
        using var client = new HttpClient(handler);
        var result = await new ReleaseClient(client).CheckAsync();
        Assert.True(result.Succeeded);
        Assert.Equal(offered, result.Release is not null);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"draft\":\"false\"}")]
    public async Task InvalidMetadataIsCheckFailure(string content)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) })));
        Assert.False((await new ReleaseClient(http).CheckAsync()).Succeeded);
    }

    [Fact]
    public async Task RateLimitsAndCancellationAreFailuresRatherThanUpToDate()
    {
        using var http = new HttpClient(new Handler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.RetryAfter = new(TimeSpan.FromHours(2));
            return Task.FromResult(response);
        }));
        var result = await new ReleaseClient(http).CheckAsync();
        Assert.False(result.Succeeded);
        Assert.NotNull(result.RetryAfter);

        using var canceled = new HttpClient(new Handler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        Assert.False((await new ReleaseClient(canceled).CheckAsync(timeout.Token)).Succeeded);
    }

    [Fact]
    public async Task ConcurrentCheckersShareAttemptAndHintsExpire()
    {
        using var box = new TestWorkspace();
        var count = 0;
        using var http = new HttpClient(new Handler(async (_, token) =>
        {
            Interlocked.Increment(ref count);
            await Task.Delay(100, token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Metadata()) };
        }));
        var settings = new LocalSettings(box.Data);
        var discoveries = Enumerable.Range(0, 12).Select(_ => new UpdateDiscovery(settings, new ReleaseClient(http))).ToArray();
        await Task.WhenAll(discoveries.Select(discovery => Task.Run(() => discovery.CheckAsync(false))));
        Assert.Equal(1, count);
        Assert.Equal("0.2.0", discoveries[0].GetHint("0.1.0")!.AvailableVersion);
        Assert.Null(discoveries[0].GetHint("0.2.0"));
        var cache = WorkJson.Decode<UpdateCache>(File.ReadAllText(discoveries[0].CachePath));
        AtomicFile.Write(discoveries[0].CachePath, WorkJson.Encode(cache with { ValidatedAt = DateTimeOffset.UtcNow.AddDays(-8) }));
        Assert.Null(discoveries[0].GetHint("0.1.0"));
        File.WriteAllText(discoveries[0].CachePath, "broken");
        Assert.Null(discoveries[0].GetHint());
    }

    [Fact]
    public async Task DisabledDiscoveryDoesNotRunOrHintButExplicitCheckWorks()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        settings.Configuration.Updates.Enabled = false;
        var count = 0;
        using var http = new HttpClient(new Handler((_, _) =>
        {
            count++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Metadata()) });
        }));
        var discovery = new UpdateDiscovery(settings, new ReleaseClient(http));
        await discovery.RunAsync(CancellationToken.None);
        Assert.Null(await discovery.CheckAsync(false));
        Assert.Equal(0, count);
        Assert.True((await discovery.CheckAsync(true))!.Succeeded);
        Assert.Equal(1, count);
        Assert.Null(discovery.GetHint());
    }

    [Fact]
    public async Task BusyCacheDoesNotPreventExplicitCheckOrJournalUse()
    {
        using var box = new TestWorkspace();
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Metadata()) })));
        var discovery = new UpdateDiscovery(new LocalSettings(box.Data), new ReleaseClient(http));
        Directory.CreateDirectory(Path.GetDirectoryName(discovery.CachePath)!);
        using var gate = new FileStream(discovery.CachePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Assert.Null(await discovery.CheckAsync(false));
        Assert.True((await discovery.CheckAsync(true))!.Succeeded);
        Assert.Null(discovery.GetHint());
    }

    [Fact]
    public async Task UnresolvedNetworkDoesNotBlockJournalCallsOrCancellation()
    {
        using var box = new TestWorkspace();
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(async (_, token) =>
        {
            requested.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new(HttpStatusCode.OK);
        }));
        var settings = new LocalSettings(box.Data);
        var discovery = new UpdateDiscovery(settings, new ReleaseClient(http));
        using var cancellation = new CancellationTokenSource();
        var background = Task.Run(() => discovery.RunAsync(cancellation.Token));
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var tools = new JournalTools(new Journal(settings, new("test-repo")), discovery);
        var logged = tools.LogWork(new("in_progress", "offline", OccurredAt: "2026-10-20T09:00:00Z"));
        Assert.True(logged.Created);
        Assert.Single(tools.QueryWork(new("2026-10-20T00:00:00Z", "2026-10-21T00:00:00Z", "UTC")).Entries);
        await cancellation.CancelAsync();
        await background.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void NearLimitReportOmitsHintWithoutChangingObservations()
    {
        using var box = new TestWorkspace();
        using var http = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("No network expected.")));
        var settings = new LocalSettings(box.Data);
        var discovery = new UpdateDiscovery(settings, new ReleaseClient(http));
        AtomicFile.Write(discovery.CachePath, WorkJson.Encode(new UpdateCache(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Release("999.0.0"))));
        var journal = new Journal(settings, new("repo"));
        journal.Log(new("in_progress", "measure", Summary: "", OccurredAt: "2026-10-20T09:00:00Z"));
        var period = new QueryRequest("2026-10-20T00:00:00Z", "2026-10-21T00:00:00Z", "UTC", Change: "measure");
        var wire = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var overhead = JsonSerializer.SerializeToUtf8Bytes(journal.Query(period), wire).Length;
        journal.Log(new("in_progress", "payload", Summary: new string('x', 64 * 1024 - overhead - 50), OccurredAt: "2026-10-20T09:00:00Z"));
        var result = new JournalTools(journal, discovery).QueryWork(period with { Change = "payload" });
        Assert.Null(result.UpdateHint);
        Assert.Single(result.Entries);
        Assert.InRange(JsonSerializer.SerializeToUtf8Bytes(result, wire).Length, 64 * 1024 - 100, 64 * 1024);
        Assert.False(result.HasMore);
    }

    public sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
