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
}
