using WorkLog.Installation;

namespace WorkLog.Integration;

internal sealed record ServerRegistration(string Command, string[] Arguments)
{
    public static ServerRegistration ForRepository(string root, string dataDirectory, string? executableOverride)
    {
        var installedRoot = executableOverride is null ? InstalledApplication.CurrentRoot() : null;
        var executable = executableOverride ?? (installedRoot is null ? Environment.ProcessPath : Path.Combine(installedRoot, "worklog.exe"))
            ?? throw new InvalidOperationException("Cannot determine executable path.");
        var arguments = new List<string>();
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add(Path.Combine(AppContext.BaseDirectory, "worklog.dll"));
        }

        arguments.AddRange(["serve", "--repository", root, "--data-dir", dataDirectory]);
        return new ServerRegistration(executable, arguments.ToArray());
    }
}
