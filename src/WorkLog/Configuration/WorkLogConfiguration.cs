namespace WorkLog.Configuration;

public sealed class WorkLogConfiguration
{
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
