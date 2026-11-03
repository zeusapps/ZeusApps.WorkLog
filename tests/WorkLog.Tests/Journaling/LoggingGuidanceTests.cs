using WorkLog.Journaling;
using Xunit;

namespace WorkLog.Tests.Journaling;

public sealed class LoggingGuidanceTests
{
    [Theory]
    [InlineData("proposals")]
    [InlineData("specs")]
    [InlineData("delegation")]
    [InlineData("review rounds")]
    [InlineData("test")]
    [InlineData("task counts")]
    [InlineData("commits")]
    [InlineData("hashes")]
    [InlineData("branches")]
    [InlineData("merges")]
    [InlineData("archiving")]
    [InlineData("syncing")]
    [InlineData("builds")]
    [InlineData("installation")]
    public void ContentRuleNamesEachExcludedWorkflowMechanic(string mechanic) =>
        Assert.Contains(mechanic, LoggingGuidance.ContentRule);
}
