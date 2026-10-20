using Microsoft.Data.Sqlite;
using WorkLog.Infrastructure;

namespace WorkLog.Persistence;

internal sealed class JournalDatabase
{
    private readonly string path;

    public JournalDatabase(string dataDirectory)
    {
        path = Path.Combine(dataDirectory, "journal.db");
        Initialize();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
            DefaultTimeout = 15
        }.ToString());
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var mutex = new Mutex(false, "worklog-" + ContentHash.Compute(Path.GetFullPath(path).ToUpperInvariant()));
        try
        {
            mutex.WaitOne();
        }
        catch (AbandonedMutexException)
        {
            // The abandoned lock is now owned by this process; schema recovery remains transactional.
        }

        try
        {
            using var connection = Open();
            JournalSchema.Upgrade(connection, path);
            SqliteCommands.Execute(connection, "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=15000;");
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}
