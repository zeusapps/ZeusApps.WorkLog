using System.Diagnostics;

namespace WorkLog.Repositories;

public static class GitRepository
{
    public static string GetRoot(string path) =>
        Path.GetFullPath(Run(path, "rev-parse", "--show-toplevel").Trim());

    public static string GetCommonDirectory(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(Run(path, "rev-parse", "--git-common-dir").Trim(), path));

    internal static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static string? TryGetCommonDirectory(string path)
    {
        if (!Directory.Exists(path)) return null;
        var result = Run(path, false, "rev-parse", "--git-common-dir").Trim();
        return result.Length == 0 ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(result, path));
    }

    public static string Run(string path, params string[] arguments) => Run(path, true, arguments);

    public static string Run(string path, bool required, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new IOException("Cannot start git.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        if (required && process.ExitCode != 0)
        {
            throw new ArgumentException("Git repository required: " + error.GetAwaiter().GetResult().Trim());
        }

        return output.GetAwaiter().GetResult();
    }
}
