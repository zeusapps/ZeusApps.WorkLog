using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using WorkLog.Infrastructure;
using WorkLog.Installation;
using WorkLog.Updates;

namespace WorkLog.DevInstall;

public sealed record DevOperation(string Id, string State, DateTimeOffset CreatedAt, string Package, string ArchiveHash, string Version,
    string BaseVersion, string Commit, bool Dirty, string Label, string InstallationRoot, string DataDirectory,
    PayloadSelection Original, PayloadSelection? Switched = null);

public sealed record PreflightResult(string OperationManifest, string Version, string Label, string BaseVersion, string Commit, bool Dirty,
    string Selected, string SelectedLabel, string Message);

/// <summary>Installs, rolls back and retires development builds using the product's own verification, publication and selection code.</summary>
public sealed partial class DevInstaller(DevTargets targets)
{
    private readonly PayloadInstaller installer = targets.SkipProbe ? new((_, _, _) => Task.CompletedTask) : new();

    public static string? NewestPackage(string repository)
    {
        var directory = Path.Combine(repository, "artifacts", "dev-builds");
        if (!Directory.Exists(directory))
            return null;
        return Directory.EnumerateFiles(directory, "worklog-*-win-x64.zip")
            .Select(path => (Path: path, Version: PackageVersion(path)))
            .Where(item => item.Version is not null)
            .OrderByDescending(item => InstallationLayout.ParseVersion(item.Version))
            .Select(item => item.Path).FirstOrDefault();
    }

    public static string? PackageVersion(string archive)
    {
        var match = PackageName().Match(Path.GetFileName(archive));
        return match.Success && InstallationLayout.ParseVersion(match.Groups[1].Value) is not null ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^worklog-(.+)-win-x64\.zip$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageName();

    /// <summary>Read-only toward the installation and data directory. Writes only the operation record in the workspace.</summary>
    public PreflightResult Preflight(string? package)
    {
        package = Path.GetFullPath(package ?? NewestPackage(targets.Repository) ?? throw new InvalidOperationException(
            "No development package found under artifacts/dev-builds. Run ./scripts/package.ps1 -Dev first."));
        var (version, manifest, hash) = InspectPackage(package);
        var marker = InstallationLayout.ValidMarker(version, manifest.Build)
            ?? throw new InvalidOperationException("The package is not a marked development build (missing or invalid build marker).");
        if (Staleness.Check(targets.Repository, marker) is { } stale)
            throw new InvalidOperationException(stale);
        var selection = InstallationLayout.ReadSelection(targets.InstallationRoot);
        var launcher = InstallationLayout.ReadLauncher(targets.InstallationRoot);
        if (manifest.LauncherContract != launcher.LauncherContract || manifest.SharedDataContract != launcher.SharedDataContract
            || manifest.JournalSchema != launcher.JournalSchema)
            throw new InvalidOperationException("The package contracts differ from the installation. Use the manual maintenance procedure.");
        if (InstallationLayout.ParseVersion(version) <= InstallationLayout.ParseVersion(selection.Current.Version))
            throw new InvalidOperationException($"The package version {version} is not above the selected version {selection.Current.Version}. Build a fresh package.");
        var existing = InstallationLayout.PayloadDirectory(targets.InstallationRoot, version);
        if (Directory.Exists(existing) && InstallationLayout.ReadPayload(existing).Version != version)
            throw new InvalidOperationException("The existing payload directory for this version is invalid.");
        var label = VersionDisplay.Format(version, marker);
        var operation = new DevOperation(Guid.NewGuid().ToString("N"), "preflighted", DateTimeOffset.UtcNow, package, hash, version,
            marker.BaseVersion!, marker.Commit!, marker.Dirty, label, targets.InstallationRoot, targets.DataDirectory, selection);
        var path = Path.Combine(targets.Workspace, operation.Id + ".json");
        Directory.CreateDirectory(targets.Workspace);
        AtomicFile.Write(path, Json.Encode(operation));
        return new(path, version, label, marker.BaseVersion!, marker.Commit!, marker.Dirty, selection.Current.Version,
            VersionDisplay.ForInstalled(targets.InstallationRoot, selection.Current.Version),
            "Preflight changed nothing in the installation or data directory. Switch selects the build for new processes only; running servers and the dashboard keep their payload until their next normal restart.");
    }

    public async Task<PayloadSelection> SwitchAsync(string operationManifest, CancellationToken token = default)
    {
        var (operation, path) = ReadOperation(operationManifest, "preflighted");
        var (version, manifest, hash) = InspectPackage(operation.Package);
        if (hash != operation.ArchiveHash || version != operation.Version)
            throw new InvalidOperationException("The package changed since Preflight. Run Preflight again.");
        var marker = InstallationLayout.ValidMarker(version, manifest.Build) ?? throw new InvalidOperationException("The package is no longer a marked development build.");
        if (Staleness.Check(targets.Repository, marker) is { } stale)
            throw new InvalidOperationException(stale);
        var current = InstallationLayout.ReadSelection(targets.InstallationRoot);
        if (current != operation.Original)
            throw new InvalidOperationException("The selection changed since Preflight. Nothing was changed; run Preflight again.");

        var stage = Path.Combine(targets.DataDirectory, "updates", "staging", Guid.NewGuid().ToString("N"));
        InstallationLayout.EnsureNoReparsePoints(stage);
        try
        {
            Directory.CreateDirectory(stage);
            var archive = Path.Combine(stage, ReleaseInfo.PackageName(version));
            File.Copy(operation.Package, archive);
            File.Copy(operation.Package + ".sha256", archive + ".sha256");
            // The running version for the comparison is the selected one: this tool has no running payload of its own.
            var prepared = await installer.PrepareDownloadedAsync(targets.InstallationRoot, targets.DataDirectory, current.Current.Version, version, stage, current, token);
            if (prepared.ArchiveHash != operation.ArchiveHash)
                throw new IOException("The staged package differs from the preflighted package.");
            var activated = await installer.ActivateAsync(prepared, token);
            if (activated.Current.Version != version || activated.OperationId != prepared.OperationId || activated.Previous != operation.Original.Current)
                throw new IOException("The build was not selected (the selection changed concurrently). Nothing was rolled back or removed.");
            AtomicFile.Write(path, Json.Encode(operation with
            {
                State = "switched",
                Switched = activated
            }));
            return activated;
        }
        finally
        {
            // The outcome record is final here, on success and on failure. Remove exactly the stage this operation created (about 250 MB each).
            DeleteStage(stage);
        }
    }

    private static void DeleteStage(string stage)
    {
        try
        {
            if (!Directory.Exists(stage))
                return;
            InstallationLayout.EnsureNoReparsePoints(stage);
            Directory.Delete(stage, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Cleanup must not mask the operation's own result or error.
            Console.Error.WriteLine($"Warning: the staging directory {stage} could not be removed: {exception.Message}");
        }
    }

    public async Task<PayloadSelection> RollbackAsync(string operationManifest, CancellationToken token = default)
    {
        var (operation, path) = ReadOperation(operationManifest, "switched");
        var switched = operation.Switched ?? throw new InvalidOperationException("The operation recorded no switch.");
        var current = InstallationLayout.ReadSelection(targets.InstallationRoot);
        if (current != switched)
            throw new InvalidOperationException("The selection changed since the switch; rollback is one level and refuses. Nothing was changed.");
        if (current.Previous != operation.Original.Current)
            throw new InvalidOperationException("The recorded previous payload is not the original selection. Nothing was changed.");
        var restored = await installer.RollbackAsync(targets.InstallationRoot, targets.DataDirectory, token, switched);
        AtomicFile.Write(path, Json.Encode(operation with
        {
            State = "rolledBack"
        }));
        return restored;
    }

    /// <summary>Removes one development-range payload that nothing needs. Returns the removed directory path.</summary>
    public async Task<string> RetireAsync(string version, CancellationToken token = default)
    {
        if (InstallationLayout.ParseVersion(version) is null)
            throw new InvalidOperationException("Expected a MAJOR.MINOR.PATCH version.");
        if (!InstallationLayout.IsDevVersion(version))
            throw new InvalidOperationException($"{version} is not in the development range (PATCH >= {InstallationLayout.DevPatchStart}); stable payloads are never retired.");
        var root = targets.InstallationRoot;
        using var lease = await PayloadInstaller.AcquirePublisherAsync(root, token);
        var selection = InstallationLayout.ReadSelection(root);
        if (selection.Current.Version == version)
            throw new InvalidOperationException($"{version} is the selected payload.");
        if (selection.Previous?.Version == version)
            throw new InvalidOperationException($"{version} is recorded as the previous payload (the rollback target), so it cannot be retired yet. Every selection change records the payload it leaves as previous and saves it in current.previous.json (the recovery selection); a payload becomes retirable only once it is neither. After it stopped being selected that takes one further selection change to another payload (a Switch or a stable update). Nothing was removed.");
        var backup = Path.Combine(root, "current.previous.json");
        if (File.Exists(backup) && InstallationLayout.Read<PayloadSelection>(backup).Current.Version == version)
            throw new InvalidOperationException($"{version} is the selection saved in current.previous.json (the recovery selection), so it cannot be retired yet. It becomes retirable once a further selection change to another payload (a Switch or a stable update) replaces both that file and the previous record. Nothing was removed.");
        var directory = InstallationLayout.PayloadDirectory(root, version);
        if (!Directory.Exists(directory))
            throw new InvalidOperationException($"Payload {version} is not installed.");
        InstallationLayout.EnsureNoReparsePoints(directory);
        RefuseWhenStaged(version);
        RefuseWhenInUse(Path.Combine(directory, "worklog.exe"));
        var manifest = InstallationLayout.ReadPayload(directory);
        if (manifest.Version != version)
            throw new InvalidOperationException("The payload manifest does not match its directory; nothing was removed.");
        // Rename first: a payload that is still open fails here and stays intact; a failed delete leaves a recoverable directory.
        var retired = Path.Combine(root, "versions", ".retired-" + version + "-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.Move(directory, retired);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Payload {version} could not be renamed, so it may be in use. Nothing was removed: {exception.Message}", exception);
        }
        try
        {
            Directory.Delete(retired, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Payload {version} was renamed to {Path.GetFileName(retired)} but could not be deleted completely: {exception.Message}. Delete it manually or rename it back.", exception);
        }
        return directory;
    }

    private void RefuseWhenStaged(string version)
    {
        var staging = Path.Combine(targets.DataDirectory, "updates", "staging");
        var outcome = UpdateInstaller.ReadOutcome(targets.DataDirectory, targets.InstallationRoot);
        if (outcome is { State: "staged" } && outcome.Version == version && outcome.OperationId is { } id
            && Directory.Exists(Path.Combine(staging, id)))
            throw new InvalidOperationException($"{version} is referenced by an unfinished update (staged, not yet activated or abandoned).");
        if (!Directory.Exists(staging))
            return;
        foreach (var stage in Directory.EnumerateDirectories(staging))
        {
            var plan = Path.Combine(stage, "plan.json");
            if (File.Exists(plan) && File.ReadAllText(plan).Contains($"\"{version}\"", StringComparison.Ordinal))
                throw new InvalidOperationException($"{version} is referenced by an unfinished legacy update plan.");
        }
    }

    private static void RefuseWhenInUse(string executable)
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (!process.ProcessName.Equals("worklog", StringComparison.OrdinalIgnoreCase))
                    continue;
                string? image = null;
                try
                {
                    image = process.MainModule?.FileName;
                }
                catch (Win32Exception)
                {
                    throw new InvalidOperationException($"Cannot inspect the WorkLog process {process.Id}, so use of this payload cannot be ruled out. Nothing was removed.");
                }
                catch (InvalidOperationException)
                {
                    // The process exited while it was inspected.
                }
                if (image is not null && image.Equals(executable, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"A running process (PID {process.Id}) uses this payload. Nothing was stopped or removed.");
            }
        }
    }

    private (DevOperation Operation, string Path) ReadOperation(string path, string expectedState)
    {
        path = Path.GetFullPath(path);
        if (!DevTargets.Within(targets.Workspace, path))
            throw new InvalidOperationException("The operation record must live in the workspace.");
        var operation = InstallationLayout.Read<DevOperation>(path, 65536);
        if (!DevTargets.Same(operation.InstallationRoot, targets.InstallationRoot) || !DevTargets.Same(operation.DataDirectory, targets.DataDirectory))
            throw new InvalidOperationException("The operation was prepared for a different installation or data directory.");
        if (operation.State != expectedState)
            throw new InvalidOperationException($"The operation is '{operation.State}', not '{expectedState}'. Run Preflight again for a new operation.");
        return (operation, path);
    }

    /// <summary>Verifies the checksum and returns the version, payload manifest and archive hash. Reads the manifest from the archive without extracting.</summary>
    private static (string Version, PayloadManifest Manifest, string Hash) InspectPackage(string archive)
    {
        var version = PackageVersion(archive) ?? throw new InvalidOperationException("Unexpected package name: " + Path.GetFileName(archive));
        var hash = ReleasePackage.VerifyChecksum(archive, archive + ".sha256", ReleaseInfo.PackageName(version));
        using var zip = ZipFile.OpenRead(archive);
        var entry = zip.GetEntry($"versions/{version}/release.json") ?? throw new InvalidOperationException("The package has no release.json.");
        if (entry.Length > 16384)
            throw new InvalidOperationException("The package manifest exceeds its size limit.");
        using var stream = entry.Open();
        var manifest = JsonSerializer.Deserialize<PayloadManifest>(stream, InstallationLayout.JsonOptions)
            ?? throw new InvalidOperationException("Unreadable package manifest.");
        if (manifest.Version != version || manifest.PackageFormat != InstallationLayout.PackageFormat)
            throw new InvalidOperationException("The package manifest does not match the package name or format.");
        if (!InstallationLayout.IsDevVersion(version))
            throw new InvalidOperationException($"{version} is not a development-range version.");
        return (version, manifest, hash);
    }
}
