using System.CommandLine;

namespace WorkLog.Cli;

internal static class CliApplication
{
    public static Task<int> RunAsync(string[] arguments, CancellationToken cancellationToken = default)
    {
        var root = new RootCommand("A private local work journal for coding agents.");
        root.Subcommands.Add(new InitCommand());
        root.Subcommands.Add(new ServeCommand());
        root.Subcommands.Add(new UpdateCommand());
        root.Options.OfType<VersionOption>().Single().Action = new ApplicationVersionAction();

        // Preserve the existing no-argument/help entry points while the library owns parsing and help output.
        var normalized = arguments.Length == 0 || arguments is ["help"] ? ["--help"] : arguments;
        var invocation = new InvocationConfiguration { EnableDefaultExceptionHandler = false };
        return root.Parse(normalized).InvokeAsync(invocation, cancellationToken);
    }
}
