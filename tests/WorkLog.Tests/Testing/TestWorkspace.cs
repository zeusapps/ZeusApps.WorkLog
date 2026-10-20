using Microsoft.Data.Sqlite;
using WorkLog.Repositories;

namespace WorkLog.Tests.Testing;

public sealed class TestWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "worklog-tests", Guid.NewGuid().ToString("N"));
    public string Data => Path.Combine(Root, "data");
    public string Home => Path.Combine(Root, "home");
    public string Repo => Path.Combine(Root, "repo");
    public TestWorkspace()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Repo);
    }
    public void Git(params string[] args) => GitRepository.Run(Repo, args);
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "worklog-tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(Root).StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid test cleanup path.");
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(Root, true);
    }
}
