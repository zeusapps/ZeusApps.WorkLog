using System.CommandLine;
using WorkLog.Configuration;
using WorkLog.Dashboard;

namespace WorkLog.Cli;

internal sealed class DashboardCommand : Command
{
    public DashboardCommand() : base("dashboard", "Open the private local work dashboard and OpenSpec reader.")
    {
        var repository = CommonOptions.Repository();
        var data = CommonOptions.DataDirectory();
        var port = new Option<int>("--port") { Description = "Loopback port (default 47831).", DefaultValueFactory = _ => 47831 };
        var noOpen = new Option<bool>("--no-open") { Description = "Print the URL without opening a browser, including repeat launches." };
        Options.Add(repository); Options.Add(data); Options.Add(port); Options.Add(noOpen);
        SetAction(async (result, cancellationToken) =>
        {
            var value = result.GetValue(port);
            if (value is < 1 or > 65535) throw new ArgumentException("--port must be between 1 and 65535.");
            await DashboardHost.RunAsync(new LocalSettings(result.GetValue(data)), result.GetValue(repository), value, result.GetValue(noOpen), cancellationToken);
        });
    }
}
