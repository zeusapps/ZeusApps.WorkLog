using WorkLog.Cli;

namespace WorkLog;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await CliApplication.RunAsync(args);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine("worklog: " + exception.Message);
            return 1;
        }
    }
}
