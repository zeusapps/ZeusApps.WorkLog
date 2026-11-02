using System.Text.Json;
using Tomlyn;
using WorkLog.Configuration;
using WorkLogJson = WorkLog.Infrastructure.Json;
using WorkLog.Integration;
using WorkLog.Journaling;
using WorkLog.Repositories;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Integration;

public sealed class InstructionOptionsTests
{
    private const string SummaryGuidance = "Include a summary with every observation: one short sentence (about 140 characters) stating the concrete outcome, decision, or remaining step; for in_progress what was achieved and what remains, for done what was completed. Do not narrate process, repeat the status, or include secrets or personal data.";

    [Fact]
    public void RenderedInstructionsShareSummaryGuidanceAndOtherwiseKeepPriorText()
    {
        var claude = AgentInstructions.Render("repo-id", "claude");
        var codex = AgentInstructions.Render("repo-id", "codex");
        Assert.Contains(SummaryGuidance, claude);
        Assert.Contains(SummaryGuidance, codex);
        Assert.DoesNotContain("small factual summary", claude);
        // Apart from the agent name, the blocks are identical, so the guidance cannot diverge per client.
        Assert.Equal(claude.Replace("Agent: claude.", "Agent: <agent>."), codex.Replace("Agent: codex.", "Agent: <agent>."));
        // Substituting the previous sentence back must restore the prior block lines exactly.
        var normalized = claude.Replace("\r\n", "\n");
        var restored = normalized.Replace(SummaryGuidance + "\n", "Include a small factual summary when useful. ");
        var lines = restored.Split('\n').Where(line => !line.Contains("digestRequest", StringComparison.Ordinal)).ToArray();
        Assert.Contains("Include a small factual summary when useful. Ticket details can come from separate official issue MCPs.", lines);
        Assert.Equal(15, lines.Length);
        Assert.Contains("Log done when this session establishes the feature is complete for reporting; archive timing is independent.", lines);
        Assert.Contains("Report logging failures; do not claim entries were saved if a call failed.", lines);
    }

    [Fact]
    public void FreshOptOutRegistersBothClientsWithoutCopyingInstructions()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        Directory.CreateDirectory(Path.Combine(box.Home, ".codex"));
        File.WriteAllText(Path.Combine(box.Home, ".codex", "config.toml"), "developer_instructions='Global guidance'\n");
        File.WriteAllText(Path.Combine(box.Repo, "AGENTS.md"), "Application rules");
        Init(box).Run(box.Repo, instructions: false);
        Init(box).Run(box.Repo);
        Assert.False(File.Exists(Path.Combine(box.Repo, "CLAUDE.local.md")));
        var codex = File.ReadAllText(Path.Combine(box.Repo, ".codex", "config.toml"));
        Assert.Contains("mcp_servers.worklog", codex);
        Assert.DoesNotContain("developer_instructions", codex);
        Assert.DoesNotContain("Global guidance", codex);
        Assert.Contains("mcpServers", File.ReadAllText(Path.Combine(box.Home, ".claude.json")));
        Assert.Equal("Application rules", File.ReadAllText(Path.Combine(box.Repo, "AGENTS.md")));
        var context = Context(box);
        Assert.False(context.InstructionsEnabled("claude"));
        Assert.False(context.InstructionsEnabled("codex"));
    }

    [Fact]
    public void OptOutRemovesOnlyOwnedContentAndExplicitReenableRestoresOneBlock()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        Directory.CreateDirectory(Path.Combine(box.Repo, ".codex"));
        var codexPath = Path.Combine(box.Repo, ".codex", "config.toml");
        const string codexOriginal = "# Application comment\ndeveloper_instructions = 'Application rules'\nmodel = 'example'\n";
        File.WriteAllText(codexPath, codexOriginal);
        var claudePath = Path.Combine(box.Repo, "CLAUDE.local.md");
        File.WriteAllText(claudePath, "Application rules\n");
        Init(box).Run(box.Repo);
        File.AppendAllText(claudePath, "After block\n");
        Init(box).Run(box.Repo, instructions: false);
        var claude = File.ReadAllText(claudePath);
        var codex = File.ReadAllText(codexPath);
        Assert.DoesNotContain("worklog:begin", claude + codex);
        Assert.DoesNotContain("updateHint", claude + codex);
        Assert.StartsWith("Application rules\n", claude);
        Assert.EndsWith("After block\n", claude);
        Assert.Contains("# Application comment", codex);
        Assert.Equal("Application rules", ((string)Toml.ToModel(codex)["developer_instructions"]).Trim());
        Assert.Equal("example", Toml.ToModel(codex)["model"]);
        Assert.Contains("mcp_servers.worklog", codex);

        Init(box).Run(box.Repo);
        Assert.Equal(claude, File.ReadAllText(claudePath));
        Assert.Equal(codex, File.ReadAllText(codexPath));
        Init(box).Run(box.Repo, instructions: true);
        Init(box).Run(box.Repo);
        foreach (var path in new[] { claudePath, codexPath })
        {
            var text = File.ReadAllText(path);
            Assert.Equal(1, text.Split("worklog:begin").Length - 1);
            Assert.Contains("updateHint", text);
            Assert.Contains("does not authorize installation", text);
        }
    }

    [Fact]
    public void DryRunDoesNotChangeFilesOrInMemoryPreferencesAndKeepsTicketPattern()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        Init(box).Run(box.Repo, ticketProvider: "jira", ticketScope: "original");
        var settings = new LocalSettings(box.Data);
        var context = new RepositoryRegistry(settings).GetContext(box.Repo);
        context.Repository = context.Repository with { TicketPattern = @"(?<id>DEP-\d+)" };
        settings.Save();
        var paths = new[] { settings.ConfigPath, Path.Combine(box.Home, ".claude.json"),
            Path.Combine(box.Repo, "CLAUDE.local.md"), Path.Combine(box.Repo, ".codex", "config.toml") };
        var before = paths.Select(File.ReadAllText).ToArray();
        var configBefore = WorkLogJson.Encode(settings.Configuration);
        var initializer = new RepositoryInitializer(settings, box.Home, false);
        var messages = initializer.Run(box.Repo, dryRun: true, ticketProvider: "jira", ticketScope: "new", instructions: false);
        Assert.Equal(before, paths.Select(File.ReadAllText).ToArray());
        Assert.Equal(configBefore, WorkLogJson.Encode(settings.Configuration));
        Assert.Contains(messages, message => message.Contains("disabled") && message.Contains("planned"));
        initializer.Run(box.Repo);
        Assert.Equal("original", Context(box).Repository.Scope);
        Assert.Contains("worklog:begin", File.ReadAllText(paths[2]));
        initializer.Run(box.Repo, ticketProvider: " jira ", ticketScope: " new ");
        var after = Context(box).Repository;
        Assert.Equal("jira", after.Provider);
        Assert.Equal("new", after.Scope);
        Assert.Equal(@"(?<id>DEP-\d+)", after.TicketPattern);
        var entry = new Journal(new LocalSettings(box.Data), after).Log(new("in_progress", Branch: "feature/DEP-12")).Entry;
        Assert.Equal("new", Assert.Single(entry.Tickets).Scope);
    }

    [Fact]
    public void MixedClientPreferencesAreInheritedWithoutSweepingSiblingFiles()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        box.Git("-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "--allow-empty", "-m", "initial");
        var worktree = Path.Combine(box.Root, "worktree");
        box.Git("worktree", "add", "-b", "test-work", worktree);
        Init(box).Run(box.Repo);
        Init(box).Run(worktree);
        var sibling = Path.Combine(worktree, "CLAUDE.local.md");
        var siblingBefore = File.ReadAllText(sibling);
        Init(box).Run(box.Repo, "claude", instructions: false);
        Assert.Equal(siblingBefore, File.ReadAllText(sibling));
        Init(box).Run(worktree);
        Assert.DoesNotContain("worklog:begin", File.ReadAllText(sibling));
        Assert.Contains("worklog:begin", File.ReadAllText(Path.Combine(worktree, ".codex", "config.toml")));
        Init(box).Run(box.Repo);
        Assert.DoesNotContain("worklog:begin", File.ReadAllText(Path.Combine(box.Repo, "CLAUDE.local.md")));
        Assert.False(Context(box).InstructionsEnabled("claude"));
        Assert.True(Context(box).InstructionsEnabled("codex"));
        var freshWorktree = Path.Combine(box.Root, "fresh-worktree");
        box.Git("worktree", "add", "-b", "fresh-work", freshWorktree);
        Init(box).Run(freshWorktree, "claude");
        Assert.False(File.Exists(Path.Combine(freshWorktree, "CLAUDE.local.md")));
    }

    [Theory]
    [InlineData("claude", "<!-- worklog:begin -->missing end")]
    [InlineData("claude", "<!-- worklog:end -->")]
    [InlineData("claude", "<!-- worklog:begin --><!-- worklog:end --><!-- worklog:begin --><!-- worklog:end -->")]
    [InlineData("codex", "<!-- worklog:begin -->missing end")]
    [InlineData("codex", "<!-- worklog:end -->")]
    [InlineData("codex", "<!-- worklog:begin --><!-- worklog:end --><!-- worklog:begin --><!-- worklog:end -->")]
    public void MalformedBlocksFailBeforeSavingPreferencesOrClientChanges(string client, string malformed)
    {
        using var box = new TestWorkspace();
        box.Git("init");
        Init(box).Run(box.Repo, client);
        var path = client == "claude" ? Path.Combine(box.Repo, "CLAUDE.local.md") : Path.Combine(box.Repo, ".codex", "config.toml");
        var text = client == "claude" ? malformed : "developer_instructions = " + JsonSerializer.Serialize(malformed) + "\n";
        File.WriteAllText(path, text);
        var settings = new LocalSettings(box.Data);
        var configBefore = File.ReadAllText(settings.ConfigPath);
        var clientBefore = client == "claude" ? File.ReadAllText(Path.Combine(box.Home, ".claude.json")) : text;
        Assert.Throws<ArgumentException>(() => new RepositoryInitializer(settings, box.Home, false).Run(box.Repo, client, instructions: false));
        Assert.Equal(text, File.ReadAllText(path));
        Assert.Equal(configBefore, File.ReadAllText(settings.ConfigPath));
        Assert.True(new RepositoryRegistry(settings).GetContext(box.Repo).InstructionsEnabled(client));
        if (client == "claude") Assert.Equal(clientBefore, File.ReadAllText(Path.Combine(box.Home, ".claude.json")));
    }

    [Fact]
    public void UntouchedTrackedInstructionsAreAllowedButOwnedRemovalStillConflicts()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        var path = Path.Combine(box.Repo, "CLAUDE.local.md");
        const string original = "Application instructions\n";
        File.WriteAllText(path, original);
        box.Git("add", "CLAUDE.local.md");
        Init(box).Run(box.Repo, "claude", instructions: false);
        Assert.Equal(original, File.ReadAllText(path));
        var owned = original + "<!-- worklog:begin -->owned<!-- worklog:end -->";
        File.WriteAllText(path, owned);
        var before = File.ReadAllText(new LocalSettings(box.Data).ConfigPath);
        Assert.Throws<InvalidOperationException>(() => Init(box).Run(box.Repo, "claude", instructions: false));
        Assert.Equal(owned, File.ReadAllText(path));
        Assert.Equal(before, File.ReadAllText(new LocalSettings(box.Data).ConfigPath));
    }

    [Fact]
    public void FreshDisabledModeDoesNotParseUnrelatedGlobalCodexInstructions()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        Directory.CreateDirectory(Path.Combine(box.Home, ".codex"));
        File.WriteAllText(Path.Combine(box.Home, ".codex", "config.toml"), "malformed=[");
        Init(box).Run(box.Repo, "codex", instructions: false);
        Assert.DoesNotContain("developer_instructions", File.ReadAllText(Path.Combine(box.Repo, ".codex", "config.toml")));
    }

    [Theory]
    [InlineData("jira", null)]
    [InlineData(null, "company")]
    [InlineData("other", "company")]
    [InlineData("jira", " ")]
    [InlineData(" ", "company")]
    public void InvalidTicketOptionsDoNotWrite(string? provider, string? scope)
    {
        using var box = new TestWorkspace();
        box.Git("init");
        Assert.Throws<ArgumentException>(() => Init(box).Run(box.Repo, ticketProvider: provider, ticketScope: scope));
        Assert.False(Directory.Exists(Path.Combine(box.Repo, ".worklog")));
        Assert.False(Directory.Exists(box.Data));
        Assert.False(File.Exists(Path.Combine(box.Home, ".claude.json")));
    }

    private static string DigestParagraph(string block) =>
        Assert.Single(block.Replace("\r\n", "\n").Split('\n'), line => line.Contains("digestRequest", StringComparison.Ordinal));

    private static string Owned(string text)
    {
        var begin = text.IndexOf("worklog:begin", StringComparison.Ordinal);
        var end = text.IndexOf("worklog:end", StringComparison.Ordinal);
        Assert.True(begin >= 0 && end > begin);
        return text[begin..end].Replace("\r\n", "\n");
    }

    [Fact]
    public void DigestParagraphIsSharedByClaudeCodeAndCodexOwnedBlocksAndOmittedWhenDisabled()
    {
        // Covers both Claude Code (CLAUDE.local.md) and Codex (.codex/config.toml developer_instructions) as written by init.
        using var box = new TestWorkspace();
        box.Git("init");
        Init(box).Run(box.Repo);
        var claude = Owned(File.ReadAllText(Path.Combine(box.Repo, "CLAUDE.local.md")));
        var codex = Owned((string)Toml.ToModel(File.ReadAllText(Path.Combine(box.Repo, ".codex", "config.toml")))["developer_instructions"]);
        var paragraph = DigestParagraph(AgentInstructions.Render("repo", "claude"));
        Assert.Equal(paragraph, DigestParagraph(AgentInstructions.Render("repo", "codex")));
        Assert.Contains(paragraph, claude);
        Assert.Contains(paragraph, codex);
        Assert.Equal(DigestParagraph(claude), DigestParagraph(codex));
        Assert.Contains("worklog.save_digest", paragraph);
        Assert.Contains("finish the user's request first", paragraph);
        Assert.Contains("Do not retry a digest rejected as stale", paragraph);
        Assert.Contains("unavailable, skip the digest without reporting an error", paragraph);
        // The blocks differ only in the agent name, so digest policy cannot diverge per client.
        Assert.Equal(claude.Replace("Agent: claude.", "Agent: <agent>."), codex.Replace("Agent: codex.", "Agent: <agent>."));

        Init(box).Run(box.Repo, instructions: false);
        var claudePath = Path.Combine(box.Repo, "CLAUDE.local.md");
        Assert.True(!File.Exists(claudePath) || !File.ReadAllText(claudePath).Contains("save_digest"));
        Assert.DoesNotContain("save_digest", File.ReadAllText(Path.Combine(box.Repo, ".codex", "config.toml")));
    }

    private static RepositoryInitializer Init(TestWorkspace box) => new(new LocalSettings(box.Data), box.Home, false);
    private static RepositoryContext Context(TestWorkspace box) => new RepositoryRegistry(new LocalSettings(box.Data)).GetContext(box.Repo);
}
