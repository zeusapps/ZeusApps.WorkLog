using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using WorkLog.Configuration;
using WorkLog.Dashboard;
using WorkLog.Journaling;
using WorkLog.Persistence;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Dashboard;

public sealed class DashboardLiveCoordinatorTests
{
    [Fact]
    public async Task SameMetadataDocumentHintRevalidatesOnlyCachedBodyAndPublishesRevision()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var path = DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n\nOriginal\n");
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "live-doc", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            using var client = await AuthorizedClient(app, authorization);
            var catalog = await client.GetFromJsonAsync<JsonElement>("/api/catalog");
            var sourceId = catalog.GetProperty("sources")[0].GetProperty("id").GetString()!;
            var request = new DocumentRequest(sourceId, "demo", "design.md");
            var original = await client.PostAsJsonAsync("/api/document", request);
            var originalDocument = (await original.Content.ReadFromJsonAsync<DocumentContent>())!;
            var timestamp = File.GetLastWriteTimeUtc(path);
            var changedText = "# Design\n\nUpdated!\n";
            Assert.Equal(new FileInfo(path).Length, System.Text.Encoding.UTF8.GetByteCount(changedText));
            File.WriteAllText(path, changedText);
            File.SetLastWriteTimeUtc(path, timestamp);

            var watch = changes.Roots.Where(root => (root.Domains & DashboardChangeDomain.Documents) != 0)
                .Where(root => IsWithin(path, root.CanonicalPath)).OrderByDescending(root => root.CanonicalPath.Length).First();
            var relative = Path.GetRelativePath(watch.CanonicalPath, path).Replace('\\', '/');
            var before = coordinator.CurrentSnapshot.Documents;
            changes.Emit(new(DashboardChangeDomain.Documents, watch.Id, relative));
            await WaitUntil(() => coordinator.CurrentSnapshot.Documents > before && coordinator.CurrentSnapshot.Dirty.Length == 0);

            var refreshed = await client.PostAsJsonAsync("/api/document", request with { KnownRevision = originalDocument.Revision });
            var document = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(document.TryGetProperty("unchanged", out _));
            Assert.Equal(changedText, document.GetProperty("text").GetString());
        }
        finally
        {
            await app.StopAsync();
            Assert.True(changes.Disposed);
        }
    }

    [Fact]
    public async Task HintBurstPromotesOneBoundedBatchAndReportsSanitizedRoots()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n");
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "live-burst", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            var watch = changes.Roots.First(root => (root.Domains & DashboardChangeDomain.Documents) != 0);
            var previousOverflows = coordinator.OverflowBatchCount;
            for (var i = 0; i < 4096; i++)
                changes.Emit(new(DashboardChangeDomain.Documents, watch.Id, $"burst-{i:D4}.md"));
            await WaitUntil(() => coordinator.OverflowBatchCount > previousOverflows && coordinator.CurrentSnapshot.Dirty.Length == 0);

            Assert.Equal(128, coordinator.MaxPendingHintCount);
            Assert.InRange(coordinator.PendingHintCount, 0, 129);
            Assert.Equal(128, coordinator.LastBatchHintHighWater);
            Assert.True(coordinator.LastBatchOverflow);
            Assert.InRange(coordinator.LastBatchQuietDelayMilliseconds, 40, 250);
            Assert.Contains(coordinator.WatchRoots, root => root.Id == watch.Id && root.Domains.Contains("documents"));
            Assert.DoesNotContain("CanonicalPath", string.Join(',', typeof(DashboardWatchRootDiagnostic).GetProperties().Select(property => property.Name)));
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task InjectedClockEnforcesFiftyMillisecondDebounceAndTwoHundredFiftyMillisecondMaximumAge()
    {
        using var box = new TestWorkspace();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "live-fake-clock", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes, timeProvider: clock);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0);
            var beforeDebounce = coordinator.ProcessedBatchCount;
            var debounceStart = clock.GetUtcNow();
            changes.Emit(new(DashboardChangeDomain.Journal, RelativePath: "single"));
            await WaitForTimerAt(clock, debounceStart.AddMilliseconds(50));

            clock.Advance(TimeSpan.FromMilliseconds(49));
            Assert.Equal(beforeDebounce, coordinator.ProcessedBatchCount);
            clock.Advance(TimeSpan.FromMilliseconds(1));
            await WaitUntil(() => coordinator.ProcessedBatchCount > beforeDebounce);
            Assert.Equal(50, coordinator.LastBatchFirstDelayMilliseconds);

            var beforeContinuous = coordinator.ProcessedBatchCount;
            var continuousStart = clock.GetUtcNow();
            changes.Emit(new(DashboardChangeDomain.Journal, RelativePath: "continuous-0"));
            await WaitForTimerAt(clock, continuousStart.AddMilliseconds(50));
            for (var index = 1; index <= 5; index++)
            {
                clock.Advance(TimeSpan.FromMilliseconds(40));
                changes.Emit(new(DashboardChangeDomain.Journal, RelativePath: "continuous-" + index));
                var due = Min(continuousStart.AddMilliseconds(250), clock.GetUtcNow().AddMilliseconds(50));
                await WaitForTimerAt(clock, due);
            }

            clock.Advance(TimeSpan.FromMilliseconds(49));
            Assert.Equal(beforeContinuous, coordinator.ProcessedBatchCount);
            clock.Advance(TimeSpan.FromMilliseconds(1));
            await WaitUntil(() => coordinator.ProcessedBatchCount > beforeContinuous);
            Assert.Equal(250, coordinator.LastBatchFirstDelayMilliseconds);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task InjectedClockPromotesExactlyTheOneHundredTwentyNinthHintToOverflow()
    {
        using var box = new TestWorkspace();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "live-overflow-edge", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes, timeProvider: clock);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0);
            var beforeOverflow = coordinator.OverflowBatchCount;
            var beforeBatches = coordinator.ProcessedBatchCount;
            var batchStarted = clock.GetUtcNow();
            for (var index = 0; index < 128; index++)
                changes.Emit(new(DashboardChangeDomain.Journal, RelativePath: "edge-" + index));

            Assert.Equal(beforeOverflow, coordinator.OverflowBatchCount);
            Assert.Equal(128, coordinator.MaxPendingHintCount);
            await WaitForTimerAt(clock, batchStarted.AddMilliseconds(50));

            changes.Emit(new(DashboardChangeDomain.Journal, RelativePath: "edge-128"));
            Assert.Equal(beforeOverflow + 1, coordinator.OverflowBatchCount);
            Assert.Equal(128, coordinator.MaxPendingHintCount);
            clock.Advance(TimeSpan.FromMilliseconds(50));
            await WaitUntil(() => coordinator.ProcessedBatchCount > beforeBatches);
            Assert.True(coordinator.LastBatchOverflow);
            Assert.Equal(128, coordinator.LastBatchHintHighWater);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task LiveNegotiationRetainsProtectedBoundaryAndDisablesLongPolling()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "live-negotiate", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: new FakeDashboardChangeSource());
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(DashboardHost.Address(app)) };
            var denied = await client.PostAsync("/api/live/negotiate?negotiateVersion=1", new StringContent(""));
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            using var hostile = new HttpRequestMessage(HttpMethod.Post, "/api/live/negotiate?negotiateVersion=1")
            { Content = new StringContent("") };
            hostile.Headers.Add("Origin", "https://other.example");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(hostile)).StatusCode);

            var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
            using var authorized = new HttpClient(handler) { BaseAddress = client.BaseAddress };
            await authorized.PostAsJsonAsync("/api/bootstrap", new { token = authorization.Bootstrap() });
            using var response = await authorized.PostAsync("/api/live/negotiate?negotiateVersion=1", new StringContent(""));
            response.EnsureSuccessStatusCode();
            var negotiation = await response.Content.ReadFromJsonAsync<JsonElement>();
            var transports = negotiation.GetProperty("availableTransports").EnumerateArray()
                .Select(transport => transport.GetProperty("transport").GetString()).ToArray();
            Assert.Contains("WebSockets", transports);
            Assert.Contains("ServerSentEvents", transports);
            Assert.DoesNotContain("LongPolling", transports);

            var connectionToken = negotiation.GetProperty("connectionToken").GetString()!;
            using var sseRequest = new HttpRequestMessage(HttpMethod.Get, "/api/live?id=" + Uri.EscapeDataString(connectionToken));
            sseRequest.Headers.Accept.ParseAdd("text/event-stream");
            using var sseDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var sse = await authorized.SendAsync(sseRequest, HttpCompletionOption.ResponseHeadersRead, sseDeadline.Token);
            Assert.Equal(HttpStatusCode.OK, sse.StatusCode);
            Assert.Equal("text/event-stream", sse.Content.Headers.ContentType?.MediaType);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task LiveTransportRequestsRejectMissingAuthorizationAndForeignOrigins()
    {
        using var box = new TestWorkspace();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "live-transport-boundary",
            authorization, new DashboardTests.FixtureCommand(box.Repo), changeSource: new FakeDashboardChangeSource());
        await app.StartAsync();
        try
        {
            var address = new Uri(DashboardHost.Address(app));
            using var anonymous = new HttpClient { BaseAddress = address };
            using var denied = new HttpRequestMessage(HttpMethod.Get, "/api/live?id=untrusted");
            denied.Headers.Accept.ParseAdd("text/event-stream");
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await anonymous.SendAsync(denied, HttpCompletionOption.ResponseHeadersRead)).StatusCode);

            using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
            using var authorized = new HttpClient(handler) { BaseAddress = address };
            (await authorized.PostAsJsonAsync("/api/bootstrap", new { token = authorization.Bootstrap() })).EnsureSuccessStatusCode();
            using var hostileSse = new HttpRequestMessage(HttpMethod.Get, "/api/live?id=untrusted");
            hostileSse.Headers.Accept.ParseAdd("text/event-stream");
            hostileSse.Headers.Add("Origin", "https://other.example");
            Assert.Equal(HttpStatusCode.Forbidden,
                (await authorized.SendAsync(hostileSse, HttpCompletionOption.ResponseHeadersRead)).StatusCode);

            using var negotiation = await authorized.PostAsync("/api/live/negotiate?negotiateVersion=1", new StringContent(""));
            negotiation.EnsureSuccessStatusCode();
            var connectionToken = (await negotiation.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("connectionToken").GetString()!;
            using var foreignSocket = new ClientWebSocket();
            foreignSocket.Options.SetRequestHeader("Cookie", authorization.CookieName + "=" + authorization.Cookie);
            foreignSocket.Options.SetRequestHeader("Origin", "https://other.example");
            var uri = new UriBuilder(address) { Scheme = "ws", Path = "/api/live", Query = "id=" + Uri.EscapeDataString(connectionToken) }.Uri;
            await Assert.ThrowsAsync<WebSocketException>(() => foreignSocket.ConnectAsync(uri, CancellationToken.None));

            using var wrongHost = new HttpRequestMessage(HttpMethod.Get, "/api/live?id=untrusted");
            wrongHost.Headers.Accept.ParseAdd("text/event-stream");
            wrongHost.Headers.Host = "localhost:" + address.Port;
            Assert.Equal(HttpStatusCode.Forbidden,
                (await authorized.SendAsync(wrongHost, HttpCompletionOption.ResponseHeadersRead)).StatusCode);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task DirectoryLifecycleHintRefreshesArchivedMetadataAndInvalidatesOldBodyCache()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var active = Path.Combine(box.Repo, "openspec", "changes", "demo");
        var archived = Path.Combine(box.Repo, "openspec", "changes", "archive", "2026-11-06-demo");
        var path = DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n\nSame text\n");
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "live-archive", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            using var client = await AuthorizedClient(app, authorization);
            var catalog = await client.GetFromJsonAsync<JsonElement>("/api/catalog");
            var sourceId = catalog.GetProperty("sources")[0].GetProperty("id").GetString()!;
            var request = new DocumentRequest(sourceId, "demo", "design.md");
            var initial = await client.PostAsJsonAsync("/api/document", request);
            Assert.False((await initial.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("archived").GetBoolean());
            var parent = changes.Roots.Where(root => (root.Domains & DashboardChangeDomain.Documents) != 0)
                .Where(root => IsWithin(active, root.CanonicalPath)).OrderByDescending(root => root.CanonicalPath.Length).First();

            Directory.CreateDirectory(Path.GetDirectoryName(archived)!);
            Directory.Move(active, archived);
            var before = coordinator.CurrentSnapshot.Documents;
            changes.Emit(new(DashboardChangeDomain.Documents, parent.Id, "demo", DirectoryChanged: true));
            await WaitUntil(() => coordinator.CurrentSnapshot.Documents > before && coordinator.CurrentSnapshot.Dirty.Length == 0);

            var refreshed = await client.PostAsJsonAsync("/api/document", request);
            var document = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(document.GetProperty("archived").GetBoolean());
            Assert.Equal("# Design\n\nSame text\n", document.GetProperty("text").GetString());
            Assert.False(File.Exists(path));
            Assert.True(File.Exists(Path.Combine(archived, "design.md")));
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task RecursiveFilesystemWatchAllowsExternalArchiveMoveAndRefreshesOpenDocument()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        var active = Path.Combine(box.Repo, "openspec", "changes", "demo");
        var archived = Path.Combine(box.Repo, "openspec", "changes", "archive", "2026-11-09-demo");
        var originalText = "# Design\n\nSame content after archive move\n";
        DashboardTests.Write(box, "openspec/changes/demo/design.md", originalText);
        Directory.CreateDirectory(Path.GetDirectoryName(archived)!);

        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var changes = new FileSystemDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "live-archive-filesystem", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && coordinator.WatchRootIds.Length > 0);
            Assert.True(coordinator.ActiveWatcherCount < coordinator.LogicalWatchRootCount);

            using var client = await AuthorizedClient(app, authorization);
            var catalog = await client.GetFromJsonAsync<JsonElement>("/api/catalog");
            var sourceId = catalog.GetProperty("sources")[0].GetProperty("id").GetString()!;
            var request = new DocumentRequest(sourceId, "demo", "design.md");
            var initial = await client.PostAsJsonAsync("/api/document", request);
            Assert.False((await initial.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("archived").GetBoolean());

            var before = coordinator.CurrentSnapshot;
            Directory.Move(active, archived);
            await WaitUntil(() => coordinator.CurrentSnapshot.Documents > before.Documents
                && coordinator.CurrentSnapshot.Dirty.Length == 0);

            var refreshed = await client.PostAsJsonAsync("/api/document", request);
            Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
            var document = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(document.GetProperty("archived").GetBoolean());
            Assert.Equal(sourceId, document.GetProperty("sourceId").GetString());
            Assert.Equal("demo", document.GetProperty("change").GetString());
            Assert.Equal("design.md", document.GetProperty("path").GetString());
            Assert.False(document.GetProperty("canComment").GetBoolean());
            Assert.Equal(originalText, document.GetProperty("text").GetString());
            Assert.False(Directory.Exists(active));
            Assert.True(File.Exists(Path.Combine(archived, "design.md")));
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task MissingChangeRootIsWatchedAtItsNearestExistingParentAndReboundWhenRestored()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        Directory.CreateDirectory(Path.Combine(box.Repo, "openspec"));
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "live-root-recovery", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            Assert.DoesNotContain(coordinator.WatchRoots,
                root => root.Id == FileSystemDashboardChangeSource.WatchId(Path.Combine(box.Repo, "openspec", "changes")));
            var recovery = changes.Roots.Where(root => (root.Domains & DashboardChangeDomain.Documents) != 0)
                .Where(root => IsWithin(Path.Combine(box.Repo, "openspec"), root.CanonicalPath))
                .OrderByDescending(root => root.CanonicalPath.Length).First();
            var before = coordinator.CurrentSnapshot.Documents;
            DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Restored\n");
            changes.Emit(new(DashboardChangeDomain.Documents, recovery.Id, "changes", DirectoryChanged: true));
            await WaitUntil(() => coordinator.CurrentSnapshot.Documents > before && coordinator.CurrentSnapshot.Dirty.Length == 0);

            Assert.Contains(changes.Roots, root => Path.GetFileName(root.CanonicalPath) == "demo"
                && (root.Domains & DashboardChangeDomain.Documents) != 0);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task JournalAndDigestAvailabilityAdvanceIndependentlyAndIgnoreSqliteShmNoise()
    {
        using var box = new TestWorkspace();
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "live-stores", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            var dataRoot = changes.Roots.First(root => Path.GetFullPath(root.CanonicalPath) == Path.GetFullPath(box.Data));
            var initial = coordinator.CurrentSnapshot;

            Directory.CreateDirectory(Path.Combine(box.Data, "journal.db"));
            Directory.CreateDirectory(Path.Combine(box.Data, "digests.db"));
            changes.Emit(new(DashboardChangeDomain.Journal, dataRoot.Id, "journal.db"));
            changes.Emit(new(DashboardChangeDomain.Digests, dataRoot.Id, "digests.db"));
            await WaitUntil(() => coordinator.CurrentSnapshot.Journal > initial.Journal
                && coordinator.CurrentSnapshot.Digests > initial.Digests && coordinator.CurrentSnapshot.Dirty.Length == 0);
            var unavailable = coordinator.CurrentSnapshot;

            Directory.Delete(Path.Combine(box.Data, "journal.db"));
            Directory.Delete(Path.Combine(box.Data, "digests.db"));
            new Journal(new LocalSettings(box.Data), new("repo")).Log(new("in_progress", "demo", Summary: "observation",
                OccurredAt: "2026-11-06T10:00:00Z"));
            new DigestStore(box.Data).Save(new("item", "2026-11-06", [], "rollup", DateTimeOffset.UtcNow));
            changes.Emit(new(DashboardChangeDomain.Journal, dataRoot.Id, "journal.db-wal"));
            changes.Emit(new(DashboardChangeDomain.Digests, dataRoot.Id, "digests.db-wal"));
            await WaitUntil(() => coordinator.CurrentSnapshot.Journal > unavailable.Journal
                && coordinator.CurrentSnapshot.Digests > unavailable.Digests && coordinator.CurrentSnapshot.Dirty.Length == 0);
            var recovered = coordinator.CurrentSnapshot;

            changes.Emit(new(DashboardChangeDomain.Journal, dataRoot.Id, "journal.db-shm"));
            changes.Emit(new(DashboardChangeDomain.Digests, dataRoot.Id, "digests.db-shm"));
            await Task.Delay(100);
            Assert.Equal(recovered.Journal, coordinator.CurrentSnapshot.Journal);
            Assert.Equal(recovered.Digests, coordinator.CurrentSnapshot.Digests);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task SameMarkerAtomicDatabaseReplacementAdvancesOnlyTheReplacedDomain()
    {
        using var box = new TestWorkspace();
        CreateJournal(box.Data, "same-row", "repo-a");
        var saved = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        new DigestStore(box.Data).Save(new("item", "2026-11-06", [], "one", saved));
        Checkpoint(Path.Combine(box.Data, "journal.db"));
        Checkpoint(Path.Combine(box.Data, "digests.db"));
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "live-replace", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            var dataRoot = changes.Roots.First(root => Path.GetFullPath(root.CanonicalPath) == Path.GetFullPath(box.Data));
            var before = coordinator.CurrentSnapshot;

            var alternateData = Path.Combine(box.Root, "alternate-data");
            Directory.CreateDirectory(alternateData);
            CreateJournal(alternateData, "same-row", "repo-b");
            new DigestStore(alternateData).Save(new("item", "2026-11-06", [], "two", saved));
            Checkpoint(Path.Combine(alternateData, "journal.db"));
            Checkpoint(Path.Combine(alternateData, "digests.db"));
            ReplaceKeepingMarkers(Path.Combine(alternateData, "journal.db"), Path.Combine(box.Data, "journal.db"));
            ReplaceKeepingMarkers(Path.Combine(alternateData, "digests.db"), Path.Combine(box.Data, "digests.db"));

            changes.Emit(new(DashboardChangeDomain.Journal, dataRoot.Id, "journal.db", FileLifecycleChanged: true));
            changes.Emit(new(DashboardChangeDomain.Digests, dataRoot.Id, "digests.db", FileLifecycleChanged: true));
            await WaitUntil(() => coordinator.CurrentSnapshot.Journal > before.Journal
                && coordinator.CurrentSnapshot.Digests > before.Digests
                && coordinator.CurrentSnapshot.Catalog > before.Catalog && coordinator.CurrentSnapshot.Dirty.Length == 0);
            Assert.True(coordinator.CurrentSnapshot.Catalog > before.Catalog);
            var catalog = await app.Services.GetRequiredService<SourceCatalog>().GetAsync();
            Assert.Contains(catalog.Repositories, repository => repository.Id == "repo-b");
            Assert.DoesNotContain(catalog.Repositories, repository => repository.Id == "repo-a");
            Assert.Equal(before.Documents, coordinator.CurrentSnapshot.Documents);
            Assert.Equal(before.Reads, coordinator.CurrentSnapshot.Reads);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task MissedJournalReplacementIsDetectedByPeriodicCheckpointAndRefreshesRepositoryCatalog()
    {
        using var box = new TestWorkspace();
        CreateJournal(box.Data, "same-row", "repo-a");
        Checkpoint(Path.Combine(box.Data, "journal.db"));
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "live-missed-journal-hint", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes, timeProvider: clock);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            var catalogService = app.Services.GetRequiredService<SourceCatalog>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            var before = coordinator.CurrentSnapshot;
            var initialCatalog = await catalogService.GetAsync();
            Assert.Contains(initialCatalog.Repositories, repository => repository.Id == "repo-a");

            var address = new Uri(DashboardHost.Address(app));
            using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
            using var client = new HttpClient(handler) { BaseAddress = address };
            (await client.PostAsJsonAsync("/api/bootstrap", new { token = authorization.Bootstrap() })).EnsureSuccessStatusCode();
            var cookie = handler.CookieContainer.GetCookies(address)[authorization.CookieName]!.ToString();
            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Cookie", cookie);
            socket.Options.SetRequestHeader("Origin", address.GetLeftPart(UriPartial.Authority));
            await socket.ConnectAsync(new UriBuilder(address) { Scheme = "ws", Path = "/api/live" }.Uri, CancellationToken.None);
            await SendSignalRFrame(socket, "{\"protocol\":\"json\",\"version\":1}\u001e");
            Assert.Equal("{}", await ReceiveSignalRFrame(socket));
            await SendSignalRFrame(socket, "{\"type\":1,\"invocationId\":\"snapshot\",\"target\":\"Snapshot\",\"arguments\":[]}\u001e");
            using (var snapshot = JsonDocument.Parse(await ReceiveSignalRFrame(socket)))
                Assert.False(snapshot.RootElement.GetProperty("result").TryGetProperty("checkpoint", out _));
            await WaitUntil(() => coordinator.ActiveConnectionCount == 1);

            var alternateData = Path.Combine(box.Root, "replacement-data");
            CreateJournal(alternateData, "same-row", "repo-b");
            Checkpoint(Path.Combine(alternateData, "journal.db"));
            var journalPath = Path.Combine(box.Data, "journal.db");
            var lastWrite = File.GetLastWriteTimeUtc(journalPath);
            var staging = journalPath + ".missed-hint-replacement";
            File.Copy(Path.Combine(alternateData, "journal.db"), staging);
            File.SetLastWriteTimeUtc(staging, lastWrite.AddMinutes(1));
            File.Move(staging, journalPath, overwrite: true);
            Assert.Equal(1, new DashboardJournal(box.Data).Snapshot());

            // Read continuously across both manual ticks so the socket send loop can drain the first (pre-reconcile)
            // checkpoint before its bounded send deadline expires under a busy full-suite scheduler.
            var receivedCheckpoint = ReceiveRevisionAsync(socket, vector => vector.TryGetProperty("checkpoint", out var sequence)
                && sequence.ValueKind == JsonValueKind.Number
                && vector.GetProperty("journal").GetInt64() > before.Journal
                && vector.GetProperty("catalog").GetInt64() > before.Catalog);
            var firstCheckpoint = clock.GetUtcNow().AddSeconds(5);
            await WaitForTimerAt(clock, firstCheckpoint);
            clock.Advance(TimeSpan.FromSeconds(5));
            await WaitUntil(() => coordinator.CurrentSnapshot.Journal > before.Journal
                && coordinator.CurrentSnapshot.Catalog > before.Catalog && coordinator.CurrentSnapshot.Dirty.Length == 0);
            var refreshed = await catalogService.GetAsync();
            Assert.Contains(refreshed.Repositories, repository => repository.Id == "repo-b");
            Assert.DoesNotContain(refreshed.Repositories, repository => repository.Id == "repo-a");

            var secondCheckpoint = clock.GetUtcNow().AddSeconds(5);
            await WaitForTimerAt(clock, secondCheckpoint);
            clock.Advance(TimeSpan.FromSeconds(5));
            using var delivered = await receivedCheckpoint;
            Assert.True(delivered.RootElement.GetProperty("checkpoint").GetInt64() > 0);
            Assert.True(delivered.RootElement.GetProperty("journal").GetInt64() > before.Journal);
            Assert.True(delivered.RootElement.GetProperty("catalog").GetInt64() > before.Catalog);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task InvalidConfigurationNoticePersistsUntilTheWatchedConfigRecovers()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n");
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        settings.Save();
        var valid = File.ReadAllBytes(settings.ConfigPath);
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "live-config", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            var dataRoot = changes.Roots.First(root => Path.GetFullPath(root.CanonicalPath) == Path.GetFullPath(box.Data));
            var starting = coordinator.CurrentSnapshot.Catalog;

            File.WriteAllText(settings.ConfigPath, "{");
            changes.Emit(new(DashboardChangeDomain.Catalog, dataRoot.Id, "config.json"));
            await WaitUntil(() => coordinator.CurrentSnapshot.Catalog > starting && coordinator.CurrentSnapshot.Dirty.Length == 0);
            using var client = await AuthorizedClient(app, authorization);
            var failed = await client.GetFromJsonAsync<JsonElement>("/api/catalog");
            Assert.Contains(failed.GetProperty("notices").EnumerateArray().Select(item => item.GetString()!),
                notice => notice.StartsWith("Repository configuration could not be reloaded;", StringComparison.Ordinal));

            var beforeRecovery = coordinator.CurrentSnapshot.Catalog;
            File.WriteAllBytes(settings.ConfigPath, valid);
            changes.Emit(new(DashboardChangeDomain.Catalog, dataRoot.Id, "config.json"));
            await WaitUntil(() => coordinator.CurrentSnapshot.Catalog > beforeRecovery && coordinator.CurrentSnapshot.Dirty.Length == 0);
            var recovered = await client.GetFromJsonAsync<JsonElement>("/api/catalog");
            Assert.DoesNotContain(recovered.GetProperty("notices").EnumerateArray().Select(item => item.GetString()!),
                notice => notice.StartsWith("Repository configuration could not be reloaded;", StringComparison.Ordinal));
            Assert.Contains(recovered.GetProperty("sources").EnumerateArray(), source => source.GetProperty("error").ValueKind == JsonValueKind.Null);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task WatcherBindingRejectsInitialAndReplacedOpenSpecLinksOutsideTheSource()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        var outsideOpenSpec = Path.Combine(box.Root, "outside-openspec");
        var outsideChanges = Path.Combine(outsideOpenSpec, "changes");
        Directory.CreateDirectory(Path.Combine(outsideChanges, "nested"));
        await File.WriteAllTextAsync(Path.Combine(outsideChanges, "nested", "leak.md"), "outside");
        var openSpec = Path.Combine(box.Repo, "openspec");
        string? linkedChangesPath = null;
        await CreateDirectoryLink(openSpec, outsideOpenSpec, box.Repo);

        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "live-watch-containment", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            Assert.DoesNotContain(changes.Roots, root => IsWithin(root.CanonicalPath, outsideOpenSpec));

            Directory.Delete(openSpec);
            Directory.CreateDirectory(Path.Combine(openSpec, "changes", "demo"));
            var repositoryRoot = changes.Roots.Single(root => PathComparer.Equals(root.CanonicalPath, box.Repo));
            changes.Emit(new(DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents, repositoryRoot.Id,
                "openspec", DirectoryChanged: true));
            await WaitUntil(() => changes.Roots.Any(root => IsWithin(root.CanonicalPath, Path.Combine(openSpec, "changes"))));

            var containedChanges = Path.Combine(openSpec, "changes");
            Directory.Move(containedChanges, containedChanges + ".saved");
            await CreateDirectoryLink(containedChanges, outsideChanges, box.Repo);
            linkedChangesPath = containedChanges;
            changes.Emit(new(DashboardChangeDomain.Catalog | DashboardChangeDomain.Documents, "rebound", null, DirectoryChanged: true));
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0
                && changes.Roots.Any(root => PathComparer.Equals(root.CanonicalPath, box.Repo)));
            Assert.DoesNotContain(changes.Roots, root => IsWithin(root.CanonicalPath, outsideOpenSpec));
            Assert.DoesNotContain(changes.Roots, root => PathComparer.Equals(root.CanonicalPath, Path.Combine(outsideChanges, "nested")));
        }
        finally
        {
            await app.StopAsync();
            if (linkedChangesPath is not null && Directory.Exists(linkedChangesPath)) Directory.Delete(linkedChangesPath);
        }
    }

    [Fact]
    public async Task SlowOpenSpecSourceDoesNotBlockIndependentJournalRevision()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        var config = DashboardTests.Write(box, "openspec/config.yaml", "schema: spec-driven\n");
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var command = new BlockingContextCommand(box.Repo);
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "live-independent", authorization,
            command, changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            var dataRoot = changes.Roots.First(root => Path.GetFullPath(root.CanonicalPath) == Path.GetFullPath(box.Data));
            var repositoryRoot = changes.Roots.Where(root => (root.Domains & DashboardChangeDomain.Catalog) != 0)
                .Where(root => IsWithin(config, root.CanonicalPath)).OrderByDescending(root => root.CanonicalPath.Length).FirstOrDefault()
                ?? throw new InvalidOperationException("Expected a repository watch root; actual roots: " + string.Join(", ", changes.Roots.Select(root => root.CanonicalPath)));
            using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(DashboardHost.Address(app)) };
            (await client.PostAsJsonAsync("/api/bootstrap", new { token = authorization.Bootstrap() })).EnsureSuccessStatusCode();
            var address = client.BaseAddress!;
            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Cookie", handler.CookieContainer.GetCookies(address)[authorization.CookieName]!.ToString());
            socket.Options.SetRequestHeader("Origin", address.GetLeftPart(UriPartial.Authority));
            await socket.ConnectAsync(new UriBuilder(address) { Scheme = "ws", Path = "/api/live" }.Uri, CancellationToken.None);
            await SendSignalRFrame(socket, "{\"protocol\":\"json\",\"version\":1}\u001e");
            Assert.Equal("{}", await ReceiveSignalRFrame(socket));
            await SendSignalRFrame(socket, "{\"type\":1,\"invocationId\":\"snapshot\",\"target\":\"Snapshot\",\"arguments\":[]}\u001e");
            _ = await ReceiveSignalRFrame(socket);
            await WaitUntil(() => coordinator.ActiveConnectionCount == 1);

            var beforeJournal = coordinator.CurrentSnapshot.Journal;
            command.BlockNextContext();
            File.WriteAllText(config, "schema: spec-driven\nchange: true\n");
            changes.Emit(new(DashboardChangeDomain.Catalog, repositoryRoot.Id, Path.GetRelativePath(repositoryRoot.CanonicalPath, config).Replace('\\', '/')));
            await command.ContextBlocked.WaitAsync(TimeSpan.FromSeconds(3));
            new Journal(new LocalSettings(box.Data), new("repo")).Log(new("in_progress", "demo", Summary: "saved",
                OccurredAt: "2026-11-06T11:00:00Z"));
            changes.Emit(new(DashboardChangeDomain.Journal, dataRoot.Id, "journal.db-wal"));

            await WaitUntil(() => coordinator.CurrentSnapshot.Journal > beforeJournal);
            Assert.Contains("catalog", coordinator.CurrentSnapshot.Dirty);
            Assert.DoesNotContain("journal", coordinator.CurrentSnapshot.Dirty);
            using (var delivered = await ReceiveRevisionAsync(socket, vector => vector.GetProperty("journal").GetInt64() > beforeJournal
                && vector.GetProperty("dirty").EnumerateArray().Any(name => name.GetString() == "catalog")))
            {
                Assert.False(delivered.RootElement.TryGetProperty("checkpoint", out _));
                Assert.True(delivered.RootElement.GetProperty("journal").GetInt64() > beforeJournal);
                Assert.Contains("catalog", delivered.RootElement.GetProperty("dirty").EnumerateArray().Select(name => name.GetString()));
            }
            command.ReleaseContext();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0);
        }
        finally
        {
            command.ReleaseContext();
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task LockedJournalDoesNotBlockDigestOrReadRevisionsAndRecoversWithoutDetectorWrites()
    {
        using var box = new TestWorkspace();
        CreateJournal(box.Data, "locked-journal");
        var saved = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        new DigestStore(box.Data).Save(new("item", "2026-11-06", [], "locked-digest", saved));
        var readsDirectory = Path.Combine(box.Data, "dashboard-reads");
        Directory.CreateDirectory(readsDirectory);
        var readsIndex = Path.Combine(readsDirectory, "index.json");
        await File.WriteAllTextAsync(readsIndex, "{}\n");
        var journalPath = Path.Combine(box.Data, "journal.db");
        SetRollbackJournalMode(journalPath);
        var journalSignature = Signature(journalPath);
        using var journalLock = HoldExclusiveLock(journalPath);

        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "live-locked-store", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => changes.Roots.Any(root => Path.GetFileName(root.CanonicalPath) == "dashboard-reads"));
            using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(DashboardHost.Address(app)) };
            (await client.PostAsJsonAsync("/api/bootstrap", new { token = authorization.Bootstrap() })).EnsureSuccessStatusCode();
            var address = client.BaseAddress!;
            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Cookie", handler.CookieContainer.GetCookies(address)[authorization.CookieName]!.ToString());
            socket.Options.SetRequestHeader("Origin", address.GetLeftPart(UriPartial.Authority));
            await socket.ConnectAsync(new UriBuilder(address) { Scheme = "ws", Path = "/api/live" }.Uri, CancellationToken.None);
            await SendSignalRFrame(socket, "{\"protocol\":\"json\",\"version\":1}\u001e");
            Assert.Equal("{}", await ReceiveSignalRFrame(socket));
            await WaitUntil(() => coordinator.ActiveConnectionCount == 1);
            var unavailable = coordinator.CurrentSnapshot;
            Assert.Contains("journal", unavailable.Dirty);
            Assert.Equal(journalSignature, Signature(journalPath));

            var beforeJournal = unavailable.Journal;
            var beforeDigests = unavailable.Digests;
            var beforeReads = unavailable.Reads;
            new DigestStore(box.Data).Save(new("item", "2026-11-06", [], "recovered-digest", saved.AddMinutes(1)));
            await File.WriteAllTextAsync(readsIndex, "{\"read\":true}\n");
            var dataRoot = changes.Roots.First(root => Path.GetFullPath(root.CanonicalPath) == Path.GetFullPath(box.Data));
            var readRoot = changes.Roots.First(root => Path.GetFileName(root.CanonicalPath) == "dashboard-reads");
            changes.Emit(new(DashboardChangeDomain.Digests, dataRoot.Id, "digests.db-wal"));
            changes.Emit(new(DashboardChangeDomain.Reads, readRoot.Id, "index.json"));
            await WaitUntil(() => coordinator.CurrentSnapshot.Digests > beforeDigests
                && coordinator.CurrentSnapshot.Reads > beforeReads && coordinator.CurrentSnapshot.Dirty.Contains("journal"));
            using (var delivered = await ReceiveRevisionAsync(socket, vector => vector.GetProperty("digests").GetInt64() > beforeDigests
                && vector.GetProperty("reads").GetInt64() > beforeReads
                && vector.GetProperty("dirty").EnumerateArray().Any(name => name.GetString() == "journal")))
            {
                Assert.True(delivered.RootElement.GetProperty("digests").GetInt64() > beforeDigests);
                Assert.True(delivered.RootElement.GetProperty("reads").GetInt64() > beforeReads);
                Assert.Contains("journal", delivered.RootElement.GetProperty("dirty").EnumerateArray().Select(name => name.GetString()));
            }

            journalLock.Dispose();
            changes.Emit(new(DashboardChangeDomain.Journal));

            await WaitUntil(() => coordinator.CurrentSnapshot.Journal > beforeJournal
                && !coordinator.CurrentSnapshot.Dirty.Contains("journal"));
            Assert.Equal(1, new DashboardJournal(box.Data).Snapshot());
            Assert.Equal(saved.AddMinutes(1).UtcTicks, new DigestStore(box.Data).Revision());
            Assert.Equal(journalSignature, Signature(journalPath));
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task WebSocketSnapshotReturnsFullVectorAndRevokedCookieCannotReceiveLaterFrames()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n");
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "live-snapshot", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            var connections = app.Services.GetRequiredService<DashboardLiveConnections>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            var address = new Uri(DashboardHost.Address(app));
            using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
            using var client = new HttpClient(handler) { BaseAddress = address };
            (await client.PostAsJsonAsync("/api/bootstrap", new { token = authorization.Bootstrap() })).EnsureSuccessStatusCode();
            var cookie = handler.CookieContainer.GetCookies(address)[authorization.CookieName]!.ToString();
            var websocketUri = new UriBuilder(address) { Scheme = "ws", Path = "/api/live" }.Uri;
            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Cookie", cookie);
            socket.Options.SetRequestHeader("Origin", address.GetLeftPart(UriPartial.Authority));
            await socket.ConnectAsync(websocketUri, CancellationToken.None);
            await SendSignalRFrame(socket, "{\"protocol\":\"json\",\"version\":1}\u001e");
            Assert.Equal("{}", await ReceiveSignalRFrame(socket));
            await SendSignalRFrame(socket, "{\"type\":1,\"invocationId\":\"snapshot\",\"target\":\"Snapshot\",\"arguments\":[]}\u001e");
            using var completion = JsonDocument.Parse(await ReceiveSignalRFrame(socket));
            var result = completion.RootElement.GetProperty("result");
            Assert.Equal("live-snapshot", result.GetProperty("instance").GetString());
            Assert.Equal(JsonValueKind.Number, result.GetProperty("catalog").ValueKind);
            Assert.Equal(JsonValueKind.Number, result.GetProperty("documents").ValueKind);
            Assert.Equal(JsonValueKind.Array, result.GetProperty("dirty").ValueKind);
            await WaitUntil(() => coordinator.ActiveConnectionCount == 1);
            await WaitUntil(() => connections.PendingHintCount == 0);
            await Task.Delay(50);
            var deliveredBeforeRevocation = connections.DeliveryCount;

            File.Delete(Path.Combine(box.Data, "dashboard.secret"));
            connections.Publish(coordinator.CurrentSnapshot);
            await WaitUntil(() => coordinator.ActiveConnectionCount == 0);
            Assert.Equal(deliveredBeforeRevocation, connections.DeliveryCount);
            using var closed = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                while (!closed.IsCancellationRequested)
                {
                    var closeResult = await socket.ReceiveAsync(new byte[64], closed.Token);
                    if (closeResult.MessageType == WebSocketMessageType.Close) break;
                }
            }
            catch (WebSocketException)
            {
                Assert.Equal(0, coordinator.ActiveConnectionCount);
            }
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task IdleAuthorizedWebSocketIsRevokedWithinTwoSecondsOfSecretRemoval()
    {
        using var box = new TestWorkspace();
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "live-idle-revoke", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            var address = new Uri(DashboardHost.Address(app));
            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Cookie", authorization.CookieName + "=" + authorization.Cookie);
            socket.Options.SetRequestHeader("Origin", address.GetLeftPart(UriPartial.Authority));
            await socket.ConnectAsync(new UriBuilder(address) { Scheme = "ws", Path = "/api/live" }.Uri, CancellationToken.None);
            await SendSignalRFrame(socket, "{\"protocol\":\"json\",\"version\":1}\u001e");
            Assert.Equal("{}", await ReceiveSignalRFrame(socket));
            await WaitUntil(() => coordinator.ActiveConnectionCount == 1);

            var started = DateTimeOffset.UtcNow;
            File.Delete(Path.Combine(box.Data, "dashboard.secret"));
            using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(2400));
            while (coordinator.ActiveConnectionCount != 0)
            {
                deadline.Token.ThrowIfCancellationRequested();
                await Task.Delay(10, deadline.Token);
            }
            Assert.True(DateTimeOffset.UtcNow - started <= TimeSpan.FromMilliseconds(2200));
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task HostCancellationDisposesEveryOwnedFileWatcher()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n");
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "live-stop", authorization,
            new DashboardTests.FixtureCommand(box.Repo));
        var source = (FileSystemDashboardChangeSource)app.Services.GetRequiredService<IDashboardChangeSource>();
        await app.StartAsync();
        var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
        await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && source.WatcherCount > 0);
        await app.StopAsync();
        Assert.Equal(0, source.WatcherCount);
    }

    [Fact]
    public async Task FiveSecondCheckpointReconcilesAChangeWithoutAnyDeliveredHint()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var path = DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n");
        var changes = new FakeDashboardChangeSource();
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "live-checkpoint", authorization,
            new DashboardTests.FixtureCommand(box.Repo), changeSource: changes);
        await app.StartAsync();
        try
        {
            var coordinator = app.Services.GetRequiredService<DashboardLiveCoordinator>();
            await WaitUntil(() => coordinator.CurrentSnapshot.Dirty.Length == 0 && changes.Roots.Length > 0);
            var before = coordinator.CurrentSnapshot.Documents;
            File.WriteAllText(path, "# Changed\n");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            while (coordinator.CurrentSnapshot.Documents == before)
            {
                deadline.Token.ThrowIfCancellationRequested();
                await Task.Delay(10, deadline.Token);
            }
            Assert.True(coordinator.CheckpointCount > 0);
            Assert.True(coordinator.CurrentSnapshot.Documents > before);
            Assert.Empty(coordinator.CurrentSnapshot.Dirty);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task SlowConnectionKeepsOnlyOnePendingRevisionAndStopsItsDeliveryLoop()
    {
        using var box = new TestWorkspace();
        var authorization = new DashboardAuthorization(box.Data);
        var proxy = new BlockingClientProxy();
        using var connections = new DashboardLiveConnections(authorization, new BlockingHubContext(proxy), TimeProvider.System);
        var context = new TestHubCallerContext(authorization);
        Assert.True(connections.Add(context));
        var vector = new DashboardRevisionVector("slow", 1, 1, 1, 1, 1, []);
        connections.Publish(vector);
        await proxy.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        for (var i = 0; i < 4096; i++)
            connections.Publish(vector with { Catalog = i + 2 });

        Assert.InRange(connections.PendingHintCount, 0, 1);
        Assert.InRange(connections.MaxPendingNotificationCount, 0, 1);
        await connections.StopAsync(CancellationToken.None);
        Assert.Equal(0, connections.ActiveConnectionCount);
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task ReplacedPendingVectorKeepsTheCheckpointTagAndCarriesTheNewestRevisions()
    {
        using var box = new TestWorkspace();
        var authorization = new DashboardAuthorization(box.Data);
        var proxy = new RecordingClientProxy();
        using var connections = new DashboardLiveConnections(authorization, new RecordingHubContext(proxy), TimeProvider.System);
        Assert.True(connections.Add(new TestHubCallerContext(authorization)));
        connections.Publish(new DashboardRevisionVector("tagged", 1, 1, 1, 1, 1, []));
        await proxy.FirstSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        // The reader is held inside the first send, so these vectors are pending together.
        connections.Publish(new DashboardRevisionVector("tagged", 2, 2, 2, 2, 2, []), checkpoint: true);
        var tag = connections.CheckpointCount;
        connections.Publish(new DashboardRevisionVector("tagged", 3, 3, 3, 3, 3, ["Journal"]));
        connections.Publish(new DashboardRevisionVector("tagged", 4, 4, 4, 4, 4, []));
        Assert.Equal(1, connections.PendingHintCount);
        proxy.ReleaseFirst();
        var delivered = await proxy.Delivered(2);

        Assert.Null(delivered[0].Checkpoint);
        Assert.Equal(4, delivered[1].Catalog);
        Assert.Equal(4, delivered[1].Reads);
        // Dirty hints from superseded pending vectors are kept (ANALYSIS-703), not dropped.
        Assert.Equal(["Journal"], delivered[1].Dirty);
        Assert.Equal(tag, delivered[1].Checkpoint);
        await connections.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ReplacedPendingVectorWithoutCheckpointStaysUntaggedAndLaterCheckpointWins()
    {
        using var box = new TestWorkspace();
        var authorization = new DashboardAuthorization(box.Data);
        var proxy = new RecordingClientProxy();
        using var connections = new DashboardLiveConnections(authorization, new RecordingHubContext(proxy), TimeProvider.System);
        Assert.True(connections.Add(new TestHubCallerContext(authorization)));
        connections.Publish(new DashboardRevisionVector("plain", 1, 1, 1, 1, 1, []));
        await proxy.FirstSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        connections.Publish(new DashboardRevisionVector("plain", 2, 2, 2, 2, 2, []));
        connections.Publish(new DashboardRevisionVector("plain", 3, 3, 3, 3, 3, []));
        connections.Publish(new DashboardRevisionVector("plain", 4, 4, 4, 4, 4, []), checkpoint: true);
        var tag = connections.CheckpointCount;
        proxy.ReleaseFirst();
        var delivered = await proxy.Delivered(2);

        Assert.Null(delivered[0].Checkpoint);
        Assert.Equal(4, delivered[1].Catalog);
        Assert.Equal(tag, delivered[1].Checkpoint);

        // Nothing pending and no checkpoint: no tag is invented.
        connections.Publish(new DashboardRevisionVector("plain", 5, 5, 5, 5, 5, []));
        connections.Publish(new DashboardRevisionVector("plain", 6, 6, 6, 6, 6, []));
        var later = await proxy.DeliveredCatalog(6);
        Assert.Null(later.Checkpoint);
        await connections.StopAsync(CancellationToken.None);
    }

    /// <summary>ANALYSIS-703: a late older offer for the same instance must not lower the pending vector.</summary>
    [Fact]
    public async Task OlderOfferAfterNewerPendingKeepsNewestRevisionsAndBothDirtyHints()
    {
        var (box, connections, proxy) = await ConnectionWithHeldFirstSendAsync("mono");
        using var boxScope = box;
        using var connectionsScope = connections;
        connections.Publish(new DashboardRevisionVector("mono", 5, 5, 5, 5, 5, ["Journal", "Catalog"]));
        connections.Publish(new DashboardRevisionVector("mono", 4, 6, 3, 4, 7, ["Catalog", "Documents"]));
        proxy.ReleaseFirst();
        var delivered = await proxy.Delivered(2);

        Assert.Equal(new DashboardRevisionVector("mono", 5, 6, 5, 5, 7, ["Journal", "Catalog", "Documents"]), delivered[1], new VectorComparer());
        await connections.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PendingCheckpointSurvivesAMergeWithAnOlderOffer()
    {
        var (box, connections, proxy) = await ConnectionWithHeldFirstSendAsync("merge-tag");
        using var boxScope = box;
        using var connectionsScope = connections;
        connections.Publish(new DashboardRevisionVector("merge-tag", 6, 6, 6, 6, 6, []), checkpoint: true);
        var tag = connections.CheckpointCount;
        connections.Publish(new DashboardRevisionVector("merge-tag", 5, 5, 5, 5, 5, ["Journal"]));
        proxy.ReleaseFirst();
        var delivered = await proxy.Delivered(2);

        Assert.Equal(6, delivered[1].Catalog);
        Assert.Equal(tag, delivered[1].Checkpoint);
        Assert.Equal(["Journal"], delivered[1].Dirty);
        await connections.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task OlderCheckpointTaggedOfferMergesIntoANewerUntaggedPendingVector()
    {
        var (box, connections, proxy) = await ConnectionWithHeldFirstSendAsync("tagged-older");
        using var boxScope = box;
        using var connectionsScope = connections;
        connections.Publish(new DashboardRevisionVector("tagged-older", 5, 5, 5, 5, 5, ["Journal"]));
        connections.Publish(new DashboardRevisionVector("tagged-older", 4, 4, 4, 4, 4, []), checkpoint: true);
        proxy.ReleaseFirst();
        var delivered = await proxy.Delivered(2);

        Assert.Equal(5, delivered[1].Catalog);
        Assert.Equal(5, delivered[1].Journal);
        Assert.Equal(5, delivered[1].Reads);
        Assert.Equal(["Journal"], delivered[1].Dirty);
        Assert.Equal(connections.CheckpointCount, delivered[1].Checkpoint);
        await connections.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task OfferFromANewInstanceReplacesThePendingVectorEvenWhenOlder()
    {
        var (box, connections, proxy) = await ConnectionWithHeldFirstSendAsync("old");
        using var boxScope = box;
        using var connectionsScope = connections;
        connections.Publish(new DashboardRevisionVector("old", 9, 9, 9, 9, 9, ["Journal"]), checkpoint: true);
        connections.Publish(new DashboardRevisionVector("new", 1, 2, 3, 4, 5, ["Digests"]));
        proxy.ReleaseFirst();
        var delivered = await proxy.Delivered(2);

        Assert.Equal(new DashboardRevisionVector("new", 1, 2, 3, 4, 5, ["Digests"]), delivered[1], new VectorComparer());
        Assert.Null(delivered[1].Checkpoint);
        await connections.StopAsync(CancellationToken.None);
    }

    private sealed class VectorComparer : IEqualityComparer<DashboardRevisionVector>
    {
        public bool Equals(DashboardRevisionVector? x, DashboardRevisionVector? y) =>
            x is not null && y is not null && x.Instance == y.Instance && x.Catalog == y.Catalog && x.Documents == y.Documents
            && x.Journal == y.Journal && x.Digests == y.Digests && x.Reads == y.Reads && x.Dirty.SequenceEqual(y.Dirty);
        public int GetHashCode(DashboardRevisionVector obj) => HashCode.Combine(obj.Instance, obj.Catalog, obj.Journal);
    }

    private static async Task<(TestWorkspace Box, DashboardLiveConnections Connections, RecordingClientProxy Proxy)> ConnectionWithHeldFirstSendAsync(string instance)
    {
        var box = new TestWorkspace();
        var authorization = new DashboardAuthorization(box.Data);
        var proxy = new RecordingClientProxy();
        var connections = new DashboardLiveConnections(authorization, new RecordingHubContext(proxy), TimeProvider.System);
        Assert.True(connections.Add(new TestHubCallerContext(authorization)));
        connections.Publish(new DashboardRevisionVector(instance, 1, 1, 1, 1, 1, []));
        await proxy.FirstSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        return (box, connections, proxy);
    }

    /// <summary>ANALYSIS-700: a send loop that already ended has disposed the slot's token; removal paths must tolerate that.</summary>
    [Fact]
    public async Task RemoveToleratesASlotWhoseSendLoopAlreadyDisposedItsToken() =>
        await RunWithDisposedSlotsAsync(["a1"], (connections, _) =>
        {
            connections.Remove("a1");
            return Task.CompletedTask;
        });

    [Fact]
    public async Task RevocationToleratesSlotsWhoseSendLoopAlreadyDisposedTheirToken() =>
        await RunWithDisposedSlotsAsync(["b1", "b2"], (connections, box) =>
        {
            File.Delete(Path.Combine(box.Data, "dashboard.secret"));
            connections.RevokeInvalidConnections();
            return Task.CompletedTask;
        });

    [Fact]
    public async Task StopAbortsEverySlotEvenWhenTheirSendLoopsAlreadyDisposedTheirTokens() =>
        await RunWithDisposedSlotsAsync(["c1", "c2", "c3"], (connections, _) => connections.StopAsync(CancellationToken.None));

    /// <summary>Ends each slot's loop through ConnectionAborted, then puts the slots back as a racing remover would still hold them.</summary>
    private static async Task RunWithDisposedSlotsAsync(string[] ids, Func<DashboardLiveConnections, TestWorkspace, Task> act)
    {
        using var box = new TestWorkspace();
        var authorization = new DashboardAuthorization(box.Data);
        using var connections = new DashboardLiveConnections(authorization, new RecordingHubContext(new RecordingClientProxy()), TimeProvider.System);
        var registry = (System.Collections.IDictionary)typeof(DashboardLiveConnections)
            .GetField("connections", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(connections)!;
        var contexts = ids.Select(id => new TestHubCallerContext(authorization, id)).ToArray();
        foreach (var context in contexts) Assert.True(connections.Add(context));
        var slots = new Dictionary<string, object>();
        foreach (System.Collections.DictionaryEntry entry in registry) slots[(string)entry.Key] = entry.Value!;
        foreach (var context in contexts) context.Abort();
        await WaitUntilAsync(() => connections.ActiveConnectionCount == 0 && slots.Values.All(IsDisposed));
        foreach (var pair in slots) registry[pair.Key] = pair.Value;
        Assert.Equal(ids.Length, connections.ActiveConnectionCount);

        await act(connections, box);

        Assert.Equal(0, connections.ActiveConnectionCount);
    }

    private static bool IsDisposed(object slot)
    {
        var stop = (CancellationTokenSource)slot.GetType().GetProperty("Stop")!.GetValue(slot)!;
        try { _ = stop.Token; return false; }
        catch (ObjectDisposedException) { return true; }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            deadline.Token.ThrowIfCancellationRequested();
            await Task.Delay(5, deadline.Token);
        }
    }

    private static async Task<HttpClient> AuthorizedClient(Microsoft.AspNetCore.Builder.WebApplication app, DashboardAuthorization authorization)
    {
        var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        var client = new HttpClient(handler) { BaseAddress = new Uri(DashboardHost.Address(app)) };
        (await client.PostAsJsonAsync("/api/bootstrap", new { token = authorization.Bootstrap() })).EnsureSuccessStatusCode();
        return client;
    }

    private static bool IsWithin(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static async Task CreateDirectoryLink(string link, string target, string workingDirectory)
    {
        try { Directory.CreateSymbolicLink(link, target); }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        {
            var result = await new LocalCommand().RunAsync("cmd.exe", workingDirectory,
                ["/d", "/c", "mklink", "/J", link, target], CancellationToken.None);
            Assert.Equal(0, result.ExitCode);
        }
    }

    private static void CreateJournal(string dataDirectory, string summary, string repository = "repo")
        => new Journal(new LocalSettings(dataDirectory), new(repository)).Log(new("in_progress", "demo", Summary: summary,
            OccurredAt: "2026-11-06T10:00:00Z"));

    private static (long Length, DateTime LastWriteUtc) Signature(string path)
    {
        var file = new FileInfo(path);
        return (file.Length, file.LastWriteTimeUtc);
    }

    private static void SetRollbackJournalMode(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE); PRAGMA journal_mode=DELETE;";
        command.ExecuteNonQuery();
    }

    private static SqliteConnection HoldExclusiveLock(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
            DefaultTimeout = 0
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "BEGIN EXCLUSIVE;";
        command.ExecuteNonQuery();
        return connection;
    }

    private static void Checkpoint(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
        command.ExecuteNonQuery();
    }

    private static void ReplaceKeepingMarkers(string replacementPath, string targetPath)
    {
        var before = new FileInfo(targetPath);
        var length = before.Length;
        var lastWrite = before.LastWriteTimeUtc;
        var creation = before.CreationTimeUtc;
        Assert.Equal(length, new FileInfo(replacementPath).Length);
        File.SetLastWriteTimeUtc(replacementPath, lastWrite);
        File.SetCreationTimeUtc(replacementPath, creation);
        Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(replacementPath));
        Assert.Equal(creation, File.GetCreationTimeUtc(replacementPath));
        SqliteConnection.ClearAllPools();
        var staging = targetPath + ".replacement";
        File.Copy(replacementPath, staging);
        File.Replace(staging, targetPath, null);
        File.SetLastWriteTimeUtc(targetPath, lastWrite);
        File.SetCreationTimeUtc(targetPath, creation);
        Assert.Equal(length, new FileInfo(targetPath).Length);
        Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(targetPath));
        Assert.Equal(creation, File.GetCreationTimeUtc(targetPath));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            deadline.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, deadline.Token);
        }
    }

    private static Task WaitForTimerAt(ManualTimeProvider clock, DateTimeOffset due)
        => WaitUntil(() => clock.ScheduledDueTimes.Contains(due));

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;

    private static async Task SendSignalRFrame(ClientWebSocket socket, string frame)
    {
        var bytes = Encoding.UTF8.GetBytes(frame);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<string> ReceiveSignalRFrame(ClientWebSocket socket)
    {
        var buffer = new byte[4096];
        var value = new StringBuilder();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("SignalR closed before a complete frame arrived.");
            value.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            var text = value.ToString();
            var separator = text.IndexOf('\u001e');
            if (separator >= 0) return text[..separator];
        }
    }

    private static async Task<JsonDocument> ReceiveRevisionAsync(ClientWebSocket socket, Func<JsonElement, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            var frame = await ReceiveSignalRFrame(socket, timeout.Token);
            using var message = JsonDocument.Parse(frame);
            if (!message.RootElement.TryGetProperty("target", out var target) || target.GetString() != "Revision"
                || !message.RootElement.TryGetProperty("arguments", out var arguments) || arguments.GetArrayLength() == 0)
                continue;
            var vector = arguments[0];
            if (predicate(vector)) return JsonDocument.Parse(vector.GetRawText());
        }
    }

    private static async Task<string> ReceiveSignalRFrame(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var value = new StringBuilder();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("SignalR closed before a complete frame arrived.");
            value.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            var text = value.ToString();
            var separator = text.IndexOf('\u001e');
            if (separator >= 0) return text[..separator];
        }
    }

    private sealed class FakeDashboardChangeSource : IDashboardChangeSource
    {
        private DashboardWatchRoot[] roots = [];
        public event Action<DashboardChangeHint>? Changed;
        public DashboardWatchRoot[] Roots => Volatile.Read(ref roots);
        public bool Disposed { get; private set; }

        public Task RebindAsync(IReadOnlyCollection<DashboardWatchRoot> next, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref roots, next.ToArray());
            return Task.CompletedTask;
        }

        public void Emit(DashboardChangeHint hint) => Changed?.Invoke(hint);
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            Changed = null;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingHubContext(BlockingClientProxy proxy) : IHubContext<DashboardLiveHub>
    {
        public IHubClients Clients { get; } = new TestHubClients(proxy);
        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class TestHubClients(IClientProxy proxy) : IHubClients
    {
        public IClientProxy All => proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Client(string connectionId) => proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => proxy;
        public IClientProxy Group(string groupName) => proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => proxy;
        public IClientProxy Others => proxy;
        public IClientProxy OthersInGroup(string groupName) => proxy;
        public IClientProxy User(string userId) => proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => proxy;
    }

    private sealed class RecordingHubContext(RecordingClientProxy proxy) : IHubContext<DashboardLiveHub>
    {
        public IHubClients Clients { get; } = new TestHubClients(proxy);
        public IGroupManager Groups => throw new NotSupportedException();
    }

    /// <summary>Holds the first send open, then records every vector it is asked to send.</summary>
    private sealed class RecordingClientProxy : IClientProxy
    {
        private readonly TaskCompletionSource firstReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<DashboardRevisionVector> sent = [];
        public TaskCompletionSource FirstSendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void ReleaseFirst() => firstReleased.TrySetResult();

        public async Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            bool first;
            lock (sent) { first = sent.Count == 0; sent.Add((DashboardRevisionVector)args[0]!); }
            if (!first) return;
            FirstSendStarted.TrySetResult();
            await firstReleased.Task.WaitAsync(cancellationToken);
        }

        public async Task<DashboardRevisionVector> DeliveredCatalog(long catalog)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (true)
            {
                lock (sent) { var match = sent.FirstOrDefault(vector => vector.Catalog == catalog); if (match is not null) return match; }
                await Task.Delay(10, deadline.Token);
            }
        }

        public async Task<DashboardRevisionVector[]> Delivered(int count)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (true)
            {
                lock (sent) { if (sent.Count >= count) return sent.ToArray(); }
                await Task.Delay(10, deadline.Token);
            }
        }
    }

    private sealed class BlockingClientProxy : IClientProxy
    {
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            SendStarted.TrySetResult();
            await released.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class BlockingContextCommand(string root) : LocalCommand
    {
        private int blockNextContext;
        private readonly TaskCompletionSource contextBlocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource contextReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task ContextBlocked => contextBlocked.Task;
        public void BlockNextContext() => Interlocked.Exchange(ref blockNextContext, 1);
        public void ReleaseContext() => contextReleased.TrySetResult();

        public override async Task<CommandOutput> RunAsync(string executable, string directory, string[] arguments, CancellationToken cancellationToken)
        {
            if (executable == "openspec" && arguments[0] == "context" && Interlocked.Exchange(ref blockNextContext, 0) == 1)
            {
                contextBlocked.TrySetResult();
                await contextReleased.Task.WaitAsync(cancellationToken);
            }
            return await new DashboardTests.FixtureCommand(root).RunAsync(executable, directory, arguments, cancellationToken);
        }
    }

    private sealed class TestHubCallerContext : HubCallerContext
    {
        private readonly CancellationTokenSource aborted = new();
        private readonly DefaultHttpContext http = new();
        public bool Aborted { get; private set; }
        public override string ConnectionId { get; }
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal User { get; } = new();
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; }
        public override CancellationToken ConnectionAborted => aborted.Token;

        public TestHubCallerContext(DashboardAuthorization authorization, string connectionId = "slow-connection")
        {
            ConnectionId = connectionId;
            var features = new FeatureCollection();
            features.Set<IHttpContextFeature>(new TestHttpContextFeature(http));
            Features = features;
            http.Request.Headers.Cookie = authorization.CookieName + "=" + authorization.Cookie;
        }
        public override void Abort()
        {
            Aborted = true;
            aborted.Cancel();
        }
        private sealed class TestHttpContextFeature(HttpContext context) : IHttpContextFeature
        {
            public HttpContext? HttpContext { get; set; } = context;
        }
    }
}
