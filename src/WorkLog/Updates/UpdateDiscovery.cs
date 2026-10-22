using System.Text.Json;
using WorkLog.Cli;
using WorkLog.Configuration;
using WorkLog.Infrastructure;
using WorkLog.Installation;

namespace WorkLog.Updates;

public sealed record UpdateCache(DateTimeOffset? AttemptedAt = null, DateTimeOffset? ValidatedAt = null,
    ReleaseInfo? Release = null, DateTimeOffset? RetryAfter = null);

public sealed class UpdateDiscovery(LocalSettings settings, ReleaseClient client, TimeProvider? timeProvider = null, string? installationRoot = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly string? root = installationRoot ?? InstalledApplication.CurrentRoot();
    public string CachePath => Path.Combine(settings.DataDirectory, "updates", "release-cache.json");

    public UpdateHint? GetHint(string? installedVersion = null)
    {
        if (!settings.Configuration.Updates.Enabled)
            return null;
        var cache = ReadCache();
        var installed = installedVersion ?? ApplicationInfo.Version;
        string? selected = null;
        var command = "worklog update";
        if (root is not null)
        {
            try
            {
                selected = InstallationLayout.ReadSelection(root, verifyPayload: false).Current.Version;
                if (cache.Release is not { } candidate || !candidate.IsNewerThan(selected))
                    return null;
                static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
                command = "& " + Quote(Path.Combine(root, "worklog.exe")) + " update --data-dir " + Quote(settings.DataDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
            {
                return null;
            }
        }
        return cache.ValidatedAt is { } validated && validated <= clock.GetUtcNow()
            && clock.GetUtcNow() - validated < TimeSpan.FromDays(7)
            && cache.Release is { } release && release.IsValid() && release.IsNewerThan(installed)
                ? new(installed, release.Version, release.ReleaseUrl, command, selected) : null;
    }

    public async Task RunAsync(CancellationToken token)
    {
        if (!settings.Configuration.Updates.Enabled)
            return;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await CheckAsync(false, token);
                await Task.Delay(TimeSpan.FromMinutes(15), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public async Task<ReleaseCheck?> CheckAsync(bool force, CancellationToken token = default)
    {
        UpdateCache cache;
        var now = clock.GetUtcNow();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            using var gate = new FileStream(CachePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            cache = ReadCache();
            if (!force && (!settings.Configuration.Updates.Enabled || cache.AttemptedAt is { } attempted && now - attempted < TimeSpan.FromDays(1)
                || cache.RetryAfter > now))
                return null;
            if (cache.RetryAfter > now)
                return new(null, "GitHub requested a later retry.", cache.RetryAfter);
            WriteCache(cache with
            {
                AttemptedAt = now
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            if (!force)
                return null;
            // Explicit checking can still work when automatic cache coordination is unavailable.
            return await client.CheckAsync(token);
        }

        var result = await client.CheckAsync(token);
        try
        {
            using var gate = new FileStream(CachePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var current = ReadCache();
            if (current.AttemptedAt == now)
                WriteCache(result.Succeeded
                    ? current with
                    {
                        ValidatedAt = clock.GetUtcNow(),
                        Release = result.Release,
                        RetryAfter = null
                    }
                    : current with
                    {
                        RetryAfter = result.RetryAfter
                    });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
        return result;
    }

    private UpdateCache ReadCache()
    {
        try
        {
            if (!File.Exists(CachePath) || new FileInfo(CachePath).Length > 16 * 1024)
                return new();
            return Json.Decode<UpdateCache>(File.ReadAllText(CachePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return new();
        }
    }

    private void WriteCache(UpdateCache cache) => AtomicFile.Write(CachePath, Json.Encode(cache));
}
