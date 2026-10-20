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

        Options.Add(repository);
        Options.Add(dataDirectory);
        Options.Add(clients);
        Options.Add(dryRun);

        SetAction(result =>
        {
            var settings = new LocalSettings(result.GetValue(dataDirectory));
            var initializer = new RepositoryInitializer(settings);
            var messages = initializer.Run(result.GetValue(repository) ?? Environment.CurrentDirectory,
                result.GetValue(clients)!, result.GetValue(dryRun));
            foreach (var message in messages)
            {
                result.InvocationConfiguration.Output.WriteLine(message);
            }
        });
    }
}
