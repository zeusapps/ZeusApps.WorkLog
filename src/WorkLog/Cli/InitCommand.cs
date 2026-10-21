using System.CommandLine;
using WorkLog.Configuration;
using WorkLog.Integration;

namespace WorkLog.Cli;

internal sealed class InitCommand : Command
{
    public InitCommand() : base("init", "Configure private WorkLog integration for a Git repository.")
    {
        var repository = CommonOptions.Repository();
        var dataDirectory = CommonOptions.DataDirectory();
        var clients = new Option<string>("--clients")
        {
            Description = "Coding agents to configure.",
            DefaultValueFactory = _ => "both",
            Arity = ArgumentArity.ExactlyOne
        };
        clients.AcceptOnlyFromAmong("both", "codex", "claude");
        var dryRun = new Option<bool>("--dry-run")
        {
            Description = "Describe configuration changes without writing files."
        };
        var ticketProvider = new Option<string>("--ticket-provider")
        {
            Description = "Repository ticket provider: jira or azure-devops. Requires --ticket-scope.",
            Arity = ArgumentArity.ExactlyOne
        };
        var ticketScope = new Option<string>("--ticket-scope")
        {
            Description = "Jira site or Azure DevOps organization/project. Requires --ticket-provider.",
            Arity = ArgumentArity.ExactlyOne
        };
        var instructions = new Option<bool>("--instructions") { Description = "Enable and remember managed instructions for selected clients." };
        var noInstructions = new Option<bool>("--no-instructions") { Description = "Remove owned instructions and remember the opt-out; keep MCP registration." };

        Options.Add(repository);
        Options.Add(dataDirectory);
        Options.Add(clients);
        Options.Add(dryRun);
        Options.Add(ticketProvider);
        Options.Add(ticketScope);
        Options.Add(instructions);
        Options.Add(noInstructions);
        Validators.Add(result =>
        {
            if (result.GetValue(instructions) && result.GetValue(noInstructions))
                result.AddError("--instructions and --no-instructions cannot be combined.");
            try { RepositoryInitializer.ValidateTicketOptions(result.GetValue(ticketProvider), result.GetValue(ticketScope)); }
            catch (ArgumentException error) { result.AddError(error.Message); }
        });

        SetAction(result =>
        {
            var settings = new LocalSettings(result.GetValue(dataDirectory));
            var initializer = new RepositoryInitializer(settings);
            var messages = initializer.Run(result.GetValue(repository) ?? Environment.CurrentDirectory,
                result.GetValue(clients)!, result.GetValue(dryRun), result.GetValue(ticketProvider), result.GetValue(ticketScope),
                result.GetValue(instructions) ? true : result.GetValue(noInstructions) ? false : null);
            foreach (var message in messages)
            {
                result.InvocationConfiguration.Output.WriteLine(message);
            }
        });
    }
}
