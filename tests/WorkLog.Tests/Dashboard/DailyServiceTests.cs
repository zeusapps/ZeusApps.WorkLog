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

public sealed class DailyServiceTests
{
    private const string Zone = "Europe/Warsaw";
    private static readonly Ticket Retry = new("jira", "company", "DEP-1232");
    private static readonly Ticket Export = new("jira", "company", "DEP-1240");

    private static Journal Open(TestWorkspace box, string repository = "repo") => new(new LocalSettings(box.Data), new(repository));

    /// <summary>Journal entries with neither ticket nor change are only possible in older journals, so tests insert them directly.</summary>
    private static void Legacy(TestWorkspace box, string status, string? summary, string occurredAt, string repository = "repo")
    {
        var at = DateTimeOffset.Parse(occurredAt);
        var entry = new Entry(Guid.NewGuid().ToString("N"), repository, null, null, [], null, status, "unclassified", summary, at, at, "test", null);
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

    // Warsaw is UTC+1 on 26 October 2026 (the clocks went back on the 25th) and UTC+2 before.
    private static DailyRequest Monday(string[]? repositories = null, string? day = null) => new("2026-10-25T23:00:00Z", Zone, repositories, day);

    [Fact]
    public void MondayShowsFridayAndSteppingBackSkipsEmptyDays()
    {
        using var box = new TestWorkspace();
        var journal = Open(box);
        journal.Log(new("in_progress", "old", Summary: "tuesday work", OccurredAt: "2026-10-20T10:00:00Z"));
        journal.Log(new("in_progress", "old", Summary: "thursday work", OccurredAt: "2026-10-22T10:00:00Z"));
        journal.Log(new("done", "old", Summary: "friday work", OccurredAt: "2026-10-23T10:00:00Z"));
        var service = new DailyService(new DashboardJournal(box.Data));

        var friday = service.Query(Monday());
        Assert.Equal("2026-10-23", friday.Day);
        Assert.Equal("2026-10-22", friday.PreviousDay);
        Assert.Null(friday.NextDay);
        Assert.Equal("friday work", Assert.Single(friday.Items).Primary.Summary);

        var thursday = service.Query(Monday(day: friday.PreviousDay));
        Assert.Equal("2026-10-22", thursday.Day);
        Assert.Equal("2026-10-20", thursday.PreviousDay);
        Assert.Equal("2026-10-23", thursday.NextDay);

        var tuesday = service.Query(Monday(day: thursday.PreviousDay));
        Assert.Equal("2026-10-20", tuesday.Day);
        Assert.Null(tuesday.PreviousDay);
        Assert.Equal("2026-10-22", tuesday.NextDay);
        Assert.Equal("tuesday work", Assert.Single(tuesday.Items).Primary.Summary);
    }

    [Fact]
    public void DaysOffLeaveTheLastRecordedDayOnTuesdayWhenOpenedOnFriday()
    {
        using var box = new TestWorkspace();
        var journal = Open(box);
        journal.Log(new("in_progress", "old", Summary: "monday", OccurredAt: "2026-10-19T10:00:00Z"));
        journal.Log(new("in_progress", "old", Summary: "tuesday", OccurredAt: "2026-10-20T10:00:00Z"));
        var service = new DailyService(new DashboardJournal(box.Data));

        // Friday 23 October 2026, 00:00 in Warsaw (UTC+2).
        var digest = service.Query(new("2026-10-22T22:00:00Z", Zone));

        Assert.Equal("2026-10-20", digest.Day);
        Assert.Equal("2026-10-19", digest.PreviousDay);
        Assert.Null(digest.NextDay);
    }

    [Fact]
    public void DayBoundariesFollowTheSelectedTimezone()
    {
        using var box = new TestWorkspace();
        var journal = Open(box);
        // 22:30Z on the 22nd is 00:30 on the 23rd in Warsaw but still the 22nd in UTC.
        journal.Log(new("in_progress", "late", Summary: "after local midnight", OccurredAt: "2026-10-22T22:30:00Z"));
        journal.Log(new("in_progress", "early", Summary: "before local midnight", OccurredAt: "2026-10-22T21:59:59Z"));
        journal.Log(new("in_progress", "next", Summary: "next local day", OccurredAt: "2026-10-23T22:00:00Z"));
        var service = new DailyService(new DashboardJournal(box.Data));

        var warsaw = service.Query(Monday(day: "2026-10-23"));
        Assert.Equal("after local midnight", Assert.Single(warsaw.Items).Primary.Summary);
        Assert.Equal("2026-10-22", warsaw.PreviousDay);
        Assert.Equal("2026-10-24", warsaw.NextDay);

        var utc = service.Query(new("2026-10-26T00:00:00Z", "UTC", null, "2026-10-22"));
        Assert.Equal(new[] { "before local midnight", "after local midnight" }.Order(), utc.Items.Select(i => i.Primary.Summary!).Order());
    }

    [Fact]
    public void RepositorySelectionFiltersTheLastRecordedDayAndItsNeighbours()
    {
        using var box = new TestWorkspace();
        Open(box, "alpha").Log(new("in_progress", "a", Summary: "alpha", OccurredAt: "2026-10-20T10:00:00Z"));
        Open(box, "beta").Log(new("in_progress", "b", Summary: "beta", OccurredAt: "2026-10-23T10:00:00Z"));
        var service = new DailyService(new DashboardJournal(box.Data));

        Assert.Equal("2026-10-20", service.Query(Monday(["alpha"])).Day);
        Assert.Equal("2026-10-23", service.Query(Monday(["beta"])).Day);
        Assert.Null(service.Query(Monday(["beta"])).PreviousDay);
        var all = service.Query(Monday());
        Assert.Equal("2026-10-23", all.Day);
        Assert.Equal("2026-10-20", all.PreviousDay);
        Assert.Equal("beta", Assert.Single(all.Items).Repository);
        Assert.Equal("alpha", Assert.Single(service.Query(Monday(day: "2026-10-20")).Items).Repository);
        Assert.Null(service.Query(Monday(["missing"])).Day);
    }

    [Fact]
    public void ItemsFromTwoRepositoriesOnOneDayEachIdentifyTheirRepository()
    {
        using var box = new TestWorkspace();
        Open(box, "alpha").Log(new("in_progress", "a", Summary: "alpha", OccurredAt: "2026-10-23T10:00:00Z"));
        Open(box, "beta").Log(new("in_progress", "b", Summary: "beta", OccurredAt: "2026-10-23T11:00:00Z"));
        var service = new DailyService(new DashboardJournal(box.Data));

        var items = service.Query(Monday()).Items;

        Assert.Equal(new[] { "beta", "alpha" }, items.Select(i => i.Repository));
    }

    [Fact]
    public void AnEmptyJournalHasNoDayAndNoItems()
    {
        using var box = new TestWorkspace();
        var service = new DailyService(new DashboardJournal(box.Data));

        var digest = service.Query(Monday());

        Assert.Null(digest.Day);
        Assert.Null(digest.PreviousDay);
        Assert.Null(digest.NextDay);
        Assert.Empty(digest.Items);
        Assert.Empty(digest.Today);
    }

    [Fact]
    public void OnlyTodayHasObservationsWhenNothingEarlierExists()
    {
        using var box = new TestWorkspace();
        Open(box).Log(new("in_progress", "now", Summary: "only today", OccurredAt: "2026-10-26T08:00:00Z"));
        var service = new DailyService(new DashboardJournal(box.Data));

        var digest = service.Query(Monday());

        Assert.Null(digest.Day);
        Assert.Empty(digest.Items);
        Assert.Equal("only today", Assert.Single(digest.Today).Primary.Summary);
    }

    [Fact]
    public void StatusIsAsOfTheEndOfTheSelectedDayAndItemsAreOrdered()
    {
        using var box = new TestWorkspace();
        var journal = Open(box);
        journal.Log(new("started", "later-done", Tickets: [Retry], Summary: "begin", OccurredAt: "2026-10-22T08:00:00Z"));
        journal.Log(new("in_progress", "later-done", Tickets: [Retry], Summary: "evening state", OccurredAt: "2026-10-22T16:00:00Z"));
        journal.Log(new("done", "later-done", Tickets: [Retry], Summary: "finished next day", OccurredAt: "2026-10-23T09:00:00Z"));
        journal.Log(new("done", "ship", Tickets: [Export], Summary: "shipped early", OccurredAt: "2026-10-22T07:00:00Z"));
        journal.Log(new("started", "just-started", Summary: "kickoff", OccurredAt: "2026-10-22T18:00:00Z"));
        journal.Log(new("done", "ship-two", Summary: "newer done", OccurredAt: "2026-10-22T12:00:00Z"));
        journal.Log(new("in_progress", "aaa", Summary: "same time a", OccurredAt: "2026-10-22T17:00:00Z"));
        journal.Log(new("in_progress", "zzz", Summary: "same time z", OccurredAt: "2026-10-22T17:00:00Z"));
        var service = new DailyService(new DashboardJournal(box.Data));

        var digest = service.Query(Monday(day: "2026-10-22"));

        Assert.Equal(new[] { "ship-two", "DEP-1240", "aaa", "zzz", "DEP-1232", "just-started" }, digest.Items.Select(i => i.Title));
        Assert.Equal(new[] { "done", "done", "in_progress", "in_progress", "in_progress", "started" }, digest.Items.Select(i => i.Status));
        var retry = digest.Items.Single(i => i.Title == "DEP-1232");
        Assert.Equal("evening state", retry.Primary.Summary);
        Assert.Equal(new[] { "begin" }, retry.Others.Select(e => e.Summary));
        Assert.Equal(new[] { "later-done" }, retry.Changes);
        Assert.Equal("DEP-1232", Assert.Single(retry.Tickets).Id);
        Assert.Equal("repo", retry.Repository);
    }

    [Fact]
    public void SeveralObservationsOfOneTicketCollapseIntoOneLineWithTheLatestPrimary()
    {
        using var box = new TestWorkspace();
        var journal = Open(box);
        journal.Log(new("started", "c", Tickets: [Retry], Summary: "one", OccurredAt: "2026-10-22T08:00:00Z"));
        journal.Log(new("in_progress", "c", Tickets: [Retry], Summary: "two", OccurredAt: "2026-10-22T10:00:00Z"));
        journal.Log(new("done", "c", Tickets: [Retry], Summary: "three", OccurredAt: "2026-10-22T12:00:00Z"));
        var service = new DailyService(new DashboardJournal(box.Data));

        var item = Assert.Single(service.Query(Monday()).Items);

        Assert.Equal("done", item.Status);
        Assert.Equal("three", item.Primary.Summary);
        Assert.Equal(new[] { "two", "one" }, item.Others.Select(e => e.Summary));
    }

    [Fact]
    public void UnlinkedEntriesBecomeOneItemEachTitledBySummary()
    {
        using var box = new TestWorkspace();
        Open(box).Log(new("in_progress", "seed", Summary: "linked", OccurredAt: "2026-10-01T10:00:00Z"));
        Legacy(box, "in_progress", "  first loose  ", "2026-10-22T09:00:00Z");
        Legacy(box, "done", "second loose", "2026-10-22T11:00:00Z");
        Legacy(box, "started", null, "2026-10-22T12:00:00Z");
        var service = new DailyService(new DashboardJournal(box.Data));

        var items = service.Query(Monday()).Items;

        Assert.Equal(3, items.Length);
        Assert.All(items, item =>
        {
            Assert.Equal("adhoc:" + item.Primary.Id, item.Id);
            Assert.Empty(item.Others);
            Assert.Empty(item.Tickets);
            Assert.Empty(item.Changes);
        });
        Assert.Equal("second loose", items[0].Title);
        Assert.Equal("done", items[0].Status);
        Assert.Equal("first loose", items[1].Title);
        Assert.Equal("", items[2].Title);
        Assert.Equal("started", items[2].Status);
        Assert.Null(items[2].Primary.Summary);
    }

    [Fact]
    public void AMissingSummaryIsKeptAsNull()
    {
        using var box = new TestWorkspace();
        Open(box).Log(new("in_progress", "c", Tickets: [Retry], OccurredAt: "2026-10-22T10:00:00Z"));
        var service = new DailyService(new DashboardJournal(box.Data));

        Assert.Null(Assert.Single(service.Query(Monday()).Items).Primary.Summary);
    }

    [Fact]
    public void AnExplicitDayThatIsNotEarlierThanTodayIsRejected()
    {
        using var box = new TestWorkspace();
        Open(box).Log(new("in_progress", "c", Summary: "x", OccurredAt: "2026-10-22T10:00:00Z"));
        var service = new DailyService(new DashboardJournal(box.Data));

        Assert.Throws<ArgumentException>(() => service.Query(Monday(day: "2026-10-26")));
        Assert.Throws<ArgumentException>(() => service.Query(Monday(day: "2026-10-27")));
        Assert.Throws<ArgumentException>(() => service.Query(Monday(day: "22 Oct")));
        Assert.Throws<ArgumentException>(() => service.Query(new("2026-10-25T23:00:00Z", "Not/AZone")));
        Assert.Throws<ArgumentException>(() => service.Query(new("2026-10-25 23:00", Zone)));
        Assert.Equal("2026-10-25", service.Query(Monday(day: "2026-10-25")).Day);
    }

    [Fact]
    public void TodayHoldsTheLatestSummaryPerItemAndIsUnaffectedByTheSelectedDay()
    {
        using var box = new TestWorkspace();
        var journal = Open(box);
        journal.Log(new("done", "c", Tickets: [Retry], Summary: "yesterday done", OccurredAt: "2026-10-23T10:00:00Z"));
        journal.Log(new("in_progress", "c", Tickets: [Retry], Summary: "today first", OccurredAt: "2026-10-26T06:00:00Z"));
        journal.Log(new("in_progress", "c", Tickets: [Retry], Summary: "today latest", OccurredAt: "2026-10-26T09:00:00Z"));
        journal.Log(new("done", "other", Tickets: [Export], Summary: "ready", OccurredAt: "2026-10-26T07:00:00Z"));
        journal.Log(new("in_progress", "tomorrow", Summary: "after the day", OccurredAt: "2026-10-26T23:30:00Z"));
        journal.Log(new("in_progress", "earlier", Tickets: [Retry], Summary: "oldest", OccurredAt: "2026-10-21T10:00:00Z"));
        var service = new DailyService(new DashboardJournal(box.Data));

        var latest = service.Query(Monday());
        var earlier = service.Query(Monday(day: "2026-10-21"));

        Assert.Equal(new[] { "DEP-1240", "DEP-1232" }, latest.Today.Select(i => i.Title));
        var retry = latest.Today.Single(i => i.Title == "DEP-1232");
        Assert.Equal("today latest", retry.Primary.Summary);
        Assert.Equal("in_progress", retry.Status);
        Assert.Equal(new[] { "today first" }, retry.Others.Select(e => e.Summary));
        Assert.Equal("2026-10-21", earlier.Day);
        Assert.Equal(latest.Today.Select(i => (i.Id, i.Primary.Id)), earlier.Today.Select(i => (i.Id, i.Primary.Id)));
        Assert.Equal("yesterday done", Assert.Single(latest.Items).Primary.Summary);
        Assert.Equal("oldest", Assert.Single(earlier.Items).Primary.Summary);
    }

    [Fact]
    public void TodayStartsAtTheLocalMidnightThatBeforeNames()
    {
        using var box = new TestWorkspace();
        var journal = Open(box);
        journal.Log(new("in_progress", "c", Summary: "last second of Sunday", OccurredAt: "2026-10-25T22:59:59Z"));
        journal.Log(new("in_progress", "c", Summary: "first second of Monday", OccurredAt: "2026-10-25T23:00:00Z"));
        var service = new DailyService(new DashboardJournal(box.Data));

        var digest = service.Query(Monday());

        Assert.Equal("last second of Sunday", Assert.Single(digest.Items).Primary.Summary);
        Assert.Equal("first second of Monday", Assert.Single(digest.Today).Primary.Summary);
    }

    [Fact]
    public void TheDayLookupsHonourSnapshotsAndBounds()
    {
        using var box = new TestWorkspace();
        Open(box).Log(new("in_progress", "c", Summary: "x", OccurredAt: "2026-10-22T10:00:00Z"));
        var journal = new DashboardJournal(box.Data);
        var snapshot = journal.Snapshot();
        Open(box).Log(new("in_progress", "c", Summary: "y", OccurredAt: "2026-10-23T10:00:00Z"));
        var limit = DateTimeOffset.Parse("2026-10-24T00:00:00Z");

        Assert.Equal(DateTimeOffset.Parse("2026-10-23T10:00:00Z"), journal.LatestBefore(limit, []));
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T10:00:00Z"), journal.LatestBefore(limit, [], snapshot));
        Assert.Null(journal.LatestBefore(DateTimeOffset.Parse("2026-10-22T10:00:00Z"), []));
        Assert.Equal(DateTimeOffset.Parse("2026-10-23T10:00:00Z"), journal.EarliestBetween(DateTimeOffset.Parse("2026-10-23T00:00:00Z"), limit, []));
        Assert.Null(journal.EarliestBetween(DateTimeOffset.Parse("2026-10-23T00:00:00Z"), limit, [], snapshot));
        Assert.Null(journal.EarliestBetween(DateTimeOffset.Parse("2026-10-23T10:00:01Z"), limit, []));
        Assert.Null(new DashboardJournal(Path.Combine(box.Root, "missing")).LatestBefore(limit, []));
    }

    [Fact]
    public void AClockChangeThatSkipsMidnightStartsTheDayAtTheFirstValidTime()
    {
        // Egypt moves its clocks from 00:00 to 01:00 on the last Friday of April, so 24 April 2026 has no local midnight.
        var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

        var start = DailyService.Midnight(new DateOnly(2026, 4, 24), cairo);

        Assert.Equal(new DateTime(2026, 4, 24, 1, 0, 0), TimeZoneInfo.ConvertTime(start, cairo).DateTime);
        Assert.Equal(new DateTime(2026, 4, 23), TimeZoneInfo.ConvertTime(start - TimeSpan.FromSeconds(1), cairo).Date);
        Assert.Equal(new DateTime(2026, 6, 1), TimeZoneInfo.ConvertTime(DailyService.Midnight(new DateOnly(2026, 6, 1), cairo), cairo).DateTime);
    }

    [Fact]
    public async Task TheEndpointRequiresAuthorizationAndRejectsAnInvalidDay()
    {
        using var box = new TestWorkspace();
        Open(box).Log(new("done", "c", Tickets: [Retry], Summary: "friday", OccurredAt: "2026-10-23T10:00:00Z"));
        var authorization = new DashboardAuthorization(box.Data);
        var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "open", authorization, new DashboardTests.FixtureCommand(box.Repo));
        await app.StartAsync();
        try
        {
            var address = new Uri(DashboardHost.Address(app));
            var body = new
            {
                before = "2026-10-25T23:00:00Z",
                timezone = Zone,
                repositories = new[] { "repo" }
            };
            using (var anonymous = new HttpClient { BaseAddress = address })
                Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/daily", body)).StatusCode);
            using var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = address };
            Assert.True((await client.PostAsJsonAsync("/api/bootstrap", new
            {
                token = authorization.Bootstrap()
            })).IsSuccessStatusCode);

            using var digest = JsonDocument.Parse(await (await client.PostAsJsonAsync("/api/daily", body)).Content.ReadAsStringAsync());
            Assert.Equal("2026-10-23", digest.RootElement.GetProperty("day").GetString());
            Assert.Equal(JsonValueKind.Null, digest.RootElement.GetProperty("previousDay").ValueKind);
            var item = Assert.Single(digest.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal("friday", item.GetProperty("primary").GetProperty("summary").GetString());
            Assert.Equal(0, item.GetProperty("others").GetArrayLength());
            Assert.Equal(0, digest.RootElement.GetProperty("today").GetArrayLength());
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/daily", new
            {
                before = body.before,
                timezone = Zone,
                day = "2026-10-26"
            })).StatusCode);
        }
        finally { await app.StopAsync(); await app.DisposeAsync(); }
    }
}
