namespace WorkLog.Configuration;

public sealed class RepositoryContext(RepositoryConfig repository)
{
    public RepositoryConfig Repository { get; set; } = repository;
    public Dictionary<string, bool> Instructions { get; set; } = new();
    // Retain legacy records for recovery, but do not treat promoted copies as authoritative.
    public List<string> LegacyPaths { get; set; } = [];

    public bool InstructionsEnabled(string client) => !Instructions.TryGetValue(client, out var enabled) || enabled;
}
