using System.Globalization;
using WorkLog.Journaling;

namespace WorkLog.Reporting;

internal static class ReportBuilder
{
    public static Report Build(QueryRequest request, ReportingPeriod period, IEnumerable<Entry> observations)
    {
        var entries = observations.Where(entry => Matches(entry, request)).ToArray();
        var days = entries
            .GroupBy(entry => (
                Date: TimeZoneInfo.ConvertTime(entry.OccurredAt, period.Zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Work: WorkIdentity(entry)))
            .Select(group => new DailyGroup(group.Key.Date, group.Key.Work, group.ToArray()))
            .ToArray();

        return new Report(1, period.From.ToUniversalTime(), period.To.ToUniversalTime(), period.Zone.Id, entries, days);
    }

    private static bool Matches(Entry entry, QueryRequest request) =>
        (request.Repository is null || entry.Repository == request.Repository)
        && (request.Change is null || entry.Change == request.Change)
        && (request.Status is null || entry.Status == request.Status)
        && (request.Classification is null || entry.Classification == request.Classification)
        && (request.Ticket is null || entry.Tickets.Any(ticket =>
            ticket.Id == request.Ticket || $"{ticket.Provider}:{ticket.Scope}:{ticket.Id}" == request.Ticket));

    private static string WorkIdentity(Entry entry)
    {
        if (entry.Change is not null)
        {
            return $"{entry.Repository}/change/{entry.Change}";
        }

        var ticket = entry.Tickets[0];
        return $"{entry.Repository}/ticket/{ticket.Provider}:{ticket.Scope}:{ticket.Id}";
    }
}
