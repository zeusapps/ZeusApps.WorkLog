using System.Globalization;
using WorkLog.Infrastructure;
using WorkLog.Journaling;
using WorkLog.Reporting;

namespace WorkLog.Dashboard;

internal sealed record TimelineQuery(string From, string To, string Timezone, string GroupBy = "ticket", string[]? Repositories = null, long? Snapshot = null, int Offset = 0);
internal sealed record TimelineRow(string Id, string Repository, string Title, string Status, DateTimeOffset FirstAt, DateTimeOffset? StartedAt,
    DateTimeOffset LastAt, double ElapsedSeconds, string[] ActivityDates, string[] CompletionDates, string[] ResumedDates,
    string[] Changes, Ticket[] Tickets, int ObservationCount, int VisibleCount, bool ClippedStart);
internal sealed record InsightItem(string ItemId, string Title, string Repository, DateTimeOffset At);
internal sealed record ActivityDay(string Date, int Items);
internal sealed record TimelineInsights(TimelineRow[] LongestSpans, InsightItem[] Completions, InsightItem[] SubsequentWork, ActivityDay[] Days);
internal sealed record TimelinePage(long Snapshot, TimelineRow[] Rows, int Total, int? NextOffset, TimelineInsights Insights);
internal sealed record ObservationPage(Entry[] Entries, int Total, int? NextOffset);

internal sealed class TimelineService(DashboardJournal journal)
{
    internal static string IdFor(Entry entry, string groupBy) => ContentHash.Compute(WorkIdentity.For(entry, groupBy));

    private static ReportingPeriod Period(TimelineQuery query)
    {
        if (query.GroupBy is not ("ticket" or "change")) throw new ArgumentException("groupBy must be ticket or change.");
        if (query.Offset < 0) throw new ArgumentException("offset must not be negative.");
        return ReportingPeriod.FromRequest(new QueryRequest(query.From, query.To, query.Timezone));
    }

    public TimelinePage Query(TimelineQuery query, CancellationToken cancellationToken = default)
    {
        var period = Period(query);
        var data = journal.Read(period.To, query.Repositories ?? [], query.Snapshot, cancellationToken);
        return Project(query, period, data.Snapshot, data.Entries);
    }

    internal static TimelinePage Project(TimelineQuery query, ReportingPeriod period, long snapshot, Entry[] entries)
    {
        string Day(Entry entry) => TimeZoneInfo.ConvertTime(entry.OccurredAt, period.Zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var rows = new List<TimelineRow>();
        var completions = new List<InsightItem>();
        var resumed = new List<InsightItem>();
        var days = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var group in entries.GroupBy(e => IdFor(e, query.GroupBy)))
        {
            var all = group.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
            var visible = all.Where(e => e.OccurredAt >= period.From).ToArray();
            if (visible.Length == 0) continue;
            var first = all[0];
            var last = all[^1];
            var changes = all.Select(e => e.Change).OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
            var tickets = all.SelectMany(e => e.Tickets).DistinctBy(t => (t.Provider, t.Scope, t.Id)).ToArray();
            var title = query.GroupBy == "ticket" && first.Tickets.Length > 0
                ? string.Join(" + ", first.Tickets.Select(t => t.Id).Distinct().Order(StringComparer.Ordinal))
                : first.Change ?? string.Join(" + ", tickets.Select(t => t.Id));
            var subsequent = new List<Entry>();
            var hadCompletion = false;
            foreach (var entry in all)
            {
                if (entry.Status == "done") hadCompletion = true;
                else if (hadCompletion) { subsequent.Add(entry); hadCompletion = false; }
            }
            var row = new TimelineRow(group.Key, first.Repository, title, last.Status, first.OccurredAt,
                all.FirstOrDefault(e => e.Status == "started")?.OccurredAt, last.OccurredAt,
                (last.OccurredAt - first.OccurredAt).TotalSeconds,
                visible.Select(Day).Distinct().Order(StringComparer.Ordinal).ToArray(),
                visible.Where(e => e.Status == "done").Select(Day).Distinct().ToArray(),
                subsequent.Where(e => e.OccurredAt >= period.From).Select(Day).Distinct().ToArray(),
                changes, tickets, all.Length, visible.Length, first.OccurredAt < period.From);
            rows.Add(row);
            foreach (var entry in visible)
            {
                var day = Day(entry);
                if (!days.TryGetValue(day, out var identities)) days[day] = identities = new(StringComparer.Ordinal);
                identities.Add(group.Key);
                if (entry.Status == "done") completions.Add(new(group.Key, title, first.Repository, entry.OccurredAt));
            }
            resumed.AddRange(subsequent.Where(e => e.OccurredAt >= period.From).Select(e => new InsightItem(group.Key, title, first.Repository, e.OccurredAt)));
        }
        var ordered = rows.OrderByDescending(r => r.LastAt).ThenBy(r => r.Id, StringComparer.Ordinal).ToArray();
        var page = ordered.Skip(query.Offset).Take(50).ToArray();
        return new(snapshot, page, ordered.Length, query.Offset + page.Length < ordered.Length ? query.Offset + page.Length : null,
            new(ordered.OrderByDescending(r => r.ElapsedSeconds).Take(5).ToArray(),
                completions.OrderByDescending(x => x.At).Take(10).ToArray(), resumed.OrderByDescending(x => x.At).Take(10).ToArray(),
                days.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new ActivityDay(x.Key, x.Value.Count)).ToArray()));
    }

    public ObservationPage Observations(TimelineQuery query, string id, int offset, CancellationToken cancellationToken = default, string? day = null)
    {
        var period = Period(query);
        if (offset < 0) throw new ArgumentException("offset must not be negative.");
        if (day is not null && !DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new ArgumentException("day must be a local date in yyyy-MM-dd format.");
        var data = journal.Read(period.To, query.Repositories ?? [], query.Snapshot, cancellationToken);
        var entries = data.Entries.Where(e => IdFor(e, query.GroupBy) == id && (day is null ||
            TimeZoneInfo.ConvertTime(e.OccurredAt, period.Zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) == day)).ToArray();
        var page = entries.Skip(offset).Take(100).ToArray();
        return new(page, entries.Length, offset + page.Length < entries.Length ? offset + page.Length : null);
    }
}
