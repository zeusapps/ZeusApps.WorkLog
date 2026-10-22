using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using WorkLog.Infrastructure;
using WorkLog.Installation;

namespace WorkLog.Updates;

public sealed record PreparedPayload(string Root, string DataDirectory, string StageDirectory, string Version,
    string RunningVersion, PayloadSelection Original, string ArchiveHash, PayloadReference Payload, string OperationId);

public sealed class PayloadInstaller
{
    private readonly Func<string, string, CancellationToken, Task> verify;
    internal Action<string>? ObserveStage
    {
        get; init;
    }
    public PayloadInstaller() : this(VerifyHealthAsync) { }
    internal PayloadInstaller(Func<string, string, CancellationToken, Task> health) => verify = health;

    public async Task<PreparedPayload> DownloadAsync(ReleaseClient client, string root, string dataDirectory,
        string runningVersion, ReleaseInfo release, CancellationToken token = default)
    {
        ValidateRoot(root, dataDirectory);
        var original = InstallationLayout.ReadSelection(root);
        if (!release.IsValid() || !release.IsNewerThan(original.Current.Version) || !release.IsNewerThan(runningVersion))
            throw new InvalidOperationException("Only a release newer than running and selected versions can activate.");
        var stage = Path.Combine(Path.GetFullPath(dataDirectory), "updates", "staging", Guid.NewGuid().ToString("N"));
        InstallationLayout.EnsureNoReparsePoints(stage);
        Directory.CreateDirectory(stage);
        var archive = Path.Combine(stage, ReleaseInfo.PackageName(release.Version));
        try
        {
            await client.DownloadAsync(release.PackageUrl, archive, ReleasePackage.MaximumPackageBytes, token);
            await client.DownloadAsync(release.ChecksumUrl, archive + ".sha256", 4096, token);
            return await PrepareDownloadedAsync(root, dataDirectory, runningVersion, release.Version, stage, original, token);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            try
            {
                AtomicFile.Write(UpdatePlan.StatusFile(dataDirectory, root), Json.Encode(new UpdateOutcome("failed", release.Version,
                    "Preparation failed before selection: " + exception.Message, OperationId: Path.GetFileName(stage))));
            }
            catch (Exception statusError) when (statusError is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    public async Task<PreparedPayload> PrepareDownloadedAsync(string root, string dataDirectory, string runningVersion,
        string version, string stage, PayloadSelection? original = null, CancellationToken token = default)
    {
        root = Path.GetFullPath(root);
        dataDirectory = Path.GetFullPath(dataDirectory);
        stage = Path.GetFullPath(stage);
        ValidateRoot(root, dataDirectory);
        original ??= InstallationLayout.ReadSelection(root);
        var candidate = new ReleaseInfo(version, "", "", "");
        if (!candidate.IsNewerThan(runningVersion) || !candidate.IsNewerThan(original.Current.Version))
            throw new InvalidOperationException("Candidate is not newer than running and selected versions.");
        var id = Path.GetFileName(stage);
        if (!Guid.TryParseExact(id, "N", out _) || stage != Path.Combine(dataDirectory, "updates", "staging", id))
            throw new InvalidOperationException("Invalid payload staging directory.");
        InstallationLayout.EnsureNoReparsePoints(stage);
        var archive = Path.Combine(stage, ReleaseInfo.PackageName(version));
        var hash = ReleasePackage.VerifyChecksum(archive, archive + ".sha256", ReleaseInfo.PackageName(version));
        var package = Path.Combine(stage, "package");
        var payload = ReleasePackage.ExtractVersioned(archive, package, version);
        await verify(payload, version, token);
        var reference = new PayloadReference(version, InstallationLayout.Hash(Path.Combine(payload, "release.json")));
        var prepared = new PreparedPayload(root, dataDirectory, stage, version, runningVersion, original, hash, reference, id);
        WriteOutcome(prepared, "staged", "Verified package staged; activation for new processes has not committed.");
        return prepared;
    }

    public async Task<PayloadSelection> ActivateAsync(PreparedPayload prepared, CancellationToken token = default)
    {
        var committed = false;
        try
        {
            ValidateRoot(prepared.Root, prepared.DataDirectory);
            using var lease = await AcquirePublisherAsync(prepared.Root, token);
            var current = InstallationLayout.ReadSelection(prepared.Root);
            if (current.Generation != prepared.Original.Generation || current != prepared.Original)
            {
                if (InstallationLayout.ParseVersion(current.Current.Version) >= InstallationLayout.ParseVersion(prepared.Version))
                {
                    WriteOutcome(prepared, "superseded", "Candidate already selected or superseded. Selection unchanged.");
                    return current;
                }
                throw new IOException("Selection changed after preparation, possibly through rollback. Retry a fresh explicit update.");
            }
            if (InstallationLayout.ParseVersion(prepared.Version) <= InstallationLayout.ParseVersion(current.Current.Version)
                || InstallationLayout.ParseVersion(prepared.Version) <= InstallationLayout.ParseVersion(prepared.RunningVersion))
                throw new InvalidOperationException("Stale candidate cannot downgrade the selected payload.");
            var archive = Path.Combine(prepared.StageDirectory, ReleaseInfo.PackageName(prepared.Version));
            if (ReleasePackage.VerifyChecksum(archive, archive + ".sha256", ReleaseInfo.PackageName(prepared.Version)) != prepared.ArchiveHash)
                throw new IOException("Archive changed after preparation.");
            var source = InstallationLayout.PayloadDirectory(Path.Combine(prepared.StageDirectory, "package"), prepared.Version);
            _ = InstallationLayout.ReadPayload(source);
            if (InstallationLayout.Hash(Path.Combine(source, "release.json")) != prepared.Payload.ManifestHash)
                throw new IOException("Prepared manifest changed.");
            ObserveStage?.Invoke("before-publish");
            token.ThrowIfCancellationRequested();
            var destination = InstallationLayout.PayloadDirectory(prepared.Root, prepared.Version);
            if (!Directory.Exists(destination))
            {
                var publishing = Path.Combine(prepared.Root, "versions", ".staging-" + Guid.NewGuid().ToString("N"));
                InstallationLayout.EnsureNoReparsePoints(publishing);
                Directory.CreateDirectory(publishing);
                var manifest = InstallationLayout.ReadPayload(source);
                foreach (var name in manifest.Files.Keys.Append("release.json"))
                    File.Copy(Path.Combine(source, name), Path.Combine(publishing, name));
                _ = InstallationLayout.ReadPayload(publishing);
                if (InstallationLayout.Hash(Path.Combine(publishing, "release.json")) != prepared.Payload.ManifestHash)
                    throw new IOException("Published manifest differs from the verified candidate.");
                Directory.Move(publishing, destination);
            }
            _ = InstallationLayout.ValidateReference(prepared.Root, prepared.Payload);
            ObserveStage?.Invoke("before-selection");
            token.ThrowIfCancellationRequested();
            var next = Commit(prepared.Root, current, prepared.Payload, prepared.OperationId);
            committed = true;
            ObserveStage?.Invoke("after-selection");
            WriteOutcome(prepared, "activated", "Activated for new processes. Existing conversations continue on their running version; reconnect normally to adopt. Rerun init where owned instructions need refreshing.");
            return next;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A committed pointer is authoritative even when outcome persistence was interrupted.
            if (!committed)
            {
                try
                {
                    WriteOutcome(prepared, "failed", exception.Message + " No selection was committed by this operation; retained payloads and private data remain.");
                }
                catch (Exception statusError) when (statusError is IOException or UnauthorizedAccessException) { }
            }
            throw;
        }
    }

    public async Task<PayloadSelection> RollbackAsync(string root, string dataDirectory, CancellationToken token = default)
    {
        ValidateRoot(root, dataDirectory);
        using var lease = await AcquirePublisherAsync(root, token);
        var current = InstallationLayout.ReadSelection(root);
        var previous = current.Previous ?? throw new InvalidOperationException("No previous payload is recorded. Follow manual recovery instructions.");
        _ = InstallationLayout.ValidateReference(root, previous);
        await verify(InstallationLayout.PayloadDirectory(root, previous.Version), previous.Version, token);
        token.ThrowIfCancellationRequested();
        var operation = Guid.NewGuid().ToString("N");
        var next = Commit(root, current, previous, operation);
        AtomicFile.Write(UpdatePlan.StatusFile(dataDirectory, root), Json.Encode(new UpdateOutcome("activated", previous.Version,
            "Previous compatible payload selected for new processes. Existing conversations continue; no journal or configuration was restored.", OperationId: operation)));
        return next;
    }

    private static PayloadSelection Commit(string root, PayloadSelection current, PayloadReference target, string operation)
    {
        var next = new PayloadSelection(target, current.Current, checked(current.Generation + 1), operation);
        AtomicFile.Write(Path.Combine(root, "current.previous.json"), Json.Encode(current));
        AtomicFile.Write(Path.Combine(root, "current.json"), Json.Encode(next));
        return next;
    }

    private static void ValidateRoot(string root, string dataDirectory)
    {
        InstalledApplication.ValidateDataSeparation(root, dataDirectory);
        _ = InstallationLayout.ReadSelection(root);
    }
    public static async Task<FileStream> AcquirePublisherAsync(string root, CancellationToken token = default)
    {
        InstallationLayout.EnsureNoReparsePoints(Path.Combine(root, ".worklog-publish.lock"));
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(Path.Combine(root, ".worklog-publish.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33 && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(30))
            {
                await Task.Delay(50, token);
            }
        }
    }

    private static void WriteOutcome(PreparedPayload prepared, string state, string message) => AtomicFile.Write(
        UpdatePlan.StatusFile(prepared.DataDirectory, prepared.Root), Json.Encode(new UpdateOutcome(state, prepared.Version, message, OperationId: prepared.OperationId)));

    private static async Task VerifyHealthAsync(string payload, string version, CancellationToken token)
    {
        await ReleasePackage.VerifyExecutableAsync(payload, version, token);
        var data = Path.Combine(Path.GetDirectoryName(payload)!, ".health-" + Guid.NewGuid().ToString("N"));
        InstallationLayout.EnsureNoReparsePoints(data);
        Directory.CreateDirectory(data);
        AtomicFile.Write(Path.Combine(data, "config.json"), "{\"updates\":{\"enabled\":false}}");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var transport = new StdioClientTransport(new()
        {
            Name = "update-health",
            Command = Path.Combine(payload, "worklog.exe"),
            Arguments = ["serve", "--data-dir", data]
        });
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
        if (client.ServerInfo.Version != version)
            throw new InvalidOperationException("Health probe server version differs.");
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        if (tools.Count != 2 || tools.Any(tool => tool.ProtocolTool.OutputSchema is null)
            || !tools.Any(tool => tool.Name == "log_work") || !tools.Any(tool => tool.Name == "query_work"))
            throw new InvalidOperationException("Candidate health probe did not advertise the expected journal tools.");
    }
}
