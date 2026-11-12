using System.Globalization;
using System.Text.Json.Serialization;
using WorkLog.Journaling;
using WorkLog.Persistence;
using WorkLog.Reporting;

namespace WorkLog.Dashboard;

/// <param name="Before">The start of the current local day, computed by the client.</param>
/// <param name="Day">An explicit earlier local date (yyyy-MM-dd); omitted for the last recorded day.</param>
internal sealed record DailyRequest(string Before, string Timezone, string[]? Repositories = null, string? Day = null);
internal sealed record DailyItem(string Id, string Title, string Repository, Ticket[] Tickets, string[] Changes, string Status, Entry Primary, Entry[] Others);
/// <param name="Covers">The sorted IDs of the observations a stored agent digest covers.</param>
/// <param name="Text">The single readable text; for a structured digest, the flattened form.</param>
/// <param name="Done">The structured bullets, present only while they still belong to the stored digest (otherwise omitted).</param>
internal sealed record DayDigest(string[] Covers, string Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Done = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Next = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Blockers = null);
internal sealed record DailyDigestIssue(string Category, string Message);
internal sealed record DailyDigest(string? Day, string? PreviousDay, string? NextDay, DailyItem[] Items, DailyItem[] Today, DayDigest[] Digests, DailyDigestIssue? DigestIssue = null);

internal sealed class DailyService(DashboardJournal journal, DigestStore? digests = null)
{
    private const string DateFormat = "yyyy-MM-dd";

    public DailyDigest Query(DailyRequest request, CancellationToken cancellationToken = default)
    {
        var before = TimestampParser.Parse(request.Before, "before");
        // Validates the timezone; the period itself is only a vehicle for it.
        var zone = ReportingPeriod.FromRequest(new QueryRequest(request.Before, before.AddDays(1).ToString("O", CultureInfo.InvariantCulture), request.Timezone)).Zone;
        var repositories = request.Repositories ?? [];
        var currentDate = LocalDate(before, zone);
        var tomorrow = Midnight(currentDate.AddDays(1), zone);
        DateOnly? requested = null;
        if (request.Day is not null)
        {
            if (!DateOnly.TryParseExact(request.Day, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                throw new ArgumentException("day must be a local date in yyyy-MM-dd format.");
            if (parsed >= currentDate)
                throw new ArgumentException("day must be earlier than the current local date.");
            requested = parsed;
        }

        // One read up to the end of the current day serves both sections.
        var data = journal.Read(tomorrow, repositories, null, cancellationToken);
        var today = Items(data.Entries, before, tomorrow, zone, data.Snapshot);

        var day = requested ?? (journal.LatestBefore(before, repositories, data.Snapshot) is { } latest ? LocalDate(latest, zone) : (DateOnly?)null);
        if (day is null)
        {
            var (todayDigests, issue) = Digests(today);
            return new(null, null, null, [], today, todayDigests, issue);
        }
        var start = Midnight(day.Value, zone);
        var end = Midnight(day.Value.AddDays(1), zone);
        var items = Items(data.Entries.Where(e => e.OccurredAt < end).ToArray(), start, end, zone, data.Snapshot);
        var previous = journal.LatestBefore(start, repositories, data.Snapshot);
        var next = journal.EarliestBetween(end, before, repositories, data.Snapshot);
        var (dayDigests, digestIssue) = Digests(items, today);
        return new(day.Value.ToString(DateFormat, CultureInfo.InvariantCulture),
            previous is { } p ? LocalDate(p, zone).ToString(DateFormat, CultureInfo.InvariantCulture) : null,
            next is { } n ? LocalDate(n, zone).ToString(DateFormat, CultureInfo.InvariantCulture) : null,
            items, today, dayDigests, digestIssue);
    }

    /// <summary>Stored digests of the items in the response. Missing storage is normal; read failures are reported separately.</summary>
    private (DayDigest[] Digests, DailyDigestIssue? Issue) Digests(params DailyItem[][] sections)
    {
        if (digests is null) return ([], null);
        try
        {
            var ids = sections.SelectMany(s => s).Where(i => !i.Id.StartsWith("adhoc:", StringComparison.Ordinal)).Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
            return (digests.Read(ids).Select(d => new DayDigest(d.Covers, d.Text, d.Parts?.Done, d.Parts?.Next, d.Parts?.Blockers)).ToArray(), null);
        }
        catch
        {
            return ([], new("unavailable", "Saved digests are temporarily unavailable."));
        }
    }

    private static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>The first instant of a local date; a date whose midnight is skipped by a clock change starts at the first valid time.</summary>
    internal static DateTimeOffset Midnight(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local))
            local = local.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    /// <summary>
    /// One item per work item with observations in [from, to), using ticket grouping with status as of <paramref name="to"/>.
    /// Entries must already end before <paramref name="to"/>. Unlinked entries become one item each.
    /// </summary>
    private static DailyItem[] Items(Entry[] entries, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo zone, long snapshot)
    {
        bool Unlinked(Entry e) => e.Change is null && e.Tickets.Length == 0;
        var query = new TimelineQuery(from.ToString("O", CultureInfo.InvariantCulture), to.ToString("O", CultureInfo.InvariantCulture), zone.Id);
        var page = TimelineService.Project(query, new ReportingPeriod(from, to, zone), snapshot, entries.Where(e => !Unlinked(e)).ToArray(), int.MaxValue);
        var inPeriod = entries.Where(e => e.OccurredAt >= from && e.OccurredAt < to).ToArray();
        var byIdentity = inPeriod.Where(e => !Unlinked(e)).GroupBy(e => TimelineService.IdFor(e, "ticket")).ToDictionary(g => g.Key, g => g.ToArray());
        var items = new List<DailyItem>();
        foreach (var row in page.Rows)
        {
            var ordered = byIdentity[row.Id].OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id, StringComparer.Ordinal).ToArray();
            items.Add(new(row.Id, row.Title, row.Repository, row.Tickets, row.Changes, row.Status, ordered[0], ordered[1..]));
        }
        foreach (var entry in inPeriod.Where(Unlinked))
            items.Add(new("adhoc:" + entry.Id, entry.Summary?.Trim() ?? "", entry.Repository, [], [], entry.Status, entry, []));
        return items.OrderBy(i => Rank(i.Status)).ThenByDescending(i => i.Primary.OccurredAt)
            .ThenBy(i => i.Title, StringComparer.Ordinal).ThenBy(i => i.Id, StringComparer.Ordinal).ToArray();
    }

    private static int Rank(string status) => status switch { "done" => 0, "in_progress" => 1, _ => 2 };
}
