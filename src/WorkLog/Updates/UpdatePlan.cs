using WorkLog.Infrastructure;

namespace WorkLog.Updates;

public sealed record UpdatePlan(string Id, string Token, string TargetDirectory, string DataDirectory,
    string OriginalVersion, string TargetVersion, string ArchiveHash, string OriginalExecutableHash,
    int ParentId, DateTime ParentStartedUtc, DateTimeOffset CreatedAt)
{
    public string StageDirectory => Path.Combine(DataDirectory, "updates", "staging", Id);
    public string PlanPath => Path.Combine(StageDirectory, "plan.json");
    public string ArchivePath => Path.Combine(StageDirectory, ReleaseInfo.PackageName(TargetVersion));
    public string ChecksumPath => ArchivePath + ".sha256";
    public string PayloadDirectory => Path.Combine(StageDirectory, "payload");
    public string BackupDirectory => Path.Combine(StageDirectory, "backup");
    public string HelperPath => Path.Combine(StageDirectory, "helper", "worklog.exe");
    public string StatusPath => StatusFile(DataDirectory, TargetDirectory);

    public static string StatusFile(string dataDirectory, string target) => Path.Combine(dataDirectory, "updates", "status-"
        + ContentHash.Compute(Path.GetFullPath(target).ToUpperInvariant()) + ".json");
}

public sealed record UpdateOutcome(string State, string Version, string Message, string? RecoveryDirectory = null, string? OperationId = null);
