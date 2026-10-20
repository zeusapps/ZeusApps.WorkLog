using System.Text.Json.Nodes;
using WorkLog.Configuration;
using WorkLog.Journaling;

namespace WorkLog.Smoke;

// Explicitly opt-in: these checks send synthetic prompts through the user's authenticated accounts.
internal sealed class LiveAgentChecks(SmokeWorkspace workspace, SmokeProcessRunner processes)
{
    public async Task RunAsync()
    {
        var configuration = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace.Home, ".claude.json")))!;
        var servers = configuration["projects"]![workspace.Repository.Replace('\\', '/')]!["mcpServers"]!.DeepClone();
        var mcpPath = Path.Combine(workspace.Root, "mcp.json");
        File.WriteAllText(mcpPath, new JsonObject { ["mcpServers"] = servers }.ToJsonString());

        var codex = await processes.RunAsync("codex",
            ["--no-daemon", "-c", workspace.TrustOverride, "exec", "--ignore-user-config", "--ephemeral", "-s", "read-only", Prompt("codex")]);
        RequireSuccess(codex, "Codex");
        Console.WriteLine("Live Codex logged and queried the shared journal.");

        var claude = await processes.RunAsync("claude",
            ["-p", "--no-session-persistence", "--setting-sources", "local", "--strict-mcp-config", "--mcp-config", mcpPath,
             "--allowedTools", "mcp__worklog__log_work", "mcp__worklog__query_work", "--tools", "", "--max-budget-usd", "1", Prompt("claude")]);
        RequireSuccess(claude, "Claude");

        var report = new Journal(new LocalSettings(workspace.Data)).Query(JournalAcceptanceChecks.Period);
        if (!report.Entries.Any(entry => entry.Change == "live-codex" && entry.Agent == "codex")
            || !report.Entries.Any(entry => entry.Change == "live-claude" && entry.Agent == "claude"))
        {
            throw new InvalidOperationException("Live entries missing.");
        }

        Console.WriteLine("Live Claude Code logged and queried the shared journal.");
    }

    private static void RequireSuccess(string output, string client)
    {
        if (!output.Contains("SMOKE_OK"))
        {
            throw new InvalidOperationException("Live " + client + " did not confirm tool calls: " + output);
        }
    }

    private static string Prompt(string agent) => $"""
        This is an isolated WorkLog integration smoke test. Use only worklog MCP tools; do not run shell commands or edit files.
        Call log_work exactly once with entry status=in_progress, change=live-{agent}, branch=feature/DEP-999-smoke,
        agent={agent}, occurredAt=2026-10-20T12:00:00Z, idempotencyKey=live-{agent}-one.
        Then call query_work with period from=2026-10-20T00:00:00Z, to=2026-10-24T00:00:00Z, timezone=Europe/Warsaw.
        Verify your entry and the preexisting feature Y/Z observations are returned. Reply SMOKE_OK if both calls succeed, otherwise state the error.
        """;
}
