using System.Text.Json;
using Microsoft.Data.Sqlite;
using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Persistence;
using WorkLog.Repositories;

namespace WorkLog.Smoke;

/// <summary>Deterministic, disposable C06 before/after fixture and independent writer controls.</summary>
internal static class C06BaselineFixture
{
    public static void Create(string directory)
    {
        var root = Path.GetFullPath(directory);
        if (Directory.Exists(root))
            throw new IOException("C06 fixture root must be new.");
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(data);
        var settings = new LocalSettings(data);
        var repos = new List<(string Id, string Path)>();
        for (var index = 1; index <= 2; index++)
        {
            var identity = $"example/c06-{index}";
            var repo = Path.Combine(root, "repositories", $"c06-{index}");
            Directory.CreateDirectory(repo);
            Directory.CreateDirectory(Path.Combine(repo, "openspec", "changes"));
            File.WriteAllText(Path.Combine(repo, "openspec", "config.yaml"), "schema: spec-driven\n");
            GitRepository.Run(repo, "init", "--initial-branch=main");
            GitRepository.Run(repo, "add", "openspec/config.yaml");
            GitRepository.Run(repo, "-c", "user.name=C06 Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-m", "Initialize isolated C06 repository");
            settings.Configuration.RepositoryContexts[GitRepository.GetCommonDirectory(repo)] = new(new(identity));
            AddChange(repo, $"c06-active-{index}", archived: false, documentCount: 50, largeFirst: true);
            AddChange(repo, $"archive/2026-10-01-c06-archived-{index}", archived: true, documentCount: 4, largeFirst: false);
            repos.Add((identity, repo));
        }
        settings.Configuration.Updates.Enabled = false;
        settings.Save();

        var now = DateTimeOffset.UtcNow;
        var journal = new Journal(settings);
        var count = 0;
        foreach (var repository in repos)
        {
            for (var index = 0; index < 500; index++)
            {
                var ticket = $"C06-{repository.Id[^1]}-{index / 2:D3}";
                var result = journal.Log(new(index % 7 == 0 ? "started" : "in_progress", Change: "c06-active-" + repository.Id[^1],
                    Tickets: [new("jira", "example.invalid", ticket)],
                    Summary: $"C06 synthetic observation {++count:D4}", OccurredAt: now.AddDays(-2).AddMinutes(index).ToString("O"),
                    Agent: "c06-fixture", Repository: repository.Id));
            }
        }
        var dailyPairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var repository in repos)
        {
            var ticket = $"C06-DAY-{repository.Id[^1]}";
            journal.Log(new("started", Tickets: [new("jira", "example.invalid", ticket)], Summary: "C06 daily digest pair one", OccurredAt: now.AddMinutes(-10).ToString("O"), Agent: "c06-fixture", Repository: repository.Id));
            journal.Log(new("in_progress", Tickets: [new("jira", "example.invalid", ticket)], Summary: "C06 daily digest pair two", OccurredAt: now.AddMinutes(-5).ToString("O"), Agent: "c06-fixture", Repository: repository.Id));
            dailyPairs.Add(repository.Id, ticket);
        }
        // Save one stored digest for each of the two fixture repositories, each covering a pair on the current local day.
        var digestStore = new DigestStore(settings.DataDirectory);
        var digestCount = 0;
        foreach (var repository in repos)
        {
            var ticket = dailyPairs[repository.Id];
            var entries = journal.Query(new(now.AddDays(-1).ToString("O"), now.AddDays(1).ToString("O"), "UTC", repository.Id,
                Ticket: ticket, PageSize: 10, View: "flat")).Entries.OrderBy(e => e.OccurredAt).ToArray();
            if (entries.Length != 2)
                throw new InvalidOperationException("C06 daily digest pair was not seeded.");
            new DigestService(journal, digestStore, TimeZoneInfo.Utc).Save(entries.Select(e => e.Id).ToArray(), "C06 fixture digest summary.");
            digestCount++;
        }

        var metadata = new
        {
            schemaVersion = 1,
            data,
            repositories = repos.Select(r => new { id = r.Id, path = r.Path }).ToArray(),
            activeChanges = 2,
            archivedChanges = 2,
            markdownDocumentsPerActiveChange = 50,
            archivedMarkdownDocumentsPerChange = 4,
            repeatedNestedBasenames = true,
            openDocument = new
            {
                change = "c06-active-1",
                path = "design.md",
                bytes = 100 * 1024
            },
            syntheticObservations = count,
            storedDailyDigests = digestCount,
            dailyDigestTickets = dailyPairs,
            seedWriter = "C06BaselineFixture, single process, WorkLog Journal and DigestService",
            createdAtUtc = DateTimeOffset.UtcNow
        };
        File.WriteAllText(Path.Combine(root, "fixture.json"), JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(metadata));
    }

    public static void Log(string directory, string repository, string change, string ticket, string summary)
    {
        var settings = new LocalSettings(Path.Combine(Path.GetFullPath(directory), "data"));
        var created = new Journal(settings).Log(new("in_progress", change, Tickets: [new("jira", "example.invalid", ticket)],
            Summary: summary, Agent: "c06-independent-writer", Repository: repository));
        var committedAt = DateTimeOffset.UtcNow;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            entryId = created.Entry.Id,
            committedAtUtc = committedAt
        }));
    }

    public static void SaveDigest(string directory, string repository, string ticket, string summary)
    {
        var settings = new LocalSettings(Path.Combine(Path.GetFullPath(directory), "data"));
        var journal = new Journal(settings);
        var journalPath = Path.Combine(settings.DataDirectory, "journal.db");
        var snapshotBefore = JournalMaxRowId(journalPath);
        var now = DateTimeOffset.UtcNow;
        var pair = journal.Query(new(now.AddDays(-1).ToString("O"), now.AddDays(1).ToString("O"), "UTC", repository,
            Ticket: ticket, PageSize: 10, View: "flat")).Entries.OrderBy(e => e.OccurredAt).ToArray();
        if (pair.Length != 2)
            throw new InvalidOperationException("C06 digest-only writer requires the fixture's unchanged pair of observations.");
        var store = new DigestStore(settings.DataDirectory);
        var saved = new DigestService(journal, store, TimeZoneInfo.Utc).Save(pair.Select(e => e.Id).ToArray(), summary);
        var committedAt = DateTimeOffset.UtcNow;
        var snapshotAfter = JournalMaxRowId(journalPath);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            committedAtUtc = committedAt,
            journalSnapshotBefore = snapshotBefore,
            journalSnapshotAfter = snapshotAfter,
            digestSaved = true,
            digestDay = saved.Day
        }));
    }

    private static long JournalMaxRowId(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(rowid), 0) FROM entries";
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    private static void AddChange(string repository, string name, bool archived, int documentCount, bool largeFirst)
    {
        var directory = Path.Combine(repository, "openspec", "changes", name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ".openspec.yaml"), "schema: spec-driven\ncreated: 2026-10-01\n");
        var largeHeader = "# C06 Active Design\n\nC06_TARGET_DOCUMENT\n";
        File.WriteAllText(Path.Combine(directory, "design.md"), largeFirst
            ? largeHeader + new string('x', 100 * 1024 - System.Text.Encoding.UTF8.GetByteCount(largeHeader))
            : $"# C06 Archived Design\n\nSynthetic archived design {name}.\n");
        if (documentCount >= 3)
        {
            File.WriteAllText(Path.Combine(directory, "proposal.md"), $"# C06 Proposal\n\nSynthetic proposal {name}.\n");
            File.WriteAllText(Path.Combine(directory, "tasks.md"), "# Tasks\n\n- [x] 1.1 Fixture\n- [ ] 1.2 Measure\n");
        }
        var remaining = documentCount - (documentCount >= 3 ? 3 : 1);
        for (var index = 0; index < remaining; index++)
        {
            var nested = index % 2 == 0
                ? Path.Combine("specs", $"capability-{index:D2}", "spec.md")
                : Path.Combine("notes", $"area-{index:D2}", "readme.md");
            var target = Path.Combine(directory, nested);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, $"# C06 nested document {index:D2}\n\nFixture {name}; repeated basenames exercise nested source resolution.\n");
        }
        _ = archived;
    }
}
