using WorkLog.Configuration;
using WorkLog.Dashboard;
using WorkLog.Journaling;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Dashboard;

public sealed class ReviewOverviewTests
{
    private static string Marker(string id, string quote, string comment) =>
        "<!-- REVIEW: id=" + id + "\nTarget: preceding passage\nQuote: " + quote + "\nComment:\n" + comment + "\n-->\n";

    private static string Id(int n) => n.ToString("x32");

    [Fact]
    public async Task OverviewListsMarkersByScopeAndSkipsCodeExamples()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        DashboardTests.Write(box, "openspec/changes/demo/design.md",
            "# Design\n\nFirst passage.\n\n" + Marker(Id(1), "First passage.", "Rewrite this.")
            + "\n```md\n<!-- REVIEW: Not a comment, just syntax. -->\n```\n\nLegacy passage.\n\n<!-- REVIEW: Legacy note -->\n");
        DashboardTests.Write(box, "openspec/changes/demo/specs/a/spec.md", "# Spec\n\nRule.\n\n" + Marker(Id(2), "Rule.", "Clarify the rule."));
        DashboardTests.Write(box, "openspec/changes/archive/2026-10-01-old/design.md", "# Old\n\nLeft behind.\n\n" + Marker(Id(3), "Left behind.", "Never addressed."));

        var active = await setup.Documents.ReviewsAsync(new());
        Assert.Equal(3, active.Items.Length);
        Assert.All(active.Items, i => Assert.False(i.Archived));
        Assert.Empty(active.Unavailable);
        var first = active.Items.Single(i => i.Id == Id(1));
        Assert.Equal(("demo", "design.md", 4, "First passage.", "Rewrite this."), (first.Change, first.Path, first.Line, first.Quote, first.Comment));
        Assert.Equal(["repo"], first.Repositories);
        Assert.False(first.Truncated);
        Assert.Contains(active.Items, i => i.Id is null && i.Comment == "Legacy note" && i.Quote == "Legacy passage.");
        Assert.Contains(active.Items, i => i.Path == "specs/a/spec.md" && i.Id == Id(2));
        Assert.DoesNotContain(active.Items, i => i.Comment.Contains("just syntax", StringComparison.Ordinal));

        var archived = Assert.Single((await setup.Documents.ReviewsAsync(new(Scope: "archived"))).Items);
        Assert.True(archived.Archived);
        Assert.Equal(("old", "design.md", Id(3)), (archived.Change, archived.Path, archived.Id));

        var all = await setup.Documents.ReviewsAsync(new(Scope: "all"));
        Assert.Equal(4, all.Items.Length);
        await Assert.ThrowsAsync<ArgumentException>(() => setup.Documents.ReviewsAsync(new(Scope: "everything")));
    }

    [Fact]
    public async Task OverviewIsDerivedFromTheFilesSoARemovedMarkerDisappears()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        var path = DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n\nPassage.\n\n" + Marker(Id(1), "Passage.", "Fix."));
        Assert.Single((await setup.Documents.ReviewsAsync(new())).Items);
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n\nPassage, fixed.\n");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
        // The listing follows the catalog's stamp of the document, which the client refreshes on every revision change.
        await setup.Catalog.GetAsync(force: true);
        Assert.Empty((await setup.Documents.ReviewsAsync(new())).Items);
    }

    [Fact]
    public async Task OverviewTruncatesLongTextWithoutSplittingCharactersAndFlagsIt()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        var quote = new string('q', 279) + "😀" + "tail";
        var comment = new string('c', 499) + "😀" + "tail";
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n\n" + Marker(Id(1), quote, comment));
        var item = Assert.Single((await setup.Documents.ReviewsAsync(new())).Items);
        Assert.True(item.Truncated);
        Assert.Equal(new string('q', 279) + "…", item.Quote);
        Assert.Equal(new string('c', 499), item.Comment);
        Assert.DoesNotContain(item.Quote + item.Comment, char.IsSurrogate);

        // Text at the limits is not truncated, and the ellipsis the writer adds to a long quote does not count.
        var exact = new string('e', 500);
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n\n" + Marker(Id(2), new string('q', 280) + "…", exact));
        await setup.Catalog.GetAsync(force: true);
        var whole = Assert.Single((await setup.Documents.ReviewsAsync(new())).Items);
        Assert.False(whole.Truncated);
        Assert.Equal(exact, whole.Comment);
        Assert.Equal(new string('q', 280) + "…", whole.Quote);
    }

    [Fact]
    public async Task UnreadableDocumentsAndSourcesAreReportedNotCountedAsEmpty()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n\nPassage.\n\n" + Marker(Id(1), "Passage.", "Fix."));
        DashboardTests.Write(box, "openspec/changes/demo/large.md", new string('x', DocumentService.MaximumBytes + 1));
        new Journal(new LocalSettings(box.Data), new("elsewhere")).Log(new("in_progress", "other-work"));

        var result = await setup.Documents.ReviewsAsync(new());
        Assert.Single(result.Items);
        Assert.Contains(result.Unavailable, n => n.StartsWith("demo/large.md:", StringComparison.Ordinal));
        Assert.Contains(result.Unavailable, n => n.StartsWith("elsewhere:", StringComparison.Ordinal));

        // A request for one repository is only made incomplete by notices about that repository.
        var scoped = await setup.Documents.ReviewsAsync(new(["repo"]));
        Assert.Single(scoped.Items);
        Assert.DoesNotContain(scoped.Unavailable, n => n.StartsWith("elsewhere:", StringComparison.Ordinal));
        var other = await setup.Documents.ReviewsAsync(new(["elsewhere"]));
        Assert.Empty(other.Items);
        Assert.Contains(other.Unavailable, n => n.StartsWith("elsewhere:", StringComparison.Ordinal));
        Assert.DoesNotContain(other.Unavailable, n => n.StartsWith("demo/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SecondListingOverUnchangedFilesDoesNotReparse()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        for (var i = 0; i < 5; i++)
            DashboardTests.Write(box, $"openspec/changes/demo/doc-{i}.md", "# Doc\n\nPassage.\n\n" + Marker(Id(i), "Passage.", "Note " + i));
        var first = await setup.Documents.ReviewsAsync(new());
        Assert.Equal(5, first.Items.Length);
        Assert.Equal(5, setup.Documents.MarkerParses);
        var hashReads = setup.Documents.HashReads;

        var second = await setup.Documents.ReviewsAsync(new());
        Assert.Equal(first.Items.Select(i => i.Id), second.Items.Select(i => i.Id));
        Assert.Equal(5, setup.Documents.MarkerParses);
        Assert.Equal(hashReads, setup.Documents.HashReads);

        // Only the edited file is parsed again.
        var edited = DashboardTests.Write(box, "openspec/changes/demo/doc-2.md", "# Doc\n\nPassage.\n\n" + Marker(Id(2), "Passage.", "Edited note"));
        File.SetLastWriteTimeUtc(edited, DateTime.UtcNow.AddSeconds(10));
        await setup.Catalog.GetAsync(force: true);
        var third = await setup.Documents.ReviewsAsync(new());
        Assert.Equal(6, setup.Documents.MarkerParses);
        Assert.Contains(third.Items, i => i.Comment == "Edited note");
    }

    [Fact]
    public async Task ASecondListingOverMoreThan512DocumentsDoesNotReparse()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        const int count = 900;
        for (var i = 0; i < count; i++)
            DashboardTests.Write(box, $"openspec/changes/many/doc-{i:000}.md", "# Doc\n\n" + Marker(Id(i), "Doc", "Note " + i));
        var first = await setup.Documents.ReviewsAsync(new());
        Assert.Equal(count, first.Items.Length);
        Assert.Equal(count, setup.Documents.MarkerParses);
        Assert.Equal(count, setup.Documents.MarkerCacheSize);
        for (var round = 0; round < 3; round++)
        {
            Assert.Equal(count, (await setup.Documents.ReviewsAsync(new())).Items.Length);
            Assert.Equal(count, setup.Documents.MarkerParses);
        }
    }

    [Fact]
    public async Task EntriesOfDocumentsRemovedFromTheCatalogAreDropped()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        var files = new List<string>();
        for (var i = 0; i < 6; i++)
            files.Add(DashboardTests.Write(box, $"openspec/changes/demo/doc-{i}.md", "# Doc\n\n" + Marker(Id(i), "Doc", "Note " + i)));
        await setup.Documents.ReviewsAsync(new());
        Assert.Equal(6, setup.Documents.MarkerCacheSize);

        File.Delete(files[0]);
        File.Delete(files[1]);
        await setup.Catalog.GetAsync(force: true);
        // A listing over a scope that holds none of the removed documents still drops them, and keeps the rest.
        await setup.Documents.ReviewsAsync(new(Scope: "archived"));
        Assert.Equal(4, setup.Documents.MarkerCacheSize);
        Assert.Equal(4, (await setup.Documents.ReviewsAsync(new())).Items.Length);
        Assert.Equal(6, setup.Documents.MarkerParses);
    }

    [Fact]
    public async Task AlternatingActiveArchivedAndActiveListingsDoNotReparse()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        for (var i = 0; i < 600; i++)
            DashboardTests.Write(box, $"openspec/changes/old/doc-{i:000}.md", "# Doc\n\n" + Marker(Id(i), "Doc", "Old " + i));
        for (var i = 0; i < 600; i++)
            DashboardTests.Write(box, $"openspec/changes/archive/2026-01-01-new/doc-{i:000}.md", "# Doc\n\n" + Marker(Id(1000 + i), "Doc", "New " + i));
        await setup.Documents.ReviewsAsync(new(Scope: "active"));
        await setup.Documents.ReviewsAsync(new(Scope: "archived"));
        Assert.Equal(1200, setup.Documents.MarkerParses);
        await setup.Documents.ReviewsAsync(new(Scope: "active"));
        await setup.Documents.ReviewsAsync(new(Scope: "archived"));
        await setup.Documents.ReviewsAsync(new(Scope: "active"));
        Assert.Equal(1200, setup.Documents.MarkerParses);
        Assert.Equal(1200, setup.Documents.MarkerCacheSize);
    }

    [Fact]
    public async Task ListingMissesDoNotEvictTheReadersDocuments()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        const int count = 300;
        for (var i = 0; i < count; i++)
            DashboardTests.Write(box, $"openspec/changes/many/doc-{i:000}.md", "# Doc\n\n" + Marker(Id(i), "Doc", "Note " + i));
        var source = Assert.Single((await setup.Catalog.GetAsync()).Sources).Id;
        await setup.Documents.GetAsync(new(source, "many", "doc-299.md"));
        Assert.Equal(1, setup.Documents.HashReads);
        // 300 documents are parsed, more than the reader's cache holds, and the reader's document is still cached.
        await setup.Documents.ReviewsAsync(new());
        Assert.Equal(count, setup.Documents.MarkerParses);
        var hashReads = setup.Documents.HashReads;
        await setup.Documents.GetAsync(new(source, "many", "doc-299.md"));
        Assert.Equal(hashReads, setup.Documents.HashReads);
    }

    [Fact]
    public async Task WarmListingResolvesNoPathsAndAChangeDirectoryIsResolvedOnceOnAMiss()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        for (var i = 0; i < 6; i++)
            DashboardTests.Write(box, $"openspec/changes/a/doc-{i}.md", "# Doc\n\n" + Marker(Id(i), "Doc", "A " + i));
        for (var i = 0; i < 4; i++)
            DashboardTests.Write(box, $"openspec/changes/archive/2026-01-01-b/doc-{i}.md", "# Doc\n\n" + Marker(Id(100 + i), "Doc", "B " + i));
        var cold = await setup.Documents.ReviewsAsync(new(Scope: "all"));
        Assert.Equal(10, cold.Items.Length);
        Assert.Equal(2, setup.Documents.ChangeResolutions);

        for (var i = 0; i < 5; i++)
            Assert.Equal(10, (await setup.Documents.ReviewsAsync(new(Scope: "all"))).Items.Length);
        Assert.Equal(2, setup.Documents.ChangeResolutions);
        Assert.Equal(10, setup.Documents.MarkerParses);

        // One edited document resolves only its own change, once, and parses only itself.
        var edited = DashboardTests.Write(box, "openspec/changes/a/doc-3.md", "# Doc\n\n" + Marker(Id(3), "Doc", "A edited"));
        File.SetLastWriteTimeUtc(edited, DateTime.UtcNow.AddSeconds(10));
        await setup.Catalog.GetAsync(force: true);
        var after = await setup.Documents.ReviewsAsync(new(Scope: "all"));
        Assert.Contains(after.Items, i => i.Comment == "A edited");
        Assert.Equal(3, setup.Documents.ChangeResolutions);
        Assert.Equal(11, setup.Documents.MarkerParses);
    }

    [Fact]
    public async Task ADocumentDeletedBetweenListingsIsNotServedFromTheCache()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n\n" + Marker(Id(1), "Design", "Keep."));
        var gone = DashboardTests.Write(box, "openspec/changes/demo/gone.md", "# Gone\n\n" + Marker(Id(2), "Gone", "Vanishes."));
        Assert.Equal(2, (await setup.Documents.ReviewsAsync(new())).Items.Length);

        File.Delete(gone);
        await setup.Catalog.GetAsync(force: true);
        var after = await setup.Documents.ReviewsAsync(new());
        Assert.Equal(Id(1), Assert.Single(after.Items).Id);

        // A document replaced by an unreadable one is reported unavailable, not answered with the old markers.
        var design = Path.Combine(box.Repo, "openspec/changes/demo/design.md");
        File.WriteAllText(design, new string('x', DocumentService.MaximumBytes + 1));
        File.SetLastWriteTimeUtc(design, DateTime.UtcNow.AddSeconds(20));
        await setup.Catalog.GetAsync(force: true);
        var unreadable = await setup.Documents.ReviewsAsync(new());
        Assert.Empty(unreadable.Items);
        Assert.Contains(unreadable.Unavailable, n => n.StartsWith("demo/design.md:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CachedListingsOfAFittingSetStayFreeAfterManyRepeatedListings()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        for (var i = 0; i < 40; i++)
            DashboardTests.Write(box, $"openspec/changes/many/doc-{i:000}.md", "# Doc\n\n" + Marker(Id(i), "Doc", "Note " + i));
        await setup.Documents.ReviewsAsync(new());
        var parses = setup.Documents.MarkerParses;
        for (var i = 0; i < 50; i++)
            await setup.Documents.ReviewsAsync(new());
        Assert.Equal(parses, setup.Documents.MarkerParses);
    }

    [Fact]
    public async Task CatalogDocumentsExposeTheirLengthAndWriteTimeWithoutChangingRevisionSemantics()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        var design = DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n");
        DashboardTests.Write(box, "openspec/changes/demo/proposal.md", "# Proposal\n");
        var before = await setup.Catalog.GetAsync(force: true);
        var document = Assert.Single(Assert.Single(before.Changes).Documents, d => d.Path == "design.md");
        var info = new FileInfo(design);
        Assert.Equal(info.Length + ":" + info.LastWriteTimeUtc.Ticks, document.Fingerprint);
        Assert.Equal(before.Revision, (await setup.Catalog.GetAsync(force: true)).Revision);

        File.WriteAllText(design, "# Design, edited\n");
        File.SetLastWriteTimeUtc(design, info.LastWriteTimeUtc.AddSeconds(3));
        var after = await setup.Catalog.GetAsync(force: true);
        Assert.NotEqual(before.Revision, after.Revision);
        Assert.NotEqual(document.Fingerprint, after.Changes[0].Documents.Single(d => d.Path == "design.md").Fingerprint);
        Assert.Equal(before.Changes[0].Documents.Single(d => d.Path == "proposal.md").Fingerprint,
            after.Changes[0].Documents.Single(d => d.Path == "proposal.md").Fingerprint);
    }
}
