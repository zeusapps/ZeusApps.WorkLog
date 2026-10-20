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

    [Fact]
    public void PagesPreserveTiesAndSnapshotAndApplyFiltersBeforeLimit()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        for (var i = 0; i < 63; i++)
            journal.Log(new("in_progress", i % 2 == 0 ? "a" : "b", OccurredAt: "2026-10-20T09:00:00Z"));
        var first = journal.Query(Period);
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
            page = journal.Query(Period with
            {
                Cursor = page.NextCursor
            });
            entries.AddRange(page.Entries);
        }
        Assert.Null(page.NextCursor);
        Assert.Equal(63, entries.Select(e => e.Id).Distinct().Count());
        Assert.DoesNotContain(entries, e => e.Id == later.Entry.Id);
        Assert.Equal(entries.Select(e => e.Id).Order(StringComparer.Ordinal), entries.Select(e => e.Id));
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
    [InlineData("compact")]
    [InlineData("flat")]
    [InlineData("grouped")]
    [InlineData("legacy")]
    public void ByteBoundedViewsCanTraverseAllObservations(string view)
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        for (var i = 0; i < 30; i++)
            journal.Log(new("in_progress", "a", Summary: new string('Ж', 2000), OccurredAt: "2026-10-20T09:00:00Z"));
        var request = Period with
        {
            PageSize = 100,
            View = view
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
