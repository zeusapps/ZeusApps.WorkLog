using WorkLog.Smoke;

var executable = args.FirstOrDefault(argument => argument != "--live-agents")
    ?? "artifacts/releases/worklog-0.1.0-win-x64/worklog.exe";
var workspace = new SmokeWorkspace(executable);
workspace.Initialize();

var processes = new SmokeProcessRunner(workspace);
await new ClientRegistrationChecks(workspace, processes).RunAsync();
await new JournalAcceptanceChecks(workspace).RunAsync();

if (args.Contains("--live-agents"))
{
    await new LiveAgentChecks(workspace, processes).RunAsync();
}

Console.WriteLine("SMOKE PASSED. Artifacts: " + workspace.Root);
