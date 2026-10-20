using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using WorkLog.Journaling;
using WorkLog.Reporting;

namespace WorkLog.Mcp;

[McpServerToolType]
public sealed class JournalTools(Journal journal)
{
    [McpServerTool(Name = "log_work", UseStructuredContent = true, ReadOnly = false,
        Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Record actual session work locally: started, in_progress, or done. Archive dates do not determine completion. Reuse idempotencyKey only for retries.")]
    public LogResult LogWork([Description("Actual activity to record. Supply a repository identity when no repository was configured. Reuse an idempotency key only for identical retries.")] LogRequest entry) => Invoke(() =>
    {
        ArgumentNullException.ThrowIfNull(entry);
        return journal.Log(entry);
    });

    [McpServerTool(Name = "query_work", UseStructuredContent = true, ReadOnly = true,
        Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return recorded work for [from,to), with offset timestamps and a reporting timezone such as Europe/Warsaw. No activity is inferred for idle days.")]
    public Report QueryWork([Description("Inclusive from/exclusive to timestamps with offsets, and a reporting timezone such as Europe/Warsaw. Optional filters narrow the stored observations.")] QueryRequest period) => Invoke(() =>
    {
        ArgumentNullException.ThrowIfNull(period);
        return journal.Query(period);
    });

    private static T Invoke<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new McpException(exception.Message);
        }
    }
}
