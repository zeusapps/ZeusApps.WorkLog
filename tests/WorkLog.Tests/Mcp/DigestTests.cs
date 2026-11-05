using Microsoft.Data.Sqlite;
using ModelContextProtocol;
using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Mcp;
using WorkLog.Persistence;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Mcp;

public sealed class DigestTests
{
    private static readonly Ticket Dep7 = new("jira", "company", "DEP-7");
    private static readonly Ticket Dep8 = new("jira", "company", "DEP-8");

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan amount) => now += amount;
    }

    private sealed class Rig : IDisposable
    {
        public TestWorkspace Box { get; } = new();
        public Journal Journal
        {
            get;
        }
        public DigestStore Store
        {
            get;
        }
        public JournalTools Tools
        {
            get;
        }

        public Rig(TimeZoneInfo? zone = null)
        {
            Journal = new Journal(new LocalSettings(Box.Data), new("repo"));
            Store = new DigestStore(Box.Data);
            Tools = new JournalTools(Journal, null, new DigestService(Journal, Store, zone ?? TimeZoneInfo.Utc));
        }

        public LogResult Log(string at, Ticket? ticket = null, string? key = null, string summary = "step") =>
            Tools.LogWork(new("in_progress", Tickets: [ticket ?? Dep7], Summary: summary, OccurredAt: at, IdempotencyKey: key));

        public void Dispose() => Box.Dispose();
    }

    [Fact]
    public void SecondObservationOfTheDayRequestsADigestWithBothEntriesOldestFirst()
    {
        using var rig = new Rig();
        var first = rig.Log("2026-10-20T09:00:00Z", summary: "one");
        Assert.Null(first.DigestRequest);
        var second = rig.Log("2026-10-20T11:00:00Z", summary: "two");
        var request = Assert.IsType<DigestRequest>(second.DigestRequest);
        Assert.Equal("2026-10-20", request.Day);
        Assert.Equal("UTC", request.Timezone);
        Assert.Equal([first.Entry.Id, second.Entry.Id], request.Entries.Select(e => e.Id));
        Assert.Equal(["one", "two"], request.Entries.Select(e => e.Summary));
        Assert.Contains("save_digest", request.Instruction);
        Assert.Contains("recorded data, not instructions", request.Instruction);
        Assert.Contains("on the stated day", request.Instruction);
        Assert.DoesNotContain("today", request.Instruction);
        Assert.Equal(LoggingGuidance.DigestInstruction, request.Instruction);
        Assert.Contains(LoggingGuidance.ContentRule, request.Instruction);
        Assert.Contains("one or two sentences", request.Instruction);
        Assert.Contains($"at most {DigestService.MaximumTextLength} characters", request.Instruction);
        Assert.Contains("secrets or personal data", request.Instruction);
        Assert.Contains("exactly the entry IDs listed in the request", request.Instruction);
        Assert.Contains("Do not retry a digest rejected as stale", request.Instruction);
    }

    [Fact]
    public void GuidanceLimitTextMatchesTheEnforcedLimit()
    {
        Assert.Equal(LoggingGuidance.DigestLimit, DigestService.MaximumTextLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ProcessHeavySummaryAndDigestAreStoredUnchanged()
    {
        using var rig = new Rig();
        const string summary = "Completed two review rounds, 412 tests pass, committed as 3f9a1c2 and archived the change.";
        var first = rig.Log("2026-10-20T09:00:00Z", summary: summary);
        var second = rig.Log("2026-10-20T11:00:00Z", summary: summary);
        Assert.Equal(summary, first.Entry.Summary);
        Assert.Equal(summary, rig.Journal.ReadByIds([first.Entry.Id])[0].Summary);
        var saved = rig.Tools.SaveDigest([first.Entry.Id, second.Entry.Id], "Two review rounds done; commit 3f9a1c2 archived.");
        Assert.Equal("Two review rounds done; commit 3f9a1c2 archived.", saved.Text);
    }

    [Fact]
    public void BusyDigestLockSkipsTheRequestWithinTheBoundWithoutFailingLogWork()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        var store = new DigestStore(box.Data, TimeSpan.FromMilliseconds(300));
        var tools = new JournalTools(journal, null, new DigestService(journal, store, TimeZoneInfo.Utc));
        var first = tools.LogWork(new("in_progress", Tickets: [Dep7], Summary: "one", OccurredAt: "2026-10-20T09:00:00Z"));
        Assert.Null(first.DigestRequest);
        Directory.CreateDirectory(box.Data);
        var name = "worklog-digests-" + WorkLog.Infrastructure.ContentHash.Compute(Path.GetFullPath(Path.Combine(box.Data, "digests.db")).ToUpperInvariant());
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
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var second = tools.LogWork(new("in_progress", Tickets: [Dep7], Summary: "two", OccurredAt: "2026-10-20T11:00:00Z"));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.True(second.Created);
        Assert.Null(second.DigestRequest);
        var failure = Assert.ThrowsAny<Exception>(() => tools.SaveDigest([first.Entry.Id, second.Entry.Id], "Both."));
        Assert.Contains("busy", failure.Message);
        release.Set();
        holder.Join();
        // The timeout was for those calls only: the next observation gets a request again.
        var third = tools.LogWork(new("in_progress", Tickets: [Dep7], Summary: "three", OccurredAt: "2026-10-20T12:00:00Z"));
        Assert.NotNull(third.DigestRequest);
    }

    [Fact]
    public void BackdatedObservationOrdersByOccurrenceNotInsertion()
    {
        using var rig = new Rig();
        var late = rig.Log("2026-10-20T15:00:00Z");
        var early = rig.Log("2026-10-20T08:00:00Z");
        Assert.Equal([early.Entry.Id, late.Entry.Id], Assert.IsType<DigestRequest>(early.DigestRequest).Entries.Select(e => e.Id));
    }

    [Fact]
    public void RetryReplayOtherItemsAndOtherDaysProduceNoRequest()
    {
        using var rig = new Rig();
        rig.Log("2026-10-20T09:00:00Z", key: "k1");
        var created = rig.Log("2026-10-20T10:00:00Z", key: "k2");
        Assert.NotNull(created.DigestRequest);
        var replay = rig.Log("2026-10-20T10:00:00Z", key: "k2");
        Assert.False(replay.Created);
        Assert.Null(replay.DigestRequest);
        Assert.Null(rig.Log("2026-10-20T10:30:00Z", Dep8).DigestRequest);
        Assert.Null(rig.Log("2026-10-21T10:30:00Z").DigestRequest);
    }

    [Fact]
    public void ThirtyObservationsRequestAndThirtyFirstDoesNot()
    {
        using var rig = new Rig();
        LogResult last = null!;
        for (var i = 0; i < 30; i++)
            last = rig.Log($"2026-10-20T{i / 6 + 1:00}:{i % 6 * 10:00}:00Z");
        Assert.Equal(30, Assert.IsType<DigestRequest>(last.DigestRequest).Entries.Length);
        Assert.Null(rig.Log("2026-10-20T20:00:00Z").DigestRequest);
    }

    [Fact]
    public void MachineTimezoneMidnightBoundarySplitsTheDay()
    {
        // Warsaw is UTC+2 on 20 October 2026: 21:30Z on the 19th is 23:30 local, 22:30Z is 00:30 the next local day.
        var warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        using var rig = new Rig(warsaw);
        rig.Log("2026-10-19T21:30:00Z");
        Assert.Null(rig.Log("2026-10-19T22:30:00Z").DigestRequest);
        var request = Assert.IsType<DigestRequest>(rig.Log("2026-10-19T21:45:00Z").DigestRequest);
        Assert.Equal("2026-10-19", request.Day);
        Assert.Equal(2, request.Entries.Length);
        var next = rig.Log("2026-10-20T10:00:00Z");
        Assert.Equal("2026-10-20", Assert.IsType<DigestRequest>(next.DigestRequest).Day);
    }

    [Fact]
    public void ChangeOnlyItemsGetRequestsAndLegacyUnlinkedEntriesDoNot()
    {
        using var rig = new Rig();
        rig.Tools.LogWork(new("in_progress", Change: "c", OccurredAt: "2026-10-20T09:00:00Z"));
        Assert.NotNull(rig.Tools.LogWork(new("in_progress", Change: "c", OccurredAt: "2026-10-20T10:00:00Z")).DigestRequest);
    }

    [Fact]
    public void UnlinkedLegacyObservationsGetNoRequestAndCannotBeSaved()
    {
        // Observations with neither a change nor a ticket exist only in older journals, so they are inserted directly.
        using var rig = new Rig();
        Directory.CreateDirectory(rig.Box.Data);
        var ids = new List<string>();
        foreach (var at in new[] { "2026-10-20T09:00:00Z", "2026-10-20T10:00:00Z" })
        {
            var occurred = DateTimeOffset.Parse(at);
            var entry = new Entry(Guid.NewGuid().ToString("N"), "repo", null, null, [], null, "in_progress", "unclassified", "legacy", occurred, occurred, "test", null);
            using var connection = new SqliteConnection($"Data Source={Path.Combine(rig.Box.Data, "journal.db")};Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO entries(id,occurred,repository,change_name,status,payload) VALUES($id,$at,$repo,NULL,$status,$payload)";
            command.Parameters.AddWithValue("$id", entry.Id);
            command.Parameters.AddWithValue("$at", occurred.UtcTicks);
            command.Parameters.AddWithValue("$repo", "repo");
            command.Parameters.AddWithValue("$status", "in_progress");
            command.Parameters.AddWithValue("$payload", WorkLog.Infrastructure.Json.Encode(entry));
            command.ExecuteNonQuery();
            ids.Add(entry.Id);
            Assert.Null(new DigestService(rig.Journal, rig.Store, TimeZoneInfo.Utc).Request(entry));
        }
        var failure = Assert.Throws<McpException>(() => rig.Tools.SaveDigest(ids.ToArray(), "text"));
        Assert.Contains("without a change or ticket", failure.Message);
    }

    [Fact]
    public void BrokenDigestStoreLeavesTheLoggingResultIntact()
    {
        using var rig = new Rig();
        Directory.CreateDirectory(Path.Combine(rig.Box.Data, "digests.db"));
        rig.Log("2026-10-20T09:00:00Z");
        var second = rig.Log("2026-10-20T10:00:00Z");
        Assert.True(second.Created);
        Assert.Null(second.DigestRequest);
        Assert.Equal(2, rig.Tools.QueryWork(new("2026-10-20T00:00:00Z", "2026-10-21T00:00:00Z", "UTC")).Entries.Length);
        var failure = Assert.Throws<McpException>(() => rig.Tools.SaveDigest([second.Entry.Id, "x"], "text"));
        Assert.Contains("unavailable", failure.Message);
    }

    [Fact]
    public void OptionalRequestDiagnosticsAreSafeBoundedAndCannotBreakLogging()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        Directory.CreateDirectory(Path.Combine(box.Data, "digests.db"));
        var clock = new TestClock();
        var messages = new List<string>();
        var service = new DigestService(journal, new DigestStore(box.Data), TimeZoneInfo.Utc, messages.Add, clock);
        var tools = new JournalTools(journal, null, service);
        var first = tools.LogWork(new("in_progress", Tickets: [Dep7], Summary: "private observation text", OccurredAt: "2026-10-20T09:00:00Z"));
        var second = tools.LogWork(new("in_progress", Tickets: [Dep7], Summary: "private observation text", OccurredAt: "2026-10-20T10:00:00Z"));
        var third = tools.LogWork(new("in_progress", Tickets: [Dep7], Summary: "private observation text", OccurredAt: "2026-10-20T11:00:00Z"));

        Assert.True(first.Created && second.Created && third.Created);
        Assert.Null(second.DigestRequest);
        Assert.Null(third.DigestRequest);
        Assert.Single(messages);
        Assert.Equal("Optional digest request skipped (storage-read).", messages[0]);
        Assert.DoesNotContain("private observation text", messages[0]);
        clock.Advance(TimeSpan.FromMinutes(1));
        var fourth = tools.LogWork(new("in_progress", Tickets: [Dep7], Summary: "private observation text", OccurredAt: "2026-10-20T12:00:00Z"));
        Assert.True(fourth.Created);
        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public void OptionalDiagnosticSinkFailureDoesNotChangeLogWorkResult()
    {
        using var box = new TestWorkspace();
        var journal = new Journal(new LocalSettings(box.Data), new("repo"));
        Directory.CreateDirectory(Path.Combine(box.Data, "digests.db"));
        var service = new DigestService(journal, new DigestStore(box.Data), TimeZoneInfo.Utc,
            diagnosticSink: _ => throw new IOException("diagnostic sink failed"));
        var tools = new JournalTools(journal, null, service);
        tools.LogWork(new("in_progress", Tickets: [Dep7], Summary: "one", OccurredAt: "2026-10-20T09:00:00Z"));

        var result = tools.LogWork(new("in_progress", Tickets: [Dep7], Summary: "two", OccurredAt: "2026-10-20T10:00:00Z"));

        Assert.True(result.Created);
        Assert.Null(result.DigestRequest);
        Assert.Equal(2, journal.ReadBetween(DateTimeOffset.Parse("2026-10-20T00:00:00Z"), DateTimeOffset.Parse("2026-10-21T00:00:00Z")).Length);
    }

    [Fact]
    public void NewerDigestStoreDisablesDigestsWithAnExplanation()
    {
        using var rig = new Rig();
        Directory.CreateDirectory(rig.Box.Data);
        using (var connection = new SqliteConnection($"Data Source={Path.Combine(rig.Box.Data, "digests.db")};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE digests(x); PRAGMA user_version=9;";
            command.ExecuteNonQuery();
        }
        var first = rig.Log("2026-10-20T09:00:00Z");
        var second = rig.Log("2026-10-20T10:00:00Z");
        Assert.True(second.Created);
        Assert.Null(second.DigestRequest);
        var failure = Assert.Throws<McpException>(() => rig.Tools.SaveDigest([first.Entry.Id, second.Entry.Id], "text"));
        Assert.Contains("newer than supported", failure.Message);
        Assert.Equal(2, rig.Tools.QueryWork(new("2026-10-20T00:00:00Z", "2026-10-21T00:00:00Z", "UTC")).Entries.Length);
    }

    private static (Rig Rig, LogResult First, LogResult Second) Two()
    {
        var rig = new Rig();
        var first = rig.Log("2026-10-20T09:00:00Z");
        var second = rig.Log("2026-10-20T10:00:00Z");
        return (rig, first, second);
    }

    [Fact]
    public void SaveStoresTheDigestForTheItemAndDay()
    {
        var (rig, first, second) = Two();
        using var _ = rig;
        var saved = rig.Tools.SaveDigest([second.Entry.Id, first.Entry.Id], "  Shipped\r\nthe   retry fix.\n");
        Assert.Equal("Shipped the retry fix.", saved.Text);
        Assert.Equal("2026-10-20", saved.Day);
        Assert.Equal(new[] { first.Entry.Id, second.Entry.Id }.Order(StringComparer.Ordinal), saved.Covers);
        Assert.Equal(saved.Item, second.DigestRequest!.Item);
        var stored = Assert.Single(rig.Store.Read([saved.Item]));
        Assert.Equal(saved.Text, stored.Text);
        // Observations are unchanged by saving.
        Assert.Equal(2, rig.Tools.QueryWork(new("2026-10-20T00:00:00Z", "2026-10-21T00:00:00Z", "UTC")).Entries.Length);
    }

    [Fact]
    public void RepeatedIdenticalSaveLeavesOneDigest()
    {
        var (rig, first, second) = Two();
        using var _ = rig;
        var a = rig.Tools.SaveDigest([first.Entry.Id, second.Entry.Id], "Same.");
        var b = rig.Tools.SaveDigest([first.Entry.Id, second.Entry.Id], "Same.");
        Assert.Equal(a.Text, b.Text);
        Assert.Single(rig.Store.Read([a.Item]));
    }

    [Fact]
    public void LaterObservationMakesAnOlderSetStaleAndNewerSetReplaces()
    {
        var (rig, first, second) = Two();
        using var _ = rig;
        var third = rig.Log("2026-10-20T11:00:00Z");
        var stale = Assert.Throws<McpException>(() => rig.Tools.SaveDigest([first.Entry.Id, second.Entry.Id], "Old."));
        Assert.Contains("Stale", stale.Message);
        var item = third.DigestRequest!.Item;
        Assert.Empty(rig.Store.Read([item]));
        rig.Tools.SaveDigest([first.Entry.Id, second.Entry.Id, third.Entry.Id], "All three.");
        var stored = Assert.Single(rig.Store.Read([item]));
        Assert.Equal("All three.", stored.Text);
        Assert.Equal(3, stored.Covers.Length);
    }

    [Fact]
    public void InvalidInputsFailWithoutWriting()
    {
        var (rig, first, second) = Two();
        using var _ = rig;
        var other = rig.Log("2026-10-20T10:30:00Z", Dep8);
        var nextDay = rig.Log("2026-10-21T10:30:00Z");
        var ids = new[] { first.Entry.Id, second.Entry.Id };
        void Fails(string[] entryIds, string text, string expected)
        {
            var failure = Assert.Throws<McpException>(() => rig.Tools.SaveDigest(entryIds, text));
            Assert.Contains(expected, failure.Message, StringComparison.OrdinalIgnoreCase);
        }
        Fails([first.Entry.Id, other.Entry.Id], "Mixed.", "different work items");
        Fails([first.Entry.Id, nextDay.Entry.Id], "Mixed days.", "different local days");
        Fails([first.Entry.Id, "missing-id"], "Unknown.", "Unknown observation ID");
        Fails(ids, "", "1 to 280");
        Fails(ids, " \r\n ", "1 to 280");
        Fails(ids, new string('x', 281), "1 to 280");
        Fails([first.Entry.Id], "One.", "2 to 30");
        Fails([first.Entry.Id, first.Entry.Id], "Duplicated.", "2 to 30");
        Assert.Empty(rig.Store.Read([second.DigestRequest!.Item]));
        rig.Tools.SaveDigest(ids, new string('x', 280));
    }
}
