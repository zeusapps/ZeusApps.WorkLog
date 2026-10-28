using System.ComponentModel;

namespace WorkLog.Journaling;

public sealed record LogRequest(
    [property: Description("started, in_progress, or done. Status reflects actual session activity, independently of OpenSpec archive timing.")] string Status,
    string? Change = null,
    string? Branch = null,
    Ticket[]? Tickets = null,
    [property: Description("Optional one short sentence (about 140 characters) stating the concrete outcome, decision, or remaining step: for in_progress what was achieved and what remains, for done what was completed. Do not narrate process, repeat the status, or include secrets or personal data.")] string? Summary = null,
    [property: Description("Optional override of the classification inferred from branch-prefix mappings.")] string? Classification = null,
    [property: Description("ISO 8601 timestamp with Z or an explicit offset. Defaults to the current instant.")] string? OccurredAt = null,
    [property: Description("Optional unique observation key. Reuse only when retrying identical input.")] string? IdempotencyKey = null,
    string? Agent = null,
    string? SessionId = null,
    string? Repository = null);
