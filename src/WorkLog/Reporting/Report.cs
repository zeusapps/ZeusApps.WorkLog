using System.ComponentModel;
using WorkLog.Journaling;

namespace WorkLog.Reporting;

[Description("One bounded page of recorded work for an explicit reporting period. Follow nextCursor until hasMore is false for a complete report.")]
public sealed record Report(
    [property: Description("Version of the report payload format. Currently 1, including pagination and selectable views; separate from application/database versions.")] int SchemaVersion,
    [property: Description("Inclusive lower bound of the reporting period, normalized to UTC. Compared against occurredAt.")] DateTimeOffset From,
    [property: Description("Exclusive upper bound of the reporting period, normalized to UTC. Compared against occurredAt.")] DateTimeOffset To,
    [property: Description("Resolved timezone used to assign observations to calendar dates, for example Europe/Warsaw.")] string Timezone,
    [property: Description("This page's observations ordered by occurredAt and ID; empty in grouped view. In legacy view do not count both views.")] Entry[] Entries,
    [property: Description("This page's observations grouped by local date and work identity; empty in flat view. Compact groups reference entry IDs. Groups can span pages.")] DailyGroup[] Days,
    [property: Description("Selected compact, flat, grouped, or legacy representation.")] string View,
    [property: Description("Requested maximum observations per page. The byte budget can return fewer.")] int PageSize,
    [property: Description("True when additional matching observations remain in this query snapshot.")] bool HasMore,
    [property: Description("Opaque continuation token. Supply as period.cursor with unchanged options; null when no observations remain.")] string? NextCursor);
