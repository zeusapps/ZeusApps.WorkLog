using Microsoft.Data.Sqlite;

namespace WorkLog.Persistence;

internal static class SqliteCommands
{
    public static SqliteCommand Create(SqliteConnection connection, string sql,
        SqliteTransaction? transaction = null, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }

    public static object? Scalar(SqliteConnection connection, string sql,
        SqliteTransaction? transaction = null, params (string Name, object? Value)[] parameters)
    {
        using var command = Create(connection, sql, transaction, parameters);
        return command.ExecuteScalar();
    }

    public static void Execute(SqliteConnection connection, string sql,
        SqliteTransaction? transaction = null, params (string Name, object? Value)[] parameters)
    {
        using var command = Create(connection, sql, transaction, parameters);
        command.ExecuteNonQuery();
    }
}
