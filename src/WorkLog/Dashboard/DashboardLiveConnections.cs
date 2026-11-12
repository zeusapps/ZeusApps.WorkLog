using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace WorkLog.Dashboard;

internal sealed record DashboardRevisionVector(string Instance, long Catalog, long Documents, long Journal, long Digests, long Reads, string[] Dirty,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Checkpoint = null);
internal sealed record DashboardLiveOptions(string Instance);

/// <summary>Bounded per-connection delivery. A slow client keeps only the newest vector; authorization is checked at send time.</summary>
internal sealed class DashboardLiveConnections : BackgroundService
{
    private sealed class Slot(HubCallerContext context, string? cookie)
    {
        public HubCallerContext Context { get; } = context;
        public string? Cookie { get; } = cookie;
        private readonly object gate = new();
        private DashboardRevisionVector? latest;

        /// <summary>Wakes the send loop; the vector itself lives in <see cref="latest"/> so a replacement can keep a pending checkpoint tag.</summary>
        public Channel<bool> Signal { get; } = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        public int PendingCount { get { lock (gate) return latest is null ? 0 : 1; } }

        /// <summary>Keeps only the newest vector; a replaced pending checkpoint tag survives on the replacement.</summary>
        public bool Offer(DashboardRevisionVector vector)
        {
            lock (gate)
            {
                if (latest?.Checkpoint is { } pending && pending > (vector.Checkpoint ?? long.MinValue))
                    vector = vector with { Checkpoint = pending };
                latest = vector;
            }
            return Signal.Writer.TryWrite(true);
        }

        public DashboardRevisionVector? Take()
        {
            lock (gate)
            {
                var vector = latest;
                latest = null;
                return vector;
            }
        }

        public CancellationTokenSource Stop { get; } = CancellationTokenSource.CreateLinkedTokenSource(context.ConnectionAborted);
    }

    private readonly ConcurrentDictionary<string, Slot> connections = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task> sendLoops = new(StringComparer.Ordinal);
    private readonly DashboardAuthorization authorization;
    private readonly IHubContext<DashboardLiveHub> hub;
    private readonly TimeProvider clock;
    private long checkpointCount;
    private long deliveryCount;
    private int maxPendingNotificationCount;
    private int stopping;
    private readonly CancellationTokenRegistration stoppingRegistration;

    /// <param name="lifetime">When supplied, live connections are refused and aborted as soon as the host begins stopping. Kestrel stops before hosted services and waits for open connections, so waiting for <see cref="StopAsync"/> would hold a graceful shutdown until the host timeout.</param>
    public DashboardLiveConnections(DashboardAuthorization authorization, IHubContext<DashboardLiveHub> hub, TimeProvider clock,
        IHostApplicationLifetime? lifetime = null)
    {
        this.authorization = authorization;
        this.hub = hub;
        this.clock = clock;
        if (lifetime is not null) stoppingRegistration = lifetime.ApplicationStopping.Register(BeginStop);
    }

    public override void Dispose()
    {
        stoppingRegistration.Dispose();
        base.Dispose();
    }

    /// <summary>Refuses new live connections and aborts every registered one. Safe to call repeatedly.</summary>
    private void BeginStop()
    {
        Interlocked.Exchange(ref stopping, 1);
        foreach (var slot in connections.Values) AbortAndRemove(slot);
    }

    internal int ActiveConnectionCount => connections.Count;
    internal bool StopRequested => Volatile.Read(ref stopping) != 0;
    internal long CheckpointCount => Interlocked.Read(ref checkpointCount);
    internal long DeliveryCount => Interlocked.Read(ref deliveryCount);
    internal int PendingHintCount => connections.Values.Sum(slot => slot.PendingCount);
    internal int MaxPendingNotificationCount => Volatile.Read(ref maxPendingNotificationCount);

    internal bool Add(HubCallerContext context)
    {
        if (Volatile.Read(ref stopping) != 0)
        {
            context.Abort();
            return false;
        }
        var cookie = context.GetHttpContext()?.Request.Cookies[authorization.CookieName];
        if (!authorization.Valid(cookie))
        {
            context.Abort();
            return false;
        }
        var slot = new Slot(context, cookie);
        if (!connections.TryAdd(context.ConnectionId, slot))
        {
            slot.Stop.Dispose();
            context.Abort();
            return false;
        }
        if (Volatile.Read(ref stopping) != 0)
        {
            AbortAndRemove(slot);
            return false;
        }
        var sendLoop = SendLoopAsync(context.ConnectionId, slot);
        sendLoops[context.ConnectionId] = sendLoop;
        _ = sendLoop.ContinueWith(completed => { sendLoops.TryRemove(context.ConnectionId, out var ignored); }, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return true;
    }

    internal void Remove(string connectionId)
    {
        if (!connections.TryRemove(connectionId, out var slot)) return;
        // The send loop may already have ended and disposed the token between TryRemove and here.
        try { slot.Stop.Cancel(); }
        catch (ObjectDisposedException) { }
        slot.Signal.Writer.TryComplete();
    }

    internal void Publish(DashboardRevisionVector vector, bool checkpoint = false)
    {
        if (checkpoint)
        {
            var sequence = Interlocked.Increment(ref checkpointCount);
            vector = vector with { Checkpoint = sequence };
        }
        foreach (var slot in connections.Values)
        {
            if (!slot.Offer(vector)) AbortAndRemove(slot);
            else UpdateMaxPendingNotificationCount(PendingHintCount);
        }
    }

    private void UpdateMaxPendingNotificationCount(int count)
    {
        var current = Volatile.Read(ref maxPendingNotificationCount);
        while (count > current)
        {
            var observed = Interlocked.CompareExchange(ref maxPendingNotificationCount, count, current);
            if (observed == current) return;
            current = observed;
        }
    }

    internal void RevokeInvalidConnections() => RevokeInvalidConnectionsCore();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                RevokeInvalidConnectionsCore();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private void RevokeInvalidConnectionsCore()
    {
        foreach (var pair in connections)
            if (!authorization.Valid(pair.Value.Cookie)) AbortAndRemove(pair.Value);
    }

    private void AbortAndRemove(Slot slot)
    {
        try { slot.Context.Abort(); }
        catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException) { }
        Remove(slot.Context.ConnectionId);
    }

    private async Task SendLoopAsync(string connectionId, Slot slot)
    {
        try
        {
            await foreach (var signal in slot.Signal.Reader.ReadAllAsync(slot.Stop.Token))
            {
                if (slot.Take() is not { } vector) continue;
                if (!authorization.Valid(slot.Cookie))
                {
                    AbortAndRemove(slot);
                    return;
                }
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2), clock);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(slot.Stop.Token, timeout.Token);
                try
                {
                    await hub.Clients.Client(connectionId).SendAsync("Revision", vector, deadline.Token);
                    Interlocked.Increment(ref deliveryCount);
                }
                catch (OperationCanceledException) when (!slot.Stop.IsCancellationRequested)
                {
                    AbortAndRemove(slot);
                    return;
                }
                catch (InvalidOperationException)
                {
                    AbortAndRemove(slot);
                    return;
                }
                catch (Exception e) when (e is IOException or ObjectDisposedException or HubException or System.Net.WebSockets.WebSocketException)
                {
                    AbortAndRemove(slot);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (slot.Stop.IsCancellationRequested) { }
        finally
        {
            Remove(connectionId);
            slot.Stop.Dispose();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        BeginStop();
        await base.StopAsync(cancellationToken);
        var pending = sendLoops.Values.ToArray();
        if (pending.Length > 0) await Task.WhenAll(pending).WaitAsync(cancellationToken);
    }
}

internal sealed class DashboardLiveHub(DashboardLiveConnections connections, DashboardLiveCoordinator coordinator,
    DashboardAuthorization authorization) : Hub
{
    public override Task OnConnectedAsync()
    {
        if (!connections.Add(Context)) return Task.CompletedTask;
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Returns the current complete vector. It accepts no client-selected source or filesystem target.</summary>
    public DashboardRevisionVector Snapshot()
    {
        var cookie = Context.GetHttpContext()?.Request.Cookies[authorization.CookieName];
        if (!authorization.Valid(cookie))
        {
            Context.Abort();
            throw new HubException("Dashboard authorization is no longer valid.");
        }
        return coordinator.CurrentSnapshot;
    }
}
