using System.Text.Json.Nodes;
using WorkLog.Configuration;
using WorkLog.Integration;
using WorkLog.Journaling;
using WorkLog.Repositories;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Configuration;

public sealed class SettingsConcurrencyTests
{
    [Fact]
    public void StaleKnownEditsPreserveNestedUnknownFieldsAndIndependentChanges()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(box.Data);
        File.WriteAllText(Path.Combine(box.Data, "config.json"), """
            {"future":{"value":42},"updates":{"enabled":true,"future":[1,2]},
             "repositoryContexts":{"one":{"repository":{"identity":"repo","future":"keep"},"future":{"a":true}}}}
            """);
        var first = new LocalSettings(box.Data);
        var second = new LocalSettings(box.Data);
        first.Configuration.Updates.Enabled = false;
        first.Save();
        second.Configuration.RepositoryContexts["one"].Repository = new("repo", "jira", "scope");
        second.Configuration.RepositoryContexts["two"] = new(new("two"));
        second.Save();
        var saved = JsonNode.Parse(File.ReadAllText(second.ConfigPath))!;
        Assert.False(saved["updates"]!["enabled"]!.GetValue<bool>());
        Assert.Equal(42, saved["future"]!["value"]!.GetValue<int>());
        Assert.Equal(2, saved["updates"]!["future"]!.AsArray().Count);
        Assert.Equal("keep", saved["repositoryContexts"]!["one"]!["repository"]!["future"]!.GetValue<string>());
        Assert.True(saved["repositoryContexts"]!["one"]!["future"]!["a"]!.GetValue<bool>());
        Assert.Equal(2, new LocalSettings(box.Data).Configuration.RepositoryContexts.Count);
    }

    [Fact]
    public void ConflictingKnownEditsFailWithoutDroppingNewerStateAndEqualRetrySucceeds()
    {
        using var box = new TestWorkspace();
        var initial = new LocalSettings(box.Data);
        initial.Save();
        var first = new LocalSettings(box.Data);
        var second = new LocalSettings(box.Data);
        first.Configuration.BranchClassifications["feature"] = "first";
        second.Configuration.BranchClassifications["feature"] = "second";
        first.Save();
        var saved = File.ReadAllText(initial.ConfigPath);
        Assert.Contains("conflict", Assert.Throws<IOException>(second.Save).Message);
        Assert.Equal(saved, File.ReadAllText(initial.ConfigPath));
        second.Configuration.BranchClassifications["feature"] = "first";
        second.Save();
        Assert.Equal("first", new LocalSettings(box.Data).Configuration.BranchClassifications["feature"]);
    }

    [Fact]
    public async Task ConcurrentFreshContextsConvergeAndIndependentSavesSurvive()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        var settings = Enumerable.Range(0, 6).Select(_ => new LocalSettings(box.Data)).ToArray();
        var identities = await Task.WhenAll(settings.Select(value => Task.Run(() => new RepositoryRegistry(value).Get(box.Repo).Identity)));
        Assert.Single(identities.Distinct());
        var writers = Enumerable.Range(0, 6).Select(_ => new LocalSettings(box.Data)).ToArray();
        await Task.WhenAll(writers.Select((value, index) => Task.Run(() =>
        {
            value.Configuration.RepositoryContexts["independent-" + index] = new(new("repo-" + index));
            value.Save();
        })));
        Assert.Equal(7, new LocalSettings(box.Data).Configuration.RepositoryContexts.Count);
    }

    [Fact]
    public void InitializerPreservesUnknownFieldsAndDryRunDoesNotCreateData()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        var settings = new LocalSettings(box.Data);
        new RepositoryInitializer(settings, box.Home, false).Run(box.Repo, "claude", dryRun: true);
        Assert.False(Directory.Exists(box.Data));
        Directory.CreateDirectory(box.Data);
        File.WriteAllText(settings.ConfigPath, """
            {"future":{"keep":true},"updates":{"enabled":false,"newPolicy":"keep"},
             "branchClassifications":{"feature":"feature","bugs":"bug","hotfix":"bug"}}
            """);
        new RepositoryInitializer(settings, box.Home, false).Run(box.Repo, "claude", instructions: false);
        Assert.Equal("bug", settings.Configuration.BranchClassifications["bugfix"]);
        Assert.Equal("bug", new Journal(settings, new("repo")).Log(new("in_progress", "repair", "bugfix/DEP-42")).Entry.Classification);
        var document = JsonNode.Parse(File.ReadAllText(settings.ConfigPath))!;
        Assert.True(document["future"]!["keep"]!.GetValue<bool>());
        Assert.Equal("keep", document["updates"]!["newPolicy"]!.GetValue<string>());
        Assert.False(new RepositoryRegistry(new LocalSettings(box.Data)).GetContext(box.Repo).InstructionsEnabled("claude"));
    }
}
