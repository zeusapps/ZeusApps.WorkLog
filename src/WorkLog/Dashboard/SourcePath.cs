using System.IO.Enumeration;
namespace WorkLog.Dashboard;

internal static class SourcePath
{
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Longest chain of links <see cref="Canonical"/> follows before it fails closed.</summary>
    private const int MaxLinkHops = 40;

    private static string[] Components(string path, int rootLength) =>
        path[rootLength..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

    public static string Canonical(string path)
    {
        var absolute = Path.GetFullPath(path);
        var root = Path.GetPathRoot(absolute)!;
        var current = root;
        var pending = new Stack<string>(Components(absolute, root.Length).Reverse());
        var hops = 0;
        while (pending.TryPop(out var part))
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is null) continue;
            // On Unix the final target can still contain links in its own parent components, so the walk restarts at the
            // target followed by the components that remain; a loop or an excessive chain fails closed.
            if (++hops > MaxLinkHops) throw new IOException("Too many levels of filesystem links.");
            var resolved = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                ?? throw new IOException("Cannot resolve filesystem link.");
            resolved = Path.GetFullPath(resolved);
            var resolvedRoot = Path.GetPathRoot(resolved)!;
            current = resolvedRoot;
            foreach (var component in Components(resolved, resolvedRoot.Length).Reverse()) pending.Push(component);
        }
        return Path.TrimEndingDirectorySeparator(current);
    }

    public static string Within(string root, string relative) => Within(root, Canonical(root), relative);

    /// <summary>Resolves <paramref name="relative"/> below a root that the caller already canonicalized, so the root is not walked again.</summary>
    public static string Within(string root, string canonicalRoot, string relative)
    {
        relative = Uri.UnescapeDataString(relative).Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relative) || relative.Contains(':') || relative.Split(Path.DirectorySeparatorChar).Any(p => p == ".."))
            throw new ArgumentException("Path escapes the selected source.");
        var candidate = Canonical(Path.Combine(canonicalRoot, relative));
        if (!candidate.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, Comparison))
            throw new ArgumentException("Path escapes the selected source.");
        return candidate;
    }

    /// <summary>A directory entry with the metadata the enumeration already returned, so no second stat is needed.</summary>
    internal readonly record struct Item(string Path, bool IsDirectory, bool IsLink, long Length, long WriteTicks);

    internal sealed record MarkdownFile(string Path, string Stamp);

    public static List<Item> List(string directory)
    {
        var options = new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = false, RecurseSubdirectories = false };
        return new FileSystemEnumerable<Item>(directory, (ref FileSystemEntry e) => new Item(e.ToFullPath(), e.IsDirectory,
            (e.Attributes & FileAttributes.ReparsePoint) != 0, e.IsDirectory ? 0 : e.Length, e.LastWriteTimeUtc.UtcTicks), options).ToList();
    }

    /// <summary>
    /// Canonical path of an entry listed from a canonical directory. Only links can leave the root, so only they are walked;
    /// other entries are canonical because their parent is.
    /// </summary>
    public static string Contained(string canonicalRoot, Item item)
    {
        if (!item.IsLink) return item.Path;
        var candidate = Canonical(item.Path);
        if (!candidate.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, Comparison))
            throw new ArgumentException("Path escapes the selected source.");
        return candidate;
    }

    public static IEnumerable<MarkdownFile> MarkdownFiles(string canonicalRoot, string canonicalDirectory)
    {
        var stack = new Stack<string>();
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        stack.Push(canonicalDirectory);
        while (stack.TryPop(out var current))
        {
            if (!seen.Add(current)) continue;
            var items = List(current);
            foreach (var file in items.Where(i => !i.IsDirectory && System.IO.Path.GetExtension(i.Path).Equals(".md", StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Path, StringComparer.Ordinal))
            {
                if (!file.IsLink) { yield return new(file.Path, file.Length + "@" + file.WriteTicks); continue; }
                var target = Contained(canonicalRoot, file);
                var info = new FileInfo(target);
                yield return new(target, info.Exists ? info.Length + "@" + info.LastWriteTimeUtc.Ticks : "-");
            }
            foreach (var child in items.Where(i => i.IsDirectory).OrderByDescending(i => i.Path, StringComparer.Ordinal))
                stack.Push(Contained(canonicalRoot, child));
        }
    }
}
