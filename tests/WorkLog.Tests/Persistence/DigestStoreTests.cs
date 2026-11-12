using Microsoft.Data.Sqlite;
using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Persistence;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Persistence;

public sealed class DigestStoreTests
{
    private static StoredDigest Digest(string item, string day = "2026-10-20", string text = "Did the thing.", params string[] covers) =>
        new(item, day, covers.Length == 0 ? ["a", "b"] : covers, text, DateTimeOffset.UtcNow);

    private static int UserVersion(string database)
    {
        using var connection = new SqliteConnection($"Data Source={database};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    [Fact]
    public void FirstSaveCreatesTheStoreWithVersionOneAndJournalStaysAtTwo()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        journal.Log(new("started", "c", OccurredAt: "2026-10-20T09:00:00Z"));
        var store = new DigestStore(box.Data);
        Assert.False(File.Exists(Path.Combine(box.Data, "digests.db")));
        store.Save(Digest("item"));
        Assert.Equal(1, UserVersion(Path.Combine(box.Data, "digests.db")));
        Assert.Equal(2, UserVersion(Path.Combine(box.Data, "journal.db")));
        var stored = Assert.Single(store.Read(["item"]));
        Assert.Equal(["a", "b"], stored.Covers);
        Assert.Equal("Did the thing.", stored.Text);
    }

    [Fact]
    public void SecondSaveForTheSameItemAndDayReplacesTheFirst()
    {
        using var box = new TestWorkspace();
        var store = new DigestStore(box.Data);
        store.Save(Digest("item", text: "first"));
        store.Save(Digest("item", text: "second", covers: ["a", "b", "c"]));
        store.Save(Digest("item", day: "2026-10-21", text: "other day"));
        var stored = store.Read(["item"]);
        Assert.Equal(2, stored.Length);
        var replaced = Assert.Single(stored, d => d.Day == "2026-10-20");
        Assert.Equal("second", replaced.Text);
        Assert.Equal(["a", "b", "c"], replaced.Covers);
    }

    [Fact]
    public void ReadReturnsOnlyRequestedItems()
    {
        using var box = new TestWorkspace();
        var store = new DigestStore(box.Data);
        store.Save(Digest("one"));
        store.Save(Digest("two"));
        Assert.Equal("one", Assert.Single(store.Read(["one", "unknown"])).Item);
        Assert.Empty(store.Read([]));
    }

    [Fact]
    public async Task TwoConcurrentWritersLoseNothing()
    {
        using var box = new TestWorkspace();
        var first = new DigestStore(box.Data);
        var second = new DigestStore(box.Data);
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            var store = i % 2 == 0 ? first : second;
            store.Save(Digest("item" + i));
            store.Save(Digest("shared", text: "writer " + i));
        })).ToArray();
        await Task.WhenAll(tasks);
        var all = first.Read(Enumerable.Range(0, 20).Select(i => "item" + i).Append("shared").ToArray());
        Assert.Equal(21, all.Length);
        Assert.Single(all, d => d.Item == "shared");
    }

    [Fact]
    public void MissingStoreReadsAsEmptyWithoutCreatingIt()
    {
        using var box = new TestWorkspace();
        Assert.Empty(new DigestStore(box.Data).Read(["item"]));
        Assert.False(File.Exists(Path.Combine(box.Data, "digests.db")));
    }

    [Fact]
    public void NewerStoreVersionIsUnavailableAndStaysSoForTheProcess()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(box.Data);
        var path = Path.Combine(box.Data, "digests.db");
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE digests(x); PRAGMA user_version=2;";
            command.ExecuteNonQuery();
        }
        var store = new DigestStore(box.Data);
        var failure = Assert.Throws<DigestStoreUnavailableException>(() => store.EnsureAvailable());
        Assert.Contains("newer than supported", failure.Message);
        Assert.Throws<DigestStoreUnavailableException>(() => store.Save(Digest("item")));
        Assert.Throws<DigestStoreUnavailableException>(() => store.Read(["item"]));
        Assert.Equal(2, UserVersion(path));
    }
    [Fact]
    public void RevisionMovesAfterASaveAndIsStableWithoutOneOrWhenUnreadable()
    {
        using var box = new TestWorkspace();
        var store = new DigestStore(box.Data);
        Assert.Equal(0, store.Revision());
        Assert.Equal(0, store.Revision());
        store.Save(Digest("item") with { Saved = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero) });
        var first = store.Revision();
        Assert.True(first > 0);
        Assert.Equal(first, store.Revision());
        store.Save(Digest("item", text: "Changed.") with { Saved = new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero) });
        var second = store.Revision();
        Assert.True(second > first);
        // An unreadable store keeps the last value instead of throwing or flipping.
        File.WriteAllText(Path.Combine(box.Data, "digests.db"), "not a database");
        File.Delete(Path.Combine(box.Data, "digests.db-wal"));
        File.Delete(Path.Combine(box.Data, "digests.db-shm"));
        Assert.Equal(second, store.Revision());
    }

    [Fact]
    public void LockWaitIsBoundedAndTimeoutDoesNotDisableTheStore()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(box.Data);
        var path = Path.Combine(box.Data, "digests.db");
        var name = "worklog-digests-" + WorkLog.Infrastructure.ContentHash.Compute(Path.GetFullPath(path).ToUpperInvariant());
        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = new Thread(() =>
        {
            using var mutex = new Mutex(false, name);
            mutex.WaitOne();
            held.Set();
            release.Wait();
            mutex.ReleaseMutex();
        });
        holder.Start();
        Assert.True(held.Wait(TimeSpan.FromSeconds(10)));
        var store = new DigestStore(box.Data, TimeSpan.FromMilliseconds(200));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Throws<DigestStoreUnavailableException>(() => store.EnsureAvailable());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
        release.Set();
        holder.Join();
        store.EnsureAvailable();
        store.Save(Digest("item"));
        Assert.Single(store.Read(["item"]));
    }

    // The exact statements a v0.4.1 process runs: its own schema, its own upsert, its own read.
    private const string V041Schema = "CREATE TABLE digests (item TEXT NOT NULL, day TEXT NOT NULL, covers TEXT NOT NULL, text TEXT NOT NULL, saved INTEGER NOT NULL, PRIMARY KEY (item, day)); PRAGMA user_version=1;";
    private const string V041Upsert = "INSERT INTO digests(item,day,covers,text,saved) VALUES($item,$day,$covers,$text,$saved) ON CONFLICT(item,day) DO UPDATE SET covers=excluded.covers, text=excluded.text, saved=excluded.saved";

    private static void Raw(string database, string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = new SqliteConnection($"Data Source={database};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }

    private static T Scalar<T>(string database, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={database};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T));
    }

    private static void V041Save(string database, string item, string day, string covers, string text) =>
        Raw(database, V041Upsert, ("$item", item), ("$day", day), ("$covers", covers), ("$text", text), ("$saved", DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks));

    private static StoredDigest Structured(string item, string[]? covers = null) =>
        new(item, "2026-10-20", covers ?? ["a", "b"], "Done: one; two. Next: three. Blockers: four.", DateTimeOffset.UtcNow,
            new DigestParts(["one", "two"], ["three"], ["four"]));

    [Fact]
    public void ExistingVersionOneStoreWithoutPartsOpensAndLegacyRowsReadWithoutParts()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(box.Data);
        var path = Path.Combine(box.Data, "digests.db");
        Raw(path, V041Schema);
        V041Save(path, "item", "2026-10-20", "[\"a\",\"b\"]", "Old text.");
        // A read-only reader (the dashboard) must cope with the missing parts table before any writer upgrades the file.
        var reader = new DigestStore(box.Data);
        var legacy = Assert.Single(reader.Read(["item"]));
        Assert.Equal("Old text.", legacy.Text);
        Assert.Null(legacy.Parts);
        var store = new DigestStore(box.Data);
        store.EnsureAvailable();
        Assert.Equal(1, UserVersion(path));
        store.Save(Structured("other"));
        Assert.Equal(1, UserVersion(path));
        Assert.Equal(1, Scalar<int>(path, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='digest_parts'"));
        Assert.Null(Assert.Single(store.Read(["item"])).Parts);
        Assert.NotNull(Assert.Single(store.Read(["other"])).Parts);
    }

    [Fact]
    public void StructuredSaveWritesBothRowsAndATextOnlyReaderSeesTheDerivedText()
    {
        using var box = new TestWorkspace();
        var store = new DigestStore(box.Data);
        store.Save(Structured("item"));
        var path = Path.Combine(box.Data, "digests.db");
        Assert.Equal(1, UserVersion(path));
        Assert.Equal(1, Scalar<int>(path, "SELECT COUNT(*) FROM digest_parts"));
        // What a v0.4.1 reader selects: a complete, readable text.
        Assert.Equal("Done: one; two. Next: three. Blockers: four.", Scalar<string>(path, "SELECT text FROM digests WHERE item='item'"));
        var stored = Assert.Single(store.Read(["item"]));
        Assert.Equal(["one", "two"], stored.Parts!.Done);
        Assert.Equal(["three"], stored.Parts.Next);
        Assert.Equal(["four"], stored.Parts.Blockers);
        Assert.Equal(stored.Text, Scalar<string>(path, "SELECT text FROM digests WHERE item='item'"));
    }

    [Fact]
    public void AnOlderProcessUpsertMakesTheStalePartsIgnoredAndItsTextIsShown()
    {
        using var box = new TestWorkspace();
        var store = new DigestStore(box.Data);
        store.Save(Structured("item"));
        var path = Path.Combine(box.Data, "digests.db");
        V041Save(path, "item", "2026-10-20", "[\"a\",\"b\",\"c\"]", "Newer single text.");
        var stored = Assert.Single(store.Read(["item"]));
        Assert.Equal("Newer single text.", stored.Text);
        Assert.Equal(["a", "b", "c"], stored.Covers);
        Assert.Null(stored.Parts);
        // The leftover row does not come back to life, and the upgraded process still saves normally.
        store.Save(Structured("item", ["a", "b", "c"]));
        Assert.Equal(["one", "two"], Assert.Single(store.Read(["item"])).Parts!.Done);
        Assert.Equal(1, Scalar<int>(path, "SELECT COUNT(*) FROM digest_parts"));
    }

    [Fact]
    public void AnOlderProcessUpsertWithTheSameCoversAndDifferentTextAlsoSupersedesTheParts()
    {
        using var box = new TestWorkspace();
        var store = new DigestStore(box.Data);
        store.Save(Structured("item"));
        V041Save(Path.Combine(box.Data, "digests.db"), "item", "2026-10-20", "[\"a\",\"b\"]", "Rewritten by the older process.");
        var stored = Assert.Single(store.Read(["item"]));
        Assert.Equal("Rewritten by the older process.", stored.Text);
        Assert.Null(stored.Parts);
    }

    [Fact]
    public void AnOlderProcessUpsertWithTheSameCoversAndTheFlattenedTextStillSupersedesTheParts()
    {
        // Staleness rests on the save stamp, not on the derived text format.
        using var box = new TestWorkspace();
        var store = new DigestStore(box.Data);
        var structured = Structured("item");
        store.Save(structured);
        V041Save(Path.Combine(box.Data, "digests.db"), "item", "2026-10-20", "[\"a\",\"b\"]", structured.Text);
        var stored = Assert.Single(store.Read(["item"]));
        Assert.Equal(structured.Text, stored.Text);
        Assert.Null(stored.Parts);
    }

    [Fact]
    public void APartsTableWithoutTheSaveStampIsRebuiltEmptyAndTheDigestStillReads()
    {
        using var box = new TestWorkspace();
        Directory.CreateDirectory(box.Data);
        var path = Path.Combine(box.Data, "digests.db");
        Raw(path, V041Schema + " CREATE TABLE digest_parts (item TEXT NOT NULL, day TEXT NOT NULL, covers TEXT NOT NULL, done TEXT NOT NULL, next TEXT NOT NULL, blockers TEXT NOT NULL, PRIMARY KEY (item, day));");
        V041Save(path, "item", "2026-10-20", "[\"a\",\"b\"]", "Plain.");
        Assert.Null(Assert.Single(new DigestStore(box.Data).Read(["item"])).Parts);
        var store = new DigestStore(box.Data);
        store.EnsureAvailable();
        store.Save(Structured("other"));
        Assert.NotNull(Assert.Single(store.Read(["other"])).Parts);
        Assert.Null(Assert.Single(store.Read(["item"])).Parts);
    }

    [Fact]
    public void RepeatedIdenticalStructuredSavesLeaveOneDigestAndOnePartsRow()
    {
        using var box = new TestWorkspace();
        var store = new DigestStore(box.Data);
        for (var i = 0; i < 3; i++)
            store.Save(Structured("item"));
        Assert.Single(store.Read(["item"]));
        var path = Path.Combine(box.Data, "digests.db");
        Assert.Equal(1, Scalar<int>(path, "SELECT COUNT(*) FROM digests"));
        Assert.Equal(1, Scalar<int>(path, "SELECT COUNT(*) FROM digest_parts"));
    }

    [Fact]
    public void ATextOnlySaveRemovesEarlierPartsForTheSameDay()
    {
        using var box = new TestWorkspace();
        var store = new DigestStore(box.Data);
        store.Save(Structured("item"));
        store.Save(Digest("item", text: "Plain."));
        var stored = Assert.Single(store.Read(["item"]));
        Assert.Equal("Plain.", stored.Text);
        Assert.Null(stored.Parts);
        Assert.Equal(0, Scalar<int>(Path.Combine(box.Data, "digests.db"), "SELECT COUNT(*) FROM digest_parts"));
    }
}
