using System.IO.Compression;
using WorkLog.Installation;

namespace WorkLog.Updates;

public static partial class ReleasePackage
{
    public static string ExtractVersioned(string archivePath, string destination, string expectedVersion)
    {
        if (InstallationLayout.ParseVersion(expectedVersion) is null)
            throw new InvalidOperationException("Invalid package version.");
        InstallationLayout.EnsureNoReparsePoints(destination);
        Directory.CreateDirectory(destination);
        if (Directory.EnumerateFileSystemEntries(destination).Any())
            throw new IOException("Extraction requires an empty staging directory.");
        var prefix = "versions/" + expectedVersion + "/";
        var required = new[] { "worklog.exe", "launcher.json", "current.json", "README.md", "INSTALL.md",
            prefix + "release.json", prefix + "worklog.exe", prefix + "README.md", prefix + "INSTALL.md" };
        var allowed = required.Append(prefix + "worklog.pdb").ToHashSet(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count is < 9 or > 16 || new FileInfo(archivePath).Length > MaximumPackageBytes)
            throw new IOException("Unexpected versioned package size or entry count.");
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            var directory = name is "versions/" || name == prefix;
            if (!names.Add(name) || !allowed.Contains(name) && !directory
                || (entry.ExternalAttributes >> 16 & 0xF000) == 0xA000
                || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new IOException("Unsafe or unexpected release archive entry: " + name);
            total = checked(total + entry.Length);
            if (entry.Length < 0 || entry.Length > 512L * 1024 * 1024 || total > 600L * 1024 * 1024 || directory && entry.Length != 0)
                throw new IOException("Extracted release exceeds its bounds.");
            if (directory)
                continue;
            var target = Path.Combine(destination, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var source = entry.Open();
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var buffer = new byte[65536];
            long copied = 0;
            int count;
            while ((count = source.Read(buffer)) != 0)
            {
                copied = checked(copied + count);
                if (copied > entry.Length)
                    throw new IOException("Archive entry expanded beyond its declared bound.");
                output.Write(buffer, 0, count);
            }
            if (copied != entry.Length)
                throw new IOException("Archive entry length differs from its declaration.");
        }
        if (required.Any(name => !names.Contains(name)))
            throw new IOException("Missing required versioned package files.");
        var selection = InstallationLayout.ReadSelection(destination);
        if (selection.Current.Version != expectedVersion || selection.Previous is not null || selection.Generation != 1)
            throw new InvalidOperationException("Package selection differs from the requested fresh-install version.");
        var payload = InstallationLayout.PayloadDirectory(destination, expectedVersion);
        var manifest = InstallationLayout.ReadPayload(payload);
        if (names.Contains(prefix + "worklog.pdb") != manifest.Files.ContainsKey("worklog.pdb"))
            throw new InvalidOperationException("Package payload files differ from the manifest.");
        return payload;
    }
}
