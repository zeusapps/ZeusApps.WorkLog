using System.Diagnostics;
using System.Text.Json;

namespace WorkLog.Smoke;

internal sealed record SmokeProcessResult(int ExitCode, string Output, string Error, string LogPath);

internal sealed class SmokeProcessRunner(SmokeWorkspace workspace)
{
    public async Task<string> RunAsync(string name, string[] arguments, Dictionary<string, string>? environment = null,
        string? workingDirectory = null)
    {
        var result = await RunDetailedAsync(name, arguments, environment, workingDirectory);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{name} exited {result.ExitCode}: {result.Error}\n{result.Output}\nLog: {result.LogPath}");
        return result.Output;
    }

    public async Task<SmokeProcessResult> RunDetailedAsync(string name, string[] arguments,
        Dictionary<string, string>? environment = null, string? workingDirectory = null)
    {
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() && name == "codex" ? "codex.cmd" : name)
        {
            WorkingDirectory = workingDirectory ?? workspace.Repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        // Explicit working directories must never fall back to the developer's client profiles.
        start.Environment["CODEX_HOME"] = workspace.CodexHome;
        start.Environment["CLAUDE_CONFIG_DIR"] = workspace.Home;
        start.Environment["WORKLOG_DATA_DIR"] = workspace.Data;
        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                start.Environment[key] = value;
            }
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start " + name);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(name + " smoke test timed out.");
        }

        var stdout = await output;
        var stderr = await error;
        var log = Path.Combine(workspace.Root, Path.GetFileName(name) + "-" + Guid.NewGuid().ToString("N") + ".log");
        File.WriteAllText(log,
            $"Command: {name}\nArguments: {JsonSerializer.Serialize(arguments)}\nWorking directory: {start.WorkingDirectory}\nExit code: {process.ExitCode}\n\n{stdout}\n{stderr}");
        return new(process.ExitCode, stdout, stderr, log);
    }
}
