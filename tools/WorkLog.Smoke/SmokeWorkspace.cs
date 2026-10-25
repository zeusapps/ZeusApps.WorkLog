using System.Text.Json;
using WorkLog.Configuration;
using WorkLog.Integration;
using WorkLog.Repositories;

namespace WorkLog.Smoke;

internal sealed class SmokeWorkspace
{
    public string Executable
    {
        get;
    }
    public string Root { get; } = Path.GetFullPath(Path.Combine("artifacts", "smoke", Guid.NewGuid().ToString("N")));
    public string Repository => Path.Combine(Root, "repo");
    public string Home => Path.Combine(Root, "home");
    public string Data => Path.Combine(Root, "data");
    public string CodexHome => Path.Combine(Home, ".codex");
    public string TrustOverride => TrustOverrideFor(Repository);
    public static string TrustOverrideFor(string repository) =>
        "projects." + JsonSerializer.Serialize(Path.GetFullPath(repository)) + ".trust_level=\"trusted\"";

    public SmokeWorkspace(string executable)
    {
        Executable = Path.GetFullPath(executable);
    }

    public void Initialize()
    {
        Directory.CreateDirectory(Repository);
        Directory.CreateDirectory(Home);
        GitRepository.Run(Repository, "init");
        File.WriteAllText(Path.Combine(Repository, "AGENTS.md"), "Shared smoke instructions must remain visible.\n");

        // Repeating setup verifies that both adapters can update their own managed configuration.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var initializer = new RepositoryInitializer(new LocalSettings(Data), Home,
                checkClients: true, serverExecutable: Executable);
            initializer.Run(Repository);
        }

        Directory.CreateDirectory(CodexHome);
        File.WriteAllText(Path.Combine(CodexHome, "config.toml"),
            "[projects." + JsonSerializer.Serialize(Repository) + "]\ntrust_level = 'trusted'\n");
        Console.WriteLine("Initialized both clients twice in " + Root);
    }
}
