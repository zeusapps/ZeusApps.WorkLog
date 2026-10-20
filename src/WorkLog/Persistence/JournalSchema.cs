using System.Globalization;
using Microsoft.Data.Sqlite;

namespace WorkLog.Persistence;

internal static class JournalSchema
{
    public const int CurrentVersion = 2;

    private static readonly string[] Migrations =
    [
        """
        CREATE TABLE entries (
            id TEXT PRIMARY KEY, occurred INTEGER NOT NULL, repository TEXT NOT NULL,
            change_name TEXT, status TEXT NOT NULL, payload TEXT NOT NULL
        );
        CREATE TABLE retries (
            key TEXT PRIMARY KEY, hash TEXT NOT NULL, entry_id TEXT NOT NULL REFERENCES entries(id)
        );
        PRAGMA user_version=1;
        """,
        """
        CREATE INDEX entries_period ON entries(occurred, id);
        CREATE UNIQUE INDEX first_start ON entries(repository, change_name)
            WHERE status='started' AND change_name IS NOT NULL;
        PRAGMA user_version=2;
        """
    ];

    public static void Upgrade(SqliteConnection connection, string databasePath)
    {
        var version = Convert.ToInt32(SqliteCommands.Scalar(connection, "PRAGMA user_version"), CultureInfo.InvariantCulture);
        if (version > CurrentVersion)
        {
            throw new InvalidOperationException($"Journal schema {version} is newer than supported {CurrentVersion}. Install a newer worklog; no migration was made.");
        }

        if (version == CurrentVersion)
        {
            return;
        }

        if (version > 0)
        {
            Backup(connection, databasePath, version);
        }

        using var transaction = connection.BeginTransaction();
        foreach (var migration in Migrations.Skip(version))
        {
            SqliteCommands.Execute(connection, migration, transaction);
        }
        transaction.Commit();
    }

    private static void Backup(SqliteConnection connection, string path, int version)
    {
        var backupPath = path + $".v{version}.{DateTime.UtcNow:yyyyMMddHHmmssfffffff}.bak";
        using var backup = new SqliteConnection($"Data Source={backupPath};Pooling=False");
        backup.Open();
        connection.BackupDatabase(backup);
    }
}
