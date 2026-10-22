using System.Text.Json;
using System.Text.Json.Nodes;
using WorkLog.Configuration;
using WorkLog.Repositories;

namespace WorkLog.Smoke;

internal sealed class ClaudeProjectAliasChecks(SmokeWorkspace workspace, SmokeProcessRunner processes)
{
    public async Task RunAsync()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Claude project-alias native acceptance requires Windows.");

        var root = Path.Combine(workspace.Root, "claude-aliases");
        var repo = Path.Combine(root, "AliasRepo");
        var home = Path.Combine(root, "home");
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(home);
        GitRepository.Run(repo, "init");
        var settings = new LocalSettings(data);
        settings.Configuration.Updates.Enabled = false;
        settings.Save();
        var environment = new Dictionary<string, string>
        {
            ["CLAUDE_CONFIG_DIR"] = home,
            ["CODEX_HOME"] = Path.Combine(home, ".codex")
        };
        var canonical = GitRepository.GetRoot(repo).Replace('\\', '/');
        var lower = canonical[..canonical.LastIndexOf('/')] + "/aliasrepo";
        Require(lower != canonical, "Fixture must have two distinct project-key spellings.");
        var paths = new[] { canonical, lower };
        var configPath = Path.Combine(home, ".claude.json");
        var worklogVersion = (await processes.RunAsync(workspace.Executable, ["--version"], environment, repo)).Trim();
        var claudeVersion = (await processes.RunAsync("claude", ["--version"], environment, repo)).Trim();
        var gitVersion = GitRepository.Run(repo, "--version").Trim();
        Task<string> InitAsync(params string[] options) => processes.RunAsync(workspace.Executable,
            ["init", "--repository", repo, "--data-dir", data, "--clients", "claude", .. options], environment, repo);

        await InitAsync();
        var seeded = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        var registration = seeded["projects"]![canonical]!["mcpServers"]!["worklog"]!.DeepClone();
        var alias = new JsonObject
        {
            ["aliasSentinel"] = "preserve lower project",
            ["mcpServers"] = new JsonObject
            {
                ["worklog"] = registration.DeepClone(),
                ["worklog-legacy"] = new JsonObject { ["type"] = "stdio", ["command"] = "unowned-legacy-sentinel" },
                ["other"] = new JsonObject { ["type"] = "stdio", ["command"] = "unowned-other-sentinel" }
            }
        };
        seeded["projects"] = new JsonObject { [lower] = alias };
        seeded["rootSentinel"] = "preserve root";
        File.WriteAllText(configPath, seeded.ToJsonString());
        var baseline = new List<object>();
        foreach (var path in paths)
        {
            // Keep each baseline independent: a client may create its own project object.
            File.WriteAllText(configPath, seeded.ToJsonString());
            try
            {
                var output = await processes.RunAsync("claude", ["mcp", "get", "worklog"], environment, path);
                baseline.Add(new
                {
                    cwd = path,
                    commandSucceeded = true,
                    connected = output.Contains("Connected", StringComparison.OrdinalIgnoreCase),
                    output
                });
            }
            catch (InvalidOperationException error)
            {
                baseline.Add(new
                {
                    cwd = path,
                    commandSucceeded = false,
                    connected = false,
                    output = error.Message
                });
            }
        }
        File.WriteAllText(Path.Combine(root, "baseline.json"), JsonSerializer.Serialize(new
        {
            windows = Environment.OSVersion.ToString(),
            worklogVersion,
            claudeVersion,
            gitVersion,
            canonical,
            lower,
            probes = baseline
        }, new JsonSerializerOptions { WriteIndented = true }));

        // Reconcile stale but provably owned args, rather than testing only duplicate JSON.
        alias["mcpServers"]!["worklog"]!["args"] = new JsonArray("stale-owned-arguments");
        File.WriteAllText(configPath, seeded.ToJsonString());
        var before = Snapshot(repo, configPath, settings.ConfigPath);
        var preview = await InitAsync("--dry-run", "--no-instructions");
        Require(before.SequenceEqual(Snapshot(repo, configPath, settings.ConfigPath)), "Alias dry-run changed files.");
        foreach (var path in paths)
            Require(preview.Contains("Claude project '" + path + "'"), "Dry-run omitted project alias: " + path);
        await InitAsync("--no-instructions");
        AssertRegistration();
        var first = File.ReadAllText(configPath);
        await InitAsync();
        Require(first == File.ReadAllText(configPath), "Repeated alias init changed the configuration.");
        Require(!File.ReadAllText(Path.Combine(repo, "CLAUDE.local.md")).Contains("worklog:begin"), "Alias init lost the persistent instruction opt-out.");

        var probes = new List<object>();
        foreach (var path in paths)
        {
            var output = await processes.RunAsync("claude", ["mcp", "get", "worklog"], environment, path);
            Require(output.Contains("Connected", StringComparison.OrdinalIgnoreCase), "Claude alias connection failed: " + path);
            Require(output.Contains(workspace.Executable, StringComparison.OrdinalIgnoreCase), "Claude did not discover the stable launcher: " + path);
            probes.Add(new
            {
                cwd = path,
                connected = true,
                output
            });
        }
        AssertRegistration();
        await InitAsync("--instructions");
        Require(File.ReadAllText(Path.Combine(repo, "CLAUDE.local.md")).Contains("worklog:begin"), "Alias init did not re-enable instructions.");
        AssertRegistration();
        File.WriteAllText(Path.Combine(root, "evidence.json"), JsonSerializer.Serialize(new
        {
            windows = Environment.OSVersion.ToString(),
            worklogVersion,
            claudeVersion,
            gitVersion,
            canonical,
            lower,
            stableLauncher = workspace.Executable,
            registration,
            probes,
            dryRunPreservedFiles = true,
            repeatedInitStable = true,
            instructionOptOutAndReenable = true,
            unrelatedSettingsPreserved = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("CLAUDE ALIASES PASSED: native discovery/connection from both spellings, launcher, preservation, preview and instruction modes. Artifacts: " + root);

        void AssertRegistration()
        {
            var actual = JsonNode.Parse(File.ReadAllText(configPath))!;
            Require(actual["rootSentinel"]?.GetValue<string>() == "preserve root", "Alias init lost root settings.");
            Require(actual["projects"]![lower]!["aliasSentinel"]?.GetValue<string>() == "preserve lower project", "Alias init lost project settings.");
            Require(actual["projects"]![canonical]!["aliasSentinel"] is null, "Alias init copied unrelated project settings.");
            foreach (var path in paths)
                Require(JsonNode.DeepEquals(registration, actual["projects"]![path]!["mcpServers"]!["worklog"]), "Alias registration mismatch: " + path);
            foreach (var name in new[] { "other", "worklog-legacy" })
                Require(JsonNode.DeepEquals(alias["mcpServers"]![name], actual["projects"]![lower]!["mcpServers"]![name]), "Alias init changed server: " + name);
        }
    }

    private static string[] Snapshot(string repo, string config, string settings) =>
        new[] { config, settings, Path.Combine(repo, ".worklog", "integration.json"),
            Path.Combine(repo, "CLAUDE.local.md"), Path.Combine(repo, ".git", "info", "exclude") }
            .Select(path => File.Exists(path) ? File.ReadAllText(path) : "<missing>").ToArray();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
