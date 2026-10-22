using WorkLog.Installation;

namespace WorkLog.Launcher;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("The launcher requires Windows.");
            var root = Path.GetDirectoryName(Environment.ProcessPath!)!;
            var selected = InstallationLayout.ReadSelection(root);
            var executable = Path.Combine(InstallationLayout.PayloadDirectory(root, selected.Current.Version), "worklog.exe");
            return WindowsChildProcess.Run(executable, args);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine("worklog launcher: " + exception.Message + " See INSTALL.md for verified selection recovery.");
            return 1;
        }
    }
}
