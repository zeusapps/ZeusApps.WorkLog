using WorkLog.Repositories;

namespace WorkLog.Integration;

internal static class PrivateGitFiles
{
    public static void ValidateTargets(InitializationPlan plan, string root)
    {
        foreach (var relative in PrivateTargets(plan.ChangedPaths, root))
        {
            if (!string.IsNullOrWhiteSpace(GitRepository.Run(root, "ls-files", "--", relative)))
            {
                throw new InvalidOperationException($"Private integration target {relative} is tracked. Untrack it yourself or select another client; it was not modified.");
            }
        }
    }

    public static void PlanExclusions(InitializationPlan plan, string root)
    {
        var excludePath = GitRepository.Run(root, "rev-parse", "--git-path", "info/exclude").Trim();
        excludePath = Path.GetFullPath(excludePath, root);
        var exclusions = InitializationPlan.Read(excludePath);
        var paths = PrivateTargets(plan.TargetPaths, root).Select(path => path == ".worklog/integration.json" ? "/.worklog/" : "/" + path).ToArray();

        foreach (var path in paths)
        {
            if (!exclusions.Split('\n').Select(line => line.Trim()).Contains(path))
            {
                exclusions = exclusions.TrimEnd() + "\n" + path + "\n";
            }
        }

        plan.Add(excludePath, exclusions);
    }

    private static IEnumerable<string> PrivateTargets(IEnumerable<string> paths, string root)
    {
        foreach (var path in paths)
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative is ".worklog/integration.json" or ".codex/config.toml" or "CLAUDE.local.md")
                yield return relative;
        }
    }
}
