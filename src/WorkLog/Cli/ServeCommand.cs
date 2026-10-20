using System.CommandLine;
using WorkLog.Configuration;
using WorkLog.Mcp;

namespace WorkLog.Cli;

internal sealed class ServeCommand : Command
{
    public ServeCommand() : base("serve", "Run the MCP server over standard input/output.")
    {
        var repository = CommonOptions.Repository();
        var dataDirectory = CommonOptions.DataDirectory();
        Options.Add(repository);
        Options.Add(dataDirectory);

        SetAction(async (result, cancellationToken) =>
        {
            var settings = new LocalSettings(result.GetValue(dataDirectory));
            await McpServerHost.RunAsync(settings, result.GetValue(repository), cancellationToken);
        });
    }
}
