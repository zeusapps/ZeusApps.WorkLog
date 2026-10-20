using WorkLog.Cli;
using WorkLog.Configuration;
using WorkLog.Infrastructure;
using WorkLog.Integration.Clients;
using WorkLog.Repositories;

namespace WorkLog.Integration;

public sealed class RepositoryInitializer(
    LocalSettings settings,
    string? userHome = null,
    bool checkClients = true,
    string? serverExecutable = null)
{
    public IReadOnlyList<string> Run(string repositoryPath, string clients = "both", bool dryRun = false)
    {
        var selected = SelectClients(clients);
        var root = GitRepository.GetRoot(repositoryPath);
        if (checkClients)
        {
            foreach (var client in selected)
            {
                AgentClientValidator.Check(client);
            }
        }

        var repository = new RepositoryRegistry(settings).Get(root, persist: false);
        var manifestPath = Path.Combine(root, ".worklog", "integration.json");
        var previous = File.Exists(manifestPath)
            ? Json.Decode<IntegrationManifest>(File.ReadAllText(manifestPath))
            : null;
        var server = ServerRegistration.ForRepository(root, settings.DataDirectory, serverExecutable);
        var setup = CreateClientSetup(root, repository.Identity, server, previous);
        var plan = new InitializationPlan();

        PrivateGitFiles.ValidateTargets(root, selected);
        if (selected.Contains("claude"))
        {
            ClaudeIntegration.Plan(plan, setup);
        }
        if (selected.Contains("codex"))
        {
            CodexIntegration.Plan(plan, setup);
        }

        PrivateGitFiles.PlanExclusions(plan, root, selected);
        var manifest = new IntegrationManifest(ApplicationInfo.Version, repository.Identity,
            selected.Union(previous?.Clients ?? []).Order().ToArray(), server.Command, server.Arguments);
        plan.Add(manifestPath, Json.Encode(manifest));
        // Writing ownership first makes an interrupted initialization safe to rerun.
        plan.Prioritize(manifestPath);
        plan.Add(settings.ConfigPath, Json.Encode(settings.Configuration));

        var messages = plan.Describe(dryRun);
        if (!dryRun)
        {
            plan.Apply(settings.DataDirectory);
        }

        messages.Add(dryRun
            ? "Dry run: no files written."
            : "Restart the selected clients. Trust this repository using each client's normal trust flow; init does not grant permissions. Logging follows session instructions, not guaranteed lifecycle hooks.");
        return messages;
    }

    private ClientSetup CreateClientSetup(string root, string repositoryIdentity,
        ServerRegistration server, IntegrationManifest? previous)
    {
        var home = userHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var claudeHome = (userHome is null ? Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") : null) ?? home;
        var codexHome = (userHome is null ? Environment.GetEnvironmentVariable("CODEX_HOME") : null) ?? Path.Combine(home, ".codex");
        return new ClientSetup(root, repositoryIdentity, Path.Combine(claudeHome, ".claude.json"),
            Path.Combine(codexHome, "config.toml"), server, previous);
    }

    private static string[] SelectClients(string clients) => clients switch
    {
        "both" => ["claude", "codex"],
        "claude" => ["claude"],
        "codex" => ["codex"],
        _ => throw new ArgumentException("Clients must be both, claude, or codex.")
    };
}
