using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WorkLog.Configuration;
using WorkLog.Infrastructure;

namespace WorkLog.Dashboard;

internal sealed record DashboardRepository(string Id, string Label);
internal sealed record DocumentSource(string Id, string[] Repositories, string Location, string? Error, [property: JsonIgnore] string Root,
    [property: JsonIgnore] string? StoreId = null);
internal sealed record ChangeDocument(string Path, string Label);
internal sealed record ChangeInfo(string SourceId, string Name, string DirectoryName, string Variant, bool Archived,
    string[] Repositories, ChangeDocument[] Documents, int? CompletedTasks, int? TotalTasks, string? Error, [property: JsonIgnore] string Directory,
    [property: JsonIgnore] string ContentStamp = "", [property: JsonIgnore] string TaskStamp = "");
internal sealed record SourceNotice(string SourceId, string Text);
internal sealed record CatalogSnapshot(DashboardRepository[] Repositories, DocumentSource[] Sources, ChangeInfo[] Changes,
    string? LaunchRepository, string Revision, string[] Notices, long JournalSnapshot = 0,
    [property: JsonIgnore] SourceNotice[]? SourceNotices = null);

internal sealed partial class SourceCatalog(LocalSettings settings, DashboardJournal journal, LocalCommand command, string? launchDirectory, TimeProvider? time = null)
{
    // Expensive-tier results (git and OpenSpec CLI invocations) stay valid this long, unless an input stamp changes first.
    internal static readonly TimeSpan ExpensiveLifetime = TimeSpan.FromSeconds(60);
    private const int Parallelism = 4;
    // A timed-out result is kept for this long (never longer than ExpensiveLifetime); its source error stays visible meanwhile.
    internal static readonly TimeSpan TimeoutBackoff = TimeSpan.FromSeconds(30);
    private sealed record Entry(CommandOutput Value, DateTimeOffset At, string Stamp, TimeSpan Lifetime);
    private readonly SemaphoreSlim gate = new(1);
    private readonly ConcurrentDictionary<string, Entry> commands = new(StringComparer.Ordinal);
    private readonly TimeProvider clock = time ?? TimeProvider.System;
    private LocalSettings current = settings;
    private string? configStamp;
    private long repositorySnapshot = -1;
    private string[] repositoryNames = [];
    private CatalogSnapshot? cached;
    private DateTimeOffset refreshed;

    public async Task<CatalogSnapshot> GetAsync(CancellationToken cancellationToken = default, bool force = false)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!force && cached is not null && clock.GetUtcNow() - refreshed < TimeSpan.FromSeconds(2)) return cached;
            cached = await DiscoverAsync(force, cancellationToken);
            refreshed = clock.GetUtcNow();
            return cached;
        }
        finally { gate.Release(); }
    }

    private async Task<CommandOutput> CachedAsync(string key, string stamp, bool force, Func<Task<CommandOutput>> run)
    {
        if (!force && commands.TryGetValue(key, out var entry) && entry.Stamp == stamp && clock.GetUtcNow() - entry.At < entry.Lifetime)
            return entry.Value;
        var value = await run();
        // A timed-out command is remembered only for a short backoff, so a hung CLI is not spawned (and waited on for 15 s) on every refresh.
        commands[key] = new(value, clock.GetUtcNow(), stamp, value.Available && value.ExitCode == -1 ? TimeoutBackoff : ExpensiveLifetime);
        return value;
    }

    private static Task RunInParallelAsync(int count, Func<int, CancellationToken, Task> body, CancellationToken token)
        => Parallel.ForEachAsync(Enumerable.Range(0, count), new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = token },
            async (index, ct) => await body(index, ct));

    private static string FileStamp(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? info.Length + "@" + info.LastWriteTimeUtc.Ticks : "-";
    }

    private async Task<CatalogSnapshot> DiscoverAsync(bool force, CancellationToken token)
    {
        var repositories = new Dictionary<string, DashboardRepository>(StringComparer.Ordinal);
        var checkouts = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var notices = new List<string>();
        var sourceNotices = new List<SourceNotice>();
        void AddRepository(string identity, string? checkout = null)
        {
            repositories.TryAdd(identity, new(identity, identity.StartsWith("local:", StringComparison.Ordinal) && checkout is not null
                ? Path.GetFileName(checkout) : identity));
            if (checkout is not null && Directory.Exists(checkout)) checkouts.TryAdd(Path.GetFullPath(checkout), identity);
        }
        long journalSnapshot = 0;
        try
        {
            // Journal rows are append-only, so the repository list only needs recomputing when the snapshot moves.
            journalSnapshot = journal.Snapshot();
            if (journalSnapshot != repositorySnapshot)
            {
                repositoryNames = journal.Repositories();
                repositorySnapshot = journalSnapshot;
            }
            foreach (var identity in repositoryNames) AddRepository(identity);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException) { notices.Add(e.Message); }
        // Repository contexts come from config.json. Reload them when the file changes; without a file the in-memory settings stay.
        var stamp = FileStamp(current.ConfigPath);
        if (configStamp is not null && stamp != configStamp)
        {
            try { current = new LocalSettings(current.DataDirectory); }
            catch (Exception e) when (e is IOException or JsonException or ArgumentException or UnauthorizedAccessException)
            { notices.Add("Repository configuration could not be reloaded: " + e.Message); }
        }
        configStamp = stamp;
        var contexts = current.Configuration.RepositoryContexts.Where(p => Directory.Exists(p.Key)).ToArray();
        foreach (var pair in current.Configuration.RepositoryContexts) AddRepository(pair.Value.Repository.Identity);
        var worktrees = new CommandOutput[contexts.Length];
        await RunInParallelAsync(contexts.Length, async (i, ct) => worktrees[i] = await CachedAsync("worktree|" + contexts[i].Key, stamp, force,
            () => command.RunAsync("git", contexts[i].Key, ["--git-dir", contexts[i].Key, "worktree", "list", "--porcelain"], ct)), token);
        for (var i = 0; i < contexts.Length; i++)
            foreach (var line in worktrees[i].Output.Split('\n'))
                if (line.StartsWith("worktree ", StringComparison.Ordinal)) AddRepository(contexts[i].Value.Repository.Identity, line[9..].TrimEnd('\r'));
        var promoted = current.Configuration.RepositoryContexts.Values.SelectMany(c => c.LegacyPaths)
            .ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var pair in current.Configuration.Repositories.Where(p => !promoted.Contains(p.Key)))
        {
            var directory = Path.GetFileName(pair.Key) == ".git" ? Path.GetDirectoryName(pair.Key)! : pair.Key;
            AddRepository(pair.Value.Identity, directory);
        }
        string? launchRepository = null;
        if (launchDirectory is not null && Directory.Exists(launchDirectory))
        {
            var git = await CachedAsync("launch|" + launchDirectory, stamp, force,
                () => command.RunAsync("git", launchDirectory, ["rev-parse", "--show-toplevel", "--git-common-dir"], token));
            var lines = git.Output.Trim().Split('\n');
            var checkout = git.ExitCode == 0 && lines.Length > 0 ? Path.GetFullPath(lines[0].Trim()) : Path.GetFullPath(launchDirectory);
            if (!checkouts.TryGetValue(checkout, out launchRepository))
            {
                var common = lines.Length > 1 ? Path.GetFullPath(lines[1].Trim(), checkout) : "";
                launchRepository = current.Configuration.RepositoryContexts.FirstOrDefault(p =>
                    string.Equals(Path.GetFullPath(p.Key), common, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).Value?.Repository.Identity;
                launchRepository ??= "source:" + ContentHash.Compute(checkout);
                AddRepository(launchRepository, checkout);
            }
        }
        var sources = new Dictionary<string, DocumentSource>(StringComparer.Ordinal);
        var ordered = checkouts.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
        var resolutions = new (string? Root, string? StoreId, string? Error)[ordered.Length];
        await RunInParallelAsync(ordered.Length, async (i, ct) => resolutions[i] = await ResolveAsync(ordered[i].Key, stamp, force, ct), token);
        for (var i = 0; i < ordered.Length; i++)
        {
            var (root, storeId, error) = resolutions[i];
            if (root is null && error is null) continue;
            var id = ContentHash.Compute(root is null ? ordered[i].Key : SourcePath.Canonical(root));
            if (sources.TryGetValue(id, out var previous))
                sources[id] = previous with { Repositories = previous.Repositories.Append(ordered[i].Value).Distinct().ToArray() };
            else sources[id] = new(id, [ordered[i].Value], root ?? ordered[i].Key, error, root ?? ordered[i].Key, storeId);
        }
        foreach (var repo in repositories.Values)
            if (!sources.Values.Any(s => s.Repositories.Contains(repo.Id))) notices.Add(repo.Label + ": no available OpenSpec source. Recorded history is still available.");
        var changes = new List<ChangeInfo>();
        var listed = new List<DocumentSource>();
        foreach (var source in sources.Values.Where(s => s.Error is null))
        {
            try
            {
                // The source root is canonicalized once; entries below a canonical directory are canonical unless they are links.
                var canonicalRoot = SourcePath.Canonical(source.Root);
                var changeRoot = SourcePath.Within(source.Root, canonicalRoot, "openspec/changes");
                if (!Directory.Exists(changeRoot)) continue;
                foreach (var directory in SourcePath.List(changeRoot).Where(i => i.IsDirectory).OrderBy(i => i.Path, StringComparer.Ordinal))
                {
                    if (Path.GetFileName(directory.Path) == "archive")
                    {
                        var archive = SourcePath.Contained(canonicalRoot, directory);
                        foreach (var archived in SourcePath.List(archive).Where(i => i.IsDirectory).OrderBy(i => i.Path, StringComparer.Ordinal))
                            AddChange(source, canonicalRoot, archived, true, changes, notices, sourceNotices);
                    }
                    else AddChange(source, canonicalRoot, directory, false, changes, notices, sourceNotices);
                }
                listed.Add(source);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                notices.Add(source.Location + ": " + e.Message);
                sourceNotices.Add(new(source.Id, source.Location + ": " + e.Message));
            }
        }
        // Task progress is an expensive-tier call per source, refreshed when the source's change directories or task files change.
        var progress = new CommandOutput[listed.Count];
        await RunInParallelAsync(listed.Count, async (i, ct) =>
        {
            var source = listed[i];
            var inputs = string.Join('\n', changes.Where(c => c.SourceId == source.Id).Select(c => c.DirectoryName + "|" + c.Archived + "|" + c.TaskStamp));
            progress[i] = await CachedAsync("list|" + source.Id, ContentHash.Compute(inputs), force, () => command.RunAsync("openspec", source.Root,
                source.StoreId is null ? ["list", "--json"] : ["list", "--json", "--store", source.StoreId], ct));
        }, token);
        for (var i = 0; i < listed.Count; i++)
        {
            var source = listed[i];
            var counts = new Dictionary<string, (int Complete, int Total)>(StringComparer.Ordinal);
            if (progress[i].Available && progress[i].ExitCode == 0)
            {
                try
                {
                    using var document = JsonDocument.Parse(progress[i].Output);
                    if (document.RootElement.TryGetProperty("changes", out var items))
                        foreach (var item in items.EnumerateArray())
                            if (item.TryGetProperty("completedTasks", out var done) && item.TryGetProperty("totalTasks", out var total)
                                && done.TryGetInt32(out var d) && total.TryGetInt32(out var t) && d >= 0 && t >= d)
                                counts[item.GetProperty("name").GetString()!] = (d, t);
                }
                catch (JsonException) { notices.Add(source.Location + ": OpenSpec task progress is unavailable."); }
            }
            for (var c = 0; c < changes.Count; c++)
                if (changes[c].SourceId == source.Id && !changes[c].Archived && counts.TryGetValue(changes[c].DirectoryName, out var count))
                    changes[c] = changes[c] with { CompletedTasks = count.Complete, TotalTasks = count.Total };
        }
        var revision = ContentHash.Compute(Json.Encode(changes.Select(c => new { c.SourceId, c.DirectoryName, c.Archived, c.Documents, c.CompletedTasks, c.TotalTasks, c.ContentStamp })));
        return new(repositories.Values.OrderBy(r => r.Label, StringComparer.Ordinal).ToArray(), sources.Values.ToArray(), changes.ToArray(), launchRepository,
            revision, notices.ToArray(), journalSnapshot, sourceNotices.ToArray());
    }

    private async Task<(string? Root, string? StoreId, string? Error)> ResolveAsync(string checkout, string settingsStamp, bool force, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var openspec = Path.Combine(checkout, "openspec");
        var inputs = settingsStamp + "|" + Directory.Exists(openspec) + "|" + FileStamp(Path.Combine(openspec, "config.yaml")) + "|" + FileStamp(Path.Combine(openspec, "config.yml"));
        var resolution = await CachedAsync("context|" + checkout, inputs, force, () => command.RunAsync("openspec", checkout, ["context", "--json"], token));
        string? root = null;
        string? storeId = null;
        string? error = null;
        if (resolution.Available)
        {
            try
            {
                using var context = JsonDocument.Parse(resolution.Output);
                if (context.RootElement.TryGetProperty("root", out var rootObject) && rootObject.ValueKind == JsonValueKind.Object
                    && rootObject.TryGetProperty("path", out var rootPath))
                {
                    root = rootPath.GetString();
                    if (rootObject.TryGetProperty("store_id", out var selectedStore)) storeId = selectedStore.GetString();
                }
                if (root is null && (Directory.Exists(openspec) || resolution.ExitCode != 0))
                    error = ErrorText(context.RootElement, resolution.Error);
            }
            catch (JsonException) { error = "OpenSpec context could not be resolved: " + resolution.Error; }
        }
        else if (Directory.Exists(openspec))
        {
            var configPath = new[] { "config.yaml", "config.yml" }.Select(n => Path.Combine(openspec, n)).FirstOrDefault(File.Exists);
            var config = configPath is null ? "" : await File.ReadAllTextAsync(configPath, token);
            if (StoreDeclaration().IsMatch(config)) error = "OpenSpec CLI is unavailable; the configured store cannot be resolved.";
            else root = checkout;
        }
        return (root, storeId, error);
    }

    private static void AddChange(DocumentSource source, string canonicalRoot, SourcePath.Item item, bool archived, List<ChangeInfo> changes, List<string> notices,
        List<SourceNotice> sourceNotices)
    {
        try
        {
            var safe = SourcePath.Contained(canonicalRoot, item);
            var name = Path.GetFileName(item.Path);
            var logical = archived ? ArchivePrefix().Replace(name, "", 1) : name;
            var files = SourcePath.MarkdownFiles(canonicalRoot, safe)
                .Select(f => (Relative: Path.GetRelativePath(safe, f.Path).Replace('\\', '/'), f.Stamp)).ToArray();
            var documents = files.Select(f => f.Relative)
                .OrderBy(p => p == "design.md" ? 0 : p == "proposal.md" ? 1 : p == "tasks.md" ? 2 : 3).ThenBy(p => p, StringComparer.Ordinal)
                .Select(p => new ChangeDocument(p, p.EndsWith(".md", StringComparison.Ordinal) ? p[..^3] : p)).ToArray();
            // Length and write time of each Markdown file: edits change the catalog revision without reading file contents.
            var stamps = files.OrderBy(f => f.Relative, StringComparer.Ordinal).ToArray();
            var content = ContentHash.Compute(string.Join('\n', stamps.Select(f => f.Relative + "=" + f.Stamp)));
            var tasks = string.Join('\n', stamps.Where(f => f.Relative.Contains("task", StringComparison.OrdinalIgnoreCase)).Select(f => f.Relative + "=" + f.Stamp));
            changes.Add(new(source.Id, logical, name, ContentHash.Compute(Path.GetRelativePath(source.Root, safe)), archived, source.Repositories,
                documents, null, null, null, safe, content, tasks));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            notices.Add(item.Path + ": " + e.Message);
            sourceNotices.Add(new(source.Id, item.Path + ": " + e.Message));
        }
    }

    public static ChangeInfo Resolve(CatalogSnapshot catalog, string sourceId, string name, string? variant = null)
    {
        var candidates = catalog.Changes.Where(c => c.SourceId == sourceId && (c.Name == name || c.DirectoryName == name)
            && (variant is null || c.Variant == variant)).ToArray();
        if (candidates.Length == 0) throw new FileNotFoundException("Change is unavailable. Refresh its source or select another checkout.");
        if (candidates.Length != 1) throw new DashboardConflictException("Several documents match this change. Select a source copy.");
        return candidates[0] with { Name = name };
    }

    private static string ErrorText(JsonElement root, string fallback)
    {
        if (root.TryGetProperty("error", out var error))
        {
            if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message)) return message.GetString() ?? fallback;
            return error.ToString();
        }
        return "OpenSpec source is unavailable. " + fallback;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}-")] private static partial Regex ArchivePrefix();
    [GeneratedRegex(@"(?m)^\s*store\s*:")] private static partial Regex StoreDeclaration();
}

internal sealed class DashboardConflictException(string message) : Exception(message);
