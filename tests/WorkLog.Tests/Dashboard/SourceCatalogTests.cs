using WorkLog.Configuration;
using WorkLog.Dashboard;
using WorkLog.Tests.Testing;
using Xunit;
using JournalJson = WorkLog.Infrastructure.Json;

namespace WorkLog.Tests.Dashboard;

public sealed class SourceCatalogTests
{
    [Fact]
    public async Task StandaloneStoreAndItsSelectionRemainAuthoritative()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        DashboardTests.Write(box, "openspec/config.yaml", "store: planning\n");
        var store = Path.Combine(box.Root, "store");
        Directory.CreateDirectory(Path.Combine(store, "openspec/changes/demo"));
        File.WriteAllText(Path.Combine(store, "openspec/changes/demo/design.md"), "# Store design");
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var command = new StoreCommand(box.Repo, store);
        var catalog = new SourceCatalog(settings, new DashboardJournal(box.Data), command, box.Repo);
        var snapshot = await catalog.GetAsync();
        Assert.Equal(store, Assert.Single(snapshot.Sources).Location);
        Assert.True(command.UsedStoreSelection);
        Assert.Single(snapshot.Changes);
        Assert.False(File.Exists(settings.ConfigPath));
    }

    [Fact]
    public async Task MissingCliFallsBackOnlyForUnambiguousLocalRoots()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(box.Repo, ".git"));
        var config = DashboardTests.Write(box, "openspec/config.yaml", "schema: spec-driven\n");
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "# Local design");
        var settings = new LocalSettings(box.Data);
        settings.Configuration.RepositoryContexts[Path.Combine(box.Repo, ".git")] = new(new("repo"));
        var catalog = new SourceCatalog(settings, new DashboardJournal(box.Data), new MissingCommand(box.Repo), box.Repo);
        var local = await catalog.GetAsync();
        Assert.Single(local.Changes);
        Assert.Null(Assert.Single(local.Changes).TotalTasks);
        File.WriteAllText(config, "store: unavailable\n");
        var unresolved = await catalog.GetAsync(force: true);
        Assert.Empty(unresolved.Changes);
        Assert.Contains("store cannot be resolved", Assert.Single(unresolved.Sources).Error);
    }

    [Fact]
    public async Task DuplicateArchivesRequireChoiceAndDatedNamesStillResolve()
    {
        using var box = new TestWorkspace(); var setup = DashboardTests.Setup(box);
        DashboardTests.Write(box, "openspec/changes/archive/2026-10-20-demo/design.md", "First");
        DashboardTests.Write(box, "openspec/changes/archive/2026-10-21-demo/design.md", "Second");
        var snapshot = await setup.Catalog.GetAsync();
        var source = Assert.Single(snapshot.Sources).Id;
        Assert.Throws<DashboardConflictException>(() => SourceCatalog.Resolve(snapshot, source, "demo"));
        Assert.Equal("2026-10-20-demo", SourceCatalog.Resolve(snapshot, source, "2026-10-20-demo").DirectoryName);
        Assert.Equal("First", (await setup.Documents.GetAsync(new(source, "demo", "design.md", snapshot.Changes[0].Variant))).Text);
    }

    [Fact]
    public async Task FilesystemLinksCannotEscapeTheDocumentRoot()
    {
        using var box = new TestWorkspace(); var setup = DashboardTests.Setup(box);
        DashboardTests.Write(box, "openspec/changes/demo/design.md", "Allowed");
        var outside = Path.Combine(box.Root, "outside.md"); File.WriteAllText(outside, "Secret");
        var snapshot = await setup.Catalog.GetAsync();
        var source = Assert.Single(snapshot.Sources).Id;
        var link = Path.Combine(box.Repo, "openspec/changes/demo/escape.md");
        try { File.CreateSymbolicLink(link, outside); }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        {
            // Directory junctions are available to ordinary Windows users without developer mode.
            var target = Path.Combine(box.Root, "external"); Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "outside.md"), "Secret");
            var junction = Path.Combine(box.Repo, "openspec/changes/demo/escape");
            var result = await new LocalCommand().RunAsync("cmd.exe", box.Repo, ["/d", "/c", "mklink", "/J", junction, target], CancellationToken.None);
            Assert.Equal(0, result.ExitCode);
            try { await Assert.ThrowsAsync<ArgumentException>(() => setup.Documents.GetAsync(new(source, "demo", "escape/outside.md"))); }
            finally { Directory.Delete(junction); }
            return;
        }
        try { await Assert.ThrowsAsync<ArgumentException>(() => setup.Documents.GetAsync(new(source, "demo", "escape.md"))); }
        finally { File.Delete(link); }
    }

    private sealed class StoreCommand(string repo, string store) : LocalCommand
    {
        public bool UsedStoreSelection { get; private set; }
        public override Task<CommandOutput> RunAsync(string executable, string directory, string[] arguments, CancellationToken cancellationToken)
        {
            if (executable == "git") return Task.FromResult(new CommandOutput(true, 0, arguments[0] == "--git-dir" ? "worktree " + repo : repo + "\n" + Path.Combine(repo, ".git"), ""));
            if (arguments[0] == "context") return Task.FromResult(new CommandOutput(true, 0, JournalJson.Encode(new { root = new { path = store, store_id = "planning" } }), ""));
            UsedStoreSelection = arguments.SequenceEqual(["list", "--json", "--store", "planning"]);
            return Task.FromResult(new CommandOutput(true, 0, "{\"changes\":[]}", ""));
        }
    }
    private sealed class MissingCommand(string repo) : LocalCommand
    {
        public override Task<CommandOutput> RunAsync(string executable, string directory, string[] arguments, CancellationToken cancellationToken)
            => executable == "git" ? new DashboardTests.FixtureCommand(repo).RunAsync(executable, directory, arguments, cancellationToken)
                : Task.FromResult(new CommandOutput(false, -1, "", "Not installed"));
    }
}
