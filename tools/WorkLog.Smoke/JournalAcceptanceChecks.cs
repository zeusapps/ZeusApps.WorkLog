using Microsoft.Data.Sqlite;
using System.Text.Json;
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
        await VerifyTicketGroupingAsync();
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

    private async Task VerifyTicketGroupingAsync()
    {
        // Keep synthetic grouping records out of the existing feature/upgrade acceptance journal.
        var data = Path.Combine(workspace.Root, "ticket-grouping", "data");
        var settings = new LocalSettings(data);
        settings.Configuration.Updates.Enabled = false;
        settings.Save();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(new()
        {
            Name = "ticket-grouping-smoke",
            Command = workspace.Executable,
            Arguments = ["serve", "--repository", workspace.Repository, "--data-dir", data]
        }), cancellationToken: timeout.Token);
        var original = new Dictionary<string, JsonElement>();
        foreach (var change in new string?[] { null, "repair-links", "followup" })
        {
            var logged = await client.CallToolAsync("log_work", new Dictionary<string, object?>
            {
                ["entry"] = new
                {
                    status = "in_progress",
                    change,
                    tickets = new[] { new { provider = "jira", scope = "company", id = "DEP-12" } },
                    occurredAt = "2026-10-20T09:00:00Z",
                    summary = "Historical/new ticket-grouping acceptance."
                }
            }, cancellationToken: timeout.Token);
            if (logged.IsError == true || logged.StructuredContent is null)
                throw new InvalidOperationException("Packaged grouping fixture could not log an observation.");
            var entry = logged.StructuredContent.Value.GetProperty("entry");
            original.Add(entry.GetProperty("id").GetString()!, entry.Clone());
        }

        async Task VerifyAsync(string? groupBy)
        {
            var ids = new List<string>();
            var identities = new HashSet<string>();
            string? cursor = null;
            do
            {
                var period = new Dictionary<string, object?>
                {
                    ["from"] = Period.From,
                    ["to"] = Period.To,
                    ["timezone"] = Period.Timezone,
                    ["pageSize"] = 1,
                    ["cursor"] = cursor
                };
                if (groupBy is not null)
                    period["groupBy"] = groupBy;
                var result = await client.CallToolAsync("query_work", new Dictionary<string, object?> { ["period"] = period },
                    cancellationToken: timeout.Token);
                if (result.IsError == true || result.StructuredContent is null)
                    throw new InvalidOperationException("Packaged ticket-grouping query failed.");
                var report = result.StructuredContent.Value;
                foreach (var entry in report.GetProperty("entries").EnumerateArray())
                {
                    var id = entry.GetProperty("id").GetString()!;
                    ids.Add(id);
                    if (!original.TryGetValue(id, out var expected) || !JsonElement.DeepEquals(expected, entry))
                        throw new InvalidOperationException("Ticket grouping altered original observation metadata.");
                }
                foreach (var day in report.GetProperty("days").EnumerateArray())
                {
                    identities.Add(day.GetProperty("workIdentity").GetString()!);
                    if (day.GetProperty("date").GetString() != "2026-10-20"
                        || !day.GetProperty("entryIds").EnumerateArray().Select(item => item.GetString()).SequenceEqual(ids.TakeLast(1)))
                        throw new InvalidOperationException("Packaged grouping returned an incorrect page fragment.");
                }
                cursor = report.GetProperty("nextCursor").GetString();
                if (report.GetProperty("hasMore").GetBoolean() != (cursor is not null) || ids.Count > 3)
                    throw new InvalidOperationException("Packaged grouping continuation failed.");
            } while (cursor is not null);
            if (ids.Count != 3 || ids.Distinct().Count() != 3 || identities.Count != (groupBy == "change" ? 3 : 1))
                throw new InvalidOperationException("Packaged grouping did not preserve exactly three observations in the expected groups.");
        }

        await VerifyAsync(null);
        await VerifyAsync("change");
        await VerifyAsync("ticket");
        await VerifyAsync(null);
        var snapshot = new Journal(new LocalSettings(data)).Query(Period);
        if (snapshot.Entries.Length != 3)
            throw new InvalidOperationException("Packaged grouping queries changed the journal.");
        File.WriteAllText(Path.Combine(workspace.Root, "ticket-grouping", "evidence.json"),
            JsonSerializer.Serialize(new
            {
                modes = new[] { "omitted", "change", "ticket" },
                pageSize = 1,
                observationCount = 3,
                preservedMetadata = true,
                report = snapshot
            }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Packaged ticket grouping combined historical/new observations, preserved metadata and traversed all pages.");
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
