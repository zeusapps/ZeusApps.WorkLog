using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using WorkLog.Infrastructure;

namespace WorkLog.Updates;

public sealed record ReleaseManifest(string Version, string Runtime, bool SelfContained);

public static partial class ReleasePackage
{
    public const long MaximumPackageBytes = 256L * 1024 * 1024;
    public static readonly string[] ManagedFiles = ["worklog.exe", "release.json", "README.md", "INSTALL.md", "worklog.pdb"];

    public static ReleaseManifest ReadManifest(string directory)
    {
        var path = Path.Combine(directory, "release.json");
        if (!File.Exists(path) || new FileInfo(path).Length > 4096)
            throw new InvalidOperationException("Expected a packaged Windows installation with release.json. Use the manual ZIP installation instructions.");
        var manifest = Json.Decode<ReleaseManifest>(File.ReadAllText(path));
        if (ReleaseInfo.ParseVersion(manifest.Version) is null || manifest.Runtime != "win-x64" || !manifest.SelfContained)
            throw new InvalidOperationException("Unsupported release manifest. Use the official Windows x64 package.");
        return manifest;
    }

    public static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static string VerifyChecksum(string archive, string checksum, string packageName)
    {
        if (new FileInfo(archive).Length > MaximumPackageBytes || new FileInfo(checksum).Length > 4096)
            throw new InvalidOperationException("Release package/checksum exceeds the size limit.");
        var parts = File.ReadAllText(checksum).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || parts[1] != packageName || parts[0].Length != 64 || !parts[0].All(Uri.IsHexDigit))
            throw new InvalidOperationException("Invalid package checksum file.");
        var hash = Hash(archive);
        if (!hash.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Package checksum mismatch; installed files were not changed.");
        return hash;
    }

    public static void Extract(string archivePath, string destination, string expectedVersion)
    {
        Directory.CreateDirectory(destination);
        if (Directory.EnumerateFileSystemEntries(destination).Any())
            throw new IOException("Extraction requires an empty staging directory.");
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count is < 4 or > 16)
            throw new IOException("Unexpected package entry count.");
        long total = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var path = Path.GetFullPath(Path.Combine(root, entry.FullName));
            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (!path.StartsWith(root, StringComparison.Ordinal) || !ManagedFiles.Contains(entry.FullName, StringComparer.Ordinal)
                || !names.Add(entry.FullName) || unixType == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new IOException("Unsafe or unexpected release archive entry: " + entry.FullName);
            total = checked(total + entry.Length);
            if (entry.Length < 0 || entry.Length > 512L * 1024 * 1024 || total > 600L * 1024 * 1024)
                throw new IOException("Extracted release exceeds the size limit.");
            entry.ExtractToFile(path, overwrite: false);
        }
        if (ManagedFiles.Take(4).Any(name => !names.Contains(name)) || ReadManifest(destination).Version != expectedVersion)
            throw new InvalidOperationException("Package version or required files do not match the selected release.");
    }

    public static async Task VerifyExecutableAsync(string directory, string version, CancellationToken token)
    {
        var start = new ProcessStartInfo(Path.Combine(directory, "worklog.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory
        };
        start.ArgumentList.Add("--version");
        using var process = Process.Start(start) ?? throw new IOException("Could not start the release version check.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        // A bounded --version subprocess is the only child the updater may terminate on timeout.
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch
        {
            if (!process.HasExited)
                process.Kill();
            throw;
        }
        if (process.ExitCode != 0 || (await output).Trim() != version)
            throw new InvalidOperationException("Release executable version verification failed: " + await error);
        await error;
    }
}
