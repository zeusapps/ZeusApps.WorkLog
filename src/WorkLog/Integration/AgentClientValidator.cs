using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WorkLog.Integration;

internal static class AgentClientValidator
{
    public static void Check(string name)
    {
        var executable = OperatingSystem.IsWindows() && name == "codex" ? "codex.cmd" : name;
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("--version");

        try
        {
            using var process = Process.Start(start) ?? throw new IOException("Client did not start.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            var output = outputTask.GetAwaiter().GetResult();
            var error = errorTask.GetAwaiter().GetResult();
            var match = Regex.Match(output, @"\d+\.\d+\.\d+");
            var minimum = name == "codex" ? new Version(0, 162, 1) : new Version(2, 1, 289);

            if (process.ExitCode != 0 || !match.Success || Version.Parse(match.Value) < minimum)
            {
                throw new IOException($"{name} must be at least {minimum}; detected {output.Trim()} {error.Trim()}.");
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException)
        {
            throw new IOException($"Cannot initialize {name}: {exception.Message} Install/update the client or select the other client.", exception);
        }
    }
}
