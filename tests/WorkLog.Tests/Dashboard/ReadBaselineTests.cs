using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WorkLog.Configuration;
using WorkLog.Dashboard;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Dashboard;

public sealed class ReadBaselineTests
{
    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan span) => now += span;
    }

    private static string Reads(TestWorkspace box) => Path.Combine(box.Data, "dashboard-reads");
    private static string[] Blobs(TestWorkspace box) => Directory.Exists(Path.Combine(Reads(box), "blobs")) ? Directory.GetFiles(Path.Combine(Reads(box), "blobs")) : [];

    [Fact]
    public void MarkRemembersExactTextAndGetReturnsIt()
    {
        using var box = new TestWorkspace();
        var store = new ReadBaselineStore(box.Data, new FakeClock());
        Assert.Null(store.Get("s:demo:design.md"));
        var text = "# Design\r\n\r\nUnicode: é中\U0001F600\r\nNo trailing newline";
        store.Mark("s:demo:design.md", "rev-1", text.Length, 123, text);
        var baseline = store.Get("s:demo:design.md");
        Assert.Equal(new ReadBaseline("rev-1", text), baseline);
        var entry = Assert.Single(store.Entries());
        Assert.Equal((123L, "rev-1"), (entry.Value.LastWriteTicks, entry.Value.Revision));
        store.Mark("s:demo:design.md", "rev-2", 5, 456, "newer");
        Assert.Equal(new ReadBaseline("rev-2", "newer"), store.Get("s:demo:design.md"));
        Assert.Single(Blobs(box));
    }

    [Fact]
    public void IdenticalContentIsStoredOnceAndKeptWhileAnyKeyUsesIt()
    {
        using var box = new TestWorkspace();
        var store = new ReadBaselineStore(box.Data, new FakeClock());
        store.Mark("a", "r", 4, 1, "same");
        store.Mark("b", "r", 4, 1, "same");
        Assert.Single(Blobs(box));
        store.Mark("a", "r2", 5, 2, "other");
        Assert.Equal(2, Blobs(box).Length);
        Assert.Equal("same", store.Get("b")!.Text);
        store.Mark("b", "r3", 6, 3, "third!");
        // Nothing refers to "same" any more.
        Assert.Equal(2, Blobs(box).Length);
        Assert.DoesNotContain(Blobs(box), file => File.ReadAllText(file) == "same");
    }

    [Fact]
    public void KeyBoundEvictsTheLeastRecentlyReadFirstAndDeletesTheirBlobs()
    {
        using var box = new TestWorkspace();
        var clock = new FakeClock();
        var store = new ReadBaselineStore(box.Data, clock);
        for (var i = 0; i < ReadBaselineStore.MaximumKeys; i++)
        {
            store.Mark("k" + i, "r" + i, 10, i, "text " + i);
            clock.Advance(TimeSpan.FromMinutes(5));
        }
        // Reading k0 later makes it the most recently read; k1 is now the oldest.
        Assert.NotNull(store.Get("k0"));
        store.Mark("new-1", "r", 1, 1, "new one");
        store.Mark("new-2", "r", 1, 1, "new two");
        var keys = store.Entries().Keys.ToHashSet();
        Assert.Equal(ReadBaselineStore.MaximumKeys, keys.Count);
        Assert.Contains("k0", keys);
        Assert.DoesNotContain("k1", keys);
        Assert.DoesNotContain("k2", keys);
        Assert.Contains("new-1", keys);
        Assert.Contains("new-2", keys);
        Assert.Equal(ReadBaselineStore.MaximumKeys, Blobs(box).Length);
        Assert.Null(store.Get("k1"));
    }

    [Fact]
    public void ByteBoundDiscardsOldestEntriesUntilTheBlobsFit()
    {
        using var box = new TestWorkspace();
        var clock = new FakeClock();
        var store = new ReadBaselineStore(box.Data, clock);
        const int size = DocumentService.MaximumBytes;
        var perBound = (int)(ReadBaselineStore.MaximumBytes / size);
        for (var i = 0; i < perBound + 3; i++)
        {
            store.Mark("big" + i, "r", size, i, new string((char)('a' + i % 26), size - 8) + i.ToString("D8"));
            clock.Advance(TimeSpan.FromDays(1));
        }
        var total = Blobs(box).Sum(file => new FileInfo(file).Length);
        Assert.True(total <= ReadBaselineStore.MaximumBytes, total + " bytes");
        var keys = store.Entries().Keys.ToHashSet();
        Assert.Contains("big" + (perBound + 2), keys);
        Assert.DoesNotContain("big0", keys);
        Assert.True(keys.Count < perBound + 3);
        // A single document under the document limit always fits, so the one just marked is never the one discarded.
        Assert.NotNull(store.Get("big" + (perBound + 2)));
    }

    [Fact]
    public void BoundsHoldAfterManyMarksOverManyDays()
    {
        using var box = new TestWorkspace();
        var clock = new FakeClock();
        var store = new ReadBaselineStore(box.Data, clock);
        for (var i = 0; i < 640; i++)
        {
            var key = "doc-" + i % 320;
            store.Mark(key, "r" + i, 20, i, "revision " + i + " of " + key);
            if (i % 50 == 0)
                clock.Advance(TimeSpan.FromDays(1));
        }
        var entries = store.Entries();
        Assert.Equal(ReadBaselineStore.MaximumKeys, entries.Count);
        Assert.Equal(ReadBaselineStore.MaximumKeys, Blobs(box).Length);
        Assert.True(new FileInfo(Path.Combine(Reads(box), "index.json")).Length < 200_000);
        // The most recent marks survive and carry their latest content.
        Assert.Equal("revision 639 of doc-319", store.Get("doc-319")!.Text);
        Assert.Empty(Directory.GetFiles(Reads(box), "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void ReadingKeepsAnEntryAliveButIsNotRewrittenWithinTheTouchInterval()
    {
        using var box = new TestWorkspace();
        var clock = new FakeClock();
        var store = new ReadBaselineStore(box.Data, clock);
        store.Mark("a", "r", 1, 1, "a");
        var index = Path.Combine(Reads(box), "index.json");
        var written = File.GetLastWriteTimeUtc(index);
        File.SetLastWriteTimeUtc(index, written.AddMinutes(-10));
        var marked = File.GetLastWriteTimeUtc(index);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.NotNull(store.Get("a"));
        Assert.Equal(marked, File.GetLastWriteTimeUtc(index));
        clock.Advance(ReadBaselineStore.TouchInterval);
        Assert.NotNull(store.Get("a"));
        Assert.NotEqual(marked, File.GetLastWriteTimeUtc(index));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"schemaVersion\":1,\"entries\":null}")]
    [InlineData("[]")]
    [InlineData("")]
    public void CorruptIndexIsTreatedAsEmptyAndRewrittenWithOrphanBlobsRemoved(string content)
    {
        using var box = new TestWorkspace();
        var store = new ReadBaselineStore(box.Data, new FakeClock());
        store.Mark("old", "r", 3, 1, "old");
        File.WriteAllText(Path.Combine(Reads(box), "index.json"), content);
        Assert.Null(store.Get("old"));
        Assert.Empty(store.Entries());
        store.Mark("fresh", "r", 5, 2, "fresh");
        Assert.Equal("fresh", store.Get("fresh")!.Text);
        Assert.Null(store.Get("old"));
        Assert.Single(Blobs(box));
        using var rewritten = JsonDocument.Parse(File.ReadAllText(Path.Combine(Reads(box), "index.json")));
        Assert.Equal(1, rewritten.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"entries\":{}}")]
    [InlineData("{\"schemaVersion\":0,\"entries\":{}}")]
    public void IndexOfAnotherSchemaVersionIsPreservedAndReportedUnavailable(string content)
    {
        using var box = new TestWorkspace();
        var store = new ReadBaselineStore(box.Data, new FakeClock());
        store.Mark("old", "r", 3, 1, "old");
        var indexPath = Path.Combine(Reads(box), "index.json");
        File.WriteAllText(indexPath, content);
        var blobs = Blobs(box).Select(b => (b, File.ReadAllText(b))).ToArray();
        var stamp = File.GetLastWriteTimeUtc(indexPath);

        Assert.Null(store.Get("old"));
        Assert.Empty(store.Entries());
        Assert.Throws<IOException>(() => store.Mark("fresh", "r", 5, 2, "fresh"));
        // Neither the index nor any blob was written, replaced or deleted.
        Assert.Equal(content, File.ReadAllText(indexPath));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(indexPath));
        Assert.Equal(blobs, Blobs(box).Select(b => (b, File.ReadAllText(b))).ToArray());
    }

    [Fact]
    public void AnIndexThatCannotBeReadIsNotDamageAndNothingIsWrittenOrDeleted()
    {
        using var box = new TestWorkspace();
        var store = new ReadBaselineStore(box.Data, new FakeClock());
        store.Mark("a", "r", 1, 1, "first");
        store.Mark("b", "r", 1, 1, "second");
        var indexPath = Path.Combine(Reads(box), "index.json");
        var before = File.ReadAllText(indexPath);
        var blobs = Blobs(box).Select(b => (b, File.ReadAllText(b))).ToArray();

        using (new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Throws<IOException>(() => store.Mark("c", "r", 1, 1, "third"));
            Assert.Throws<IOException>(() => store.Get("a"));
            Assert.Throws<IOException>(() => store.Entries());
        }

        Assert.Equal(before, File.ReadAllText(indexPath));
        Assert.Equal(blobs, Blobs(box).Select(b => (b, File.ReadAllText(b))).ToArray());
        Assert.Equal("first", store.Get("a")!.Text);
        Assert.Equal("second", store.Get("b")!.Text);
        Assert.Equal(2, store.Entries().Count);
    }

    [Fact]
    public void ABlobThatCannotBeReadKeepsItsEntryAndOtherBaselines()
    {
        using var box = new TestWorkspace();
        var store = new ReadBaselineStore(box.Data, new FakeClock());
        store.Mark("a", "r", 1, 1, "first");
        store.Mark("b", "r", 1, 1, "second");
        var blob = Blobs(box).Single(f => File.ReadAllText(f) == "first");
        var before = File.ReadAllText(Path.Combine(Reads(box), "index.json"));

        using (new FileStream(blob, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Throws<IOException>(() => store.Get("a"));
            Assert.Equal("second", store.Get("b")!.Text);
            // Marking the content that is held repeats the read of its blob, so it fails instead of rewriting anything.
            Assert.Throws<IOException>(() => store.Mark("c", "r", 1, 1, "first"));
        }

        Assert.Equal(before, File.ReadAllText(Path.Combine(Reads(box), "index.json")));
        Assert.Equal("first", store.Get("a")!.Text);
        Assert.Equal(2, Blobs(box).Length);
    }

    [Fact]
    public void TamperedEntriesAndDamagedBlobsAreNeverTrustedOrFollowedOutOfTheDirectory()
    {
        using var box = new TestWorkspace();
        var store = new ReadBaselineStore(box.Data, new FakeClock());
        store.Mark("good", "r", 4, 1, "good");
        store.Mark("damaged", "r", 3, 1, "was this");
        var outside = Path.Combine(box.Data, "outside.md");
        File.WriteAllText(outside, "secret");
        var indexPath = Path.Combine(Reads(box), "index.json");
        var index = File.ReadAllText(indexPath).Replace("\"entries\": {", "\"entries\": {\n    \"evil\": { \"revision\": \"r\", \"length\": 1, \"lastWriteTicks\": 1, \"readAt\": \"2026-10-27T12:00:00+00:00\", \"blob\": \"..\\\\..\\\\outside\" },", StringComparison.Ordinal);
        File.WriteAllText(indexPath, index);
        Assert.Null(store.Get("evil"));
        Assert.True(File.Exists(outside));
        Assert.DoesNotContain("evil", store.Entries().Keys);

        foreach (var file in Blobs(box).Where(f => File.ReadAllText(f) == "was this"))
            File.WriteAllText(file, "tampered");
        Assert.Null(store.Get("damaged"));
        Assert.DoesNotContain("damaged", store.Entries().Keys);
        Assert.Equal("good", store.Get("good")!.Text);
        // Marking the same content again repairs a damaged blob.
        store.Mark("damaged-2", "r", 3, 1, "good");
        Assert.Equal("good", store.Get("damaged-2")!.Text);
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task EndpointsRejectUnauthorizedRequestsAndKeepBaselinesCurrent()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var path = DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n\nOriginal.\n");
        DashboardTests.Write(box, "openspec/changes/demo/proposal.md", "# Proposal\n");
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "reads", authorization, new DashboardTests.FixtureCommand(box.Repo));
        await app.StartAsync();
        using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(DashboardHost.Address(app)) };

        // Nothing is served, and nothing is written, before the browser is authorized.
        var anonymous = new DocumentRequest("any", "demo", "design.md");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/reads/get", new ReadDocumentRequest(anonymous))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/reads/mark", new ReadMarkRequest(anonymous, "x"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/reads/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/reviews", new ReviewsRequest())).StatusCode);
        Assert.False(Directory.Exists(Path.Combine(box.Data, "dashboard-reads")));

        await client.PostAsJsonAsync("/api/bootstrap", new
        {
            token = authorization.Bootstrap()
        });
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/catalog");
        var source = catalog.GetProperty("sources")[0].GetProperty("id").GetString()!;
        var request = new DocumentRequest(source, "demo", "design.md");
        var document = (await (await client.PostAsJsonAsync("/api/document", request)).Content.ReadFromJsonAsync<DocumentContent>())!;

        Assert.Equal("null", (await (await client.PostAsJsonAsync("/api/reads/get", new ReadDocumentRequest(request))).Content.ReadAsStringAsync()).Trim());
        var marked = await client.PostAsJsonAsync("/api/reads/mark", new ReadMarkRequest(request, document.Revision));
        Assert.True(marked.IsSuccessStatusCode);
        var baseline = await (await client.PostAsJsonAsync("/api/reads/get", new ReadDocumentRequest(request))).Content.ReadFromJsonAsync<ReadBaseline>();
        Assert.Equal(new ReadBaseline(document.Revision, "# Design\n\nOriginal.\n"), baseline);

        // The summary carries the stored length and write time for the documents of the current catalog.
        var info = new FileInfo(path);
        var summary = await client.GetFromJsonAsync<JsonElement>("/api/reads/summary");
        var entry = summary.GetProperty("entries").GetProperty(source + ":demo:design.md");
        Assert.Equal(info.Length + ":" + info.LastWriteTimeUtc.Ticks, entry.GetString());
        Assert.Equal(entry.GetString(), (await marked.Content.ReadFromJsonAsync<ReadMarkResult>())!.Fingerprint);
        // The catalog reports the same text for the document, so the client can compare the two as strings.
        var listed = catalog.GetProperty("changes")[0].GetProperty("documents").EnumerateArray().Single(d => d.GetProperty("path").GetString() == "design.md");
        Assert.Equal(entry.GetString(), listed.GetProperty("fingerprint").GetString());
        Assert.False(summary.GetProperty("entries").TryGetProperty(source + ":demo:proposal.md", out _));

        // A revision that is no longer current is refused, and the baseline stays what the user last saw.
        File.WriteAllText(path, "# Design\n\nAgent rewrote this.\n");
        var stale = await client.PostAsJsonAsync("/api/reads/mark", new ReadMarkRequest(request, document.Revision));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("# Design\n\nOriginal.\n", (await (await client.PostAsJsonAsync("/api/reads/get", new ReadDocumentRequest(request))).Content.ReadFromJsonAsync<ReadBaseline>())!.Text);
        var current = (await (await client.PostAsJsonAsync("/api/document", request)).Content.ReadFromJsonAsync<DocumentContent>())!;
        Assert.True((await client.PostAsJsonAsync("/api/reads/mark", new ReadMarkRequest(request, current.Revision))).IsSuccessStatusCode);
        Assert.Equal("# Design\n\nAgent rewrote this.\n", (await (await client.PostAsJsonAsync("/api/reads/get", new ReadDocumentRequest(request))).Content.ReadFromJsonAsync<ReadBaseline>())!.Text);

        // Requests cannot name files outside the source or documents that do not exist.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/reads/mark", new ReadMarkRequest(request with
        {
            Path = "../other.md"
        }, "x"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/reads/get", new ReadDocumentRequest(request with
        {
            Path = "../other.md"
        }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/reads/mark", new ReadMarkRequest(request with
        {
            Change = "missing"
        }, "x"))).StatusCode);

        // Malformed bodies are client errors on every endpoint that takes a document, not server failures.
        foreach (var body in new[] { "{}", "{\"document\":null}", "{\"document\":{}}", "{\"document\":{\"sourceId\":\"x\"}}", "{\"document\":{\"sourceId\":null,\"change\":null,\"path\":null},\"revision\":\"r\"}", "[]" })
        {
            foreach (var endpoint in new[] { "/api/reads/get", "/api/reads/mark" })
            {
                var response = await client.PostAsync(endpoint, new StringContent(body, Encoding.UTF8, "application/json"));
                Assert.True(response.StatusCode == HttpStatusCode.BadRequest, endpoint + " " + body + " -> " + (int)response.StatusCode);
            }
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/reads/mark", new { document = request })).StatusCode);
        foreach (var body in new[] { "{\"scope\":null}", "{\"scope\":\"nope\"}", "[]", "null" })
        {
            var response = await client.PostAsync("/api/reviews", new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, "/api/reviews " + body + " -> " + (int)response.StatusCode);
        }
        Assert.True((await client.PostAsync("/api/reviews", new StringContent("{}", Encoding.UTF8, "application/json"))).IsSuccessStatusCode);

        // The overview endpoint answers the same authorized client.
        var reviews = await client.PostAsJsonAsync("/api/reviews", new ReviewsRequest(Scope: "all"));
        Assert.True(reviews.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/reviews", new ReviewsRequest(Scope: "bogus"))).StatusCode);
        await app.StopAsync();
    }

    [Fact]
    public async Task BaselineSurvivesAnArchiveMoveAndStaysSeparatePerSource()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n\nBefore archive.\n");
        var service = new ReadBaselineService(setup.Catalog, setup.Documents, new ReadBaselineStore(box.Data));
        var source = Assert.Single((await setup.Catalog.GetAsync()).Sources).Id;
        var request = new DocumentRequest(source, "demo", "design.md");
        var document = await setup.Documents.GetAsync(request);
        await service.MarkAsync(new(request, document.Revision), default);

        Directory.CreateDirectory(Path.Combine(box.Repo, "openspec/changes/archive"));
        Directory.Move(Path.Combine(box.Repo, "openspec/changes/demo"), Path.Combine(box.Repo, "openspec/changes/archive/2026-10-27-demo"));
        await setup.Catalog.GetAsync(force: true);
        var moved = await service.GetAsync(new(request), default);
        Assert.Equal("# Design\n\nBefore archive.\n", moved!.Text);
        // The archive directory name works as a request too and reaches the same baseline.
        Assert.NotNull(await service.GetAsync(new(request with
        {
            Change = "2026-10-27-demo"
        }), default));
        var summary = await service.SummaryAsync(default);
        Assert.Contains(source + ":demo:design.md", summary.Entries.Keys);
    }

    [Fact]
    public async Task MarkPreservesTextEncodedWithABomAndAnotherEncodingAsReadText()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design é\r\n", new UTF8Encoding(true));
        DashboardTests.Write(box, "openspec/changes/demo/proposal.md", "# Proposal 中\r\n", Encoding.Unicode);
        var service = new ReadBaselineService(setup.Catalog, setup.Documents, new ReadBaselineStore(box.Data));
        var source = Assert.Single((await setup.Catalog.GetAsync()).Sources).Id;
        foreach (var (file, text) in new[] { ("design.md", "# Design é\r\n"), ("proposal.md", "# Proposal 中\r\n") })
        {
            var request = new DocumentRequest(source, "demo", file);
            var document = await setup.Documents.GetAsync(request);
            await service.MarkAsync(new(request, document.Revision), default);
            Assert.Equal(new ReadBaseline(document.Revision, text), await service.GetAsync(new(request), default));
        }
    }
}
