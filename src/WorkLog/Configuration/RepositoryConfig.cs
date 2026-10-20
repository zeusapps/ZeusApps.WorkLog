namespace WorkLog.Configuration;

public sealed record RepositoryConfig(
    string Identity,
    string? Provider = null,
    string? Scope = null,
    string TicketPattern = @"(?<id>[A-Z][A-Z0-9]+-\d+)");
