using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WorkLog.Configuration;
using WorkLog.Dashboard;
using WorkLog.Journaling;
using WorkLog.Tests.Testing;
using Xunit;
using JournalJson = WorkLog.Infrastructure.Json;

namespace WorkLog.Tests.Dashboard;

public sealed class DashboardReliabilityTests
{
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan span) => now += span;
    }

    /// <summary>Counts every git and OpenSpec invocation by kind and working directory.</summary>
    private sealed class CountingCommand(string launch) : LocalCommand
    {
        public ConcurrentDictionary<string, int> Calls { get; } = new();
        public int Count(string kind, string? directory = null) => Calls.Where(p => p.Key.StartsWith(kind + "|", StringComparison.Ordinal)
            && (directory is null || p.Key.EndsWith("|" + directory, StringComparison.Ordinal))).Sum(p => p.Value);
        public override Task<CommandOutput> RunAsync(string executable, string directory, string[] arguments, CancellationToken cancellationToken)
        {
            string kind, output;
            if (executable == "git" && arguments[0] == "--git-dir")
            {
                kind = "worktree";
                output = "worktree " + Path.GetDirectoryName(arguments[1]);
                directory = arguments[1];
            }
            else if (executable == "git")
            {
                kind = "launch";
                output = launch + "\n" + Path.Combine(launch, ".git");
            }
            else if (arguments[0] == "context")
            {
                kind = "context";
                output = JournalJson.Encode(new
                {
                    root = new
                    {
                        path = directory
                    }
                });
            }
            else
            {
                kind = "list";
                output = "{\"changes\":[{\"name\":\"demo\",\"completedTasks\":1,\"totalTasks\":3}]}";
            }
            Calls.AddOrUpdate(kind + "|" + directory, 1, (_, count) => count + 1);
            return Task.FromResult(new CommandOutput(true, 0, output, ""));
        }
    }

    private static string Repository(TestWorkspace box, string name)
    {
        var repo = Path.Combine(box.Root, name);
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        Directory.CreateDirectory(Path.Combine(repo, "openspec/changes/demo"));
        File.WriteAllText(Path.Combine(repo, "openspec/changes/demo/design.md"), "# Design " + name);
        File.WriteAllText(Path.Combine(repo, "openspec/changes/demo/tasks.md"), "- [ ] 1.1 First\n");
        return repo;
    }

    [Fact]
    public async Task CliResultsAreReusedUntilTheirInputsChange()
    {
        using var box = new TestWorkspace();
        var first = Repository(box, "first");
        var second = Repository(box, "second");
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(first, ".git")] = new(new("first"));
        settings.Configuration.RepositoryContexts[Path.Combine(second, ".git")] = new(new("second"));
        var command = new CountingCommand(first);
        var clock = new TestClock();
        var catalog = new SourceCatalog(settings, new DashboardJournal(box.Data), command, first, clock);
        var initial = await catalog.GetAsync();
        Assert.Equal(2, initial.Sources.Length);
        Assert.Equal(2, command.Count("worktree"));
        Assert.Equal(1, command.Count("launch"));
        Assert.Equal(2, command.Count("context"));
        Assert.Equal(2, command.Count("list"));

        // Repeated requests past the 2 s response cache but inside 60 s with unchanged inputs spawn nothing.
        for (var i = 0; i < 5; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal(initial.Revision, (await catalog.GetAsync()).Revision);
        }
        Assert.Equal(7, command.Calls.Values.Sum());

        // Touching one source's tasks.md re-runs only that source's task progress.
        File.WriteAllText(Path.Combine(first, "openspec/changes/demo/tasks.md"), "- [ ] 1.1 First\n- [x] 1.2 Second\n");
        clock.Advance(TimeSpan.FromSeconds(3));
        var touched = await catalog.GetAsync();
        Assert.Equal(2, command.Count("list", first));
        Assert.Equal(1, command.Count("list", second));
        Assert.Equal(2, command.Count("context"));
        Assert.Equal(2, command.Count("worktree"));
        Assert.NotEqual(initial.Revision, touched.Revision);

        // Creating or archiving a change directory is also an input of that source's progress.
        Directory.CreateDirectory(Path.Combine(second, "openspec/changes/another"));
        clock.Advance(TimeSpan.FromSeconds(3));
        await catalog.GetAsync();
        Assert.Equal(2, command.Count("list", first));
        Assert.Equal(2, command.Count("list", second));

        // After the 60 s bound everything is resolved again, once.
        clock.Advance(TimeSpan.FromSeconds(61));
        await catalog.GetAsync();
        Assert.Equal(4, command.Count("worktree"));
        Assert.Equal(2, command.Count("launch"));
        Assert.Equal(2, command.Count("context", first));
        Assert.Equal(2, command.Count("context", second));
        Assert.Equal(3, command.Count("list", first));
        Assert.Equal(3, command.Count("list", second));
    }

    [Fact]
    public async Task ChangedRepositoryConfigurationIsReloadedAndInvalidatesCachedResolution()
    {
        using var box = new TestWorkspace();
        var first = Repository(box, "first");
        var second = Repository(box, "second");
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(first, ".git")] = new(new("first"));
        settings.Save();
        var command = new CountingCommand(first);
        var clock = new TestClock();
        var catalog = new SourceCatalog(settings, new DashboardJournal(box.Data), command, first, clock);
        Assert.Equal(["first"], (await catalog.GetAsync()).Repositories.Select(r => r.Id));
        var other = new LocalSettings(box.Data);
        other.Configuration.RepositoryContexts[Path.Combine(second, ".git")] = new(new("second"));
        other.Save();
        clock.Advance(TimeSpan.FromSeconds(3));
        var reloaded = await catalog.GetAsync();
        Assert.Equal(["first", "second"], reloaded.Repositories.Select(r => r.Id).Order(StringComparer.Ordinal));
        Assert.Equal(2, reloaded.Sources.Length);
    }

    [Fact]
    public async Task DocumentEditsChangeTheRevisionAndJournalEntriesChangeTheSnapshot()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        var design = DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\nOne");
        var before = await setup.Catalog.GetAsync(force: true);
        Assert.Equal(0, before.JournalSnapshot);
        File.WriteAllText(design, "# Design\nOne and more");
        var edited = await setup.Catalog.GetAsync(force: true);
        Assert.NotEqual(before.Revision, edited.Revision);
        Assert.Equal(edited.Revision, (await setup.Catalog.GetAsync(force: true)).Revision);
        new Journal(new LocalSettings(box.Data), new("repo")).Log(new("in_progress", "demo"));
        var logged = await setup.Catalog.GetAsync(force: true);
        Assert.True(logged.JournalSnapshot > edited.JournalSnapshot);
        Assert.Equal(edited.Revision, logged.Revision);
        new Journal(new LocalSettings(box.Data), new("repo")).Log(new("in_progress", "demo"));
        Assert.True((await setup.Catalog.GetAsync(force: true)).JournalSnapshot > logged.JournalSnapshot);
    }

    [Fact]
    public async Task ConsecutiveSearchesReuseCachedReadsAndEvictOldestFirst()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "needle one");
        DashboardTests.Write(box, "openspec/changes/demo/proposal.md", "needle two");
        var first = await setup.Documents.SearchAsync(new("needle"));
        Assert.Equal(2, first.Matches.Length);
        var reads = setup.Documents.HashReads;
        Assert.True(reads > 0);
        var second = await setup.Documents.SearchAsync(new("needle"));
        Assert.Equal(2, second.Matches.Length);
        Assert.Equal(reads, setup.Documents.HashReads);
        // An edited file is read again.
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "needle one, edited");
        Assert.Contains("edited", (await setup.Documents.SearchAsync(new("needle"))).Matches.Single(m => m.Path == "design.md").Excerpt);
        Assert.Equal(reads + 1, setup.Documents.HashReads);

        for (var i = 0; i < 300; i++)
            DashboardTests.Write(box, $"openspec/changes/many/doc-{i:000}.md", "body " + i);
        var source = Assert.Single((await setup.Catalog.GetAsync(force: true)).Sources).Id;
        for (var i = 0; i < 300; i++)
            await setup.Documents.GetAsync(new(source, "many", $"doc-{i:000}.md"));
        var afterScan = setup.Documents.HashReads;
        for (var i = 290; i < 300; i++)
            await setup.Documents.GetAsync(new(source, "many", $"doc-{i:000}.md"));
        Assert.Equal(afterScan, setup.Documents.HashReads);
        await setup.Documents.GetAsync(new(source, "many", "doc-000.md"));
        Assert.Equal(afterScan + 1, setup.Documents.HashReads);
    }

    [Fact]
    public async Task SearchReportsOnlyUnavailableSourcesWithinTheRequestedRepositories()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "needle");
        new Journal(new LocalSettings(box.Data), new("elsewhere")).Log(new("in_progress", "other-work"));
        var snapshot = await setup.Catalog.GetAsync(force: true);
        Assert.Contains(snapshot.Notices, n => n.Contains("elsewhere", StringComparison.Ordinal));
        var scoped = await setup.Documents.SearchAsync(new("needle", ["repo"]));
        Assert.Single(scoped.Matches);
        Assert.Empty(scoped.Unavailable);
        var everywhere = await setup.Documents.SearchAsync(new("needle"));
        Assert.Contains(everywhere.Unavailable, n => n.StartsWith("elsewhere:", StringComparison.Ordinal));
        var requested = await setup.Documents.SearchAsync(new("needle", ["elsewhere"]));
        Assert.Contains(requested.Unavailable, n => n.StartsWith("elsewhere:", StringComparison.Ordinal));
        Assert.Empty(requested.Matches);
    }

    [Fact]
    public async Task SectionLabelsIgnoreHeadingLookalikesInsideFencedCode()
    {
        using var box = new TestWorkspace();
        var setup = DashboardTests.Setup(box);
        DashboardTests.Write(box, "openspec/changes/demo/design.md",
            "# Real heading\n\n```sh\n# comment, not a heading\nneedle inside\n```\n\n~~~~\n# also not a heading\n```\nneedle still fenced\n~~~~\n\nneedle outside\n\n## Later heading\nneedle later\n");
        var matches = (await setup.Documents.SearchAsync(new("needle"))).Matches.OrderBy(m => m.Line).ToArray();
        Assert.Equal(["Real heading", "Real heading", "Real heading", "Later heading"], matches.Select(m => m.Section));
    }

    [Fact]
    public async Task PreferencesAreRejectedAboveTheLimitAndUnknownLinkSourcesAreNotFound()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n");
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "reliability", authorization, new DashboardTests.FixtureCommand(box.Repo));
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(DashboardHost.Address(app)) };
        await client.PostAsJsonAsync("/api/bootstrap", new
        {
            token = authorization.Bootstrap()
        });

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/state", new
        {
            schemaVersion = 1,
            theme = "light"
        })).StatusCode);
        var oversize = await client.PutAsJsonAsync("/api/state", new
        {
            schemaVersion = 1,
            padding = new string('x', 70_000)
        });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversize.StatusCode);
        Assert.Contains("64 KiB", await oversize.Content.ReadAsStringAsync());
        Assert.Equal("light", (await client.GetFromJsonAsync<JsonElement>("/api/state")).GetProperty("theme").GetString());

        var unknown = await client.PostAsJsonAsync("/api/link", new
        {
            document = new
            {
                sourceId = "missing",
                change = "demo",
                path = "design.md"
            },
            target = "proposal.md"
        });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        await app.StopAsync();
    }

    [Fact]
    public async Task UnchangedKnownRevisionSkipsTheTextAndOtherRequestsKeepTheFullResponse()
    {
        using var box = new TestWorkspace();
        var settings = new LocalSettings(box.Data);
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var path = DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Design\n\n- [ ] 1.1 A task\n");
        var authorization = new DashboardAuthorization(box.Data);
        await using var app = DashboardHost.Create(settings, box.Repo, 0, "unchanged", authorization, new DashboardTests.FixtureCommand(box.Repo));
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(DashboardHost.Address(app)) };
        await client.PostAsJsonAsync("/api/bootstrap", new
        {
            token = authorization.Bootstrap()
        });
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/catalog");
        var source = catalog.GetProperty("sources")[0].GetProperty("id").GetString()!;
        async Task<JsonElement> Post(object body) => await (await client.PostAsJsonAsync("/api/document", body)).Content.ReadFromJsonAsync<JsonElement>();

        var full = await Post(new
        {
            sourceId = source,
            change = "demo",
            path = "design.md"
        });
        var revision = full.GetProperty("revision").GetString()!;
        Assert.Equal("# Design\n\n- [ ] 1.1 A task\n", full.GetProperty("text").GetString());
        Assert.Equal(1, full.GetProperty("tasks").GetArrayLength());
        Assert.False(full.TryGetProperty("unchanged", out _));

        var unchanged = await Post(new
        {
            sourceId = source,
            change = "demo",
            path = "design.md",
            knownRevision = revision
        });
        Assert.True(unchanged.GetProperty("unchanged").GetBoolean());
        Assert.Equal(revision, unchanged.GetProperty("revision").GetString());
        Assert.False(unchanged.TryGetProperty("text", out _));
        Assert.False(unchanged.TryGetProperty("tasks", out _));

        var stale = await Post(new
        {
            sourceId = source,
            change = "demo",
            path = "design.md",
            knownRevision = "0000"
        });
        Assert.Equal(revision, stale.GetProperty("revision").GetString());
        Assert.False(stale.TryGetProperty("unchanged", out _));
        Assert.Equal(full.GetProperty("text").GetString(), stale.GetProperty("text").GetString());

        File.WriteAllText(path, "# Design\n\nChanged by an agent\n");
        var changed = await Post(new
        {
            sourceId = source,
            change = "demo",
            path = "design.md",
            knownRevision = revision
        });
        Assert.False(changed.TryGetProperty("unchanged", out _));
        Assert.NotEqual(revision, changed.GetProperty("revision").GetString());
        Assert.Contains("Changed by an agent", changed.GetProperty("text").GetString());
        await app.StopAsync();
    }

    /// <summary>Answers every <c>openspec context</c> with the timeout result and counts the spawns.</summary>
    private sealed class TimeoutCommand(string launch) : LocalCommand
    {
        private int contexts;
        public int Contexts => contexts;
        public override Task<CommandOutput> RunAsync(string executable, string directory, string[] arguments, CancellationToken cancellationToken)
        {
            if (executable == "git") return Task.FromResult(new CommandOutput(true, 0, arguments[0] == "--git-dir" ? "" : launch + "\n" + Path.Combine(launch, ".git"), ""));
            if (arguments[0] == "context")
            {
                Interlocked.Increment(ref contexts);
                return Task.FromResult(new CommandOutput(true, -1, "", "Local command timed out."));
            }
            return Task.FromResult(new CommandOutput(true, 0, "{\"changes\":[]}", ""));
        }
    }

    [Fact]
    public async Task TimedOutCliResultIsRememberedForABoundedBackoffAndStillReported()
    {
        using var box = new TestWorkspace();
        var repo = Repository(box, "first");
        var command = new TimeoutCommand(repo);
        var clock = new TestClock();
        var catalog = new SourceCatalog(new LocalSettings(box.Data), new DashboardJournal(box.Data), command, repo, clock);
        await catalog.GetAsync();
        Assert.Equal(1, command.Contexts);
        Assert.True(SourceCatalog.TimeoutBackoff <= SourceCatalog.ExpensiveLifetime);

        // Refreshes inside the backoff window reuse the timeout and keep reporting it as the source error.
        for (var elapsed = 3; elapsed < SourceCatalog.TimeoutBackoff.TotalSeconds; elapsed += 3)
        {
            clock.Advance(TimeSpan.FromSeconds(3));
            var snapshot = await catalog.GetAsync();
            Assert.Contains("timed out", Assert.Single(snapshot.Sources).Error);
        }
        Assert.Equal(1, command.Contexts);

        // After the backoff the CLI is tried once more.
        clock.Advance(SourceCatalog.TimeoutBackoff);
        await catalog.GetAsync();
        Assert.Equal(2, command.Contexts);

        // A changed input stamp (here the OpenSpec config) bypasses the negative entry.
        File.WriteAllText(Path.Combine(repo, "openspec", "config.yaml"), "schema: spec-driven\n");
        clock.Advance(TimeSpan.FromSeconds(3));
        await catalog.GetAsync();
        Assert.Equal(3, command.Contexts);
    }

    [Fact]
    public async Task LinkAtANestedDirectoryBelowAChangeCannotEscapeTheSource()
    {
        using var box = new TestWorkspace();
        var repo = Repository(box, "first");
        var external = Path.Combine(box.Root, "external");
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, "secret.md"), "Secret");
        var nested = Path.Combine(repo, "openspec/changes/demo/specs");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "spec.md"), "# Spec");
        var link = Path.GetFullPath(Path.Combine(nested, "escape"));
        if (OperatingSystem.IsWindows())
        {
            // Directory junctions need no elevation.
            var made = await new LocalCommand().RunAsync("cmd.exe", repo, ["/d", "/c", "mklink", "/J", link, external], CancellationToken.None);
            Assert.Equal(0, made.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, external);
        try
        {
            var catalog = new SourceCatalog(new LocalSettings(box.Data), new DashboardJournal(box.Data), new CountingCommand(repo), repo, new TestClock());
            var snapshot = await catalog.GetAsync();
            Assert.DoesNotContain(snapshot.Changes, c => c.DirectoryName == "demo");
            Assert.Contains(snapshot.Notices, n => n.Contains("demo", StringComparison.Ordinal) && n.Contains("escapes", StringComparison.Ordinal));
            Assert.DoesNotContain(snapshot.Changes.SelectMany(c => c.Documents), d => d.Path.Contains("secret", StringComparison.Ordinal));
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public async Task LinkWhoseTargetHasALinkedParentCannotEscapeTheSource()
    {
        // <repo>/.../demo/specs/hop -> <box>/other/c/d, while <box>/other/c is itself a link to <box>/external.
        using var box = new TestWorkspace();
        var repo = Repository(box, "first");
        var external = Path.Combine(box.Root, "external");
        Directory.CreateDirectory(Path.Combine(external, "d"));
        File.WriteAllText(Path.Combine(external, "d", "secret.md"), "Secret");
        var other = Path.Combine(box.Root, "other");
        Directory.CreateDirectory(other);
        var specs = Path.Combine(repo, "openspec/changes/demo/specs");
        Directory.CreateDirectory(specs);
        File.WriteAllText(Path.Combine(specs, "spec.md"), "# Spec");
        async Task Link(string link, string target)
        {
            if (OperatingSystem.IsWindows())
            {
                var made = await new LocalCommand().RunAsync("cmd.exe", repo, ["/d", "/c", "mklink", "/J", link, target], CancellationToken.None);
                Assert.Equal(0, made.ExitCode);
            }
            else Directory.CreateSymbolicLink(link, target);
        }
        var inner = Path.GetFullPath(Path.Combine(other, "c"));
        var hop = Path.GetFullPath(Path.Combine(specs, "hop"));
        await Link(inner, external);
        try
        {
            await Link(hop, Path.Combine(inner, "d"));
            try
            {
                Assert.Equal(Path.Combine(Path.GetFullPath(external), "d"), SourcePath.Canonical(hop), ignoreCase: OperatingSystem.IsWindows());
                var catalog = new SourceCatalog(new LocalSettings(box.Data), new DashboardJournal(box.Data), new CountingCommand(repo), repo, new TestClock());
                var snapshot = await catalog.GetAsync();
                Assert.DoesNotContain(snapshot.Changes, c => c.DirectoryName == "demo");
                Assert.Contains(snapshot.Notices, n => n.Contains("demo", StringComparison.Ordinal) && n.Contains("escapes", StringComparison.Ordinal));
            }
            finally { Directory.Delete(hop); }
        }
        finally { Directory.Delete(inner); }
    }

    [Fact]
    public void LinkLoopFailsClosedInsteadOfLooping()
    {
        using var box = new TestWorkspace();
        var a = Path.Combine(box.Root, "loop-a");
        var b = Path.Combine(box.Root, "loop-b");
        try { Directory.CreateSymbolicLink(a, b); Directory.CreateSymbolicLink(b, a); }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException) { return; } // symbolic links need elevation on Windows
        try { Assert.Throws<IOException>(() => SourcePath.Canonical(Path.Combine(a, "x"))); }
        finally { Directory.Delete(a); Directory.Delete(b); }
    }

    [Fact]
    public async Task LinkInsideTheSourceIsFollowedOnceAndKeepsDocumentOrderAndStamps()
    {
        using var box = new TestWorkspace();
        var repo = Repository(box, "first");
        var change = Path.Combine(repo, "openspec/changes/demo");
        Directory.CreateDirectory(Path.Combine(change, "specs/a"));
        File.WriteAllText(Path.Combine(change, "specs/a/spec.md"), "# A");
        File.WriteAllText(Path.Combine(change, "proposal.md"), "# Proposal");
        var link = Path.GetFullPath(Path.Combine(change, "specs", "alias"));
        if (OperatingSystem.IsWindows())
        {
            var made = await new LocalCommand().RunAsync("cmd.exe", repo, ["/d", "/c", "mklink", "/J", link, Path.GetFullPath(Path.Combine(change, "specs", "a"))], CancellationToken.None);
            Assert.Equal(0, made.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, Path.GetFullPath(Path.Combine(change, "specs", "a")));
        try
        {
            var catalog = new SourceCatalog(new LocalSettings(box.Data), new DashboardJournal(box.Data), new CountingCommand(repo), repo, new TestClock());
            var demo = Assert.Single((await catalog.GetAsync()).Changes, c => c.DirectoryName == "demo");
            Assert.Equal(["design.md", "proposal.md", "tasks.md", "specs/a/spec.md"], demo.Documents.Select(d => d.Path));
            string Stamp(string relative)
            {
                var info = new FileInfo(Path.Combine(change, relative));
                return relative + "=" + info.Length + "@" + info.LastWriteTimeUtc.Ticks;
            }
            var stamps = new[] { "design.md", "proposal.md", "specs/a/spec.md", "tasks.md" }.Select(Stamp).ToArray();
            Assert.Equal(WorkLog.Infrastructure.ContentHash.Compute(string.Join('\n', stamps)), demo.ContentStamp);
            Assert.Equal(stamps[3], demo.TaskStamp);
        }
        finally { Directory.Delete(link); }
    }
}
