using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Reporting;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Journaling;

public sealed class AuditRegressionTests
{
    private static readonly QueryRequest Period = new("2026-10-20T00:00:00Z", "2026-10-21T00:00:00Z", "UTC");

    [Fact]
    public void ExactRetryRecognizesPreNormalizationFingerprint()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        var request = new LogRequest("in_progress", "old", Tickets: [new("jira", " company ", " DEP-12 ")],
            OccurredAt: "2026-10-20T09:00:00Z", IdempotencyKey: "old-key");
        var entry = journal.Log(request);
        var oldRequest = request with
        {
            Repository = "repo",
            OccurredAt = DateTimeOffset.Parse(request.OccurredAt!).ToString("O"),
            IdempotencyKey = null
        };
        var oldHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(WorkLog.Infrastructure.Json.Encode(oldRequest))));
        using var connection = new SqliteConnection($"Data Source={Path.Combine(box.Data, "journal.db")};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE retries SET hash=$hash WHERE key='old-key'";
        command.Parameters.AddWithValue("$hash", oldHash);
        Assert.Equal(1, command.ExecuteNonQuery());
        Assert.Equal(entry.Entry.Id, journal.Log(request).Entry.Id);
        Assert.Throws<ArgumentException>(() => journal.Log(request with { Summary = "different" }));
    }

    [Fact]
    public void EffectiveRepositoryControlsInferredScope()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        settings.Configuration.Repositories["other-path"] = new("other", "jira", "other-company");
        var journal = new Journal(settings, new("bound", "jira", "bound-company"));
        var request = new LogRequest("in_progress", "a", "feature/DEP-12-test", Repository: "other");
        Assert.Equal("other-company", Assert.Single(journal.Log(request).Entry.Tickets).Scope);
        var unknown = journal.Log(request with
        {
            Repository = "unknown"
        }).Entry;
        Assert.Empty(unknown.Tickets);
        Assert.Equal("DEP-12", unknown.ExtractedTicketId);
        settings.Configuration.Repositories["conflicting-path"] = new("other", "jira", "conflicting-company");
        Assert.Throws<ArgumentException>(() => journal.Log(request));
    }

    [Fact]
    public void NormalizedTicketSetsGroupStablyAndRemainFilterable()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        var a = new Ticket(" jira ", " company ", " DEP-12 ");
        var b = new Ticket("jira", "company", "DEP-13");
        var request = new LogRequest("in_progress", Tickets: [a, b], OccurredAt: "2026-10-20T09:00:00Z", IdempotencyKey: "trim");
        var first = journal.Log(request);
        Assert.Equal("DEP-12", first.Entry.Tickets[0].Id);
        Assert.Equal(first.Entry.Id, journal.Log(request with
        {
            Tickets = [a with { Provider = "jira", Scope = "company", Id = "DEP-12" }, b]
        }).Entry.Id);
        journal.Log(request with
        {
            Tickets = [b, a],
            IdempotencyKey = "reverse"
        });
        Assert.Single(journal.Query(Period).Days);
        Assert.Equal(2, journal.Query(Period with
        {
            Ticket = "DEP-12"
        }).Entries.Length);
        Assert.Equal(2, journal.Query(Period with
        {
            Ticket = "jira:company:DEP-12"
        }).Entries.Length);
        Assert.Contains("entry.tickets[0]", Assert.Throws<ArgumentException>(() => journal.Log(request with { Tickets = [null!] })).Message);
        Assert.Contains("numeric", Assert.Throws<ArgumentException>(() => journal.Log(request with { Tickets = [new("azure-devops", "org/project", "abc")] })).Message);
        Assert.Contains("period.from", Assert.Throws<ArgumentException>(() => journal.Query(Period with { From = null! })).Message);
        Assert.Contains("period.timezone", Assert.Throws<ArgumentException>(() => journal.Query(Period with { Timezone = null! })).Message);
    }
}
