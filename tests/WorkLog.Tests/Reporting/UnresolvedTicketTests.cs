using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Reporting;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Reporting;

public sealed class UnresolvedTicketTests
{
    private static readonly QueryRequest Period = new("2026-10-20T00:00:00Z", "2026-10-21T00:00:00Z", "UTC");

    [Fact]
    public void BareIdFindsHistoricalUnresolvedEntriesWithoutQualifyingThem()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        var journal = new Journal(settings, new("repo"));
        var request = new LogRequest("in_progress", "repair-links", "feature/DEP-7483-repair", OccurredAt: "2026-10-20T09:00:00Z");
        var unresolved = journal.Log(request).Entry;
        var qualified = journal.Log(request with { Tickets = [new("jira", "company", "DEP-7483")] }).Entry;
        settings.Configuration.RepositoryContexts["context"] = new(new("repo", "jira", "company"));

        var bare = journal.Query(Period with { Ticket = " DEP-7483 " });
        Assert.Equal(2, bare.Entries.Length);
        var historical = bare.Entries.Single(entry => entry.Id == unresolved.Id);
        Assert.Empty(historical.Tickets);
        Assert.Equal("DEP-7483", historical.ExtractedTicketId);
        var scoped = journal.Query(Period with { Ticket = "jira:company:DEP-7483" });
        Assert.Equal(qualified.Id, Assert.Single(scoped.Entries).Id);
        Assert.Empty(journal.Query(Period with { Ticket = "jira:other:DEP-7483" }).Entries);

        // A colon-containing Jira ID must not masquerade as somebody else's qualified reference.
        journal.Log(request with { Tickets = [new("jira", "other", "jira:company:DEP-7483")] });
        Assert.Equal(qualified.Id, Assert.Single(journal.Query(Period with { Ticket = "jira:company:DEP-7483" }).Entries).Id);
        Assert.Equal(unresolved.ExtractedTicketId, new Journal(new LocalSettings(box.Data)).Query(Period).Entries.Single(entry => entry.Id == unresolved.Id).ExtractedTicketId);
    }

    [Fact]
    public void UnresolvedMatchesObeyEveryFilterAndSnapshotBeforePagination()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        var request = new LogRequest("in_progress", "repair", "bugfix/DEP-12", OccurredAt: "2026-10-20T09:00:00Z");
        var expected = Enumerable.Range(0, 3).Select(_ => journal.Log(request).Entry.Id).ToHashSet();
        journal.Log(request with { Repository = "other" });
        journal.Log(request with { Change = "other" });
        journal.Log(request with { Status = "done" });
        journal.Log(request with { Classification = "other" });
        journal.Log(request with { OccurredAt = Period.To });
        journal.Log(request with { Branch = "bugfix/DEP-13" });
        var query = Period with { Ticket = "DEP-12", Repository = "repo", Change = "repair", Status = "in_progress", Classification = "bug", PageSize = 1 };
        var first = journal.Query(query);
        Assert.True(first.HasMore);
        journal.Log(request with { OccurredAt = "2026-10-20T08:00:00Z" });
        var ids = first.Entries.Select(entry => entry.Id).ToList();
        var page = first;
        while (page.HasMore)
        {
            page = journal.Query(query with { Cursor = page.NextCursor });
            ids.AddRange(page.Entries.Select(entry => entry.Id));
        }
        Assert.Equal(3, ids.Count);
        Assert.True(expected.SetEquals(ids));
        Assert.Equal(4, journal.Query(query with { PageSize = 100 }).Entries.Length);
    }
}
