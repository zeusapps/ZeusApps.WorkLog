namespace WorkLog.Journaling;

public sealed record Ticket(string Provider, string Scope, string Id, string? Url = null);
