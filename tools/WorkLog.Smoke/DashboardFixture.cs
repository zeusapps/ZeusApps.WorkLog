using Microsoft.Data.Sqlite;
using ModelContextProtocol.Client;
using WorkLog.Configuration;
using WorkLog.Infrastructure;
using WorkLog.Journaling;
using WorkLog.Persistence;
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
        var links = Repository(root, "links-demo");
        settings.Configuration.RepositoryContexts[GitRepository.GetCommonDirectory(links)] = new(new("github.com/example/links-demo"));
        var storeMember = Repository(root, "store-member");
        File.WriteAllText(Path.Combine(storeMember, "openspec", "config.yaml"), "store: dashboard-fixture-missing-" + Guid.NewGuid().ToString("N") + "\n");
        settings.Configuration.RepositoryContexts[GitRepository.GetCommonDirectory(storeMember)] = new(new("example/store-member"));
        var precedence = Repository(root, "store-precedence");
        File.WriteAllText(Path.Combine(precedence, "openspec", "config.yaml"), "store: dashboard-fixture-missing-" + Guid.NewGuid().ToString("N") + "\n");
        settings.Configuration.RepositoryContexts[GitRepository.GetCommonDirectory(precedence)] = new(new("example/store-precedence"));
        Change(precedence, "real-root-precedence", "# Real root precedence\n\nA real planning root takes precedence over its store declaration.\n");
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
        Change(links, "quiet-change", "# Quiet change\n\nA change whose latest recorded observation is old.\n");
        Change(links, "never-logged", "# Never logged\n\nA change without any recorded observation.\n", ticked: 0);
        var linksJournal = new Journal(settings, new("github.com/example/links-demo"));
        void LogLink(Journal target, string status, string branch, Ticket ticket, int days, string summary, string? change = null) => target.Log(new(status, change, branch,
            Tickets: [ticket], Summary: summary, OccurredAt: now.AddDays(-days).ToString("O"), Agent: "fixture"));
        LogLink(linksJournal, "started", "feature/DEP-12", new("jira", "company.atlassian.net", "DEP-12"), 6, "Started the linked Jira work.");
        LogLink(linksJournal, "in_progress", "feature/DEP-12", new("jira", "company.atlassian.net", "DEP-12"), 2, "Continued the linked Jira work.");
        LogLink(linksJournal, "in_progress", "feature/DEP-12-other", new("jira", "other.atlassian.net", "DEP-12"), 1, "Same ID on a different Jira site.");
        LogLink(linksJournal, "in_progress", "main", new("azure-devops", "contoso/Payments", "4211"), 1, "Azure DevOps work item.");
        LogLink(linksJournal, "in_progress", "main", new("jira", "tracker", "REC-5", "https://tracker.example.invalid/browse/REC-5"), 1, "Ticket with a recorded URL.");
        LogLink(linksJournal, "in_progress", "main", new("jira", "Some Site", "UNK-7"), 1, "Ticket with an unrecognized scope.");
        linksJournal.Log(new("in_progress", "quiet-change", Summary: "Old recorded observation.", OccurredAt: now.AddDays(-20).ToString("O"), Agent: "fixture"));
        LogLink(new Journal(settings, new("local:links-demo-local")), "in_progress", "feature/DEP-30", new("jira", "company.atlassian.net", "DEP-30"), 1, "Work in a local-only repository.");
        new Journal(settings, new("example/bell-tower")).Log(new("in_progress", "shipping-reports", Summary: "Prepared report evidence.", OccurredAt: now.AddDays(-1).ToString("O"), Agent: "fixture"));
        BoardDemo(root, settings, now);
        DailyDemo(root, settings);
        DigestDemo(root, settings);
        AtomicFile.Write(Path.Combine(root, "fixture.json"), Json.Encode(new { root, data = settings.DataDirectory, repo = a, otherRepo = b, worktree }));
        Console.WriteLine(root);
    }

    /// <summary>
    /// A repository for the Ongoing board: one change per column, a second in-progress change to reorder, a follow-up ticket for an archived change,
    /// a ticket without a change, and ad-hoc observations 6 and 8 days old. Observations with neither ticket nor change cannot be logged
    /// through the current tools, so they are written as an older journal would have held them.
    /// </summary>
    private static void BoardDemo(string root, LocalSettings settings, DateTimeOffset now)
    {
        var board = Repository(root, "board-demo");
        settings.Configuration.RepositoryContexts[GitRepository.GetCommonDirectory(board)] = new(new("example/board-demo"));
        settings.Save();
        Change(board, "board-proposal", "# Board proposal\n\nNothing is ticked and nothing is logged.\n", ticked: 0);
        Change(board, "board-started", "# Board started\n\nWork was logged but no task is ticked.\n", ticked: 0);
        Change(board, "board-second", "# Board second\n\nA second in-progress change.\n", ticked: 0);
        Change(board, "board-recorded-done", "# Board recorded done\n\nDone was logged while tasks stay unticked.\n", ticked: 1);
        Change(board, "archive/2026-10-01-board-shipped", "# Board shipped\n\nAn archived change that later work follows up.\n");
        var journal = new Journal(settings, new("example/board-demo"));
        void Log(string status, string? change, int days, string summary, params Ticket[] tickets) => journal.Log(new(status, change, Tickets: tickets,
            Summary: summary, OccurredAt: now.AddDays(-days).ToString("O"), Agent: "fixture"));
        Log("in_progress", "board-started", 1, "Logged progress without ticking tasks.", new Ticket("jira", "example.local", "BRD-1"));
        Log("in_progress", "board-second", 3, "Started the second change.");
        Log("done", "board-recorded-done", 2, "Recorded the work as done.");
        Log("in_progress", "board-shipped", 2, "Follow-up work after the archive.", new Ticket("jira", "example.local", "BRD-70"));
        Log("in_progress", null, 1, "Ticket work with no OpenSpec change.", new Ticket("jira", "example.local", "BRD-71"));
        using var connection = new SqliteConnection($"Data Source={Path.Combine(settings.DataDirectory, "journal.db")};Pooling=False");
        connection.Open();
        void Unlinked(int days, string status, string summary, string? extracted = null)
        {
            var at = now.AddDays(-days);
            var entry = new Entry(Guid.NewGuid().ToString("N"), "example/board-demo", extracted is null ? null : "feature/" + extracted, null, [], extracted, status,
                "unclassified", summary, at, at, "fixture", null);
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO entries(id,occurred,repository,change_name,status,payload) VALUES($id,$at,$repo,NULL,$status,$payload)";
            command.Parameters.AddWithValue("$id", entry.Id);
            command.Parameters.AddWithValue("$at", at.UtcTicks);
            command.Parameters.AddWithValue("$repo", entry.Repository);
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$payload", Json.Encode(entry));
            command.ExecuteNonQuery();
        }
        Unlinked(6, "done", "Adhoc six days old", "BRD-99");
        Unlinked(8, "done", "Adhoc eight days old");
    }

    /// <summary>
    /// A repository for the Daily view at fixed past dates, so a test with a fake clock on Monday 12 January 2026 sees Friday as the last recorded day.
    /// Times sit around noon UTC, which keeps each observation on the same calendar day in any timezone from UTC-11 to UTC+10.
    /// </summary>
    private static void DailyDemo(string root, LocalSettings settings)
    {
        var daily = Repository(root, "daily-demo");
        settings.Configuration.RepositoryContexts[GitRepository.GetCommonDirectory(daily)] = new(new("example/daily-demo"));
        settings.Save();
        Change(daily, "daily-flow", "# Daily flow\n\nA change with work recorded on the last working day.\n", ticked: 0);
        var journal = new Journal(settings, new("example/daily-demo"));
        void Log(string status, string? change, string at, string summary, string ticket) => journal.Log(new(status, change,
            Tickets: [new("jira", "example.local", ticket)], Summary: summary, OccurredAt: at, Agent: "fixture"));
        Log("in_progress", null, "2026-01-06T12:00:00Z", "Sketched the export schedule.", "DEMO-3");
        Log("started", null, "2026-01-09T11:00:00Z", "Began the retry policy.", "DEMO-1");
        Log("in_progress", "daily-flow", "2026-01-09T12:00:00Z", "Wired the scheduler hook; load test remains.", "DEMO-2");
        Log("done", null, "2026-01-09T13:00:00Z", "Retry policy for export jobs merged.", "DEMO-1");
    }

    /// <summary>
    /// A repository for agent digests on the Daily view, at the same fixed dates as <see cref="DailyDemo"/>: DGT-1 has two observations on Friday 9 January
    /// and DGT-2 has two on Monday 12 January, each pair covered by a stored digest written through the real save path into the isolated <c>digests.db</c>.
    /// </summary>
    private static void DigestDemo(string root, LocalSettings settings)
    {
        var digest = Repository(root, "digest-demo");
        settings.Configuration.RepositoryContexts[GitRepository.GetCommonDirectory(digest)] = new(new("example/digest-demo"));
        settings.Save();
        var journal = new Journal(settings, new("example/digest-demo"));
        string Log(string status, string at, string summary, string ticket) => journal.Log(new(status, null, Tickets: [new("jira", "example.local", ticket)],
            Summary: summary, OccurredAt: at, Agent: "fixture")).Entry.Id;
        var service = new DigestService(journal, new DigestStore(settings.DataDirectory), TimeZoneInfo.Utc);
        service.Save([Log("started", "2026-01-09T11:00:00Z", "Drafted the audit export.", "DGT-1"), Log("done", "2026-01-09T13:00:00Z", "Audit export reviewed and merged.", "DGT-1")],
            "Built and merged the audit export.");
        service.Save([Log("started", "2026-01-12T10:00:00Z", "Began the retention sweep.", "DGT-2"), Log("in_progress", "2026-01-12T11:00:00Z", "Sweep deletes expired rows in batches.", "DGT-2")],
            "Retention sweep runs in batches.");
    }

    /// <summary>Records one observation with a ticket into the fixture journal, as another agent session would while the dashboard is open.</summary>
    public static void Log(string directory, string repository, string status, string change, string ticket, string occurredAt, string summary)
    {
        var settings = new LocalSettings(Path.Combine(Path.GetFullPath(directory), "data"));
        new Journal(settings, new(repository)).Log(new(status, change is "-" ? null : change, Tickets: [new("jira", "example.local", ticket)],
            Summary: summary, OccurredAt: occurredAt, Agent: "fixture"));
    }

    /// <summary>Saves a digest through the real save path for the ticket's observations on the day of its latest one, as an agent would while the dashboard is open.</summary>
    public static void SaveDigest(string directory, string repository, string ticket, string text) =>
        SaveDigest(directory, repository, ticket, (service, ids) => service.Save(ids, text));

    /// <summary>Like <see cref="SaveDigest(string,string,string,string)"/> but through the structured save; each list is bullets separated by <c>|</c> (empty for none).</summary>
    public static void SaveStructuredDigest(string directory, string repository, string ticket, string done, string next, string blockers)
    {
        static string[] Split(string list) => list.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        SaveDigest(directory, repository, ticket, (service, ids) => service.Save(ids, Split(done), Split(next), Split(blockers)));
    }

    private static void SaveDigest(string directory, string repository, string ticket, Action<DigestService, string[]> save)
    {
        var settings = new LocalSettings(Path.Combine(Path.GetFullPath(directory), "data"));
        var journal = new Journal(settings, new(repository));
        var service = new DigestService(journal, new DigestStore(settings.DataDirectory), TimeZoneInfo.Utc);
        var latest = journal.Query(new("2000-01-01T00:00:00Z", "2100-01-01T00:00:00Z", "UTC", repository, Ticket: ticket, PageSize: 100, View: "flat"))
            .Entries.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id, StringComparer.Ordinal).Last();
        var request = service.Request(latest) ?? throw new InvalidOperationException("No digest request for " + ticket + ".");
        save(service, request.Entries.Select(e => e.Id).ToArray());
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
    private static void Change(string repo, string name, string design, int ticked = 1)
    {
        var directory = Path.Combine(repo, "openspec", "changes", name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ".openspec.yaml"), "schema: spec-driven\ncreated: 2026-10-01\n");
        File.WriteAllText(Path.Combine(directory, "design.md"), design);
        File.WriteAllText(Path.Combine(directory, "proposal.md"), "# Proposal\n\nA synthetic dashboard acceptance change.\n");
        File.WriteAllText(Path.Combine(directory, "tasks.md"), "# Tasks\n\n## 1. Implementation\n\n" + string.Join("", new[] { "1.1 Establish the approach", "1.2 Implement the change", "1.3 Verify the outcome" }.Select((task, index) => (index < ticked ? "- [x] " : "- [ ] ") + task + "\n")));
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
