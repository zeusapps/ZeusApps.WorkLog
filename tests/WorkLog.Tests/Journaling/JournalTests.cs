using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Reporting;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Journaling;

public sealed class JournalTests
{
    private static QueryRequest Period(string? status = null) => new("2026-10-20T00:00:00+02:00", "2026-10-25T00:00:00+02:00", "Europe/Warsaw", Status: status);

    [Fact]
    public void LogsPersistAndValidateWithoutPartialWrites()
    {
        using var box = new TestWorkspace();
        var context = new LocalSettings(box.Data);
        var journal = new Journal(context, new("repo"));
        var result = journal.Log(new("started", "feature-a", OccurredAt: "2026-10-20T09:00:00+02:00"));
        Assert.True(result.Created);
        Assert.Throws<ArgumentException>(() => journal.Log(new("invalid", "a")));
        Assert.Throws<ArgumentException>(() => journal.Log(new("done")));
        Assert.Throws<ArgumentException>(() => journal.Log(new("done", "a", OccurredAt: "2026-10-20T10:00:00")));
        var restarted = new Journal(new LocalSettings(box.Data));
        Assert.Equal(result.Entry, Assert.Single(restarted.Query(Period()).Entries) with
        {
            Tickets = result.Entry.Tickets
        });
        var ticketOnly = journal.Log(new("done", Tickets: [new("azure-devops", "org/project", "123")], OccurredAt: "2026-10-21T12:00:00Z"));
        Assert.Equal("123", Assert.Single(ticketOnly.Entry.Tickets).Id);
    }

    [Fact]
    public async Task ConcurrentStartsAndRetryKeysAreAtomic()
    {
        using var box = new TestWorkspace();
        var context = new LocalSettings(box.Data);
        var journal = new Journal(context, new("repo"));
        var request = new LogRequest("started", "a", OccurredAt: "2026-10-20T09:00:00Z");
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Task.Run(() =>
            new Journal(context, new("repo")).Log(request with
            {
                Agent = i % 2 == 0 ? "codex" : "claude",
                IdempotencyKey = "start-" + i
            }))));
        Assert.Single(results, r => r.Created);
        Assert.Single(results.Select(r => r.Entry.Id).Distinct());
        var progress = new LogRequest("in_progress", "a", IdempotencyKey: "retry");
        var first = journal.Log(progress);
        Assert.Equal(first.Entry.Id, journal.Log(progress).Entry.Id);
        Assert.Throws<ArgumentException>(() => journal.Log(progress with { Summary = "different" }));
        Assert.Equal(2, journal.Query(new("2000-01-01T00:00:00Z", "2100-01-01T00:00:00Z", "UTC")).Entries.Length);
    }

    [Theory]
    [InlineData("feature/DEP-1232-adding-links", "feature")]
    [InlineData("bugs/DEP-1233-missing-citation", "bug")]
    [InlineData("bugfix/DEP-1233-missing-citation", "bug")]
    [InlineData("BUGFIX/DEP-1233-missing-citation", "bug")]
    [InlineData("hotfix/DEP-2231-failing-requests", "bug")]
    [InlineData("FEATURE/DEP-1232", "feature")]
    [InlineData("main", "unclassified")]
    public void ClassificationAndTicketInference(string branch, string expected)
    {
        using var box = new TestWorkspace();
        var context = new LocalSettings(box.Data);
        var journal = new Journal(context, new("repo", "jira", "company.atlassian.net"));
        var entry = journal.Log(new("in_progress", "a", branch, OccurredAt: "2026-10-20T09:00:00Z")).Entry;
        Assert.Equal(expected, entry.Classification);
        if (branch != "main")
            Assert.Equal("jira", Assert.Single(entry.Tickets).Provider);
        context.Configuration.BranchClassifications["hotfix"] = "emergency";
        Assert.Equal(expected, Assert.Single(journal.Query(Period()).Entries).Classification);
        Assert.Equal("custom", journal.Log(new("done", "a", branch, Classification: "custom")).Entry.Classification);
        var unresolved = new Journal(context, new("other")).Log(new("in_progress", "b", "feature/DEP-12-test")).Entry;
        Assert.Equal("DEP-12", unresolved.ExtractedTicketId);
        Assert.Empty(unresolved.Tickets);
    }

    [Fact]
    public void BugfixDefaultUpgradesOldConfigurationWithoutChangingHistory()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        settings.Configuration.BranchClassifications.Remove("bugfix");
        // Simulate a legacy writer without invoking today's normalization on save.
        Directory.CreateDirectory(box.Data);
        File.WriteAllText(settings.ConfigPath, WorkLog.Infrastructure.Json.Encode(settings.Configuration));
        var old = new Journal(settings, new("repo"));
        var request = new LogRequest("in_progress", "repair", "bugfix/DEP-12", OccurredAt: "2026-10-20T09:00:00Z");
        var historical = old.Log(request).Entry;
        Assert.Equal("unclassified", historical.Classification);
        var beforeRead = File.ReadAllText(settings.ConfigPath);
        var upgradedSettings = new LocalSettings(box.Data);
        Assert.Equal(beforeRead, File.ReadAllText(settings.ConfigPath));
        var upgraded = new Journal(upgradedSettings, new("repo"));
        Assert.Equal("bug", upgraded.Log(request).Entry.Classification);
        Assert.Equal("unclassified", upgraded.Query(Period()).Entries.Single(entry => entry.Id == historical.Id).Classification);

        upgradedSettings.Configuration.BranchClassifications.Remove("bugfix");
        upgradedSettings.Configuration.BranchClassifications["BUGFIX"] = "maintenance";
        upgradedSettings.Save();
        var customSettings = new LocalSettings(box.Data);
        Assert.Single(customSettings.Configuration.BranchClassifications.Keys, key => key.Equals("bugfix", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("maintenance", new Journal(customSettings, new("repo")).Log(request).Entry.Classification);
    }
}
