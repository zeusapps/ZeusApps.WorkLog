using WorkLog.Cli;
using WorkLog.Updates;

namespace WorkLog;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args is ["__apply-update", "--plan", var plan, "--token", var token])
                return await UpdateHelper.ApplyAsync(plan, token);
            // Coordinate packaged servers with the updater once an installation has an update lock.
            var lockPath = Path.Combine(UpdateInstallation.CurrentDirectory, ".worklog-update.lock");
            using var installationLease = args.FirstOrDefault() is "serve" or "init" && File.Exists(lockPath)
                ? new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
            return await CliApplication.RunAsync(args);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine("worklog: " + exception.Message);
            return 1;
        }
    }
}
