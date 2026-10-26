using ModelContextProtocol.Client;
using WorkLog.Configuration;
using WorkLog.Infrastructure;
using WorkLog.Journaling;
using WorkLog.Repositories;

namespace WorkLog.Smoke;

internal static class DashboardFixture
{
    public static void Create(string directory)
    {
        var root = Path.GetFullPath(directory);
        if (Directory.Exists(root)) throw new IOException("Dashboard fixture must use a new directory.");
        Directory.CreateDirectory(root);
        var settings = new LocalSettings(Path.Combine(root, "data"));
        settings.Configuration.Updates.Enabled = false;
        var a = Repository(root, "worklog");
        var b = Repository(root, "bell-tower");
        settings.Configuration.RepositoryContexts[GitRepository.GetCommonDirectory(a)] = new(new("example/worklog"));
        settings.Configuration.RepositoryContexts[GitRepository.GetCommonDirectory(b)] = new(new("example/bell-tower"));
        var storeMember = Repository(root, "store-member");
        File.WriteAllText(Path.Combine(storeMember, "openspec", "config.yaml"), "store: dashboard-fixture-missing-" + Guid.NewGuid().ToString("N") + "\n");
        settings.Configuration.RepositoryContexts[GitRepository.GetCommonDirectory(storeMember)] = new(new("example/store-member"));
        Change(storeMember, "must-not-fallback", "# Local shadow\n\nA store declaration must never silently use these local documents.\n");
        var planningStore = Path.Combine(root, "planning-store");
        Directory.CreateDirectory(Path.Combine(planningStore, "openspec"));
        File.WriteAllText(Path.Combine(planningStore, "openspec", "config.yaml"), "schema: spec-driven\n");
        Change(planningStore, "central-planning", "# Central planning\n\nThis store is intentionally unavailable until registered.\n");
        settings.Save();
        Change(a, "activity-timeline", "# Activity timeline\n\n## Why this matters\n\nA timeline connects recorded moments without pretending the gaps were work.\n\n## Design decisions\n\nShow elapsed spans with activity dots. Keep the journal local.\n\n| Measure | Meaning |\n| --- | --- |\n| Elapsed span | Time between recorded observations |\n| Activity date | A date with a saved observation |\n\n## Implementation\n\n```csharp\nvar history = journal.Query(period);\n```\n\n![Disabled remote image](https://example.invalid/remote.png)\n");
        Change(a, "inline-review-comments", "# Inline review comments\n\n## Review after editing\n\nWrite feedback where the agent already reads the design.\n\n<!-- REVIEW: Keep comments beside their source passage. -->\n");
        Change(a, "repository-filters", "# Repository filters\n\nChoose one project, several projects, or all your local work.\n");
        Change(b, "shipping-reports", "# Shipping reports\n\n## Decision\n\nGive reports a clear source and an explicit period.\n");
        Change(a, "archive/2026-10-01-update-rollout", "# Update rollout\n\nCompleted update selection keeps existing sessions alive.\n");
        var worktree = Path.Combine(root, "worklog-worktree");
        GitRepository.Run(a, "worktree", "add", "--detach", worktree, "HEAD");
        File.WriteAllText(Path.Combine(worktree, "openspec", "config.yaml"), "schema: spec-driven\n");
        Change(worktree, "activity-timeline", "# Activity timeline alternate\n\nA separate worktree keeps its own uncommitted design.\n");
        var journal = new Journal(settings, new("example/worklog"));
        var now = DateTimeOffset.UtcNow;
        void Log(string status, string change, int days, string summary, string ticket) => journal.Log(new(status, change, "feature/" + ticket,
            Tickets: [new("jira", "example.local", ticket)], Summary: summary, OccurredAt: now.AddDays(-days).ToString("O"), Agent: "fixture"));
        Log("started", "activity-timeline", 12, "Explored the shape of a visual work history.", "WL-142");
        Log("in_progress", "activity-timeline", 8, "Defined elapsed spans and local activity dates.", "WL-142");
        Log("in_progress", "activity-timeline", 2, "Implemented timeline projection and paging.", "WL-142");
        Log("started", "inline-review-comments", 5, "Planned contextual review comments.", "WL-156");
        Log("done", "inline-review-comments", 3, "Added inline review markers and source lines.", "WL-156");
        Log("in_progress", "inline-review-comments", 1, "Revisited stale block handling.", "WL-156");
        Log("started", "repository-filters", 4, "Created shared repository selection.", "WL-161");
        Log("done", "update-rollout", 15, "Verified compatible payload activation.", "WL-120");
        for (var i = 0; i < 125; i++) Log("in_progress", "large-history", 2, "Synthetic continuation observation " + i, "WL-190");
        for (var i = 0; i < 55; i++) Log(i % 3 == 0 ? "done" : "in_progress", "recorded-work-" + i, 3 + i % 20, "Synthetic timeline fixture " + i, "WL-" + (200 + i));
        new Journal(settings, new("example/bell-tower")).Log(new("in_progress", "shipping-reports", Summary: "Prepared report evidence.", OccurredAt: now.AddDays(-1).ToString("O"), Agent: "fixture"));
        AtomicFile.Write(Path.Combine(root, "fixture.json"), Json.Encode(new { root, data = settings.DataDirectory, repo = a, otherRepo = b, worktree }));
        Console.WriteLine(root);
    }

    private static string Repository(string root, string name)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(Path.Combine(directory, "openspec"));
        File.WriteAllText(Path.Combine(directory, "openspec", "config.yaml"), "schema: spec-driven\n");
        GitRepository.Run(directory, "init", "--initial-branch=main");
        GitRepository.Run(directory, "add", "openspec/config.yaml");
        GitRepository.Run(directory, "-c", "user.name=Dashboard fixture", "-c", "user.email=fixture@example.invalid", "commit", "-m", "Initialize isolated dashboard fixture");
        return directory;
    }
    private static void Change(string repo, string name, string design)
    {
        var directory = Path.Combine(repo, "openspec", "changes", name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ".openspec.yaml"), "schema: spec-driven\ncreated: 2026-10-01\n");
        File.WriteAllText(Path.Combine(directory, "design.md"), design);
        File.WriteAllText(Path.Combine(directory, "proposal.md"), "# Proposal\n\nA synthetic dashboard acceptance change.\n");
        File.WriteAllText(Path.Combine(directory, "tasks.md"), "# Tasks\n\n## 1. Implementation\n\n- [x] 1.1 Establish the approach\n- [ ] 1.2 Implement the change\n- [ ] 1.3 Verify the outcome\n");
        foreach (var capability in new[] { "work-dashboard", "openspec-browser" })
        {
            var specs = Path.Combine(directory, "specs", capability);
            Directory.CreateDirectory(specs);
            File.WriteAllText(Path.Combine(specs, "spec.md"), $"# {capability} specification\n\nNested acceptance specification.\n");
        }
    }

    public static async Task ExerciseMcpAsync(string executable, string directory)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var data = Path.Combine(Path.GetFullPath(directory), "data");
        var repo = Path.Combine(Path.GetFullPath(directory), "worklog");
        var isDll = executable.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(new()
        {
            Name = "dashboard-concurrency", Command = isDll ? "dotnet" : executable,
            Arguments = (isDll ? new[] { Path.GetFullPath(executable) } : []).Concat(["serve", "--repository", repo, "--data-dir", data]).ToArray()
        }), cancellationToken: timeout.Token);
        File.WriteAllText(Path.Combine(Path.GetFullPath(directory), "dashboard-mcp.started"), "ready");
        for (var i = 0; i < 5; i++)
        {
            var result = await client.CallToolAsync("log_work", new Dictionary<string, object?> { ["entry"] = new { status = "in_progress", change = "concurrent-mcp", summary = "Isolated dashboard MCP observation " + i, idempotencyKey = "dashboard-mcp-" + i } }, cancellationToken: timeout.Token);
            if (result.IsError == true) throw new InvalidOperationException("Concurrent MCP logging failed.");
            await Task.Delay(2000, timeout.Token);
        }
        var query = await client.CallToolAsync("query_work", new Dictionary<string, object?> { ["period"] = new { from = DateTimeOffset.UtcNow.AddDays(-1).ToString("O"), to = DateTimeOffset.UtcNow.AddDays(1).ToString("O"), timezone = "UTC", change = "concurrent-mcp", pageSize = 100 } }, cancellationToken: timeout.Token);
        if (query.IsError == true || query.StructuredContent?.GetProperty("entries").GetArrayLength() != 5)
            throw new InvalidOperationException("Concurrent MCP query did not preserve five observations.");
        Console.WriteLine("DASHBOARD MCP PASSED");
    }
}
