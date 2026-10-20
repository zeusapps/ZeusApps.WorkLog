using System.Diagnostics;
using System.Security.Cryptography;
using WorkLog.Infrastructure;

namespace WorkLog.Updates;

public sealed class UpdateInstaller(ReleaseClient client)
{
    public async Task<UpdatePlan> PrepareAsync(string target, string dataDirectory, ReleaseInfo release, CancellationToken token = default)
    {
        UpdateInstallation.ValidateTarget(target, dataDirectory);
        if (!release.IsValid() || !release.IsNewerThan(ReleasePackage.ReadManifest(target).Version))
            throw new InvalidOperationException("Only a newer supported stable release can be installed.");
        UpdateInstallation.EnsureIdle(target, Environment.ProcessId);
        var stage = Path.Combine(Path.GetFullPath(dataDirectory), "updates", "staging", Guid.NewGuid().ToString("N"));
        UpdateInstallation.EnsureNoReparsePoints(stage);
        Directory.CreateDirectory(stage);
        var archive = Path.Combine(stage, ReleaseInfo.PackageName(release.Version));
        await client.DownloadAsync(release.PackageUrl, archive, ReleasePackage.MaximumPackageBytes, token);
        await client.DownloadAsync(release.ChecksumUrl, archive + ".sha256", 4096, token);
        return await PrepareDownloadedAsync(target, dataDirectory, release.Version, stage, token);
    }

    // The same verified staging path is used by packaged acceptance checks with controlled local downloads.
    public static async Task<UpdatePlan> PrepareDownloadedAsync(string target, string dataDirectory, string version, string stage, CancellationToken token = default)
    {
        target = Path.GetFullPath(target);
        dataDirectory = Path.GetFullPath(dataDirectory);
        stage = Path.GetFullPath(stage);
        UpdateInstallation.ValidateTarget(target, dataDirectory);
        var original = ReleasePackage.ReadManifest(target).Version;
        if (!new ReleaseInfo(version, "", "", "").IsNewerThan(original)) throw new InvalidOperationException("Update must be newer than the installed version.");
        var id = Path.GetFileName(stage);
        if (!Guid.TryParseExact(id, "N", out _) || stage != Path.Combine(dataDirectory, "updates", "staging", id))
            throw new InvalidOperationException("Invalid update staging directory.");
        UpdateInstallation.EnsureNoReparsePoints(stage);
        var archive = Path.Combine(stage, ReleaseInfo.PackageName(version));
        var hash = ReleasePackage.VerifyChecksum(archive, archive + ".sha256", ReleaseInfo.PackageName(version));
        var payload = Path.Combine(stage, "payload");
        ReleasePackage.Extract(archive, payload, version);
        await ReleasePackage.VerifyExecutableAsync(payload, version, token);
        using var parent = Process.GetCurrentProcess();
        var plan = new UpdatePlan(id, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), target, dataDirectory,
            original, version, hash, ReleasePackage.Hash(Path.Combine(target, "worklog.exe")),
            Environment.ProcessId, parent.StartTime.ToUniversalTime(), DateTimeOffset.UtcNow);
        Directory.CreateDirectory(Path.GetDirectoryName(plan.HelperPath)!);
        File.Copy(Path.Combine(target, "worklog.exe"), plan.HelperPath, overwrite: false);
        AtomicFile.Write(plan.PlanPath, Json.Encode(plan));
        WriteOutcome(plan, new("staged", version,
            "Verified package staged. Installation is not complete. Stop affected MCP clients, then check worklog update --status. Restart clients after installation and rerun init where instructions need refreshing."));
        return plan;
    }

    public static void StartHelper(UpdatePlan plan)
    {
        var start = new ProcessStartInfo(plan.HelperPath)
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = plan.StageDirectory
        };
        foreach (var argument in new[] { "__apply-update", "--plan", plan.PlanPath, "--token", plan.Token }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Could not start update helper.");
    }

    public static void WriteOutcome(UpdatePlan plan, UpdateOutcome outcome) => AtomicFile.Write(plan.StatusPath, Json.Encode(outcome));

    public static UpdateOutcome? ReadOutcome(string dataDirectory, string target)
    {
        var path = UpdatePlan.StatusFile(dataDirectory, target);
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 16 * 1024) throw new IOException("Invalid update status file.");
        return Json.Decode<UpdateOutcome>(File.ReadAllText(path));
    }
}
