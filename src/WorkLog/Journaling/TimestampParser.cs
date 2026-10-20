using System.Globalization;
using System.Text.RegularExpressions;

namespace WorkLog.Journaling;

internal static class TimestampParser
{
    public static DateTimeOffset Parse(string input)
    {
        if (!Regex.IsMatch(input, @"T.*(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)
            || !DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            throw new ArgumentException("Timestamp must be ISO 8601 with Z or an explicit offset.");
        }

        return result;
    }
}
