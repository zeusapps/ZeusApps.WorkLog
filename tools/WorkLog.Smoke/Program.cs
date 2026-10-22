using WorkLog.Cli;
using WorkLog.Configuration;
using WorkLog.Smoke;
using WorkLog.Updates;

if (args is ["--settings-fixture", var settingsData, var key, var gate])
{
    var settings = new LocalSettings(settingsData);
    settings.Configuration.RepositoryContexts[key] = new(new(key));
    File.WriteAllText(gate + ".ready-" + key, "ready");
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    while (!File.Exists(gate))
        await Task.Delay(20, timeout.Token);
    settings.Save();
    return;
}

if (args is ["--versioned-updates", var priorExecutable, var candidateArchive])
{
    await new UpdateAcceptanceChecks(priorExecutable).RunMixedAsync(candidateArchive);
    return;
}

if (args is ["--versioned-native", var nativePriorExecutable, var nativeCandidateArchive])
{
    await new UpdateAcceptanceChecks(nativePriorExecutable).RunMixedAsync(nativeCandidateArchive, nativeClients: true);
    return;
}

if (args is ["--launcher-only", var launcherExecutable, var probeExecutable])
{
    await new LauncherAcceptanceChecks(launcherExecutable, probeExecutable).RunAsync();
    return;
}

if (args is ["--updates-only", var updateExecutable])
{
    await new UpdateAcceptanceChecks(updateExecutable).RunAsync();
    return;
}

if (args is ["--claude-aliases-only", var aliasExecutable])
{
    var aliasWorkspace = new SmokeWorkspace(aliasExecutable);
    Directory.CreateDirectory(aliasWorkspace.Root);
    await new ClaudeProjectAliasChecks(aliasWorkspace, new SmokeProcessRunner(aliasWorkspace)).RunAsync();
    return;
}

var executable = args.FirstOrDefault(argument => argument != "--live-agents")
    ?? $"artifacts/releases/worklog-{ApplicationInfo.Version}-win-x64/worklog.exe";
var workspace = new SmokeWorkspace(executable);
workspace.Initialize();

var processes = new SmokeProcessRunner(workspace);
await new ClientRegistrationChecks(workspace, processes).RunAsync();
await new JournalAcceptanceChecks(workspace).RunAsync();
await new InitializationAcceptanceChecks(workspace, processes).RunAsync();
await new ClaudeProjectAliasChecks(workspace, processes).RunAsync();
await new UpdateAcceptanceChecks(workspace.Executable).RunAsync();

if (args.Contains("--live-agents"))
{
    await new LiveAgentChecks(workspace, processes).RunAsync();
}

Console.WriteLine("SMOKE PASSED. Artifacts: " + workspace.Root);
