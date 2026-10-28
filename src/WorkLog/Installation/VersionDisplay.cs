using System.Globalization;

namespace WorkLog.Installation;

/// <summary>The one text that names a running or selected version: the plain version for stable builds,
/// <c>0.3.9001 dev 50df7b8 [dirty] (2026-10-28 09:12 UTC)</c> for marked development builds, and
/// <c>0.3.9001 dev (unmarked)</c> for development-range payloads without a valid marker.</summary>
public static class VersionDisplay
{
    public static string Format(string version, BuildMarker? marker = null)
    {
        if (!InstallationLayout.IsDevVersion(version))
            return version;
        if (InstallationLayout.ValidMarker(version, marker) is not { } valid)
            return version + " dev (unmarked)";
        var built = DateTimeOffset.Parse(valid.BuiltAt!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
            .UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return $"{version} dev {valid.Commit![..7].ToLowerInvariant()}{(valid.Dirty ? " dirty" : "")} ({built} UTC)";
    }

    /// <summary>Label for a payload published in an installation (its manifest supplies the marker).</summary>
    public static string ForInstalled(string? root, string version) =>
        InstallationLayout.IsDevVersion(version) && root is not null
            ? Format(version, InstallationLayout.TryReadManifest(root, version)?.Build)
            : Format(version);
}
