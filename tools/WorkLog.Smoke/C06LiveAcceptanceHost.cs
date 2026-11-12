using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using WorkLog.Configuration;
using WorkLog.Dashboard;

namespace WorkLog.Smoke;

/// <summary>
/// A disposable C06 host fixture that injects deterministic watcher hints without adding a product HTTP control route.
/// The controller and host communicate only through atomic JSON files inside the fixture directory.
/// </summary>
internal static class C06LiveAcceptanceHost
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task<int> RunAsync(string dataDirectory, string repository, int port, string controlDirectory, bool useRealWatchers)
    {
        var data = Path.GetFullPath(dataDirectory);
        var repo = Path.GetFullPath(repository);
        var controls = Path.GetFullPath(controlDirectory);
        Directory.CreateDirectory(controls);
        var settings = new LocalSettings(data);
        var instance = Guid.NewGuid().ToString("N");
        var source = useRealWatchers ? null : new ManualChangeSource(data);
        await using var app = DashboardHost.Create(settings, repo, port, instance,
            new DashboardAuthorization(data), changeSource: source, timeProvider: TimeProvider.System);
        DashboardLiveCoordinator? activeCoordinator = null;
        var actualPort = port;
        try
        {
            await app.StartAsync();
            actualPort = new Uri(DashboardHost.Address(app)).Port;
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            activeCoordinator = coordinator;
            if (source is not null) source.ObservePendingHints = () => coordinator.PendingHintCount;
            for (var attempt = 0; attempt < 500 && coordinator.WatchRootIds.Length == 0; attempt++)
                await Task.Delay(20, app.Lifetime.ApplicationStopping);
            if (coordinator.WatchRootIds.Length == 0)
                throw new TimeoutException("C06 live host did not bind any server-derived watch roots.");
            var lastCommand = ReadLastCommand(controls);
            await WriteJsonAsync(Path.Combine(controls, "ready.json"), Snapshot("ready", null, actualPort, instance, source, coordinator));

            while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
            {
                var command = await ReadCommandAsync(controls, lastCommand);
                if (command is null)
                {
                    await Task.Delay(20, app.Lifetime.ApplicationStopping);
                    continue;
                }

                lastCommand = command.Value.Id;
                object reply;
                switch (command.Value.Name)
                {
                    case "status":
                        reply = Snapshot("status", lastCommand, actualPort, instance, source, coordinator);
                        break;
                    case "emit":
                        var hint = command.Value.Payload;
                        RequireManualSource(source).Emit(ParseHint(hint));
                        reply = Snapshot("emitted", lastCommand, actualPort, instance, source, coordinator);
                        break;
                    case "emitBatch":
                        if (!command.Value.Payload.TryGetProperty("hints", out var hints) || hints.ValueKind != JsonValueKind.Array || hints.GetArrayLength() > 128)
                            throw new InvalidDataException("A C06 hint batch must contain at most 128 items.");
                        foreach (var item in hints.EnumerateArray()) RequireManualSource(source).Emit(ParseHint(item));
                        reply = Snapshot("emittedBatch", lastCommand, actualPort, instance, source, coordinator);
                        break;
                    case "burst":
                    {
                        var burstDomains = DashboardChangeDomain.None;
                        var hasDomain = command.Value.Payload.TryGetProperty("domain", out var burstDomain)
                            && Enum.TryParse(burstDomain.GetString(), true, out burstDomains);
                        var burstRootId = command.Value.Payload.TryGetProperty("rootId", out var burstRoot) ? burstRoot.GetString() : null;
                        var burstCount = command.Value.Payload.TryGetProperty("count", out var countElement) && countElement.TryGetInt32(out var parsedCount) ? parsedCount : 0;
                        if (!hasDomain || string.IsNullOrWhiteSpace(burstRootId) || burstCount is < 1 or > 4096)
                            throw new InvalidDataException("C06 burst controls require a valid domain, server-derived root, and count from 1 through 4096.");
                        for (var index = 0; index < burstCount; index++)
                            RequireManualSource(source).Emit(new(burstDomains, burstRootId, $"c06-burst/{index:D4}.md"));
                        reply = Snapshot("burst", lastCommand, actualPort, instance, source, coordinator);
                        break;
                    }
                    case "drop":
                        RequireManualSource(source).RecordDroppedHint();
                        reply = Snapshot("dropped", lastCommand, actualPort, instance, source, coordinator);
                        break;
                    case "overflow":
                        var domains = ParseDomains(command.Value.Payload);
                        RequireManualSource(source).EmitOverflow(domains);
                        reply = Snapshot("overflow", lastCommand, actualPort, instance, source, coordinator);
                        break;
                    case "failure":
                        RequireManualSource(source).EmitFailure(ParseDomains(command.Value.Payload));
                        reply = Snapshot("failure", lastCommand, actualPort, instance, source, coordinator);
                        break;
                    case "stop":
                        reply = Snapshot("stopping", lastCommand, actualPort, instance, source, coordinator);
                        await WriteJsonAsync(Path.Combine(controls, "ack.json"), reply);
                        await app.StopAsync();
                        return 0;
                    default:
                        throw new InvalidDataException("Unknown C06 host control command.");
                }

                await WriteJsonAsync(Path.Combine(controls, "ack.json"), reply);
            }

            return 0;
        }
        finally
        {
            await app.StopAsync();
            if (activeCoordinator is not null)
                await WriteJsonAsync(Path.Combine(controls, "final.json"), Snapshot("stopped", null, actualPort, instance, source, activeCoordinator));
        }
    }

    private static DashboardChangeHint ParseHint(JsonElement payload)
    {
        if (!payload.TryGetProperty("domain", out var domainElement) || !Enum.TryParse<DashboardChangeDomain>(domainElement.GetString(), true, out var domain))
            throw new InvalidDataException("C06 hint domain is invalid.");
        var rootId = OptionalString(payload, "rootId", 256);
        var relativePath = OptionalString(payload, "relativePath", 512);
        var directoryChanged = payload.TryGetProperty("directoryChanged", out var directoryElement) && directoryElement.ValueKind == JsonValueKind.True;
        if (relativePath is not null && (Path.IsPathRooted(relativePath) || relativePath.Contains("..", StringComparison.Ordinal) || relativePath.Contains('\\') || relativePath.Contains('\0')))
            throw new InvalidDataException("C06 hints accept only bounded relative paths.");
        return new(domain, rootId, relativePath, DirectoryChanged: directoryChanged);
    }

    private static DashboardChangeDomain ParseDomains(JsonElement payload)
    {
        if (!payload.TryGetProperty("domains", out var element) || element.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("C06 domain list is invalid.");
        var result = (DashboardChangeDomain)0;
        foreach (var item in element.EnumerateArray())
        {
            if (!Enum.TryParse<DashboardChangeDomain>(item.GetString(), true, out var domain))
                throw new InvalidDataException("C06 domain list contains an invalid domain.");
            result |= domain;
        }
        return result;
    }

    private static string? OptionalString(JsonElement element, string name, int maximumLength)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        var text = value.GetString();
        if (text is null || text.Length > maximumLength) throw new InvalidDataException("C06 control value exceeds its bound.");
        return text;
    }

    private static ManualChangeSource RequireManualSource(ManualChangeSource? source) =>
        source ?? throw new InvalidOperationException("This C06 host uses the production filesystem watcher, not a manual hint source.");

    private static object Snapshot(string kind, long? commandId, int port, string instance, ManualChangeSource? source, DashboardLiveCoordinator coordinator) => new
    {
        kind,
        commandId,
        pid = Environment.ProcessId,
        port,
        instance,
        snapshot = coordinator.CurrentSnapshot,
        logicalWatchRootCount = coordinator.LogicalWatchRootCount,
        sampleWatchRootIds = coordinator.WatchRootIds.Take(16).ToArray(),
        watchRootSampleTruncated = coordinator.LogicalWatchRootCount > 16,
        activeWatcherCount = coordinator.ActiveWatcherCount,
        pendingHintCount = coordinator.PendingHintCount,
        maxPendingHintCount = coordinator.MaxPendingHintCount,
        overflowBatchCount = coordinator.OverflowBatchCount,
        maxPendingNotificationCount = coordinator.MaxPendingNotificationCount,
        processedBatchCount = coordinator.ProcessedBatchCount,
        lastBatchHintHighWater = coordinator.LastBatchHintHighWater,
        lastBatchFirstDelayMilliseconds = coordinator.LastBatchFirstDelayMilliseconds,
        lastBatchQuietDelayMilliseconds = coordinator.LastBatchQuietDelayMilliseconds,
        lastBatchOverflow = coordinator.LastBatchOverflow,
        watchRoots = coordinator.WatchRoots.Take(16).ToArray(),
        activeConnectionCount = coordinator.ActiveConnectionCount,
        checkpointCount = coordinator.CheckpointCount,
        source = source is null ? null : new { source.RebindCount, source.EmissionCount, source.DroppedHintCount, source.IsDisposed, source.MaxObservedPendingHintCount }
    };

    private static async Task<(long Id, string Name, JsonElement Payload)?> ReadCommandAsync(string directory, long lastId)
    {
        var path = Path.Combine(directory, "command.json");
        if (!File.Exists(path)) return null;
        byte[] bytes;
        try { bytes = await ReadSharedAsync(path); }
        // The runner replaces command.json atomically; a sharing violation or vanished file is transient.
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        JsonDocument document;
        try { document = JsonDocument.Parse(bytes); }
        catch (JsonException) { return null; }
        using (document)
        {
            var root = document.RootElement;
            var id = root.GetProperty("id").GetInt64();
            if (id <= lastId) return null;
            var name = root.GetProperty("command").GetString() ?? throw new InvalidDataException("C06 command name is missing.");
            var payload = root.TryGetProperty("payload", out var value) ? value.Clone() : EmptyPayload();
            return (id, name, payload);
        }
    }

    // Open with FileShare.Delete so the runner's atomic rename over the control file never fails while this host reads it.
    private static async Task<byte[]> ReadSharedAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static JsonElement EmptyPayload()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private static long ReadLastCommand(string directory)
    {
        var path = Path.Combine(directory, "ack.json");
        if (!File.Exists(path)) return 0;
        try { using var document = JsonDocument.Parse(ReadSharedAsync(path).GetAwaiter().GetResult()); return document.RootElement.GetProperty("commandId").GetInt64(); }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException) { return 0; }
    }

    private static async Task WriteJsonAsync(string path, object value)
    {
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(value, JsonOptions));
        // The runner polls these files; a replace can briefly be denied while it holds the file open, so retry instead of crashing the host.
        for (var attempt = 0; ; attempt++)
        {
            try { File.Move(temp, path, overwrite: true); return; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 100) { await Task.Delay(10); }
        }
    }

    private sealed class ManualChangeSource : IDashboardChangeSource
    {
        private readonly FileSystemWatcher secretWatcher;
        private IReadOnlyCollection<DashboardWatchRoot> roots = [];
        public event Action<DashboardChangeHint>? Changed;
        public int RebindCount { get; private set; }
        public int EmissionCount { get; private set; }
        public int DroppedHintCount { get; private set; }
        public int MaxObservedPendingHintCount { get; private set; }
        public bool IsDisposed { get; private set; }
        public Func<int>? ObservePendingHints { get; set; }

        public ManualChangeSource(string dataDirectory)
        {
            secretWatcher = new FileSystemWatcher(dataDirectory, "dashboard.secret")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            secretWatcher.Changed += OnSecretChanged;
            secretWatcher.Created += OnSecretChanged;
            secretWatcher.Deleted += OnSecretChanged;
            secretWatcher.Renamed += OnSecretRenamed;
        }

        public Task RebindAsync(IReadOnlyCollection<DashboardWatchRoot> next, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            roots = next.ToArray();
            RebindCount++;
            return Task.CompletedTask;
        }

        public void Emit(DashboardChangeHint hint)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(ManualChangeSource));
            if (hint.RootId is not null && roots.All(root => root.Id != hint.RootId))
                throw new InvalidDataException("C06 hint names no server-derived watch root.");
            EmissionCount++;
            Changed?.Invoke(hint);
            MaxObservedPendingHintCount = Math.Max(MaxObservedPendingHintCount, ObservePendingHints?.Invoke() ?? 0);
        }

        public void EmitOverflow(DashboardChangeDomain domains) => Emit(new(domains, Overflow: true));
        public void EmitFailure(DashboardChangeDomain domains) => Emit(new(domains, Failed: true));
        public void RecordDroppedHint() => DroppedHintCount++;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            secretWatcher.Dispose();
            roots = [];
            Changed = null;
            return ValueTask.CompletedTask;
        }

        private void OnSecretChanged(object sender, FileSystemEventArgs args)
        {
            if (!IsDisposed) Emit(new(DashboardChangeDomain.Authorization));
        }

        private void OnSecretRenamed(object sender, RenamedEventArgs args)
        {
            if (!IsDisposed) Emit(new(DashboardChangeDomain.Authorization));
        }
    }
}
