using WorkLog.Journaling;

namespace WorkLog.Reporting;

internal sealed record ReportingPeriod(DateTimeOffset From, DateTimeOffset To, TimeZoneInfo Zone)
{
    public static ReportingPeriod FromRequest(QueryRequest request)
    {
        var from = TimestampParser.Parse(request.From);
        var to = TimestampParser.Parse(request.To);
        if (to <= from)
        {
            throw new ArgumentException("to must be later than from.");
        }

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(request.Timezone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException("Unknown reporting timezone.", exception);
        }

        if (request.Status is not null && request.Status is not ("started" or "in_progress" or "done"))
        {
            throw new ArgumentException("Invalid status filter.");
        }

        return new ReportingPeriod(from, to, zone);
    }
}
