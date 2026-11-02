using System.Diagnostics;
using System.IO.Compression;
using WorkLog.DevInstall;
using WorkLog.Infrastructure;
using WorkLog.Installation;
using WorkLog.Repositories;
using WorkLog.Updates;
using Xunit;
using WorkJson = WorkLog.Infrastructure.Json;

namespace WorkLog.Tests.Updates;

public sealed class DevInstallTests
{
    [Fact]
    public async Task PreflightChangesNothingAndSwitchSelectsTheBuildWithTheOriginalAsPrevious()
    {
        using var fixture = new Fixture();
        var package = fixture.Package("0.1.9001");
        var before = fixture.Snapshot();
        var preflight = fixture.Installer.Preflight(package);
        Assert.Equal(before, fixture.Snapshot());
        Assert.StartsWith("0.1.9001 dev ", preflight.Label);
        Assert.True(File.Exists(preflight.OperationManifest));
        Assert.True(DevTargets.Within(fixture.Targets.Workspace, preflight.OperationManifest));

        var original = InstallationLayout.ReadSelection(fixture.Install);
        var selection = await fixture.Installer.SwitchAsync(preflight.OperationManifest);
        Assert.Equal("0.1.9001", selection.Current.Version);
        Assert.Equal(original.Current, selection.Previous);
        Assert.Equal(selection, InstallationLayout.ReadSelection(fixture.Install));
        Assert.True(Directory.Exists(Path.Combine(fixture.Install, "versions", "0.1.0")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Installer.SwitchAsync(preflight.OperationManifest));
    }

    [Fact]
    public void PreflightRefusesStaleUnmarkedAndCorruptPackagesAndChangesNothing()
    {
        using var fixture = new Fixture();
        var before = fixture.Snapshot();
        // The newest source entry may be a file or a directory; the message names which, so assert the shared part.
        var staleMessage = Assert.Throws<InvalidOperationException>(() =>
            fixture.Installer.Preflight(fixture.Package("0.1.9001", builtAt: "2001-01-01T00:00:00Z"))).Message;
        Assert.StartsWith("Source ", staleMessage);
        Assert.Contains("changed after the package was built", staleMessage);
        Assert.Contains("built from commit", Assert.Throws<InvalidOperationException>(() =>
            fixture.Installer.Preflight(fixture.Package("0.1.9002", commit: "1234567890abcdef1234567890abcdef12345678"))).Message);
        Assert.Contains("not a marked", Assert.Throws<InvalidOperationException>(() =>
            fixture.Installer.Preflight(fixture.Package("0.1.9003", marked: false))).Message);
        var corrupt = fixture.Package("0.1.9004");
        File.AppendAllText(corrupt, "tampered");
        Assert.Throws<InvalidOperationException>(() => fixture.Installer.Preflight(corrupt));
        Assert.Throws<InvalidOperationException>(() => fixture.Installer.Preflight(fixture.Package("0.1.0", marked: false)));
        Assert.Equal(before, fixture.Snapshot());
        Assert.False(Directory.Exists(fixture.Targets.Workspace) && Directory.EnumerateFiles(fixture.Targets.Workspace).Any());
    }

    [Fact]
    public async Task SwitchRemovesItsStageDirectoryOnSuccess()
    {
        using var fixture = new Fixture();
        var operation = fixture.Installer.Preflight(fixture.Package("0.1.9001")).OperationManifest;
        var other = Path.Combine(fixture.Data, "updates", "staging", "unrelated");
        Directory.CreateDirectory(other);
        await fixture.Installer.SwitchAsync(operation);
        Assert.Equal([other], Directory.EnumerateDirectories(Path.Combine(fixture.Data, "updates", "staging")).ToArray());
        Assert.Equal("0.1.9001", InstallationLayout.ReadSelection(fixture.Install).Current.Version);
        Assert.Equal("activated", UpdateInstaller.ReadOutcome(fixture.Data, fixture.Install)?.State);
    }

    [Fact]
    public async Task SwitchRemovesItsStageDirectoryWhenActivationFails()
    {
        using var fixture = new Fixture();
        // A different payload already occupies the version folder, so publication refuses after the stage was prepared.
        fixture.AddPayload("0.1.9001", null);
        var operation = fixture.Installer.Preflight(fixture.Package("0.1.9001")).OperationManifest;
        var before = InstallationLayout.ReadSelection(fixture.Install);
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Installer.SwitchAsync(operation));
        Assert.Equal(before, InstallationLayout.ReadSelection(fixture.Install));
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(fixture.Data, "updates", "staging")));
    }

    [Fact]
    public async Task RetireRefusesTheRolledBackPayloadUntilAFurtherSelectionChange()
    {
        using var fixture = new Fixture();
        var operation = fixture.Installer.Preflight(fixture.Package("0.1.9001")).OperationManifest;
        await fixture.Installer.SwitchAsync(operation);
        await fixture.Installer.RollbackAsync(operation);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Installer.RetireAsync("0.1.9001"));
        Assert.Contains("previous payload", error.Message);
        Assert.Contains("further selection change", error.Message);
        Assert.True(Directory.Exists(Path.Combine(fixture.Install, "versions", "0.1.9001")));

        await fixture.Activate("0.2.0");
        await fixture.Installer.RetireAsync("0.1.9001");
        Assert.False(Directory.Exists(Path.Combine(fixture.Install, "versions", "0.1.9001")));
        Assert.Equal("0.2.0", InstallationLayout.ReadSelection(fixture.Install).Current.Version);
    }

    [Fact]
    public async Task SwitchRefusesWhenTheSelectionChangedSincePreflight()
    {
        using var fixture = new Fixture();
        var operation = fixture.Installer.Preflight(fixture.Package("0.1.9001")).OperationManifest;
        await fixture.Activate("0.2.0");
        var before = InstallationLayout.ReadSelection(fixture.Install);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Installer.SwitchAsync(operation));
        Assert.Equal(before, InstallationLayout.ReadSelection(fixture.Install));
        Assert.False(Directory.Exists(Path.Combine(fixture.Install, "versions", "0.1.9001")));
    }

    [Fact]
    public async Task RollbackRestoresTheOriginalSelectionOnceAndRefusesAfterALaterChange()
    {
        using var fixture = new Fixture();
        var original = InstallationLayout.ReadSelection(fixture.Install);
        var first = fixture.Installer.Preflight(fixture.Package("0.1.9001")).OperationManifest;
        await fixture.Installer.SwitchAsync(first);
        var restored = await fixture.Installer.RollbackAsync(first);
        Assert.Equal(original.Current, restored.Current);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Installer.RollbackAsync(first));

        var second = fixture.Installer.Preflight(fixture.Package("0.1.9002")).OperationManifest;
        await fixture.Installer.SwitchAsync(second);
        await fixture.Activate("0.2.0");
        var changed = InstallationLayout.ReadSelection(fixture.Install);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Installer.RollbackAsync(second));
        Assert.Contains("selection changed", error.Message);
        Assert.Equal(changed, InstallationLayout.ReadSelection(fixture.Install));
    }

    [Fact]
    public async Task PayloadInstallerRollbackHonorsTheExpectedSelectionUnderTheLock()
    {
        using var fixture = new Fixture();
        await fixture.Activate("0.2.0");
        var current = InstallationLayout.ReadSelection(fixture.Install);
        var installer = new PayloadInstaller((_, _, _) => Task.CompletedTask);
        await Assert.ThrowsAsync<IOException>(() => installer.RollbackAsync(fixture.Install, fixture.Data, expected: current with { Generation = current.Generation + 1 }));
        Assert.Equal(current, InstallationLayout.ReadSelection(fixture.Install));
        Assert.Equal("0.1.0", (await installer.RollbackAsync(fixture.Install, fixture.Data, expected: current)).Current.Version);
    }

    [Fact]
    public void DisposableSwitchesAreRefusedForTheDefaultInstallationAndOutsideArtifacts()
    {
        var repo = Fixture.Repository;
        Assert.Throws<InvalidOperationException>(() => DevTargets.Resolve(repo, null, null, null, skipProbe: true));
        Assert.Throws<InvalidOperationException>(() => DevTargets.Resolve(repo, DevTargets.DefaultInstallationRoot, null, null, skipProbe: true));
        Assert.Throws<InvalidOperationException>(() => DevTargets.Resolve(repo, Path.Combine(Path.GetTempPath(), "worklog-install"), null, null, skipProbe: false));
        Assert.Throws<InvalidOperationException>(() => DevTargets.Resolve(repo, Path.Combine(repo, "artifacts", "x"), DevTargets.DefaultDataDirectory, null, skipProbe: false));
        Assert.Throws<InvalidOperationException>(() => DevTargets.Resolve(repo, Path.Combine(repo, "artifacts", "x"), Path.Combine(repo, "artifacts", "d"), Path.GetTempPath(), skipProbe: false));
        var defaults = DevTargets.Resolve(repo, null, null, null, skipProbe: false);
        Assert.True(defaults.IsDefaultInstallation);
        Assert.False(defaults.SkipProbe);
        Assert.True(DevTargets.Resolve(repo, Path.Combine(repo, "artifacts", "x"), Path.Combine(repo, "artifacts", "d"), null, skipProbe: true).SkipProbe);
    }

    [Theory]
    [InlineData("0.1.0", "stable")]
    [InlineData("0.1.9001", "selected")]
    [InlineData("0.1.9002", "previous")]
    [InlineData("0.1.9003", "staged")]
    [InlineData("0.1.9099", "not installed")]
    public async Task RetireRefusesStableSelectedPreviousStagedAndMissingPayloads(string version, string reason)
    {
        using var fixture = new Fixture();
        fixture.AddPayload("0.1.9002", null);
        fixture.AddPayload("0.1.9003", null);
        fixture.AddPayload("0.1.9001", null);
        fixture.SelectWithPrevious("0.1.9001", "0.1.9002");
        if (reason == "staged")
        {
            var stage = Path.Combine(fixture.Data, "updates", "staging", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            AtomicFile.Write(UpdatePlan.StatusFile(fixture.Data, fixture.Install), WorkJson.Encode(new UpdateOutcome("staged", version, "staged", OperationId: Path.GetFileName(stage))));
        }
        var before = fixture.Snapshot();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Installer.RetireAsync(version));
        Assert.Equal(before, fixture.Snapshot());
    }

    [Fact]
    public async Task RetireTreatsAFinishedStagingDirectoryAsUnreferenced()
    {
        using var fixture = new Fixture();
        fixture.AddPayload("0.1.9003", null);
        var stage = Path.Combine(fixture.Data, "updates", "staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        AtomicFile.Write(UpdatePlan.StatusFile(fixture.Data, fixture.Install), WorkJson.Encode(new UpdateOutcome("activated", "0.1.9003", "done", OperationId: Path.GetFileName(stage))));
        await fixture.Installer.RetireAsync("0.1.9003");
        Assert.False(Directory.Exists(Path.Combine(fixture.Install, "versions", "0.1.9003")));
    }

    [Fact]
    public async Task RetireRemovesAnUnmarkedDevRangePayloadAndNothingElse()
    {
        using var fixture = new Fixture();
        fixture.AddPayload("0.1.9001", null);
        fixture.AddPayload("0.1.9002", DevBuildVersionTests.Marker("0.1.0"));
        var selection = InstallationLayout.ReadSelection(fixture.Install);
        var removed = await fixture.Installer.RetireAsync("0.1.9001");
        Assert.EndsWith(Path.Combine("versions", "0.1.9001"), removed);
        Assert.False(Directory.Exists(removed));
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(fixture.Install, "versions"), ".retired-*"));
        Assert.True(Directory.Exists(Path.Combine(fixture.Install, "versions", "0.1.9002")));
        Assert.True(Directory.Exists(Path.Combine(fixture.Install, "versions", "0.1.0")));
        Assert.Equal(selection, InstallationLayout.ReadSelection(fixture.Install));
    }

    [Fact]
    public async Task RetireLeavesAPayloadThatFailsHashVerification()
    {
        using var fixture = new Fixture();
        fixture.AddPayload("0.1.9001", null);
        File.AppendAllText(Path.Combine(fixture.Install, "versions", "0.1.9001", "README.md"), "tampered");
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Installer.RetireAsync("0.1.9001"));
        Assert.True(Directory.Exists(Path.Combine(fixture.Install, "versions", "0.1.9001")));
    }

    [Fact]
    public async Task RetireRefusesAPayloadWhoseExecutableIsRunningAndNeverStopsIt()
    {
        if (!OperatingSystem.IsWindows())
            return;
        using var fixture = new Fixture();
        // A copy of a small console program that stays alive stands in for a running worklog.exe of that payload.
        fixture.AddPayload("0.1.9001", null, Path.Combine(Environment.SystemDirectory, "ping.exe"));
        var executable = Path.Combine(fixture.Install, "versions", "0.1.9001", "worklog.exe");
        using var process = Process.Start(new ProcessStartInfo(executable, "-n 60 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Installer.RetireAsync("0.1.9001"));
            Assert.Contains(process.Id.ToString(), error.Message);
            Assert.False(process.HasExited);
            Assert.True(Directory.Exists(Path.GetDirectoryName(executable)));
        }
        finally
        {
            process.Kill();
            process.WaitForExit();
        }
        await fixture.Installer.RetireAsync("0.1.9001");
        Assert.False(Directory.Exists(Path.GetDirectoryName(executable)));
    }

    private sealed class Fixture : IDisposable
    {
        public static readonly string Repository = FindRepository();
        public string Root { get; } = Path.Combine(Repository, "artifacts", "dev-install-tests", Guid.NewGuid().ToString("N"));
        public string Install => Path.Combine(Root, "install");
        public string Data => Path.Combine(Root, "data");
        public DevTargets Targets
        {
            get;
        }
        public DevInstaller Installer
        {
            get;
        }
        private readonly string commit = GitRepository.Run(Repository, "rev-parse", "HEAD").Trim();

        public Fixture()
        {
            Targets = DevTargets.Resolve(Repository, Install, Data, Path.Combine(Root, "operations"), skipProbe: true);
            Installer = new(Targets);
            AddPayload("0.1.0", null);
            Select("0.1.0", null);
        }

        public static string FindRepository()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "ZeusApps.WorkLog.slnx")))
                    return directory.FullName;
            throw new InvalidOperationException("Repository root not found.");
        }

        public string Package(string version, bool marked = true, string? commit = null, string? builtAt = null)
        {
            var package = Path.Combine(Root, "package-" + Guid.NewGuid().ToString("N"));
            var marker = marked ? DevBuildVersionTests.Marker("0.1.0", commit: commit ?? this.commit) with
            {
                BuiltAt = builtAt ?? DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                // The working tree of this repository is usually dirty; the clean-tree git check is covered with a disposable repository.
                Dirty = true
            } : null;
            AddPayload(version, marker, root: package);
            Select(version, null, package);
            var archive = Path.Combine(Root, "packages", $"worklog-{version}-win-x64.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
            ZipFile.CreateFromDirectory(package, archive);
            File.WriteAllText(archive + ".sha256", ReleasePackage.Hash(archive) + "  " + Path.GetFileName(archive));
            return archive;
        }

        public async Task Activate(string version)
        {
            var installer = new PayloadInstaller((_, _, _) => Task.CompletedTask);
            var package = Path.Combine(Root, "package-" + Guid.NewGuid().ToString("N"));
            AddPayload(version, null, root: package);
            Select(version, null, package);
            var stage = Path.Combine(Data, "updates", "staging", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            var archive = Path.Combine(stage, ReleaseInfo.PackageName(version));
            ZipFile.CreateFromDirectory(package, archive);
            File.WriteAllText(archive + ".sha256", ReleasePackage.Hash(archive) + "  " + Path.GetFileName(archive));
            var current = InstallationLayout.ReadSelection(Install).Current.Version;
            await installer.ActivateAsync(await installer.PrepareDownloadedAsync(Install, Data, current, version, stage));
        }

        public void AddPayload(string version, BuildMarker? marker, string? executable = null, string? root = null)
        {
            root ??= Install;
            var payload = InstallationLayout.PayloadDirectory(root, version);
            Directory.CreateDirectory(payload);
            foreach (var name in new[] { "worklog.exe", "README.md", "INSTALL.md" })
            {
                if (name == "worklog.exe" && executable is not null)
                    File.Copy(executable, Path.Combine(payload, name));
                else
                    File.WriteAllText(Path.Combine(payload, name), version + name);
                File.WriteAllText(Path.Combine(root, name), "bootstrap " + name);
            }
            var hashes = InstallationLayout.PayloadFiles.Take(3).ToDictionary(name => name, name => InstallationLayout.Hash(Path.Combine(payload, name)));
            AtomicFile.Write(Path.Combine(payload, "release.json"), WorkJson.Encode(new PayloadManifest(version, "win-x64", true, 2, 1, 1, 2, hashes, marker)));
            AtomicFile.Write(Path.Combine(root, "launcher.json"), WorkJson.Encode(new LauncherManifest("1.0.0", 1, 1, 2)));
        }

        public void Select(string version, string? previous, string? root = null)
        {
            root ??= Install;
            PayloadReference Reference(string value) => new(value, InstallationLayout.Hash(Path.Combine(InstallationLayout.PayloadDirectory(root, value), "release.json")));
            AtomicFile.Write(Path.Combine(root, "current.json"), WorkJson.Encode(new PayloadSelection(Reference(version),
                previous is null ? null : Reference(previous), 1, Guid.NewGuid().ToString("N"))));
        }

        public void SelectWithPrevious(string version, string previous) => Select(version, previous);

        public string Snapshot() => string.Join("|", new[] { Install, Data }.Where(Directory.Exists).SelectMany(directory =>
            Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
                .Where(file => Path.GetFileName(file) != ".worklog-publish.lock")
                .Select(file => Path.GetRelativePath(Root, file) + ":" + InstallationLayout.Hash(file))));

        public void Dispose()
        {
            if (!DevTargets.Within(Path.Combine(Repository, "artifacts", "dev-install-tests"), Root))
                throw new InvalidOperationException("Invalid test cleanup path.");
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Root, true);
        }
    }
}
