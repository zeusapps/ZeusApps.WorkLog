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
        // Claude canonicalizes Windows project paths with forward slashes.
        var project = Child(projects, setup.Root.Replace('\\', '/'));
        var servers = Child(project, "mcpServers");
        ValidateOwnership(servers, setup.Previous);

        servers["worklog"] = new JsonObject
        {
            ["type"] = "stdio",
            ["command"] = setup.Server.Command,
            ["args"] = new JsonArray(setup.Server.Arguments.Select(argument => (JsonNode?)JsonValue.Create(argument)).ToArray())
        };
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
            if (remaining != current) plan.Add(instructionPath, remaining);
        }
    }

    private static void ValidateOwnership(JsonObject servers, IntegrationManifest? previous)
    {
        if (servers["worklog"] is not null && previous?.Clients.Contains("claude") != true)
        {
            throw new InvalidOperationException("Claude MCP server 'worklog' already exists without an ownership manifest.");
        }

        if (servers["worklog"] is JsonObject owned && previous is not null
            && owned["command"]?.GetValue<string>() != previous.Command)
        {
            throw new InvalidOperationException("Claude worklog server was changed outside worklog; resolve the conflict first.");
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
