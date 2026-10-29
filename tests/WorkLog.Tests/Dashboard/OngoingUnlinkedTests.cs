using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WorkLog.Configuration;
using WorkLog.Dashboard;
using WorkLog.Journaling;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Dashboard;

public sealed class OngoingUnlinkedTests
{
    /// <summary>Journal entries with neither ticket nor change are only possible in older journals, so tests insert them directly.</summary>
    private static void Legacy(TestWorkspace box, string status, string? summary, string occurredAt, string? extracted = null, string repository = "repo")
    {
        var at = DateTimeOffset.Parse(occurredAt);
        var entry = new Entry(Guid.NewGuid().ToString("N"), repository, extracted is null ? null : "feature/" + extracted, null, [], extracted, status,
            "unclassified", summary, at, at, "test", null);
        using var connection = new SqliteConnection($"Data Source={Path.Combine(box.Data, "journal.db")};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO entries(id,occurred,repository,change_name,status,payload) VALUES($id,$at,$repo,NULL,$status,$payload)";
        command.Parameters.AddWithValue("$id", entry.Id);
        command.Parameters.AddWithValue("$at", at.UtcTicks);
        command.Parameters.AddWithValue("$repo", repository);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$payload", WorkLog.Infrastructure.Json.Encode(entry));
        command.ExecuteNonQuery();
    }

    private static readonly Ticket Company = new("jira", "company", "DEP-12");

    [Fact]
    public void OnlyObservationsWithNeitherTicketNorChangeAreReturnedNewestFirstInTheWindow()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        journal.Log(new("in_progress", Tickets: [Company], Summary: "ticket only", OccurredAt: "2026-10-20T10:00:00Z"));
        journal.Log(new("in_progress", "demo", Summary: "change only", OccurredAt: "2026-10-20T11:00:00Z"));
        journal.Log(new("in_progress", "demo", Tickets: [Company], Summary: "both", OccurredAt: "2026-10-20T12:00:00Z"));
        Legacy(box, "in_progress", "extracted id only", "2026-10-20T13:00:00Z", "DEP-99");
        Legacy(box, "done", "plain", "2026-10-21T10:00:00Z");
        Legacy(box, "done", "too old", "2026-10-10T10:00:00Z");
        Legacy(box, "done", "at the end bound", "2026-10-26T00:00:00Z");
        Legacy(box, "done", "other repo", "2026-10-21T10:00:00Z", repository: "other");
        var service = new TimelineService(new DashboardJournal(box.Data));

        var page = service.Unlinked(new("2026-10-19T00:00:00Z", "2026-10-26T00:00:00Z", ["repo"]));

        Assert.Equal(new[] { "plain", "extracted id only" }, page.Entries.Select(e => e.Summary));
        Assert.False(page.Truncated);
        Assert.Equal("DEP-99", page.Entries[1].ExtractedTicketId);
        Assert.Equal("other repo", Assert.Single(service.Unlinked(new("2026-10-19T00:00:00Z", "2026-10-26T00:00:00Z", ["other"])).Entries).Summary);
        Assert.Empty(service.Unlinked(new("2026-10-19T00:00:00Z", "2026-10-26T00:00:00Z", ["missing"])).Entries);
        Assert.Throws<ArgumentException>(() => service.Unlinked(new("2026-10-26T00:00:00Z", "2026-10-19T00:00:00Z")));
    }

    [Fact]
    public void TheListIsCappedAndMarkedTruncated()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        journal.Log(new("in_progress", "seed", OccurredAt: "2026-10-01T00:00:00Z"));
        for (var i = 0; i < TimelineService.UnlinkedLimit + 1; i++)
            Legacy(box, "in_progress", "entry " + i, DateTimeOffset.Parse("2026-10-20T00:00:00Z").AddMinutes(i).ToString("O"));
        var service = new TimelineService(new DashboardJournal(box.Data));
        var window = new UnlinkedQuery("2026-10-19T00:00:00Z", "2026-10-26T00:00:00Z");

        var capped = service.Unlinked(window);
        Assert.Equal(TimelineService.UnlinkedLimit, capped.Entries.Length);
        Assert.True(capped.Truncated);
        Assert.Equal("entry " + TimelineService.UnlinkedLimit, capped.Entries[0].Summary);

        var exact = service.Unlinked(window with
        {
            To = "2026-10-20T03:20:00Z"
        });
        Assert.Equal(TimelineService.UnlinkedLimit, exact.Entries.Length);
        Assert.False(exact.Truncated);
    }

    [Fact]
    public void ASnapshotKeepsLaterObservationsOut()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        journal.Log(new("in_progress", "seed", OccurredAt: "2026-10-01T00:00:00Z"));
        Legacy(box, "in_progress", "first", "2026-10-20T10:00:00Z");
        var service = new TimelineService(new DashboardJournal(box.Data));
        var window = new UnlinkedQuery("2026-10-19T00:00:00Z", "2026-10-26T00:00:00Z");
        var first = service.Unlinked(window);
        Legacy(box, "done", "second", "2026-10-21T10:00:00Z");

        Assert.Equal("first", Assert.Single(service.Unlinked(window with
        {
            Snapshot = first.Snapshot
        }).Entries).Summary);
        Assert.Equal(2, service.Unlinked(window).Entries.Length);
        Assert.Throws<ArgumentException>(() => service.Unlinked(window with { Snapshot = first.Snapshot + 50 }));
    }

    [Fact]
    public async Task TheEndpointRequiresAuthorizationAndReturnsEntries()
    {
        using var box = new TestWorkspace();
        new Journal(new LocalSettings(box.Data), new("repo")).Log(new("in_progress", "seed", OccurredAt: "2026-10-01T00:00:00Z"));
        Legacy(box, "done", "plain", "2026-10-21T10:00:00Z");
        var authorization = new DashboardAuthorization(box.Data);
        var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "open", authorization, new DashboardTests.FixtureCommand(box.Repo));
        await app.StartAsync();
        try
        {
            var address = new Uri(DashboardHost.Address(app));
            var body = new
            {
                repositories = new[] { "repo" },
                from = "2026-10-19T00:00:00Z",
                to = "2026-10-26T00:00:00Z"
            };
            using (var anonymous = new HttpClient { BaseAddress = address })
                Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/ongoing/unlinked", body)).StatusCode);
            using var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = address };
            Assert.True((await client.PostAsJsonAsync("/api/bootstrap", new
            {
                token = authorization.Bootstrap()
            })).IsSuccessStatusCode);

            using var page = JsonDocument.Parse(await (await client.PostAsJsonAsync("/api/ongoing/unlinked", body)).Content.ReadAsStringAsync());
            Assert.False(page.RootElement.GetProperty("truncated").GetBoolean());
            Assert.Equal("plain", Assert.Single(page.RootElement.GetProperty("entries").EnumerateArray()).GetProperty("summary").GetString());
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ongoing/unlinked", body with
            {
                to = body.from
            })).StatusCode);
        }
        finally { await app.StopAsync(); await app.DisposeAsync(); }
    }
}
