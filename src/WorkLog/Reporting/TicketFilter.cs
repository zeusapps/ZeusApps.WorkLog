using WorkLog.Journaling;

namespace WorkLog.Reporting;

/// <summary>
/// In-memory form of the <c>query_work</c> ticket filter. It mirrors the SQL in <c>SqliteEntryStore.ReadPage</c>:
/// a bare ID matches an unresolved extracted ID or a stored ticket ID, and a <c>provider:scope:id</c> value matches
/// only a stored ticket with that exact provider, scope, and ID. Stored ticket parts are compared after SQLite's
/// <c>trim()</c>, which removes spaces only; the filter itself is trimmed like the tool request.
/// </summary>
internal static class TicketFilter
{
    public static bool Matches(Entry entry, string filter)
    {
        var wanted = filter.Trim();
        var bare = !wanted.Contains(':');
        if (bare && entry.ExtractedTicketId == wanted)
            return true;
        foreach (var ticket in entry.Tickets)
        {
            var id = ticket.Id.Trim(' ');
            if (bare ? id == wanted : $"{ticket.Provider.Trim(' ')}:{ticket.Scope.Trim(' ')}:{id}" == wanted)
                return true;
        }
        return false;
    }
}
