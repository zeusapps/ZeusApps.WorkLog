using System.ComponentModel;

namespace WorkLog.Reporting;

public sealed record QueryRequest(
    [property: Description("Inclusive lower bound: ISO 8601 timestamp with Z or an explicit offset.")] string From,
    [property: Description("Exclusive upper bound: ISO 8601 timestamp with Z or an explicit offset.")] string To,
    [property: Description("Timezone used for daily grouping, for example Europe/Warsaw.")] string Timezone,
    [property: Description("Optional exact repository identity filter. Omit to report across this local journal.")] string? Repository = null,
    [property: Description("Optional exact OpenSpec change name filter.")] string? Change = null,
    [property: Description("Optional ticket filter. A bare ID also matches unresolved extractedTicketId; provider:scope:id matches only a stored qualified reference, for example jira:company.atlassian.net:DEP-12. Surrounding whitespace is trimmed.")] string? Ticket = null,
    [property: Description("Optional status filter: started, in_progress, or done.")] string? Status = null,
    [property: Description("Optional exact stored classification filter.")] string? Classification = null,
    [property: Description("Maximum observations per page, from 1 to 100. Defaults to 25; a byte budget can return fewer.")] int PageSize = 25,
    [property: Description("Opaque nextCursor from a previous page. Keep all query options unchanged while continuing.")] string? Cursor = null,
    [property: Description("compact (default): entries once, daily groups reference IDs; flat: entries only; grouped: observations only in daily groups; legacy: both full views.")] string View = "compact",
    [property: Description("Include stored summaries. Set false to reduce output or retrieve entries whose summaries exceed the page byte budget.")] bool IncludeSummaries = true);
