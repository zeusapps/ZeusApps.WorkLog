using System.Text.Json.Nodes;
using Tomlyn;
using WorkLog.Cli;
using WorkLog.Configuration;
using WorkLog.Integration;
using WorkLog.Journaling;
using WorkLog.Reporting;
using WorkLog.Repositories;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Integration;

public sealed class RepositoryInitializerTests
{
    [Fact]
    public void PartialFailureReportsAppliedStepsAndAllowsRerun()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        var context = new LocalSettings(box.Data);
        Directory.CreateDirectory(box.Data);
        // A file where the backup directory belongs creates a deterministic apply-time failure.
        File.WriteAllText(Path.Combine(box.Data, "integration-backups"), "block backups");
        File.WriteAllText(Path.Combine(box.Home, ".claude.json"), "{}");
        var init = new RepositoryInitializer(context, box.Home, false);
        var error = Assert.Throws<System.IO.IOException>(() => init.Run(box.Repo, "claude"));
        Assert.Contains("Applied:", error.Message);
        Assert.True(File.Exists(Path.Combine(box.Repo, ".worklog", "integration.json")));
        File.Delete(Path.Combine(box.Data, "integration-backups"));
        init.Run(box.Repo, "claude");
        Assert.Contains("worklog:begin", File.ReadAllText(Path.Combine(box.Repo, "CLAUDE.local.md")));
    }

    [Fact]
    public void CodexInheritsUserInstructionsWithoutReplacingBaseInstructions()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        Directory.CreateDirectory(Path.Combine(box.Home, ".codex"));
        File.WriteAllText(Path.Combine(box.Home, ".codex", "config.toml"), "developer_instructions='User guidance'\n");
        new RepositoryInitializer(new LocalSettings(box.Data), box.Home, false).Run(box.Repo, "codex");
        var text = File.ReadAllText(Path.Combine(box.Repo, ".codex", "config.toml"));
        Assert.Contains("User guidance", text);
        Assert.DoesNotContain("model_instructions_file", text);
        Assert.Contains("mcp_servers.worklog", text);
    }

    [Fact]
    public void DryRunAndRepeatedInitPreservePrivateAndSharedSettings()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        File.WriteAllText(Path.Combine(box.Repo, "AGENTS.md"), "Team instructions");
        File.WriteAllText(Path.Combine(box.Repo, "CLAUDE.md"), "Claude team instructions");
        Directory.CreateDirectory(Path.Combine(box.Repo, ".codex"));
        var codexPath = Path.Combine(box.Repo, ".codex", "config.toml");
        File.WriteAllText(codexPath, "# Keep this comment\ndeveloper_instructions = '''Project instructions'''\nmodel = 'example'\n[mcp_servers.other]\ncommand = 'other'\n");
        File.WriteAllText(Path.Combine(box.Home, ".claude.json"), "{\"existing\":true}");
        var context = new LocalSettings(box.Data);
        var init = new RepositoryInitializer(context, box.Home, checkClients: false);
        init.Run(box.Repo, dryRun: true);
        Assert.False(File.Exists(context.ConfigPath));
        Assert.False(File.Exists(Path.Combine(box.Repo, "CLAUDE.local.md")));
        init.Run(box.Repo);
        var first = File.ReadAllText(codexPath);
        init.Run(box.Repo);
        Assert.Equal(first, File.ReadAllText(codexPath));
        var toml = Toml.ToModel(first);
        Assert.Equal("example", toml["model"]);
        Assert.Contains("Project instructions", (string)toml["developer_instructions"]);
        Assert.Contains("# Keep this comment", first);
        var local = File.ReadAllText(Path.Combine(box.Repo, "CLAUDE.local.md"));
        Assert.Contains("updateHint", local);
        Assert.Contains("once per version per assistant session", local);
        Assert.Contains("updateHint", first);
        Assert.Contains("does not authorize installation", first);
        Assert.Contains("@AGENTS.md", local);
        Assert.Equal("Team instructions", File.ReadAllText(Path.Combine(box.Repo, "AGENTS.md")));
        Assert.Equal("Claude team instructions", File.ReadAllText(Path.Combine(box.Repo, "CLAUDE.md")));
        Assert.True(JsonNode.Parse(File.ReadAllText(Path.Combine(box.Home, ".claude.json")))!["existing"]!.GetValue<bool>());
        Assert.Contains(".codex/config.toml", GitRepository.Run(box.Repo, "check-ignore", ".codex/config.toml"));
        Assert.Contains("CLAUDE.local.md", GitRepository.Run(box.Repo, "check-ignore", "CLAUDE.local.md"));
        Assert.False(File.Exists(Path.Combine(box.Repo, ".gitignore")));
        Assert.DoesNotContain(".worklog", GitRepository.Run(box.Repo, "status", "--porcelain"));
    }

    [Fact]
    public void MalformedAndTrackedTargetsFailBeforeAnyWrites()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        var path = Path.Combine(box.Home, ".claude.json");
        File.WriteAllText(path, "{");
        var init = new RepositoryInitializer(new LocalSettings(box.Data), box.Home, false);
        Assert.ThrowsAny<Exception>(() => init.Run(box.Repo));
        Assert.Equal("{", File.ReadAllText(path));
        Assert.False(File.Exists(Path.Combine(box.Repo, "CLAUDE.local.md")));
        File.WriteAllText(path, "{}");
        File.WriteAllText(Path.Combine(box.Repo, "CLAUDE.local.md"), "Tracked");
        box.Git("add", "CLAUDE.local.md");
        Assert.Throws<InvalidOperationException>(() => init.Run(box.Repo));
        Assert.Equal("Tracked", File.ReadAllText(Path.Combine(box.Repo, "CLAUDE.local.md")));
    }

    [Fact]
    public void UnownedServerIsNotOverwritten()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        Directory.CreateDirectory(Path.Combine(box.Repo, ".codex"));
        var path = Path.Combine(box.Repo, ".codex", "config.toml");
        const string original = "[mcp_servers.worklog]\ncommand='something-else'\n";
        File.WriteAllText(path, original);
        Assert.Throws<InvalidOperationException>(() => new RepositoryInitializer(new LocalSettings(box.Data), box.Home, false).Run(box.Repo, "codex"));
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public void WorktreesUseResolvedLocalExclusionsAndStableIdentity()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        box.Git("-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "--allow-empty", "-m", "initial");
        var worktree = Path.Combine(box.Root, "worktree");
        box.Git("worktree", "add", "-b", "test-work", worktree);
        var context = new LocalSettings(box.Data);
        var first = new RepositoryRegistry(context).Get(box.Repo);
        new RepositoryInitializer(context, box.Home, false).Run(worktree, "claude");
        Assert.Contains("CLAUDE.local.md", GitRepository.Run(worktree, "check-ignore", "CLAUDE.local.md"));
        Assert.Equal(first.Identity, new RepositoryRegistry(new LocalSettings(box.Data)).Get(box.Repo).Identity);
    }
}
