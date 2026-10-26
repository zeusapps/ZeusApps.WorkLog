using Microsoft.Data.Sqlite;
using WorkLog.Infrastructure;
using WorkLog.Journaling;
using WorkLog.Persistence;

namespace WorkLog.Dashboard;

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
