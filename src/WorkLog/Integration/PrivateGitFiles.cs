using WorkLog.Repositories;

namespace WorkLog.Integration;

internal static class PrivateGitFiles
{
    public static void ValidateTargets(string root, string[] clients)
    {
        foreach (var relative in PrivateTargets(clients))
        {
            if (!string.IsNullOrWhiteSpace(GitRepository.Run(root, "ls-files", "--", relative)))
            {
                throw new InvalidOperationException($"Private integration target {relative} is tracked. Untrack it yourself or select another client; it was not modified.");
            }
        }
    }

    public static void PlanExclusions(InitializationPlan plan, string root, string[] clients)
    {
        var excludePath = GitRepository.Run(root, "rev-parse", "--git-path", "info/exclude").Trim();
        excludePath = Path.GetFullPath(excludePath, root);
        var exclusions = InitializationPlan.Read(excludePath);
        var paths = PrivateTargets(clients).Select(path => path == ".worklog/integration.json" ? "/.worklog/" : "/" + path);

        foreach (var path in paths)
        {
            if (!exclusions.Split('\n').Select(line => line.Trim()).Contains(path))
            {
                exclusions = exclusions.TrimEnd() + "\n" + path + "\n";
            }
        }

        plan.Add(excludePath, exclusions);
    }

    private static IEnumerable<string> PrivateTargets(string[] clients)
    {
        yield return ".worklog/integration.json";
        if (clients.Contains("codex"))
        {
            yield return ".codex/config.toml";
        }
        if (clients.Contains("claude"))
        {
            yield return "CLAUDE.local.md";
        }
    }
}
