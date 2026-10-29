using Microsoft.Data.Sqlite;
using WorkLog.Infrastructure;
using WorkLog.Journaling;
using WorkLog.Persistence;

namespace WorkLog.Dashboard;

internal sealed record ChangeActivity(string Repository, string Change, DateTimeOffset FirstAt, DateTimeOffset LastAt, string LastStatus);

internal sealed class DashboardJournal(string dataDirectory)
{
    private readonly string path = Path.Combine(dataDirectory, "journal.db");

    private SqliteConnection? Open()
    {
        if (!File.Exists(path)) return null;
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false, DefaultTimeout = 5
        }.ToString());
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version";
            var version = Convert.ToInt32(command.ExecuteScalar());
            if (version != JournalSchema.CurrentVersion)
                throw new InvalidOperationException($"Journal schema {version} is not supported by this dashboard. Use a compatible WorkLog release to open or upgrade the journal. No migration was made.");
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    public string[] Repositories()
    {
        using var connection = Open();
        if (connection is null) return [];
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT repository FROM entries ORDER BY repository";
        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result.ToArray();
    }

    /// <summary>Highest journal row id, or 0 when no journal exists. Rows are append-only, so a change means new observations.</summary>
    public long Snapshot()
    {
        using var connection = Open();
        if (connection is null) return 0;
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(rowid),0) FROM entries";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    /// <summary>
    /// The first and latest observation of every recorded change name, with the latest status (ties on the occurrence time go to the highest id).
    /// One read-only pass over the journal; a missing journal is empty.
    /// </summary>
    public ChangeActivity[] Activity(string[] repositories, CancellationToken cancellationToken = default)
    {
        using var connection = Open();
        if (connection is null) return [];
        using var command = connection.CreateCommand();
        var repoClause = "";
        var distinct = repositories.Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length > 0)
        {
            var names = distinct.Select((repo, i) =>
            {
                command.Parameters.AddWithValue("$r" + i, repo);
                return "$r" + i;
            });
            repoClause = " AND repository IN (" + string.Join(',', names) + ")";
        }
        command.CommandText = "SELECT repository, change_name, first_at, occurred, status FROM ("
            + "SELECT repository, change_name, status, occurred, MIN(occurred) OVER (PARTITION BY repository, change_name) AS first_at, "
            + "ROW_NUMBER() OVER (PARTITION BY repository, change_name ORDER BY occurred DESC, id DESC) AS position "
            + "FROM entries WHERE change_name IS NOT NULL" + repoClause + ") WHERE position = 1 ORDER BY repository, change_name";
        using var registration = cancellationToken.Register(command.Cancel);
        using var reader = command.ExecuteReader();
        var result = new List<ChangeActivity>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(new(reader.GetString(0), reader.GetString(1), new DateTimeOffset(reader.GetInt64(2), TimeSpan.Zero),
                new DateTimeOffset(reader.GetInt64(3), TimeSpan.Zero), reader.GetString(4)));
        }
        return result.ToArray();
    }

    private static string RepositoryClause(SqliteCommand command, string[] repositories)
    {
        var distinct = repositories.Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length == 0) return "";
        var names = distinct.Select((repo, i) =>
        {
            command.Parameters.AddWithValue("$r" + i, repo);
            return "$r" + i;
        });
        return " AND repository IN (" + string.Join(',', names) + ")";
    }

    private DateTimeOffset? Occurred(string aggregate, string window, string[] repositories, long? snapshot, params (string Name, DateTimeOffset Value)[] bounds)
    {
        using var connection = Open();
        if (connection is null) return null;
        using var command = connection.CreateCommand();
        foreach (var (name, value) in bounds) command.Parameters.AddWithValue(name, value.UtcTicks);
        var snapshotClause = "";
        if (snapshot is not null)
        {
            snapshotClause = " AND rowid <= $snapshot";
            command.Parameters.AddWithValue("$snapshot", snapshot.Value);
        }
        command.CommandText = $"SELECT {aggregate}(occurred) FROM entries WHERE {window}{snapshotClause}{RepositoryClause(command, repositories)}";
        return command.ExecuteScalar() is long ticks ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;
    }

    /// <summary>The latest observation strictly before an instant, or null. Uses the (occurred, id) index.</summary>
    public DateTimeOffset? LatestBefore(DateTimeOffset before, string[] repositories, long? snapshot = null) =>
        Occurred("MAX", "occurred < $before", repositories, snapshot, ("$before", before));

    /// <summary>The earliest observation in [from, to), or null. Uses the (occurred, id) index.</summary>
    public DateTimeOffset? EarliestBetween(DateTimeOffset from, DateTimeOffset to, string[] repositories, long? snapshot = null) =>
        Occurred("MIN", "occurred >= $from AND occurred < $to", repositories, snapshot, ("$from", from), ("$to", to));

    public (long Snapshot, Entry[] Entries) Read(DateTimeOffset to, string[] repositories, long? snapshot, CancellationToken cancellationToken = default)
    {
        using var connection = Open();
        if (connection is null) return (0, []);
        using var maximum = connection.CreateCommand();
        maximum.CommandText = "SELECT COALESCE(MAX(rowid),0) FROM entries";
        var current = Convert.ToInt64(maximum.ExecuteScalar());
        var bound = snapshot ?? current;
        if (bound < 0 || bound > current) throw new ArgumentException("Invalid journal snapshot. Refresh the dashboard.");
        using var command = connection.CreateCommand();
        var repoClause = "";
        if (repositories.Length > 0)
        {
            var names = repositories.Distinct(StringComparer.Ordinal).Select((repo, i) =>
            {
                var name = "$r" + i;
                command.Parameters.AddWithValue(name, repo);
                return name;
            });
            repoClause = " AND repository IN (" + string.Join(',', names) + ")";
        }
        command.CommandText = "SELECT payload FROM entries WHERE rowid <= $snapshot AND occurred < $to" + repoClause + " ORDER BY occurred,id";
        command.Parameters.AddWithValue("$snapshot", bound);
        command.Parameters.AddWithValue("$to", to.UtcTicks);
        using var registration = cancellationToken.Register(command.Cancel);
        using var reader = command.ExecuteReader();
        var entries = new List<Entry>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(Json.Decode<Entry>(reader.GetString(0)));
        }
        return (bound, entries.ToArray());
    }
}
