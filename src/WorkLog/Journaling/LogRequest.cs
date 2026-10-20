using System.ComponentModel;

namespace WorkLog.Journaling;

public sealed record LogRequest(
    [property: Description("started, in_progress, or done. Status reflects actual session activity, independently of OpenSpec archive timing.")] string Status,
    string? Change = null,
    string? Branch = null,
    Ticket[]? Tickets = null,
    string? Summary = null,
    [property: Description("Optional override of the classification inferred from branch-prefix mappings.")] string? Classification = null,
    [property: Description("ISO 8601 timestamp with Z or an explicit offset. Defaults to the current instant.")] string? OccurredAt = null,
    [property: Description("Optional unique observation key. Reuse only when retrying identical input.")] string? IdempotencyKey = null,
    string? Agent = null,
    string? SessionId = null,
    string? Repository = null);
