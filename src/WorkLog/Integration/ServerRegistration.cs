namespace WorkLog.Integration;

internal sealed record ServerRegistration(string Command, string[] Arguments)
{
    public static ServerRegistration ForRepository(string root, string dataDirectory, string? executableOverride)
    {
        var executable = executableOverride ?? Environment.ProcessPath
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
