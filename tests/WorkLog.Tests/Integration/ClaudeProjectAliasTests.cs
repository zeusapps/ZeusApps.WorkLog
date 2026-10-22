using System.Text.Json.Nodes;
using WorkLog.Configuration;
using WorkLog.Integration;
using WorkLog.Integration.Clients;
using WorkLog.Repositories;
using WorkLog.Tests.Testing;
using Xunit;
using WorkLogJson = WorkLog.Infrastructure.Json;

namespace WorkLog.Tests.Integration;

public sealed class ClaudeProjectAliasTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AliasSelectionUsesFullCheckoutPathsAndPlatformComparison(bool windows)
    {
        const string canonical = "C:/Code/AliasRepo";
        var projects = new JsonObject
        {
            ["c:/code/aliasrepo"] = new JsonObject(),
            ["C:\\Code\\AliasRepo"] = new JsonObject(),
            [canonical + "-other"] = new JsonObject(),
            ["C:/Code/Worktree"] = new JsonObject()
        };
        Assert.Equal(windows ? ["c:/code/aliasrepo", "C:\\Code\\AliasRepo", canonical] : [canonical],
            ClaudeIntegration.ProjectKeys(projects, canonical, windows));
        projects[canonical] = new JsonObject();
        Assert.Single(ClaudeIntegration.ProjectKeys(projects, canonical, windows), key => key == canonical);
    }

    [Theory]
    [InlineData("lower")]
    [InlineData("canonical")]
    [InlineData("both")]
    [InlineData("all")]
    public void ReconciliationPreservesEachProjectAndUpdatesOnlyOwnedRegistrations(string variants)
    {
        if (!OperatingSystem.IsWindows())
            return;
        using var box = new TestWorkspace();
        box.Git("init");
        var settings = new LocalSettings(box.Data);
        new RepositoryInitializer(settings, box.Home, false, "old-worklog.exe").Run(box.Repo, "claude");
        var canonical = GitRepository.GetRoot(box.Repo).Replace('\\', '/');
        var lower = canonical.ToLowerInvariant();
        var keys = variants switch
        {
            "lower" => new[] { lower },
            "canonical" => [canonical],
            "both" => [lower, canonical],
            _ => [lower, canonical, canonical.ToUpperInvariant(), canonical.Replace('/', '\\')]
        };
        var projects = new JsonObject();
        foreach (var key in keys.Distinct())
            projects[key] = Project(key);
        var unrelated = canonical + "-other";
        var worktree = Path.Combine(box.Root, "worktree").Replace('\\', '/');
        if (variants == "all")
        {
            box.Git("-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "--allow-empty", "-m", "initial");
            box.Git("worktree", "add", "-b", "alias-test-worktree", worktree);
        }
        projects[unrelated] = Project("unrelated");
        projects[worktree] = Project("worktree");
        var original = projects.DeepClone();
        var path = Path.Combine(box.Home, ".claude.json");
        File.WriteAllText(path, new JsonObject { ["rootSentinel"] = 42, ["projects"] = projects }.ToJsonString());

        var init = new RepositoryInitializer(settings, box.Home, false, "new-worklog.exe");
        var before = Snapshot(box, settings);
        var preview = init.Run(box.Repo, "claude", dryRun: true);
        Assert.Equal(before, Snapshot(box, settings));
        foreach (var key in keys.Append(canonical).Distinct())
            Assert.Contains(preview, message => message.Contains("Would reconcile Claude project '" + key + "'"));
        init.Run(box.Repo, "claude");
        var result = JsonNode.Parse(File.ReadAllText(path))!;
        var updated = result["projects"]!.AsObject();
        Assert.Equal(42, result["rootSentinel"]!.GetValue<int>());
        Assert.Equal(keys.Append(canonical).Distinct().Count() + 2, updated.Count);
        Assert.True(JsonNode.DeepEquals(original[unrelated], updated[unrelated]));
        Assert.True(JsonNode.DeepEquals(original[worktree], updated[worktree]));
        JsonNode? registration = null;
        foreach (var key in keys.Append(canonical).Distinct())
        {
            var actual = updated[key]!.AsObject();
            var server = actual["mcpServers"]!["worklog"]!;
            Assert.Equal("new-worklog.exe", server["command"]!.GetValue<string>());
            Assert.Equal(box.Data, server["args"]!.AsArray().Last()!.GetValue<string>());
            if (registration is not null)
                Assert.True(JsonNode.DeepEquals(registration, server));
            registration = server;
            var withoutOwned = actual.DeepClone();
            withoutOwned["mcpServers"]!.AsObject().Remove("worklog");
            var expected = original[key]?.DeepClone() ?? new JsonObject { ["mcpServers"] = new JsonObject() };
            expected["mcpServers"]!.AsObject().Remove("worklog");
            Assert.True(JsonNode.DeepEquals(expected, withoutOwned));
        }
        var first = File.ReadAllText(path);
        init.Run(box.Repo, "claude");
        Assert.Equal(first, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("unowned")]
    [InlineData("changed-command")]
    [InlineData("project-array")]
    [InlineData("servers-array")]
    [InlineData("server-array")]
    [InlineData("server-string")]
    [InlineData("command-number")]
    [InlineData("args-object")]
    [InlineData("args-number")]
    [InlineData("type-http")]
    public void ConflictUnderAliasAbortsEveryTargetAndPreferenceChange(string conflict)
    {
        if (!OperatingSystem.IsWindows())
            return;
        using var box = new TestWorkspace();
        box.Git("init");
        var settings = new LocalSettings(box.Data);
        var init = new RepositoryInitializer(settings, box.Home, false, "old-worklog.exe");
        init.Run(box.Repo);
        var canonical = GitRepository.GetRoot(box.Repo).Replace('\\', '/');
        var alias = canonical.ToUpperInvariant();
        var path = Path.Combine(box.Home, ".claude.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["projects"]![alias] = Project("alias");
        var project = document["projects"]![alias]!;
        switch (conflict)
        {
            case "unowned":
                File.Delete(Path.Combine(box.Repo, ".worklog", "integration.json"));
                break;
            case "changed-command":
                project["mcpServers"]!["worklog"]!["command"] = "unowned.exe";
                break;
            case "project-array":
                document["projects"]![alias] = new JsonArray();
                break;
            case "servers-array":
                project["mcpServers"] = new JsonArray();
                break;
            case "server-array":
                project["mcpServers"]!["worklog"] = new JsonArray();
                break;
            case "server-string":
                project["mcpServers"]!["worklog"] = "malformed";
                break;
            case "command-number":
                project["mcpServers"]!["worklog"]!["command"] = 12;
                break;
            case "args-object":
                project["mcpServers"]!["worklog"]!["args"] = new JsonObject();
                break;
            case "args-number":
                project["mcpServers"]!["worklog"]!["args"] = new JsonArray(12);
                break;
            case "type-http":
                project["mcpServers"]!["worklog"]!["type"] = "http";
                break;
        }
        // Keep the canonical entry clean, so the alias is the first actual conflict.
        if (conflict == "unowned")
            document["projects"]![canonical]!["mcpServers"]!.AsObject().Remove("worklog");
        File.WriteAllText(path, document.ToJsonString());
        var before = Snapshot(box, settings);
        var error = Record.Exception(() => init.Run(box.Repo, instructions: false, ticketProvider: "jira", ticketScope: "new-scope"));
        Assert.NotNull(error);
        Assert.Contains(alias, error.Message);
        Assert.Equal(before, Snapshot(box, settings));
    }

    [Fact]
    public void FirstInitReconcilesEmptyAliasWithoutClaimingOtherServers()
    {
        if (!OperatingSystem.IsWindows())
            return;
        using var box = new TestWorkspace();
        box.Git("init");
        var canonical = GitRepository.GetRoot(box.Repo).Replace('\\', '/');
        var alias = canonical.ToUpperInvariant();
        var project = Project("preexisting");
        project["mcpServers"]!.AsObject().Remove("worklog");
        var configPath = Path.Combine(box.Home, ".claude.json");
        File.WriteAllText(configPath, new JsonObject { ["projects"] = new JsonObject { [alias] = project } }.ToJsonString());
        var settings = new LocalSettings(box.Data);
        new RepositoryInitializer(settings, box.Home, false, "new-worklog.exe").Run(box.Repo, "claude", instructions: false);
        var projects = JsonNode.Parse(File.ReadAllText(configPath))!["projects"]!;
        foreach (var key in new[] { canonical, alias })
            Assert.Equal("new-worklog.exe", projects[key]!["mcpServers"]!["worklog"]!["command"]!.GetValue<string>());
        Assert.Equal("preexisting", projects[alias]!["marker"]!.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(box.Repo, "CLAUDE.local.md")));
    }

    [Fact]
    public void AliasReconciliationRespectsPersistentInstructionPreference()
    {
        if (!OperatingSystem.IsWindows())
            return;
        using var box = new TestWorkspace();
        box.Git("init");
        var settings = new LocalSettings(box.Data);
        var init = new RepositoryInitializer(settings, box.Home, false, "old-worklog.exe");
        init.Run(box.Repo, "claude");
        var path = Path.Combine(box.Home, ".claude.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        var canonical = GitRepository.GetRoot(box.Repo).Replace('\\', '/');
        var alias = canonical.ToUpperInvariant();
        document["projects"]![alias] = Project("alias");
        File.WriteAllText(path, document.ToJsonString());
        var before = Snapshot(box, settings);
        init.Run(box.Repo, "claude", dryRun: true, instructions: false);
        Assert.Equal(before, Snapshot(box, settings));
        init.Run(box.Repo, "claude", instructions: false);
        init.Run(box.Repo, "claude");
        Assert.DoesNotContain("worklog:begin", File.ReadAllText(Path.Combine(box.Repo, "CLAUDE.local.md")));
        Assert.False(new RepositoryRegistry(settings).GetContext(box.Repo).InstructionsEnabled("claude"));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(path))!["projects"]![canonical]!["mcpServers"]!["worklog"],
            JsonNode.Parse(File.ReadAllText(path))!["projects"]![alias]!["mcpServers"]!["worklog"]));
        init.Run(box.Repo, "claude", instructions: true);
        Assert.Contains("worklog:begin", File.ReadAllText(Path.Combine(box.Repo, "CLAUDE.local.md")));
    }

    private static JsonObject Project(string marker) => new()
    {
        ["marker"] = marker,
        ["hasTrustDialogAccepted"] = true,
        ["mcpServers"] = new JsonObject
        {
            ["other"] = new JsonObject { ["type"] = "http", ["url"] = "https://example.invalid/mcp" },
            ["worklog-legacy"] = new JsonObject { ["command"] = "leave-legacy-alone" },
            ["worklog"] = new JsonObject { ["type"] = "stdio", ["command"] = "old-worklog.exe", ["args"] = new JsonArray("stale") }
        }
    };

    private static string[] Snapshot(TestWorkspace box, LocalSettings settings) =>
        new[] { settings.ConfigPath, Path.Combine(box.Home, ".claude.json"), Path.Combine(box.Repo, ".worklog", "integration.json"),
            Path.Combine(box.Repo, ".git", "info", "exclude"), Path.Combine(box.Repo, "CLAUDE.local.md"), Path.Combine(box.Repo, ".codex", "config.toml") }
            .Select(path => File.Exists(path) ? File.ReadAllText(path) : "<missing>")
            .Append(WorkLogJson.Encode(settings.Configuration)).ToArray();
}
