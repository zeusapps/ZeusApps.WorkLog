using WorkLog.Journaling;
using WorkLog.Persistence;
using WorkLog.Reporting;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Reporting;

public sealed class TicketFilterTests
{
    private static readonly QueryRequest Period = new("2026-10-20T00:00:00Z", "2026-10-21T00:00:00Z", "UTC", PageSize: 100);

    private static Entry Make(string id, Ticket[] tickets, string? extracted = null) => new(id, "repo", null, null, tickets, extracted, "in_progress",
        "unclassified", null, DateTimeOffset.Parse("2026-10-20T09:00:00Z"), DateTimeOffset.Parse("2026-10-20T09:00:00Z"), null, null);

    [Fact]
    public void MatchesTheSameEntriesAsTheQueryWorkSqlFilter()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(box.Data);
        var entries = new[]
        {
            Make("bare", [new("jira", "company", "DEP-12")]),
            Make("padded", [new(" jira ", " company ", "  DEP-12 ")]),
            Make("other-site", [new("jira", "other", "DEP-12")]),
            Make("azure", [new("azure-devops", "contoso/Payments", "4211")]),
            Make("unresolved", [], "DEP-12"),
            Make("padded-unresolved", [], " DEP-12 "),
            Make("colon-id", [new("jira", "other", "jira:company:DEP-12")]),
            Make("none", []),
            Make("both", [new("jira", "company", "DEP-13")], "DEP-12")
        };
        var store = new SqliteEntryStore(new JournalDatabase(box.Data));
        foreach (var entry in entries)
            store.Append(new PreparedEntry(entry, null, entry.Id, entry.Id));
        var period = ReportingPeriod.FromRequest(Period);

        foreach (var filter in new[]
        {
            "DEP-12", " DEP-12 ", "DEP-13", "4211", "DEP-99", "jira:company:DEP-12", "jira:other:DEP-12", "jira:company:DEP-13",
            "azure-devops:contoso/Payments:4211", "azure-devops:contoso:4211", "jira:company:", "jira:company:DEP-12:x"
        })
        {
            var request = Period with
            {
                Ticket = filter.Trim()
            };
            var sql = store.ReadPage(request, period, null).Entries.Select(e => e.Id).Order().ToArray();
            var memory = entries.Where(e => TicketFilter.Matches(e, filter)).Select(e => e.Id).Order().ToArray();
            Assert.Equal(sql, memory);
        }

        Assert.Equal(["bare", "both", "other-site", "padded", "unresolved"], entries.Where(e => TicketFilter.Matches(e, "DEP-12")).Select(e => e.Id).Order().ToArray());
        Assert.Equal(["bare", "padded"], entries.Where(e => TicketFilter.Matches(e, "jira:company:DEP-12")).Select(e => e.Id).Order().ToArray());
        Assert.DoesNotContain(entries, e => e.Id == "other-site" && TicketFilter.Matches(e, "jira:company:DEP-12"));
    }
}
