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
    internal const int MinimumEntries = 2;
    internal const int MaximumEntries = 30;
    internal const int MaximumTextLength = 280;
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

    public SavedDigest Save(string[] entryIds, string text)
    {
        ArgumentNullException.ThrowIfNull(entryIds);
        ArgumentNullException.ThrowIfNull(text);
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

        var normalized = Whitespace().Replace(text.Trim(), " ");
        if (normalized.Length == 0 || normalized.Length > MaximumTextLength)
            throw new ArgumentException($"Digest text must be 1 to {MaximumTextLength} characters after trimming and replacing line breaks with spaces.");
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
                throw new ArgumentException("Stale: the item's observations for that day changed (a later observation exists). The digest was not stored; do not retry.");

            var day = days[0].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            store.Save(new StoredDigest(items[0], day, ids, normalized, DateTimeOffset.UtcNow));
            return new SavedDigest(items[0], day, ids, normalized);
        }
        catch (Exception exception) when (exception is not (ArgumentException or DigestStoreUnavailableException))
        {
            throw new InvalidOperationException("Digest could not be saved: " + exception.Message, exception);
        }
    }

    private static bool Unlinked(Entry entry) => entry.Change is null && entry.Tickets.Length == 0;

    private DateOnly LocalDay(Entry entry) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(entry.OccurredAt, zone).DateTime);

    private Entry[] EntriesOf(string item, DateOnly day) =>
        journal.ReadBetween(DailyService.Midnight(day, zone), DailyService.Midnight(day.AddDays(1), zone))
            .Where(e => !Unlinked(e) && TimelineService.IdFor(e, "ticket") == item)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
