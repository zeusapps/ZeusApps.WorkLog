using System.Text.RegularExpressions;
using WorkLog.Configuration;
using WorkLog.Infrastructure;

namespace WorkLog.Journaling;

internal sealed record PreparedEntry(Entry Entry, string? IdempotencyKey, string RequestHash);

internal sealed class EntryFactory(WorkLogConfiguration configuration, RepositoryConfig? repository)
{
    public PreparedEntry Prepare(LogRequest request)
    {
        ValidateStatus(request.Status);
        var repositoryId = Nonblank(request.Repository) ?? repository?.Identity
            ?? throw new ArgumentException("Repository is required; initialize a repository or supply its identity.");
        var change = Nonblank(request.Change);
        var branch = Nonblank(request.Branch);
        var classification = ResolveClassification(request.Classification, branch);
        var (tickets, extractedId) = ResolveTickets(request.Tickets, branch, repositoryId);

        if (change is null && tickets.Length == 0)
        {
            throw new ArgumentException("An OpenSpec change or qualified ticket reference is required.");
        }

        var occurred = request.OccurredAt is null ? DateTimeOffset.UtcNow : TimestampParser.Parse(request.OccurredAt);
        // Defaults such as the clock and configured mappings must not change retry identity.
        var normalized = request with
        {
            Repository = repositoryId,
            Change = change,
            Branch = branch,
            OccurredAt = request.OccurredAt is null ? null : occurred.ToString("O"),
            IdempotencyKey = null
        };

        var entry = new Entry(Guid.NewGuid().ToString("N"), repositoryId, branch, change, tickets,
            extractedId, request.Status, classification, request.Summary, occurred,
            DateTimeOffset.UtcNow, request.Agent, request.SessionId);

        return new PreparedEntry(entry, Nonblank(request.IdempotencyKey), ContentHash.Compute(Json.Encode(normalized)));
    }

    private string ResolveClassification(string? explicitClassification, string? branch)
    {
        var prefix = branch?.Split('/')[0];
        var mapped = configuration.BranchClassifications
            .FirstOrDefault(pair => pair.Key.Equals(prefix, StringComparison.OrdinalIgnoreCase)).Value;

        return Nonblank(explicitClassification) ?? mapped ?? "unclassified";
    }

    private (Ticket[] Tickets, string? ExtractedId) ResolveTickets(Ticket[]? supplied, string? branch, string repositoryId)
    {
        var tickets = supplied ?? [];
        foreach (var ticket in tickets)
        {
            ValidateTicket(ticket);
        }

        if (branch is null || tickets.Length > 0)
        {
            return (tickets, null);
        }

        var pattern = repository?.TicketPattern ?? new RepositoryConfig(repositoryId).TicketPattern;
        var match = Regex.Match(branch, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var extractedId = Nonblank(match.Groups["id"].Value);

        if (extractedId is null || repository?.Provider is null || repository.Scope is null)
        {
            return (tickets, extractedId);
        }

        if (repository.Provider is not ("jira" or "azure-devops"))
        {
            throw new ArgumentException("Configured ticket provider is invalid.");
        }

        return ([new Ticket(repository.Provider, repository.Scope, extractedId)], null);
    }

    private static void ValidateTicket(Ticket ticket)
    {
        if (ticket.Provider is not ("jira" or "azure-devops") || Nonblank(ticket.Scope) is null || Nonblank(ticket.Id) is null)
        {
            throw new ArgumentException("Tickets require provider jira or azure-devops, scope, and ID.");
        }

        if (ticket.Url is not null
            && (!Uri.TryCreate(ticket.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
        {
            throw new ArgumentException("Ticket URL must be an absolute HTTP(S) URL.");
        }
    }

    private static void ValidateStatus(string status)
    {
        if (status is not ("started" or "in_progress" or "done"))
        {
            throw new ArgumentException("Status must be started, in_progress, or done.");
        }
    }

    private static string? Nonblank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
