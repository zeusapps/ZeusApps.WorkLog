using System.Text.RegularExpressions;

namespace WorkLog.Repositories;

public static class RepositoryIdentity
{
    public static string? FromRemote(string remote)
    {
        if (string.IsNullOrWhiteSpace(remote))
        {
            return null;
        }

        string identity;
        if (Uri.TryCreate(remote, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            identity = uri.Host.ToLowerInvariant() + uri.AbsolutePath.TrimEnd('/');
        }
        else if (Regex.Match(remote, @"^(?:[^@/]+@)?(?<host>[^:/]+):(?<path>.+)$") is { Success: true } match)
        {
            identity = match.Groups["host"].Value.ToLowerInvariant() + "/" + match.Groups["path"].Value.TrimEnd('/');
        }
        else
        {
            identity = "path:" + Path.GetFullPath(remote);
        }

        return identity.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? identity[..^4] : identity;
    }
}
