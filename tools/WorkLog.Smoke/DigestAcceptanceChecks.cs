using System.Text.Json;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using WorkLog.Configuration;

namespace WorkLog.Smoke;

/// <summary>
/// Stdio MCP acceptance for day digests against the packaged executable, using a disposable data directory.
/// The tool contract is client-neutral, so this covers Claude Code and Codex alike; managed instruction text is verified in the unit tests.
/// </summary>
internal sealed class DigestAcceptanceChecks(SmokeWorkspace workspace)
{
    public async Task RunAsync()
    {
        var data = Path.Combine(workspace.Root, "digests", "data");
        var settings = new LocalSettings(data);
        settings.Configuration.Updates.Enabled = false;
        settings.Save();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(new()
        {
            Name = "digest-smoke",
            Command = workspace.Executable,
            Arguments = ["serve", "--repository", workspace.Repository, "--data-dir", data]
        }), cancellationToken: timeout.Token);

        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        var save = tools.SingleOrDefault(tool => tool.Name == "save_digest")?.ProtocolTool
            ?? throw new InvalidOperationException("save_digest is not exposed by the packaged server.");
        Require(save.Annotations is { ReadOnlyHint: false, DestructiveHint: false, IdempotentHint: true, OpenWorldHint: false },
            "save_digest annotations are wrong.");

        async Task<CallToolResult> Log(string at, string summary) => await client.CallToolAsync("log_work", new Dictionary<string, object?>
        {
            ["entry"] = new
            {
                status = "in_progress",
                tickets = new[] { new { provider = "jira", scope = "company", id = "DEP-7" } },
                occurredAt = at,
                summary
            }
        }, cancellationToken: timeout.Token);

        // Times within minutes of each other stay on one local day in every timezone.
        var first = await Log("2026-10-20T10:07:00Z", "First step.");
        Require(first.IsError != true && !first.StructuredContent!.Value.TryGetProperty("digestRequest", out _), "The first observation must not request a digest.");
        var second = await Log("2026-10-20T10:11:00Z", "Second step.");
        Require(second.IsError != true, "The second observation could not be logged.");
        Require(second.StructuredContent!.Value.TryGetProperty("digestRequest", out var request), "The second observation must request a digest.");
        var covered = request.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("id").GetString()!).ToArray();
        Require(covered.Length == 2 && covered[0] == first.StructuredContent!.Value.GetProperty("entry").GetProperty("id").GetString(), "The digest request must list both observations, oldest first.");

        var saved = await client.CallToolAsync("save_digest", new Dictionary<string, object?>
        {
            ["entryIds"] = covered,
            ["text"] = "Completed both steps."
        }, cancellationToken: timeout.Token);
        Require(saved.IsError != true && saved.StructuredContent!.Value.GetProperty("text").GetString() == "Completed both steps.", "save_digest did not store the digest.");

        var third = await Log("2026-10-20T10:13:00Z", "Third step.");
        Require(third.StructuredContent!.Value.GetProperty("digestRequest").GetProperty("entries").GetArrayLength() == 3, "The third observation must request a digest over all three.");
        var stale = await client.CallToolAsync("save_digest", new Dictionary<string, object?>
        {
            ["entryIds"] = covered,
            ["text"] = "Repeated after a later observation."
        }, cancellationToken: timeout.Token);
        Require(stale.IsError == true && stale.Content.OfType<TextContentBlock>().Any(block => block.Text.Contains("Stale", StringComparison.Ordinal)),
            "A repeated save after a later observation must be rejected as stale.");

        var journalVersion = UserVersion(Path.Combine(data, "journal.db"));
        var digestVersion = UserVersion(Path.Combine(data, "digests.db"));
        Require(journalVersion == 2, $"journal.db user_version must stay 2 but is {journalVersion}.");
        Require(digestVersion == 1, $"digests.db user_version must be 1 but is {digestVersion}.");
        File.WriteAllText(Path.Combine(workspace.Root, "digests", "evidence.json"), JsonSerializer.Serialize(new
        {
            tools = tools.Select(tool => tool.Name).Order().ToArray(),
            requestOnSecondObservation = true,
            savedDigest = true,
            staleAfterThirdObservation = true,
            journalUserVersion = journalVersion,
            digestsUserVersion = digestVersion
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Packaged digests: request on the second observation, save, stale rejection after a third, journal user_version 2, digests user_version 1.");
    }

    private static int UserVersion(string database)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
