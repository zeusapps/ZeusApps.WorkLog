using System.Diagnostics;
using System.Text.Json;
using Json.Schema;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using WorkLog.Cli;
using WorkLog.Configuration;
using WorkLog.Infrastructure;
using WorkLog.Updates;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Mcp;

public sealed class McpTests
{
    [Fact]
    public async Task ServerStopsAfterClientClosesStandardInput()
    {
        using var workspace = new TestWorkspace();
        var settings = new LocalSettings(workspace.Data);
        settings.Configuration.Updates.Enabled = false;
        settings.Save();
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
        var cachedRelease = new ReleaseInfo("999.0.0", ReleaseInfo.ReleasesUrl + "/tag/v999.0.0",
            ReleaseInfo.DownloadUrl("999.0.0"), ReleaseInfo.DownloadUrl("999.0.0") + ".sha256");
        AtomicFile.Write(Path.Combine(box.Data, "updates", "release-cache.json"),
            WorkLog.Infrastructure.Json.Encode(new UpdateCache(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, cachedRelease)));
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
        var loggingSchema = loggingTool.ProtocolTool.OutputSchema!.Value;
        AssertDescription(loggingSchema, "created", "False when a retry");
        var entrySchema = ResolveSchema(loggingSchema.GetProperty("properties").GetProperty("entry"), loggingSchema);
        AssertDescription(entrySchema, "occurredAt", "Used for period filtering");
        AssertDescription(entrySchema, "recordedAt", "When the server saved");
        AssertDescription(entrySchema, "classification", "Later branch-mapping changes");
        var ticketSchema = ResolveSchema(entrySchema.GetProperty("properties").GetProperty("tickets").GetProperty("items"), loggingSchema);
        AssertDescription(ticketSchema, "scope", "Provider namespace");

        var reportSchema = Assert.Single(tools, tool => tool.Name == "query_work").ProtocolTool.OutputSchema!.Value;
        AssertDescription(reportSchema, "from", "Inclusive lower bound");
        AssertDescription(reportSchema, "to", "Exclusive upper bound");
        AssertDescription(reportSchema, "entries", "do not count both views");
        var dailySchema = ResolveSchema(reportSchema.GetProperty("properties").GetProperty("days").GetProperty("items"), reportSchema);
        AssertDescription(dailySchema, "date", "report timezone");
        AssertDescription(dailySchema, "observations", "do not count this view again");
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
        AssertValidOutput(loggingSchema, logged);
        var replayed = await client.CallToolAsync("log_work", new Dictionary<string, object?>
        {
            ["entry"] = new { status = "started", change = "mcp-test", branch = "hotfix/DEP-123-test", occurredAt = "2026-10-20T09:00:00Z", idempotencyKey = "mcp-test" }
        }, cancellationToken: timeout.Token);
        AssertValidOutput(loggingSchema, replayed);
        Assert.False(replayed.StructuredContent!.Value.GetProperty("created").GetBoolean());
        Assert.True(logged.StructuredContent.Value.GetProperty("created").GetBoolean());
        Assert.Equal("999.0.0", logged.StructuredContent.Value.GetProperty("updateHint").GetProperty("availableVersion").GetString());
        var report = await client.CallToolAsync("query_work", new Dictionary<string, object?>
        {
            ["period"] = new { from = "2026-10-20T00:00:00Z", to = "2026-10-21T00:00:00Z", timezone = "Europe/Warsaw" }
        }, cancellationToken: timeout.Token);
        Assert.NotEqual(true, report.IsError);
        Assert.NotNull(report.StructuredContent);
        AssertValidOutput(reportSchema, report);
        Assert.Single(report.StructuredContent.Value.GetProperty("entries").EnumerateArray());
        Assert.Equal("worklog update", report.StructuredContent.Value.GetProperty("updateHint").GetProperty("command").GetString());
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
        AssertValidOutput(reportSchema, afterError);
        foreach (var view in new[] { "compact", "flat", "grouped", "legacy" })
        {
            var projected = await client.CallToolAsync("query_work", new Dictionary<string, object?>
            {
                ["period"] = new { from = "2026-10-20T00:00:00Z", to = "2026-10-21T00:00:00Z", timezone = "UTC", view }
            }, cancellationToken: timeout.Token);
            AssertValidOutput(reportSchema, projected);
        }
        var empty = await client.CallToolAsync("query_work", new Dictionary<string, object?>
        {
            ["period"] = new { from = "2020-01-01T00:00:00Z", to = "2020-01-02T00:00:00Z", timezone = "UTC" }
        }, cancellationToken: timeout.Token);
        AssertValidOutput(reportSchema, empty);
        var full = await client.CallToolAsync("log_work", new Dictionary<string, object?>
        {
            ["entry"] = new { status = "done", change = "full", branch = "feature/DEP-14", summary = "Completed implementation.", agent = "codex", sessionId = "test-session", tickets = new[] { new { provider = "jira", scope = "example.test", id = "DEP-14", url = "https://example.test/DEP-14" } }, occurredAt = "2026-10-20T10:00:00Z" }
        }, cancellationToken: timeout.Token);
        AssertValidOutput(loggingSchema, full);
        string? cursor = null;
        var pageCount = 0;
        do
        {
            var page = await client.CallToolAsync("query_work", new Dictionary<string, object?>
            {
                ["period"] = new { from = "2026-10-20T00:00:00Z", to = "2026-10-21T00:00:00Z", timezone = "UTC", pageSize = 1, cursor }
            }, cancellationToken: timeout.Token);
            AssertValidOutput(reportSchema, page);
            cursor = page.StructuredContent!.Value.GetProperty("nextCursor").GetString();
            Assert.True(++pageCount <= 2);
        } while (cursor is not null);
        Assert.Equal(2, pageCount);
        var typo = await client.CallToolAsync("log_work", new Dictionary<string, object?>
        {
            ["entry"] = new { status = "done", change = "typo", sumary = "misspelled" }
        }, cancellationToken: timeout.Token);
        Assert.True(typo.IsError);
        Assert.Contains("entry.sumary", Assert.IsType<TextContentBlock>(Assert.Single(typo.Content)).Text);
        var nestedTypo = await client.CallToolAsync("log_work", new Dictionary<string, object?>
        {
            ["entry"] = new { status = "done", change = "typo", tickets = new[] { new { provider = "jira", scope = "example.test", id = "DEP-14", urll = "https://example.test/DEP-14" } } }
        }, cancellationToken: timeout.Token);
        Assert.True(nestedTypo.IsError);
        Assert.Contains("entry.tickets[0].urll", Assert.IsType<TextContentBlock>(Assert.Single(nestedTypo.Content)).Text);
        var periodTypo = await client.CallToolAsync("query_work", new Dictionary<string, object?>
        {
            ["period"] = new { from = "2026-10-20T00:00:00Z", to = "2026-10-21T00:00:00Z", timezone = "UTC", pagesizee = 1 }
        }, cancellationToken: timeout.Token);
        Assert.True(periodTypo.IsError);
        Assert.Contains("period.pagesizee", Assert.IsType<TextContentBlock>(Assert.Single(periodTypo.Content)).Text);
        var afterTypos = await client.CallToolAsync("query_work", new Dictionary<string, object?>
        {
            ["period"] = new { from = "2026-10-20T00:00:00Z", to = "2026-10-21T00:00:00Z", timezone = "UTC" }
        }, cancellationToken: timeout.Token);
        AssertValidOutput(reportSchema, afterTypos);
        Assert.Equal(2, afterTypos.StructuredContent!.Value.GetProperty("entries").GetArrayLength());
        var nullTicket = await client.CallToolAsync("log_work", new Dictionary<string, object?>
        {
            ["entry"] = new { status = "done", tickets = new object?[] { null } }
        }, cancellationToken: timeout.Token);
        Assert.True(nullTicket.IsError);
        Assert.Contains("entry.tickets[0]", Assert.IsType<TextContentBlock>(Assert.Single(nullTicket.Content)).Text);
        using var invalidOutput = JsonDocument.Parse("{}");
        Assert.False(JsonSchema.Build(loggingSchema).Evaluate(invalidOutput.RootElement).IsValid);
    }

    [Fact]
    public async Task TicketGroupingIsDiscoverableValidatedAndBoundedAcrossViews()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        var settings = new LocalSettings(box.Data);
        settings.Configuration.Updates.Enabled = false;
        settings.Save();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(new()
        {
            Name = "grouping-test",
            Command = "dotnet",
            Arguments = [typeof(Program).Assembly.Location, "serve", "--repository", box.Repo, "--data-dir", box.Data]
        }), cancellationToken: timeout.Token);
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        var tool = Assert.Single(tools, item => item.Name == "query_work").ProtocolTool;
        var input = tool.InputSchema;
        var periodSchema = ResolveSchema(input.GetProperty("properties").GetProperty("period"), input);
        AssertDescription(periodSchema, "groupBy", "complete qualified ticket set");
        AssertDescription(periodSchema, "groupBy", "ticket (default)");
        Assert.DoesNotContain(periodSchema.GetProperty("required").EnumerateArray(), item => item.GetString() == "groupBy");
        var schema = tool.OutputSchema!.Value;
        var dailySchema = ResolveSchema(schema.GetProperty("properties").GetProperty("days").GetProperty("items"), schema);
        AssertDescription(dailySchema, "workIdentity", "period.groupBy");
        var original = new Dictionary<string, JsonElement>();
        for (var index = 0; index < 6; index++)
        {
            var logged = await client.CallToolAsync("log_work", new Dictionary<string, object?>
            {
                ["entry"] = new
                {
                    status = "in_progress",
                    change = index % 2 == 0 ? "repair-links" : null,
                    tickets = new[] { new { provider = "jira", scope = "company", id = "DEP-12" } },
                    occurredAt = "2026-10-20T09:00:00Z",
                    summary = new string('Ж', 2000)
                }
            }, cancellationToken: timeout.Token);
            AssertValidOutput(Assert.Single(tools, item => item.Name == "log_work").ProtocolTool.OutputSchema!.Value, logged);
            var entry = logged.StructuredContent!.Value.GetProperty("entry");
            original.Add(entry.GetProperty("id").GetString()!, entry.Clone());
        }

        Dictionary<string, object?> Period() => new()
        {
            ["from"] = "2026-10-20T00:00:00Z",
            ["to"] = "2026-10-21T00:00:00Z",
            ["timezone"] = "UTC"
        };
        async Task<CallToolResult> Query(Dictionary<string, object?> period) => await client.CallToolAsync("query_work",
            new Dictionary<string, object?> { ["period"] = period }, cancellationToken: timeout.Token);

        foreach (var groupBy in new string?[] { null, "change", "ticket" })
            foreach (var view in new[] { "compact", "flat", "grouped", "legacy" })
                foreach (var includeSummaries in new[] { true, false })
                {
                    var request = Period();
                    if (groupBy is not null)
                        request["groupBy"] = groupBy;
                    request["view"] = view;
                    request["includeSummaries"] = includeSummaries;
                    request["pageSize"] = 100;
                    var ids = new List<string>();
                    var groupIds = new List<string>();
                    var identities = new HashSet<string>();
                    while (true)
                    {
                        var result = await Query(request);
                        AssertValidOutput(schema, result);
                        var report = result.StructuredContent!.Value;
                        Assert.True(JsonSerializer.SerializeToUtf8Bytes(report).Length <= 64 * 1024);
                        var days = report.GetProperty("days").EnumerateArray().ToArray();
                        var entries = view == "grouped"
                            ? days.SelectMany(day => day.GetProperty("observations").EnumerateArray())
                            : report.GetProperty("entries").EnumerateArray().ToArray().AsEnumerable();
                        foreach (var entry in entries)
                        {
                            var id = entry.GetProperty("id").GetString()!;
                            ids.Add(id);
                            Assert.True(JsonElement.DeepEquals(original[id].GetProperty("change"), entry.GetProperty("change")));
                            Assert.True(JsonElement.DeepEquals(original[id].GetProperty("tickets"), entry.GetProperty("tickets")));
                            if (includeSummaries)
                                Assert.Equal(original[id].GetProperty("summary").GetString(), entry.GetProperty("summary").GetString());
                            else
                                Assert.Equal(JsonValueKind.Null, entry.GetProperty("summary").ValueKind);
                        }
                        foreach (var day in days)
                        {
                            identities.Add(day.GetProperty("workIdentity").GetString()!);
                            groupIds.AddRange(view == "compact"
                                ? day.GetProperty("entryIds").EnumerateArray().Select(item => item.GetString()!)
                                : day.GetProperty("observations").EnumerateArray().Select(item => item.GetProperty("id").GetString()!));
                        }
                        if (!report.GetProperty("hasMore").GetBoolean())
                            break;
                        request["cursor"] = report.GetProperty("nextCursor").GetString();
                        Assert.True(ids.Count < 6);
                    }
                    Assert.Equal(6, ids.Count);
                    Assert.True(original.Keys.ToHashSet().SetEquals(ids));
                    Assert.Equal(view == "flat" ? 0 : groupBy == "change" ? 2 : 1, identities.Count);
                    if (view != "flat")
                        Assert.Equal(ids.Order(StringComparer.Ordinal), groupIds.Order(StringComparer.Ordinal));
                }

        foreach (var invalid in new object?[] { null, "", " ", "tickets", "Ticket" })
        {
            var request = Period();
            request["groupBy"] = invalid;
            var rejected = await Query(request);
            Assert.True(rejected.IsError);
            Assert.Contains("period.groupBy", Assert.IsType<TextContentBlock>(Assert.Single(rejected.Content)).Text);
        }
        var typoRequest = Period();
        typoRequest["groupByy"] = "ticket";
        var typo = await Query(typoRequest);
        Assert.True(typo.IsError);
        Assert.Contains("period.groupByy", Assert.IsType<TextContentBlock>(Assert.Single(typo.Content)).Text);
        var afterRequest = Period();
        afterRequest["includeSummaries"] = false;
        var after = await Query(afterRequest);
        AssertValidOutput(schema, after);
        Assert.Equal(6, after.StructuredContent!.Value.GetProperty("entries").GetArrayLength());
        afterRequest["pageSize"] = 1;
        var first = await Query(afterRequest);
        AssertValidOutput(schema, first);
        afterRequest["cursor"] = first.StructuredContent!.Value.GetProperty("nextCursor").GetString();
        var omittedContinuation = await Query(afterRequest);
        AssertValidOutput(schema, omittedContinuation);
        afterRequest["groupBy"] = "ticket";
        var explicitContinuation = await Query(afterRequest);
        AssertValidOutput(schema, explicitContinuation);
        Assert.True(JsonElement.DeepEquals(omittedContinuation.StructuredContent!.Value, explicitContinuation.StructuredContent!.Value));
        afterRequest["groupBy"] = "change";
        var switchedMode = await Query(afterRequest);
        Assert.True(switchedMode.IsError);
        Assert.Contains("period.cursor", Assert.IsType<TextContentBlock>(Assert.Single(switchedMode.Content)).Text);
    }

    private static void AssertValidOutput(JsonElement schema, CallToolResult response)
    {
        Assert.NotEqual(true, response.IsError);
        var content = response.StructuredContent!.Value;
        var validation = JsonSchema.Build(schema).Evaluate(content, new EvaluationOptions { RequireFormatValidation = true });
        Assert.True(validation.IsValid, JsonSerializer.Serialize(validation));
        using var fallback = JsonDocument.Parse(Assert.IsType<TextContentBlock>(Assert.Single(response.Content)).Text);
        Assert.True(JsonElement.DeepEquals(content, fallback.RootElement));
    }

    private static void AssertDescription(JsonElement schema, string property, string expected)
    {
        var description = schema.GetProperty("properties").GetProperty(property).GetProperty("description").GetString();
        Assert.Contains(expected, description);
    }

    private static JsonElement ResolveSchema(JsonElement schema, JsonElement root)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
        {
            return schema;
        }

        var path = reference.GetString()!;
        Assert.StartsWith("#/", path);
        var resolved = root;
        foreach (var segment in path[2..].Split('/'))
        {
            resolved = resolved.GetProperty(segment.Replace("~1", "/").Replace("~0", "~"));
        }
        return resolved;
    }
}
