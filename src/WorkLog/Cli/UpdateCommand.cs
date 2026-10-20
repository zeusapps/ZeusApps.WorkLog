using System.CommandLine;
using WorkLog.Configuration;
using WorkLog.Updates;

namespace WorkLog.Cli;

internal sealed class UpdateCommand : Command
{
    public UpdateCommand() : base("update", "Check for or explicitly install a verified stable Windows release.")
    {
        var check = new Option<bool>("--check") { Description = "Check GitHub now without downloading or installing an update." };
        var status = new Option<bool>("--status") { Description = "Read the last staged/installed/failed update outcome without network access." };
        var dataDirectory = CommonOptions.DataDirectory();
        Options.Add(check);
        Options.Add(status);
        Options.Add(dataDirectory);
        Validators.Add(result =>
        {
            if (result.GetValue(check) && result.GetValue(status)) result.AddError("--check and --status cannot be combined.");
        });
        SetAction(async (result, token) =>
        {
            var settings = new LocalSettings(result.GetValue(dataDirectory));
            var output = result.InvocationConfiguration.Output;
            if (result.GetValue(status))
            {
                var outcome = UpdateInstaller.ReadOutcome(settings.DataDirectory, UpdateInstallation.CurrentDirectory);
                output.WriteLine(outcome is null ? "No update outcome recorded for this installation and data directory." :
                    $"{outcome.State}: {outcome.Version}. {outcome.Message}" + (outcome.RecoveryDirectory is null ? "" : " Recovery files: " + outcome.RecoveryDirectory));
                return outcome?.State == "failed" ? 1 : 0;
            }

            // Refuse unsupported installation before initiating any download or staging.
            var target = result.GetValue(check) ? null : UpdateInstallation.ValidateCurrent(settings.DataDirectory);
            using var http = ReleaseClient.CreateHttpClient();
            var client = new ReleaseClient(http);
            var discovery = new UpdateDiscovery(settings, client);
            var releaseCheck = await discovery.CheckAsync(true, token);
            if (releaseCheck?.Succeeded != true) throw new InvalidOperationException(releaseCheck?.Error ?? "Release check unavailable.");
            var release = releaseCheck.Release;
            if (release is null)
            {
                output.WriteLine($"Installed {ApplicationInfo.Version}; no compatible stable Windows release is available.");
                return 0;
            }
            if (!release.IsNewerThan(ApplicationInfo.Version))
            {
                output.WriteLine($"Up to date: installed {ApplicationInfo.Version}, published {release.Version}.");
                return 0;
            }
            output.WriteLine($"Installed {ApplicationInfo.Version}; available {release.Version}. {release.ReleaseUrl}");
            if (result.GetValue(check)) return 0;
            using var lease = UpdateInstallation.Acquire(target!);
            var plan = await new UpdateInstaller(client).PrepareAsync(target!, settings.DataDirectory, release, token);
            try { UpdateInstaller.StartHelper(plan); }
            catch
            {
                UpdateInstaller.WriteOutcome(plan, new("failed", release.Version, "Could not start helper; installed files were not changed."));
                throw;
            }
            output.WriteLine("staged: " + release.Version + ". Installation is not complete yet. Stop affected MCP clients and check worklog update --status"
                + (result.GetValue(dataDirectory) is null ? "." : " with the same --data-dir.")
                + " Restart clients afterward; rerun init where instructions need refreshing.");
            return 0;
        });
    }
}
