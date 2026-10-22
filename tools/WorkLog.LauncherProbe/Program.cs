using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

if (args is [var controller, var launcher, var report] && controller is "--console-controller" or "--limited-controller")
{
    // Started in an independent hidden console by the harness. CTRL+C never reaches the user's console.
    Console.CancelKeyPress += (_, eventArgs) => eventArgs.Cancel = true;
    var limited = controller == "--limited-controller";
    using var outerJob = Native.EnterOuterJob(limited);
    var start = new ProcessStartInfo(launcher) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
    start.ArgumentList.Add("--wait-cancel");
    using var child = Process.Start(start)!;
    var errors = child.StandardError.ReadToEndAsync();
    try
    {
        if (!limited)
        {
            if (await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) != "ready")
                throw new IOException("Cancellation child did not become ready.");
            if (!Native.GenerateConsoleCtrlEvent(0, 0))
                throw new IOException("Could not generate isolated console cancellation.");
        }
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            exitCode = child.ExitCode,
            error = await errors
        }));
        return child.ExitCode == (limited ? 1 : 23) ? 0 : 1;
    }
    finally { if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); } }
}
if (args is ["--wait-cancel"])
{
    var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; stopped.TrySetResult(); };
    Console.WriteLine("ready");
    await stopped.Task.WaitAsync(TimeSpan.FromSeconds(15));
    return 23;
}
Console.Error.WriteLine(JsonSerializer.Serialize(new { args, directory = Environment.CurrentDirectory, environment = Environment.GetEnvironmentVariable("WORKLOG_PROBE_VALUE") }));
await Console.OpenStandardInput().CopyToAsync(Console.OpenStandardOutput());
return 7;

internal static class Native
{
    internal static SafeFileHandle EnterOuterJob(bool limited)
    {
        var job = CreateJobObjectW(0, null);
        var information = Marshal.AllocHGlobal(144); // JOBOBJECT_EXTENDED_LIMIT_INFORMATION, Windows x64 fixture
        try
        {
            Marshal.Copy(new byte[144], 0, information, 144);
            if (limited)
            {
                Marshal.WriteInt32(information, 16, 8);
                Marshal.WriteInt32(information, 40, 2);
            }
            if (job.IsInvalid || !SetInformationJobObject(job, 9, information, 144) || !AssignProcessToJobObject(job, -1))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return job;
        }
        catch { job.Dispose(); throw; }
        finally { Marshal.FreeHGlobal(information); }
    }
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern SafeFileHandle CreateJobObjectW(nint security, string? name);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(SafeFileHandle job, int infoClass, nint info, uint length);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, nint process);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GenerateConsoleCtrlEvent(uint controlEvent, uint processGroup);
}
