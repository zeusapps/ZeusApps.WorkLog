using System.Text.Json;
using Tomlyn;
using Tomlyn.Model;

namespace WorkLog.Integration.Clients;

internal static class CodexIntegration
{
    public static void Plan(InitializationPlan plan, ClientSetup setup)
    {
        var configPath = Path.Combine(setup.Root, ".codex", "config.toml");
        var text = InitializationPlan.Read(configPath);
        var model = Toml.ToModel(text);
        ValidateOwnership(model, setup.Previous);

        var globalModel = Toml.ToModel(InitializationPlan.Read(setup.CodexUserConfigPath));
        var instructions = MergeInstructions(Value(model, "developer_instructions"),
            Value(globalModel, "developer_instructions"), setup.RepositoryIdentity);

        text = ReplaceInstructions(text, instructions);
        var registration = "[mcp_servers.worklog]\ncommand = " + Quote(setup.Server.Command)
            + "\nargs = [" + string.Join(", ", setup.Server.Arguments.Select(Quote)) + "]";
        text = ManagedBlock.Replace(text, ManagedBlock.ServerBegin, ManagedBlock.ServerEnd, registration);

        _ = Toml.ToModel(text); // Validate the entire result before scheduling a file update.
        plan.Add(configPath, text);
    }

    private static void ValidateOwnership(TomlTable model, IntegrationManifest? previous)
    {
        if (!model.TryGetValue("mcp_servers", out var rawServers) || rawServers is not TomlTable servers
            || !servers.TryGetValue("worklog", out var rawServer))
        {
            return;
        }

        if (previous?.Clients.Contains("codex") != true)
        {
            throw new InvalidOperationException("Codex MCP server 'worklog' already exists without an ownership manifest.");
        }
        if (rawServer is TomlTable owned && Value(owned, "command") != previous.Command)
        {
            throw new InvalidOperationException("Codex worklog server was changed outside worklog; resolve the conflict first.");
        }
    }

    private static string MergeInstructions(string? current, string? inherited, string repositoryIdentity)
    {
        if (current is null)
        {
            current = inherited ?? "";
        }
        else if (inherited is not null && !current.Contains(inherited, StringComparison.Ordinal))
        {
            current = inherited + "\n" + current;
        }

        return ManagedBlock.Replace(current, ManagedBlock.InstructionsBegin, ManagedBlock.InstructionsEnd,
            AgentInstructions.Render(repositoryIdentity, "codex"));
    }

    private static string ReplaceInstructions(string text, string instructions)
    {
        var document = Toml.Parse(text);
        var existing = document.KeyValues.FirstOrDefault(pair =>
            pair.Key?.ToString().Trim().Trim('"', '\'') == "developer_instructions");
        if (existing is null)
        {
            return "developer_instructions = " + Quote(instructions) + "\n" + text;
        }

        var span = existing.Value!.Span;
        return text[..span.Offset] + Quote(instructions) + text[(span.Offset + span.Length)..];
    }

    private static string Quote(string value) => JsonSerializer.Serialize(value);

    private static string? Value(TomlTable table, string key) =>
        table.TryGetValue(key, out var value) ? value as string : null;
}
