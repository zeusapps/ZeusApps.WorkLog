using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorkLog.Installation;

public sealed record LauncherManifest(string Version, int LauncherContract, int SharedDataContract, int JournalSchema);
public sealed record PayloadManifest(string Version, string Runtime, bool SelfContained, int PackageFormat,
    int LauncherContract, int SharedDataContract, int JournalSchema, Dictionary<string, string> Files);
public sealed record PayloadReference(string Version, string ManifestHash);
public sealed record PayloadSelection(PayloadReference Current, PayloadReference? Previous, long Generation, string OperationId);

// Shared as source with the dependency-free launcher. No journal or network initialization belongs here.
public static class InstallationLayout
{
    public const int LauncherContract = 1, SharedDataContract = 1, JournalSchema = 2, PackageFormat = 2;
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static readonly string[] PayloadFiles = ["worklog.exe", "README.md", "INSTALL.md", "worklog.pdb"];

    public static Version? ParseVersion(string? value) => value is not null && value.Length <= 64
        && Regex.IsMatch(value, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant)
        && Version.TryParse(value, out var version) ? version : null;

    public static T Read<T>(string path, int maximumBytes = 16384)
    {
        EnsureNoReparsePoints(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (file.Length > maximumBytes)
            throw new InvalidOperationException("Installation metadata exceeds its size limit: " + path);
        using var memory = new MemoryStream();
        var buffer = new byte[4096];
        int count;
        while ((count = file.Read(buffer)) != 0)
        {
            if (memory.Length + count > maximumBytes)
                throw new InvalidOperationException("Installation metadata exceeds its size limit.");
            memory.Write(buffer, 0, count);
        }
        return JsonSerializer.Deserialize<T>(memory.ToArray(), JsonOptions)
            ?? throw new InvalidOperationException("Expected installation metadata: " + path);
    }

    public static string Hash(string path)
    {
        EnsureNoReparsePoints(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
    public static bool IsHash(string? hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
    public static string PayloadDirectory(string root, string version)
    {
        if (ParseVersion(version) is null)
            throw new InvalidOperationException("Invalid payload version.");
        return Path.Combine(Path.GetFullPath(root), "versions", version);
    }

    public static LauncherManifest ReadLauncher(string root)
    {
        EnsureNoReparsePoints(root);
        var launcher = Read<LauncherManifest>(Path.Combine(root, "launcher.json"));
        if (ParseVersion(launcher.Version) is null || launcher.LauncherContract != LauncherContract
            || launcher.SharedDataContract != SharedDataContract || launcher.JournalSchema != JournalSchema
            || !File.Exists(Path.Combine(root, "worklog.exe")))
            throw new InvalidOperationException("Unsupported launcher/data contract. Use the manual maintenance installation procedure.");
        EnsureNoReparsePoints(Path.Combine(root, "worklog.exe"));
        return launcher;
    }

    public static PayloadManifest ReadPayload(string directory)
    {
        var manifest = Read<PayloadManifest>(Path.Combine(directory, "release.json"));
        if (ParseVersion(manifest.Version) is null || manifest.Runtime != "win-x64" || !manifest.SelfContained
            || manifest.PackageFormat != PackageFormat || manifest.LauncherContract != LauncherContract
            || manifest.SharedDataContract != SharedDataContract || manifest.JournalSchema != JournalSchema
            || manifest.Files is null || manifest.Files.Count is < 3 or > 4
            || PayloadFiles.Take(3).Any(name => !manifest.Files.ContainsKey(name)))
            throw new InvalidOperationException("Unsupported payload/package/data contract. Use the manual maintenance installation procedure.");
        foreach (var (name, hash) in manifest.Files)
        {
            if (!PayloadFiles.Contains(name, StringComparer.Ordinal) || !IsHash(hash)
                || !string.Equals(Hash(Path.Combine(directory, name)), hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Payload file hash or name is invalid: " + name);
        }
        return manifest;
    }

    public static PayloadManifest ValidateReference(string root, PayloadReference reference)
    {
        if (reference is null || !IsHash(reference.ManifestHash))
            throw new InvalidOperationException("Invalid payload selection reference.");
        var directory = PayloadDirectory(root, reference.Version);
        if (!string.Equals(Hash(Path.Combine(directory, "release.json")), reference.ManifestHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selected payload manifest hash differs. Restore a verified selection using the maintenance instructions.");
        var manifest = ReadPayload(directory);
        if (manifest.Version != reference.Version)
            throw new InvalidOperationException("Payload directory and version differ.");
        return manifest;
    }

    public static PayloadSelection ReadSelection(string root, bool verifyPayload = true)
    {
        _ = ReadLauncher(root);
        var selection = Read<PayloadSelection>(Path.Combine(root, "current.json"));
        if (selection.Current is null || ParseVersion(selection.Current.Version) is null
            || !IsHash(selection.Current.ManifestHash) || selection.Generation < 1
            || !Guid.TryParseExact(selection.OperationId, "N", out _)
            || selection.Previous is { } previous && (ParseVersion(previous.Version) is null || !IsHash(previous.ManifestHash)))
            throw new InvalidOperationException("Invalid current.json. Restore a verified selection using the maintenance instructions.");
        if (verifyPayload)
            _ = ValidateReference(root, selection.Current);
        return selection;
    }

    public static string? FindRoot(string executable)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        var parent = Directory.GetParent(directory);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (parent is null || !parent.Name.Equals("versions", comparison) || ParseVersion(Path.GetFileName(directory)) is null)
            return null;
        var root = parent.Parent!.FullName;
        _ = ReadLauncher(root);
        var manifest = ReadPayload(directory);
        if (manifest.Version != Path.GetFileName(directory) || !Path.GetFileName(executable).Equals("worklog.exe", comparison))
            throw new InvalidOperationException("Invalid installed payload location.");
        return root;
    }

    public static void EnsureNoReparsePoints(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Installation paths must not contain symbolic links or reparse points: " + current);
    }
}
