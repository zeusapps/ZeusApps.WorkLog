using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using WorkLog.Cli;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Mcp;

public sealed class McpTests
{
    [Fact]
    public async Task ServerStopsAfterClientClosesStandardInput()
    {
        using var workspace = new TestWorkspace();
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { typeof(Program).Assembly.Location, "serve", "--data-dir", workspace.Data })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await process.StandardInput.WriteLineAsync("""
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"lifecycle-test","version":"1.0"}}}
                """);
            var response = await process.StandardOutput.ReadLineAsync(timeout.Token);
            using var initialization = JsonDocument.Parse(response!);
            Assert.Equal(1, initialization.RootElement.GetProperty("id").GetInt32());
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
            process.StandardInput.Close();
            var remaining = process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(await remaining));
            await errors;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [Fact]
    public async Task StdioDiscoveryLoggingQueriesAndErrors()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var assembly = typeof(Program).Assembly.Location;
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(new()
        {
            Name = "worklog-test",
            Command = "dotnet",
            Arguments = [assembly, "serve", "--repository", box.Repo, "--data-dir", box.Data]
        }), cancellationToken: timeout.Token);
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        Assert.Equal(new[] { "log_work", "query_work" }, tools.Select(t => t.Name).Order());
        Assert.Equal("worklog", client.ServerInfo.Name);
        Assert.Equal(ApplicationInfo.Version, client.ServerInfo.Version);
        foreach (var tool in tools)
        {
            Assert.NotNull(tool.ProtocolTool.OutputSchema);
            Assert.Equal("object", tool.ProtocolTool.OutputSchema.Value.GetProperty("type").GetString());
            Assert.False(tool.ProtocolTool.Annotations!.OpenWorldHint);
            Assert.False(tool.ProtocolTool.Annotations.DestructiveHint);
        }
        var loggingTool = Assert.Single(tools, tool => tool.Name == "log_work");
        Assert.False(loggingTool.ProtocolTool.Annotations!.ReadOnlyHint);
        // Without an optional idempotency key, repeated progress entries are intentionally distinct.
        Assert.False(loggingTool.ProtocolTool.Annotations.IdempotentHint);
        Assert.True(Assert.Single(tools, tool => tool.Name == "query_work").ProtocolTool.Annotations!.ReadOnlyHint);
        var logged = await client.CallToolAsync("log_work", new Dictionary<string, object?>
        {
            ["entry"] = new { status = "started", change = "mcp-test", branch = "hotfix/DEP-123-test", occurredAt = "2026-10-20T09:00:00Z", idempotencyKey = "mcp-test" }
        }, cancellationToken: timeout.Token);
        Assert.NotEqual(true, logged.IsError);
        Assert.NotNull(logged.StructuredContent);
        Assert.True(logged.StructuredContent.Value.GetProperty("created").GetBoolean());
        var report = await client.CallToolAsync("query_work", new Dictionary<string, object?>
        {
            ["period"] = new { from = "2026-10-20T00:00:00Z", to = "2026-10-21T00:00:00Z", timezone = "Europe/Warsaw" }
        }, cancellationToken: timeout.Token);
        Assert.NotEqual(true, report.IsError);
        Assert.NotNull(report.StructuredContent);
        Assert.Single(report.StructuredContent.Value.GetProperty("entries").EnumerateArray());
        var text = Assert.IsType<TextContentBlock>(Assert.Single(report.Content)).Text;
        using var json = JsonDocument.Parse(text);
        Assert.Single(json.RootElement.GetProperty("entries").EnumerateArray());
        Assert.True(JsonElement.DeepEquals(json.RootElement, report.StructuredContent.Value));
        var invalid = await client.CallToolAsync("log_work", new Dictionary<string, object?>
        {
            ["entry"] = new { status = "invalid", change = "test" }
        }, cancellationToken: timeout.Token);
        Assert.True(invalid.IsError);
        Assert.Contains("Status must be", Assert.IsType<TextContentBlock>(Assert.Single(invalid.Content)).Text);
        // A tool execution failure does not terminate the MCP connection.
        var afterError = await client.CallToolAsync("query_work", new Dictionary<string, object?>
        {
            ["period"] = new { from = "2026-10-20T00:00:00Z", to = "2026-10-21T00:00:00Z", timezone = "Europe/Warsaw" }
        }, cancellationToken: timeout.Token);
        Assert.NotEqual(true, afterError.IsError);
        Assert.Single(afterError.StructuredContent!.Value.GetProperty("entries").EnumerateArray());
    }
}
