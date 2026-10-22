using System.IO.Compression;
using System.Net;
using System.Text.Json.Nodes;
using WorkLog.Configuration;
using WorkLog.Infrastructure;
using WorkLog.Installation;
using WorkLog.Tests.Testing;
using WorkLog.Updates;
using Xunit;
using WorkJson = WorkLog.Infrastructure.Json;

namespace WorkLog.Tests.Updates;

public sealed class VersionedUpdateTests
{
    [Fact]
    public void SelectionPinsManifestAndRejectsMissingContractsAndEscape()
    {
        using var fixture = new Fixture();
        var selected = InstallationLayout.ReadSelection(fixture.Install);
        Assert.Equal(fixture.Install, InstallationLayout.FindRoot(Path.Combine(fixture.Install, "versions", "0.1.0", "worklog.exe")));
        if (OperatingSystem.IsWindows())
            Assert.Equal(fixture.Install.ToUpperInvariant(), InstallationLayout.FindRoot(Path.Combine(fixture.Install, "VERSIONS", "0.1.0", "WORKLOG.EXE").ToUpperInvariant()));
        Assert.Null(InstallationLayout.FindRoot(Path.Combine(fixture.Box.Root, "source", "worklog.exe")));
        var path = Path.Combine(fixture.Install, "current.json");
        AtomicFile.Write(path, WorkJson.Encode(selected with
        {
            Current = selected.Current with
            {
                Version = "../../outside"
            }
        }));
        Assert.Throws<InvalidOperationException>(() => InstallationLayout.ReadSelection(fixture.Install));
        AtomicFile.Write(path, WorkJson.Encode(selected));
        File.AppendAllText(Path.Combine(fixture.Install, "versions", "0.1.0", "worklog.exe"), "tampered");
        Assert.Throws<InvalidOperationException>(() => InstallationLayout.ReadSelection(fixture.Install));
        AtomicFile.Write(Path.Combine(fixture.Install, "launcher.json"), "{\"version\":\"1.0.0\"}");
        Assert.Throws<InvalidOperationException>(() => InstallationLayout.ReadLauncher(fixture.Install));
    }

    [Theory]
    [InlineData("../escape", 0)]
    [InlineData("versions/0.2.0/../../../escape", 0)]
    [InlineData("versions/0.2.0/WORKLOG.EXE", 0)]
    [InlineData("versions/0.2.0/link", -1610612736)]
    [InlineData("journal.db", 0)]
    public async Task NestedArchiveRejectsUnsafeAndCollidingEntries(string entryName, int attributes)
    {
        using var fixture = new Fixture();
        var stage = fixture.Stage("0.2.0");
        var archive = Path.Combine(stage, ReleaseInfo.PackageName("0.2.0"));
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        {
            var entry = zip.CreateEntry(entryName);
            entry.ExternalAttributes = attributes;
            using var writer = new StreamWriter(entry.Open());
            writer.Write("bad");
        }
        File.WriteAllText(archive + ".sha256", ReleasePackage.Hash(archive) + "  " + Path.GetFileName(archive));
        await Assert.ThrowsAsync<IOException>(() => fixture.Installer.PrepareDownloadedAsync(fixture.Install, fixture.Box.Data, "0.1.0", "0.2.0", stage));
        Assert.Equal("0.1.0", InstallationLayout.ReadSelection(fixture.Install).Current.Version);
        Assert.False(File.Exists(Path.Combine(fixture.Box.Root, "escape")));
    }

    [Fact]
    public async Task ActivationRetainsPayloadsAndRollbackRejectsStaleGeneration()
    {
        using var fixture = new Fixture();
        var first = await fixture.Prepare("0.2.0");
        var newer = await fixture.Prepare("0.3.0");
        var activated = await fixture.Installer.ActivateAsync(newer);
        Assert.Equal("0.3.0", activated.Current.Version);
        Assert.Equal("0.3.0", (await fixture.Installer.ActivateAsync(first)).Current.Version);
        var stale = await fixture.Prepare("0.4.0", "0.3.0");
        var restored = await fixture.Installer.RollbackAsync(fixture.Install, fixture.Box.Data);
        Assert.Equal("0.1.0", restored.Current.Version);
        await Assert.ThrowsAsync<IOException>(() => fixture.Installer.ActivateAsync(stale));
        Assert.Equal(restored, InstallationLayout.ReadSelection(fixture.Install));
        Assert.True(Directory.Exists(Path.Combine(fixture.Install, "versions", "0.3.0")));
        Assert.Equal("unowned", File.ReadAllText(Path.Combine(fixture.Install, "keep.txt")));
    }

    [Fact]
    public async Task InterruptionsPreserveSelectionAndCommittedPointerOutranksStagedOutcome()
    {
        using var fixture = new Fixture();
        var prepared = await fixture.Prepare("0.2.0");
        var before = InstallationLayout.ReadSelection(fixture.Install);
        var failsBefore = new PayloadInstaller((_, _, _) => Task.CompletedTask)
        {
            ObserveStage = stage => { if (stage == "before-selection") throw new IOException("injected interruption"); }
        };
        await Assert.ThrowsAsync<IOException>(() => failsBefore.ActivateAsync(prepared));
        Assert.Equal(before, InstallationLayout.ReadSelection(fixture.Install));
        var failsAfter = new PayloadInstaller((_, _, _) => Task.CompletedTask)
        {
            ObserveStage = stage => { if (stage == "after-selection") throw new IOException("injected interruption"); }
        };
        await Assert.ThrowsAsync<IOException>(() => failsAfter.ActivateAsync(prepared));
        var actual = InstallationLayout.ReadSelection(fixture.Install);
        Assert.Equal(prepared.OperationId, actual.OperationId);
        Assert.Equal("0.2.0", actual.Current.Version);
        Assert.Equal(before, InstallationLayout.Read<PayloadSelection>(Path.Combine(fixture.Install, "current.previous.json")));
        Assert.NotEqual("activated", UpdateInstaller.ReadOutcome(fixture.Box.Data, fixture.Install)!.State);
    }

    [Fact]
    public async Task CorruptArchiveOrPublishedDirectoryCannotChangeSelection()
    {
        using var fixture = new Fixture();
        var prepared = await fixture.Prepare("0.2.0");
        var partial = Path.Combine(fixture.Install, "versions", "0.2.0");
        Directory.CreateDirectory(partial);
        await Assert.ThrowsAsync<FileNotFoundException>(() => fixture.Installer.ActivateAsync(prepared));
        Assert.Equal("0.1.0", InstallationLayout.ReadSelection(fixture.Install).Current.Version);
        File.AppendAllText(Path.Combine(prepared.StageDirectory, ReleaseInfo.PackageName("0.2.0")), "corrupt");
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Installer.ActivateAsync(prepared));
        Assert.Equal("0.1.0", InstallationLayout.ReadSelection(fixture.Install).Current.Version);
    }

    [Fact]
    public async Task PublisherCancellationAndPermanentAccessFailureAreBounded()
    {
        using var fixture = new Fixture();
        using (var held = await PayloadInstaller.AcquirePublisherAsync(fixture.Install))
        {
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PayloadInstaller.AcquirePublisherAsync(fixture.Install, cancel.Token));
        }
        var path = Path.Combine(fixture.Install, ".worklog-publish.lock");
        File.SetAttributes(path, FileAttributes.ReadOnly);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => PayloadInstaller.AcquirePublisherAsync(fixture.Install));
    }

    [Fact]
    public async Task OldServerHintsRereadSelectionAndPreserveCacheRules()
    {
        using var fixture = new Fixture();
        using var http = new HttpClient(new DiscoveryTests.Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var discovery = new UpdateDiscovery(new LocalSettings(fixture.Box.Data), new ReleaseClient(http), installationRoot: fixture.Install);
        AtomicFile.Write(discovery.CachePath, WorkJson.Encode(new UpdateCache(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DiscoveryTests.Release("0.2.0"))));
        var hint = discovery.GetHint("0.1.0")!;
        Assert.Equal("0.1.0", hint.SelectedVersion);
        Assert.Contains(Path.Combine(fixture.Install, "worklog.exe"), hint.Command);
        Assert.Contains(fixture.Box.Data, hint.Command);
        await fixture.Installer.ActivateAsync(await fixture.Prepare("0.2.0"));
        Assert.Null(discovery.GetHint("0.1.0"));
        File.WriteAllText(Path.Combine(fixture.Install, "current.json"), "broken");
        Assert.Null(discovery.GetHint("0.1.0"));
    }

    [Fact]
    public async Task FailedDownloadIsRecordedWithoutChangingSelection()
    {
        using var fixture = new Fixture();
        using var http = new HttpClient(new DiscoveryTests.Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        var before = InstallationLayout.ReadSelection(fixture.Install);
        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Installer.DownloadAsync(new ReleaseClient(http), fixture.Install,
            fixture.Box.Data, "0.1.0", DiscoveryTests.Release("0.2.0")));
        Assert.Equal(before, InstallationLayout.ReadSelection(fixture.Install));
        Assert.Equal("failed", UpdateInstaller.ReadOutcome(fixture.Box.Data, fixture.Install)!.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RollbackRefusesMissingOrCorruptPreviousPayload(bool corrupt)
    {
        using var fixture = new Fixture();
        await fixture.Installer.ActivateAsync(await fixture.Prepare("0.2.0"));
        var before = InstallationLayout.ReadSelection(fixture.Install);
        var prior = Path.Combine(fixture.Install, "versions", "0.1.0", "worklog.exe");
        if (corrupt)
            File.AppendAllText(prior, "corrupt");
        else
            File.Delete(prior);
        var error = await Record.ExceptionAsync(() => fixture.Installer.RollbackAsync(fixture.Install, fixture.Box.Data));
        Assert.True(error is IOException or InvalidOperationException);
        Assert.Equal(before, InstallationLayout.ReadSelection(fixture.Install));
    }

    [Theory]
    [InlineData("launcherContract", 2)]
    [InlineData("sharedDataContract", 2)]
    [InlineData("journalSchema", 3)]
    [InlineData("packageFormat", 0)]
    public void IncompatiblePayloadDeclarationsFail(string property, int value)
    {
        using var fixture = new Fixture();
        var payload = Path.Combine(fixture.Install, "versions", "0.1.0");
        var path = Path.Combine(payload, "release.json");
        var manifest = JsonNode.Parse(File.ReadAllText(path))!;
        manifest[property] = value;
        File.WriteAllText(path, manifest.ToJsonString());
        Assert.Throws<InvalidOperationException>(() => InstallationLayout.ReadPayload(payload));
    }

    private sealed class Fixture : IDisposable
    {
        public TestWorkspace Box { get; } = new();
        public string Install => Path.Combine(Box.Root, "install");
        public PayloadInstaller Installer { get; } = new((_, _, _) => Task.CompletedTask);
        public Fixture()
        {
            MakeInstallation(Install, "0.1.0");
            File.WriteAllText(Path.Combine(Install, "keep.txt"), "unowned");
        }
        public string Stage(string version)
        {
            var package = Path.Combine(Box.Root, "package-" + Guid.NewGuid().ToString("N"));
            MakeInstallation(package, version);
            var stage = Path.Combine(Box.Data, "updates", "staging", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            var archive = Path.Combine(stage, ReleaseInfo.PackageName(version));
            ZipFile.CreateFromDirectory(package, archive);
            File.WriteAllText(archive + ".sha256", ReleasePackage.Hash(archive) + "  " + Path.GetFileName(archive));
            return stage;
        }
        public Task<PreparedPayload> Prepare(string version, string running = "0.1.0") =>
            Installer.PrepareDownloadedAsync(Install, Box.Data, running, version, Stage(version));
        private static void MakeInstallation(string root, string version)
        {
            var payload = InstallationLayout.PayloadDirectory(root, version);
            Directory.CreateDirectory(payload);
            foreach (var name in new[] { "worklog.exe", "README.md", "INSTALL.md" })
                File.WriteAllText(Path.Combine(payload, name), version + name);
            foreach (var name in new[] { "worklog.exe", "README.md", "INSTALL.md" })
                File.WriteAllText(Path.Combine(root, name), "bootstrap " + name);
            var hashes = InstallationLayout.PayloadFiles.Take(3).ToDictionary(name => name, name => InstallationLayout.Hash(Path.Combine(payload, name)));
            AtomicFile.Write(Path.Combine(payload, "release.json"), WorkJson.Encode(new PayloadManifest(version, "win-x64", true, 2, 1, 1, 2, hashes)));
            AtomicFile.Write(Path.Combine(root, "launcher.json"), WorkJson.Encode(new LauncherManifest("1.0.0", 1, 1, 2)));
            AtomicFile.Write(Path.Combine(root, "current.json"), WorkJson.Encode(new PayloadSelection(new(version, InstallationLayout.Hash(Path.Combine(payload, "release.json"))), null, 1, Guid.NewGuid().ToString("N"))));
        }
        public void Dispose() => Box.Dispose();
    }
}
