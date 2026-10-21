using WorkLog.Infrastructure;

namespace WorkLog.Integration;

internal sealed class InitializationPlan
{
    private sealed record FileChange(string Path, string Content, string? Before);
    private readonly List<FileChange> changes = [];
    private readonly HashSet<string> targets = [];

    public static string Read(string path) => File.Exists(path) ? File.ReadAllText(path) : "";

    public IEnumerable<string> ChangedPaths => changes.Select(change => change.Path);
    public IEnumerable<string> TargetPaths => targets;

    public void Add(string path, string content)
    {
        targets.Add(path);
        var before = File.Exists(path) ? File.ReadAllText(path) : null;
        if (before != content)
        {
            changes.Add(new FileChange(path, content, before));
        }
    }

    public void Prioritize(string path)
    {
        var change = changes.FirstOrDefault(candidate => candidate.Path == path);
        if (change is not null)
        {
            changes.Remove(change);
            changes.Insert(0, change);
        }
    }

    public List<string> Describe(bool dryRun) =>
        changes.Select(change => (dryRun ? "Would update " : "Updated ") + change.Path).ToList();

    public void Apply(string dataDirectory)
    {
        var applied = new List<string>();
        try
        {
            foreach (var change in changes)
            {
                // Planning can be followed by external edits; do not overwrite them.
                var current = File.Exists(change.Path) ? File.ReadAllText(change.Path) : null;
                if (current != change.Before)
                {
                    throw new IOException("File changed during initialization: " + change.Path);
                }

                if (change.Before is not null)
                {
                    var backup = Path.Combine(dataDirectory, "integration-backups", Guid.NewGuid().ToString("N") + ".bak");
                    AtomicFile.Write(backup, change.Before);
                }

                AtomicFile.Write(change.Path, change.Content);
                applied.Add(change.Path);
            }
        }
        catch (Exception exception)
        {
            throw new IOException($"Initialization incomplete: {exception.Message}. Applied: {string.Join(", ", applied)}. Rerun init after resolving the error; do not assume both clients are configured.", exception);
        }
    }
}
