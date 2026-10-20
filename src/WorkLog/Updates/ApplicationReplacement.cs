namespace WorkLog.Updates;

public sealed class ApplicationReplacement(Func<string, string, CancellationToken, Task>? verify = null)
{
    private readonly Func<string, string, CancellationToken, Task> verifyExecutable = verify ?? ReleasePackage.VerifyExecutableAsync;

    public async Task ApplyAsync(UpdatePlan plan, string verified, CancellationToken token = default)
    {
        UpdateInstallation.EnsureNoReparsePoints(plan.TargetDirectory);
        UpdateInstallation.EnsureNoReparsePoints(verified);
        var incoming = Directory.GetFiles(verified).Select(Path.GetFileName).Cast<string>().ToArray();
        if (incoming.Any(name => !ReleasePackage.ManagedFiles.Contains(name, StringComparer.Ordinal)))
            throw new IOException("Unexpected replacement file.");
        var existing = incoming.Where(name => File.Exists(Path.Combine(plan.TargetDirectory, name))).ToArray();
        // Check every target before the first mutation, including loaded executable image locks.
        foreach (var name in existing)
        {
            var path = Path.Combine(plan.TargetDirectory, name);
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Target file is a reparse point.");
            using var probe = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        Directory.CreateDirectory(plan.BackupDirectory);
        foreach (var name in existing) File.Copy(Path.Combine(plan.TargetDirectory, name), Path.Combine(plan.BackupDirectory, name), overwrite: false);

        var changed = new List<string>();
        try
        {
            foreach (var name in incoming)
            {
                var destination = Path.Combine(plan.TargetDirectory, name);
                var temporary = destination + "." + plan.Id + ".new";
                try
                {
                    File.Copy(Path.Combine(verified, name), temporary, overwrite: false);
                    File.Move(temporary, destination, overwrite: true);
                    changed.Add(name);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            await verifyExecutable(plan.TargetDirectory, plan.TargetVersion, token);
        }
        catch (Exception failure)
        {
            var recoveryErrors = new List<string>();
            foreach (var name in changed.AsEnumerable().Reverse())
            {
                try
                {
                    var destination = Path.Combine(plan.TargetDirectory, name);
                    if (existing.Contains(name)) File.Copy(Path.Combine(plan.BackupDirectory, name), destination, overwrite: true);
                    else File.Delete(destination);
                }
                catch (Exception recovery) when (recovery is IOException or UnauthorizedAccessException) { recoveryErrors.Add(recovery.Message); }
            }
            throw new IOException("Update failed: " + failure.Message + (recoveryErrors.Count == 0 ? " Prior application files restored." :
                " Restore application files manually from " + plan.BackupDirectory + ": " + string.Join("; ", recoveryErrors)), failure);
        }
    }
}
