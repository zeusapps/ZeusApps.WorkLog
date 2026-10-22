using System.Diagnostics.CodeAnalysis;
using WorkLog.Cli;

namespace WorkLog.Installation;

public static class InstalledApplication
{
    public static string? CurrentRoot() => Environment.ProcessPath is { } executable ? InstallationLayout.FindRoot(executable) : null;

    [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "Only a bundled payload can update a versioned installation.")]
    public static string RequireCurrent(string dataDirectory)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess || !string.IsNullOrEmpty(typeof(ApplicationInfo).Assembly.Location))
            throw new InvalidOperationException("Self-update requires a self-contained Windows versioned installation. Use the manual stop/reinstall/init/restart migration in INSTALL.md.");
        var root = CurrentRoot() ?? throw new InvalidOperationException("Legacy/unsupported installation. Use the manual stop/reinstall/init/restart migration in INSTALL.md.");
        var manifest = InstallationLayout.ReadPayload(Path.GetDirectoryName(Environment.ProcessPath!)!);
        if (manifest.Version != ApplicationInfo.Version)
            throw new InvalidOperationException("Running executable differs from its payload manifest.");
        ValidateDataSeparation(root, dataDirectory);
        return root;
    }

    public static void ValidateDataSeparation(string root, string dataDirectory)
    {
        if (Updates.UpdateInstallation.Contains(root, dataDirectory) || Updates.UpdateInstallation.Contains(dataDirectory, root))
            throw new InvalidOperationException("Keep the application installation and journal data in separate directories.");
        InstallationLayout.EnsureNoReparsePoints(root);
        InstallationLayout.EnsureNoReparsePoints(dataDirectory);
    }
}
