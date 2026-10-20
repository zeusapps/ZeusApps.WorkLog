using System.Reflection;

namespace WorkLog.Cli;

public static class ApplicationInfo
{
    public static string Version => typeof(ApplicationInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
        .InformationalVersion.Split('+')[0];
}
