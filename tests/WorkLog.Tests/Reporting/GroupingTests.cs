using WorkLog.Configuration;
using JournalJson = WorkLog.Infrastructure.Json;
using WorkLog.Journaling;
using WorkLog.Reporting;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Reporting;

public sealed class GroupingTests
{
    private static readonly QueryRequest Period = new("2026-10-20T00:00:00Z", "2026-10-21T00:00:00Z", "UTC");

    [Theory]
    [InlineData("compact")]
    [InlineData("flat")]
    [InlineData("grouped")]
    [InlineData("legacy")]
    public void TicketModeCombinesHistoricalAndDifferentChangesWithoutChangingObservations(string view)
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        var request = new LogRequest("in_progress", Tickets: [new("jira", "company", "DEP-12")],
            OccurredAt: "2026-10-20T09:00:00Z");
        var historical = journal.Log(request).Entry;
        var repair = journal.Log(request with
        {
            Change = "repair-links",
            Status = "done"
        }).Entry;
        var followup = journal.Log(request with
        {
            Change = "followup"
        }).Entry;
        var expected = new[] { historical, repair, followup }.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToArray();

        var original = journal.Query(Period with
        {
            View = view
        });
        var explicitChange = journal.Query(Period with
        {
            View = view,
            GroupBy = "change"
        });
        var ticket = journal.Query(Period with
        {
            View = view,
            GroupBy = "ticket"
        });
        Assert.Equal(JournalJson.Encode(original), JournalJson.Encode(ticket));
        Assert.Equal(JournalJson.Encode(expected), JournalJson.Encode(Observations(ticket).ToArray()));
        Assert.Equal(1, ticket.SchemaVersion);
        if (view == "flat")
        {
            Assert.Empty(ticket.Days);
            Assert.Equal(JournalJson.Encode(original), JournalJson.Encode(ticket));
        }
        else
        {
            Assert.Equal(new[] { "repo/change/followup", "repo/change/repair-links", "repo/ticket/jira:company:DEP-12" },
                explicitChange.Days.Select(day => day.WorkIdentity).Order(StringComparer.Ordinal));
            var group = Assert.Single(ticket.Days);
            Assert.Equal("repo/ticket/jira:company:DEP-12", group.WorkIdentity);
            var ids = view == "compact" ? group.EntryIds : group.Observations.Select(entry => entry.Id).ToArray();
            Assert.Equal(expected.Select(entry => entry.Id), ids);
            Assert.Equal(3, ids.Distinct().Count());
        }
        Assert.Equal(JournalJson.Encode(original), JournalJson.Encode(journal.Query(Period with
        {
            View = view
        })));
    }

    [Fact]
    public void CompleteTicketSetsAreSortedDistinctAndNeverDuplicatedIntoOverlappingGroups()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        Ticket a = new("jira", "company", "A");
        Ticket b = new("jira", "company", "B");
        Ticket c = new("jira", "company", "C");
        Entry Log(Ticket[] tickets, string? change) => journal.Log(new("in_progress", change,
            Tickets: tickets, OccurredAt: "2026-10-20T09:00:00Z")).Entry;
        var ab = Log([a, b], "first");
        var baa = Log([b, a with { Provider = " jira ", Scope = " company ", Id = " A " }, a], "second");
        var onlyA = Log([a], null);
        var ac = Log([a, c], "third");
        var report = journal.Query(Period with
        {
            GroupBy = "ticket"
        });
        Assert.Equal(3, report.Days.Length);
        var set = Assert.Single(report.Days, day => day.EntryIds.Contains(ab.Id));
        Assert.Equal("repo/tickets/jira%3Acompany%3AA|jira%3Acompany%3AB", set.WorkIdentity);
        Assert.Contains(baa.Id, set.EntryIds);
        Assert.Single(report.Days, day => day.EntryIds.SequenceEqual(new[] { onlyA.Id }));
        Assert.Single(report.Days, day => day.EntryIds.SequenceEqual(new[] { ac.Id }));
        Assert.Equal(4, report.Days.SelectMany(day => day.EntryIds).Distinct().Count());
        Assert.Equal(4, report.Days.Sum(day => day.EntryIds.Length));
        // Filtering by A selects observations before grouping; it does not erase B or C.
        Assert.Equal(JournalJson.Encode(report), JournalJson.Encode(journal.Query(Period with
        {
            GroupBy = "ticket",
            Ticket = "A"
        })));
    }

    [Fact]
    public void TicketIdentityKeepsRepositoryProviderAndScopeSeparate()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        var request = new LogRequest("in_progress", "same-change", Tickets: [new("jira", "company", "12")],
            OccurredAt: "2026-10-20T09:00:00Z");
        journal.Log(request);
        journal.Log(request with
        {
            Repository = "other"
        });
        journal.Log(request with
        {
            Tickets = [new("jira", "other", "12")]
        });
        journal.Log(request with
        {
            Tickets = [new("azure-devops", "company", "12")]
        });
        var report = journal.Query(Period with
        {
            GroupBy = "ticket"
        });
        Assert.Equal(4, report.Days.Length);
        Assert.Equal(4, report.Days.Select(day => day.WorkIdentity).Distinct().Count());
        Assert.All(report.Days, day => Assert.Single(day.EntryIds));
    }

    [Fact]
    public void MissingQualifiedTicketsRetainChangeIdentityEvenAfterContextIsConfigured()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        var journal = new Journal(settings, new("repo"));
        var request = new LogRequest("in_progress", "repair-links", "bugfix/DEP-12",
            OccurredAt: "2026-10-20T09:00:00Z");
        var unresolved = journal.Log(request).Entry;
        var noTicket = journal.Log(request with
        {
            Branch = null
        }).Entry;
        var qualified = journal.Log(request with
        {
            Tickets = [new("jira", "company", "DEP-12")]
        }).Entry;
        settings.Configuration.RepositoryContexts["context"] = new(new("repo", "jira", "company"));
        var report = journal.Query(Period with
        {
            GroupBy = "ticket"
        });
        Assert.Equal(2, report.Days.Length);
        var fallback = Assert.Single(report.Days, day => day.WorkIdentity == "repo/change/repair-links");
        Assert.Equal(new[] { unresolved.Id, noTicket.Id }.Order(StringComparer.Ordinal), fallback.EntryIds);
        Assert.Equal(qualified.Id, Assert.Single(Assert.Single(report.Days,
            day => day.WorkIdentity == "repo/ticket/jira:company:DEP-12").EntryIds));
        var stored = Assert.Single(report.Entries, entry => entry.Id == unresolved.Id);
        Assert.Empty(stored.Tickets);
        Assert.Equal("DEP-12", stored.ExtractedTicketId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("tickets")]
    [InlineData("Ticket")]
    [InlineData(" ticket ")]
    public void InvalidGroupingIsRejectedEvenInFlatView(string? groupBy)
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        Assert.Contains("period.groupBy", Assert.Throws<ArgumentException>(() =>
            journal.Query(Period with
            {
                GroupBy = groupBy!,
                View = "flat"
            })).Message);
        Assert.Empty(journal.Query(Period).Entries);
    }

    private static IEnumerable<Entry> Observations(Report report) => report.View == "grouped"
        ? report.Days.SelectMany(day => day.Observations).OrderBy(entry => entry.Id, StringComparer.Ordinal)
        : report.Entries;
}
