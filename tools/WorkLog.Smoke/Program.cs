using WorkLog.Cli;
using WorkLog.Configuration;
using WorkLog.Smoke;
using WorkLog.Updates;

if (args is ["--dashboard-fixture", var dashboardRoot])
{
    DashboardFixture.Create(dashboardRoot);
    return;
}
if (args is ["--dashboard-log", var logRoot, var logRepository, var logStatus, var logChange, var logTicket, var logAt, var logSummary])
{
    DashboardFixture.Log(logRoot, logRepository, logStatus, logChange, logTicket, logAt, logSummary);
    return;
}
if (args is ["--dashboard-digest", var digestRoot, var digestRepository, var digestTicket, var digestText])
{
    DashboardFixture.SaveDigest(digestRoot, digestRepository, digestTicket, digestText);
    return;
}
if (args is ["--dashboard-digest-structured", var structuredRoot, var structuredRepository, var structuredTicket, var structuredDone, var structuredNext, var structuredBlockers])
{
    DashboardFixture.SaveStructuredDigest(structuredRoot, structuredRepository, structuredTicket, structuredDone, structuredNext, structuredBlockers);
    return;
}
if (args is ["--c06-fixture", var c06Root])
{
    C06BaselineFixture.Create(c06Root);
    return;
}
if (args is ["--c06-log", var c06LogRoot, var c06Repository, var c06Change, var c06Ticket, var c06Summary])
{
    C06BaselineFixture.Log(c06LogRoot, c06Repository, c06Change, c06Ticket, c06Summary);
    return;
}
if (args is ["--c06-digest-save", var c06DigestRoot, var c06DigestRepository, var c06DigestTicket, var c06DigestSummary])
{
    C06BaselineFixture.SaveDigest(c06DigestRoot, c06DigestRepository, c06DigestTicket, c06DigestSummary);
    return;
}
if (args is ["--c06-owned-process", .. var c06OwnedArguments])
{
    Environment.ExitCode = C06ProcessTree.RunOwned(c06OwnedArguments);
    return;
}
if (args is ["--c06-hanging-child", var c06ChildMarker])
{
    Environment.ExitCode = C06ProcessTree.RunHangingChild(c06ChildMarker);
    return;
}
if (args is ["--c06-live-host", var c06LiveData, var c06LiveRepository, var c06LivePort, var c06LiveControlDirectory, var c06LiveWatcherMode])
{
    if (!int.TryParse(c06LivePort, out var parsedPort) || parsedPort is < 1 or > 65535)
        throw new ArgumentException("C06 live fixture requires a valid isolated port.");
    if (c06LiveWatcherMode is not ("fake" or "real"))
        throw new ArgumentException("C06 live fixture watcher mode must be fake or real.");
    Environment.ExitCode = await C06LiveAcceptanceHost.RunAsync(c06LiveData, c06LiveRepository, parsedPort, c06LiveControlDirectory, c06LiveWatcherMode == "real");
    return;
}
if (args is ["--dashboard-mcp", var dashboardExecutable, var dashboardDirectory])
{
    await DashboardFixture.ExerciseMcpAsync(dashboardExecutable, dashboardDirectory);
    return;
}

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

if (args is ["--digests-only", var digestExecutable])
{
    var digestWorkspace = new SmokeWorkspace(digestExecutable);
    Directory.CreateDirectory(digestWorkspace.Root);
    Directory.CreateDirectory(digestWorkspace.Repository);
    await new DigestAcceptanceChecks(digestWorkspace).RunAsync();
    Console.WriteLine("SMOKE PASSED. Artifacts: " + digestWorkspace.Root);
    return;
}

if (args is ["--worktrees-only", var worktreeExecutable])
{
    var worktreeWorkspace = new SmokeWorkspace(worktreeExecutable);
    Directory.CreateDirectory(worktreeWorkspace.Root);
    await new WorktreeAcceptanceChecks(worktreeWorkspace, new SmokeProcessRunner(worktreeWorkspace)).RunAsync();
    return;
}

var executable = args.FirstOrDefault(argument => argument != "--live-agents")
    ?? $"artifacts/releases/worklog-{ApplicationInfo.Version}-win-x64/worklog.exe";
var workspace = new SmokeWorkspace(executable);
workspace.Initialize();

var processes = new SmokeProcessRunner(workspace);
await new ClientRegistrationChecks(workspace, processes).RunAsync();
await new JournalAcceptanceChecks(workspace).RunAsync();
await new DigestAcceptanceChecks(workspace).RunAsync();
await new InitializationAcceptanceChecks(workspace, processes).RunAsync();
await new ClaudeProjectAliasChecks(workspace, processes).RunAsync();
await new WorktreeAcceptanceChecks(workspace, processes).RunAsync();
await new UpdateAcceptanceChecks(workspace.Executable).RunAsync();

if (args.Contains("--live-agents"))
{
    await new LiveAgentChecks(workspace, processes).RunAsync();
}

Console.WriteLine("SMOKE PASSED. Artifacts: " + workspace.Root);
