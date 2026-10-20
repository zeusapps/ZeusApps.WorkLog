using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using WorkLog.Cli;

namespace WorkLog.Updates;

public static class UpdateInstallation
{
    public static string CurrentDirectory => Path.GetDirectoryName(Environment.ProcessPath
        ?? throw new InvalidOperationException("Cannot identify the executable."))!;

    [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "An empty Assembly.Location is deliberately used to identify a bundled single-file release.")]
    public static string ValidateCurrent(string dataDirectory)
    {
        var executable = Environment.ProcessPath ?? "";
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess || Path.GetFileName(executable) != "worklog.exe"
            || !string.IsNullOrEmpty(typeof(ApplicationInfo).Assembly.Location))
            throw new InvalidOperationException("Self-update requires an installed self-contained Windows x64 ZIP release. Download a release and follow " + ReleaseInfo.ReleasesUrl + ".");
        var target = CurrentDirectory;
        ValidateTarget(target, dataDirectory);
        if (ReleasePackage.ReadManifest(target).Version != ApplicationInfo.Version)
            throw new InvalidOperationException("Installed manifest differs from the running version. Reinstall the official ZIP manually.");
        return target;
    }

    public static void ValidateTarget(string target, string dataDirectory)
    {
        target = Path.GetFullPath(target);
        dataDirectory = Path.GetFullPath(dataDirectory);
        if (target == Path.GetPathRoot(target) || Contains(target, dataDirectory) || Contains(dataDirectory, target))
            throw new InvalidOperationException("Keep the application installation and journal data in separate directories.");
        EnsureNoReparsePoints(target);
        EnsureNoReparsePoints(dataDirectory);
        _ = ReleasePackage.ReadManifest(target);
        if (!File.Exists(Path.Combine(target, "worklog.exe"))) throw new IOException("Installed worklog.exe is missing.");
    }

    public static void EnsureNoReparsePoints(string path)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(path)); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Update paths must not contain symbolic links or reparse points: " + current.FullName);
    }

    public static bool Contains(string parent, string child) => Path.GetFullPath(child).Equals(Path.GetFullPath(parent), StringComparison.OrdinalIgnoreCase)
        || Path.GetFullPath(child).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public static FileStream Acquire(string target)
    {
        try
        {
            EnsureNoReparsePoints(target);
            return new FileStream(Path.Combine(target, ".worklog-update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException("Another update is running, or the installation is not writable. Retry when idle or reinstall manually.", exception);
        }
    }

    public static void EnsureIdle(string target, params int[] allowedProcessIds)
    {
        var executable = Path.Combine(Path.GetFullPath(target), "worklog.exe");
        foreach (var process in Process.GetProcessesByName("worklog"))
        {
            using (process)
            {
                if (allowedProcessIds.Contains(process.Id)) continue;
                try
                {
                    if (process.MainModule?.FileName.Equals(executable, StringComparison.OrdinalIgnoreCase) == true)
                        throw new IOException("WorkLog is in use. Stop MCP clients using this installation and retry; no processes were stopped.");
                }
                catch (Win32Exception) { /* Exclusive file checks below also detect inaccessible users' loaded images. */ }
                catch (InvalidOperationException) { /* A process may exit during enumeration. */ }
            }
        }
    }
}
