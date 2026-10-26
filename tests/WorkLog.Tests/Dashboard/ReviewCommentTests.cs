using System.Text;
using WorkLog.Dashboard;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Dashboard;

public sealed class ReviewCommentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditAndDeleteFollowUniqueMarkerAfterEarlierEditsAndPreserveOtherBytes(bool utf16)
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        Encoding encoding = utf16 ? new UnicodeEncoding(false, true) : new UTF8Encoding(true);
        var path = DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\r\n\r\nThe selected passage.\r\n\r\nTail\r\n", encoding);
        var source = Assert.Single((await setup.Catalog.GetAsync()).Sources).Id;
        var saved = await setup.Documents.CommentAsync(new(source, "demo", "design.md", "", 2, 3, "The selected passage.", "Initial feedback"));
        var marker = Assert.Single(ReviewMarker.Parse(saved.Text));
        var beforeEdit = "Earlier unrelated paragraph\r\n\r\n" + saved.Text + "Another edit\r\n";
        File.WriteAllText(path, beforeEdit, encoding);
        var edited = await setup.Documents.MutateCommentAsync(new(source, "demo", "design.md", saved.Revision, marker.Line, marker.Raw,
            "Revised --> feedback\nSecond line", marker.Id), delete: false);
        var updated = Assert.Single(ReviewMarker.Parse(edited.Text));
        Assert.Equal(marker.Id, updated.Id);
        Assert.Equal("The selected passage.", updated.Quote);
        Assert.Equal("Revised --> feedback\nSecond line", updated.Comment);
        Assert.Equal(beforeEdit.Replace(marker.Raw, updated.Raw, StringComparison.Ordinal), edited.Text);
        Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith(encoding.GetPreamble()));
        Assert.DoesNotContain('\n', edited.Text.Replace("\r\n", "", StringComparison.Ordinal));
        var beforeDelete = edited.Text + "Retain this too\r\n";
        File.WriteAllText(path, beforeDelete, encoding);
        var deleted = await setup.Documents.MutateCommentAsync(new(source, "demo", "design.md", edited.Revision, updated.Line,
            updated.Raw, Id: updated.Id), delete: true);
        Assert.Empty(ReviewMarker.Parse(deleted.Text));
        Assert.Equal(beforeDelete.Replace(updated.Raw, "", StringComparison.Ordinal), deleted.Text);
        Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith(encoding.GetPreamble()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedMissingOrAmbiguousNotesCannotBeOverwrittenOrDeleted(bool delete)
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        var raw = ReviewMarker.Create("Original", "Passage", "\n");
        var original = "Passage\n\n" + raw + "\n";
        var path = DashboardTests.Write(box, "openspec/changes/demo/design.md", original);
        var source = Assert.Single((await setup.Catalog.GetAsync()).Sources).Id;
        var marker = Assert.Single(ReviewMarker.Parse(original));
        var request = new ReviewMutationRequest(source, "demo", "design.md", "old", marker.Line, marker.Raw, "User edit", marker.Id);
        foreach (var changed in new[] { original.Replace("Original", "Agent changed"), "Passage\n", original + "\n" + raw })
        {
            File.WriteAllText(path, changed);
            var before = File.ReadAllBytes(path);
            await Assert.ThrowsAsync<DashboardConflictException>(() => setup.Documents.MutateCommentAsync(request, delete));
            Assert.Equal(before, File.ReadAllBytes(path));
        }
    }

    [Fact]
    public async Task LegacyNotesCanBeEditedAndCodeExamplesCannotBeMutated()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        const string text = "Passage to review\n\n<!-- REVIEW old feedback -->\n\n```md\n<!-- REVIEW: example -->\n```\n\n~~~md\n<!-- REVIEW: tilde example -->\n~~~\n\n    <!-- REVIEW: indented example -->\n";
        var path = DashboardTests.Write(box, "openspec/changes/demo/design.md", text);
        var source = Assert.Single((await setup.Catalog.GetAsync()).Sources).Id;
        var original = await setup.Documents.GetAsync(new(source, "demo", "design.md"));
        var marker = Assert.Single(ReviewMarker.Parse(text));
        Assert.Null(marker.Id);
        var edited = await setup.Documents.MutateCommentAsync(new(source, "demo", "design.md", original.Revision, marker.Line, marker.Raw,
            "Updated legacy note"), delete: false);
        var updated = Assert.Single(ReviewMarker.Parse(edited.Text));
        Assert.NotNull(updated.Id);
        Assert.Equal("Passage to review", updated.Quote);
        Assert.Equal(text.Replace(marker.Raw, updated.Raw, StringComparison.Ordinal), edited.Text);
        foreach (var line in new[] { 5, 9, 12 })
        {
            await Assert.ThrowsAsync<DashboardConflictException>(() => setup.Documents.MutateCommentAsync(
                new(source, "demo", "design.md", original.Revision, line, "<!-- REVIEW: example -->"), delete: true));
        }
        Assert.Equal(edited.Text, File.ReadAllText(path));
    }

    [Fact]
    public async Task ReviewMutationsRejectArchivesOutsidePathsAndNonMarkers()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        var raw = ReviewMarker.Create("Feedback", "Passage", "\n");
        var path = DashboardTests.Write(box, "openspec/changes/demo/design.md", "Passage\n\n" + raw);
        DashboardTests.Write(box, "openspec/changes/archive/2026-10-01-old/design.md", raw);
        var source = Assert.Single((await setup.Catalog.GetAsync()).Sources).Id;
        var marker = Assert.Single(ReviewMarker.Parse(File.ReadAllText(path)));
        var request = new ReviewMutationRequest(source, "demo", "design.md", "", marker.Line, marker.Raw, "Edited", marker.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => setup.Documents.MutateCommentAsync(request with { Change = "old" }, delete: true));
        await Assert.ThrowsAsync<ArgumentException>(() => setup.Documents.MutateCommentAsync(request with { Path = "../../config.md" }, delete: false));
        await Assert.ThrowsAsync<DashboardConflictException>(() => setup.Documents.MutateCommentAsync(request with { MarkerText = "Passage" }, delete: true));
        Assert.Contains(raw, File.ReadAllText(path));
    }

    [Fact]
    public void ReferencesAreBoundedAndMarkerTerminatorsRemainEscaped()
    {
        var raw = ReviewMarker.Create("Do not break --> this\nComment:\nmore", new string('a', 400) + " -->", "\n");
        var marker = Assert.Single(ReviewMarker.Parse(raw));
        Assert.Equal(281, marker.Quote.Length);
        Assert.Equal("Do not break --> this\nComment:\nmore", marker.Comment);
        Assert.Contains("Target: preceding passage\nQuote: ", raw);
        Assert.Equal(1, raw.Split("-->").Length - 1);
        var unicode = ReviewMarker.Create("Feedback", new string('a', 279) + "🙂more", "\n");
        Assert.Equal(new string('a', 279) + "…", Assert.Single(ReviewMarker.Parse(unicode)).Quote);
        _ = new UTF8Encoding(false, true).GetBytes(unicode);
    }
}
