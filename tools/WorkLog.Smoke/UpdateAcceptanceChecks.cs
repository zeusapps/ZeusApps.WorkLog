using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using WorkLog.Cli;
using WorkLog.Configuration;
using WorkLog.Infrastructure;
using WorkLog.Journaling;
using WorkLog.Updates;

namespace WorkLog.Smoke;

internal sealed class UpdateAcceptanceChecks(string executable)
{
    private readonly string root = Path.GetFullPath(Path.Combine("artifacts", "update-smoke", Guid.NewGuid().ToString("N")));

    public async Task RunAsync()
    {
        var target = Path.Combine(root, "install");
        var data = Path.Combine(root, "data");
        var payload = Path.Combine(root, "package");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(payload);
        File.Copy(Path.GetFullPath(executable), Path.Combine(target, "worklog.exe"));
        File.Copy(Path.GetFullPath(executable), Path.Combine(payload, "worklog.exe"));
        File.WriteAllText(Path.Combine(target, "release.json"), Json.Encode(new ReleaseManifest("0.0.0", "win-x64", true)));
        File.WriteAllText(Path.Combine(payload, "release.json"), Json.Encode(new ReleaseManifest(ApplicationInfo.Version, "win-x64", true)));
        File.WriteAllText(Path.Combine(target, "keep.txt"), "unowned sentinel");
        foreach (var name in new[] { "README.md", "INSTALL.md" })
        {
            File.WriteAllText(Path.Combine(target, name), "old " + name);
            File.WriteAllText(Path.Combine(payload, name), "new " + name);
        }
        var settings = new LocalSettings(data);
        settings.Configuration.Updates.Enabled = false;
        settings.Save();
        var config = File.ReadAllText(settings.ConfigPath);
        var logged = new Journal(settings, new("update-fixture")).Log(new("in_progress", "update-smoke", OccurredAt: "2026-10-20T09:00:00Z"));
        SqliteConnection.ClearAllPools();
        var stage = Path.Combine(data, "updates", "staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var archive = Path.Combine(stage, ReleaseInfo.PackageName(ApplicationInfo.Version));
        ZipFile.CreateFromDirectory(payload, archive);
        File.WriteAllText(archive + ".sha256", ReleasePackage.Hash(archive) + "  " + Path.GetFileName(archive));

        // Stage in a child process so the real helper must wait for its actual parent to exit.
        await RunStageChildAsync(target, data, stage);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        UpdateOutcome? outcome;
        do
        {
            await Task.Delay(100, timeout.Token);
            outcome = UpdateInstaller.ReadOutcome(data, target);
        } while (outcome?.State is null or "staged");
        if (outcome.State != "installed") throw new InvalidOperationException("Packaged update failed: " + outcome.Message);
        if (ReleasePackage.ReadManifest(target).Version != ApplicationInfo.Version) throw new InvalidOperationException("Updated manifest mismatch.");
        await ReleasePackage.VerifyExecutableAsync(target, ApplicationInfo.Version, timeout.Token);
        if (File.ReadAllText(Path.Combine(target, "keep.txt")) != "unowned sentinel" || File.ReadAllText(settings.ConfigPath) != config)
            throw new InvalidOperationException("Update changed unowned files or private configuration.");
        var report = new Journal(new LocalSettings(data), new("update-fixture")).Query(new("2026-10-20T00:00:00Z", "2026-10-21T00:00:00Z", "UTC"));
        if (report.Entries.Single().Id != logged.Entry.Id) throw new InvalidOperationException("Update changed journal observations.");

        // A packaged server holds the shared installation lease and blocks another updater.
        var serverStart = new ProcessStartInfo(Path.Combine(target, "worklog.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { "serve", "--data-dir", data }) serverStart.ArgumentList.Add(argument);
        using var server = Process.Start(serverStart)!;
        var errors = server.StandardError.ReadToEndAsync();
        try
        {
            await server.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"update-smoke","version":"1.0"}}}""");
            var response = await server.StandardOutput.ReadLineAsync(timeout.Token);
            if (response is null) throw new InvalidOperationException("Packaged server did not start: " + await errors);
            try
            {
                using var unexpected = UpdateInstallation.Acquire(target);
                throw new InvalidOperationException("Updater acquired a busy installation.");
            }
            catch (IOException) { }
            server.StandardInput.Close();
            await server.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            if (!server.HasExited) { server.Kill(); await server.WaitForExitAsync(); }
        }
        await errors;
        Console.WriteLine("PACKAGED UPDATE PASSED: helper handoff, executable replacement, journal/config preservation, active-server refusal. Artifacts: " + root);
    }

    private static async Task RunStageChildAsync(string target, string data, string stage)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { typeof(UpdateAcceptanceChecks).Assembly.Location, "--stage-update-fixture", target, data, stage, ApplicationInfo.Version })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        await process.WaitForExitAsync(timeout.Token);
        if (process.ExitCode != 0) throw new InvalidOperationException("Update staging child failed: " + await errors);
        await output;
        await errors;
    }
}
