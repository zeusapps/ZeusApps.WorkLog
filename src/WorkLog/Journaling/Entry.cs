using System.ComponentModel;

namespace WorkLog.Journaling;

[Description("A saved observation of actual work, with repository and branch metadata captured at logging time.")]
public sealed record Entry(
    [property: Description("Unique identifier of the saved observation. Replayed or deduplicated entries retain their original ID.")] string Id,
    [property: Description("Stable repository identity, not the machine-specific checkout path.")] string Repository,
    [property: Description("Git branch supplied when the work was logged. Omitted when unavailable.")] string? Branch,
    [property: Description("OpenSpec change name associated with this observation. Omitted for ticket-only work.")] string? Change,
    [property: Description("Qualified ticket references attached to this observation. Empty for work without tickets; no issue-service lookup is performed.")] Ticket[] Tickets,
    [property: Description("Ticket ID extracted from the branch but not qualified because provider or scope was unknown. Null when absent or resolved.")] string? ExtractedTicketId,
    [property: Description("Reported work status: started, in_progress, or done. Describes this observation, independently of archive timing.")] string Status,
    [property: Description("Classification resolved and stored when the entry was logged. Later branch-mapping changes do not reclassify it.")] string Classification,
    [property: Description("Optional factual summary of the work performed.")] string? Summary,
    [property: Description("When the work happened. Used for period filtering and daily grouping; can be supplied retrospectively.")] DateTimeOffset OccurredAt,
    [property: Description("When the server saved the observation. Assigned by WorkLog; not used to select the reporting period.")] DateTimeOffset RecordedAt,
    [property: Description("Optional name of the agent that logged the work, such as codex or claude.")] string? Agent,
    [property: Description("Optional caller-supplied session identifier linking observations from the same session.")] string? SessionId);
