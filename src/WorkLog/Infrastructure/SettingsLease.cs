namespace WorkLog.Infrastructure;

internal sealed class SettingsLease : IDisposable
{
    private readonly Mutex mutex;
    public SettingsLease(string directory)
    {
        // No files are created during dry-run. Settings operations are synchronous.
        var canonical = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (OperatingSystem.IsWindows())
            canonical = canonical.ToUpperInvariant();
        mutex = new Mutex(false, "worklog-settings-" + ContentHash.Compute(canonical));
        try
        {
            if (!mutex.WaitOne(TimeSpan.FromSeconds(30)))
                throw new IOException("Settings are busy. Retry after the other writer finishes.");
        }
        catch (AbandonedMutexException) { /* This process now owns the lock; reload disk state. */ }
        catch { mutex.Dispose(); throw; }
    }
    public void Dispose()
    {
        mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
