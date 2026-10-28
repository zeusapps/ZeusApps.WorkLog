using WorkLog.Configuration;
using WorkLog.Dashboard;
using WorkLog.Journaling;
using WorkLog.Tests.Testing;
using Xunit;
using JournalJson = WorkLog.Infrastructure.Json;

namespace WorkLog.Tests.Dashboard;

public sealed class TimelineTicketFilterTests
{
    [Fact]
    public void TicketFilterSelectsWorkItemsAndKeepsTheirFullLifecycle()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        var company = new[] { new Ticket("jira", "company", "DEP-12") };
        journal.Log(new("started", Tickets: company, OccurredAt: "2026-10-01T10:00:00Z"));
        journal.Log(new("in_progress", Tickets: company, OccurredAt: "2026-10-22T10:00:00Z"));
        journal.Log(new("in_progress", Tickets: [new("jira", "other", "DEP-12")], OccurredAt: "2026-10-22T11:00:00Z"));
        journal.Log(new("in_progress", Tickets: [new("jira", "company", "DEP-13")], OccurredAt: "2026-10-22T12:00:00Z"));
        journal.Log(new("in_progress", "branch-only", "feature/DEP-12-x", OccurredAt: "2026-10-22T13:00:00Z"));
        var service = new TimelineService(new DashboardJournal(box.Data));
        var query = new TimelineQuery("2026-10-20T00:00:00Z", "2026-10-26T00:00:00Z", "UTC");

        var all = service.Query(query).Rows;
        var bare = service.Query(query with
        {
            Ticket = " DEP-12 "
        }).Rows;
        Assert.Equal(3, bare.Length);
        Assert.DoesNotContain(bare, row => row.Title == "DEP-13");

        var qualified = Assert.Single(service.Query(query with
        {
            Ticket = "jira:company:DEP-12"
        }).Rows);
        var unfiltered = Assert.Single(all, row => row.Id == qualified.Id);
        Assert.Equal(JournalJson.Encode(unfiltered), JournalJson.Encode(qualified));
        Assert.Equal(2, qualified.ObservationCount);
        Assert.Equal(1, qualified.VisibleCount);
        Assert.True(qualified.ClippedStart);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T10:00:00Z"), qualified.FirstAt);
        Assert.Equal(unfiltered.ElapsedSeconds, qualified.ElapsedSeconds);

        Assert.Empty(service.Query(query with
        {
            Ticket = "jira:missing:DEP-12"
        }).Rows);
        Assert.Equal(all.Length, service.Query(query with
        {
            Ticket = "  "
        }).Rows.Length);
        var observations = service.Observations(query with
        {
            Ticket = "jira:company:DEP-12"
        }, qualified.Id, 0);
        Assert.Equal(2, observations.Total);
    }
}
