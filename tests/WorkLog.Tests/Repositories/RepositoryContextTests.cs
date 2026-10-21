using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Repositories;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Repositories;

public sealed class RepositoryContextTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void WorktreesShareContextInEitherOrderAcrossRestarts(bool withOrigin, bool worktreeFirst)
    {
        using var box = new TestWorkspace();
        var worktree = CreateWorktree(box);
        if (withOrigin) box.Git("remote", "add", "origin", "https://example.invalid/org/repo.git");
        var settings = new LocalSettings(box.Data);
        var context = new RepositoryRegistry(settings).GetContext(worktreeFirst ? worktree : box.Repo);
        context.Repository = context.Repository with { Provider = "jira", Scope = "company", TicketPattern = @"(?<id>DEP-\d+)" };
        context.Instructions["claude"] = false;
        settings.Save();

        var restarted = new LocalSettings(box.Data);
        var other = new RepositoryRegistry(restarted).GetContext(worktreeFirst ? box.Repo : worktree);
        Assert.Equal(context.Repository, other.Repository);
        Assert.False(other.InstructionsEnabled("claude"));
        Assert.True(other.InstructionsEnabled("codex"));
        var entry = new Journal(restarted, other.Repository).Log(new("in_progress", Branch: "feature/DEP-12-repair")).Entry;
        Assert.Equal(context.Repository.Identity, entry.Repository);
        Assert.Equal("company", Assert.Single(entry.Tickets).Scope);
        Assert.Single(restarted.Configuration.RepositoryContexts);
    }

    [Fact]
    public void CompatibleLegacyRecordsPromoteWithoutLosingLocalIdentityOrContext()
    {
        using var box = new TestWorkspace();
        var worktree = CreateWorktree(box);
        box.Git("remote", "add", "origin", "https://example.invalid/new/origin.git");
        var settings = new LocalSettings(box.Data);
        settings.Configuration.Repositories[box.Repo] = new("local:saved", "jira", "company");
        settings.Configuration.Repositories[worktree] = new("local:saved");
        var missing = Path.Combine(box.Root, "removed-checkout");
        settings.Configuration.Repositories[missing] = new("other", "jira", "untouched");
        settings.Save();

        var context = new RepositoryRegistry(settings).GetContext(worktree);
        Assert.Equal(new RepositoryConfig("local:saved", "jira", "company"), context.Repository);
        Assert.Equal(3, settings.Configuration.Repositories.Count);
        Assert.Equal("untouched", settings.Configuration.Repositories[missing].Scope);
        Assert.Contains(missing, new LocalSettings(box.Data).Configuration.Repositories.Keys);

        // The promoted incomplete copy must not make effective-identity lookup ambiguous.
        var entry = new Journal(settings).Log(new("in_progress", Branch: "feature/DEP-12", Repository: "local:saved")).Entry;
        Assert.Equal("company", Assert.Single(entry.Tickets).Scope);
        context.Repository = context.Repository with { Scope = "updated" };
        settings.Save();
        Assert.Equal("updated", new RepositoryRegistry(new LocalSettings(box.Data)).Get(box.Repo).Scope);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("scope")]
    [InlineData("provider")]
    [InlineData("ticketPattern")]
    public void ConflictingLegacyRecordsFailWithoutPersisting(string field)
    {
        using var box = new TestWorkspace();
        var worktree = CreateWorktree(box);
        var settings = new LocalSettings(box.Data);
        var first = new RepositoryConfig("local:saved", "jira", "company");
        settings.Configuration.Repositories[box.Repo] = first;
        settings.Configuration.Repositories[worktree] = field switch
        {
            "identity" => first with { Identity = "local:other" },
            "scope" => first with { Scope = "other" },
            "provider" => first with { Provider = "azure-devops" },
            _ => first with { TicketPattern = @"(?<id>\d+)" }
        };
        settings.Save();
        var before = File.ReadAllText(settings.ConfigPath);
        var error = Assert.Throws<ArgumentException>(() => new RepositoryRegistry(settings).Get(worktree));
        Assert.Contains(field, error.Message);
        Assert.Contains("Reconcile", error.Message);
        Assert.Equal(before, File.ReadAllText(settings.ConfigPath));
        Assert.Empty(settings.Configuration.RepositoryContexts);
    }

    [Fact]
    public void LegacyExtractionPatternPreservesSignificantWhitespace()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        var settings = new LocalSettings(box.Data);
        const string pattern = @" (?<id>DEP-\d+) ";
        settings.Configuration.Repositories[box.Repo] = new("local:saved", "jira", "company", pattern);
        settings.Save();
        var repository = new RepositoryRegistry(settings).Get(box.Repo);
        Assert.Equal(pattern, repository.TicketPattern);
        var journal = new Journal(settings, repository);
        Assert.Throws<ArgumentException>(() => journal.Log(new("in_progress", Branch: "feature/DEP-12-repair")));
        Assert.Equal("DEP-12", Assert.Single(journal.Log(new("in_progress", Branch: "feature/ DEP-12 repair")).Entry.Tickets).Id);
    }

    [Fact]
    public void IndependentClonesDoNotImportTicketSettingsFromMatchingOrigin()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        box.Git("remote", "add", "origin", "https://example.invalid/org/repo.git");
        var settings = new LocalSettings(box.Data);
        var context = new RepositoryRegistry(settings).GetContext(box.Repo);
        context.Repository = context.Repository with { Provider = "jira", Scope = "company" };
        settings.Save();
        var otherPath = Path.Combine(box.Root, "clone");
        Directory.CreateDirectory(otherPath);
        GitRepository.Run(otherPath, "init");
        GitRepository.Run(otherPath, "remote", "add", "origin", "https://example.invalid/org/repo.git");
        var other = new RepositoryRegistry(settings).Get(otherPath);
        Assert.Equal(context.Repository.Identity, other.Identity);
        Assert.Null(other.Provider);
        Assert.Null(other.Scope);
    }

    [Fact]
    public void OldConfigurationLoadsAndSharedContextRoundTrips()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(box.Data);
        File.WriteAllText(Path.Combine(box.Data, "config.json"), """
            {"branchClassifications":{"bugs":"custom"},"repositories":{"old-path":{"identity":"saved"}}}
            """);
        var settings = new LocalSettings(box.Data);
        Assert.Equal("saved", settings.Configuration.Repositories["old-path"].Identity);
        Assert.Empty(settings.Configuration.RepositoryContexts);
        Assert.Equal("bug", settings.Configuration.BranchClassifications["bugfix"]);
        Assert.Equal("custom", settings.Configuration.BranchClassifications["bugs"]);
        settings.Configuration.RepositoryContexts["common"] = new(new("saved", "jira", "company"))
        {
            Instructions = new() { ["claude"] = false }, LegacyPaths = ["old-path"]
        };
        settings.Save();
        var context = Assert.Single(new LocalSettings(box.Data).Configuration.RepositoryContexts).Value;
        Assert.False(context.InstructionsEnabled("claude"));
        Assert.True(context.InstructionsEnabled("codex"));
        Assert.Equal("company", context.Repository.Scope);
        Assert.Equal(["old-path"], context.LegacyPaths);
    }

    [Fact]
    public void SharedContextsParticipateInEffectiveRepositoryOverrides()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts["other"] = new(new("other", "jira", "company"));
        var journal = new Journal(settings, new("bound"));
        var request = new LogRequest("in_progress", Branch: "feature/DEP-12", Repository: "other");
        Assert.Equal("company", Assert.Single(journal.Log(request).Entry.Tickets).Scope);
        settings.Configuration.RepositoryContexts["conflicting"] = new(new("other", "jira", "conflict"));
        Assert.Throws<ArgumentException>(() => journal.Log(request));
        Assert.Throws<ArgumentException>(() => journal.Log(request with { Repository = "unknown" }));
    }

    private static string CreateWorktree(TestWorkspace box)
    {
        box.Git("init");
        box.Git("-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "--allow-empty", "-m", "initial");
        var path = Path.Combine(box.Root, "worktree");
        box.Git("worktree", "add", "-b", "test-work", path);
        return path;
    }
}
