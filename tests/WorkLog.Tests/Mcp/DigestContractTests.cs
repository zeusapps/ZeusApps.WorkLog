using System.Text.Json;
using System.Collections.Concurrent;
using Json.Schema;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using WorkLog.Configuration;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Mcp;

public sealed class DigestContractTests
{
    private static string[] Properties(JsonElement schema) => schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order().ToArray();

    private static void AssertValid(JsonElement schema, CallToolResult response)
    {
        Assert.NotEqual(true, response.IsError);
        var validation = JsonSchema.Build(schema).Evaluate(response.StructuredContent!.Value, new EvaluationOptions { RequireFormatValidation = true });
        Assert.True(validation.IsValid, JsonSerializer.Serialize(validation));
    }

    [Fact]
    public async Task ToolListingExposesSaveDigestAndLeavesTheOtherContractsAdditive()
    {
        // Server side only; the tool contract is client-neutral, so this covers Claude Code and Codex alike.
        using var box = new TestWorkspace();
        box.Git("init");
        var settings = new LocalSettings(box.Data);
        settings.Configuration.Updates.Enabled = false;
        settings.Save();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(new()
        {
            Name = "digest-contract",
            Command = "dotnet",
            Arguments = [typeof(Program).Assembly.Location, "serve", "--repository", box.Repo, "--data-dir", box.Data]
        }), cancellationToken: timeout.Token);
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        Assert.Equal(["log_work", "query_work", "save_digest"], tools.Select(t => t.Name).Order());

        var save = Assert.Single(tools, t => t.Name == "save_digest").ProtocolTool;
        Assert.False(save.Annotations!.ReadOnlyHint);
        Assert.False(save.Annotations.DestructiveHint);
        Assert.True(save.Annotations.IdempotentHint);
        Assert.False(save.Annotations.OpenWorldHint);
        Assert.Equal(["entryIds", "text"], Properties(save.InputSchema));
        Assert.Equal(["entryIds", "text"], save.InputSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).Order());
        var ids = save.InputSchema.GetProperty("properties").GetProperty("entryIds");
        Assert.Equal("array", ids.GetProperty("type").GetString());
        Assert.Equal("string", ids.GetProperty("items").GetProperty("type").GetString());
        Assert.Equal("string", save.InputSchema.GetProperty("properties").GetProperty("text").GetProperty("type").GetString());
        Assert.Equal(["covers", "day", "item", "text"], Properties(save.OutputSchema!.Value));

        var log = Assert.Single(tools, t => t.Name == "log_work").ProtocolTool;
        Assert.False(log.Annotations!.ReadOnlyHint);
        Assert.False(log.Annotations.IdempotentHint);
        Assert.Equal(["entry"], Properties(log.InputSchema));
        var query = Assert.Single(tools, t => t.Name == "query_work").ProtocolTool;
        Assert.True(query.Annotations!.ReadOnlyHint);
        Assert.Equal(["period"], Properties(query.InputSchema));
        // The only log_work output change is the optional digestRequest.
        var output = log.OutputSchema!.Value;
        Assert.Equal(["created", "digestRequest", "entry", "updateHint"], Properties(output));
        Assert.DoesNotContain("digestRequest", output.GetProperty("required").EnumerateArray().Select(e => e.GetString()));

        // End to end over stdio: two observations, a request, a save, a stale repeat, and an unknown field.
        async Task<CallToolResult> Log(string at) => await client.CallToolAsync("log_work", new Dictionary<string, object?>
        {
            ["entry"] = new { status = "in_progress", change = "contract", occurredAt = at, summary = "step " + at }
        }, cancellationToken: timeout.Token);
        var first = await Log("2026-10-20T10:07:00Z");
        AssertValid(output, first);
        Assert.False(first.StructuredContent!.Value.TryGetProperty("digestRequest", out _));
        var second = await Log("2026-10-20T10:11:00Z");
        AssertValid(output, second);
        var request = second.StructuredContent!.Value.GetProperty("digestRequest");
        var entryIds = request.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToArray();
        Assert.Equal(2, entryIds.Length);
        var saved = await client.CallToolAsync("save_digest", new Dictionary<string, object?>
        {
            ["entryIds"] = entryIds,
            ["text"] = "Contract check passed."
        }, cancellationToken: timeout.Token);
        AssertValid(save.OutputSchema!.Value, saved);
        Assert.Equal("Contract check passed.", saved.StructuredContent!.Value.GetProperty("text").GetString());
        var third = await Log("2026-10-20T10:13:00Z");
        Assert.True(third.StructuredContent!.Value.TryGetProperty("digestRequest", out _));
        var stale = await client.CallToolAsync("save_digest", new Dictionary<string, object?>
        {
            ["entryIds"] = entryIds,
            ["text"] = "Stale repeat."
        }, cancellationToken: timeout.Token);
        Assert.True(stale.IsError);
        Assert.Contains("Stale", Assert.IsType<TextContentBlock>(Assert.Single(stale.Content)).Text);
        var unknown = await client.CallToolAsync("save_digest", new Dictionary<string, object?>
        {
            ["entryIds"] = entryIds,
            ["text"] = "x",
            ["extra"] = 1
        }, cancellationToken: timeout.Token);
        Assert.True(unknown.IsError);
        Assert.Contains("Unknown field 'extra'", Assert.IsType<TextContentBlock>(Assert.Single(unknown.Content)).Text);
    }

    [Theory]
    [InlineData("Claude Code protocol fixture")]
    [InlineData("Codex protocol fixture")]
    public async Task OptionalDigestFailureKeepsSavedLoggingAndStdoutProtocolUsable(string fixtureLabel)
    {
        // These equivalent labels exercise the shared MCP server protocol fixture; they do not run either native agent.
        Assert.False(string.IsNullOrWhiteSpace(fixtureLabel));
        using var box = new TestWorkspace();
        box.Git("init");
        Directory.CreateDirectory(Path.Combine(box.Data, "digests.db"));
        var settings = new LocalSettings(box.Data);
        settings.Configuration.Updates.Enabled = false;
        settings.Save();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var stderr = new ConcurrentQueue<string>();
        var transport = new StdioClientTransport(new()
        {
            Name = "optional-digest-" + fixtureLabel,
            Command = "dotnet",
            Arguments = [typeof(Program).Assembly.Location, "serve", "--repository", box.Repo, "--data-dir", box.Data],
            StandardErrorLines = stderr.Enqueue
        });
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);

        async Task<CallToolResult> Log(string at) => await client.CallToolAsync("log_work", new Dictionary<string, object?>
        {
            ["entry"] = new { status = "in_progress", change = "protocol-fixture", occurredAt = at, summary = "saved observation" }
        }, cancellationToken: timeout.Token);
        var first = await Log("2026-10-20T10:07:00Z");
        var second = await Log("2026-10-20T10:11:00Z");
        var third = await Log("2026-10-20T10:13:00Z");
        Assert.NotEqual(true, first.IsError);
        Assert.NotEqual(true, second.IsError);
        Assert.NotEqual(true, third.IsError);
        Assert.True(second.StructuredContent!.Value.GetProperty("created").GetBoolean());
        Assert.False(second.StructuredContent.Value.TryGetProperty("digestRequest", out _));
        Assert.True(third.StructuredContent!.Value.GetProperty("created").GetBoolean());
        Assert.False(third.StructuredContent.Value.TryGetProperty("digestRequest", out _));
        var digestDiagnostics = stderr.Where(line => line.Contains("Optional digest request skipped", StringComparison.Ordinal)).ToArray();
        Assert.Single(digestDiagnostics);
        Assert.Contains("storage-read", digestDiagnostics[0]);
        Assert.DoesNotContain("saved observation", string.Join("\n", stderr));
        var report = await client.CallToolAsync("query_work", new Dictionary<string, object?>
        {
            ["period"] = new { from = "2026-10-20T00:00:00Z", to = "2026-10-21T00:00:00Z", timezone = "UTC" }
        }, cancellationToken: timeout.Token);
        Assert.NotEqual(true, report.IsError);
        Assert.Equal(3, report.StructuredContent!.Value.GetProperty("entries").GetArrayLength());
        using var protocolText = JsonDocument.Parse(Assert.IsType<TextContentBlock>(Assert.Single(report.Content)).Text);
        Assert.True(JsonElement.DeepEquals(protocolText.RootElement, report.StructuredContent.Value));
    }
}
