using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Reporting;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Reporting;

public sealed class ReportTests
{
    private static QueryRequest Period(string? status = null) => new("2026-10-20T00:00:00+02:00", "2026-10-25T00:00:00+02:00", "Europe/Warsaw", Status: status);

    [Fact]
    public void ReportingPreservesWorkSwitchesAndAppliesEveryFilter()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        void Log(string status, string change, string at) => journal.Log(new(status, change, "feature/DEP-12-test",
            Tickets: [new("jira", "company", "DEP-12")], OccurredAt: at));
        Log("started", "Y", "2026-10-20T00:00:00+02:00");
        Log("in_progress", "Y", "2026-10-21T10:00:00+02:00");
        Log("started", "Z", "2026-10-21T12:00:00+02:00");
        Log("done", "Z", "2026-10-22T12:00:00+02:00");
        Log("in_progress", "Y", "2026-10-23T12:00:00+02:00");
        Log("done", "Y", "2026-10-25T00:00:00+02:00");
        var report = journal.Query(Period() with
        {
            GroupBy = "change"
        });
        Assert.Equal(5, report.Entries.Length);
        Assert.DoesNotContain(report.Days, g => g.Date == "2026-10-22" && g.WorkIdentity.EndsWith("/Y"));
        Assert.Equal(2, report.Days.Count(group => group.Date == "2026-10-21"));
        var defaultDay = Assert.Single(journal.Query(Period()).Days, group => group.Date == "2026-10-21");
        Assert.EndsWith("/ticket/jira:company:DEP-12", defaultDay.WorkIdentity);
        Assert.Equal(2, defaultDay.EntryIds.Length);
        Assert.Equal(5, journal.Query(Period() with
        {
            Repository = "repo",
            Ticket = "DEP-12",
            Classification = "feature"
        }).Entries.Length);
        Assert.Single(journal.Query(Period("done") with
        {
            Change = "Z"
        }).Entries);
        Assert.Empty(journal.Query(Period() with
        {
            Repository = "other"
        }).Entries);
        Assert.Empty(journal.Query(Period() with
        {
            Ticket = "missing"
        }).Entries);
        Assert.Empty(journal.Query(Period() with
        {
            Classification = "bug"
        }).Entries);
        Assert.Throws<ArgumentException>(() => journal.Query(Period() with { To = Period().From }));
        Assert.Throws<ArgumentException>(() => journal.Query(Period() with { Timezone = "unknown" }));
    }

    [Fact]
    public void WarsawDaylightSavingAndMultipleDailyStatuses()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        journal.Log(new("started", "a", OccurredAt: "2026-10-24T22:30:00Z"));
        journal.Log(new("done", "a", OccurredAt: "2026-10-25T23:30:00Z"));
        journal.Log(new("in_progress", "a", OccurredAt: "2026-10-26T00:00:00Z"));
        var report = journal.Query(new("2026-10-24T00:00:00Z", "2026-10-27T00:00:00Z", "Europe/Warsaw"));
        Assert.Equal(new[] { "2026-10-25", "2026-10-26" }, report.Days.Select(g => g.Date));
        Assert.Equal(new[] { "done", "in_progress" }, report.Days[1].EntryIds.Select(id => report.Entries.Single(e => e.Id == id).Status));
    }
}
