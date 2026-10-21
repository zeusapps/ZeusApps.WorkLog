namespace WorkLog.Configuration;

public sealed class WorkLogConfiguration
{
    public UpdateSettings Updates { get; set; } = new();
    public Dictionary<string, string> BranchClassifications
    {
        get; set;
    } = new()
    {
        ["feature"] = "feature",
        ["bugs"] = "bug",
        ["bugfix"] = "bug",
        ["hotfix"] = "bug"
    };

    public Dictionary<string, RepositoryConfig> Repositories { get; set; } = new();
    public Dictionary<string, RepositoryContext> RepositoryContexts { get; set; } = new();

    public void Normalize()
    {
        if (!BranchClassifications.Keys.Any(key => key.Equals("bugfix", StringComparison.OrdinalIgnoreCase)))
            BranchClassifications["bugfix"] = "bug";
    }

    public IEnumerable<RepositoryConfig> EffectiveRepositories()
    {
        var promoted = RepositoryContexts.Values.SelectMany(context => context.LegacyPaths)
            .ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        return RepositoryContexts.Values.Select(context => context.Repository)
            .Concat(Repositories.Where(pair => !promoted.Contains(pair.Key)).Select(pair => pair.Value));
    }
}

public sealed class UpdateSettings
{
    public bool Enabled { get; set; } = true;
}
