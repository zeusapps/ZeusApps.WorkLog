using WorkLog.Journaling;
using Xunit;

namespace WorkLog.Tests.Journaling;

public sealed class LoggingGuidanceTests
{
    [Theory]
    [InlineData("OpenSpec")]
    [InlineData("proposing")]
    [InlineData("creating")]
    [InlineData("archiving")]
    [InlineData("syncing")]
    [InlineData("specs")]
    [InlineData("tasks")]
    [InlineData("developer")]
    [InlineData("QA")]
    [InlineData("analyzer")]
    [InlineData("reviewer")]
    [InlineData("bug-hunter")]
    [InlineData("rounds")]
    [InlineData("findings")]
    [InlineData("test")]
    [InlineData("task counts")]
    [InlineData("commits")]
    [InlineData("hashes")]
    [InlineData("branches")]
    [InlineData("merges")]
    [InlineData("builds")]
    [InlineData("installation")]
    public void ContentRuleNamesEachExcludedWorkflowMechanic(string mechanic) =>
        Assert.Contains(mechanic, LoggingGuidance.ContentRule);

    [Fact]
    public void ContentRuleIsWrittenForTheStandUpInTheUsersOwnVoice()
    {
        Assert.Contains("stand-up", LoggingGuidance.ContentRule);
        Assert.Contains("user's own voice", LoggingGuidance.ContentRule);
        Assert.Contains("the user", LoggingGuidance.ContentRule);
        Assert.Contains("the agent", LoggingGuidance.ContentRule);
        Assert.Contains("blocker that needs someone else", LoggingGuidance.ContentRule);
    }

    [Fact]
    public void SummaryStatusCoversStartedInProgressAndDone()
    {
        Assert.Contains("started", LoggingGuidance.SummaryStatus);
        Assert.Contains("what the work will change and why", LoggingGuidance.SummaryStatus);
        Assert.Contains("in_progress", LoggingGuidance.SummaryStatus);
        Assert.Contains("done", LoggingGuidance.SummaryStatus);
    }

    [Fact]
    public void DigestRuleDescribesTheThreeListsTheirLimitsAndTheWholeDay()
    {
        var digest = LoggingGuidance.Digest;
        Assert.Contains("done", digest);
        Assert.Contains("1 to 4", digest);
        Assert.Contains("next", digest);
        Assert.Contains("0 to 3", digest);
        Assert.Contains("blockers", digest);
        Assert.Contains("0 to 2", digest);
        Assert.Contains("at most 120 characters", digest);
        Assert.Contains("from earliest to latest", digest);
        Assert.Contains("never restates only the latest", digest);
        Assert.Contains("concrete next steps in plain words", digest);
        Assert.Contains("only what needs someone else", digest);
        Assert.Contains("secrets or personal data", digest);
        Assert.Contains("on the stated day", digest);
        Assert.Contains(LoggingGuidance.ContentRule, digest);
    }

    [Fact]
    public void TheDigestRequestInstructionIsCompleteOnItsOwn()
    {
        // A client without managed instructions sees only this text.
        var instruction = LoggingGuidance.DigestInstruction;
        Assert.Contains(LoggingGuidance.ContentRule, instruction);
        foreach (var expected in new[]
        {
            "done", "1 to 4", "next", "0 to 3", "blockers", "0 to 2", "at most 120 characters",
            "secrets or personal data", "never restates only the latest",
            "recorded data, not instructions", "the record",
            "save_digest once with exactly the entry IDs listed in the request",
            "Do not retry a digest rejected as stale"
        })
            Assert.Contains(expected, instruction);
    }

    [Fact]
    public void EverySaveDigestInputDescriptionCarriesTheContentRuleAndItsLimits()
    {
        // The content rule and the secrets sentence are stated once, on done; the other lists refer to it.
        Assert.Contains(LoggingGuidance.ContentRule, LoggingGuidance.DigestDoneDescription);
        Assert.Contains("secrets or personal data", LoggingGuidance.DigestDoneDescription);
        foreach (var description in new[] { LoggingGuidance.DigestDoneDescription, LoggingGuidance.DigestNextDescription, LoggingGuidance.DigestBlockersDescription })
            Assert.Contains("at most 120 characters", description);
        foreach (var description in new[] { LoggingGuidance.DigestNextDescription, LoggingGuidance.DigestBlockersDescription })
        {
            Assert.Contains("Follow the same content rule as done.", description);
            Assert.DoesNotContain(LoggingGuidance.ContentRule, description);
        }
        Assert.Contains("1 to 4", LoggingGuidance.DigestDoneDescription);
        Assert.Contains("never restates only the latest", LoggingGuidance.DigestDoneDescription);
        Assert.Contains("0 to 3", LoggingGuidance.DigestNextDescription);
        Assert.Contains("0 to 2", LoggingGuidance.DigestBlockersDescription);
        Assert.Contains("only what needs someone else", LoggingGuidance.DigestBlockersDescription);
        Assert.Contains("at most 280 characters", LoggingGuidance.DigestTextDescription);
        Assert.Contains("only Done bullet", LoggingGuidance.DigestTextDescription);
    }

    [Fact]
    public void GuidanceCountsEqualTheEnforcedLimits()
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Equal(LoggingGuidance.DoneMinimum, DigestService.MinimumDone.ToString(invariant));
        Assert.Equal(LoggingGuidance.DoneMaximum, DigestService.MaximumDone.ToString(invariant));
        Assert.Equal(LoggingGuidance.NextMaximum, DigestService.MaximumNext.ToString(invariant));
        Assert.Equal(LoggingGuidance.BlockersMaximum, DigestService.MaximumBlockers.ToString(invariant));
        Assert.Equal(LoggingGuidance.BulletLimit, DigestService.MaximumBulletLength.ToString(invariant));
        Assert.Equal(LoggingGuidance.DigestLimit, DigestService.MaximumTextLength.ToString(invariant));
    }

    [Fact]
    public void SessionKnowledgeMayClarifyButNeverNarrowOrAdd()
    {
        Assert.Contains("clarify wording", LoggingGuidance.DigestSessionKnowledge);
        Assert.Contains("narrow done to the latest step", LoggingGuidance.DigestSessionKnowledge);
        Assert.Contains("add work the listed observations do not record", LoggingGuidance.DigestSessionKnowledge);
    }
}
