using Microsoft.Data.Sqlite;
using WorkLog.Infrastructure;
using WorkLog.Journaling;

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

    public Entry[] ReadPeriod(DateTimeOffset from, DateTimeOffset to)
    {
        using var connection = database.Open();
        using var command = SqliteCommands.Create(connection,
            "SELECT payload FROM entries WHERE occurred >= $from AND occurred < $to ORDER BY occurred, id",
            parameters: [("$from", from.UtcTicks), ("$to", to.UtcTicks)]);
        using var reader = command.ExecuteReader();
        var entries = new List<Entry>();

        while (reader.Read())
        {
            entries.Add(Json.Decode<Entry>(reader.GetString(0)));
        }

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

        if (reader.GetString(0) != prepared.RequestHash)
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
