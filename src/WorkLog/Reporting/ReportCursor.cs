using System.Text;
using System.Text.Json;
using WorkLog.Infrastructure;

namespace WorkLog.Reporting;

internal sealed record ReportCursor(string QueryHash, long Snapshot, long OccurredTicks, string Id)
{
    public string Encode() => Convert.ToBase64String(Encoding.UTF8.GetBytes(Json.Encode(this)))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static ReportCursor? Parse(string? token, string queryHash, ReportingPeriod period)
    {
        if (token is null)
            return null;
        try
        {
            if (token.Length > 4096)
                throw new FormatException();
            var base64 = token.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            var cursor = Json.Decode<ReportCursor>(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
            if (cursor.QueryHash != queryHash || cursor.Snapshot < 1 || !Guid.TryParseExact(cursor.Id, "N", out _)
                || cursor.OccurredTicks < period.From.UtcTicks || cursor.OccurredTicks >= period.To.UtcTicks)
                throw new FormatException();
            return cursor;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            throw new ArgumentException("period.cursor is invalid or belongs to different query options.");
        }
    }
}
