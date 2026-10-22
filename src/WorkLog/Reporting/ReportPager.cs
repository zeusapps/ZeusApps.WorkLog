using System.Text.Json;
using WorkLog.Infrastructure;
using WorkLog.Journaling;
using WorkLog.Persistence;

namespace WorkLog.Reporting;

internal sealed class ReportPager(SqliteEntryStore store, string journalIdentity)
{
    public const int MaximumStructuredBytes = 64 * 1024;
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web);

    public Report Query(QueryRequest request)
    {
        request = Normalize(request);
        var period = ReportingPeriod.FromRequest(request);
        var queryHash = ContentHash.Compute(journalIdentity + Json.Encode(request with
        {
            Cursor = null
        }));
        var cursor = ReportCursor.Parse(request.Cursor, queryHash, period);
        var page = store.ReadPage(request, period, cursor);
        Report result = ReportBuilder.Build(request, period, [], false, null);
        var accepted = new List<Entry>();
        foreach (var entry in page.Entries.Take(request.PageSize))
        {
            accepted.Add(entry);
            var hasMore = accepted.Count < page.Entries.Length;
            var next = hasMore ? new ReportCursor(queryHash, page.Snapshot, entry.OccurredAt.UtcTicks, entry.Id).Encode() : null;
            var candidate = ReportBuilder.Build(request, period, accepted.ToArray(), hasMore, next);
            // Whitespace is disabled to match the SDK's tool-result serialization.
            if (JsonSerializer.SerializeToUtf8Bytes(candidate, WireOptions).Length > MaximumStructuredBytes)
            {
                if (accepted.Count == 1)
                    throw new ArgumentException("An observation exceeds the report byte budget. Retry with period.includeSummaries=false.");
                break;
            }
            result = candidate;
        }
        return result;
    }

    private static QueryRequest Normalize(QueryRequest request)
    {
        if (request.PageSize is < 1 or > 100)
            throw new ArgumentException("period.pageSize must be between 1 and 100.");
        if (request.View is not ("compact" or "flat" or "grouped" or "legacy"))
            throw new ArgumentException("period.view must be compact, flat, grouped, or legacy.");
        if (request.GroupBy is not ("change" or "ticket"))
            throw new ArgumentException("period.groupBy must be change or ticket.");
        return request with
        {
            Repository = Filter(request.Repository, "repository"),
            Change = Filter(request.Change, "change"),
            Ticket = Filter(request.Ticket, "ticket"),
            Status = Filter(request.Status, "status"),
            Classification = Filter(request.Classification, "classification")
        };
    }

    private static string? Filter(string? value, string name)
    {
        if (value is null)
            return null;
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"period.{name} cannot be empty.");
        return value.Trim();
    }
}
