using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WorkLog.Configuration;
using WorkLog.Dashboard;
using WorkLog.Journaling;
using WorkLog.Persistence;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Dashboard;

public sealed class DailyDigestResponseTests
{
    private const string Zone = "Europe/Warsaw";
    private static readonly Ticket Retry = new("jira", "company", "DEP-1232");
    private static readonly Ticket Export = new("jira", "company", "DEP-1240");

    // Warsaw is UTC+1 on 26 October 2026, so "now" is Monday 26 October 00:00 local; Friday 23 October is the last recorded day.
    private static DailyRequest Monday() => new("2026-10-25T23:00:00Z", Zone);

    private static Journal Open(TestWorkspace box) => new(new LocalSettings(box.Data), new("repo"));

    private static string Item(Entry entry) => TimelineService.IdFor(entry, "ticket");

    [Fact]
    public void DigestsOfBothTheSelectedDayAndTodayAreReturnedAndUnrelatedItemsAreLeftOut()
    {
        using var box = new TestWorkspace();
        var journal = Open(box);
        var f1 = journal.Log(new("in_progress", "c", Tickets: [Retry], Summary: "a", OccurredAt: "2026-10-23T08:00:00Z")).Entry;
        var f2 = journal.Log(new("done", "c", Tickets: [Retry], Summary: "b", OccurredAt: "2026-10-23T10:00:00Z")).Entry;
        var t1 = journal.Log(new("in_progress", "d", Tickets: [Export], Summary: "c", OccurredAt: "2026-10-26T08:00:00Z")).Entry;
        var t2 = journal.Log(new("in_progress", "d", Tickets: [Export], Summary: "d", OccurredAt: "2026-10-26T09:00:00Z")).Entry;
        var other = journal.Log(new("in_progress", "e", Tickets: [new("jira", "company", "DEP-9")], Summary: "unrelated", OccurredAt: "2026-10-01T09:00:00Z")).Entry;
        var store = new DigestStore(box.Data);
        store.Save(new(Item(f1), "2026-10-23", new[] { f1.Id, f2.Id }.Order(StringComparer.Ordinal).ToArray(), "Friday rollup.", DateTimeOffset.UtcNow));
        store.Save(new(Item(t1), "2026-10-26", new[] { t1.Id, t2.Id }.Order(StringComparer.Ordinal).ToArray(), "Today rollup.", DateTimeOffset.UtcNow));
        store.Save(new(Item(other), "2026-10-01", ["x", "y"], "Unrelated.", DateTimeOffset.UtcNow));

        var result = new DailyService(new DashboardJournal(box.Data), store).Query(Monday());
        Assert.Equal("2026-10-23", result.Day);
        Assert.Equal(["Friday rollup.", "Today rollup."], result.Digests.Select(d => d.Text).Order());
        var friday = Assert.Single(result.Digests, d => d.Text == "Friday rollup.");
        Assert.Equal(new[] { f1.Id, f2.Id }.Order(StringComparer.Ordinal), friday.Covers);
    }

    [Fact]
    public void MissingStoreIsNormalAndUnreadableStoreHasASafeIssue()
    {
        using var box = new TestWorkspace();
        var friday = Open(box).Log(new("done", "c", Tickets: [Retry], Summary: "friday", OccurredAt: "2026-10-23T10:00:00Z")).Entry;
        var journal = new DashboardJournal(box.Data);
        var missing = new DailyService(journal, new DigestStore(box.Data)).Query(Monday());
        Assert.Empty(missing.Digests);
        Assert.Null(missing.DigestIssue);
        var noStore = new DailyService(journal).Query(Monday());
        Assert.Empty(noStore.Digests);
        Assert.Null(noStore.DigestIssue);
        Directory.CreateDirectory(Path.Combine(box.Data, "digests.db"));
        var result = new DailyService(journal, new DigestStore(box.Data)).Query(Monday());
        Assert.Empty(result.Digests);
        Assert.Equal("unavailable", result.DigestIssue?.Category);
        Assert.Equal("Saved digests are temporarily unavailable.", result.DigestIssue?.Message);
        Assert.Equal("2026-10-23", result.Day);
        var noItems = new DailyService(journal, new DigestStore(box.Data)).Query(Monday() with { Repositories = ["unmatched"] });
        Assert.Empty(noItems.Today);
        Assert.Null(noItems.DigestIssue);
        var repeated = new DailyService(journal, new DigestStore(box.Data)).Query(Monday());
        Assert.Equal("unavailable", repeated.DigestIssue?.Category);
        Directory.Delete(Path.Combine(box.Data, "digests.db"));
        new DigestStore(box.Data).Save(new(Item(friday), "2026-10-23", [friday.Id], "Recovered digest.", DateTimeOffset.UtcNow));
        var recovered = new DailyService(journal, new DigestStore(box.Data)).Query(Monday());
        Assert.Null(recovered.DigestIssue);
        Assert.Equal("Recovered digest.", Assert.Single(recovered.Digests).Text);
    }

    [Fact]
    public async Task TheEndpointReturnsDigestsWithCoversAndText()
    {
        using var box = new TestWorkspace();
        var journal = Open(box);
        var f1 = journal.Log(new("in_progress", "c", Tickets: [Retry], Summary: "a", OccurredAt: "2026-10-23T08:00:00Z")).Entry;
        var f2 = journal.Log(new("done", "c", Tickets: [Retry], Summary: "b", OccurredAt: "2026-10-23T10:00:00Z")).Entry;
        new DigestStore(box.Data).Save(new(Item(f1), "2026-10-23", new[] { f1.Id, f2.Id }.Order(StringComparer.Ordinal).ToArray(), "Friday rollup.", DateTimeOffset.UtcNow));
        var authorization = new DashboardAuthorization(box.Data);
        var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "open", authorization, new DashboardTests.FixtureCommand(box.Repo));
        await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(DashboardHost.Address(app)) };
            Assert.True((await client.PostAsJsonAsync("/api/bootstrap", new
            {
                token = authorization.Bootstrap()
            })).IsSuccessStatusCode);
            using var response = JsonDocument.Parse(await (await client.PostAsJsonAsync("/api/daily", new
            {
                before = "2026-10-25T23:00:00Z",
                timezone = Zone
            })).Content.ReadAsStringAsync());
            var digest = Assert.Single(response.RootElement.GetProperty("digests").EnumerateArray());
            Assert.Equal("Friday rollup.", digest.GetProperty("text").GetString());
            Assert.Equal(2, digest.GetProperty("covers").GetArrayLength());
            Assert.True(!response.RootElement.TryGetProperty("digestIssue", out var issue) || issue.ValueKind == JsonValueKind.Null);
        }
        finally { await app.StopAsync(); await app.DisposeAsync(); }
    }
}
