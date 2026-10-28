using WorkLog.Installation;

namespace WorkLog.DevInstall;

/// <summary>The installation, data directory and workspace an operation acts on. Anything other than the default
/// per-user installation is a disposable target and must live entirely under the repository's artifacts folder.</summary>
public sealed record DevTargets(string Repository, string InstallationRoot, string DataDirectory, string Workspace, bool SkipProbe)
{
    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string DefaultInstallationRoot => Path.Combine(LocalAppData, "Programs", "WorkLog");
    public static string DefaultDataDirectory => Path.GetFullPath(Environment.GetEnvironmentVariable("WORKLOG_DATA_DIR")
        ?? Path.Combine(LocalAppData, "ZeusApps.WorkLog"));

    public bool IsDefaultInstallation => Same(InstallationRoot, DefaultInstallationRoot);

    public static DevTargets Resolve(string repository, string? installation, string? data, string? workspace, bool skipProbe)
    {
        repository = Full(repository);
        var artifacts = Path.Combine(repository, "artifacts");
        var root = Full(installation ?? DefaultInstallationRoot);
        var dataDirectory = Full(data ?? DefaultDataDirectory);
        var space = Full(workspace ?? Path.Combine(artifacts, "dev-builds", "operations"));
        var disposable = !Same(root, DefaultInstallationRoot) || !Same(dataDirectory, DefaultDataDirectory) || skipProbe;
        if (skipProbe && Same(root, DefaultInstallationRoot))
            throw new InvalidOperationException("The disposable switches (custom installation or data directory, skipped health probe) are refused for the default installation.");
        if (disposable && !(Within(artifacts, root) && Within(artifacts, dataDirectory)))
            throw new InvalidOperationException("The disposable switches only work for an installation and data directory under the repository artifacts folder.");
        if (!Within(artifacts, space))
            throw new InvalidOperationException("The workspace must live under the repository artifacts folder.");
        InstallationLayout.EnsureNoReparsePoints(root);
        InstallationLayout.EnsureNoReparsePoints(dataDirectory);
        InstallationLayout.EnsureNoReparsePoints(space);
        return new(repository, root, dataDirectory, space, skipProbe);
    }

    public static string Full(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    public static bool Same(string left, string right) => Full(left).Equals(Full(right), StringComparison.OrdinalIgnoreCase);
    public static bool Within(string parent, string child) =>
        Full(child).StartsWith(Full(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
