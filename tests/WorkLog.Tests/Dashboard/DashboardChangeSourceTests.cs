using WorkLog.Dashboard;
using WorkLog.Infrastructure;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Dashboard;

public sealed class DashboardChangeSourceTests
{
    [Fact]
    public async Task FileWatcherDeduplicatesCanonicalRootsAndReportsAtomicReplacementLifecycle()
    {
        using var box = new TestWorkspace();
        var root = Path.Combine(box.Root, "watched");
        Directory.CreateDirectory(root);
        await using var source = new FileSystemDashboardChangeSource();
        var changed = new TaskCompletionSource<DashboardChangeHint>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Changed += hint =>
        {
            if (hint.RelativePath == "design.md" && hint.FileLifecycleChanged)
                changed.TrySetResult(hint);
        };

        await source.RebindAsync([
            new("documents", Path.GetFullPath(root), DashboardChangeDomain.Documents),
            new("catalog", Path.Combine(root, "."), DashboardChangeDomain.Catalog)
        ], CancellationToken.None);

        Assert.Equal(1, source.WatcherCount);
        var temporary = Path.Combine(root, "design.tmp");
        await File.WriteAllTextAsync(temporary, "# Complete replacement\n");
        File.Move(temporary, Path.Combine(root, "design.md"));

        var hint = await changed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(DashboardChangeDomain.Documents | DashboardChangeDomain.Catalog, hint.Domains);
        Assert.Equal("design.md", hint.RelativePath);

        var next = Path.Combine(box.Root, "next");
        Directory.CreateDirectory(next);
        await source.RebindAsync([new("next", next, DashboardChangeDomain.Reads)], CancellationToken.None);
        Assert.Equal(1, source.WatcherCount);
    }

    [Fact]
    public async Task FileWatcherDisposalRemovesAllSubscriptions()
    {
        using var box = new TestWorkspace();
        var root = Path.Combine(box.Root, "watched");
        Directory.CreateDirectory(root);
        var source = new FileSystemDashboardChangeSource();
        await source.RebindAsync([new("documents", root, DashboardChangeDomain.Documents)], CancellationToken.None);
        Assert.Equal(1, source.WatcherCount);

        await source.DisposeAsync();

        Assert.Equal(0, source.WatcherCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            source.RebindAsync([new("documents", root, DashboardChangeDomain.Documents)], CancellationToken.None));
    }

    [Fact]
    public async Task RecursivePhysicalWatcherCoalescesLogicalRootsAndFiltersOutsideJunctionEvents()
    {
        using var box = new TestWorkspace();
        var changesRoot = Path.Combine(box.Root, "repo", "openspec", "changes");
        var changeRoot = Path.Combine(changesRoot, "demo");
        var nestedRoot = Path.Combine(changeRoot, "specs", "capability");
        var outside = Path.Combine(box.Root, "outside");
        Directory.CreateDirectory(nestedRoot);
        Directory.CreateDirectory(outside);
        var junction = Path.Combine(nestedRoot, "outside-link");
        await CreateDirectoryLink(junction, outside, box.Root);

        await using var source = new FileSystemDashboardChangeSource();
        var observed = new System.Collections.Concurrent.ConcurrentQueue<DashboardChangeHint>();
        var nestedEvent = new TaskCompletionSource<DashboardChangeHint>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Changed += hint =>
        {
            observed.Enqueue(hint);
            if (hint.RootId == "nested-root" && hint.RelativePath == "inside.md") nestedEvent.TrySetResult(hint);
        };
        await source.RebindAsync([
            new("changes-root", changesRoot, DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents, Recursive: true),
            new("change-root", changeRoot, DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents),
            new("nested-root", nestedRoot, DashboardChangeDomain.Documents)
        ], CancellationToken.None);

        Assert.Equal(1, source.WatcherCount);
        await File.WriteAllTextAsync(Path.Combine(outside, "outside.md"), "outside the approved root");
        await File.WriteAllTextAsync(Path.Combine(nestedRoot, "inside.md"), "contained update");
        var hint = await nestedEvent.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(100);

        Assert.Equal("nested-root", hint.RootId);
        Assert.Equal("inside.md", hint.RelativePath);
        Assert.NotEmpty(observed);
        Assert.All(observed, item => Assert.Equal("nested-root", item.RootId));
        Assert.DoesNotContain(observed, item => item.RelativePath?.Contains("outside", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static async Task CreateDirectoryLink(string link, string target, string workingDirectory)
    {
        try { Directory.CreateSymbolicLink(link, target); }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        {
            var result = await new LocalCommand().RunAsync("cmd.exe", workingDirectory,
                ["/d", "/c", "mklink", "/J", link, target], CancellationToken.None);
            Assert.Equal(0, result.ExitCode);
        }
    }
}
