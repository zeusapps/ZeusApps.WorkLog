using System.CommandLine;
using WorkLog.Configuration;
using WorkLog.Installation;
using WorkLog.Updates;

namespace WorkLog.Cli;

internal sealed class UpdateCommand : Command
{
    public UpdateCommand() : base("update", "Check, activate a verified compatible release, or select a retained previous payload.")
    {
        var check = new Option<bool>("--check") { Description = "Check GitHub now without downloading or installing an update." };
        var status = new Option<bool>("--status") { Description = "Read running/selected versions and staged/activated/failed outcome offline." };
        var rollback = new Option<bool>("--rollback") { Description = "Explicitly select the previous verified compatible payload, without restoring journal data." };
        var dataDirectory = CommonOptions.DataDirectory();
        Options.Add(check);
        Options.Add(status);
        Options.Add(rollback);
        Options.Add(dataDirectory);
        Validators.Add(result =>
        {
            if (new[] { result.GetValue(check), result.GetValue(status), result.GetValue(rollback) }.Count(value => value) > 1)
                result.AddError("--check, --status and --rollback cannot be combined.");
        });
        SetAction(async (result, token) =>
        {
            var settings = new LocalSettings(result.GetValue(dataDirectory));
            var output = result.InvocationConfiguration.Output;
            var root = InstalledApplication.CurrentRoot();
            var selection = root is null ? null : InstallationLayout.ReadSelection(root);
            if (result.GetValue(status))
            {
                var outcome = UpdateInstaller.ReadOutcome(settings.DataDirectory, root ?? UpdateInstallation.CurrentDirectory);
                if (selection is not null)
                {
                    output.WriteLine($"Running {ApplicationInfo.Version}; selected {selection.Current.Version} for new processes; generation {selection.Generation}.");
                    if (outcome?.OperationId == selection.OperationId)
                    {
                        output.WriteLine($"activated: {selection.Current.Version}. Selection committed. Existing conversations adopt at normal reconnect/restart.");
                        return 0;
                    }
                    else
                        output.WriteLine("Selection is authoritative; the operation below may belong to another attempt or data directory.");
                }
                output.WriteLine(outcome is null ? "No update outcome recorded for this installation and data directory." :
                    $"{outcome.State}: {outcome.Version}. {outcome.Message}");
                return outcome?.State == "failed" && outcome.OperationId != selection?.OperationId ? 1 : 0;
            }

            if (!result.GetValue(check))
                root = InstalledApplication.RequireCurrent(settings.DataDirectory);
            var installer = new PayloadInstaller();
            if (result.GetValue(rollback))
            {
                var restored = await installer.RollbackAsync(root!, settings.DataDirectory, token);
                output.WriteLine($"activated: {restored.Current.Version} for new processes. Existing conversations continue; journal/configuration unchanged.");
                return 0;
            }
            using var http = ReleaseClient.CreateHttpClient();
            var client = new ReleaseClient(http);
            var discovery = new UpdateDiscovery(settings, client);
            var releaseCheck = await discovery.CheckAsync(true, token);
            if (releaseCheck?.Succeeded != true)
                throw new InvalidOperationException(releaseCheck?.Error ?? "Release check unavailable.");
            var release = releaseCheck.Release;
            if (release is null)
            {
                output.WriteLine($"Running {ApplicationInfo.Version}; selected {selection?.Current.Version ?? "n/a (source)"}; no stable Windows release candidate is available.");
                return 0;
            }
            if (!release.IsNewerThan(ApplicationInfo.Version) || selection is not null && !release.IsNewerThan(selection.Current.Version))
            {
                output.WriteLine($"Up to date: running {ApplicationInfo.Version}; selected {selection?.Current.Version ?? "n/a (source)"}; published {release.Version}.");
                return 0;
            }
            output.WriteLine($"Running {ApplicationInfo.Version}; selected {selection?.Current.Version ?? "n/a (source)"}; available {release.Version}. Compatibility is verified before activation. {release.ReleaseUrl}");
            if (result.GetValue(check))
                return 0;
            var prepared = await installer.DownloadAsync(client, root!, settings.DataDirectory, ApplicationInfo.Version, release, token);
            output.WriteLine("staged: " + release.Version + ". Verified; activation has not committed yet.");
            var activated = await installer.ActivateAsync(prepared, token);
            output.WriteLine($"Selected {activated.Current.Version} for new processes. Existing conversations continue on their original version; reconnect normally to adopt. Rerun init where instructions need refreshing.");
            return 0;
        });
    }
}
