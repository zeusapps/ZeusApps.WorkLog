using System.Reflection;
using WorkLog.Installation;

namespace WorkLog.Cli;

public static class ApplicationInfo
{
    /// <summary>The bare MAJOR.MINOR.PATCH version. A development build's informational version is
    /// <c>0.3.9001-dev+commit[.dirty]</c>; only the leading numbers are the version.</summary>
    public static string Version => ParseVersion(typeof(ApplicationInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);

    internal static string ParseVersion(string informationalVersion) => informationalVersion.Split('+')[0].Split('-')[0];

    /// <summary>The label shown to people (see <see cref="VersionDisplay"/>) for the running payload.</summary>
    public static string DisplayVersion => display ??= ComputeDisplay();
    private static string? display;

    private static string ComputeDisplay()
    {
        var version = Version;
        if (!InstallationLayout.IsDevVersion(version))
            return version;
        try
        {
            var directory = Path.GetDirectoryName(Environment.ProcessPath);
            var path = directory is null ? null : Path.Combine(directory, "release.json");
            var manifest = path is not null && File.Exists(path) ? InstallationLayout.Read<PayloadManifest>(path) : null;
            return VersionDisplay.Format(version, manifest?.Version == version ? manifest.Build : null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return VersionDisplay.Format(version);
        }
    }
}
