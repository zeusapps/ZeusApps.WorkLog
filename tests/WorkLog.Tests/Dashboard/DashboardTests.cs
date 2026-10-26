using System.Text;
using Microsoft.Data.Sqlite;
using WorkLog.Configuration;
using WorkLog.Dashboard;
using WorkLog.Infrastructure;
using WorkLog.Journaling;
using WorkLog.Tests.Testing;
using Xunit;
using JournalJson = WorkLog.Infrastructure.Json;

namespace WorkLog.Tests.Dashboard;

public sealed class DashboardTests
{
    [Fact]
    public void MissingAndUnsupportedJournalsAreNeverCreatedOrMigrated()
    {
        using var box = new TestWorkspace();
        var reader = new DashboardJournal(box.Data);
        Assert.Empty(reader.Repositories());
        Assert.False(Directory.Exists(box.Data));
        Directory.CreateDirectory(box.Data);
        var path = Path.Combine(box.Data, "journal.db");
        using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            db.Open(); using var command = db.CreateCommand(); command.CommandText = "PRAGMA user_version=999"; command.ExecuteNonQuery();
        }
        var before = File.ReadAllBytes(path);
        Assert.Throws<InvalidOperationException>(() => reader.Repositories());
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(box.Data, "*.bak"));
    }

    [Fact]
    public void TimelineRetainsLifecycleResumptionSnapshotAndPaging()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        journal.Log(new("started", "A", OccurredAt: "2026-10-01T10:00:00Z"));
        journal.Log(new("done", "A", OccurredAt: "2026-10-24T22:30:00Z"));
        journal.Log(new("in_progress", "A", OccurredAt: "2026-10-25T01:30:00Z"));
        for (var i = 0; i < 105; i++) journal.Log(new("in_progress", "A", OccurredAt: "2026-10-25T14:00:00Z"));
        var service = new TimelineService(new DashboardJournal(box.Data));
        var query = new TimelineQuery("2026-10-24T00:00:00Z", "2026-10-26T00:00:00Z", "Europe/Warsaw", "change");
        var first = service.Query(query);
        var row = Assert.Single(first.Rows);
        Assert.True(row.ClippedStart);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T10:00:00Z"), row.FirstAt);
        Assert.Equal("in_progress", row.Status);
        Assert.Equal(new[] { "2026-10-25" }, row.ActivityDates);
        Assert.Single(first.Insights.SubsequentWork);
        Assert.Equal(108, row.ObservationCount);
        var observations = service.Observations(query with { Snapshot = first.Snapshot }, row.Id, 0);
        Assert.Equal(100, observations.Entries.Length);
        Assert.Equal(100, observations.NextOffset);
        Assert.Equal(8, service.Observations(query with { Snapshot = first.Snapshot }, row.Id, 100).Entries.Length);
        journal.Log(new("done", "A", OccurredAt: "2026-10-25T20:00:00Z"));
        Assert.Equal("in_progress", Assert.Single(service.Query(query with { Snapshot = first.Snapshot }).Rows).Status);
        Assert.Equal("done", Assert.Single(service.Query(query).Rows).Status);
        Assert.Equal(row, Assert.Single(service.Query(query with { Snapshot = first.Snapshot }).Rows), new TimelineRowComparer());
    }

    private sealed class TimelineRowComparer : IEqualityComparer<TimelineRow>
    {
        public bool Equals(TimelineRow? x, TimelineRow? y) => JournalJson.Encode(x) == JournalJson.Encode(y);
        public int GetHashCode(TimelineRow obj) => obj.Id.GetHashCode(StringComparison.Ordinal);
    }

    [Fact]
    public void TicketNamespacesSetsAndFutureCompletionRemainDistinct()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        var a = new Ticket("jira", "one", "T-1"); var b = new Ticket("jira", "two", "T-1");
        journal.Log(new("in_progress", "first", Tickets: [a], OccurredAt: "2026-10-20T10:00:00Z"));
        journal.Log(new("done", "second", Tickets: [a], OccurredAt: "2026-10-20T11:00:00Z"));
        journal.Log(new("in_progress", "third", Tickets: [b], OccurredAt: "2026-10-20T12:00:00Z"));
        journal.Log(new("in_progress", "set", Tickets: [a, b, a], OccurredAt: "2026-10-20T13:00:00Z"));
        journal.Log(new("done", "third", Tickets: [b], OccurredAt: "2026-10-28T12:00:00Z"));
        var result = new TimelineService(new DashboardJournal(box.Data)).Query(new("2026-10-20T00:00:00Z", "2026-10-21T00:00:00Z", "UTC"));
        Assert.Equal(3, result.Total);
        Assert.Equal(3, Assert.Single(result.Insights.Days).Items);
        Assert.Equal(2, result.Rows.Single(r => r.Status == "done").Changes.Length);
        Assert.Equal(4, result.Rows.Sum(r => r.VisibleCount));
    }

    [Fact]
    public void DayDrillDownFiltersBeforePaging()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        for (var i = 0; i < 110; i++) journal.Log(new("in_progress", "A", OccurredAt: "2026-10-20T10:00:00Z"));
        journal.Log(new("done", "A", OccurredAt: "2026-10-24T22:30:00Z"));
        var service = new TimelineService(new DashboardJournal(box.Data));
        var query = new TimelineQuery("2026-10-20T00:00:00Z", "2026-10-26T00:00:00Z", "Europe/Warsaw", "change");
        var page = service.Query(query);
        var day = service.Observations(query with { Snapshot = page.Snapshot }, Assert.Single(page.Rows).Id, 0, day: "2026-10-25");
        Assert.Equal(1, day.Total);
        Assert.Equal("done", Assert.Single(day.Entries).Status);
        Assert.Null(day.NextOffset);
    }

    [Fact]
    public async Task CatalogPreservesArchivesNestedSpecsAndCliCounts()
    {
        using var box = new TestWorkspace();
        var setup = Setup(box);
        Write(box, "openspec/changes/demo/design.md", "# Design\nHello");
        Write(box, "openspec/changes/demo/specs/nested/spec.md", "# Nested");
        var catalog = await setup.Catalog.GetAsync();
        var active = Assert.Single(catalog.Changes);
        Assert.Equal(2, active.CompletedTasks);
        Assert.Equal(7, active.TotalTasks);
        Assert.Contains(active.Documents, d => d.Path == "specs/nested/spec.md");
        var source = active.SourceId;
        Directory.CreateDirectory(Path.Combine(box.Repo, "openspec/changes/archive"));
        Directory.Move(Path.Combine(box.Repo, "openspec/changes/demo"), Path.Combine(box.Repo, "openspec/changes/archive/2026-10-26-demo"));
        var archived = SourceCatalog.Resolve(await setup.Catalog.GetAsync(force: true), source, "demo");
        Assert.True(archived.Archived);
        Assert.Null(archived.TotalTasks);
        var document = await setup.Documents.GetAsync(new(source, "demo", "design.md"));
        Assert.False(document.CanComment);
        Assert.Equal("# Design\nHello", document.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReviewCommentsPreserveBytesEncodingAndEarlierEdits(bool bom)
    {
        using var box = new TestWorkspace();
        var setup = Setup(box);
        var encoding = new UTF8Encoding(bom);
        var path = Write(box, "openspec/changes/demo/design.md", "# Design\r\n\r\nOriginal block\r\n\r\nTail\r\n", encoding);
        var source = Assert.Single((await setup.Catalog.GetAsync()).Sources).Id;
        var original = await setup.Documents.GetAsync(new(source, "demo", "design.md"));
        Assert.Equal(1, setup.Documents.HashReads);
        await setup.Documents.GetAsync(new(source, "demo", "design.md"));
        Assert.Equal(1, setup.Documents.HashReads);
        File.WriteAllText(path, "# Design\r\n\r\nOriginal block\r\n\r\nUpdated tail\r\n", encoding);
        var saved = await setup.Documents.CommentAsync(new(source, "demo", "design.md", original.Revision, 2, 3, "Original block", "Fix --> this"));
        var marker = Assert.Single(ReviewMarker.Parse(saved.Text));
        Assert.NotNull(marker.Id);
        Assert.Equal("Original block", marker.Quote);
        Assert.Equal("Fix --> this", marker.Comment);
        Assert.Equal("# Design\r\n\r\nOriginal block\r\n\r\n\r\n\r\nUpdated tail\r\n", saved.Text.Remove(marker.Start, marker.End - marker.Start));
        Assert.Equal(bom, File.ReadAllBytes(path).AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        File.WriteAllText(path, "# Design\r\n\r\nChanged block\r\n", encoding);
        var before = File.ReadAllBytes(path);
        await Assert.ThrowsAsync<DashboardConflictException>(() => setup.Documents.CommentAsync(new(source, "demo", "design.md", original.Revision, 2, 3, "Original block", "Retain me")));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task SearchScopesAndDocumentLimitsAreExplicit()
    {
        using var box = new TestWorkspace(); var setup = Setup(box);
        Write(box, "openspec/changes/demo/design.md", string.Join('\n', Enumerable.Repeat("needle", 55)));
        Write(box, "openspec/changes/archive/2026-10-01-old/design.md", "archived needle");
        Write(box, "openspec/changes/demo/large.md", new string('x', DocumentService.MaximumBytes + 1));
        var snapshot = await setup.Catalog.GetAsync();
        var source = Assert.Single(snapshot.Sources).Id;
        var limited = await setup.Documents.SearchAsync(new("needle"));
        Assert.Equal(50, limited.Matches.Length); Assert.True(limited.LimitReached);
        var archived = await setup.Documents.SearchAsync(new("needle", Scope: "archived"));
        Assert.Single(archived.Matches);
        Assert.Empty((await setup.Documents.SearchAsync(new("needle", ["other"]))).Matches);
        await Assert.ThrowsAsync<ArgumentException>(() => setup.Documents.GetAsync(new(source, "demo", "large.md")));
        await Assert.ThrowsAsync<ArgumentException>(() => setup.Documents.GetAsync(new(source, "demo", "../../config.yaml")));
        await Assert.ThrowsAsync<ArgumentException>(() => setup.Documents.GetAsync(new(source, "demo", "%2e%2e/design.md")));
    }

    [Fact]
    public void PreferencesAreSeparateVersionedAndRecoverable()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(box.Data);
        var state = new DashboardState(box.Data);
        state.Write(new System.Text.Json.Nodes.JsonObject { ["repositories"] = new System.Text.Json.Nodes.JsonArray("one", "two") });
        Assert.Equal(2, state.Read()["repositories"]!.AsArray().Count);
        File.WriteAllText(Path.Combine(box.Data, "dashboard-state.json"), "{\"schemaVersion\":999}");
        Assert.Equal(1, state.Read()["schemaVersion"]!.GetValue<int>());
        Assert.False(File.Exists(Path.Combine(box.Data, "config.json")));
        Assert.False(File.Exists(Path.Combine(box.Data, "journal.db")));
    }

    internal static (SourceCatalog Catalog, DocumentService Documents) Setup(TestWorkspace box)
    {
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var catalog = new SourceCatalog(settings, new DashboardJournal(box.Data), new FixtureCommand(box.Repo), box.Repo);
        return (catalog, new DocumentService(catalog));
    }

    internal static string Write(TestWorkspace box, string relative, string text, Encoding? encoding = null)
    {
        var path = Path.Combine(box.Repo, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, encoding ?? new UTF8Encoding(false)); return path;
    }

    internal sealed class FixtureCommand(string root) : LocalCommand
    {
        public override Task<CommandOutput> RunAsync(string executable, string directory, string[] arguments, CancellationToken cancellationToken)
        {
            var output = executable == "git" ? (arguments[0] == "--git-dir" ? "worktree " + root : root + "\n" + Path.Combine(root, ".git"))
                : arguments[0] == "context" ? JournalJson.Encode(new { root = new { path = root } })
                : "{\"changes\":[{\"name\":\"demo\",\"completedTasks\":2,\"totalTasks\":7}]}";
            return Task.FromResult(new CommandOutput(true, 0, output, ""));
        }
    }
}
