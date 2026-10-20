using System.Diagnostics;
using WorkLog.Infrastructure;

namespace WorkLog.Updates;

public static class UpdateHelper
{
    public static async Task<int> ApplyAsync(string planPath, string token, CancellationToken cancellationToken = default)
    {
        UpdatePlan? plan = null;
        try
        {
            if (!OperatingSystem.IsWindows() || !File.Exists(planPath) || new FileInfo(planPath).Length > 16 * 1024)
                throw new InvalidOperationException("Invalid update plan.");
            var candidate = Json.Decode<UpdatePlan>(File.ReadAllText(planPath));
            ValidatePlan(candidate, planPath, token);
            plan = candidate;
            await WaitForParentAsync(plan, cancellationToken);
            using var lease = UpdateInstallation.Acquire(plan.TargetDirectory);
            UpdateInstallation.ValidateTarget(plan.TargetDirectory, plan.DataDirectory);
            UpdateInstallation.EnsureIdle(plan.TargetDirectory);
            if (ReleasePackage.ReadManifest(plan.TargetDirectory).Version != plan.OriginalVersion
                || ReleasePackage.Hash(Path.Combine(plan.TargetDirectory, "worklog.exe")) != plan.OriginalExecutableHash)
                throw new IOException("Installation changed after staging. Retry from the current installed release.");
            var hash = ReleasePackage.VerifyChecksum(plan.ArchivePath, plan.ChecksumPath, ReleaseInfo.PackageName(plan.TargetVersion));
            if (hash != plan.ArchiveHash) throw new IOException("Staged archive changed after verification.");
            var verified = Path.Combine(plan.StageDirectory, "apply-payload");
            ReleasePackage.Extract(plan.ArchivePath, verified, plan.TargetVersion);
            await ReleasePackage.VerifyExecutableAsync(verified, plan.TargetVersion, cancellationToken);
            await new ApplicationReplacement().ApplyAsync(plan, verified, cancellationToken);
            UpdateInstaller.WriteOutcome(plan, new("installed", plan.TargetVersion,
                "Update installed and verified. Restart MCP clients; rerun worklog init in repositories needing refreshed instructions.", plan.BackupDirectory));
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (plan is not null)
            {
                try { UpdateInstaller.WriteOutcome(plan, new("failed", plan.TargetVersion, exception.Message
                    + " Installation was not completed. Keep recovery files and retry or follow the manual ZIP upgrade instructions.", plan.BackupDirectory)); }
                catch (Exception statusException) when (statusException is IOException or UnauthorizedAccessException) { }
            }
            Console.Error.WriteLine("worklog update: " + exception.Message);
            return 1;
        }
    }

    private static void ValidatePlan(UpdatePlan plan, string path, string token)
    {
        if (!Guid.TryParseExact(plan.Id, "N", out _) || token.Length != 64 || token != plan.Token
            || Path.GetFullPath(path) != plan.PlanPath || plan.DataDirectory != Path.GetFullPath(plan.DataDirectory)
            || plan.TargetDirectory != Path.GetFullPath(plan.TargetDirectory)
            || DateTimeOffset.UtcNow - plan.CreatedAt > TimeSpan.FromMinutes(15) || plan.CreatedAt > DateTimeOffset.UtcNow
            || ReleaseInfo.ParseVersion(plan.OriginalVersion) is null || ReleaseInfo.ParseVersion(plan.TargetVersion) is null
            || !new ReleaseInfo(plan.TargetVersion, "", "", "").IsNewerThan(plan.OriginalVersion)
            || !string.Equals(Environment.ProcessPath, plan.HelperPath, StringComparison.OrdinalIgnoreCase)
            || ReleasePackage.Hash(plan.HelperPath) != plan.OriginalExecutableHash)
            throw new InvalidOperationException("Invalid, expired, or mismatched update plan/helper.");
        UpdateInstallation.EnsureNoReparsePoints(plan.StageDirectory);
        UpdateInstallation.ValidateTarget(plan.TargetDirectory, plan.DataDirectory);
    }

    private static async Task WaitForParentAsync(UpdatePlan plan, CancellationToken token)
    {
        try
        {
            using var parent = Process.GetProcessById(plan.ParentId);
            if (parent.StartTime.ToUniversalTime() != plan.ParentStartedUtc) return;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await parent.WaitForExitAsync(timeout.Token);
        }
        catch (ArgumentException) { /* Original updater already exited. */ }
    }

}
