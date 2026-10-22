using System.Text.Json.Nodes;
using WorkLog.Infrastructure;

namespace WorkLog.Integration.Clients;

internal static class ClaudeIntegration
{
    public static void Plan(InitializationPlan plan, ClientSetup setup)
    {
        var text = InitializationPlan.Read(setup.ClaudeConfigPath);
        var config = string.IsNullOrWhiteSpace(text) ? new JsonObject()
            : JsonNode.Parse(text) as JsonObject ?? throw new ArgumentException("Expected a JSON object in client configuration.");

        var projects = Child(config, "projects");
        var keys = ProjectKeys(projects, setup.Root, OperatingSystem.IsWindows());
        // Validate every alias before adding any client edits to the initialization plan.
        foreach (var key in keys)
        {
            var project = ReadObject(projects, key, $"Claude project '{key}'");
            var servers = ReadObject(project, "mcpServers", $"Claude project '{key}' mcpServers");
            ValidateOwnership(servers, setup.Previous, key);
        }

        foreach (var key in keys)
        {
            var servers = Child(Child(projects, key), "mcpServers");
            servers["worklog"] = new JsonObject
            {
                ["type"] = "stdio",
                ["command"] = setup.Server.Command,
                ["args"] = new JsonArray(setup.Server.Arguments.Select(argument => (JsonNode?)JsonValue.Create(argument)).ToArray())
            };
            plan.DescribeTarget($"Claude project '{key}' worklog registration");
        }
        plan.Add(setup.ClaudeConfigPath, config.ToJsonString(Json.Options));

        var instructionPath = Path.Combine(setup.Root, "CLAUDE.local.md");
        if (setup.Context.InstructionsEnabled("claude"))
        {
            var instructions = AgentInstructions.Render(setup.RepositoryIdentity, "claude");
            var imports = FindInstructionImports(setup.Root);
            plan.Add(instructionPath, ManagedBlock.Replace(InitializationPlan.Read(instructionPath),
                ManagedBlock.InstructionsBegin, ManagedBlock.InstructionsEnd, instructions + "\n" + string.Join("\n", imports)));
        }
        else if (File.Exists(instructionPath))
        {
            var current = InitializationPlan.Read(instructionPath);
            var remaining = ManagedBlock.Remove(current, ManagedBlock.InstructionsBegin, ManagedBlock.InstructionsEnd);
            if (remaining != current)
                plan.Add(instructionPath, remaining);
        }
    }

    internal static string[] ProjectKeys(JsonObject projects, string root, bool windows)
    {
        var canonical = root.Replace('\\', '/');
        var comparison = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return projects.Select(project => project.Key)
            .Where(key => string.Equals(windows ? key.Replace('\\', '/') : key, canonical, comparison))
            .Append(canonical).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonObject ReadObject(JsonObject parent, string key, string description) =>
        parent[key] is null ? new JsonObject()
        : parent[key] as JsonObject ?? throw new ArgumentException($"Expected an object for {description}.");

    private static void ValidateOwnership(JsonObject servers, IntegrationManifest? previous, string key)
    {
        if (servers["worklog"] is not { } registration)
            return;
        if (previous?.Clients.Contains("claude") != true)
        {
            throw new InvalidOperationException($"Claude project '{key}' MCP server 'worklog' already exists without an ownership manifest.");
        }

        if (registration is not JsonObject owned
            || owned["command"] is not JsonValue command || !command.TryGetValue<string>(out var executable)
            || (owned["type"] is not null && (owned["type"] is not JsonValue type
                || !type.TryGetValue<string>(out var transport) || transport != "stdio"))
            || (owned["args"] is not null && (owned["args"] is not JsonArray arguments
                || arguments.Any(argument => argument is not JsonValue value || !value.TryGetValue<string>(out _)))))
        {
            throw new ArgumentException($"Expected a stdio worklog server object for Claude project '{key}'.");
        }
        if (executable != previous.Command)
        {
            throw new InvalidOperationException($"Claude project '{key}' worklog server was changed outside worklog; resolve the conflict first.");
        }
    }

    private static IEnumerable<string> FindInstructionImports(string root)
    {
        // An explicit import preserves AGENTS.md when CLAUDE.local.md disables automatic discovery.
        for (DirectoryInfo? ancestor = new(root); ancestor is not null; ancestor = ancestor.Parent)
        {
            foreach (var relative in new[] { "AGENTS.md", ".claude/AGENTS.md" })
            {
                var candidate = Path.Combine(ancestor.FullName, relative);
                if (File.Exists(candidate))
                {
                    yield return "@" + Path.GetRelativePath(root, candidate).Replace('\\', '/');
                }
            }
        }
    }

    private static JsonObject Child(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject child)
        {
            return child;
        }
        if (parent[key] is not null)
        {
            throw new ArgumentException("Expected an object for client configuration key: " + key);
        }

        var result = new JsonObject();
        parent[key] = result;
        return result;
    }
}
