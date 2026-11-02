using System.Text.Json;
using ModelContextProtocol.Protocol;
using WorkLog.Updates;
using Xunit;

namespace WorkLog.Tests.Updates;

public sealed class HealthProbeToolListingTests
{
    private static Tool ToolOf(string name, bool schema = true) => new()
    {
        Name = name,
        InputSchema = JsonDocument.Parse("{\"type\":\"object\"}").RootElement.Clone(),
        OutputSchema = schema ? JsonDocument.Parse("{\"type\":\"object\"}").RootElement.Clone() : null
    };

    [Fact]
    public void AcceptsJournalToolsAlone() =>
        PayloadInstaller.ValidateAdvertisedTools([ToolOf("log_work"), ToolOf("query_work")]);

    [Fact]
    public void AcceptsAdditionalToolWithOutputSchema() =>
        PayloadInstaller.ValidateAdvertisedTools([ToolOf("log_work"), ToolOf("query_work"), ToolOf("save_digest")]);

    [Theory]
    [InlineData("log_work")]
    [InlineData("query_work")]
    public void RejectsMissingJournalTool(string missing) =>
        Assert.Throws<InvalidOperationException>(() => PayloadInstaller.ValidateAdvertisedTools(
            [.. new[] { "log_work", "query_work", "save_digest" }.Where(name => name != missing).Select(name => ToolOf(name))]));

    [Fact]
    public void RejectsToolWithoutOutputSchema() =>
        Assert.Throws<InvalidOperationException>(() => PayloadInstaller.ValidateAdvertisedTools(
            [ToolOf("log_work"), ToolOf("query_work"), ToolOf("save_digest", schema: false)]));
}
