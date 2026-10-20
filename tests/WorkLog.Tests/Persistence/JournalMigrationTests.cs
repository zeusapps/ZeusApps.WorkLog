using Microsoft.Data.Sqlite;
using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Reporting;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Persistence;

public sealed class JournalMigrationTests
{
    private static QueryRequest Period(string? status = null) => new("2026-10-20T00:00:00+02:00", "2026-10-25T00:00:00+02:00", "Europe/Warsaw", Status: status);

    [Fact]
    public void MigrationBacksUpPreservesDataAndRejectsFutureSchemas()
    {
        using var box = new TestWorkspace();
        var context = new LocalSettings(box.Data);
        var journal = new Journal(context, new("repo"));
        var entry = journal.Log(new("started", "a", OccurredAt: "2026-10-20T09:00:00Z"));
        var path = Path.Combine(box.Data, "journal.db");
        Execute(path, "DROP INDEX entries_period; DROP INDEX first_start; PRAGMA user_version=1;");
        var upgraded = new Journal(context);
        Assert.Equal(entry.Entry.Id, Assert.Single(upgraded.Query(Period()).Entries).Id);
        Assert.Single(Directory.GetFiles(box.Data, "*.bak"));
        Execute(path, "PRAGMA wal_checkpoint(TRUNCATE); PRAGMA journal_mode=DELETE; PRAGMA user_version=99;");
        var bytes = File.ReadAllBytes(path);
        Assert.Throws<InvalidOperationException>(() => new Journal(context));
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void FailedMigrationRollsBackAtomically()
    {
        using var box = new TestWorkspace();
        var context = new LocalSettings(box.Data);
        _ = new Journal(context);
        var path = Path.Combine(box.Data, "journal.db");
        Execute(path, "DROP INDEX entries_period; PRAGMA user_version=1;"); // Existing first_start makes migration 2 fail.
        Assert.Throws<SqliteException>(() => new Journal(context));
        using var db = new SqliteConnection($"Data Source={path};Pooling=False");
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        Assert.Equal(1L, command.ExecuteScalar());
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name='entries_period'";
        Assert.Equal(0L, command.ExecuteScalar());
    }

    internal static void Execute(string path, string sql)
    {
        using var db = new SqliteConnection($"Data Source={path};Pooling=False");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
