using System.ComponentModel;
using System.Diagnostics;

namespace WorkLog.Dashboard;

internal sealed record CommandOutput(bool Available, int ExitCode, string Output, string Error);

internal class LocalCommand
{
    public virtual async Task<CommandOutput> RunAsync(string executable, string directory, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (OperatingSystem.IsWindows() && executable == "openspec")
        {
            var shim = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                .Select(p => Path.Combine(p.Trim('"'), "node_modules", "@fission-ai", "openspec", "bin", "openspec.js"))
                .FirstOrDefault(File.Exists);
            if (shim is null) return new(false, -1, "", "OpenSpec CLI is unavailable on PATH.");
            start.FileName = "node";
            start.ArgumentList.Add(shim);
        }
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (Exception e) when (e is Win32Exception or FileNotFoundException) { return new(false, -1, "", e.Message); }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return new(true, process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            return new(true, -1, "", "Local command timed out.");
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }
}
