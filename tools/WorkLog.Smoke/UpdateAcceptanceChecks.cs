using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorkLog.Configuration;
using WorkLog.Infrastructure;
using WorkLog.Installation;
using WorkLog.Repositories;
using WorkLog.Updates;

namespace WorkLog.Smoke;

internal sealed class UpdateAcceptanceChecks(string executable)
{
    private readonly string root = Path.GetFullPath(Path.Combine("artifacts", "update-smoke", Guid.NewGuid().ToString("N")));
    private string Install => Path.Combine(root, "install");
    private string Data => Path.Combine(root, "data");
    private string EntryPoint => Path.Combine(Install, "worklog.exe");
    private static readonly object Period = new { from = "2000-01-01T00:00:00Z", to = "2100-01-01T00:00:00Z", timezone = "UTC", view = "flat", pageSize = 1 };

    public async Task RunAsync()
    {
        Initialize();
        var selected = InstallationLayout.ReadSelection(Install);
        var actual = await CommandAsync(EntryPoint, ["--version"]);
        Require(actual.Output.Trim() == selected.Current.Version, "Bootstrap launcher version mismatch.");
        await using (var server = await PackagedServer.StartAsync(EntryPoint, Data, Path.Combine(root, "bootstrap-mcp.log")))
            Require(server.Version == selected.Current.Version, "Bootstrap MCP version mismatch.");
        var status = await CommandAsync(EntryPoint, ["update", "--status", "--data-dir", Data]);
        Require(status.Output.Contains("selected " + selected.Current.Version), "Status did not report selection.");
        Console.WriteLine("BOOTSTRAP PASSED: self-contained launcher/version, MCP protocol/EOF, offline status. Artifacts: " + root);
    }

    private void Initialize()
    {
        Directory.CreateDirectory(root);
        var source = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        _ = InstallationLayout.ReadSelection(source);
        CopyDirectory(source, Install);
        File.WriteAllText(Path.Combine(Install, "keep.txt"), "unowned sentinel");
        var settings = new LocalSettings(Data);
        settings.Save();
        var configuration = JsonNode.Parse(File.ReadAllText(settings.ConfigPath))!;
        configuration["future"] = new JsonObject { ["preserve"] = 42 };
        configuration["updates"]!["future"] = "keep";
        File.WriteAllText(settings.ConfigPath, configuration.ToJsonString());
        Console.WriteLine("Fixture Windows: " + Environment.OSVersion + "; logs: " + root);
    }

    public async Task RunMixedAsync(string candidateArchive, bool nativeClients = false)
    {
        Initialize();
        candidateArchive = Path.GetFullPath(candidateArchive);
        var before = InstallationLayout.ReadSelection(Install);
        var oldVersion = before.Current.Version;
        string newVersion;
        using (var archive = ZipFile.OpenRead(candidateArchive))
        using (var stream = archive.GetEntry("current.json")!.Open())
            newVersion = JsonSerializer.Deserialize<PayloadSelection>(stream, InstallationLayout.JsonOptions)!.Current.Version;
        Require(new ReleaseInfo(newVersion, "", "", "").IsNewerThan(oldVersion), "Fixture versions must actually differ.");
        var oldHash = InstallationLayout.Hash(Path.Combine(Install, "versions", oldVersion, "worklog.exe"));
        var launcherHash = InstallationLayout.Hash(EntryPoint);
        var originalConfig = File.ReadAllText(Path.Combine(Data, "config.json"));
        var cache = Path.Combine(Data, "updates", "release-cache.json");
        var release = new ReleaseInfo(newVersion, ReleaseInfo.ReleasesUrl + "/tag/v" + newVersion, ReleaseInfo.DownloadUrl(newVersion), ReleaseInfo.DownloadUrl(newVersion) + ".sha256");
        AtomicFile.Write(cache, Json.Encode(new UpdateCache(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, release)));
        var installer = new PayloadInstaller();
        var plan = await PrepareAsync(installer, candidateArchive, oldVersion, newVersion);
        var competing = await PrepareAsync(installer, candidateArchive, oldVersion, newVersion);
        await using var old = await PackagedServer.StartAsync(EntryPoint, Data, Path.Combine(root, "old-mcp.log"));
        Require(old.Version == oldVersion, "Old MCP serverInfo version mismatch.");
        using var payloadProcess = FindPayload(oldVersion);
        var oldPid = payloadProcess.Id;
        var oldStart = payloadProcess.StartTime;
        var request = new
        {
            status = "in_progress",
            change = "compatible-update",
            repository = "fixture",
            branch = "hotfix/DEP-12",
            idempotencyKey = "cross-version-retry",
            occurredAt = "2026-10-20T09:00:00Z"
        };
        var first = Structured(await old.ToolAsync("log_work", new
        {
            entry = request
        }));
        Require(first.GetProperty("updateHint").GetProperty("selectedVersion").GetString() == oldVersion, "Old server did not advertise running/selected hint context.");
        var entryId = first.GetProperty("entry").GetProperty("id").GetString();

        using var reached = new ManualResetEventSlim();
        using var releaseCommit = new ManualResetEventSlim();
        var coordinated = new PayloadInstaller
        {
            ObserveStage = phase =>
            {
                if (phase != "before-selection")
                    return;
                reached.Set();
                if (!releaseCommit.Wait(TimeSpan.FromSeconds(20)))
                    throw new TimeoutException("Fixture overlap did not finish.");
            }
        };
        var activation = Task.Run(() => coordinated.ActivateAsync(plan));
        try
        {
            Require(reached.Wait(TimeSpan.FromSeconds(20)), "Did not observe activation overlap.");
            var otherPublisher = Task.Run(() => installer.ActivateAsync(competing));
            var readers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CommandAsync(EntryPoint, ["update", "--status", "--data-dir", Data])));
            Require(readers.All(result => result.Output.Contains("selected " + oldVersion)), "Installed status readers were not observed during precommit.");
            await using (var concurrentLaunch = await PackagedServer.StartAsync(EntryPoint, Data, Path.Combine(root, "overlapping-launch.log")))
                Require(concurrentLaunch.Version == oldVersion, "Launch before commit did not use complete old payload.");
            releaseCommit.Set();
            await Task.WhenAll(activation, otherPublisher);
        }
        finally { releaseCommit.Set(); }

        var selected = InstallationLayout.ReadSelection(Install);
        Require(selected.Current.Version == newVersion && selected.Generation == before.Generation + 1, "Concurrent publisher regressed selection or duplicated the commit.");
        payloadProcess.Refresh();
        Require(!payloadProcess.HasExited && payloadProcess.Id == oldPid && payloadProcess.StartTime == oldStart, "Activation replaced the original process.");
        Require(InstallationLayout.Hash(EntryPoint) == launcherHash, "Activation replaced the stable launcher.");
        Require(InstallationLayout.Hash(Path.Combine(Install, "versions", newVersion, "worklog.exe")) != oldHash, "Fixture used the same executable bytes for both versions.");
        Require(File.ReadAllText(Path.Combine(Data, "config.json")) == originalConfig, "Activation modified private configuration.");
        await using var newer = await PackagedServer.StartAsync(EntryPoint, Data, Path.Combine(root, "new-mcp.log"));
        Require(newer.Version == newVersion, "New connection did not adopt selected payload.");
        var replay = Structured(await newer.ToolAsync("log_work", new
        {
            entry = request
        }));
        Require(replay.GetProperty("entry").GetProperty("id").GetString() == entryId && !replay.GetProperty("created").GetBoolean(), "Cross-version retry duplicated an observation.");
        var oldReplay = Structured(await old.ToolAsync("log_work", new
        {
            entry = request
        }));
        Require(!oldReplay.TryGetProperty("updateHint", out var offered) || offered.ValueKind == JsonValueKind.Null, "Old server kept offering already activated version.");
        await newer.ToolAsync("log_work", new
        {
            entry = new
            {
                status = request.status,
                change = request.change,
                repository = request.repository,
                branch = request.branch,
                idempotencyKey = request.idempotencyKey,
                occurredAt = request.occurredAt,
                summary = "conflicting retry"
            }
        }, expectError: true);
        var starts = await Task.WhenAll(new[] { old, newer }.Select(server => server.ToolAsync("log_work", new
        {
            entry = new
            {
                status = "started",
                change = "concurrent-start",
                repository = "fixture",
                occurredAt = "2026-10-20T10:00:00Z"
            }
        })));
        Require(starts.Select(result => Structured(result).GetProperty("entry").GetProperty("id").GetString()).Distinct().Count() == 1, "Cross-version first starts duplicated.");
        await VerifyCursorAsync(old, newer);
        await VerifySettingsProcessesAsync();
        await VerifyCrossVersionRepositoryWritersAsync(oldVersion, newVersion);
        if (nativeClients)
            await VerifyRetainedInitAsync(oldVersion, newVersion);

        var otherData = Path.Combine(root, "second-data");
        var otherSettings = new LocalSettings(otherData);
        otherSettings.Configuration.Updates.Enabled = false;
        otherSettings.Save();
        await using (var secondDataServer = await PackagedServer.StartAsync(EntryPoint, otherData, Path.Combine(root, "second-data-mcp.log")))
        {
            Require(secondDataServer.Version == newVersion, "Selection differed for another data directory.");
            var query = Structured(await secondDataServer.ToolAsync("query_work", new
            {
                period = Period
            }));
            Require(query.GetProperty("entries").GetArrayLength() == 0, "Custom data directories leaked observations.");
        }
        await CommandAsync(EntryPoint, ["update", "--rollback", "--data-dir", Data]);
        Require(InstallationLayout.ReadSelection(Install).Current.Version == oldVersion, "Explicit CLI rollback failed.");
        Require(Structured(await newer.ToolAsync("log_work", new
        {
            entry = request
        })).GetProperty("entry").GetProperty("id").GetString() == entryId, "Rollback broke a live new-version connection.");
        await VerifyInterruptedActivationAsync(candidateArchive, oldVersion, newVersion);
        await VerifyFailureCasesAsync(candidateArchive, oldVersion, newVersion);
        await VerifyRejectedArchivesAsync(candidateArchive, oldVersion, newVersion);
        Require(File.ReadAllText(Path.Combine(Install, "keep.txt")) == "unowned sentinel", "Unowned installation content changed.");
        Require(Directory.Exists(Path.Combine(Install, "versions", oldVersion)) && Directory.Exists(Path.Combine(Install, "versions", newVersion)), "Published payload was deleted.");
        await old.KillLauncherAsync();
        await WaitForExitAsync(payloadProcess);
        File.WriteAllText(Path.Combine(root, "evidence.json"), Json.Encode(new
        {
            windows = Environment.OSVersion.ToString(),
            oldVersion,
            newVersion,
            oldPid,
            oldStart,
            oldExecutableHash = oldHash,
            newExecutableHash = InstallationLayout.Hash(Path.Combine(Install, "versions", newVersion, "worklog.exe")),
            launcherHash,
            statusReadersDuringPrecommit = 4,
            originalStreamSurvived = true,
            crossVersionRetry = entryId,
            outcome = "passed",
            selection = InstallationLayout.ReadSelection(Install)
        }));
        Console.WriteLine("MIXED-VERSION PASSED: original PID/stream survived, new version adopted, retries/starts/cursors/settings/status/rollback/recovery/retention and launcher lifetime verified. Artifacts: " + root);
    }

    private async Task VerifyCursorAsync(PackagedServer old, PackagedServer newer)
    {
        var first = Structured(await old.ToolAsync("query_work", new
        {
            period = Period
        }));
        Require(first.GetProperty("hasMore").GetBoolean(), "Cursor fixture needs more observations.");
        var ids = first.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("id").GetString()).ToList();
        var cursor = first.GetProperty("nextCursor").GetString();
        var late = Structured(await newer.ToolAsync("log_work", new
        {
            entry = new
            {
                status = "in_progress",
                change = "late-backdated",
                repository = "fixture",
                occurredAt = "2026-10-20T09:30:00Z"
            }
        })).GetProperty("entry").GetProperty("id").GetString();
        while (cursor is not null)
        {
            var options = JsonSerializer.SerializeToNode(Period)!.AsObject();
            options["cursor"] = cursor;
            var next = Structured(await newer.ToolAsync("query_work", new
            {
                period = options
            }));
            ids.AddRange(next.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("id").GetString()));
            cursor = next.GetProperty("nextCursor").GetString();
        }
        Require(ids.Count == 2 && ids.Distinct().Count() == 2 && !ids.Contains(late), "Cross-version cursor lost its snapshot.");
    }

    private async Task VerifySettingsProcessesAsync()
    {
        var gate = Path.Combine(root, "settings-gate");
        var children = Enumerable.Range(0, 4).Select(index => StartCommand("dotnet", [typeof(UpdateAcceptanceChecks).Assembly.Location, "--settings-fixture", Data, "writer-" + index, gate])).ToArray();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (Enumerable.Range(0, children.Length).Any(index => !File.Exists(gate + ".ready-writer-" + index)))
                await Task.Delay(20, timeout.Token);
            File.WriteAllText(gate, "go");
            await Task.WhenAll(children.Select(process => CompleteCommandAsync(process, "settings-writer", false)));
        }
        finally { foreach (var child in children) { if (!child.HasExited) child.Kill(entireProcessTree: true); child.Dispose(); } }
        var saved = JsonNode.Parse(File.ReadAllText(Path.Combine(Data, "config.json")))!;
        Require(saved["repositoryContexts"]!.AsObject().Count == 4 && saved["future"]!["preserve"]!.GetValue<int>() == 42
            && saved["updates"]!["future"]!.GetValue<string>() == "keep", "Independent process settings saves dropped state.");
    }

    private async Task VerifyInterruptedActivationAsync(string archive, string oldVersion, string newVersion)
    {
        var normal = new PayloadInstaller();
        var plan = await PrepareAsync(normal, archive, oldVersion, newVersion);
        foreach (var phase in new[] { "before-publish", "before-selection", "after-selection" })
        {
            var injected = new PayloadInstaller { ObserveStage = actual => { if (actual == phase) throw new IOException("fixture interruption: " + phase); } };
            try
            {
                await injected.ActivateAsync(plan);
                throw new InvalidOperationException("Interruption was not reached.");
            }
            catch (IOException error) when (error.Message.StartsWith("fixture interruption", StringComparison.Ordinal)) { }
            var actual = InstallationLayout.ReadSelection(Install);
            Require(actual.Current.Version == (phase == "after-selection" ? newVersion : oldVersion), "Interrupted selection was inconsistent.");
        }
        var status = await CommandAsync(EntryPoint, ["update", "--status", "--data-dir", Data]);
        Require(status.Output.Contains("activated: " + newVersion), "Post-commit outcome recovery ignored committed selection.");
    }

    private async Task VerifyRetainedInitAsync(string oldVersion, string newVersion)
    {
        var repo = Path.Combine(root, "native repo with spaces");
        var home = Path.Combine(root, "native-home");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(Path.Combine(home, ".codex"));
        GitRepository.Run(repo, "init");
        var environment = new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = home, ["CODEX_HOME"] = Path.Combine(home, ".codex") };
        await CommandAsync(EntryPoint, ["init", "--repository", repo, "--data-dir", Data, "--no-instructions"], environment: environment);
        var retained = Path.Combine(Install, "versions", oldVersion, "worklog.exe");
        await CommandAsync(retained, ["init", "--repository", repo, "--data-dir", Data], environment: environment);
        var owned = JsonNode.Parse(File.ReadAllText(Path.Combine(repo, ".worklog", "integration.json")))!;
        if (owned["command"]?.GetValue<string>() != EntryPoint)
            throw new InvalidOperationException("Retained-payload init pinned its payload instead of the stable launcher.");
        var registration = JsonNode.Parse(File.ReadAllText(Path.Combine(home, ".claude.json")))!
            ["projects"]![GitRepository.GetRoot(repo).Replace('\\', '/')]!["mcpServers"]!["worklog"]!;
        Require(registration["command"]!.GetValue<string>() == EntryPoint && !File.Exists(Path.Combine(repo, "CLAUDE.local.md")), "Retained init changed stable registration or instruction opt-out.");
        var claude = await CommandAsync("claude", ["mcp", "get", "worklog"], environment: environment, directory: repo);
        Require(claude.Output.Contains("Connected", StringComparison.OrdinalIgnoreCase), "Native Claude could not connect through the reconciled launcher.");
        var trust = "projects." + JsonSerializer.Serialize(GitRepository.GetRoot(repo)) + ".trust_level=\"trusted\"";
        // Native project config loading needs persisted trust in the disposable home, as in SmokeWorkspace.
        File.WriteAllText(Path.Combine(home, ".codex", "config.toml"), "[projects." + JsonSerializer.Serialize(GitRepository.GetRoot(repo)) + "]\ntrust_level='trusted'\n");
        var codex = await CommandAsync("codex.cmd", ["-c", trust, "mcp", "list", "--json"], environment: environment, directory: repo);
        Require(codex.Output.Contains("worklog"), "Native Codex did not discover the launcher registration.");
        var claudeVersion = await CommandAsync("claude", ["--version"], environment: environment, directory: repo);
        var codexVersion = await CommandAsync("codex.cmd", ["--version"], environment: environment, directory: repo);
        File.WriteAllText(Path.Combine(root, "native-clients.json"), Json.Encode(new
        {
            claude = claudeVersion.Output.Trim(),
            codex = codexVersion.Output.Trim(),
            retainedInitVersion = oldVersion,
            selectedVersion = newVersion,
            stableLauncher = EntryPoint
        }));
    }

    private async Task VerifyCrossVersionRepositoryWritersAsync(string oldVersion, string newVersion)
    {
        var versions = new[] { oldVersion, newVersion };
        var repositories = versions.Select(version => Path.Combine(root, "repository-" + version)).ToArray();
        foreach (var repo in repositories)
        {
            Directory.CreateDirectory(repo);
            GitRepository.Run(repo, "init");
        }
        var independent = await Task.WhenAll(versions.Select((version, index) => PackagedServer.StartAsync(
            Path.Combine(Install, "versions", version, "worklog.exe"), Data, Path.Combine(root, "repository-writer-" + version + ".log"), repositories[index])));
        foreach (var server in independent)
            await server.DisposeAsync();
        var shared = Path.Combine(root, "shared-repository");
        Directory.CreateDirectory(shared);
        GitRepository.Run(shared, "init");
        var same = await Task.WhenAll(versions.Select(version => PackagedServer.StartAsync(
            Path.Combine(Install, "versions", version, "worklog.exe"), Data, Path.Combine(root, "same-repository-" + version + ".log"), shared)));
        try
        {
            var results = await Task.WhenAll(same.Select(server => server.ToolAsync("log_work", new
            {
                entry = new
                {
                    status = "started",
                    change = "repository-first-use",
                    occurredAt = "2026-10-20T12:00:00Z"
                }
            })));
            Require(results.Select(result => Structured(result).GetProperty("entry").GetProperty("repository").GetString()).Distinct().Count() == 1,
                "Different compiled versions created conflicting first-use repository identities.");
            Require(results.Select(result => Structured(result).GetProperty("entry").GetProperty("id").GetString()).Distinct().Count() == 1,
                "Cross-version bound-repository starts diverged.");
        }
        finally { foreach (var server in same) await server.DisposeAsync(); }
        var saved = JsonNode.Parse(File.ReadAllText(Path.Combine(Data, "config.json")))!;
        Require(saved["repositoryContexts"]!.AsObject().Count == 7 && saved["future"]!["preserve"]!.GetValue<int>() == 42
            && saved["updates"]!["future"]!.GetValue<string>() == "keep", "Old/new packaged settings writers lost independent contexts or future fields.");
    }

    private async Task VerifyFailureCasesAsync(string archive, string oldVersion, string newVersion)
    {
        var current = InstallationLayout.ReadSelection(Install);
        var pointer = File.ReadAllText(Path.Combine(Install, "current.json"));
        foreach (var bad in new[] { "{", Json.Encode(current with { Current = new("../../escape", current.Current.ManifestHash) }),
            Json.Encode(current with { Current = new("999.0.0", current.Current.ManifestHash) }) })
        {
            AtomicFile.Write(Path.Combine(Install, "current.json"), bad);
            var failure = await CommandAsync(EntryPoint, ["--version"], expectFailure: true);
            Require(string.IsNullOrWhiteSpace(failure.Output), "Invalid pointer emitted non-protocol stdout.");
        }
        AtomicFile.Write(Path.Combine(Install, "current.json"), pointer);
        await new PayloadInstaller().RollbackAsync(Install, Data);
        var prepared = await PrepareAsync(new PayloadInstaller(), archive, oldVersion, newVersion);
        var before = InstallationLayout.ReadSelection(Install);
        var publisherLock = Path.Combine(Install, ".worklog-publish.lock");
        File.SetAttributes(publisherLock, FileAttributes.ReadOnly);
        try
        {
            try
            {
                await new PayloadInstaller().ActivateAsync(prepared);
                throw new InvalidOperationException("Read-only publisher unexpectedly succeeded.");
            }
            catch (UnauthorizedAccessException) { }
        }
        finally { File.SetAttributes(publisherLock, FileAttributes.Normal); }
        Require(InstallationLayout.ReadSelection(Install) == before, "Read-only failure changed selection.");
        var candidate = Path.Combine(prepared.StageDirectory, "package", "versions", newVersion, "release.json");
        var document = JsonNode.Parse(File.ReadAllText(candidate))!;
        document["sharedDataContract"] = 2;
        File.WriteAllText(candidate, document.ToJsonString());
        try
        {
            await new PayloadInstaller().ActivateAsync(prepared);
            throw new InvalidOperationException("Incompatible candidate unexpectedly succeeded.");
        }
        catch (InvalidOperationException error) when (error.Message.Contains("maintenance")) { }
        Require(InstallationLayout.ReadSelection(Install) == before, "Incompatible failure changed selection.");
        // Reactivate a fresh verified package while both original connections are still alive.
        await new PayloadInstaller().ActivateAsync(await PrepareAsync(new PayloadInstaller(), archive, oldVersion, newVersion));
    }

    private async Task VerifyRejectedArchivesAsync(string archive, string oldVersion, string newVersion)
    {
        var installer = new PayloadInstaller();
        await installer.RollbackAsync(Install, Data);
        var before = InstallationLayout.ReadSelection(Install);
        foreach (var fault in new[] { "checksum", "manifest", "runtime", "version", "hash", "shared-data", "schema", "traversal", "reparse", "collision" })
        {
            var stage = Path.Combine(Data, "updates", "staging", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            var damaged = Path.Combine(stage, ReleaseInfo.PackageName(newVersion));
            File.Copy(archive, damaged);
            if (fault != "checksum")
            {
                using var zip = ZipFile.Open(damaged, ZipArchiveMode.Update);
                static void Write(ZipArchive zip, string name, string contents, int attributes = 0)
                {
                    zip.GetEntry(name)?.Delete();
                    var entry = zip.CreateEntry(name);
                    entry.ExternalAttributes = attributes;
                    using var writer = new StreamWriter(entry.Open(), new System.Text.UTF8Encoding(false));
                    writer.Write(contents);
                }
                if (fault is "manifest" or "runtime" or "version" or "shared-data" or "schema")
                {
                    var name = "versions/" + newVersion + "/release.json";
                    JsonNode manifest;
                    using (var reader = new StreamReader(zip.GetEntry(name)!.Open()))
                        manifest = JsonNode.Parse(reader.ReadToEnd())!;
                    switch (fault)
                    {
                        case "manifest":
                            manifest["packageFormat"] = 0;
                            break;
                        case "runtime":
                            manifest["runtime"] = "win-x86";
                            break;
                        case "version":
                            manifest["version"] = "999.0.0";
                            break;
                        case "shared-data":
                            manifest["sharedDataContract"] = 2;
                            break;
                        case "schema":
                            manifest["journalSchema"] = 3;
                            break;
                    }
                    var content = manifest.ToJsonString();
                    Write(zip, name, content);
                    var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
                    JsonNode pointer;
                    using (var reader = new StreamReader(zip.GetEntry("current.json")!.Open()))
                        pointer = JsonNode.Parse(reader.ReadToEnd())!;
                    pointer["current"]!["manifestHash"] = hash;
                    Write(zip, "current.json", pointer.ToJsonString());
                }
                else if (fault == "hash")
                    Write(zip, "versions/" + newVersion + "/worklog.exe", "tampered executable");
                else if (fault == "traversal")
                    Write(zip, "../escape-from-package", "unsafe");
                else if (fault == "reparse")
                    Write(zip, "versions/" + newVersion + "/README.md", "unsafe", (int)FileAttributes.ReparsePoint);
                else
                    Write(zip, "WORKLOG.EXE", "case collision");
            }
            File.WriteAllText(damaged + ".sha256", (fault == "checksum" ? new string('0', 64) : ReleasePackage.Hash(damaged)) + "  " + Path.GetFileName(damaged));
            var accepted = false;
            try
            {
                await installer.PrepareDownloadedAsync(Install, Data, oldVersion, newVersion, stage);
                accepted = true;
            }
            catch (Exception error) when (error is IOException or InvalidOperationException or JsonException)
            {
                File.WriteAllText(Path.Combine(root, "rejected-" + fault + ".log"), error.Message);
            }
            Require(!accepted && InstallationLayout.ReadSelection(Install) == before, "Invalid packaged candidate was accepted: " + fault);
            // This failed fixture operation has no live child or published payload. Keep logs, release bulky temporary copies.
            var stagingRoot = Path.GetFullPath(Path.Combine(Data, "updates", "staging")) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(stage).StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Invalid fixture cleanup path.");
            Directory.Delete(stage, recursive: true);
        }
        await installer.ActivateAsync(await PrepareAsync(installer, archive, oldVersion, newVersion));
    }

    private async Task<PreparedPayload> PrepareAsync(PayloadInstaller installer, string archive, string running, string version)
    {
        var stage = Path.Combine(Data, "updates", "staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var copy = Path.Combine(stage, ReleaseInfo.PackageName(version));
        File.Copy(archive, copy);
        File.Copy(archive + ".sha256", copy + ".sha256");
        return await installer.PrepareDownloadedAsync(Install, Data, running, version, stage);
    }
    private Process FindPayload(string version)
    {
        var expected = Path.Combine(Install, "versions", version, "worklog.exe");
        foreach (var process in Process.GetProcessesByName("worklog"))
        {
            try
            {
                if (process.MainModule?.FileName.Equals(expected, StringComparison.OrdinalIgnoreCase) == true)
                    return process;
            }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException) { }
            process.Dispose();
        }
        throw new InvalidOperationException("Fixture payload process was not found.");
    }
    private static async Task WaitForExitAsync(Process process)
    {
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }
    private static JsonElement Structured(JsonElement result) => result.GetProperty("structuredContent");
    private static void Require(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }
    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (var directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }
    internal static Process StartCommand(string command, string[] arguments, Dictionary<string, string>? environment = null, string? directory = null)
    {
        var start = new ProcessStartInfo(command) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        if (environment is not null)
            foreach (var (key, value) in environment)
                start.Environment[key] = value;
        if (directory is not null)
            start.WorkingDirectory = directory;
        return Process.Start(start)!;
    }
    private async Task<(string Output, string Error)> CommandAsync(string command, string[] arguments, bool expectFailure = false, Dictionary<string, string>? environment = null, string? directory = null)
    {
        using var process = StartCommand(command, arguments, environment, directory);
        return await CompleteCommandAsync(process, Path.GetFileName(command), expectFailure);
    }
    private async Task<(string Output, string Error)> CompleteCommandAsync(Process process, string name, bool expectFailure)
    {
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardInput.Close();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
        }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var output = await stdout;
        var error = await stderr;
        File.WriteAllText(Path.Combine(root, name + "-" + Guid.NewGuid().ToString("N") + ".log"), output + "\n" + error);
        Require(expectFailure ? process.ExitCode != 0 : process.ExitCode == 0, "Unexpected command exit: " + process.ExitCode + " " + error + output);
        return (output, error);
    }
}
