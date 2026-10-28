using WorkLog.Installation;
using WorkLog.Repositories;

namespace WorkLog.DevInstall;

/// <summary>
/// A package is stale when its commit is not the checkout's HEAD, when a build input or one of its directories changed after the package build
/// started (a directory's time moves on delete, rename and add, which file times alone miss), or, for a package built from a clean tree, when
/// <c>git status</c> reports any change to a build input (this catches deletes and timestamp-preserving copies).
/// This is the single list of stale inputs: <see cref="InputPaths"/> and the directory walk. It is deliberately narrower than the dirty-tree set in
/// <c>scripts/package.ps1</c> (src, tests, tools, scripts, dashboard, docs and the root files), which only labels a build "dirty"; tests, tools and
/// scripts do not change what is packaged. The Git check lists every input whether or not it still exists, so a clean package also detects a deleted
/// individually-listed file (README.md and similar); a dirty package skips that check and cannot detect such a deletion.
/// </summary>
public static class Staleness
{
    private static readonly string[] ExcludedDirectories = ["bin", "obj", "node_modules", "dist", ".angular", "TestResults", "test-results", "playwright-report", "out-tsc", "e2e"];
    private static readonly string[] SourceFiles = ["Directory.Build.props", "global.json", "README.md", "docs/installation.md",
        "dashboard/package.json", "dashboard/package-lock.json", "dashboard/angular.json"];
    private static readonly string[] SourceDirectories = ["src", "dashboard/src", "dashboard/scripts"];

    /// <summary>The repository-relative paths that feed the build, as Git pathspecs (the full list, existing or not, so a deleted input shows in <c>git status</c>), with excluded directories and spec files removed.</summary>
    public static IReadOnlyList<string> InputPaths(string repository)
    {
        var paths = new List<string>(SourceFiles.Concat(SourceDirectories)) { ":(glob)dashboard/tsconfig*.json" };
        paths.Add(":(exclude,glob)**/*.spec.ts");
        paths.AddRange(ExcludedDirectories.Select(name => $":(exclude,glob)**/{name}/**"));
        return paths;
    }

    public static string? Check(string repository, BuildMarker marker)
    {
        var head = GitRepository.Run(repository, "rev-parse", "HEAD").Trim();
        if (!string.Equals(head, marker.Commit, StringComparison.OrdinalIgnoreCase) && !(marker.Commit!.Length < 40 && head.StartsWith(marker.Commit, StringComparison.OrdinalIgnoreCase)))
            return $"The package was built from commit {marker.Commit![..7]} but the checkout is at {head[..7]}. Build a fresh package.";
        var builtAt = DateTimeOffset.Parse(marker.BuiltAt!, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal).AddSeconds(1);
        var newer = NewestSource(repository);
        if (newer is { } file && file.Time > builtAt)
            return $"Source {(Directory.Exists(file.Path) ? "directory" : "file")} {Path.GetRelativePath(repository, file.Path)} changed after the package was built. Build a fresh package.";
        if (!marker.Dirty && GitRepository.Run(repository, ["status", "--porcelain", "--untracked-files=normal", "--", .. InputPaths(repository)]).Trim().Length > 0)
            return "The package was built from a clean tree, but build inputs have uncommitted changes (git status over the stale inputs is not empty). Build a fresh package.";
        return null;
    }

    private static (string Path, DateTimeOffset Time)? NewestSource(string repository)
    {
        (string, DateTimeOffset)? newest = null;
        void Consider(string path)
        {
            var time = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            if (newest is null || time > newest.Value.Item2)
                newest = (path, time);
        }
        foreach (var relative in SourceFiles)
            if (File.Exists(Path.Combine(repository, relative)))
                Consider(Path.Combine(repository, relative));
        foreach (var root in new[] { "src", "dashboard/src", "dashboard/scripts" })
            Walk(Path.Combine(repository, root), Consider);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(repository, "dashboard"), "tsconfig*.json"))
            Consider(file);
        return newest;
    }

    private static void Walk(string directory, Action<string> consider)
    {
        if (!Directory.Exists(directory))
            return;
        consider(directory);
        foreach (var file in Directory.EnumerateFiles(directory))
            if (!file.EndsWith(".spec.ts", StringComparison.Ordinal))
                consider(file);
        foreach (var child in Directory.EnumerateDirectories(directory))
            if (!ExcludedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                Walk(child, consider);
    }
}
