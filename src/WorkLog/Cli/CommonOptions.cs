using System.CommandLine;

namespace WorkLog.Cli;

internal static class CommonOptions
{
    public static Option<string?> Repository() => new("--repository")
    {
        Description = "Git repository path. Init defaults to the current repository; serve uses explicit entry identities when omitted.",
        Arity = ArgumentArity.ExactlyOne
    };

    public static Option<string?> DataDirectory() => new("--data-dir")
    {
        Description = "Local journal directory. Defaults to WORKLOG_DATA_DIR or the per-user application data directory.",
        Arity = ArgumentArity.ExactlyOne
    };
}
