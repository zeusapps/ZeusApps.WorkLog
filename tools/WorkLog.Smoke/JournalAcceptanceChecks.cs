using Microsoft.Data.Sqlite;
using ModelContextProtocol.Client;
using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Reporting;

namespace WorkLog.Smoke;

internal sealed class JournalAcceptanceChecks(SmokeWorkspace workspace)
{
    internal static QueryRequest Period => new("2026-10-20T00:00:00Z", "2026-10-24T00:00:00Z", "Europe/Warsaw");

    public async Task RunAsync()
    {
        await ExerciseToolsAsync();
        VerifyFeatureSwitching();
        VerifyUpgrade();
    }

    private async Task ExerciseToolsAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var transport = new StdioClientTransport(new()
        {
            Name = "release-smoke",
            Command = workspace.Executable,
            Arguments = ["serve", "--repository", workspace.Repository, "--data-dir", workspace.Data]
        });
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        if (tools.Count != 2 || tools.Any(tool => tool.ProtocolTool.OutputSchema is null))
        {
            throw new InvalidOperationException("Expected two MCP tools with output schemas.");
        }

        (string Status, string Change, string OccurredAt)[] activities =
        [
            ("started", "Y", "2026-10-20T09:00:00+02:00"),
            ("in_progress", "Y", "2026-10-21T09:00:00+02:00"),
            ("started", "Z", "2026-10-21T12:00:00+02:00"),
            ("done", "Z", "2026-10-22T09:00:00+02:00"),
            ("in_progress", "Y", "2026-10-23T09:00:00+02:00")
        ];

        foreach (var activity in activities)
        {
            var result = await client.CallToolAsync("log_work", new Dictionary<string, object?>
            {
                ["entry"] = new
                {
                    status = activity.Status,
                    change = activity.Change,
                    occurredAt = activity.OccurredAt,
                    branch = "hotfix/DEP-123-test",
                    idempotencyKey = string.Join('-', activity)
                }
            }, cancellationToken: timeout.Token);
            if (result.IsError == true || result.StructuredContent is null)
            {
                throw new InvalidOperationException("Release log_work failed to return structured content.");
            }
        }

        var query = await client.CallToolAsync("query_work", new Dictionary<string, object?>
        {
            ["period"] = new { from = Period.From, to = Period.To, timezone = Period.Timezone }
        }, cancellationToken: timeout.Token);
        if (query.IsError == true || query.StructuredContent is null)
        {
            throw new InvalidOperationException("Release query_work failed to return structured content.");
        }
    }

    private void VerifyFeatureSwitching()
    {
        var report = new Journal(new LocalSettings(workspace.Data)).Query(Period);
        if (report.Entries.Length != 5 || report.Days.Any(day => day.Date == "2026-10-22" && day.WorkIdentity.EndsWith("/Y")))
        {
            throw new InvalidOperationException("Feature-switch acceptance failed.");
        }

        Console.WriteLine("Self-contained executable passed stdio logging/query and feature-switch acceptance.");
    }

    private void VerifyUpgrade()
    {
        using (var connection = new SqliteConnection($"Data Source={Path.Combine(workspace.Data, "journal.db")};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP INDEX entries_period; DROP INDEX first_start; PRAGMA user_version=1;";
            command.ExecuteNonQuery();
        }

        var journal = new Journal(new LocalSettings(workspace.Data));
        if (journal.Query(Period).Entries.Length != 5 || !Directory.GetFiles(workspace.Data, "*.bak").Any())
        {
            throw new InvalidOperationException("Acceptance journal upgrade failed.");
        }

        Console.WriteLine("Acceptance observations survived a schema upgrade with a recoverable backup.");
    }
}
