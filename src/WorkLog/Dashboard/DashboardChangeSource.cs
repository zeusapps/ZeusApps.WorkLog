using WorkLog.Infrastructure;

namespace WorkLog.Dashboard;

[Flags]
internal enum DashboardChangeDomain
{
    None = 0,
    Catalog = 1,
    Documents = 2,
    Journal = 4,
    Digests = 8,
    Reads = 16,
    Authorization = 32,
    All = Catalog | Documents | Journal | Digests | Reads
}

internal sealed record DashboardWatchRoot(string Id, string CanonicalPath, DashboardChangeDomain Domains, bool Recursive = false);
internal sealed record DashboardWatchRootDiagnostic(string Id, string[] Domains);

internal readonly record struct DashboardChangeHint(DashboardChangeDomain Domains, string? RootId = null,
    string? RelativePath = null, bool Overflow = false, bool Failed = false, bool DirectoryChanged = false,
    bool FileLifecycleChanged = false);

/// <summary>A host-owned filesystem hint source. Roots are always derived by the server from its current approved data/catalog state.</summary>
internal interface IDashboardChangeSource : IAsyncDisposable
{
    event Action<DashboardChangeHint>? Changed;
    Task RebindAsync(IReadOnlyCollection<DashboardWatchRoot> roots, CancellationToken cancellationToken);
}

/// <summary>
/// Physical watchers are coalesced at explicitly approved recursive roots. Events are routed back
/// through the coordinator's current canonical logical roots before they can trigger reconciliation.
/// </summary>
internal sealed class FileSystemDashboardChangeSource : IDashboardChangeSource
{
    private const int MaximumWatchers = 512;
    private sealed record WatcherEntry(string Path, DashboardChangeDomain Domains, bool Recursive,
        DashboardWatchRoot[] LogicalRoots, FileSystemWatcher Watcher);
    private sealed record PhysicalRoot(string Path, DashboardChangeDomain Domains, bool Recursive,
        DashboardWatchRoot[] LogicalRoots);
    private readonly object gate = new();
    private readonly Dictionary<string, WatcherEntry> watchers = new(PathComparer);
    private bool disposed;

    public event Action<DashboardChangeHint>? Changed;
    internal int WatcherCount { get { lock (gate) return watchers.Count; } }

    public Task RebindAsync(IReadOnlyCollection<DashboardWatchRoot> roots, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var logicalRoots = roots
            .Where(root => Directory.Exists(root.CanonicalPath))
            .GroupBy(root => Path.GetFullPath(root.CanonicalPath), PathComparer)
            .Select(group => new DashboardWatchRoot(group.First().Id, group.Key,
                group.Aggregate(DashboardChangeDomain.None, (value, root) => value | root.Domains), group.Any(root => root.Recursive)))
            .ToArray();
        var desired = new Dictionary<string, PhysicalRoot>(PathComparer);
        foreach (var logical in logicalRoots.OrderBy(root => root.CanonicalPath.Length).ThenBy(root => root.CanonicalPath, PathComparer))
        {
            var ancestor = desired.Values.FirstOrDefault(root => root.Recursive && IsWithinOrEqual(logical.CanonicalPath, root.Path));
            if (ancestor is not null)
            {
                desired[ancestor.Path] = ancestor with
                {
                    Domains = ancestor.Domains | logical.Domains,
                    LogicalRoots = [.. ancestor.LogicalRoots, logical]
                };
            }
            else
                desired[logical.CanonicalPath] = new(logical.CanonicalPath, logical.Domains, logical.Recursive, [logical]);
        }
        if (desired.Count > MaximumWatchers)
        {
            var overflowDomains = desired.Values.Aggregate(DashboardChangeDomain.None, (value, root) => value | root.Domains);
            Changed?.Invoke(new(overflowDomains, Overflow: true));
            desired = desired.OrderBy(pair => pair.Key, PathComparer).Take(MaximumWatchers)
                .ToDictionary(pair => pair.Key, pair => pair.Value, PathComparer);
        }
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            foreach (var stale in watchers.Keys.Where(path => !desired.ContainsKey(path)).ToArray())
            {
                watchers[stale].Watcher.Dispose();
                watchers.Remove(stale);
            }
            foreach (var (path, physical) in desired)
            {
                if (watchers.TryGetValue(path, out var existing))
                {
                    if (existing.Domains == physical.Domains && existing.Recursive == physical.Recursive)
                    {
                        watchers[path] = existing with { LogicalRoots = physical.LogicalRoots };
                        continue;
                    }
                    existing.Watcher.Dispose();
                    watchers.Remove(path);
                }
                try
                {
                    var watcher = new FileSystemWatcher(path)
                    {
                        Filter = "*",
                        IncludeSubdirectories = physical.Recursive,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite |
                            NotifyFilters.Size | NotifyFilters.CreationTime,
                        InternalBufferSize = 16 * 1024,
                        EnableRaisingEvents = false
                    };
                    watcher.Changed += (_, e) => ReportPhysical(path, e.FullPath, Directory.Exists(e.FullPath));
                    watcher.Created += (_, e) => ReportPhysical(path, e.FullPath, Directory.Exists(e.FullPath), fileLifecycleChanged: true);
                    watcher.Deleted += (_, e) => ReportPhysical(path, e.FullPath, fileLifecycleChanged: true);
                    watcher.Renamed += (_, e) =>
                    {
                        var isDirectory = Directory.Exists(e.FullPath);
                        ReportPhysical(path, e.OldFullPath, isDirectory, fileLifecycleChanged: true);
                        ReportPhysical(path, e.FullPath, isDirectory, fileLifecycleChanged: true);
                    };
                    watcher.Error += (_, _) => Changed?.Invoke(new(physical.Domains, Overflow: true, Failed: true));
                    watcher.EnableRaisingEvents = true;
                    watchers[path] = new(path, physical.Domains, physical.Recursive, physical.LogicalRoots, watcher);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or ObjectDisposedException)
                {
                    Changed?.Invoke(new(physical.Domains, Overflow: true, Failed: true));
                }
            }
        }
        return Task.CompletedTask;
    }

    private void ReportPhysical(string physicalRoot, string path, bool directoryChanged = false, bool fileLifecycleChanged = false)
    {
        WatcherEntry? watcher;
        lock (gate) watchers.TryGetValue(physicalRoot, out watcher);
        if (watcher is null) return;
        var fullPath = Path.GetFullPath(path);
        string containedPath;
        try { containedPath = SourcePath.Canonical(fullPath); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return; }
        var logicalRoot = watcher.LogicalRoots
            .Where(root => IsWithinOrEqual(containedPath, root.CanonicalPath))
            .OrderByDescending(root => root.CanonicalPath.Length)
            .FirstOrDefault();
        // Recursive native watchers can report paths reached through reparse points. Only events
        // under a current canonical logical root are allowed to trigger reconciliation.
        if (logicalRoot is null) return;
        Report(logicalRoot, containedPath, directoryChanged, fileLifecycleChanged);
    }

    private void Report(DashboardWatchRoot root, string path, bool directoryChanged = false, bool fileLifecycleChanged = false)
    {
        try
        {
            var relative = Path.GetRelativePath(root.CanonicalPath, path).Replace('\\', '/');
            var segments = relative.Split('/');
            if (Path.IsPathRooted(relative) || segments.Any(segment => segment is ".." or ""))
            {
                Changed?.Invoke(new(root.Domains, Overflow: true, DirectoryChanged: directoryChanged, FileLifecycleChanged: fileLifecycleChanged));
                return;
            }
            Changed?.Invoke(new(root.Domains, root.Id, relative.Length <= 512 ? relative : null,
                Overflow: relative.Length > 512, DirectoryChanged: directoryChanged, FileLifecycleChanged: fileLifecycleChanged));
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Changed?.Invoke(new(root.Domains, Overflow: true));
        }
    }

    internal static string WatchId(string path) => ContentHash.Compute(Path.GetFullPath(path))[..16].ToLowerInvariant();

    private static bool IsWithinOrEqual(string path, string root)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return PathComparer.Equals(fullPath, fullRoot)
            || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (disposed) return ValueTask.CompletedTask;
            disposed = true;
            foreach (var watcher in watchers.Values) watcher.Watcher.Dispose();
            watchers.Clear();
        }
        Changed = null;
        return ValueTask.CompletedTask;
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
