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
        ["hotfix"] = "bug"
    };

    public Dictionary<string, RepositoryConfig> Repositories { get; set; } = new();
}

public sealed class UpdateSettings
{
    public bool Enabled { get; set; } = true;
}
