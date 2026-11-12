using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using WorkLog.Configuration;
using WorkLog.Infrastructure;
using WorkLog.Persistence;

namespace WorkLog.Dashboard;

/// <summary>Owns watcher bindings, authoritative revisions, bounded coalescing, and periodic recovery checkpoints.</summary>
internal sealed class DashboardLiveCoordinator : BackgroundService
{
    private const int MaximumHintsPerBatch = 128;
    private const int SqliteBusyResultCode = 5;
    private const int SqliteLockedResultCode = 6;
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan MaximumBatchDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ReconcileInterval = TimeSpan.FromSeconds(5);

    private sealed class PendingBatch
    {
        public DashboardChangeDomain Domains;
        public readonly HashSet<DashboardChangeHint> Hints = [];
        public int MaxHints;
        public bool Overflow;
        public bool FullCheck;
        public bool Immediate;
        public bool JournalLifecycleChanged;
        public bool DigestLifecycleChanged;
        public DateTimeOffset? FirstAt;
        public DateTimeOffset LastAt;
    }

    private sealed class ReconcileLane(DashboardChangeDomain domains)
    {
        public DashboardChangeDomain Domains { get; } = domains;
        public readonly object Gate = new();
        public readonly SemaphoreSlim Wake = new(0, 1);
        public PendingBatch? Pending;
    }

    private sealed record JournalMarker(string Stamp, long? Snapshot, string Availability, string Files, string FileIdentity,
        long LifecycleGeneration);

    private readonly object stateGate = new();
    private readonly object batchGate = new();
    private readonly SemaphoreSlim wake = new(0, 1);
    private readonly DashboardJournal journal;
    private readonly DigestStore digests;
    private readonly SourceCatalog catalog;
    private readonly DocumentService documents;
    private readonly IDashboardChangeSource changes;
    private readonly DashboardLiveConnections connections;
    private readonly TimeProvider clock;
    private readonly string instance;
    private readonly string dataDirectory;
    private LocalSettings currentSettings;
    private PendingBatch pending = new();
    private readonly ReconcileLane[] lanes =
    [
        new(DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents),
        new(DashboardChangeDomain.Journal),
        new(DashboardChangeDomain.Digests),
        new(DashboardChangeDomain.Reads)
    ];
    private Dictionary<string, DashboardWatchRoot> watchRoots = new(StringComparer.Ordinal);
    private Dictionary<string, string[]> recoveryContextsByRoot = new(StringComparer.Ordinal);
    private string? catalogStamp;
    private string? documentsStamp;
    private JournalMarker? journalStamp;
    private string? digestStamp;
    private string? readsStamp;
    private DashboardChangeDomain dirty = DashboardChangeDomain.All;
    private long catalogRevision;
    private long documentsRevision;
    private long journalRevision;
    private long digestRevision;
    private long readsRevision;
    private long journalLifecycleGeneration;
    private long digestLifecycleGeneration;
    private int maxPendingHintCount;
    private long overflowBatchCount;
    private long processedBatchCount;
    private int lastBatchHintHighWater;
    private long lastBatchFirstDelayMilliseconds;
    private long lastBatchQuietDelayMilliseconds;
    private int lastBatchOverflow;

    public DashboardLiveCoordinator(LocalSettings settings, DashboardJournal journal, DigestStore digests, SourceCatalog catalog,
        DocumentService documents, IDashboardChangeSource changes, DashboardLiveConnections connections, TimeProvider clock,
        DashboardLiveOptions options)
    {
        currentSettings = settings;
        dataDirectory = settings.DataDirectory;
        this.journal = journal;
        this.digests = digests;
        this.catalog = catalog;
        this.documents = documents;
        this.changes = changes;
        this.connections = connections;
        this.clock = clock;
        instance = options.Instance;
    }

    internal DashboardRevisionVector CurrentSnapshot
    {
        get
        {
            lock (stateGate)
                return new(instance, catalogRevision, documentsRevision, journalRevision, digestRevision, readsRevision, DirtyNames(dirty));
        }
    }

    internal string[] WatchRootIds
    {
        get { lock (stateGate) return watchRoots.Keys.Order(StringComparer.Ordinal).ToArray(); }
    }

    internal DashboardWatchRootDiagnostic[] WatchRoots
    {
        get
        {
            lock (stateGate)
                return watchRoots.Values.OrderBy(root => root.Id, StringComparer.Ordinal)
                    .Select(root => new DashboardWatchRootDiagnostic(root.Id, DomainNames(root.Domains))).ToArray();
        }
    }

    /// <summary>Actual operating-system watcher subscriptions; injected fake sources intentionally report zero.</summary>
    internal int ActiveWatcherCount => changes is FileSystemDashboardChangeSource fileSystem ? fileSystem.WatcherCount : 0;
    internal int LogicalWatchRootCount { get { lock (stateGate) return watchRoots.Count; } }
    internal int PendingHintCount
    {
        get
        {
            lock (batchGate) return pending.Hints.Count + connections.PendingHintCount;
        }
    }
    /// <summary>High-water count of distinct pending source hints; capped at the configured batch limit.</summary>
    internal int MaxPendingHintCount => Volatile.Read(ref maxPendingHintCount);
    /// <summary>Number of batches promoted to domain-wide reconciliation by source overflow or the hint cap.</summary>
    internal long OverflowBatchCount => Interlocked.Read(ref overflowBatchCount);
    internal long ProcessedBatchCount => Interlocked.Read(ref processedBatchCount);
    internal int LastBatchHintHighWater => Volatile.Read(ref lastBatchHintHighWater);
    internal long LastBatchFirstDelayMilliseconds => Interlocked.Read(ref lastBatchFirstDelayMilliseconds);
    internal long LastBatchQuietDelayMilliseconds => Interlocked.Read(ref lastBatchQuietDelayMilliseconds);
    internal bool LastBatchOverflow => Volatile.Read(ref lastBatchOverflow) != 0;
    internal int ActiveConnectionCount => connections.ActiveConnectionCount;
    internal long CheckpointCount => connections.CheckpointCount;
    internal int MaxPendingNotificationCount => connections.MaxPendingNotificationCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        changes.Changed += OnChange;
        QueueFullCheck(immediate: true);
        await Task.WhenAll(new[] { ProcessHintsAsync(stoppingToken), PublishCheckpointsAsync(stoppingToken) }
            .Concat(lanes.Select(lane => RunLaneAsync(lane, stoppingToken))));
    }

    private async Task ProcessHintsAsync(CancellationToken token)
    {
        while (true)
        {
            await wake.WaitAsync(token);
            if (!IsImmediate()) await WaitForDebounceAsync(token);
            var batch = TakeBatch();
            if (batch is null) continue;
            ProcessBatch(batch);
        }
    }

    private async Task PublishCheckpointsAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(ReconcileInterval, clock);
        while (await timer.WaitForNextTickAsync(token))
        {
            connections.Publish(CurrentSnapshot, checkpoint: true);
            QueueFullCheck(immediate: true);
        }
    }

    private void OnChange(DashboardChangeHint hint)
    {
        var domains = Classify(hint);
        if (domains == DashboardChangeDomain.None) return;
        if ((domains & DashboardChangeDomain.Authorization) != 0) connections.RevokeInvalidConnections();
        var publicDomains = domains & DashboardChangeDomain.All;
        if (publicDomains != DashboardChangeDomain.None)
        {
            lock (stateGate) dirty |= publicDomains;
        }
        lock (batchGate)
        {
            pending.Domains |= domains;
            pending.FirstAt ??= clock.GetUtcNow();
            pending.LastAt = clock.GetUtcNow();
            if (hint.Overflow || hint.Failed)
            {
                RecordLifecycleChange(pending, hint, domains);
                MarkOverflow(pending);
            }
            else if (!pending.Overflow && pending.Hints.Count < MaximumHintsPerBatch)
            {
                var added = pending.Hints.Add(hint);
                if (added) RecordLifecycleChange(pending, hint, domains);
                pending.MaxHints = Math.Max(pending.MaxHints, pending.Hints.Count);
                UpdateMaxPendingHintCount(pending.Hints.Count);
            }
            else
            {
                RecordLifecycleChange(pending, hint, domains);
                MarkOverflow(pending);
            }
        }
        SignalWorker();
        if (publicDomains != DashboardChangeDomain.None) connections.Publish(CurrentSnapshot);
    }

    private DashboardChangeDomain Classify(DashboardChangeHint hint)
    {
        if (hint.RootId is null || hint.RelativePath is null)
            return hint.Domains;
        DashboardWatchRoot? root;
        lock (stateGate) watchRoots.TryGetValue(hint.RootId, out root);
        if (root is null) return hint.Domains;
        var relative = hint.RelativePath.Replace('\\', '/');
        var name = relative[(relative.LastIndexOf('/') + 1)..];
        var directory = Path.GetFileName(root.CanonicalPath);
        if (PathComparer.Equals(root.CanonicalPath, dataDirectory))
        {
            if (name.Equals("config.json", StringComparison.OrdinalIgnoreCase)) return DashboardChangeDomain.Catalog;
            if (name.Equals("dashboard.secret", StringComparison.OrdinalIgnoreCase)) return DashboardChangeDomain.Authorization;
            if (name.Equals("journal.db", StringComparison.OrdinalIgnoreCase) || name.Equals("journal.db-wal", StringComparison.OrdinalIgnoreCase)) return DashboardChangeDomain.Journal;
            if (name.Equals("digests.db", StringComparison.OrdinalIgnoreCase) || name.Equals("digests.db-wal", StringComparison.OrdinalIgnoreCase)) return DashboardChangeDomain.Digests;
            if (name.EndsWith("-shm", StringComparison.OrdinalIgnoreCase)) return DashboardChangeDomain.None;
            return DashboardChangeDomain.None;
        }
        if (directory.Equals("dashboard-reads", StringComparison.OrdinalIgnoreCase)) return DashboardChangeDomain.Reads;
        if ((hint.Domains & (DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents)) != 0)
        {
            if (hint.DirectoryChanged) return DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents;
            if (name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return DashboardChangeDomain.Documents;
            if (name.Equals("config.yaml", StringComparison.OrdinalIgnoreCase) || name.Equals("config.yml", StringComparison.OrdinalIgnoreCase)
                || name.Equals(".openspec.yaml", StringComparison.OrdinalIgnoreCase) || name.Equals(".openspec.yml", StringComparison.OrdinalIgnoreCase))
                return DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents;
            if (hint.RelativePath.IndexOf('/') < 0 || string.IsNullOrEmpty(Path.GetExtension(name)))
                return DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents;
            return DashboardChangeDomain.None;
        }
        return hint.Domains;
    }

    private async Task WaitForDebounceAsync(CancellationToken token)
    {
        DateTimeOffset first;
        DateTimeOffset last;
        lock (batchGate)
        {
            first = pending.FirstAt ?? clock.GetUtcNow();
            last = pending.LastAt;
        }
        while (true)
        {
            var now = clock.GetUtcNow();
            var due = Min(first + MaximumBatchDelay, last + Debounce);
            var delay = due - now;
            if (delay <= TimeSpan.Zero) return;
            using var race = CancellationTokenSource.CreateLinkedTokenSource(token);
            var signaled = wake.WaitAsync(race.Token);
            var elapsed = Task.Delay(delay, clock, race.Token);
            var completed = await Task.WhenAny(signaled, elapsed);
            race.Cancel();
            try { await completed; }
            catch (OperationCanceledException) when (race.IsCancellationRequested) { }
            if (completed == elapsed) return;
            lock (batchGate) last = pending.LastAt;
        }
    }

    private bool IsImmediate()
    {
        lock (batchGate) return pending.Immediate;
    }

    private PendingBatch? TakeBatch()
    {
        lock (batchGate)
        {
            if (pending.Domains == DashboardChangeDomain.None && !pending.FullCheck) return null;
            var current = pending;
            pending = new();
            return current;
        }
    }

    private void QueueFullCheck(bool immediate)
    {
        lock (batchGate)
        {
            pending.FullCheck = true;
            pending.Immediate |= immediate;
            pending.FirstAt ??= clock.GetUtcNow();
            pending.LastAt = clock.GetUtcNow();
        }
        SignalWorker();
    }

    private void SignalWorker()
    {
        try { wake.Release(); }
        catch (SemaphoreFullException) { }
    }

    private void MarkOverflow(PendingBatch batch)
    {
        if (batch.Overflow) return;
        batch.Overflow = true;
        batch.Hints.Clear();
        Interlocked.Increment(ref overflowBatchCount);
    }

    private static void RecordLifecycleChange(PendingBatch batch, DashboardChangeHint hint, DashboardChangeDomain domains)
    {
        if (!hint.FileLifecycleChanged) return;
        batch.JournalLifecycleChanged |= (domains & DashboardChangeDomain.Journal) != 0;
        batch.DigestLifecycleChanged |= (domains & DashboardChangeDomain.Digests) != 0;
    }

    private void UpdateMaxPendingHintCount(int count)
    {
        var current = Volatile.Read(ref maxPendingHintCount);
        while (count > current)
        {
            var observed = Interlocked.CompareExchange(ref maxPendingHintCount, count, current);
            if (observed == current) return;
            current = observed;
        }
    }

    private void ProcessBatch(PendingBatch batch)
    {
        if (batch.JournalLifecycleChanged)
        {
            Interlocked.Increment(ref journalLifecycleGeneration);
        }
        if (batch.DigestLifecycleChanged) Interlocked.Increment(ref digestLifecycleGeneration);
        var started = clock.GetUtcNow();
        Interlocked.Increment(ref processedBatchCount);
        Volatile.Write(ref lastBatchHintHighWater, batch.MaxHints);
        Interlocked.Exchange(ref lastBatchFirstDelayMilliseconds,
            batch.FirstAt is { } first ? Math.Max(0, (long)(started - first).TotalMilliseconds) : 0);
        Interlocked.Exchange(ref lastBatchQuietDelayMilliseconds,
            Math.Max(0, (long)(started - batch.LastAt).TotalMilliseconds));
        Volatile.Write(ref lastBatchOverflow, batch.Overflow ? 1 : 0);
        var requested = batch.FullCheck ? DashboardChangeDomain.All : batch.Domains;
        if (batch.Overflow) requested |= batch.Domains & DashboardChangeDomain.All;
        if (requested == DashboardChangeDomain.None && (batch.Domains & DashboardChangeDomain.Authorization) != 0)
            connections.RevokeInvalidConnections();
        foreach (var lane in lanes)
        {
            var selected = requested & lane.Domains;
            if (selected == DashboardChangeDomain.None) continue;
            var laneBatch = SelectBatch(batch, selected);
            QueueLane(lane, laneBatch);
        }
        if (batch.FullCheck || batch.Domains != DashboardChangeDomain.None) connections.Publish(CurrentSnapshot);
    }

    private PendingBatch SelectBatch(PendingBatch source, DashboardChangeDomain domains)
    {
        var result = new PendingBatch
        {
            Domains = domains,
            FullCheck = source.FullCheck,
            Overflow = source.Overflow,
            FirstAt = source.FirstAt,
            LastAt = source.LastAt,
            MaxHints = source.MaxHints,
            JournalLifecycleChanged = source.JournalLifecycleChanged && (domains & DashboardChangeDomain.Journal) != 0,
            DigestLifecycleChanged = source.DigestLifecycleChanged && (domains & DashboardChangeDomain.Digests) != 0
        };
        if (!source.Overflow)
            foreach (var hint in source.Hints)
            {
                var selected = Classify(hint) & domains;
                if (selected != DashboardChangeDomain.None) result.Hints.Add(hint with { Domains = selected });
            }
        return result;
    }

    private void QueueLane(ReconcileLane lane, PendingBatch batch)
    {
        lock (lane.Gate)
        {
            if (lane.Pending is null) lane.Pending = batch;
            else MergeLaneBatch(lane.Pending, batch);
        }
        try { lane.Wake.Release(); }
        catch (SemaphoreFullException) { }
    }

    private void MergeLaneBatch(PendingBatch target, PendingBatch source)
    {
        target.Domains |= source.Domains;
        target.FullCheck |= source.FullCheck;
        target.FirstAt = target.FirstAt is null ? source.FirstAt : source.FirstAt is null ? target.FirstAt : Min(target.FirstAt.Value, source.FirstAt.Value);
        target.LastAt = target.LastAt > source.LastAt ? target.LastAt : source.LastAt;
        target.JournalLifecycleChanged |= source.JournalLifecycleChanged;
        target.DigestLifecycleChanged |= source.DigestLifecycleChanged;
        target.MaxHints = Math.Max(target.MaxHints, source.MaxHints);
        if (target.Overflow || source.Overflow) { target.Overflow = true; target.Hints.Clear(); return; }
        foreach (var hint in source.Hints)
        {
            target.Hints.Add(hint);
            if (target.Hints.Count <= MaximumHintsPerBatch) continue;
            target.Overflow = true;
            target.Hints.Clear();
            Interlocked.Increment(ref overflowBatchCount);
            break;
        }
        target.MaxHints = Math.Min(MaximumHintsPerBatch, Math.Max(target.MaxHints, target.Hints.Count));
        UpdateMaxPendingHintCount(target.Hints.Count);
    }

    private async Task RunLaneAsync(ReconcileLane lane, CancellationToken token)
    {
        while (true)
        {
            await lane.Wake.WaitAsync(token);
            PendingBatch? batch;
            lock (lane.Gate) { batch = lane.Pending; lane.Pending = null; }
            if (batch is null) continue;
            try
            {
                if ((lane.Domains & (DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents)) != 0)
                    await ReconcileCatalogAsync(batch.Domains, batch, token);
                else if ((lane.Domains & DashboardChangeDomain.Journal) != 0)
                    await ReconcileJournalAsync(token);
                else if ((lane.Domains & DashboardChangeDomain.Digests) != 0)
                    await ReconcileDigestsAsync(token);
                else
                    await ReconcileReadsAsync(token);
                connections.Publish(CurrentSnapshot);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        }
    }

    private async Task ReconcileCatalogAsync(DashboardChangeDomain requested, PendingBatch batch, CancellationToken token)
    {
        try
        {
            var documentChanged = false;
            if ((requested & DashboardChangeDomain.Documents) != 0)
                documentChanged = await documents.RevalidateSuspectAsync(SuspectDocumentPaths(batch), token);
            var recoveryContexts = RecoveryContexts(batch);
            if (recoveryContexts.Length > 0) catalog.InvalidateSourceResolutions(recoveryContexts);
            catalog.InvalidateSnapshot();
            var snapshot = await catalog.GetAsync(token);
            var currentCatalogStamp = CatalogProjectionStamp(snapshot);
            UpdateSettingsIfValid();
            await RebindWatchersAsync(snapshot, token);
            lock (stateGate)
            {
                if (catalogStamp is null || catalogStamp != currentCatalogStamp) catalogRevision++;
                if (documentsStamp is null || documentsStamp != snapshot.Revision || documentChanged) documentsRevision++;
                catalogStamp = currentCatalogStamp;
                documentsStamp = snapshot.Revision;
                dirty &= ~(DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception e) when (IsRecoverable(e))
        {
            lock (stateGate) dirty |= requested & (DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents);
        }
    }

    private async Task ReconcileJournalAsync(CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            // DashboardJournal is synchronous SQLite. Keep its bounded busy timeout on a worker so it cannot serialize
            // the other domain lanes on the coordinator's async thread.
            var marker = await Task.Run(ReadJournalMarker, token);
            JournalMarker? previous;
            lock (stateGate)
            {
                previous = journalStamp;
                if (previous is null || previous.Stamp != marker.Stamp) journalRevision++;
                journalStamp = marker;
                dirty &= ~DashboardChangeDomain.Journal;
            }
            if (previous is not null && JournalRepositoryIdentityChanged(previous, marker))
            {
                catalog.InvalidateJournalRepositories();
                lock (stateGate) dirty |= DashboardChangeDomain.Catalog;
                var now = clock.GetUtcNow();
                QueueLane(lanes[0], new PendingBatch
                {
                    Domains = DashboardChangeDomain.Catalog,
                    Immediate = true,
                    FirstAt = now,
                    LastAt = now
                });
                connections.Publish(CurrentSnapshot);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception e) when (IsRecoverable(e)) { lock (stateGate) dirty |= DashboardChangeDomain.Journal; }
        await Task.CompletedTask;
    }

    private async Task ReconcileDigestsAsync(CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            var marker = await Task.Run(ReadDigestMarker, token);
            lock (stateGate)
            {
                if (digestStamp is null || digestStamp != marker) digestRevision++;
                digestStamp = marker;
                dirty &= ~DashboardChangeDomain.Digests;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception e) when (IsRecoverable(e)) { lock (stateGate) dirty |= DashboardChangeDomain.Digests; }
        await Task.CompletedTask;
    }

    private async Task ReconcileReadsAsync(CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            var marker = FileSignature(Path.Combine(dataDirectory, "dashboard-reads", "index.json"));
            lock (stateGate)
            {
                if (readsStamp is null || readsStamp != marker) readsRevision++;
                readsStamp = marker;
                dirty &= ~DashboardChangeDomain.Reads;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception e) when (IsRecoverable(e)) { lock (stateGate) dirty |= DashboardChangeDomain.Reads; }
        await Task.CompletedTask;
    }

    private JournalMarker ReadJournalMarker()
    {
        var path = Path.Combine(dataDirectory, "journal.db");
        var files = FileSignature(path) + "|" + FileSignature(path + "-wal");
        var fileIdentity = FileLifecycleSignature(path) + "|" + FileLifecycleSignature(path + "-wal");
        var generation = Interlocked.Read(ref journalLifecycleGeneration);
        if (Directory.Exists(path)) return JournalMarkerValue("directory", null, files, fileIdentity, generation);
        var exists = File.Exists(path);
        if (!exists) return JournalMarkerValue("missing", 0, files, fileIdentity, generation);
        try { return JournalMarkerValue("available", journal.Snapshot(), files, fileIdentity, generation); }
        catch (SqliteException e) when (IsTransientStoreLock(e)) { throw; }
        catch (Exception e) when (IsRecoverable(e)) { return JournalMarkerValue("unavailable", null, files, fileIdentity, generation); }
    }

    private static JournalMarker JournalMarkerValue(string availability, long? snapshot, string files, string fileIdentity, long generation)
    {
        var life = generation.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var revision = string.Join("|", availability, snapshot?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-", "life:" + life, files);
        return new(revision, snapshot, availability, files, fileIdentity, generation);
    }

    private static bool JournalRepositoryIdentityChanged(JournalMarker previous, JournalMarker current)
        => previous.Availability != current.Availability
            || previous.LifecycleGeneration != current.LifecycleGeneration
            || (previous.Snapshot == current.Snapshot
                && (previous.FileIdentity != current.FileIdentity || previous.Files != current.Files));

    private static string FileLifecycleSignature(string path)
    {
        try
        {
            if (Directory.Exists(path)) return "directory";
            var info = new FileInfo(path);
            return info.Exists ? "present:" + info.CreationTimeUtc.Ticks : "missing";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return "unavailable"; }
    }

    private string ReadDigestMarker()
    {
        var path = Path.Combine(dataDirectory, "digests.db");
        var files = FileSignature(path) + "|" + FileSignature(path + "-wal");
        var generation = Interlocked.Read(ref digestLifecycleGeneration).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (Directory.Exists(path)) return "unavailable|directory|life:" + generation + "|" + files;
        if (!File.Exists(path)) return "available|missing|0|life:" + generation + "|" + files;
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = 2
            }.ToString());
            connection.Open();
            var version = Convert.ToInt32(SqliteCommands.Scalar(connection, "PRAGMA user_version"), System.Globalization.CultureInfo.InvariantCulture);
            if (version != DigestStore.CurrentVersion) return "unavailable|version|" + version + "|life:" + generation + "|" + files;
            var revision = Convert.ToInt64(SqliteCommands.Scalar(connection, "SELECT COALESCE(MAX(saved),0) FROM digests"), System.Globalization.CultureInfo.InvariantCulture);
            return "available|present|" + revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|life:" + generation + "|" + files;
        }
        catch (SqliteException e) when (IsTransientStoreLock(e)) { throw; }
        catch (Exception e) when (IsRecoverable(e)) { return "unavailable|present|life:" + generation + "|" + files; }
    }

    private static string FileSignature(string path)
    {
        try
        {
            if (Directory.Exists(path)) return "directory:" + Directory.GetLastWriteTimeUtc(path).Ticks;
            var info = new FileInfo(path);
            return info.Exists
                ? string.Join(":", "present", info.Length, info.LastWriteTimeUtc.Ticks, info.CreationTimeUtc.Ticks)
                : "missing";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return "unavailable"; }
    }

    private static string CatalogProjectionStamp(CatalogSnapshot snapshot) => ContentHash.Compute(Json.Encode(new
    {
        snapshot.Repositories,
        Sources = snapshot.Sources.Select(source => new { source.Id, source.Repositories, source.Location, source.Error }),
        snapshot.LaunchRepository,
        snapshot.Notices,
        snapshot.SourceNotices
    }));

    private IEnumerable<string>? SuspectDocumentPaths(PendingBatch batch)
    {
        if (batch.Overflow || batch.Hints.Any(h => (h.Domains & DashboardChangeDomain.Documents) != 0
            && (h.RelativePath is null || h.DirectoryChanged))) return null;
        var suspect = new HashSet<string>(PathComparer);
        DashboardWatchRoot[] roots;
        lock (stateGate) roots = watchRoots.Values.ToArray();
        foreach (var hint in batch.Hints.Where(h => (h.Domains & DashboardChangeDomain.Documents) != 0 && h.RelativePath is not null))
        {
            var root = roots.FirstOrDefault(item => item.Id == hint.RootId);
            if (root is null || !hint.RelativePath!.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var fullPath = Path.GetFullPath(Path.Combine(root.CanonicalPath, hint.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!SourcePrefixIsContained(fullPath, root.CanonicalPath)) continue;
                var source = catalog.GetCachedSnapshot()?.Sources.FirstOrDefault(s => IsWithinRoot(root.CanonicalPath, s.Root));
                if (source is null) continue;
                var relativeToSource = Path.GetRelativePath(source.Root, fullPath);
                suspect.Add(SourcePath.Within(source.Root, relativeToSource));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return suspect;
    }

    private string[] RecoveryContexts(PendingBatch batch)
    {
        var rootIds = batch.Hints.Where(h => h.DirectoryChanged && h.RootId is not null)
            .Select(h => h.RootId!).ToHashSet(StringComparer.Ordinal);
        lock (stateGate)
            return rootIds.Where(recoveryContextsByRoot.ContainsKey).SelectMany(id => recoveryContextsByRoot[id])
                .Distinct(PathComparer).ToArray();
    }

    private async Task RebindWatchersAsync(CatalogSnapshot snapshot, CancellationToken token)
    {
        var roots = new Dictionary<string, DashboardWatchRoot>(StringComparer.Ordinal);
        var recoveryContexts = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        void Add(string path, DashboardChangeDomain domains, bool recursive = false)
        {
            if (!Directory.Exists(path)) return;
            try
            {
                var canonical = SourcePath.Canonical(path);
                if (roots.TryGetValue(canonical, out var current))
                    roots[canonical] = current with { Domains = current.Domains | domains, Recursive = current.Recursive || recursive };
                else
                    roots[canonical] = new(FileSystemDashboardChangeSource.WatchId(canonical), canonical, domains, recursive);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
        }

        Add(dataDirectory, DashboardChangeDomain.Catalog | DashboardChangeDomain.Journal | DashboardChangeDomain.Digests | DashboardChangeDomain.Authorization);
        Add(Path.Combine(dataDirectory, "dashboard-reads"), DashboardChangeDomain.Reads);

        LocalSettings sourceSettings;
        try { sourceSettings = new LocalSettings(dataDirectory); currentSettings = sourceSettings; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or JsonException) { sourceSettings = currentSettings; }

        foreach (var contextPath in sourceSettings.Configuration.RepositoryContexts.Keys)
        {
            var checkout = Path.GetDirectoryName(contextPath);
            if (checkout is not null)
            {
                Add(checkout, DashboardChangeDomain.Catalog);
                var parent = Path.GetDirectoryName(checkout);
                if (parent is not null && !IsBroadRoot(parent)) Add(parent, DashboardChangeDomain.Catalog);
            }
        }

        foreach (var source in snapshot.Sources)
        {
            var sourceRoot = source.Root;
            if (!Directory.Exists(sourceRoot))
            {
                var parent = Path.GetFullPath(sourceRoot);
                while (!Directory.Exists(parent))
                {
                    var next = Path.GetDirectoryName(parent);
                    if (next is null || PathComparer.Equals(next, parent)) break;
                    parent = next;
                }
                if (Directory.Exists(parent) && !IsBroadRoot(parent))
                {
                    Add(parent, DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents);
                    var id = FileSystemDashboardChangeSource.WatchId(SourcePath.Canonical(parent));
                    var contexts = sourceSettings.Configuration.RepositoryContexts
                        .Where(pair => source.Repositories.Contains(pair.Value.Repository.Identity, StringComparer.Ordinal))
                        .Select(pair => Path.GetDirectoryName(pair.Key) ?? pair.Key);
                    if (!recoveryContexts.TryGetValue(id, out var values)) recoveryContexts[id] = values = new(PathComparer);
                    values.UnionWith(contexts);
                }
                continue;
            }
            var canonicalRoot = SourcePath.Canonical(sourceRoot);
            string changesPath;
            try { changesPath = SourcePath.Within(canonicalRoot, "openspec/changes"); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // The approved source root remains useful for observing a later contained config/root repair.
                Add(canonicalRoot, DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents);
                continue;
            }
            var existing = changesPath;
            while (!Directory.Exists(existing) && !PathComparer.Equals(existing, canonicalRoot))
                existing = Path.GetDirectoryName(existing) ?? canonicalRoot;
            try { existing = SourcePath.Within(canonicalRoot, Path.GetRelativePath(canonicalRoot, existing)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Add(canonicalRoot, DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents);
                continue;
            }
            Add(existing, DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents);
            if (!Directory.Exists(changesPath)) continue;
            // The approved OpenSpec subtree is the only recursive physical watcher root. Logical
            // child roots below it remain registered for event classification and containment.
            Add(changesPath, DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents, recursive: true);

            var pendingDirectories = new Stack<string>();
            var visited = new HashSet<string>(PathComparer);
            pendingDirectories.Push(changesPath);
            while (pendingDirectories.TryPop(out var directory))
            {
                token.ThrowIfCancellationRequested();
                if (!visited.Add(directory)) continue;
                Add(directory, DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents);
                foreach (var item in SourcePath.List(directory).Where(item => item.IsDirectory))
                {
                    try
                    {
                        var contained = SourcePath.Contained(canonicalRoot, item);
                        if (Directory.Exists(contained)) pendingDirectories.Push(contained);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
                }
            }
        }

        var rootList = roots.Values.ToArray();
        var byId = rootList.ToDictionary(root => root.Id, StringComparer.Ordinal);
        await changes.RebindAsync(rootList, token);
        lock (stateGate)
        {
            watchRoots = byId;
            recoveryContextsByRoot = recoveryContexts.ToDictionary(pair => pair.Key,
                pair => pair.Value.Order(PathComparer).ToArray(), StringComparer.Ordinal);
        }
    }

    private void UpdateSettingsIfValid()
    {
        try { currentSettings = new LocalSettings(dataDirectory); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or JsonException) { }
    }

    private static string[] DirtyNames(DashboardChangeDomain value)
    {
        var names = new List<string>(5);
        if ((value & DashboardChangeDomain.Catalog) != 0) names.Add("catalog");
        if ((value & DashboardChangeDomain.Documents) != 0) names.Add("documents");
        if ((value & DashboardChangeDomain.Journal) != 0) names.Add("journal");
        if ((value & DashboardChangeDomain.Digests) != 0) names.Add("digests");
        if ((value & DashboardChangeDomain.Reads) != 0) names.Add("reads");
        return names.ToArray();
    }

    private static string[] DomainNames(DashboardChangeDomain value)
    {
        var names = DirtyNames(value).ToList();
        if ((value & DashboardChangeDomain.Authorization) != 0) names.Add("authorization");
        return names.ToArray();
    }

    private static bool IsRecoverable(Exception e) => e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or JsonException or SqliteException;
    private static bool IsTransientStoreLock(SqliteException e) => e.SqliteErrorCode is SqliteBusyResultCode or SqliteLockedResultCode;
    private static bool SourcePrefixIsContained(string path, string root) => IsWithinRoot(path, root) || PathComparer.Equals(Path.GetFullPath(path), Path.GetFullPath(root));
    private static bool IsWithinRoot(string path, string root)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, PathComparison);
    }
    private static bool IsBroadRoot(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return PathComparer.Equals(full, Path.GetPathRoot(full)) || (profile.Length > 0 && PathComparer.Equals(full, Path.TrimEndingDirectorySeparator(profile)));
    }
    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        changes.Changed -= OnChange;
        try { wake.Release(); }
        catch (SemaphoreFullException) { }
        try { await base.StopAsync(cancellationToken); }
        finally { await changes.DisposeAsync(); }
    }
}
