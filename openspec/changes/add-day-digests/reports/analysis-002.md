<!-- Saved by the orchestrator. Reviewer: C2 code-analyzer subagent; client Claude Code 2.1.312, background subagent mode; model claude-opus-5-5 (role default). Case: the client refused the reviewer's write. Exact refusal message: "Subagents should return findings as text, not write report files. Include this content in your final response instead." Report text below is verbatim as returned. -->

# Analysis report: add-day-digests / C2 (full review)

Saved by the orchestrator: the client refused the analyzer's own write ("Subagents should return findings as text, not write report files. Include this content in your final response instead."), and the text below is the analyzer's report verbatim.

## Review target

- Reviewer / role: code-analyzer subagent (assignment C2) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 / subagent under the orchestrator session, auto mode / claude-opus-5-5 (role default)
- Date: 2026-10-29T15:15Z
- Change root / assignment: `openspec/changes/add-day-digests`, assignment C2
- Task and requirement references: tasks 1.1, 2.1, 2.2, 2.3, 2.4, 3.1, 4.1, 4.2, 4.3, 5.1 ([tasks.md](../tasks.md)); design Decisions 1–5 and 7 ([design.md](../design.md)); [day-digests](../specs/day-digests/spec.md), [daily-preparation](../specs/daily-preparation/spec.md), [local-agent-integration](../specs/local-agent-integration/spec.md); accepted deviations in [findings.md](findings.md) "Scope decisions"
- Scope and relevant neighboring paths: the 36 manifest paths (`src/WorkLog`, `tests`, `tools/WorkLog.Smoke`, `dashboard/src`, `dashboard/e2e`, `docs`). Neighbors read for comparison: `src/WorkLog/Persistence/JournalDatabase.cs`, `JournalSchema.cs`, `SqliteCommands.cs`, `src/WorkLog/Reporting/WorkIdentity.cs`, `src/WorkLog/Dashboard/TimelineService.cs` (IdFor, Unlinked), `DashboardJournal.cs` (open), `SourceCatalog.cs` (journal snapshot), `src/WorkLog/Infrastructure/SettingsLease.cs` (mutex wait), `src/WorkLog/Journaling/EntryFactory.cs` (ID format), `dashboard/src/app.ts` (poll and Daily loading), `dashboard/e2e/server.mjs`, `docs/architecture.md`
- Reading limits and paths read outside scope: none set beyond the assignment. Outside the manifest I read only the neighbors above plus `.agents/roles/code-analyzer.md`, `.agents/roles/project-facts.md`, `.agents/templates/analysis-report.md`, `AGENTS.md`
- Baseline commit: `5a73ab9`
- Implementation identity (before): `pwsh ./scripts/review-identity.ps1 -Baseline 5a73ab9 -Paths src/WorkLog tests tools/WorkLog.Smoke dashboard/src dashboard/e2e docs` → HEAD `5a73ab94bdc5`, 27 modified and 9 untracked paths, tracked diff SHA-256 `2c27c3a4f152b0e0494e8c57e928d9778ee07ae04191cd191702613aeb3083aa`; summary `head=5a73ab9 baseline=5a73ab9 files=36 diff=2c27c3a4f152 untracked=9/6019131d0fc6` (matches the assignment)
- Before/after identity: same command after the analysis → `head=5a73ab9 baseline=5a73ab9 files=36 diff=2c27c3a4f152 untracked=9/6019131d0fc6`. Identical; the target was not mutated.
- Prior reports / rechecked findings: [analysis-001](analysis-001.md) (task 0.1, ANALYSIS-001 deferred; not rechecked here). No rechecks assigned.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Duplication: stores | Side-by-side read of `DigestStore` with `JournalDatabase`, `JournalSchema`, `DashboardJournal`, `SqliteEntryStore.ReadByIds` | Same connection-string builder, named-mutex block with abandoned-mutex handling, `user_version` read and migration loop; the same lazy `IN (...)` parameter builder in two files | ANALYSIS-013 |
| Duplication: identity and local day | Grep for `IdFor`, `Unlinked`, `Midnight`, `ConvertTime` | The unlinked rule is written in 3 places; local-date conversion in `DailyService.LocalDate` and `DigestService.LocalDay`. The client sorts and joins IDs on its own, and this agrees with the server (IDs are GUIDs in `N` format, so ordinal and UTF-16 sort orders match) | ANALYSIS-012; client/server set comparison is consistent, no finding |
| Architecture fit | Compared namespaces with the `docs/architecture.md` area table | `Journaling.DigestService` imports `WorkLog.Dashboard` (`TimelineService.IdFor`, `DailyService.Midnight`); the only other importer of `Dashboard` is `Cli/DashboardCommand.cs` | ANALYSIS-012 |
| Correctness: stale race, set equality, normalization | Read `DigestService.Save`/`Request`, `digestFor`, tests | Set equality is sorted-ordinal on both sides; duplicates are removed before the count (accepted deviation 4); the race is accepted by Decision 3; normalization collapses all `\s+` (stricter than the spec, consistent with the design) | No finding beyond ANALYSIS-014 |
| Correctness: timezone/day boundaries | Read `LocalDay`/`EntriesOf`/`Midnight`, the Warsaw case in `DigestTests`, the e2e fixture | Request and Save share one zone and one range computation; the fixture validates in the machine zone | ANALYSIS-018, ANALYSIS-019 |
| Correctness: Daily refresh | Read `app.ts` `poll()` and the `SourceCatalog` snapshot | Daily reloads only when the journal snapshot changes or the day rolls over; `save_digest` writes only `digests.db` | ANALYSIS-011 |
| Performance: per-log journal read | Scratch console app outside the repository, referencing a copy of the Release `worklog.dll` from `src/WorkLog/bin/Release/net10.0` (not identity-verified, see limitations); disposable temp data directories; 25 timed `JournalTools.LogWork` calls per case after warm-up | Median `log_work`, without vs with digests: 0 other entries that day 5.22 vs 6.78 ms; 200 other entries 5.61 vs 10.97 ms; 1000 other entries 6.73 vs 16.27 ms | ANALYSIS-015 |
| Performance: `/api/daily` digest read | Same scratch app: `DigestStore.Read` for 40 items × 120 stored days (synthetic upper case) | 4800 rows, median 20.0 ms, about 1.7 MiB JSON | ANALYSIS-016 (realistic sizes are a hypothesis) |
| Diagnosability | Read the catch blocks | Bare `catch` in `DigestService.Request` and `DailyService.Digests`, no log | ANALYSIS-017 |
| Claude Code / Codex parity | Read the `AgentInstructions.cs` diff, the `InstructionOptionsTests` diff, the manifest (no `Integration/Clients` change), `JournalTools`, `UnknownFieldValidation` | One shared paragraph rendered for both clients; a test asserts that both owned blocks differ only in the agent name; the MCP contract and the tool-result `instruction` are server-side and client-neutral | Covers both clients; no parity gap and no per-client policy. ANALYSIS-018 affects both clients identically |
| Update probe (2.4) | Read `PayloadInstaller.ValidateAdvertisedTools` and the smoke assertions | The probe requires both journal tools and output schemas and accepts extra tools; the smoke checks assert the exact current listing, as Decision 7 says | No finding |
| Docs consistency | Read the `docs/mcp-validation.md` diff | Still calls the update incompatibility an open finding; test count 397 | ANALYSIS-020 |
| Formatting | `dotnet format --verify-no-changes` | Not run: QA C1 builds in parallel and loading the format workspace could contend with it | not performed |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Dashboard 2 s poll (`app.ts` `poll()`); MCP server | Source observation | The change adds no timer. An idle poll still makes one `catalog` request. `/api/daily` runs only when the journal snapshot changes or the day rolls over, and each run now adds one read-only `digests.db` open and one SELECT. The MCP server has no new loop; each created `log_work` adds one day-range journal read (measured). No processes are spawned. The flip side is ANALYSIS-011: a digest write never triggers a refresh. |
| Cache lifetime versus interval and refresh duration | `DigestStore.initialized` / `disabledReason` | Source observation | Process-lifetime flags with no expiry. They save a mutex wait and an open per call, but a version bump by a later release is never noticed (ANALYSIS-014). No time-based caches were added. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `digests.db` rows; the `/api/daily` `digests` array; the client `digestIndex` map | Source observation + measured synthetic size | One row per item per day with a saved digest, and no retention (a design Non-Goal). `/api/daily` returns every day's digest for each listed item, so the response grows with how long an item has been worked on (synthetic: 4800 rows ≈ 1.7 MiB, ANALYSIS-016). The SQLite parameter limit (32766) on the item IN-list is far away. In-process state is bounded. |
| Repeated full reads where a change check would do | `DigestService.Request` per log; `/api/daily` | Measured + source observation | Each created log reads and decodes every entry of that local day across all repositories, even for a first observation or above 30 entries (ANALYSIS-015). `/api/daily` still reads the journal up to tomorrow, as before; the new digest read has no day filter (ANALYSIS-016). |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | `DigestStore` named mutex, SQLite busy wait | Source observation | No external tools or PATH lookups; `digests.db` is at a fixed path in the data directory. `EnsureAvailable` waits on the named mutex with no timeout, and the first read-write open can wait up to 15 s of busy time, both inside `log_work` (ANALYSIS-013). |

## Findings

### ANALYSIS-011: A saved digest does not reach an open Daily view or its copy until the journal changes again

- Severity: warning
- Related task / requirement: 4.2, 4.3; daily-preparation "Digests on daily lines", "Copy for daily"; design Decision 4
- Locations: `dashboard/src/app.ts:2029-2035` (refreshes only when `snapshot.journalSnapshot` changes or the day rolls over); `src/WorkLog/Dashboard/SourceCatalog.cs:86-94` (the catalog snapshot is the journal's max rowid only); `src/WorkLog/Journaling/DigestService.cs:87` (`save_digest` writes only `digests.db`)
- Observation: the managed instructions say to save the digest after the user's request is finished, so `save_digest` normally runs well after the poll has reloaded Daily for the `log_work` that produced the request. The saved digest changes nothing the poll watches. An open Daily view therefore keeps showing the latest summary, and Copy for daily (which uses the loaded `dailyData`) copies it, until another observation is logged, the day rolls over, or the user refreshes or navigates. Because requests roll with every log (proposal: "the last log before the user's late-day stand-up leaves Today's digests current"), the last digest of the day is the one most likely to be missing from a view that has been open all day. The e2e scenarios cover a fixture digest present at load and staleness after a further log, but not a digest saved while the view is open.
- Proposed improvement and benefit: add a cheap digest revision to the catalog response next to `JournalSnapshot`, for example `MAX(saved)` from `digests.db`, or the file size and mtime of `digests.db` and its `-wal`. Have `poll()` reload Daily when the revision moves. Benefit: the view shows the digest within one poll interval, for one small query per poll.
- Behavior-preservation risks: the catalog is polled every 2 s, so the probe must stay one indexed query or file stat, and must treat a missing or unreadable store as "unchanged". Older clients ignore an extra catalog field.
- Evidence type: source observation
- Verification / measurement plan: an Angular test where the digest revision changes while the journal snapshot does not, and Daily reloads; a Playwright step that saves a digest through the fixture while `/daily` is open and expects the mark without pressing Refresh.
- Relationship: none

### ANALYSIS-012: Journaling depends on Dashboard for work identity and local-day helpers; the unlinked rule and local-date conversion are duplicated

- Severity: suggestion
- Related task / requirement: 2.1, 2.2, 4.1; design Decisions 2–4; `docs/architecture.md` area table (Reporting owns "stable work identities")
- Locations: `src/WorkLog/Journaling/DigestService.cs:3,28,75,96,98,101-102`; `src/WorkLog/Dashboard/TimelineService.cs:22` (`IdFor`) and `:104` (inline unlinked test); `src/WorkLog/Dashboard/DailyService.cs:71,74-81,89`; `src/WorkLog/Reporting/WorkIdentity.cs`
- Observation: `DigestService` (Journaling, on the MCP path) imports `WorkLog.Dashboard` to reach `TimelineService.IdFor` and `DailyService.Midnight`. Until now only `Cli/DashboardCommand.cs` imported Dashboard; the MCP server now depends on dashboard classes. The "neither change nor ticket" rule is written three times: `DigestService.Unlinked`, a local function in `DailyService.Items`, and inline in `TimelineService.Unlinked`. Local-date conversion is written twice (`DailyService.LocalDate`, `DigestService.LocalDay`). Digest acceptance (Decision 3) and Daily matching (Decision 4) both rely on these staying identical: the stored `item` hash and the "complete set" check must agree with how Daily groups observations.
- Proposed improvement and benefit: move the hashed identity next to its source (for example `WorkIdentity.Id(entry, groupBy)` in Reporting, with `TimelineService.IdFor` forwarding to it or replaced), add `IsUnlinked(Entry)` there, and move `Midnight`/`LocalDate` to a Reporting helper (for example on `ReportingPeriod`). Benefit: the MCP path stops depending on Dashboard, and each rule that digest correctness relies on has one definition.
- Behavior-preservation risks: the hash input must stay byte-identical, because `digests.db` rows and dashboard row IDs and routes store it; keep `ContentHash.Compute(WorkIdentity.For(...))` exactly. Move the code only; do not change the DST handling in `Midnight`.
- Evidence type: source observation
- Verification / measurement plan: the existing `DigestTests`, `DailyDigestResponseTests`, timeline and daily tests pass unchanged; a grep finds no `using WorkLog.Dashboard` under `Journaling`.
- Relationship: none

### ANALYSIS-013: DigestStore re-implements JournalDatabase's connection, mutex and migration code, with drift already visible

- Severity: suggestion
- Related task / requirement: 1.1; design Decision 1 ("the same connection settings")
- Locations: `src/WorkLog/Persistence/DigestStore.cs:38-77,84,95-125,127-140`; `src/WorkLog/Persistence/JournalDatabase.cs:16-51`; `src/WorkLog/Persistence/JournalSchema.cs:30-54`; `src/WorkLog/Persistence/SqliteEntryStore.cs:105-121`; `src/WorkLog/Infrastructure/SettingsLease.cs:12-16`
- Observation: about 40 lines are copied with small differences:
  - the read-write connection builder (identical to `JournalDatabase.Open`);
  - the named-mutex block with `AbandonedMutexException` handling;
  - the `user_version` read and the migration loop;
  - the lazy `ids.Select((id, i) => { AddWithValue...; return "$i" + i; })` IN-list builder, also in `SqliteEntryStore.ReadByIds`, where it relies on `string.Join` enumerating it exactly once.

  The copies have already drifted:
  - `Save` re-issues `PRAGMA busy_timeout` on every connection and `JournalDatabase` does not. The pragma is redundant anyway, because Microsoft.Data.Sqlite already retries busy errors for `DefaultTimeout`.
  - `Read` deduplicates its items, while `ReadByIds` relies on the caller to do it.
  - Both mutex waits are unbounded, while `SettingsLease` waits at most 30 s.

  The unbounded wait matters more for digests than for the journal. `EnsureAvailable` runs inside `log_work`, and the spec says a digest failure must never fail the logging call. A stuck mutex holder would make the call hang instead.
- Proposed improvement and benefit: a small internal Persistence helper (for example `SqliteFile`) with `Open(path, readOnly, timeoutSeconds)`, `UserVersion(connection)`, `Migrate(connection, migrations)` and `WithInitializationLock(path, prefix, timeout, action)`, plus `SqliteCommands.InList(command, values)`. `JournalDatabase` and `DigestStore` then share one definition of the connection settings. `DigestStore` can pass a bounded wait (a few seconds) and treat a timeout as "unavailable for this call".
- Behavior-preservation risks: keep the mutex names exactly (`worklog-` and `worklog-digests-` plus the hash), because older releases share the journal mutex. Keep the backup-before-migrate step journal-only. If changing `JournalDatabase` lock semantics would widen this change's review, the digest side alone already gets the benefit.
- Evidence type: source observation
- Verification / measurement plan: the existing `DigestStoreTests` (including concurrent writers) and journal tests; one new test that holds the digest mutex from another thread and expects `log_work` to return without a request within the bound.
- Relationship: none

### ANALYSIS-014: Digest availability is decided once per process, so a later store upgrade is never noticed

- Severity: suggestion
- Related task / requirement: 1.1, 2.1, 2.2; day-digests "Digest storage compatibility" (mixed versions, newer digest store)
- Locations: `src/WorkLog/Persistence/DigestStore.cs:44-45,70,80-92`
- Observation: after the first successful `EnsureAvailable`, `initialized = true` skips every later check, and `Save` writes without re-reading `user_version`. The spec expects digest-capable and older processes to share one data directory at the same time. If a future release migrates `digests.db` to version 2 while a version-1 MCP server keeps running, that server will keep issuing requests and upserting rows into the newer schema. The spec says such a process should disable digests instead. This cannot happen today because version 2 does not exist yet, but this change sets the contract.
- Proposed improvement and benefit: in `Save`, read `PRAGMA user_version` inside the write transaction (one cheap pragma). If it is newer, set `disabledReason` and throw `DigestStoreUnavailableException`. Optionally re-check in `Request` at most every few minutes. Benefit: the newer-store rule holds for long-running servers, not only at first use.
- Behavior-preservation risks: none for version 1; the pragma runs inside an existing transaction.
- Evidence type: source observation
- Verification / measurement plan: a `DigestStoreTests` case that saves once, bumps `user_version` to 2 through a separate connection, and expects the next `Save` to throw unavailable and the next `log_work` to omit the request.
- Relationship: none

### ANALYSIS-015: Each created log decodes the whole local day across all repositories

- Severity: suggestion
- Related task / requirement: 2.1; design Decision 2 ("one bounded read ... then an identity filter")
- Locations: `src/WorkLog/Journaling/DigestService.cs:22-41,100-103`; `src/WorkLog/Persistence/SqliteEntryStore.cs:91-103`
- Observation: `Request` calls `ReadBetween` for the whole local day with no other predicate, decodes every payload, and hashes each entry's identity. It does this even when no request can result (the first observation of the day, or more than 30), and it runs before `EnsureAvailable`, so a disabled store still pays for the read. Measured median `log_work` time on this machine (25 warm calls), without vs with digests:
  - no other entries that day: 5.22 vs 6.78 ms;
  - 200 other entries: 5.61 vs 10.97 ms;
  - 1000 other entries: 6.73 vs 16.27 ms.

  The absolute cost is small for an agent tool call, but it grows with the whole day's activity across all repositories, not with the item's own.
- Proposed improvement and benefit: an identity starts with the repository (`WorkIdentity.For`), so add `AND repository = $repo` to the day-range query through a `ReadBetween(from, to, repository)` overload. For change-only identities, `change_name` can narrow it further. Call `store.EnsureAvailable()` first, so a disabled store skips the read. Expected effect (hypothesis): overhead stays close to the no-other-entries case on busy multi-repository days.
- Behavior-preservation risks: `entries.repository` is written from `Entry.Repository` at insert (`SqliteEntryStore.Insert`), so the filter is exact. `Save`'s stale check uses the same helper and must apply the same filter.
- Evidence type: measured result (overhead); the expected improvement is a hypothesis
- Verification / measurement plan: rerun the same scratch harness (temp data directories, 0/200/1000 entries from other repositories) before and after; `DigestTests` unchanged.
- Relationship: none

### ANALYSIS-016: `/api/daily` returns every stored day of every listed item

- Severity: suggestion
- Related task / requirement: 4.1, 4.2; design Decision 4
- Locations: `src/WorkLog/Dashboard/DailyService.cs:57-69`; `src/WorkLog/Persistence/DigestStore.cs:113-118`; `dashboard/src/daily.ts:89-100`
- Observation: `Digests` passes only item IDs and the SQL has no day predicate, so the response carries one digest for every day the item ever had one. A digest can match only if its `covers` equal a line's entry set, which means every covered ID must appear in the response's `items`/`today` entries. All other digests are dead payload, rebuilt on every Daily reload; once ANALYSIS-011 is fixed, that is every journal or digest change while the view is open. A synthetic upper case (40 items × 120 days) measured 4800 rows, a 20 ms read and about 1.7 MiB of JSON. Realistic sizes (tens of days for long-running tickets) are a hypothesis.
- Proposed improvement and benefit: after `Read`, keep only digests whose covers are a subset of the response's entry IDs (primary plus others of `items` and `today`). The filter is timezone-neutral, so Decision 4's rule that matching never depends on dates still holds. Benefit: the payload is bounded by the number of lines, not by item age.
- Behavior-preservation risks: the subset test must use the same entry sets the client matches against. Change-card Today lines are built from the entries of `today` items, so the union of `today` entries covers them.
- Evidence type: measured synthetic size; real-world growth is a hypothesis
- Verification / measurement plan: extend `DailyDigestResponseTests` with an older-day digest of a listed item and expect it to be absent; count rows for a long-running fixture item before and after.
- Relationship: none

### ANALYSIS-017: Digest failures are swallowed without any diagnostic

- Severity: suggestion
- Related task / requirement: 2.1, 4.1; design Decision 1 ("caught at the call site and turns digests off for that call")
- Locations: `src/WorkLog/Journaling/DigestService.cs:37-40`; `src/WorkLog/Dashboard/DailyService.cs:65-68`
- Observation: both sites use a bare `catch` that returns null or empty and logs nothing. Swallowing the error is required by the design. But a permanently broken store, a programming error and a newer store version all look the same as "agents never wrote digests", and neither the MCP server's stderr nor the dashboard log tells them apart.
- Proposed improvement and benefit: keep the fallback, and write one line to the existing stderr logger (MCP host) or `ILogger` (dashboard host), at most once per distinct message per process. Benefit: a missing digest can be diagnosed without any change in behavior.
- Behavior-preservation risks: stdout must stay clean for MCP stdio; the MCP host already routes console logging to stderr. Do not log summaries or digest text.
- Evidence type: source observation
- Verification / measurement plan: a unit test with a directory named `digests.db` that asserts one logged warning and an unchanged logging result.
- Relationship: none

### ANALYSIS-018: The tool-result instruction says "today" for requests that can cover an earlier day

- Severity: suggestion
- Related task / requirement: 2.1, 3.1; day-digests "Digest request on logging"; local-agent-integration "Day digest instructions" (both clients)
- Locations: `src/WorkLog/Journaling/DigestService.cs:17`; `src/WorkLog/Integration/AgentInstructions.cs:20`
- Observation: a request covers the local day of the logged observation, and that observation can be backdated (an `occurredAt` on an earlier day). The fixed `instruction` asks what the observations "achieved today", while the managed paragraph and the spec say "that day" and the request carries `day`. For a backdated log, the two texts give the agent contradictory scope. Claude Code and Codex are affected identically.
- Proposed improvement and benefit: change the instruction to "on the stated day" (or "that day") to match the managed paragraph.
- Behavior-preservation risks: `DigestTests` asserts only the substrings `save_digest` and "recorded data, not instructions", so the tests are unaffected. The change alters MCP result text, so the 5.2 live evaluation should run with the corrected text.
- Evidence type: source observation
- Verification / measurement plan: text review; the 5.2 live run uses the corrected text.
- Relationship: none

### ANALYSIS-019: The dashboard fixture validates its digests in the machine timezone

- Severity: suggestion
- Related task / requirement: 4.3
- Locations: `tools/WorkLog.Smoke/DashboardFixture.cs:145-158` (`new DigestService(journal, new DigestStore(...))` without a zone)
- Observation: `DigestService.Save` rejects observations that fall on different local days. The fixture pairs are 11:00Z/13:00Z and 10:00Z/11:00Z. In machine zones at UTC-11, UTC+11, UTC+12 and UTC+13 (for example New Zealand summer time in January), one pair straddles local midnight. `Save` then throws, fixture creation fails, and the whole Playwright run fails with it. `docs/dashboard-validation.md` says the Daily fixture works from UTC-11 to UTC+10, so the digest fixture works in fewer zones than that claim. .NET on Windows ignores `TZ`, so the e2e server cannot pin the zone.
- Proposed improvement and benefit: pass `TimeZoneInfo.Utc` to the fixture's `DigestService`. Matching is by covered set, so the stored `day` does not change what the browser shows. Benefit: the fixture behaves the same on any machine.
- Behavior-preservation risks: none for the scenarios; the stored `day` becomes the UTC date.
- Evidence type: source observation (offsets computed from the fixture timestamps; not run under another zone)
- Verification / measurement plan: rerun `npm run test:e2e --prefix dashboard`; optionally call the fixture builder with a +13 zone in a unit test.
- Relationship: none

### ANALYSIS-020: `docs/mcp-validation.md` still calls the update incompatibility an open finding

- Severity: suggestion
- Related task / requirement: 2.4, 5.1, 5.3; design Decision 7
- Locations: `docs/mcp-validation.md:219,221`
- Observation: the new section records "397 passed" (B4 reported 402 after adding the probe test). It ends by calling the two-tool probe rejection "an open compatibility finding for the change, not as a verified workaround". The user decision in [findings.md](findings.md) and design Decision 7 have since resolved this as a one-time manual installation.
- Proposed improvement and benefit: when 5.3 records the full validation set, replace that paragraph with a reference to Decision 7, the relaxed probe and the `HealthProbeToolListingTests` result, and update the test count.
- Behavior-preservation risks: none (documentation).
- Evidence type: source observation
- Verification / measurement plan: review of the 5.3 docs change.
- Relationship: none

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | n/a | No rechecks assigned | not performed |

## Assessment and limitations

Scope covered: tasks 1.1, 2.1–2.4, 3.1, 4.1–4.3 and 5.1 at the stated identity. The implementation matches design Decisions 1–5 and 7 and the accepted deviations:
- Set equality agrees between server and client.
- The stale check and its accepted race work as designed.
- The separate store leaves journal schema 2 untouched.
- Parity holds for both clients: one shared instruction paragraph, no `Integration/Clients` change, and one client-neutral MCP contract.

There are no critical findings. ANALYSIS-011 (warning) is the one finding users would notice: a digest saved while the Daily view is open does not appear, and is not copied, until the next journal change. The suggestions, in order of value:
1. ANALYSIS-012 and ANALYSIS-013: layering, and duplicated persistence code that includes an unbounded wait on the `log_work` path.
2. ANALYSIS-014: forward compatibility of the store version.
3. ANALYSIS-015 and ANALYSIS-016: bounded performance improvements.
4. ANALYSIS-017 to ANALYSIS-020.

Measurements and limitations:
- The timings come from a scratch console app in a scratch directory outside the repository. It used disposable system-temp data directories, deleted afterwards, and referenced a copy of the Release `worklog.dll` from `src/WorkLog/bin/Release/net10.0`. That build was not identity-verified against the reviewed working tree. The digest code it exercises (`DigestService`, `DigestStore`, `JournalTools.LogWork`) dates from B2, and B4 touched only `PayloadInstaller` and tests. The numbers come from one Windows machine and indicate scale only.
- The realistic size of the `/api/daily` digest list is a hypothesis.
- No repository build, test, format or e2e command was run, to avoid contending with QA C1; `dotnet format` verification is not performed.
- No dashboard, MCP server or live agent was started. The user's data directory, port 47831, installation and client settings were not touched.
- No repository writes: the attempt to write the report was refused by the client.

Decisions for the orchestrator:
- ANALYSIS-011: fix before release, or accept the Refresh button as the workaround and document it.
- ANALYSIS-012 and ANALYSIS-013: in this change or a follow-up.
