using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using Tomlyn;
using WorkLog.Configuration;
using WorkLog.Repositories;

namespace WorkLog.Smoke;

internal sealed class InitializationAcceptanceChecks(SmokeWorkspace workspace, SmokeProcessRunner processes)
{
    public async Task RunAsync()
    {
        var root = Path.Combine(workspace.Root, "initialization");
        var repo = Path.Combine(root, "repo");
        var worktree = Path.Combine(root, "worktree");
        var home = Path.Combine(root, "home");
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(home);
        GitRepository.Run(repo, "init");
        GitRepository.Run(repo, "-c", "user.name=Smoke", "-c", "user.email=smoke@example.invalid", "commit", "--allow-empty", "-m", "initial");
        GitRepository.Run(repo, "worktree", "add", "-b", "smoke-context", worktree);
        var settings = new LocalSettings(data);
        settings.Configuration.Updates.Enabled = false;
        settings.Save();
        Directory.CreateDirectory(Path.Combine(repo, ".codex"));
        File.WriteAllText(Path.Combine(repo, "CLAUDE.local.md"), "Application rules\n");
        File.WriteAllText(Path.Combine(repo, ".codex", "config.toml"), "developer_instructions='Application rules'\n");
        var environment = new Dictionary<string, string>
        {
            ["CLAUDE_CONFIG_DIR"] = home, ["CODEX_HOME"] = Path.Combine(home, ".codex")
        };
        async Task InitAsync(string checkout, params string[] options) =>
            _ = await processes.RunAsync(workspace.Executable,
                ["init", "--repository", checkout, "--data-dir", data, .. options], environment);

        await InitAsync(repo);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var historicalId = "";
        await using (var client = await ConnectAsync(repo, data, timeout.Token))
        {
            var result = await client.CallToolAsync("log_work", new Dictionary<string, object?>
            {
                ["entry"] = new { status = "in_progress", change = "unresolved", branch = "bugfix/DEP-7483-repair", occurredAt = "2026-10-20T09:00:00Z" }
            }, cancellationToken: timeout.Token);
            if (result.IsError == true || result.StructuredContent is null) throw new InvalidOperationException("Packaged unresolved log failed.");
            var entry = result.StructuredContent.Value.GetProperty("entry");
            historicalId = entry.GetProperty("id").GetString()!;
            if (entry.GetProperty("classification").GetString() != "bug" || entry.GetProperty("tickets").GetArrayLength() != 0
                || entry.GetProperty("extractedTicketId").GetString() != "DEP-7483")
                throw new InvalidOperationException("Packaged branch classification/unresolved metadata check failed.");
        }

        await InitAsync(repo, "--no-instructions", "--ticket-provider", "jira", "--ticket-scope", "company.atlassian.net");
        var claudeBefore = File.ReadAllText(Path.Combine(repo, "CLAUDE.local.md"));
        var codexBefore = File.ReadAllText(Path.Combine(repo, ".codex", "config.toml"));
        await InitAsync(repo);
        await InitAsync(worktree);
        if (claudeBefore.Contains("worklog:begin") || codexBefore.Contains("worklog:begin")
            || !claudeBefore.Contains("Application rules") || !codexBefore.Contains("Application rules")
            || claudeBefore != File.ReadAllText(Path.Combine(repo, "CLAUDE.local.md"))
            || codexBefore != File.ReadAllText(Path.Combine(repo, ".codex", "config.toml"))
            || File.Exists(Path.Combine(worktree, "CLAUDE.local.md"))
            || Toml.ToModel(File.ReadAllText(Path.Combine(worktree, ".codex", "config.toml"))).ContainsKey("developer_instructions"))
            throw new InvalidOperationException("Packaged persistent instruction opt-out failed.");

        var restarted = new LocalSettings(data);
        var mainContext = new RepositoryRegistry(restarted).Get(repo);
        var worktreeContext = new RepositoryRegistry(restarted).Get(worktree);
        if (mainContext != worktreeContext || mainContext.Scope != "company.atlassian.net")
            throw new InvalidOperationException("Packaged shared worktree context failed.");

        await using (var client = await ConnectAsync(worktree, data, timeout.Token))
        {
            var logged = await client.CallToolAsync("log_work", new Dictionary<string, object?>
            {
                ["entry"] = new { status = "in_progress", branch = "bugfix/DEP-7483-repair", occurredAt = "2026-10-20T10:00:00Z" }
            }, cancellationToken: timeout.Token);
            if (logged.IsError == true || logged.StructuredContent is null
                || logged.StructuredContent.Value.GetProperty("entry").GetProperty("tickets").GetArrayLength() != 1)
                throw new InvalidOperationException("Packaged ticket-only branch inference failed.");
            foreach (var ticket in new[] { "DEP-7483", "jira:company.atlassian.net:DEP-7483" })
            {
                var report = await client.CallToolAsync("query_work", new Dictionary<string, object?>
                {
                    ["period"] = new { from = JournalAcceptanceChecks.Period.From, to = JournalAcceptanceChecks.Period.To, timezone = "UTC", ticket }
                }, cancellationToken: timeout.Token);
                if (report.IsError == true || report.StructuredContent is null) throw new InvalidOperationException("Packaged ticket query failed.");
                var entries = report.StructuredContent.Value.GetProperty("entries").EnumerateArray().ToArray();
                if (entries.Length != (ticket == "DEP-7483" ? 2 : 1)) throw new InvalidOperationException("Packaged bare/qualified ticket filtering failed.");
                if (ticket == "DEP-7483" && entries.Single(entry => entry.GetProperty("id").GetString() == historicalId).GetProperty("tickets").GetArrayLength() != 0)
                    throw new InvalidOperationException("Historical unresolved observation was requalified.");
            }
        }

        // Read the actual owned registration, rather than reconstructing its server arguments.
        var registration = JsonNode.Parse(File.ReadAllText(Path.Combine(home, ".claude.json")))!
            ["projects"]![worktree.Replace('\\', '/')]!["mcpServers"]!["worklog"]!;
        if (!registration["args"]!.AsArray().Any(argument => argument!.GetValue<string>() == worktree))
            throw new InvalidOperationException("Packaged worktree registration lost its checkout context.");
        await InitAsync(repo, "--clients", "claude", "--instructions");
        if (!File.ReadAllText(Path.Combine(repo, "CLAUDE.local.md")).Contains("updateHint")
            || File.ReadAllText(Path.Combine(repo, ".codex", "config.toml")).Contains("worklog:begin")
            || File.Exists(Path.Combine(worktree, "CLAUDE.local.md")))
            throw new InvalidOperationException("Packaged per-client re-enable or checkout-local cleanup failed.");

        // Also verify native client discovery/health with instructions removed in the original fixture.
        var originalEnvironment = new Dictionary<string, string>
        {
            ["CLAUDE_CONFIG_DIR"] = workspace.Home, ["CODEX_HOME"] = workspace.CodexHome
        };
        await processes.RunAsync(workspace.Executable,
            ["init", "--repository", workspace.Repository, "--data-dir", workspace.Data, "--no-instructions"], originalEnvironment);
        try { await new ClientRegistrationChecks(workspace, processes).RunAsync(instructionsEnabled: false); }
        finally
        {
            await processes.RunAsync(workspace.Executable,
                ["init", "--repository", workspace.Repository, "--data-dir", workspace.Data, "--instructions"], originalEnvironment);
        }
        Console.WriteLine("Packaged init passed ticket setup/filtering, shared context, bugfix classification, and persistent instruction opt-out/re-enable checks.");
    }

    private Task<McpClient> ConnectAsync(string repo, string data, CancellationToken token) =>
        McpClient.CreateAsync(new StdioClientTransport(new()
        {
            Name = "initialization-smoke", Command = workspace.Executable,
            Arguments = ["serve", "--repository", repo, "--data-dir", data]
        }), cancellationToken: token);
}
