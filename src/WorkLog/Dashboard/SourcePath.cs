namespace WorkLog.Dashboard;

internal static class SourcePath
{
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Canonical(string path)
    {
        var absolute = Path.GetFullPath(path);
        var root = Path.GetPathRoot(absolute)!;
        var current = root;
        foreach (var part in absolute[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null) current = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                ?? throw new IOException("Cannot resolve filesystem link.");
        }
        return Path.TrimEndingDirectorySeparator(current);
    }

    public static string Within(string root, string relative)
    {
        relative = Uri.UnescapeDataString(relative).Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relative) || relative.Contains(':') || relative.Split(Path.DirectorySeparatorChar).Any(p => p == ".."))
            throw new ArgumentException("Path escapes the selected source.");
        var canonicalRoot = Canonical(root);
        var candidate = Canonical(Path.Combine(canonicalRoot, relative));
        if (!candidate.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, Comparison))
            throw new ArgumentException("Path escapes the selected source.");
        return candidate;
    }

    public static IEnumerable<string> MarkdownFiles(string root, string directory)
    {
        var stack = new Stack<string>();
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        stack.Push(directory);
        while (stack.TryPop(out var current))
        {
            var safe = Within(root, Path.GetRelativePath(root, current));
            if (!seen.Add(safe)) continue;
            foreach (var file in Directory.EnumerateFiles(safe).Order(StringComparer.Ordinal))
                if (Path.GetExtension(file).Equals(".md", StringComparison.OrdinalIgnoreCase))
                    yield return Within(root, Path.GetRelativePath(root, file));
            foreach (var child in Directory.EnumerateDirectories(safe).OrderDescending(StringComparer.Ordinal))
                stack.Push(child);
        }
    }
}
