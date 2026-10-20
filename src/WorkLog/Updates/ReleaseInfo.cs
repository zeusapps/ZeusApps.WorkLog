using System.ComponentModel;
using System.Text.RegularExpressions;

namespace WorkLog.Updates;

public sealed record ReleaseInfo(string Version, string ReleaseUrl, string PackageUrl, string ChecksumUrl)
{
    public const string Repository = "zeusapps/ZeusApps.WorkLog";
    public const string ReleasesUrl = "https://github.com/" + Repository + "/releases";
    public const string Endpoint = "https://api.github.com/repos/" + Repository + "/releases/latest";

    public static Version? ParseVersion(string? text)
    {
        if (text is null || text.Length > 64 || !Regex.IsMatch(text, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant))
            return null;
        return System.Version.TryParse(text, out var version) ? version : null;
    }

    public bool IsNewerThan(string installed) => ParseVersion(Version) is { } available
        && ParseVersion(installed) is { } current && available > current;

    public bool IsValid() => ParseVersion(Version) is not null
        && ReleaseUrl == ReleasesUrl + "/tag/v" + Version
        && PackageUrl == DownloadUrl(Version)
        && ChecksumUrl == DownloadUrl(Version) + ".sha256";

    public static string PackageName(string version) => $"worklog-{version}-win-x64.zip";
    public static string DownloadUrl(string version) => ReleasesUrl + "/download/v" + version + "/" + PackageName(version);
}

public sealed record UpdateHint(
    [property: Description("Version of the running WorkLog executable.")] string InstalledVersion,
    [property: Description("Newer stable WorkLog version available from the official repository.")] string AvailableVersion,
    [property: Description("Official release page for this version.")] string ReleaseUrl,
    [property: Description("Explicit update command. Stop affected MCP clients first and restart them afterward; a hint does not authorize installation.")] string Command);

public sealed record ReleaseCheck(ReleaseInfo? Release, string? Error = null, DateTimeOffset? RetryAfter = null)
{
    public bool Succeeded => Error is null;
}
