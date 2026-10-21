using WorkLog.Cli;

namespace WorkLog.Smoke;

internal sealed class ClientRegistrationChecks(SmokeWorkspace workspace, SmokeProcessRunner processes)
{
    public async Task RunAsync(bool instructionsEnabled = true)
    {
        await CheckSelfContainedVersionAsync();
        await CheckCodexAsync(instructionsEnabled);
        await CheckClaudeAsync();
    }

    private async Task CheckSelfContainedVersionAsync()
    {
        var output = await processes.RunAsync(workspace.Executable, ["--version"], new()
        {
            ["DOTNET_ROOT"] = Path.Combine(workspace.Root, "no-runtime"),
            ["DOTNET_MULTILEVEL_LOOKUP"] = "0"
        });
        if (output.Trim() != ApplicationInfo.Version)
        {
            throw new InvalidOperationException("Self-contained version check failed.");
        }
    }

    private async Task CheckCodexAsync(bool instructionsEnabled)
    {
        var environment = new Dictionary<string, string> { ["CODEX_HOME"] = workspace.CodexHome };
        var servers = await processes.RunAsync("codex", ["-c", workspace.TrustOverride, "mcp", "list", "--json"], environment);
        if (!servers.Contains("worklog"))
        {
            throw new InvalidOperationException("Codex did not discover registration.");
        }

        var prompt = await processes.RunAsync("codex",
            ["-c", workspace.TrustOverride, "debug", "prompt-input", "Smoke inspection only."], environment);
        if (prompt.Contains("Work journal integration") != instructionsEnabled)
        {
            throw new InvalidOperationException("Codex instruction loading disagrees with the saved preference.");
        }

        Console.WriteLine($"Codex loaded private server registration with managed instructions {(instructionsEnabled ? "enabled" : "disabled")}.");
    }

    private async Task CheckClaudeAsync()
    {
        var output = await processes.RunAsync("claude", ["mcp", "get", "worklog"], new()
        {
            ["CLAUDE_CONFIG_DIR"] = workspace.Home
        });
        if (!output.Contains("Connected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Claude MCP health check failed: " + output);
        }

        Console.WriteLine("Claude Code connected to its private local-scope MCP server.");
    }
}
