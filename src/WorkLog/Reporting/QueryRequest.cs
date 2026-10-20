using System.ComponentModel;

namespace WorkLog.Reporting;

public sealed record QueryRequest(
    [property: Description("Inclusive lower bound: ISO 8601 timestamp with Z or an explicit offset.")] string From,
    [property: Description("Exclusive upper bound: ISO 8601 timestamp with Z or an explicit offset.")] string To,
    [property: Description("Timezone used for daily grouping, for example Europe/Warsaw.")] string Timezone,
    string? Repository = null,
    string? Change = null,
    string? Ticket = null,
    string? Status = null,
    string? Classification = null);
