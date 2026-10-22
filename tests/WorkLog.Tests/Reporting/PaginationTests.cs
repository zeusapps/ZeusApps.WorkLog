using System.Text.Json;
using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Reporting;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Reporting;

public sealed class PaginationTests
{
    private static readonly QueryRequest Period = new("2026-10-20T00:00:00Z", "2026-10-21T00:00:00Z", "UTC");
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("change")]
    [InlineData("ticket")]
    public void PagesPreserveTiesAndSnapshotAndApplyFiltersBeforeLimit(string groupBy)
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        for (var i = 0; i < 63; i++)
            journal.Log(new("in_progress", i % 2 == 0 ? "a" : "b", Tickets: [new("jira", "company", "DEP-12")], OccurredAt: "2026-10-20T09:00:00Z"));
        var request = Period with
        {
            GroupBy = groupBy
        };
        var first = journal.Query(request);
        if (groupBy == "ticket")
            Assert.Equal(WorkLog.Infrastructure.Json.Encode(first), WorkLog.Infrastructure.Json.Encode(journal.Query(Period)));
        Assert.Equal(1, first.SchemaVersion);
        Assert.Equal(25, first.Entries.Length);
        Assert.True(first.HasMore);
        Assert.All(first.Days, day =>
        {
            Assert.Empty(day.Observations);
            Assert.All(day.EntryIds, id => Assert.Contains(first.Entries, e => e.Id == id));
        });
        var later = journal.Log(new("done", "late", OccurredAt: "2026-10-20T08:00:00Z"));
        var entries = first.Entries.ToList();
        var page = first;
        while (page.HasMore)
        {
            var cursor = page.NextCursor;
            page = journal.Query(request with
            {
                Cursor = cursor
            });
            if (groupBy == "ticket")
                Assert.Equal(WorkLog.Infrastructure.Json.Encode(page), WorkLog.Infrastructure.Json.Encode(journal.Query(Period with
                {
                    Cursor = cursor
                })));
            entries.AddRange(page.Entries);
        }
        Assert.Null(page.NextCursor);
        Assert.Equal(63, entries.Select(e => e.Id).Distinct().Count());
        Assert.DoesNotContain(entries, e => e.Id == later.Entry.Id);
        Assert.Equal(entries.Select(e => e.Id).Order(StringComparer.Ordinal), entries.Select(e => e.Id));
        Assert.Throws<ArgumentException>(() => journal.Query(request with
        {
            Cursor = first.NextCursor,
            GroupBy = groupBy == "ticket" ? "change" : "ticket"
        }));
        Assert.Single(journal.Query(Period with
        {
            Status = "done",
            PageSize = 1
        }).Entries);
        Assert.Throws<ArgumentException>(() => journal.Query(Period with { Cursor = first.NextCursor, Change = "a" }));
        Assert.Throws<ArgumentException>(() => journal.Query(Period with { Cursor = "bad" }));
        Assert.Throws<ArgumentException>(() => journal.Query(Period with { PageSize = 0 }));
        Assert.Throws<ArgumentException>(() => journal.Query(Period with { PageSize = 101 }));
        Assert.Throws<ArgumentException>(() => journal.Query(Period with { View = "bad" }));
    }

    [Theory]
    [InlineData("compact", "change")]
    [InlineData("flat", "change")]
    [InlineData("grouped", "change")]
    [InlineData("legacy", "change")]
    [InlineData("compact", "ticket")]
    [InlineData("flat", "ticket")]
    [InlineData("grouped", "ticket")]
    [InlineData("legacy", "ticket")]
    public void ByteBoundedViewsCanTraverseAllObservations(string view, string groupBy)
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        for (var i = 0; i < 30; i++)
            journal.Log(new("in_progress", i % 2 == 0 ? "a" : null, Tickets: [new("jira", "company", "DEP-12")], Summary: new string('Ж', 2000), OccurredAt: "2026-10-20T09:00:00Z"));
        var request = Period with
        {
            PageSize = 100,
            View = view,
            GroupBy = groupBy
        };
        var ids = new List<string>();
        Report page;
        do
        {
            page = journal.Query(request);
            Assert.True(JsonSerializer.SerializeToUtf8Bytes(page, Wire).Length <= 64 * 1024);
            ids.AddRange(view == "grouped" ? page.Days.SelectMany(d => d.Observations).Select(e => e.Id) : page.Entries.Select(e => e.Id));
            request = request with
            {
                Cursor = page.NextCursor
            };
        } while (page.HasMore);
        Assert.Equal(30, ids.Count);
        Assert.Equal(30, ids.Distinct().Count());
        if (view == "flat")
            Assert.Empty(page.Days);
        if (view == "grouped")
            Assert.Empty(page.Entries);
    }

    [Fact]
    public void TicketGroupFragmentsRespectTimezoneFiltersAndInitialSnapshot()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        var activity = new LogRequest("in_progress", "repair", "bugfix/DEP-12",
            Tickets: [new("jira", "company", "DEP-12")], OccurredAt: "2026-10-20T21:59:00Z");
        var expected = Enumerable.Range(0, 5).Select(index => journal.Log(activity with
        {
            OccurredAt = index < 2 ? activity.OccurredAt : "2026-10-20T22:00:00Z"
        }).Entry.Id).ToHashSet();
        journal.Log(activity with
        {
            Repository = "other"
        });
        journal.Log(activity with
        {
            Change = "other"
        });
        journal.Log(activity with
        {
            Status = "done"
        });
        journal.Log(activity with
        {
            Classification = "other"
        });
        journal.Log(activity with
        {
            Tickets = [new("jira", "other", "DEP-12")]
        });
        journal.Log(activity with
        {
            OccurredAt = Period.To
        });
        var request = Period with
        {
            GroupBy = "ticket",
            Timezone = "Europe/Warsaw",
            Repository = "repo",
            Change = "repair",
            Ticket = "jira:company:DEP-12",
            Status = "in_progress",
            Classification = "bug",
            PageSize = 1
        };
        var page = journal.Query(request);
        var late = journal.Log(activity with
        {
            OccurredAt = "2026-10-20T20:00:00Z"
        }).Entry;
        var ids = new List<string>();
        var groups = new Dictionary<(string Date, string Identity), int>();
        while (true)
        {
            ids.AddRange(page.Entries.Select(entry => entry.Id));
            var group = Assert.Single(page.Days);
            var key = (group.Date, group.WorkIdentity);
            groups[key] = groups.GetValueOrDefault(key) + group.EntryIds.Length;
            if (!page.HasMore)
                break;
            page = journal.Query(request with
            {
                Cursor = page.NextCursor
            });
        }
        Assert.Equal(5, ids.Count);
        Assert.True(expected.SetEquals(ids));
        Assert.DoesNotContain(late.Id, ids);
        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups[("2026-10-20", "repo/ticket/jira:company:DEP-12")]);
        Assert.Equal(3, groups[("2026-10-21", "repo/ticket/jira:company:DEP-12")]);
        Assert.Equal(6, journal.Query(request with
        {
            PageSize = 100
        }).Entries.Length);
    }

    [Fact]
    public void OversizedSummaryCanBeExcludedWithoutAlteringJournal()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        var entry = journal.Log(new("done", "a", Summary: new string('x', 70000), OccurredAt: "2026-10-20T09:00:00Z"));
        Assert.Contains("includeSummaries=false", Assert.Throws<ArgumentException>(() => journal.Query(Period)).Message);
        var small = journal.Query(Period with
        {
            IncludeSummaries = false
        });
        Assert.Equal(entry.Entry.Id, Assert.Single(small.Entries).Id);
        Assert.Null(small.Entries[0].Summary);
        Assert.Throws<ArgumentException>(() => journal.Query(Period));
    }
}
