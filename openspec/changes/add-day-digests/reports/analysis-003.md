<!-- Saved by the orchestrator. Reviewer: E2 code-analyzer subagent; client Claude Code 2.1.312, background subagent mode; model claude-opus-5-5 (role default). Case: the client refused the reviewer's write. Exact refusal message: "Subagents should return findings as text, not write report files. Include this content in your final response instead." Report text below is verbatim as returned (the separate return block is recorded in findings.md, not here). -->

# Analysis report: add-day-digests / E2 (recheck)

## Review target

- Reviewer / role: code-analyzer subagent (assignment E2) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 / background subagent under the orchestrator session, auto mode / claude-opus-5-5 (role default)
- Date: 2026-10-29 (UTC)
- Change root / assignment: `openspec/changes/add-day-digests`, assignment E2 (code-analyzer recheck of D1)
- Task and requirement references: tasks 1.1, 2.1, 3.1, 4.2, 4.3 ([tasks.md](../tasks.md)); [day-digests](../specs/day-digests/spec.md) "Digest request on logging", "Digest storage compatibility"; [daily-preparation](../specs/daily-preparation/spec.md) "Digests on daily lines", "Copy for daily"; [local-agent-integration](../specs/local-agent-integration/spec.md) "Day digest instructions"; design Decisions 1, 3, 4
- Scope and relevant neighboring paths: the D1 diff within the 37 manifest paths:
  - `src/WorkLog/Persistence/DigestStore.cs` (`Revision()`, `lockTimeout`)
  - `src/WorkLog/Dashboard/SourceCatalog.cs` (`CatalogSnapshot.DigestRevision`), `src/WorkLog/Dashboard/DashboardHost.cs` (wiring), `src/WorkLog/Dashboard/DailyService.cs`
  - `src/WorkLog/Journaling/DigestService.cs` (instruction text), `src/WorkLog/Integration/AgentInstructions.cs`, `src/WorkLog/Mcp/JournalTools.cs`
  - `dashboard/src/app.ts` (`poll()`), `dashboard/src/models.ts`, `dashboard/src/app.daily.spec.ts`, `dashboard/e2e/dashboard.spec.ts` (digest scenarios)
  - `tools/WorkLog.Smoke/DashboardFixture.cs`, `tools/WorkLog.Smoke/Program.cs` (`--dashboard-digest`)
  - `tests/WorkLog.Tests/Persistence/DigestStoreTests.cs`, `tests/WorkLog.Tests/Mcp/DigestTests.cs`, `tests/WorkLog.Tests/Integration/InstructionOptionsTests.cs`
  - `docs/dashboard-validation.md`
- Reading limits and paths read outside scope: recheck only (assigned IDs plus D1 regression, no new suggestions). Outside the manifest I read:
  - `.agents/roles/code-analyzer.md`, `.agents/roles/project-facts.md`, `.agents/templates/analysis-report.md`
  - `dashboard/playwright.config.ts` (timeouts) and `docs/architecture.md` (grep only)
  - this change's [analysis-002](analysis-002.md) and [findings.md](findings.md)
- Baseline commit: `5a73ab9`
- Implementation identity (before): `pwsh ./scripts/review-identity.ps1 -Baseline 5a73ab9 -Paths src/WorkLog tests tools/WorkLog.Smoke dashboard/src dashboard/e2e docs` gave:
  - HEAD `5a73ab94bdc5`; 28 modified and 9 untracked paths
  - tracked diff SHA-256 `b095bae97849402988a6a8bbe091ae43c5acc06ababdd321b2b8b12ee24fe2a4`
  - summary `head=5a73ab9 baseline=5a73ab9 files=37 diff=b095bae97849 untracked=9/3fedf13b4948` (matches the assignment)
- Before/after identity: the same command after the analysis gave `head=5a73ab9 baseline=5a73ab9 files=37 diff=b095bae97849 untracked=9/3fedf13b4948`. Identical, so the target was not changed.
- Prior reports / rechecked findings: [analysis-002](analysis-002.md): ANALYSIS-011, ANALYSIS-013 (bounded digest mutex wait only; the shared-helper refactor was not requested), ANALYSIS-018, ANALYSIS-019

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-011: server marker | Read `DigestStore.Revision()` (lines 132-154), `SourceCatalog.DiscoverAsync` (line 215) and `GetAsync` (lines 42-53) | Each catalog discovery does one read-only, unpooled open plus `SELECT COALESCE(MAX(saved),0) FROM digests`. The catalog semaphore runs one discovery at a time, so the unsynchronized `lastRevision` field is never written concurrently. A missing file is not created (`File.Exists` check, read-only mode). | Correct |
| ANALYSIS-011: missing / unreadable / newer store | Read `Revision()`; `DigestStoreTests.RevisionMovesAfterASaveAndIsStableWithoutOneOrWhenUnreadable` | A missing file returns the last seen value (0 at first). A non-database file, a version-0 file (no table) or a locked file raises `SqliteException`, which is caught, so the last value is returned. Nothing is thrown into the catalog path. In a newer store that still has a `saved` column the marker may move; the reload is harmless because `DailyService.Digests` catches the failed read and returns `[]`. | Correct: the marker never throws and never flips on a transient failure |
| ANALYSIS-011: client | Read `app.ts` `poll()` (lines 2031-2045), `refreshData()` (line 715) and start-up (line 435); `app.daily.spec.ts` lines 300-316 | The start-up catalog sets `digestRevision`, so the first poll does not reload. When only the digest marker moves, Daily reloads once, and only if the Daily view is open. When the journal also moves, or the day rolls over, in the same poll, `dataRefreshed` skips the second load, because `refreshData()` already loads Daily. An older server leaves the field `undefined`, so it never differs. If a poll returns early (document epoch changed), the marker is not recorded and the next poll picks it up. | Correct, with no double Daily reload. The unit test covers only "digest moved" and "nothing moved"; I checked the "both moved" branch by reading the source |
| ANALYSIS-011: end to end | Read the e2e scenario "a digest saved while Daily is open ...", `DashboardFixture.SaveDigest` and `Program.cs` `--dashboard-digest` | A second process saves through the real `DigestService.Save` while `/daily` is open. The scenario expects the digest text and mark within 10 s without pressing Refresh, then checks the copied line. The smoke mode is fixture-only, uses the fixture's isolated data directory, and is not part of the default smoke run. | Covers the reported scenario |
| ANALYSIS-013: bounded wait | Read `EnsureAvailable` (lines 42-82); `DigestStoreTests.LockWaitIsBoundedAndTimeoutDoesNotDisableTheStore`; `DigestTests.BusyDigestLockSkipsTheRequestWithinTheBoundWithoutFailingLogWork` | The wait is `WaitOne(lockTimeout ?? 3 s)`. A timeout throws `DigestStoreUnavailableException` without setting `disabledReason` or `initialized`. It throws before the `ReleaseMutex` block, so a mutex this process does not own is never released. The abandoned-mutex path is unchanged. The tests show that `log_work` returns within the bound with no request, that `save_digest` fails with "busy", and that the next call after the lock is released gets a request again. | Correct: a timeout makes digests unavailable for that call only, and a newer store version still disables them for the whole process |
| ANALYSIS-018 and parity | Read `DigestService.Instruction` (line 17), the paragraph in `AgentInstructions.cs` (line 20), the `save_digest` descriptions in `JournalTools.cs`, `InstructionOptionsTests.DigestParagraphIsSharedByClaudeCodeAndCodexOwnedBlocksAndOmittedWhenDisabled`, and `git status` of `src/WorkLog/Integration/Clients` | The tool-result text now says "on the stated day", and `DigestTests` asserts that and the absence of "today". The managed paragraph and the tool descriptions say "that day". The instruction is a server-side constant in the MCP result, so both clients get the same text. The managed paragraph is rendered once in `AgentInstructions`, and the test asserts it is identical in the Claude Code (`CLAUDE.local.md`) and Codex (`.codex/config.toml`) owned blocks. Nothing under `Integration/Clients` changed. | Claude Code and Codex are covered identically: no parity gap and no per-client policy |
| ANALYSIS-019 | Read `DashboardFixture.DigestDemo` and `SaveDigest`; `docs/dashboard-validation.md` lines 261-262 | Both build `DigestService(..., TimeZoneInfo.Utc)`, so creating the fixture no longer depends on the machine's timezone. The browser assertions still rely on the Daily fixture's documented UTC-11 to UTC+10 range (observations around noon UTC), and the docs say so. | Correct |
| Regression: wiring | Read the `DashboardHost.cs` diff | `DailyService` (reads) and `SourceCatalog` (marker) share one `DigestStore` instance. The dashboard never calls `EnsureAvailable` or `Save`, so it never creates or migrates `digests.db`. Other `SourceCatalog` constructions default to `digests: null`, which gives a marker of 0. | No regression |
| Poll cost | Scratch Python 3 / SQLite 3.50.4 script in the session scratchpad. Synthetic stores used the production schema (WAL, 250-character text, 5 covered IDs per row). Each sample opened a fresh read-only connection and ran the marker query, 50 samples per size | Median (p95) per open plus query: 60 rows 0.74 (1.36) ms; 4800 rows / 3.2 MiB 2.57 (3.52) ms; 20000 rows / 13 MiB 6.61 (8.43) ms. Time grows linearly with store size because `saved` has no index, so `MAX(saved)` reads the whole table. | One query per catalog discovery, from under 1 ms to a few ms at realistic and synthetic upper sizes. Measured with Python's SQLite, not Microsoft.Data.Sqlite, so the numbers show scale only |
| Formatting | `dotnet format --verify-no-changes` | Not run: QA E1 builds concurrently, and loading the format workspace could contend with it | not performed |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Dashboard 2 s `poll()`; `SourceCatalog.DiscoverAsync` | Measured (scratch, synthetic) + source observation | An idle poll is still one `catalog` request. D1 adds one `File.Exists`, one read-only SQLite open and one `MAX(saved)` query per discovery: about 0.7 ms at realistic sizes and 2.6 ms at the synthetic 4800-row upper case. No process is spawned and the journal is not read. `/api/daily` reruns only when the marker moves, which is once per saved digest, a few times a day. The catalog cache lasts 2 s and the poll runs every 2 s, so discovery effectively runs on every poll, as it did before D1. |
| Cache lifetime versus interval and refresh duration | Catalog snapshot cache (2 s); `DigestStore.lastRevision` | Source observation | The marker uses the existing 2 s catalog cache, so a saved digest appears within one or two poll intervals (about 4 s at most); the e2e allows 10 s. `lastRevision` is only a fallback value for failed reads and does not expire. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `digests.db` rows | Measured (synthetic) + source observation | D1 does not change this: there is one row per item per day and no retention (a design Non-Goal). The marker query's cost grows linearly with the row count because `saved` has no index (measured above). The rest of ANALYSIS-016 (deferred) is unchanged. |
| Repeated full reads where a change check would do | Marker query; Daily reload | Source observation | The marker is itself the change check: Daily rereads only when it moves, and a journal change and a digest change in the same poll cause a single reload. The marker query scans the digest table (see above); at current sizes that costs less than reading the journal snapshot. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | `DigestStore` named mutex; SQLite busy timeouts | Source observation | There are no external tools or PATH lookups. The mutex wait is now bounded at 3 s, and it applies only until a process has initialized the store once. Other waits are unchanged and outside this recheck's scope: the read-write open inside the lock keeps SQLite's 15 s busy timeout, and the read-only opens in `Revision()` and `Read()` use 5 s. A reader blocks only while another process holds a lock outside WAL mode, for example during the first migration. |

## Findings

**No findings.** This recheck raises no new findings; ANALYSIS-051 – ANALYSIS-070 are unused.

Two minor observations for the orchestrator. Neither reaches the finding threshold or counts as a new suggestion:
- The marker is `MAX(saved)`, based on the wall clock. If the system clock is set back between two saves, and the second save does not replace the row that holds the maximum, the marker does not move until the next journal change. This needs a backwards clock step between saves, so in practice the effect is negligible.
- When the store is busy, `save_digest` fails with a `DigestStoreUnavailableException` whose text ends "Digests are skipped for this call; try again later." The managed paragraph tells agents to skip the digest when `save_digest` is unavailable. An agent may treat "busy" as either case, and both outcomes are harmless. The text is server-side and identical for both clients.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-011 | `head=5a73ab9 baseline=5a73ab9 files=37 diff=b095bae97849 untracked=9/3fedf13b4948` | Source read: `DigestStore.Revision()`, `SourceCatalog` line 215, the `DashboardHost` wiring, and `app.ts` `poll()` lines 2031-2045. Tests read: `DigestStoreTests.RevisionMovesAfterASaveAndIsStableWithoutOneOrWhenUnreadable`; `app.daily.spec.ts` "reloads Daily when only the digest revision moved"; and the e2e "a digest saved while Daily is open ... without Refresh". Measured marker cost: 0.7–2.6 ms per poll. A missing or unreadable store keeps the last value, there is no double reload, and older servers and clients stay compatible. I did not run the tests; QA E1 owns execution. | verified |
| ANALYSIS-013 (bounded mutex wait only) | same identity | `EnsureAvailable` lines 52-57 bound the wait (3 s by default, injectable). A timeout throws "unavailable" without setting `disabledReason` or `initialized`, and never releases a mutex this process does not own. Tests read: `LockWaitIsBoundedAndTimeoutDoesNotDisableTheStore` and `BusyDigestLockSkipsTheRequestWithinTheBoundWithoutFailingLogWork`: `log_work` succeeds within the bound with no request, and the next call recovers. The shared-helper refactor was not requested and I did not check it. | verified (bounded-wait scope) |
| ANALYSIS-018 | same identity | `DigestService.Instruction` says "on the stated day"; `DigestTests` asserts that and that "today" is absent. This agrees with the managed paragraph and the `save_digest` descriptions ("that day"). The instruction is server-side and identical for Claude Code and Codex; the managed paragraph is shared, and a test asserts it is identical in both clients' owned blocks. | verified |
| ANALYSIS-019 | same identity | `DashboardFixture.DigestDemo` and `SaveDigest` pass `TimeZoneInfo.Utc`, and `docs/dashboard-validation.md` records this. The browser assertions keep the documented zone range of the Daily fixture. | verified |

## Assessment and limitations

The D1 corrections resolve all four assigned findings within the requested scope, and the D1 diff shows no regression:
- The catalog adds one cheap read-only query per discovery, with no spawned process and no journal read.
- A missing, unreadable or newer store never breaks the catalog and never causes repeated false reloads.
- Daily reloads once per saved digest, and once (not twice) when the journal and the digests change in the same poll.
- A lock timeout makes digests unavailable for that call only, while a newer store version still disables them for the process.
- The instruction text is server-side and the managed paragraph is shared, so Claude Code and Codex behave identically.
- The fixture no longer depends on the machine's timezone, and the new `--dashboard-digest` smoke mode is fixture-only.

Limitations:
- No repository build, test, e2e or `dotnet format` command was run, to avoid contending with the concurrent QA E1 build. Test outcomes come from reading the source and from the D1 results in [findings.md](findings.md): dotnet 405/405, npm 245/245, packaged e2e 39/39.
- The "journal and digest moved in the same poll" branch of `poll()` has no unit test; I verified it by reading the source.
- The poll-cost timings come from a Python SQLite script on synthetic stores in the session scratchpad, not from the .NET provider or a running dashboard. They show scale only.
- No dashboard, MCP server or live agent was started. The user's data directory, port 47831, installation and client settings were not touched.
- The only repository write attempted was this report, and the client refused it.
