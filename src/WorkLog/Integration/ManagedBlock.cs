namespace WorkLog.Integration;

internal static class ManagedBlock
{
    public const string InstructionsBegin = "<!-- worklog:begin -->";
    public const string InstructionsEnd = "<!-- worklog:end -->";
    public const string ServerBegin = "# worklog:server:begin";
    public const string ServerEnd = "# worklog:server:end";

    public static string Replace(string text, string begin, string end, string body)
    {
        var span = Find(text, begin, end);
        var block = begin + "\n" + body.TrimEnd() + "\n" + end;

        if (span is null)
        {
            return text.TrimEnd() + (text.Length == 0 ? "" : "\n\n") + block + "\n";
        }

        return text[..span.Value.Start] + block + text[span.Value.End..];
    }

    public static string Remove(string text, string begin, string end)
    {
        var span = Find(text, begin, end);
        return span is null ? text : text[..span.Value.Start] + text[span.Value.End..];
    }

    private static (int Start, int End)? Find(string text, string begin, string end)
    {
        var start = text.IndexOf(begin, StringComparison.Ordinal);
        var finish = text.IndexOf(end, StringComparison.Ordinal);
        if (start < 0 && finish < 0) return null;
        if (start < 0 || finish < start
            || text.IndexOf(begin, start + begin.Length, StringComparison.Ordinal) >= 0
            || text.IndexOf(end, finish + end.Length, StringComparison.Ordinal) >= 0)
        {
            throw new ArgumentException("Malformed or duplicate managed worklog block; preserve the file and resolve markers first.");
        }

        return (start, finish + end.Length);
    }
}
