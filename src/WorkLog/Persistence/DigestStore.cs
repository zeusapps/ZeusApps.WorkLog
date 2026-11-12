using System.Globalization;
using Microsoft.Data.Sqlite;
using WorkLog.Infrastructure;

namespace WorkLog.Persistence;

/// <summary>The structured bullets of a digest. Every digest also keeps a readable single text, derived with <see cref="Flatten"/>.</summary>
public sealed record DigestParts(string[] Done, string[] Next, string[] Blockers)
{
    /// <summary>The single-text form older readers display, for example <c>Done: a; b. Next: c. Blockers: d.</c>; empty sections are left out.</summary>
    public string Flatten()
    {
        var sections = new List<string>();
        void Add(string label, string[] bullets)
        {
            if (bullets.Length > 0)
                sections.Add(label + ": " + string.Join("; ", bullets.Select(b => b.TrimEnd('.', ' '))) + ".");
        }
        Add("Done", Done);
        Add("Next", Next);
        Add("Blockers", Blockers);
        return string.Join(' ', sections);
    }
}

/// <summary>
/// A stored day digest: the item (work identity hash), the local day, the exact covered entry IDs, and the agent-written text.
/// <paramref name="Parts"/> carries the structured bullets; a read returns them only while their covers and save stamp still equal those of the stored digest.
/// </summary>
public sealed record StoredDigest(string Item, string Day, string[] Covers, string Text, DateTimeOffset Saved, DigestParts? Parts = null);

/// <summary>Digest storage cannot be used by this process (for example a newer store version); logging and querying are unaffected.</summary>
public sealed class DigestStoreUnavailableException(string message, Exception? inner = null) : InvalidOperationException(message, inner);

/// <summary>
/// Derived day digests in <c>digests.db</c>, separate from the journal so the journal schema and the shared-data contract stay unchanged.
/// The file is created lazily on the first write; reads of a missing store are empty.
/// </summary>
public sealed class DigestStore(string dataDirectory, TimeSpan? lockTimeout = null)
{
    public const int CurrentVersion = 1;

    /// <summary>How long initialization waits for another process's lock before digests are unavailable for that call only.</summary>
    internal static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(3);

    private static readonly string[] Migrations =
    [
        """
        CREATE TABLE digests (
            item TEXT NOT NULL, day TEXT NOT NULL, covers TEXT NOT NULL, text TEXT NOT NULL, saved INTEGER NOT NULL,
            PRIMARY KEY (item, day)
        );
        PRAGMA user_version=1;
        """
    ];

    /// <summary>Additive structured parts. Created if absent by every writer, so version 1 stays readable and writable by older processes.</summary>
    private const string PartsTable = """
        CREATE TABLE IF NOT EXISTS digest_parts (
            item TEXT NOT NULL, day TEXT NOT NULL, covers TEXT NOT NULL, saved INTEGER NOT NULL, done TEXT NOT NULL, next TEXT NOT NULL, blockers TEXT NOT NULL,
            PRIMARY KEY (item, day)
        );
        """;

    private readonly string path = Path.Combine(dataDirectory, "digests.db");
    private readonly object gate = new();
    private string? disabledReason;
    private bool initialized;
    private long lastRevision;

    /// <summary>Creates or upgrades the store when needed. Throws <see cref="DigestStoreUnavailableException"/> once a newer version was seen; the process then stays disabled.</summary>
    public void EnsureAvailable()
    {
        lock (gate)
        {
            if (disabledReason is not null)
                throw new DigestStoreUnavailableException(disabledReason);
            if (initialized)
                return;
            Directory.CreateDirectory(dataDirectory);
            using var mutex = new Mutex(false, "worklog-digests-" + ContentHash.Compute(Path.GetFullPath(path).ToUpperInvariant()));
            try
            {
                if (!mutex.WaitOne(lockTimeout ?? DefaultLockTimeout))
                    throw new DigestStoreUnavailableException("Digest storage is busy (another process is initializing it). Digests are skipped for this call; try again later.");
            }
            catch (AbandonedMutexException) { /* The lock is now owned by this process; migration is transactional. */ }
            try
            {
                using var connection = OpenReadWrite();
                var version = Version(connection);
                if (version > CurrentVersion)
                {
                    disabledReason = $"Digest store version {version} is newer than supported {CurrentVersion}. Digests are disabled in this process; install a newer worklog.";
                    throw new DigestStoreUnavailableException(disabledReason);
                }
                if (version < CurrentVersion)
                {
                    using var transaction = connection.BeginTransaction();
                    foreach (var migration in Migrations.Skip(version))
                        SqliteCommands.Execute(connection, migration, transaction);
                    transaction.Commit();
                }
                // Parts are derived data: a table without the save stamp (an unreleased shape) is rebuilt empty.
                if (SqliteCommands.Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='digest_parts'") is long existing && existing > 0 && !HasPartsStamp(connection))
                    SqliteCommands.Execute(connection, "DROP TABLE digest_parts");
                SqliteCommands.Execute(connection, PartsTable);
                SqliteCommands.Execute(connection, "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=15000;");
                initialized = true;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }

    /// <summary>Stores the digest, replacing any earlier digest for the same item and day.</summary>
    public void Save(StoredDigest digest)
    {
        EnsureAvailable();
        using var connection = OpenReadWrite();
        SqliteCommands.Execute(connection, "PRAGMA busy_timeout=15000;");
        using var transaction = connection.BeginTransaction();
        SqliteCommands.Execute(connection,
            "INSERT INTO digests(item,day,covers,text,saved) VALUES($item,$day,$covers,$text,$saved) "
            + "ON CONFLICT(item,day) DO UPDATE SET covers=excluded.covers, text=excluded.text, saved=excluded.saved",
            transaction, ("$item", digest.Item), ("$day", digest.Day), ("$covers", Json.Encode(digest.Covers)),
            ("$text", digest.Text), ("$saved", digest.Saved.UtcTicks));
        if (digest.Parts is { } parts)
            SqliteCommands.Execute(connection,
                "INSERT INTO digest_parts(item,day,covers,saved,done,next,blockers) VALUES($item,$day,$covers,$saved,$done,$next,$blockers) "
                + "ON CONFLICT(item,day) DO UPDATE SET covers=excluded.covers, saved=excluded.saved, done=excluded.done, next=excluded.next, blockers=excluded.blockers",
                transaction, ("$item", digest.Item), ("$day", digest.Day), ("$covers", Json.Encode(digest.Covers)), ("$saved", digest.Saved.UtcTicks),
                ("$done", Json.Encode(parts.Done)), ("$next", Json.Encode(parts.Next)), ("$blockers", Json.Encode(parts.Blockers)));
        else
            SqliteCommands.Execute(connection, "DELETE FROM digest_parts WHERE item=$item AND day=$day",
                transaction, ("$item", digest.Item), ("$day", digest.Day));
        transaction.Commit();
    }

    /// <summary>Digests of the given items. Read-only; a missing store is empty. Throws when the store cannot be read or is newer than supported.</summary>
    public StoredDigest[] Read(IReadOnlyCollection<string> items)
    {
        if (items.Count == 0)
            return [];
        FileAttributes attributes;
        try { attributes = File.GetAttributes(path); }
        catch (FileNotFoundException) { return []; }
        catch (DirectoryNotFoundException) { return []; }
        if ((attributes & FileAttributes.Directory) != 0)
            throw new IOException("Digest store path is a directory and cannot be read as a database file.");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString());
        connection.Open();
        var version = Version(connection);
        if (version > CurrentVersion)
            throw new DigestStoreUnavailableException($"Digest store version {version} is newer than supported {CurrentVersion}.");
        if (version == 0)
            return [];
        using var command = connection.CreateCommand();
        var names = string.Join(',', items.Distinct(StringComparer.Ordinal).Select((item, i) =>
        {
            command.Parameters.AddWithValue("$i" + i, item);
            return "$i" + i;
        }));
        // Parts count only while they carry the same covers and save stamp as the digest: an older process that rewrites the digest
        // changes both, so its text wins. A store only older processes wrote has no parts table.
        var withParts = HasPartsStamp(connection);
        command.CommandText = withParts
            ? "SELECT d.item,d.day,d.covers,d.text,d.saved,p.done,p.next,p.blockers FROM digests d "
              + "LEFT JOIN digest_parts p ON p.item=d.item AND p.day=d.day AND p.covers=d.covers AND p.saved=d.saved "
              + $"WHERE d.item IN ({names}) ORDER BY d.item,d.day"
            : $"SELECT item,day,covers,text,saved FROM digests WHERE item IN ({names}) ORDER BY item,day";
        var result = new List<StoredDigest>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(new(reader.GetString(0), reader.GetString(1), Json.Decode<string[]>(reader.GetString(2)),
                reader.GetString(3), new DateTimeOffset(reader.GetInt64(4), TimeSpan.Zero),
                withParts && !reader.IsDBNull(5)
                    ? new DigestParts(Json.Decode<string[]>(reader.GetString(5)), Json.Decode<string[]>(reader.GetString(6)), Json.Decode<string[]>(reader.GetString(7)))
                    : null));
        return result.ToArray();
    }

    private static bool HasPartsStamp(SqliteConnection connection) =>
        Convert.ToInt32(SqliteCommands.Scalar(connection, "SELECT COUNT(*) FROM pragma_table_info('digest_parts') WHERE name='saved'"), CultureInfo.InvariantCulture) > 0;

    /// <summary>
    /// A cheap change marker: the latest save time (UTC ticks), or 0 without a store. One read-only SELECT; a store that cannot be read
    /// right now keeps the last value seen, so the marker never throws and never flips on a transient failure.
    /// </summary>
    public long Revision()
    {
        try
        {
            if (!File.Exists(path))
                return lastRevision;
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = 5
            }.ToString());
            connection.Open();
            lastRevision = Convert.ToInt64(SqliteCommands.Scalar(connection, "SELECT COALESCE(MAX(saved),0) FROM digests"), CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is SqliteException or IOException or InvalidOperationException or UnauthorizedAccessException) { }
        return lastRevision;
    }

    private SqliteConnection OpenReadWrite()
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

    private static int Version(SqliteConnection connection) =>
        Convert.ToInt32(SqliteCommands.Scalar(connection, "PRAGMA user_version"), CultureInfo.InvariantCulture);
}
