using WorkLog.Journaling;

namespace WorkLog.Reporting;

internal static class WorkIdentity
{
    public static string For(Entry entry, string groupBy)
    {
        if (entry.Change is not null && (groupBy == "change" || entry.Tickets.Length == 0))
        {
            return $"{entry.Repository}/change/{entry.Change}";
        }

        var keys = entry.Tickets.Select(ticket => $"{ticket.Provider.Trim()}:{ticket.Scope.Trim()}:{ticket.Id.Trim()}")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return keys.Length == 1
            ? $"{entry.Repository}/ticket/{keys[0]}"
            : $"{entry.Repository}/tickets/{string.Join("|", keys.Select(Uri.EscapeDataString))}";
    }
}
