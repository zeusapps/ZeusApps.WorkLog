using System.Globalization;
using System.Text.RegularExpressions;
using WorkLog.Dashboard;
using WorkLog.Persistence;

namespace WorkLog.Journaling;

/// <summary>
/// Computes digest requests for logged observations and validates and stores agent-written day digests.
/// Work items use ticket grouping; local days use the machine's timezone. Digests never change observations.
/// </summary>
public sealed partial class DigestService(Journal journal, DigestStore store, TimeZoneInfo? zone = null,
    Action<string>? diagnosticSink = null, TimeProvider? timeProvider = null)
{
    internal const int MinimumEntries = 1;
    internal const int MaximumEntries = 30;
    internal const int MaximumTextLength = 280;
    internal const int MaximumBulletLength = 120;
    internal const int MaximumDone = 4;
    internal const int MaximumNext = 3;
    internal const int MaximumBlockers = 2;
    internal const int MinimumDone = 1;
    internal const string Instruction = LoggingGuidance.DigestInstruction;

    private readonly TimeZoneInfo zone = zone ?? TimeZoneInfo.Local;
    private readonly TimeProvider diagnosticClock = timeProvider ?? TimeProvider.System;
    private readonly object diagnosticGate = new();
    private readonly Dictionary<string, DateTimeOffset> lastDiagnostic = new(StringComparer.Ordinal);

    /// <summary>The digest request for a newly created observation, or null. Never throws: a failure only drops the request.</summary>
    public DigestRequest? Request(Entry created)
    {
        try
        {
            if (Unlinked(created))
                return null;
            var item = TimelineService.IdFor(created, "ticket");
            var day = LocalDay(created);
            var entries = EntriesOf(item, day);
            if (entries.Length < MinimumEntries || entries.Length > MaximumEntries)
                return null;
            store.EnsureAvailable();
            return new DigestRequest(item, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), zone.Id,
                entries.Select(e => new DigestEntry(e.Id, e.OccurredAt, e.Status, e.Change, e.Tickets, e.Summary)).ToArray(), Instruction);
        }
        catch (Exception exception)
        {
            ReportOptionalFailure(exception);
            return null;
        }
    }

    private void ReportOptionalFailure(Exception exception)
    {
        if (diagnosticSink is null) return;
        var category = exception switch
        {
            DigestStoreUnavailableException => "store-unavailable",
            IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException => "storage-read",
            _ => "request-failed"
        };
        var now = diagnosticClock.GetUtcNow();
        lock (diagnosticGate)
        {
            if (lastDiagnostic.TryGetValue(category, out var previous) && now - previous < TimeSpan.FromMinutes(1)) return;
            lastDiagnostic[category] = now;
        }
        try { diagnosticSink($"Optional digest request skipped ({category})."); }
        catch { /* Diagnostics must never change the result of a saved observation. */ }
    }

    /// <summary>Saves a legacy single-text digest as the only Done bullet (the form earlier clients and fixtures use).</summary>
    public SavedDigest Save(string[] entryIds, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Save(entryIds, null, null, null, text);
    }

    /// <summary>
    /// Saves a digest. Exactly one form is accepted: the <paramref name="done"/>, <paramref name="next"/> and <paramref name="blockers"/> lists,
    /// or the legacy <paramref name="text"/>, which is stored as the only Done bullet.
    /// </summary>
    public SavedDigest Save(string[] entryIds, string[]? done = null, string[]? next = null, string[]? blockers = null, string? text = null)
    {
        ArgumentNullException.ThrowIfNull(entryIds);
        var (parts, flat) = Normalize(done, next, blockers, text);
        try
        {
            store.EnsureAvailable();
        }
        catch (DigestStoreUnavailableException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Digest storage is unavailable: " + exception.Message, exception);
        }

        var ids = entryIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (ids.Length is < MinimumEntries or > MaximumEntries)
            throw new ArgumentException($"A digest covers {MinimumEntries} to {MaximumEntries} distinct observation IDs.");

        try
        {
            var found = journal.ReadByIds(ids);
            var unknown = ids.Except(found.Select(e => e.Id), StringComparer.Ordinal).FirstOrDefault();
            if (unknown is not null)
                throw new ArgumentException($"Unknown observation ID '{unknown}'.");
            if (found.Any(Unlinked))
                throw new ArgumentException("Observations without a change or ticket do not belong to a work item and cannot be digested.");
            var items = found.Select(e => TimelineService.IdFor(e, "ticket")).Distinct(StringComparer.Ordinal).ToArray();
            if (items.Length != 1)
                throw new ArgumentException("The observations belong to different work items. Pass exactly the IDs of one digest request.");
            var days = found.Select(LocalDay).Distinct().ToArray();
            if (days.Length != 1)
                throw new ArgumentException("The observations fall on different local days. Pass exactly the IDs of one digest request.");

            var current = EntriesOf(items[0], days[0]).Select(e => e.Id).Order(StringComparer.Ordinal).ToArray();
            if (!current.SequenceEqual(ids, StringComparer.Ordinal))
                throw new ArgumentException("Stale: the IDs are not exactly the item's observations for that day (one is missing or a later one exists). The digest was not stored; do not retry.");

            var day = days[0].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            store.Save(new StoredDigest(items[0], day, ids, flat, DateTimeOffset.UtcNow, parts));
            return new SavedDigest(items[0], day, ids, flat);
        }
        catch (Exception exception) when (exception is not (ArgumentException or DigestStoreUnavailableException))
        {
            throw new InvalidOperationException("Digest could not be saved: " + exception.Message, exception);
        }
    }

    /// <summary>Validates one input form and returns the parts and the single text stored beside them.</summary>
    private static (DigestParts? Parts, string Text) Normalize(string[]? done, string[]? next, string[]? blockers, string? text)
    {
        var lists = done is not null || next is not null || blockers is not null;
        if (lists == (text is not null))
            throw new ArgumentException("Provide either done (with optional next and blockers) or the legacy text, not both and not neither.");
        if (text is not null)
        {
            var single = Clean(text);
            if (single.Length == 0 || single.Length > MaximumTextLength)
                throw new ArgumentException($"Digest text must be 1 to {MaximumTextLength} characters after trimming and replacing line breaks with spaces.");
            // Exactly what a v0.4.1 save writes: the plain text and no parts row. Readers show it as the only Done bullet.
            return (null, single);
        }
        var parts = new DigestParts(Bullets("done", done, MinimumDone, MaximumDone), Bullets("next", next, 0, MaximumNext), Bullets("blockers", blockers, 0, MaximumBlockers));
        return (parts, parts.Flatten());
    }

    private static string[] Bullets(string name, string[]? bullets, int minimum, int maximum)
    {
        bullets ??= [];
        if (bullets.Length < minimum || bullets.Length > maximum)
            throw new ArgumentException($"{name} takes {minimum} to {maximum} bullets but got {bullets.Length}.");
        return bullets.Select((bullet, index) =>
        {
            var cleaned = Clean(bullet ?? "");
            if (cleaned.Length == 0)
                throw new ArgumentException($"{name}[{index}] is empty; each bullet must have text.");
            if (cleaned.Length > MaximumBulletLength)
                throw new ArgumentException($"{name}[{index}] is {cleaned.Length} characters; at most {MaximumBulletLength} after trimming and replacing line breaks with spaces.");
            return cleaned;
        }).ToArray();
    }

    private static string Clean(string value) => Whitespace().Replace(value.Trim(), " ");

    private static bool Unlinked(Entry entry) => entry.Change is null && entry.Tickets.Length == 0;

    private DateOnly LocalDay(Entry entry) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(entry.OccurredAt, zone).DateTime);

    private Entry[] EntriesOf(string item, DateOnly day) =>
        journal.ReadBetween(DailyService.Midnight(day, zone), DailyService.Midnight(day.AddDays(1), zone))
            .Where(e => !Unlinked(e) && TimelineService.IdFor(e, "ticket") == item)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
