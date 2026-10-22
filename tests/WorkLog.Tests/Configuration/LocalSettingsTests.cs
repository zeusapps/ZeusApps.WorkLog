using System.Text.Json.Nodes;
using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Repositories;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Configuration;

public sealed class LocalSettingsTests
{
    [Theory]
    [InlineData(null, "bug")]
    [InlineData("maintenance", "maintenance")]
    [InlineData("unclassified", "unclassified")]
    public void SaveRetainsNormalizedClassificationAndCustomOverrides(string? custom, string expected)
    {
        using var box = new TestWorkspace();
        var settings = LegacySettings(box, custom);
        settings.Configuration.Updates.Enabled = false;

        settings.Save();

        AssertClassification(settings, new("repo"), expected);
        AssertClassification(new LocalSettings(box.Data), new("repo"), expected);
        var saved = JsonNode.Parse(File.ReadAllText(settings.ConfigPath))!;
        Assert.False(saved["updates"]!["enabled"]!.GetValue<bool>());
        Assert.True(saved["future"]!["keep"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(false, null, "bug")]
    [InlineData(true, null, "bug")]
    [InlineData(false, "maintenance", "maintenance")]
    [InlineData(false, "unclassified", "unclassified")]
    public void RepositoryResolutionRetainsNormalizedClassificationAndCustomOverrides(bool existing, string? custom, string expected)
    {
        using var box = new TestWorkspace();
        box.Git("init");
        if (existing)
            _ = new RepositoryRegistry(new LocalSettings(box.Data)).Get(box.Repo);
        var settings = LegacySettings(box, custom);
        var before = File.ReadAllText(settings.ConfigPath);

        var repository = new RepositoryRegistry(settings).Get(box.Repo);

        AssertClassification(settings, repository, expected);
        if (existing)
            Assert.Equal(before, File.ReadAllText(settings.ConfigPath));
        AssertClassification(new LocalSettings(box.Data), repository, expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SettingsCopiesRetainNormalizationWithoutWriting(bool fresh)
    {
        using var box = new TestWorkspace();
        var settings = LegacySettings(box);
        var before = File.ReadAllText(settings.ConfigPath);

        var copy = fresh ? settings.FreshCopy() : settings.Copy();

        AssertClassification(copy, new("repo"), "bug");
        Assert.Equal(before, File.ReadAllText(settings.ConfigPath));
    }

    private static LocalSettings LegacySettings(TestWorkspace box, string? custom = null)
    {
        Directory.CreateDirectory(box.Data);
        var path = Path.Combine(box.Data, "config.json");
        var document = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))!.AsObject() : new JsonObject();
        document["branchClassifications"] = new JsonObject { ["feature"] = "feature", ["bugs"] = "bug", ["hotfix"] = "bug" };
        if (custom is not null)
            document["branchClassifications"]!["BUGFIX"] = custom;
        document["future"] = new JsonObject { ["keep"] = true };
        var legacy = document.ToJsonString();
        File.WriteAllText(path, legacy);
        var settings = new LocalSettings(box.Data);
        Assert.Equal(legacy, File.ReadAllText(path));
        return settings;
    }

    private static void AssertClassification(LocalSettings settings, RepositoryConfig repository, string expected)
    {
        var mapping = Assert.Single(settings.Configuration.BranchClassifications,
            pair => pair.Key.Equals("bugfix", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expected, mapping.Value);
        var entry = new Journal(settings, repository).Log(new("in_progress", "repair", "bugfix/DEP-42")).Entry;
        Assert.Equal(expected, entry.Classification);
    }
}
