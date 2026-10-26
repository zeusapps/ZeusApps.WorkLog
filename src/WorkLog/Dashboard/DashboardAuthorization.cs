using System.Collections.Concurrent;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace WorkLog.Dashboard;

internal sealed class DashboardAuthorization
{
    private readonly string path;
    private readonly byte[] key;
    private readonly ConcurrentDictionary<string, DateTimeOffset> bootstraps = new(StringComparer.Ordinal);
    public string CookieName { get; }
    public string Cookie { get; }

    public DashboardAuthorization(string directory)
    {
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "dashboard.secret");
        if (!File.Exists(path))
        {
            var newKey = RandomNumberGenerator.GetBytes(32);
            try
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                if (OperatingSystem.IsWindows())
                {
                    var security = new FileSecurity();
                    security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                    security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
                    new FileInfo(path).SetAccessControl(security);
                }
                else File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                stream.Write(newKey);
                stream.Flush(true);
            }
            catch (IOException) when (File.Exists(path)) { }
        }
        key = File.ReadAllBytes(path);
        if (key.Length != 32) throw new InvalidOperationException("Invalid dashboard authorization file. Remove dashboard.secret and relaunch to reset browser sessions.");
        Cookie = Sign("session-v1");
        CookieName = "worklog-" + Infrastructure.ContentHash.Compute(Path.GetFullPath(directory))[..12];
    }

    public string Sign(string message) => Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(message)));
    public bool Valid(string? value) => SecretExists() && EqualsSecret(value, Cookie);
    public bool ValidLaunch(string? value, string instance) => SecretExists() && EqualsSecret(value, Sign("launch:" + instance));
    private bool SecretExists()
    {
        try { return File.Exists(path) && CryptographicOperations.FixedTimeEquals(File.ReadAllBytes(path), key); }
        catch (IOException) { return false; }
    }
    private static bool EqualsSecret(string? a, string b) => a is not null && a.Length == b.Length
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    public string Bootstrap()
    {
        foreach (var item in bootstraps.Where(p => p.Value < DateTimeOffset.UtcNow)) bootstraps.TryRemove(item.Key, out _);
        if (bootstraps.Count > 100) throw new InvalidOperationException("Too many pending dashboard launches. Retry in a minute.");
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        bootstraps[token] = DateTimeOffset.UtcNow.AddMinutes(2);
        return token;
    }
    public bool Exchange(string token) => SecretExists() && bootstraps.TryRemove(token, out var expiry) && expiry >= DateTimeOffset.UtcNow;
}
