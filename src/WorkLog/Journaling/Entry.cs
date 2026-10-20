namespace WorkLog.Journaling;

public sealed record Entry(
    string Id,
    string Repository,
    string? Branch,
    string? Change,
    Ticket[] Tickets,
    string? ExtractedTicketId,
    string Status,
    string Classification,
    string? Summary,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    string? Agent,
    string? SessionId);
