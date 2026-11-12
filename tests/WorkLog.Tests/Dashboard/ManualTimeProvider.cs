namespace WorkLog.Tests.Dashboard;

/// <summary>TimeProvider whose timers run only when a test advances its UTC clock.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object gate = new();
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset now;

    public ManualTimeProvider(DateTimeOffset? start = null) => now = start ?? DateTimeOffset.UnixEpoch;

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate) return now;
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        lock (gate) return now.UtcTicks;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new ManualTimer(this, callback, state);
        Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    public DateTimeOffset[] ScheduledDueTimes
    {
        get
        {
            lock (gate) return timers.Where(timer => timer.DueTicks is not null)
                .Select(timer => new DateTimeOffset(timer.DueTicks!.Value, TimeSpan.Zero)).Order().ToArray();
        }
    }

    public void Advance(TimeSpan amount)
    {
        if (amount < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(amount));
        DateTimeOffset target;
        lock (gate) target = now + amount;
        while (true)
        {
            ManualTimer? timer;
            TimerCallback? callback;
            object? state;
            lock (gate)
            {
                timer = timers.Where(item => item.DueTicks is not null && item.DueTicks <= target.UtcTicks)
                    .OrderBy(item => item.DueTicks).FirstOrDefault();
                if (timer is null)
                {
                    now = target;
                    return;
                }
                now = new DateTimeOffset(timer.DueTicks!.Value, TimeSpan.Zero);
                (callback, state) = timer.Fire();
            }
            callback?.Invoke(state);
        }
    }

    private void Add(ManualTimer timer)
    {
        lock (gate) timers.Add(timer);
    }

    private void Change(ManualTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        Validate(dueTime, nameof(dueTime));
        Validate(period, nameof(period));
        lock (gate)
        {
            if (timer.IsDisposed) return;
            timer.PeriodTicks = period <= TimeSpan.Zero ? null : period.Ticks;
            timer.DueTicks = dueTime == Timeout.InfiniteTimeSpan ? null : now.UtcTicks + dueTime.Ticks;
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (gate)
        {
            timer.MarkDisposed();
            timers.Remove(timer);
        }
    }

    private static void Validate(TimeSpan value, string name)
    {
        if (value < Timeout.InfiniteTimeSpan) throw new ArgumentOutOfRangeException(name);
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        internal long? DueTicks { get; set; }
        internal long? PeriodTicks { get; set; }
        internal bool IsDisposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            owner.Change(this, dueTime, period);
            return !IsDisposed;
        }

        internal (TimerCallback? Callback, object? State) Fire()
        {
            if (IsDisposed || DueTicks is null) return (null, null);
            DueTicks = PeriodTicks is { } period ? DueTicks.Value + period : null;
            return (callback, state);
        }

        internal void MarkDisposed()
        {
            IsDisposed = true;
            DueTicks = null;
            PeriodTicks = null;
        }

        public void Dispose() => owner.Remove(this);
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
