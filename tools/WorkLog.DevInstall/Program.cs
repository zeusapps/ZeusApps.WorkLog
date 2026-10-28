using System.Text.Json;
using WorkLog.DevInstall;
using WorkLog.Infrastructure;

// Usage: worklog-devinstall <preflight|switch|rollback|retire> --repo <path> [--package <zip>] [--operation <json>] [--version <v>]
//        [--installation <dir>] [--data-dir <dir>] [--workspace <dir>] [--skip-probe]
var action = args.FirstOrDefault()?.ToLowerInvariant();
var options = new Dictionary<string, string>(StringComparer.Ordinal);
var skipProbe = false;
for (var i = 1; i < args.Length; i++)
{
    if (args[i] == "--skip-probe")
        skipProbe = true;
    else if (args[i].StartsWith("--", StringComparison.Ordinal) && i + 1 < args.Length)
        options[args[i]] = args[++i];
    else
        return Fail("Unexpected argument: " + args[i]);
}

try
{
    var repo = options.GetValueOrDefault("--repo") ?? Directory.GetCurrentDirectory();
    var targets = DevTargets.Resolve(repo, options.GetValueOrDefault("--installation"), options.GetValueOrDefault("--data-dir"),
        options.GetValueOrDefault("--workspace"), skipProbe);
    var installer = new DevInstaller(targets);
    switch (action)
    {
        case "preflight":
            Console.WriteLine(Json.Encode(installer.Preflight(options.GetValueOrDefault("--package"))));
            break;
        case "switch":
            {
                var selection = await installer.SwitchAsync(Require("--operation"));
                Console.WriteLine($"Selected {selection.Current.Version} for new processes; previous {selection.Previous!.Version} recorded for rollback (generation {selection.Generation}). " +
                    "No process was stopped or restarted: running MCP servers and dashboards keep their payload and adopt this one at their next normal restart.");
                break;
            }
        case "rollback":
            {
                var selection = await installer.RollbackAsync(Require("--operation"));
                Console.WriteLine($"Restored {selection.Current.Version} for new processes (generation {selection.Generation}). No process was stopped or restarted; journal, configuration and data are unchanged.");
                break;
            }
        case "retire":
            Console.WriteLine("Removed " + await installer.RetireAsync(Require("--version")));
            break;
        default:
            return Fail("Expected an action: preflight, switch, rollback or retire.");
    }
    return 0;

    string Require(string name) => options.GetValueOrDefault(name) ?? throw new InvalidOperationException($"{name} is required.");
}
catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException or ArgumentException)
{
    return Fail(exception.Message);
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}
