using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using WorkLog.Infrastructure;
using WorkLog.Installation;

namespace WorkLog.Smoke;

internal sealed class LauncherAcceptanceChecks(string executable, string probe)
{
    public async Task RunAsync()
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "launcher-smoke", Guid.NewGuid().ToString("N")));
        var install = Path.Combine(root, "install with spaces żółć");
        var source = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        Directory.CreateDirectory(install);
        var selection = InstallationLayout.ReadSelection(source);
        foreach (var name in new[] { "worklog.exe", "launcher.json", "README.md", "INSTALL.md" })
            File.Copy(Path.Combine(source, name), Path.Combine(install, name));
        var payload = InstallationLayout.PayloadDirectory(install, selection.Current.Version);
        Directory.CreateDirectory(payload);
        var manifest = InstallationLayout.ReadPayload(InstallationLayout.PayloadDirectory(source, selection.Current.Version));
        foreach (var name in manifest.Files.Keys)
            File.Copy(name == "worklog.exe" ? Path.GetFullPath(probe) : Path.Combine(InstallationLayout.PayloadDirectory(source, selection.Current.Version), name), Path.Combine(payload, name));
        var hashes = manifest.Files.Keys.ToDictionary(name => name, name => InstallationLayout.Hash(Path.Combine(payload, name)));
        AtomicFile.Write(Path.Combine(payload, "release.json"), Json.Encode(manifest with
        {
            Files = hashes
        }));
        selection = selection with
        {
            Current = new(selection.Current.Version, InstallationLayout.Hash(Path.Combine(payload, "release.json"))),
            Previous = null
        };
        AtomicFile.Write(Path.Combine(install, "current.json"), Json.Encode(selection));
        var entryPoint = Path.Combine(install, "worklog.exe");
        var directory = Path.Combine(root, "cwd with spaces żółć");
        Directory.CreateDirectory(directory);
        string[] arguments = ["space words", "", "quote\"inside", "żółć", "trailing\\", "backslash\\\"quote"];
        var start = new ProcessStartInfo(entryPoint) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment["WORKLOG_PROBE_VALUE"] = "inherited żółć";
        using (var child = Process.Start(start)!)
        {
            var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("bidirectional żółć 0123456789\r\n", 32768)));
            var output = new MemoryStream();
            var receive = child.StandardOutput.BaseStream.CopyToAsync(output);
            var errors = child.StandardError.ReadToEndAsync();
            var send = Task.Run(async () => { await child.StandardInput.BaseStream.WriteAsync(bytes); child.StandardInput.Close(); });
            try
            {
                await Task.WhenAll(send, receive, child.WaitForExitAsync()).WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch { if (!child.HasExited) child.Kill(entireProcessTree: true); throw; }
            if (child.ExitCode != 7 || !bytes.SequenceEqual(output.ToArray()))
                throw new IOException("Launcher byte-stream/EOF/exit forwarding failed.");
            var metadata = JsonDocument.Parse((await errors).Trim()).RootElement;
            if (!arguments.SequenceEqual(metadata.GetProperty("args").EnumerateArray().Select(value => value.GetString()))
                || metadata.GetProperty("directory").GetString() != directory || metadata.GetProperty("environment").GetString() != "inherited żółć")
                throw new IOException("Launcher changed argument boundaries, cwd, or environment.");
        }
        foreach (var mode in new[] { "--console-controller", "--limited-controller" })
        {
            var report = Path.Combine(root, mode.TrimStart('-') + ".json");
            await RunHiddenConsoleAsync(Path.GetFullPath(probe), entryPoint, report, mode, root);
            using var result = JsonDocument.Parse(File.ReadAllText(report));
            if (result.RootElement.GetProperty("exitCode").GetInt32() != (mode == "--console-controller" ? 23 : 1))
                throw new IOException("Isolated console cancellation or nested-job failure check failed.");
        }
        File.WriteAllText(Path.Combine(root, "evidence.json"), Json.Encode(new
        {
            windows = Environment.OSVersion.ToString(),
            byteCount = 32768 * Encoding.UTF8.GetByteCount("bidirectional żółć 0123456789\r\n"),
            arguments,
            cwd = directory,
            inheritedEnvironment = true,
            exitCode = 7,
            eof = true,
            cancellationInNestedJob = true,
            restrictedJobRefusal = true
        }));
        Console.WriteLine("LAUNCHER PASSED: exact argv/cwd/environment, concurrent byte streams/backpressure/EOF/stderr/exit, isolated CTRL+C and nested-job refusal. Artifacts: " + root);
    }

    private static async Task RunHiddenConsoleAsync(string probe, string launcher, string report, string mode, string root)
    {
        var info = new Startup { Size = (uint)Marshal.SizeOf<Startup>(), Flags = 1, Show = 0 };
        // All arguments are fixture file paths (Windows filenames cannot contain double quotes), not shell code.
        var command = new StringBuilder($"\"{probe}\" {mode} \"{launcher}\" \"{report}\"");
        if (!CreateProcessW(probe, command, 0, 0, false, 0x10, 0, root, ref info, out var created))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        using var processHandle = new SafeFileHandle(created.Process, true);
        using var threadHandle = new SafeFileHandle(created.Thread, true);
        using var process = Process.GetProcessById((int)created.ProcessId);
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        if (process.ExitCode != 0)
            throw new IOException("Hidden console fixture failed; report: " + report);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Startup
    {
        public uint Size;
        public nint Reserved, Desktop, Title;
        public uint X, Y, Width, Height, XChars, YChars, Fill, Flags;
        public ushort Show, ReservedSize;
        public nint ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo
    {
        public nint Process, Thread; public uint ProcessId, ThreadId;
    }
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateProcessW(string application, StringBuilder command, nint processSecurity, nint threadSecurity, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, nint environment, string directory, ref Startup startup, out ProcessInfo process);
}
