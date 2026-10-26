using System.Text.RegularExpressions;

namespace WorkLog.Dashboard;

internal sealed record ReviewMarker(int Line, int Start, int End, string Raw, string? Id, string Quote, string Comment)
{
    public static string Create(string comment, string quote, string newline, string? id = null)
    {
        var excerpt = Regex.Replace(quote, @"\s+", " ").Trim();
        if (excerpt.Length > 280)
        {
            var length = char.IsHighSurrogate(excerpt[279]) ? 279 : 280;
            excerpt = excerpt[..length] + "…";
        }
        static string Escape(string value) => value.Replace("--", "-&#45;", StringComparison.Ordinal);
        var body = Regex.Replace(comment.Trim(), @"\r\n|\r|\n", newline);
        return "<!-- REVIEW: id=" + (id ?? Guid.NewGuid().ToString("N")) + newline
            + "Target: preceding passage" + newline + "Quote: " + Escape(excerpt) + newline
            + "Comment:" + newline + Escape(body) + newline + "-->";
    }

    public static ReviewMarker[] Parse(string text)
    {
        var lines = Regex.Split(text, @"\r\n|\r|\n");
        var breaks = Regex.Matches(text, @"\r\n|\r|\n");
        var offsets = new int[lines.Length];
        for (var i = 1; i < offsets.Length; i++) offsets[i] = breaks[i - 1].Index + breaks[i - 1].Length;
        var result = new List<ReviewMarker>();
        char fence = '\0';
        var fenceLength = 0;
        for (var line = 0; line < lines.Length; line++)
        {
            var match = Regex.Match(lines[line], @"^ {0,3}(`{3,}|~{3,})(.*)$");
            if (fence != '\0')
            {
                if (match.Success && match.Groups[1].Value[0] == fence && match.Groups[1].Length >= fenceLength
                    && string.IsNullOrWhiteSpace(match.Groups[2].Value)) fence = '\0';
                continue;
            }
            if (match.Success)
            {
                fence = match.Groups[1].Value[0];
                fenceLength = match.Groups[1].Length;
                continue;
            }
            var prefix = Regex.Match(lines[line], @"^ {0,3}<!-- REVIEW(?::|(?=\s))");
            if (!prefix.Success) continue;
            var start = offsets[line] + lines[line].IndexOf("<!--", StringComparison.Ordinal);
            var close = text.IndexOf("-->", offsets[line] + prefix.Length, StringComparison.Ordinal);
            if (close < 0) continue;
            var endLine = line;
            while (endLine + 1 < offsets.Length && offsets[endLine + 1] <= close + 2) endLine++;
            if (!string.IsNullOrWhiteSpace(text[(close + 3)..(offsets[endLine] + lines[endLine].Length)])) continue;
            var raw = text[start..(close + 3)];
            var content = Regex.Replace(text[(offsets[line] + prefix.Length)..close], @"\r\n|\r", "\n").Trim();
            var structured = Regex.Match(content, @"\Aid=([a-f0-9]{32})\nTarget: preceding passage\nQuote: ([^\n]*)\nComment:\n([\s\S]*)\z");
            static string Decode(string value) => value.Replace("-&#45;", "--", StringComparison.Ordinal);
            var quote = structured.Success ? Decode(structured.Groups[2].Value) : PreviousPassage(lines, line, result);
            result.Add(new(line, start, close + 3, raw,
                structured.Success ? structured.Groups[1].Value : null, quote,
                Decode(structured.Success ? structured.Groups[3].Value.Trim() : content)));
            line = endLine;
        }
        return result.ToArray();
    }

    private static string PreviousPassage(string[] lines, int line, List<ReviewMarker> earlier)
    {
        var end = line - 1;
        while (end >= 0 && string.IsNullOrWhiteSpace(lines[end])) end--;
        if (end < 0) return "";
        if (lines[end].TrimEnd().EndsWith("-->", StringComparison.Ordinal) && earlier.Count > 0) return earlier[^1].Quote;
        var start = end;
        while (start > 0 && !string.IsNullOrWhiteSpace(lines[start - 1])) start--;
        return string.Join(' ', lines[start..(end + 1)]);
    }
}
