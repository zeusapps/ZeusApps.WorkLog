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
    string[] Repositories, ChangeDocument[] Documents, int? CompletedTasks, int? TotalTasks, string? Error, [property: JsonIgnore] string Directory);
internal sealed record CatalogSnapshot(DashboardRepository[] Repositories, DocumentSource[] Sources, ChangeInfo[] Changes,
    string? LaunchRepository, string Revision, string[] Notices);

internal sealed partial class SourceCatalog(LocalSettings settings, DashboardJournal journal, LocalCommand command, string? launchDirectory)
{
    private readonly SemaphoreSlim gate = new(1);
    private CatalogSnapshot? cached;
    private DateTimeOffset refreshed;

    public async Task<CatalogSnapshot> GetAsync(CancellationToken cancellationToken = default, bool force = false)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!force && cached is not null && DateTimeOffset.UtcNow - refreshed < TimeSpan.FromSeconds(2)) return cached;
            cached = await DiscoverAsync(cancellationToken);
            refreshed = DateTimeOffset.UtcNow;
            return cached;
        }
        finally { gate.Release(); }
    }

    private async Task<CatalogSnapshot> DiscoverAsync(CancellationToken token)
    {
        var repositories = new Dictionary<string, DashboardRepository>(StringComparer.Ordinal);
        var checkouts = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var notices = new List<string>();
        void AddRepository(string identity, string? checkout = null)
        {
            repositories.TryAdd(identity, new(identity, identity.StartsWith("local:", StringComparison.Ordinal) && checkout is not null
                ? Path.GetFileName(checkout) : identity));
            if (checkout is not null && Directory.Exists(checkout)) checkouts.TryAdd(Path.GetFullPath(checkout), identity);
        }
        try { foreach (var identity in journal.Repositories()) AddRepository(identity); }
        catch (Exception e) when (e is IOException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException) { notices.Add(e.Message); }
        foreach (var pair in settings.Configuration.RepositoryContexts)
        {
            AddRepository(pair.Value.Repository.Identity);
            if (!Directory.Exists(pair.Key)) continue;
            var result = await command.RunAsync("git", pair.Key, ["--git-dir", pair.Key, "worktree", "list", "--porcelain"], token);
            foreach (var line in result.Output.Split('\n'))
                if (line.StartsWith("worktree ", StringComparison.Ordinal)) AddRepository(pair.Value.Repository.Identity, line[9..].TrimEnd('\r'));
        }
        var promoted = settings.Configuration.RepositoryContexts.Values.SelectMany(c => c.LegacyPaths)
            .ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var pair in settings.Configuration.Repositories.Where(p => !promoted.Contains(p.Key)))
        {
            var directory = Path.GetFileName(pair.Key) == ".git" ? Path.GetDirectoryName(pair.Key)! : pair.Key;
            AddRepository(pair.Value.Identity, directory);
        }
        string? launchRepository = null;
        if (launchDirectory is not null && Directory.Exists(launchDirectory))
        {
            var git = await command.RunAsync("git", launchDirectory, ["rev-parse", "--show-toplevel", "--git-common-dir"], token);
            var lines = git.Output.Trim().Split('\n');
            var checkout = git.ExitCode == 0 && lines.Length > 0 ? Path.GetFullPath(lines[0].Trim()) : Path.GetFullPath(launchDirectory);
            if (!checkouts.TryGetValue(checkout, out launchRepository))
            {
                var common = lines.Length > 1 ? Path.GetFullPath(lines[1].Trim(), checkout) : "";
                launchRepository = settings.Configuration.RepositoryContexts.FirstOrDefault(p =>
                    string.Equals(Path.GetFullPath(p.Key), common, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).Value?.Repository.Identity;
                launchRepository ??= "source:" + ContentHash.Compute(checkout);
                AddRepository(launchRepository, checkout);
            }
        }
        var sources = new Dictionary<string, DocumentSource>(StringComparer.Ordinal);
        foreach (var checkout in checkouts.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            var resolution = await command.RunAsync("openspec", checkout.Key, ["context", "--json"], token);
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
                    if (root is null && (Directory.Exists(Path.Combine(checkout.Key, "openspec")) || resolution.ExitCode != 0))
                        error = ErrorText(context.RootElement, resolution.Error);
                }
                catch (JsonException) { error = "OpenSpec context could not be resolved: " + resolution.Error; }
            }
            else if (Directory.Exists(Path.Combine(checkout.Key, "openspec")))
            {
                var configPath = new[] { "config.yaml", "config.yml" }.Select(n => Path.Combine(checkout.Key, "openspec", n)).FirstOrDefault(File.Exists);
                var config = configPath is null ? "" : await File.ReadAllTextAsync(configPath, token);
                if (StoreDeclaration().IsMatch(config)) error = "OpenSpec CLI is unavailable; the configured store cannot be resolved.";
                else root = checkout.Key;
            }
            if (root is null && error is null) continue;
            var id = ContentHash.Compute(root is null ? checkout.Key : SourcePath.Canonical(root));
            if (sources.TryGetValue(id, out var previous))
                sources[id] = previous with { Repositories = previous.Repositories.Append(checkout.Value).Distinct().ToArray() };
            else sources[id] = new(id, [checkout.Value], root ?? checkout.Key, error, root ?? checkout.Key, storeId);
        }
        foreach (var repo in repositories.Values)
            if (!sources.Values.Any(s => s.Repositories.Contains(repo.Id))) notices.Add(repo.Label + ": no available OpenSpec source. Recorded history is still available.");
        var changes = new List<ChangeInfo>();
        foreach (var source in sources.Values.Where(s => s.Error is null))
        {
            try
            {
                var changeRoot = SourcePath.Within(source.Root, "openspec/changes");
                if (!Directory.Exists(changeRoot)) continue;
                var progress = await command.RunAsync("openspec", source.Root,
                    source.StoreId is null ? ["list", "--json"] : ["list", "--json", "--store", source.StoreId], token);
                var counts = new Dictionary<string, (int Complete, int Total)>(StringComparer.Ordinal);
                if (progress.Available && progress.ExitCode == 0)
                {
                    try
                    {
                        using var document = JsonDocument.Parse(progress.Output);
                        if (document.RootElement.TryGetProperty("changes", out var items))
                            foreach (var item in items.EnumerateArray())
                                if (item.TryGetProperty("completedTasks", out var done) && item.TryGetProperty("totalTasks", out var total)
                                    && done.TryGetInt32(out var d) && total.TryGetInt32(out var t) && d >= 0 && t >= d)
                                    counts[item.GetProperty("name").GetString()!] = (d, t);
                    }
                    catch (JsonException) { notices.Add(source.Location + ": OpenSpec task progress is unavailable."); }
                }
                foreach (var directory in Directory.EnumerateDirectories(changeRoot).Order(StringComparer.Ordinal))
                {
                    if (Path.GetFileName(directory) == "archive")
                    {
                        var archive = SourcePath.Within(source.Root, Path.GetRelativePath(source.Root, directory));
                        foreach (var archived in Directory.EnumerateDirectories(archive).Order(StringComparer.Ordinal))
                            AddChange(source, archived, true, counts, changes, notices);
                    }
                    else AddChange(source, directory, false, counts, changes, notices);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            { notices.Add(source.Location + ": " + e.Message); }
        }
        var revision = ContentHash.Compute(Json.Encode(changes.Select(c => new { c.SourceId, c.DirectoryName, c.Archived, c.Documents, c.CompletedTasks, c.TotalTasks })));
        return new(repositories.Values.OrderBy(r => r.Label, StringComparer.Ordinal).ToArray(), sources.Values.ToArray(), changes.ToArray(), launchRepository, revision, notices.ToArray());
    }

    private static void AddChange(DocumentSource source, string directory, bool archived, Dictionary<string, (int Complete, int Total)> counts,
        List<ChangeInfo> changes, List<string> notices)
    {
        try
        {
            var safe = SourcePath.Within(source.Root, Path.GetRelativePath(source.Root, directory));
            var name = Path.GetFileName(directory);
            var logical = archived ? ArchivePrefix().Replace(name, "", 1) : name;
            var documents = SourcePath.MarkdownFiles(source.Root, safe).Select(p => Path.GetRelativePath(safe, p).Replace('\\', '/'))
                .OrderBy(p => p == "design.md" ? 0 : p == "proposal.md" ? 1 : p == "tasks.md" ? 2 : 3).ThenBy(p => p, StringComparer.Ordinal)
                .Select(p => new ChangeDocument(p, p.EndsWith(".md", StringComparison.Ordinal) ? p[..^3] : p)).ToArray();
            var count = !archived && counts.TryGetValue(name, out var c) ? c : ((int Complete, int Total)?)null;
            changes.Add(new(source.Id, logical, name, ContentHash.Compute(Path.GetRelativePath(source.Root, safe)), archived, source.Repositories,
                documents, count?.Complete, count?.Total, null, safe));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { notices.Add(directory + ": " + e.Message); }
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
