using System.Globalization;
using WorkLog.Journaling;

namespace WorkLog.Reporting;

internal static class ReportBuilder
{
    public static Report Build(QueryRequest request, ReportingPeriod period, Entry[] entries, bool hasMore, string? nextCursor)
    {
        var days = request.View == "flat" ? [] : entries
            .GroupBy(entry => (
                Date: TimeZoneInfo.ConvertTime(entry.OccurredAt, period.Zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Work: WorkIdentity.For(entry)))
            .Select(group => new DailyGroup(group.Key.Date, group.Key.Work,
                request.View == "compact" ? [] : group.ToArray(),
                request.View == "compact" ? group.Select(entry => entry.Id).ToArray() : []))
            .ToArray();

        return new Report(1, period.From.ToUniversalTime(), period.To.ToUniversalTime(), period.Zone.Id,
            request.View == "grouped" ? [] : entries, days, request.View, request.PageSize, hasMore, nextCursor);
    }
}
