using System.IO.Compression;
using System.Net;
using System.Text.Json;
using WorkLog.Cli;
using WorkLog.Configuration;
using WorkLog.Infrastructure;
using WorkLog.Installation;
using WorkLog.Tests.Testing;
using WorkLog.Updates;
using Xunit;
using WorkJson = WorkLog.Infrastructure.Json;

namespace WorkLog.Tests.Updates;

public sealed class DevBuildVersionTests
{
    private sealed record Manifest030(string Version, string Runtime, bool SelfContained, int PackageFormat,
        int LauncherContract, int SharedDataContract, int JournalSchema, Dictionary<string, string> Files);

    public static BuildMarker Marker(string baseVersion = "0.3.0", bool dirty = false, string commit = "50df7b8d1c2e3f4a5b6c7d8e9f00112233445566") =>
        new("dev", commit, dirty, "2026-10-28T09:12:44Z", baseVersion);

    [Theory]
    [InlineData("0.3.8999", false)]
    [InlineData("0.3.9000", true)]
    [InlineData("0.3.9001", true)]
    [InlineData("1.0.12345", true)]
    [InlineData("0.3.0", false)]
    [InlineData("0.3.09001", false)]
    [InlineData("0.3.9001-dev", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void DevRangeStartsAtPatch9000(string? version, bool dev) => Assert.Equal(dev, InstallationLayout.IsDevVersion(version));

    [Theory]
    [InlineData("0.3.0", null, "0.3.0")]
    [InlineData("0.3.8999", null, "0.3.8999")]
    [InlineData("0.3.9001", null, "0.3.0")]
    [InlineData("2.7.9000", null, "2.7.0")]
    [InlineData("0.3.9001", "0.3.0", "0.3.0")]
    [InlineData("0.3.9001", "0.3.2", "0.3.2")]
    [InlineData("0.3.9001", "0.4.0", "0.3.0")]
    [InlineData("0.3.9001", "0.3.9000", "0.3.0")]
    [InlineData("0.3.9001", "junk", "0.3.0")]
    public void EffectiveVersionIsTheBaseOfADevBuild(string version, string? baseVersion, string expected)
    {
        var manifest = baseVersion is null ? null : Manifest(version, Marker(baseVersion));
        Assert.Equal(expected, InstallationLayout.EffectiveVersion(version, manifest));
    }

    [Fact]
    public void StableManifestMarkerIsIgnored()
    {
        Assert.Equal("0.3.1", InstallationLayout.EffectiveVersion("0.3.1", Manifest("0.3.1", Marker())));
        Assert.Equal("0.3.1", VersionDisplay.Format("0.3.1", Marker()));
    }

    [Fact]
    public void ManifestWithBuildIsReadByTheOldManifestOptionsAndOldManifestHasNoBuild()
    {
        var withBuild = WorkJson.Encode(Manifest("0.3.9001", Marker()));
        var old = JsonSerializer.Deserialize<Manifest030>(withBuild, InstallationLayout.JsonOptions)!;
        Assert.Equal("0.3.9001", old.Version);
        Assert.Equal(2, old.PackageFormat);
        var current = JsonSerializer.Deserialize<PayloadManifest>(withBuild, InstallationLayout.JsonOptions)!;
        Assert.Equal(Marker(), current.Build);
        var oldText = JsonSerializer.Serialize(old, InstallationLayout.JsonOptions);
        Assert.Null(JsonSerializer.Deserialize<PayloadManifest>(oldText, InstallationLayout.JsonOptions)!.Build);
        using var fixture = new Fixture("0.3.9001", Marker());
        var read = InstallationLayout.ReadPayload(InstallationLayout.PayloadDirectory(fixture.Install, "0.3.9001"));
        Assert.Equal("dev", read.Build!.Channel);
        Assert.Equal("0.3.0", read.Build.BaseVersion);
    }

    [Fact]
    public void ManifestFieldNamesMatchWhatThePackagerWrites()
    {
        const string json = """{"version":"0.3.9001","runtime":"win-x64","selfContained":true,"packageFormat":2,"launcherContract":1,"sharedDataContract":1,"journalSchema":2,"files":{},"build":{"channel":"dev","commit":"50df7b8","dirty":true,"builtAt":"2026-10-28T09:12:44Z","baseVersion":"0.3.0"}}""";
        var manifest = JsonSerializer.Deserialize<PayloadManifest>(json, InstallationLayout.JsonOptions)!;
        Assert.Equal(new BuildMarker("dev", "50df7b8", true, "2026-10-28T09:12:44Z", "0.3.0"), manifest.Build);
    }

    [Theory]
    [InlineData("0.3.0", false, "0.3.0")]
    [InlineData("0.3.9001", false, "0.3.9001 dev 50df7b8 (2026-10-28 09:12 UTC)")]
    [InlineData("0.3.9001", true, "0.3.9001 dev 50df7b8 dirty (2026-10-28 09:12 UTC)")]
    public void LabelsStableMarkedAndDirtyBuilds(string version, bool dirty, string expected)
    {
        Assert.Equal(expected, VersionDisplay.Format(version, version == "0.3.0" ? null : Marker(dirty: dirty)));
        Assert.Equal(version, expected.Split(' ')[0]);
    }

    [Fact]
    public void UnmarkedOrMalformedDevRangeIsLabeledUnmarked()
    {
        Assert.Equal("0.3.9001 dev (unmarked)", VersionDisplay.Format("0.3.9001"));
        Assert.Equal("0.3.9001 dev (unmarked)", VersionDisplay.Format("0.3.9001", Marker() with
        {
            Channel = "stable"
        }));
        Assert.Equal("0.3.9001 dev (unmarked)", VersionDisplay.Format("0.3.9001", Marker() with
        {
            Commit = "xyz"
        }));
        Assert.Equal("0.3.9001 dev (unmarked)", VersionDisplay.Format("0.3.9001", Marker() with
        {
            BuiltAt = "yesterday"
        }));
        Assert.Equal("0.3.9001 dev (unmarked)", VersionDisplay.Format("0.3.9001", Marker(baseVersion: "0.4.0")));
    }

    [Fact]
    public void LabelOfAnInstalledPayloadReadsItsManifest()
    {
        using var fixture = new Fixture("0.3.9001", Marker(dirty: true));
        fixture.AddPayload("0.3.9002", null);
        Assert.Equal("0.3.9001 dev 50df7b8 dirty (2026-10-28 09:12 UTC)", VersionDisplay.ForInstalled(fixture.Install, "0.3.9001"));
        Assert.Equal("0.3.9002 dev (unmarked)", VersionDisplay.ForInstalled(fixture.Install, "0.3.9002"));
        Assert.Equal("0.3.9003 dev (unmarked)", VersionDisplay.ForInstalled(fixture.Install, "0.3.9003"));
        Assert.Equal("0.3.0", VersionDisplay.ForInstalled(fixture.Install, "0.3.0"));
    }

    [Theory]
    [InlineData("0.3.0+50df7b8", "0.3.0")]
    [InlineData("0.3.9001-dev+50df7b8", "0.3.9001")]
    [InlineData("0.3.9001-dev+50df7b8.dirty", "0.3.9001")]
    [InlineData("0.3.9001", "0.3.9001")]
    public void InformationalVersionYieldsTheBareVersion(string informational, string expected) =>
        Assert.Equal(expected, ApplicationInfo.ParseVersion(informational));

    [Fact]
    public void StableBuildDisplaysItsPlainVersion() => Assert.Equal(ApplicationInfo.Version, ApplicationInfo.DisplayVersion);

    [Theory]
    [InlineData("0.3.1", true)]
    [InlineData("0.4.0", true)]
    [InlineData("0.3.0", false)]
    [InlineData("0.2.9", false)]
    [InlineData("0.3.9005", false)]
    public void UnmarkedAndMarkedDevSelectionsOfferOnlyStableReleasesAboveTheirBase(string published, bool offered)
    {
        foreach (var marker in new BuildMarker?[] { null, Marker() })
        {
            using var fixture = new Fixture("0.3.9001", marker);
            var discovery = fixture.Discovery();
            AtomicFile.Write(discovery.CachePath, WorkJson.Encode(new UpdateCache(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DiscoveryTests.Release(published))));
            Assert.Equal(offered, discovery.GetHint("0.3.9001") is not null);
            Assert.Equal(offered ? published : null, discovery.GetHint("0.3.9001")?.AvailableVersion);
        }
    }

    [Fact]
    public void ABaseVersionAboveZeroRaisesTheComparison()
    {
        using var fixture = new Fixture("0.3.9001", Marker("0.3.2"));
        var discovery = fixture.Discovery();
        AtomicFile.Write(discovery.CachePath, WorkJson.Encode(new UpdateCache(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DiscoveryTests.Release("0.3.2"))));
        Assert.Null(discovery.GetHint("0.3.9001"));
        AtomicFile.Write(discovery.CachePath, WorkJson.Encode(new UpdateCache(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DiscoveryTests.Release("0.3.3"))));
        Assert.Equal("0.3.3", discovery.GetHint("0.3.9001")!.AvailableVersion);
    }

    [Fact]
    public void DevRangeReleasesAreInvalidOffers()
    {
        Assert.False(DiscoveryTests.Release("0.3.9005").IsValid());
        Assert.True(DiscoveryTests.Release("0.3.1").IsValid());
    }

    [Fact]
    public async Task PublishedDevRangeReleaseIsNotOffered()
    {
        using var handler = new DiscoveryTests.Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(DiscoveryTests.Metadata("0.3.9005"))
        }));
        using var http = new HttpClient(handler);
        var check = await new ReleaseClient(http).CheckAsync(CancellationToken.None);
        Assert.Null(check.Release);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StableReleaseActivatesOverSelectedDevBuildAndRecordsItAsPrevious(bool marked)
    {
        using var fixture = new Fixture("0.3.9001", marked ? Marker() : null);
        var installer = new PayloadInstaller((_, _, _) => Task.CompletedTask);
        var before = InstallationLayout.ReadSelection(fixture.Install);
        var prepared = await installer.PrepareDownloadedAsync(fixture.Install, fixture.Box.Data, "0.3.9001", "0.3.1", fixture.Stage("0.3.1"));
        var activated = await installer.ActivateAsync(prepared);
        Assert.Equal("0.3.1", activated.Current.Version);
        Assert.Equal(before.Current, activated.Previous);
        var rolled = await installer.RollbackAsync(fixture.Install, fixture.Box.Data);
        Assert.Equal("0.3.9001", rolled.Current.Version);
    }

    [Theory]
    [InlineData("0.3.0")]
    [InlineData("0.2.5")]
    public async Task ReleaseNotAboveTheBaseIsStillRefusedOverADevBuild(string version)
    {
        using var fixture = new Fixture("0.3.9001", Marker());
        var installer = new PayloadInstaller((_, _, _) => Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            installer.PrepareDownloadedAsync(fixture.Install, fixture.Box.Data, "0.3.9001", version, fixture.Stage(version)));
        Assert.Equal("0.3.9001", InstallationLayout.ReadSelection(fixture.Install).Current.Version);
    }

    [Fact]
    public async Task DownloadRejectsADevRangeOfferBeforeFetching()
    {
        using var fixture = new Fixture("0.3.9001", Marker());
        var requests = 0;
        using var http = new HttpClient(new DiscoveryTests.Handler((_, _) => { requests++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)); }));
        var installer = new PayloadInstaller((_, _, _) => Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() => installer.DownloadAsync(new ReleaseClient(http), fixture.Install, fixture.Box.Data,
            "0.3.9001", DiscoveryTests.Release("0.3.9005")));
        Assert.Equal(0, requests);
    }

    [Fact]
    public async Task DevCandidateActivatesOverStableAndOverAnEarlierDevBuild()
    {
        using var fixture = new Fixture("0.3.0", null);
        var installer = new PayloadInstaller((_, _, _) => Task.CompletedTask);
        var first = await installer.ActivateAsync(await installer.PrepareDownloadedAsync(fixture.Install, fixture.Box.Data, "0.3.0", "0.3.9001", fixture.Stage("0.3.9001", Marker())));
        Assert.Equal("0.3.0", first.Previous!.Version);
        var second = await installer.ActivateAsync(await installer.PrepareDownloadedAsync(fixture.Install, fixture.Box.Data, "0.3.9001", "0.3.9002", fixture.Stage("0.3.9002", Marker())));
        Assert.Equal("0.3.9001", second.Previous!.Version);
        Assert.Equal("0.3.9002", second.Current.Version);
    }

    private static PayloadManifest Manifest(string version, BuildMarker? marker) =>
        new(version, "win-x64", true, 2, 1, 1, 2, new()
        {
            ["worklog.exe"] = new string('a', 64),
            ["README.md"] = new string('b', 64),
            ["INSTALL.md"] = new string('c', 64)
        }, marker);

    private sealed class Fixture : IDisposable
    {
        public TestWorkspace Box { get; } = new();
        public string Install => Path.Combine(Box.Root, "install");
        public Fixture(string version, BuildMarker? marker)
        {
            AddPayload(version, marker, root: Install);
            Select(Install, version);
        }
        public UpdateDiscovery Discovery()
        {
            var http = new HttpClient(new DiscoveryTests.Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
            return new UpdateDiscovery(new LocalSettings(Box.Data), new ReleaseClient(http), installationRoot: Install);
        }
        public void AddPayload(string version, BuildMarker? marker, string? root = null)
        {
            root ??= Install;
            var payload = InstallationLayout.PayloadDirectory(root, version);
            Directory.CreateDirectory(payload);
            foreach (var name in new[] { "worklog.exe", "README.md", "INSTALL.md" })
            {
                File.WriteAllText(Path.Combine(payload, name), version + name);
                File.WriteAllText(Path.Combine(root, name), "bootstrap " + name);
            }
            var hashes = InstallationLayout.PayloadFiles.Take(3).ToDictionary(name => name, name => InstallationLayout.Hash(Path.Combine(payload, name)));
            AtomicFile.Write(Path.Combine(payload, "release.json"), WorkJson.Encode(new PayloadManifest(version, "win-x64", true, 2, 1, 1, 2, hashes, marker)));
            AtomicFile.Write(Path.Combine(root, "launcher.json"), WorkJson.Encode(new LauncherManifest("1.0.0", 1, 1, 2)));
        }
        private static void Select(string root, string version) => AtomicFile.Write(Path.Combine(root, "current.json"), WorkJson.Encode(new PayloadSelection(
            new(version, InstallationLayout.Hash(Path.Combine(InstallationLayout.PayloadDirectory(root, version), "release.json"))), null, 1, Guid.NewGuid().ToString("N"))));
        public string Stage(string version, BuildMarker? marker = null)
        {
            var package = Path.Combine(Box.Root, "package-" + Guid.NewGuid().ToString("N"));
            AddPayload(version, marker, package);
            Select(package, version);
            var stage = Path.Combine(Box.Data, "updates", "staging", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            var archive = Path.Combine(stage, ReleaseInfo.PackageName(version));
            ZipFile.CreateFromDirectory(package, archive);
            File.WriteAllText(archive + ".sha256", ReleasePackage.Hash(archive) + "  " + Path.GetFileName(archive));
            return stage;
        }
        public void Dispose() => Box.Dispose();
    }
}
