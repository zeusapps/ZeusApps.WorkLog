using System.CommandLine;
using System.CommandLine.Invocation;

namespace WorkLog.Cli;

internal sealed class ApplicationVersionAction : SynchronousCommandLineAction
{
    public override int Invoke(ParseResult parseResult)
    {
        parseResult.InvocationConfiguration.Output.WriteLine(ApplicationInfo.DisplayVersion);
        return 0;
    }
}
