using System.Text.RegularExpressions;
using WorkLog.Configuration;
using WorkLog.Infrastructure;

namespace WorkLog.Journaling;

internal sealed record PreparedEntry(Entry Entry, string? IdempotencyKey, string RequestHash, string LegacyRequestHash);

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

        var occurred = request.OccurredAt is null ? DateTimeOffset.UtcNow : TimestampParser.Parse(request.OccurredAt, "entry.occurredAt");
        // Defaults such as the clock and configured mappings must not change retry identity.
        var normalized = request with
        {
            Repository = repositoryId,
            Change = change,
            Branch = branch,
            Tickets = request.Tickets is null ? null : tickets,
            OccurredAt = request.OccurredAt is null ? null : occurred.ToString("O"),
            IdempotencyKey = null
        };

        var entry = new Entry(Guid.NewGuid().ToString("N"), repositoryId, branch, change, tickets,
            extractedId, request.Status, classification, request.Summary, occurred,
            DateTimeOffset.UtcNow, request.Agent, request.SessionId);

        // Accept exact retries of observations written before ticket normalization was introduced.
        var legacy = normalized with
        {
            Tickets = request.Tickets
        };
        return new PreparedEntry(entry, Nonblank(request.IdempotencyKey), ContentHash.Compute(Json.Encode(normalized)),
            ContentHash.Compute(Json.Encode(legacy)));
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
        var tickets = new Ticket[supplied?.Length ?? 0];
        for (var index = 0; index < tickets.Length; index++)
        {
            tickets[index] = NormalizeTicket(supplied![index], index);
        }

        if (branch is null || tickets.Length > 0)
        {
            return (tickets, null);
        }

        var context = ResolveTicketContext(repositoryId);
        var pattern = context?.TicketPattern ?? new RepositoryConfig(repositoryId).TicketPattern;
        var match = Regex.Match(branch, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var extractedId = Nonblank(match.Groups["id"].Value);

        if (extractedId is null || context?.Provider is null || context.Scope is null)
        {
            return (tickets, extractedId);
        }

        return ([NormalizeTicket(new Ticket(context.Provider, context.Scope, extractedId), 0)], null);
    }

    private RepositoryConfig? ResolveTicketContext(string repositoryId)
    {
        if (repository?.Identity == repositoryId)
        {
            return repository;
        }

        var matches = configuration.Repositories.Values.Where(candidate => candidate.Identity == repositoryId).Distinct().ToArray();
        if (matches.Length > 1)
        {
            throw new ArgumentException("entry.repository has conflicting ticket configurations. Supply explicit qualified tickets.");
        }
        return matches.SingleOrDefault();
    }

    private static Ticket NormalizeTicket(Ticket? ticket, int index)
    {
        var field = $"entry.tickets[{index}]";
        if (ticket is null)
        {
            throw new ArgumentException(field + " must be an object.");
        }

        var normalized = ticket with
        {
            Provider = Nonblank(ticket.Provider) ?? "",
            Scope = Nonblank(ticket.Scope) ?? "",
            Id = Nonblank(ticket.Id) ?? "",
            Url = Nonblank(ticket.Url)
        };
        if (normalized.Provider is not ("jira" or "azure-devops"))
        {
            throw new ArgumentException(field + ".provider must be jira or azure-devops.");
        }
        if (normalized.Scope.Length == 0 || normalized.Id.Length == 0)
        {
            throw new ArgumentException(field + " requires nonempty scope and id.");
        }
        if (normalized.Provider == "azure-devops" && !Regex.IsMatch(normalized.Id, @"^[0-9]+$"))
        {
            throw new ArgumentException(field + ".id must be numeric for azure-devops.");
        }
        if (normalized.Url is not null
            && (!Uri.TryCreate(normalized.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
        {
            throw new ArgumentException(field + ".url must be an absolute HTTP(S) URL.");
        }
        return normalized;
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
