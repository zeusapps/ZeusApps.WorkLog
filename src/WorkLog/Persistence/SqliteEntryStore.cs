using Microsoft.Data.Sqlite;
using WorkLog.Infrastructure;
using WorkLog.Journaling;
using WorkLog.Reporting;

namespace WorkLog.Persistence;

internal sealed class SqliteEntryStore(JournalDatabase database)
{
    public LogResult Append(PreparedEntry prepared)
    {
        using var connection = database.Open();
        SqliteCommands.Execute(connection, "PRAGMA foreign_keys=ON;");
        using var transaction = connection.BeginTransaction();

        var replay = FindRetry(connection, transaction, prepared);
        if (replay is not null)
        {
            return new LogResult(false, replay);
        }

        var existing = FindInitialStart(connection, transaction, prepared.Entry);
        var entry = existing ?? prepared.Entry;
        if (existing is null)
        {
            Insert(connection, transaction, entry);
        }

        if (prepared.IdempotencyKey is not null)
        {
            SqliteCommands.Execute(connection,
                "INSERT INTO retries(key,hash,entry_id) VALUES($key,$hash,$id)", transaction,
                ("$key", prepared.IdempotencyKey), ("$hash", prepared.RequestHash), ("$id", entry.Id));
        }

        transaction.Commit();
        return new LogResult(existing is null, entry);
    }

    public (Entry[] Entries, long Snapshot) ReadPage(QueryRequest request, ReportingPeriod period, ReportCursor? cursor)
    {
        using var connection = database.Open();
        var snapshot = cursor?.Snapshot ?? Convert.ToInt64(SqliteCommands.Scalar(connection, "SELECT COALESCE(MAX(rowid),0) FROM entries"));
        var conditions = new List<string> { "e.occurred >= $from", "e.occurred < $to", "e.rowid <= $snapshot" };
        var parameters = new List<(string, object?)>
        {
            ("$from", period.From.UtcTicks), ("$to", period.To.UtcTicks), ("$snapshot", snapshot), ("$limit", request.PageSize + 1)
        };
        void Filter(string expression, string name, string? value)
        {
            if (value is null)
                return;
            conditions.Add(expression + "=" + name);
            parameters.Add((name, value));
        }
        Filter("e.repository", "$repo", request.Repository);
        Filter("e.change_name", "$change", request.Change);
        Filter("e.status", "$status", request.Status);
        Filter("json_extract(e.payload,'$.classification')", "$classification", request.Classification);
        if (request.Ticket is not null)
        {
            conditions.Add("""
                ((instr($ticket,':')=0 AND json_extract(e.payload,'$.extractedTicketId')=$ticket) OR
                EXISTS (SELECT 1 FROM json_each(e.payload,'$.tickets') t WHERE
                    (instr($ticket,':')=0 AND trim(json_extract(t.value,'$.id'))=$ticket) OR
                    trim(json_extract(t.value,'$.provider'))||':'||trim(json_extract(t.value,'$.scope'))||':'||trim(json_extract(t.value,'$.id'))=$ticket))
                """);
            parameters.Add(("$ticket", request.Ticket));
        }
        if (cursor is not null)
        {
            conditions.Add("(e.occurred > $after OR (e.occurred=$after AND e.id>$id))");
            parameters.Add(("$after", cursor.OccurredTicks));
            parameters.Add(("$id", cursor.Id));
        }
        var projection = request.IncludeSummaries ? "e.payload" : "json_set(e.payload,'$.summary',NULL)";
        using var command = SqliteCommands.Create(connection,
            $"SELECT {projection} FROM entries e WHERE {string.Join(" AND ", conditions)} ORDER BY e.occurred, e.id LIMIT $limit",
            parameters: parameters.ToArray());
        using var reader = command.ExecuteReader();
        var entries = new List<Entry>();

        while (reader.Read())
        {
            entries.Add(Json.Decode<Entry>(reader.GetString(0)));
        }

        return (entries.ToArray(), snapshot);
    }

    /// <summary>Entries with an occurrence time in [from, to), oldest first. Uses the (occurred, id) index.</summary>
    public Entry[] ReadBetween(DateTimeOffset from, DateTimeOffset to)
    {
        using var connection = database.Open();
        using var command = SqliteCommands.Create(connection,
            "SELECT payload FROM entries WHERE occurred >= $from AND occurred < $to ORDER BY occurred, id",
            parameters: [("$from", from.UtcTicks), ("$to", to.UtcTicks)]);
        using var reader = command.ExecuteReader();
        var entries = new List<Entry>();
        while (reader.Read())
            entries.Add(Json.Decode<Entry>(reader.GetString(0)));
        return entries.ToArray();
    }

    /// <summary>The stored entries among the given IDs; unknown IDs are absent.</summary>
    public Entry[] ReadByIds(IReadOnlyCollection<string> ids)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        var names = ids.Select((id, i) =>
        {
            command.Parameters.AddWithValue("$i" + i, id);
            return "$i" + i;
        });
        command.CommandText = $"SELECT payload FROM entries WHERE id IN ({string.Join(',', names)}) ORDER BY occurred, id";
        using var reader = command.ExecuteReader();
        var entries = new List<Entry>();
        while (reader.Read())
            entries.Add(Json.Decode<Entry>(reader.GetString(0)));
        return entries.ToArray();
    }

    private static Entry? FindRetry(SqliteConnection connection, SqliteTransaction transaction, PreparedEntry prepared)
    {
        if (prepared.IdempotencyKey is null)
        {
            return null;
        }

        using var command = SqliteCommands.Create(connection,
            "SELECT hash, entry_id FROM retries WHERE key=$key", transaction, ("$key", prepared.IdempotencyKey));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        if (reader.GetString(0) != prepared.RequestHash && reader.GetString(0) != prepared.LegacyRequestHash)
        {
            throw new ArgumentException("Idempotency key was already used for different input.");
        }

        var entryId = reader.GetString(1);
        reader.Close();
        var payload = (string)SqliteCommands.Scalar(connection,
            "SELECT payload FROM entries WHERE id=$id", transaction, ("$id", entryId))!;
        return Json.Decode<Entry>(payload);
    }

    private static Entry? FindInitialStart(SqliteConnection connection, SqliteTransaction transaction, Entry entry)
    {
        if (entry.Status != "started" || entry.Change is null)
        {
            return null;
        }

        var payload = SqliteCommands.Scalar(connection,
            "SELECT payload FROM entries WHERE repository=$repo AND change_name=$change AND status='started'",
            transaction, ("$repo", entry.Repository), ("$change", entry.Change)) as string;
        return payload is null ? null : Json.Decode<Entry>(payload);
    }

    private static void Insert(SqliteConnection connection, SqliteTransaction transaction, Entry entry)
    {
        SqliteCommands.Execute(connection,
            "INSERT INTO entries(id,occurred,repository,change_name,status,payload) VALUES($id,$at,$repo,$change,$status,$payload)",
            transaction, ("$id", entry.Id), ("$at", entry.OccurredAt.UtcTicks), ("$repo", entry.Repository),
            ("$change", entry.Change), ("$status", entry.Status), ("$payload", Json.Encode(entry)));
    }
}
