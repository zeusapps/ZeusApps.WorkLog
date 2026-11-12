using System.Diagnostics;
using System.Text.Json;
using WorkLog.Launcher;

namespace WorkLog.Smoke;

/// <summary>Owns C06 probe descendants through the launcher's tested Windows kill-on-close job.</summary>
internal static class C06ProcessTree
{
    public static int RunOwned(string[] arguments)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("C06 process-tree smoke currently requires Windows.");
        if (arguments.Length == 0 || string.IsNullOrWhiteSpace(arguments[0]))
            throw new ArgumentException("An executable is required.");
        return WindowsChildProcess.Run(ResolveExecutable(arguments[0]), arguments[1..]);
    }

    private static string ResolveExecutable(string executable)
    {
        if (Path.IsPathFullyQualified(executable) || executable.Contains(Path.DirectorySeparatorChar) || executable.Contains(Path.AltDirectorySeparatorChar))
        {
            var fullPath = Path.GetFullPath(executable);
            return File.Exists(fullPath) ? fullPath : throw new FileNotFoundException("The owned C06 executable was not found.", fullPath);
        }

        var extensions = Path.HasExtension(executable) ? new[] { "" } : new[] { ".exe", ".com" };
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var extension in extensions)
            {
                var candidate = Path.GetFullPath(Path.Combine(directory, executable + extension));
                if (File.Exists(candidate))
                    return candidate;
            }
        throw new FileNotFoundException("The owned C06 executable could not be resolved from PATH.");
    }

    public static int RunHangingChild(string markerPath)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("C06 process-tree smoke currently requires Windows.");
        var processInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        processInfo.ArgumentList.Add("-NoProfile");
        processInfo.ArgumentList.Add("-Command");
        processInfo.ArgumentList.Add("Start-Sleep -Seconds 600");
        using var child = Process.Start(processInfo) ?? throw new IOException("Could not start the C06 controlled hanging child.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(markerPath))!);
        File.WriteAllText(markerPath, JsonSerializer.Serialize(new
        {
            wrapperPid = Environment.ProcessId,
            wrapperStartTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,
            childPid = child.Id,
            childStartTicks = child.StartTime.ToUniversalTime().Ticks
        }));
        child.WaitForExit();
        return child.ExitCode;
    }
}
