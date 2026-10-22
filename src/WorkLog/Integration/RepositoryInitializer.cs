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
    public IReadOnlyList<string> Run(string repositoryPath, string clients = "both", bool dryRun = false,
        string? ticketProvider = null, string? ticketScope = null, bool? instructions = null)
    {
        ValidateTicketOptions(ticketProvider, ticketScope);
        var selected = SelectClients(clients);
        var root = GitRepository.GetRoot(repositoryPath);
        if (checkClients)
        {
            foreach (var client in selected)
            {
                AgentClientValidator.Check(client);
            }
        }

        // Plan against a copy: dry runs and preflight failures must not change saved or in-memory preferences.
        using var settingsLease = settings.AcquireWrite();
        var plannedSettings = settings.FreshCopy();
        var context = new RepositoryRegistry(plannedSettings).GetContext(root, persist: false);
        if (ticketProvider is not null)
            context.Repository = context.Repository with
            {
                Provider = ticketProvider.Trim(),
                Scope = ticketScope!.Trim()
            };
        if (instructions.HasValue)
            foreach (var client in selected)
                context.Instructions[client] = instructions.Value;
        var repository = context.Repository;
        var manifestPath = Path.Combine(root, ".worklog", "integration.json");
        var previous = File.Exists(manifestPath)
            ? Json.Decode<IntegrationManifest>(File.ReadAllText(manifestPath))
            : null;
        var server = ServerRegistration.ForRepository(root, settings.DataDirectory, serverExecutable);
        var setup = CreateClientSetup(root, repository.Identity, server, previous, context);
        var plan = new InitializationPlan();

        if (selected.Contains("claude"))
        {
            ClaudeIntegration.Plan(plan, setup);
        }
        if (selected.Contains("codex"))
        {
            CodexIntegration.Plan(plan, setup);
        }

        var manifest = new IntegrationManifest(ApplicationInfo.Version, repository.Identity,
            selected.Union(previous?.Clients ?? []).Order().ToArray(), server.Command, server.Arguments);
        plan.Add(manifestPath, Json.Encode(manifest));
        PrivateGitFiles.ValidateTargets(plan, root);
        PrivateGitFiles.PlanExclusions(plan, root);
        // Writing ownership first makes an interrupted initialization safe to rerun.
        plan.Prioritize(manifestPath);
        plan.Add(settings.ConfigPath, plannedSettings.SerializePreserving());

        var messages = plan.Describe(dryRun);
        if (!dryRun)
        {
            plan.Apply(settings.DataDirectory);
            settings.Adopt(plannedSettings);
        }

        foreach (var client in selected)
            messages.Add($"{client}: managed instructions {(context.InstructionsEnabled(client) ? "enabled" : "disabled")} "
                + $"{(dryRun ? "(planned preference)" : "(saved preference)")}; MCP registration enabled.");
        messages.Add(dryRun
            ? "Dry run: no files written."
            : "Restart the selected clients. Trust this repository using each client's normal trust flow; init does not grant permissions. Logging follows session instructions, not guaranteed lifecycle hooks.");
        return messages;
    }

    private ClientSetup CreateClientSetup(string root, string repositoryIdentity,
        ServerRegistration server, IntegrationManifest? previous, RepositoryContext context)
    {
        var home = userHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var claudeHome = (userHome is null ? Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") : null) ?? home;
        var codexHome = (userHome is null ? Environment.GetEnvironmentVariable("CODEX_HOME") : null) ?? Path.Combine(home, ".codex");
        return new ClientSetup(root, repositoryIdentity, Path.Combine(claudeHome, ".claude.json"),
            Path.Combine(codexHome, "config.toml"), server, previous, context);
    }

    internal static void ValidateTicketOptions(string? provider, string? scope)
    {
        if (provider is null && scope is null)
            return;
        if (provider?.Trim() is not ("jira" or "azure-devops") || string.IsNullOrWhiteSpace(scope))
            throw new ArgumentException("Supply --ticket-provider (jira or azure-devops) and a nonempty --ticket-scope together.");
    }

    private static string[] SelectClients(string clients) => clients switch
    {
        "both" => ["claude", "codex"],
        "claude" => ["claude"],
        "codex" => ["codex"],
        _ => throw new ArgumentException("Clients must be both, claude, or codex.")
    };
}
