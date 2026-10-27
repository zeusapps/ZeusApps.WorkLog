<!--
Saved by the orchestrator (fallback per .agents/workflows/openspec-delegation.md, Reports and evidence).
- Reviewer: code-analyzer subagent, assignment A3
- Client / version / mode: Claude Code 2.1.311, project subagent, auto mode
- Model: claude-opus-5-5 (role default)
- Case: refused write. The subagent attempted the write of openspec/changes/fix-dashboard-reliability/reports/analysis-001.md; exact refusal message: "Subagents should return findings as text, not write report files. Include this content in your final response instead."
- Saved verbatim from the subagent's final response on 2026-10-27 by orchestrator session eae5a20b-6b19-45bd-b040-24cfe844c709. Only this header was added; the harness indentation of the hand-back was removed.
-->

# Analysis report: fix-dashboard-reliability / round 1

## Review target

- Reviewer / role: assignment A3 subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.311 / subagent launched by the orchestrator, auto mode / `claude-opus-5-5`
- Date: 2026-10-27T14:25Z
- Change root / assignment: `openspec/changes/fix-dashboard-reliability` / A3
- Task and requirement references: tasks 1.1–1.5, 2.1–2.2, 4.1–4.3, 5.1–5.6 ([tasks.md](../tasks.md)); [design.md](../design.md) Decisions 1–8; [work-dashboard delta](../specs/work-dashboard/spec.md) (Idle refresh cost, Bounded dashboard preferences, Ongoing work and history navigation); [openspec-browser delta](../specs/openspec-browser/spec.md) (Scoped document search). The four developer deviations are listed under "Scope decisions" in [findings.md](findings.md).
- Scope and relevant neighboring paths: `src/WorkLog/Dashboard/` (DashboardHost, DashboardJournal, DashboardState, DocumentService, ReviewMarker, SourceCatalog, plus the unchanged neighbors LocalCommand and SourcePath); `tests/WorkLog.Tests/Dashboard/DashboardReliabilityTests.cs`; `dashboard/src/` (api.ts, app.ts, app.html, models.ts, range.ts, app.reliability.spec.ts, and the app.spec.ts diff); `dashboard/e2e/dashboard.spec.ts` (diff stat only); `docs/dashboard-validation.md`.
- Reading limits and paths read outside scope: no reading limits were set. Paths read outside the listed scope: `src/WorkLog/Configuration/LocalSettings.cs`, `src/WorkLog/Repositories/RepositoryRegistry.cs` (when `config.json` is rewritten), `src/WorkLog/Persistence/JournalSchema.cs` (first 40 lines), grep hits in `src/WorkLog/Journaling/Journal.cs` and `src/WorkLog/WorkLog.csproj`, `Directory.Build.props`, `.gitignore`, this change's `reports/findings.md` (read only), and grep hits in `docs/handoffs/2026-10-27-dashboard-follow-ups.md`.
- Baseline commit: `bf65aeb` on branch `change/fix-dashboard-reliability`.
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline bf65aeb -Paths src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard/DashboardReliabilityTests.cs dashboard/src dashboard/e2e docs/dashboard-validation.md` gave HEAD `bf65aeb9c89f…` with 18 manifest entries (4 untracked: `app.reliability.spec.ts`, `range.spec.ts`, `range.ts`, `DashboardReliabilityTests.cs`; 14 modified), tracked diff SHA-256 `73aa0085eaa0…`, and summary `head=bf65aeb baseline=bf65aeb files=18 diff=73aa0085eaa0 untracked=4/ee2ea065ab53`.
- Before/after identity: the same command after the analysis printed `head=bf65aeb baseline=bf65aeb files=18 diff=73aa0085eaa0 untracked=4/ee2ea065ab53`. It is identical, and the analysis made no unexpected changes. `git status` shows no entries outside the expected set.
- Prior reports / rechecked findings: none for this change. This is not a recheck.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Performance: catalog cheap tier | In-process benchmark of `SourceCatalog.GetAsync` with a fake `LocalCommand` and a fake clock (advanced 3 s per request so the 2 s response cache is bypassed and the 60 s tier is not). It ran against a copy of the Release `worklog.dll` built after the last source edit (SHA-256 matched the copy in `src/WorkLog/bin/Release`). No dashboard and no port were used. The harness and output are in ignored `artifacts/analysis-fdr/bench/` and `bench-run-3.txt`. | Fixture with 60 Markdown files: 23 ms median per refresh. Fixture with 1,280 files (20 active and 300 archived changes, 4 files each): 746 ms median (p90 845 ms). Recursive enumeration through `SourcePath.MarkdownFiles` plus stat: 653 ms. Plain `Directory.EnumerateFiles` plus stat over the same files: 23 ms. No spawns in 15 cheap requests. | Measured. Leads to ANALYSIS-001. The fixture paths have 16 components, more than a typical checkout. Canonicalization cost grows with path depth, so real checkouts are probably cheaper per file. |
| Performance: journal snapshot | Same harness: `DashboardJournal.Snapshot()` on a 20,480-row journal | Median 0.4–1.0 ms (p90 ≤ 1.3 ms), using a new connection with pooling off. `Repositories()` (a DISTINCT scan) took 8–11 ms and runs only when the snapshot moves. A full `Read` decode took 150–160 ms. | Measured. The `MAX(rowid)` read per catalog request is negligible. No finding. |
| Performance: unchanged-document poll | Same harness: `DocumentService.ReadAsync` with `knownRevision` | 0.3–0.8 ms median server time, and `DocumentUnchanged` is returned | Measured. Task 5.6 works as designed. |
| Performance: search cache (Decision 7) | Same harness: two consecutive `scope=all` searches over unchanged files | With 1,280 documents, a query with many matches took 0 extra hash reads (it returns at 50 matches). A query with no matches took +1,179 and then +1,280 hash reads, about 0.9 s each. At 60 documents: 0 extra reads. | Measured. Leads to ANALYSIS-003. |
| Performance: CLI timeout handling (deviation 2) | Same harness: a fake `openspec context` that always returns the timeout result `(true, -1)` | 10 `context` spawns over 10 catalog refreshes (30 s simulated) | Measured with a fake, not wall time. Leads to ANALYSIS-002. |
| Performance: expensive-tier refresh latency | Source inspection of `SourceCatalog.GetAsync` and `DiscoverAsync`, using the CLI durations in design.md | The gate is held across the parallel `context` phase and then the parallel `list` phase | Hypothesis for wall time. Leads to ANALYSIS-004. |
| Architecture: unchanged-document protocol (deviation 4) | Source inspection of `DocumentService.ReadAsync` and the `app.ts` poll | The server compares only the text revision, and the client cross-checks `archived` against the catalog | Leads to ANALYSIS-005. |
| Duplication: notices (deviation 3) | Source inspection | Parallel `Notices` and `SourceNotices` lists, and the "no available OpenSpec source" rule appears in two classes | Leads to ANALYSIS-006. |
| Unbounded state | Source inspection of every cache and map in scope | `SourceCatalog.commands` is never pruned. `DocumentService.cache` is bounded by entry count (256), not by bytes. Client `positions`/`documents` are capped at 100/200. The client `drafts` map is per-visit and not capped (it predates this change). | Leads to ANALYSIS-007. The other items are in the table below. |
| Design quality: config reload (deviation 1) | Source inspection of `SourceCatalog.cs:97-105` and of `LocalSettings`/`RepositoryRegistry` write paths | `config.json` is rewritten only when its content changes (`RepositoryRegistry.GetContext` compares serialized settings), so the stamp does not churn on every log. A reload failure notice lasts for one snapshot only. | The reload approach is sound. ANALYSIS-008 covers the transient notice. |
| Duplication: `MarkdownFence` | Source inspection of `ReviewMarker.cs:66-87` and `DocumentService.cs:213-216` | One struct now serves review markers and search section labels, and the client parser is separate (as it was before) | Good consolidation. No finding. |
| Range and boundary code | Source inspection of `range.ts` and `api.ts:47-90` | `boundary()` binary-searches a 48 h window (about 18 `Intl` formats) only when the first candidate falls on the previous local date. `touchRecent` is O(limit) per call. Keys contain `:` and are never array-index-like, so insertion order holds. | No cost or complexity concern. No finding. |
| Client poll (Decision 6) | Source inspection of `app.ts:1136-1180` | Each poll makes 1 catalog request, plus 1 document request when a document is open. Search runs on a revision change. `refreshData` runs on a snapshot change or day rollover. `persist()` skips identical bodies, and the poll never calls `remember()`. | Matches Decision 6 and the developer's 60 requests per minute. See the steady-state table. |
| Formatting / tests | Not run | — | QA scope. The analyzer did not rerun `dotnet test`, `npm test`, or `dotnet format`. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Client `setInterval(poll, 2000)` (`app.ts:252`); server `SourceCatalog.GetAsync` with its 2 s response cache and 60 s expensive tier | Measured in process (server per-refresh cost); source observation (rates); developer-reported fixture figures, not remeasured | **Per 2 s poll, idle, document open:** `GET /api/catalog` plus `POST /api/document` with `knownRevision` returning 96 bytes. That makes 60 requests per minute, and the code matches the developer's figure. There are no search, timeline, or `PUT /api/state` requests, consistent with the developer's 0 / 0 / 0 (source-confirmed: the poll calls `search()` only on a revision change, `refreshData()` only on a snapshot change or day rollover, and never calls `remember()`). **Per server refresh (at most every 2 s, shared by all tabs):** 1 SQLite open plus `PRAGMA user_version` plus `MAX(rowid)` (measured 0.4–1.0 ms); 1 stat of `config.json`; 3 stats per checkout for `context` inputs; directory enumeration of every active and archived change; a canonicalized path walk and stat of every Markdown file (measured 23 ms at 60 files, 746 ms at 1,280 files; ANALYSIS-001); and SHA-256 of the stamp strings. No file contents are read or hashed. **Per 60 s:** one expensive round, made of `git worktree list` per configured context, 1 `git rev-parse`, `openspec context` per checkout, and `openspec list` per source. For the developer's fixture that is about 4 git and about 8 OpenSpec spawns, matching the reported 4 and 8 (source-derived; I did not reproduce the counts). **Input change rate:** documents and the journal change only while an agent works. The archive changes on archive moves only, yet it is re-enumerated and re-canonicalized on every refresh (ANALYSIS-001). **The unchanged document request** costs 0.3–0.8 ms on the server (measured), so skipping it when `catalog.revision` is unchanged would save requests but little work. That is not raised as a finding. |
| Cache lifetime versus interval and refresh duration | 2 s catalog response cache; 60 s CLI cache (`ExpensiveLifetime`); 256-entry document cache; client `searchRevision` and `journalSnapshot` | Source observation; measured for the document cache | **The 2 s response cache equals the 2 s poll interval.** `refreshed` is stamped after discovery completes, so whether the next poll hits the cache depends on timing jitter and on discovery duration. With a slow cheap tier (hundreds of ms) every other poll gets the previous snapshot, so freshness is up to about 4 s and the refresh rate is 15–30 per minute (hypothesis; this stays within the spec's "within a few seconds"). **The 60 s CLI cache** gives full relief while idle (0 spawns in 15 cheap requests, measured). However, all entries are stamped in the same round, so they expire together, and the round runs inline under the gate (ANALYSIS-004). Timed-out results are never cached, so a hanging CLI gets no relief at all (ANALYSIS-002). **The document cache** gives relief for scopes of 256 documents or fewer. Above that, a sparse search misses on every document every time because eviction is FIFO by insertion order (`Order` is not refreshed on a hit) (ANALYSIS-003, measured). **Client revision gating** works: search and History are re-queried only when their inputs change. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `SourceCatalog.commands`; `DocumentService.cache`; client `preferences.positions` and `documents`; client `drafts`; `dashboard-state.json`; archived changes on disk | Source observation | **`SourceCatalog.commands`** (`SourceCatalog.cs:29`) is keyed by checkout path and source ID, has no eviction, and grows with worktree churn over the life of the process (ANALYSIS-007; small). **`DocumentService.cache`** is bounded at 256 entries and evicts the oldest-inserted entry. It is not bounded by bytes: the theoretical worst case is 256 × 2 MiB files held as UTF-16 strings (hypothesis about 1 GiB; with realistic 5–20 KiB documents, about 2–10 MiB). **`positions` 100 and `documents` 200** are most-recently-used capped (Decision 2). The file stays far below the 64 KiB server cap, and an oversize payload now gets 413 with one notice. **`drafts`** (`app.ts:612-619`) stores an entry, possibly empty, for every document visited in a tab. It predates this change, lives in memory, and is cleared on reload. Not raised. **Archived changes** only grow, and the per-refresh cheap-tier cost grows linearly with them (ANALYSIS-001). The journal grows by design, and its per-refresh read is O(log n). |
| Repeated full reads where a change check would do | Catalog cheap tier; search; History refresh | Measured / source observation | **Cheap tier:** reads no file contents, which is good, but each refresh re-walks and re-canonicalizes every Markdown path, including the archive. That walk is 96% of the measured refresh cost (653 of 746 ms at 1,280 files), while plain enumeration with stat costs 23 ms (ANALYSIS-001). **Search** re-reads and re-hashes every file in scope when the scope exceeds 256 documents (ANALYSIS-003). Even fully cached, each document pays `Resolve` → `SourcePath.Within` canonicalization plus the O(changes) scan in `SourceCatalog.Resolve`: 22–44 ms for 60 documents (measured). That predates this change. **History** re-reads and decodes the whole journal up to `to` (150–160 ms at 20,480 rows, measured), twice per journal change (`loadHistory` plus `loadOngoing`). This happens only on a change now, and design.md lists it as a non-goal. Not raised. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | `LocalCommand.RunAsync` for `git`, `node`, and `openspec`; the SQLite journal | Source observation | **`git`** is resolved by `Process.Start` from the executable name (with `UseShellExecute = false`, the Windows `CreateProcess` search order; PATH on other platforms). **`openspec` on Windows** looks only for `<PATH entry>\node_modules\@fission-ai\openspec\bin\openspec.js` and runs it with `node` from PATH. That covers npm's global prefix, and layouts where `node_modules` sits beside `node.exe` (nvm-windows, fnm). It does not cover pnpm, Volta, Yarn global, or bun shims, which make the CLI "unavailable" and fall back to treating the checkout as the root (predates this change; not raised as a new finding). **On other platforms** `openspec` is resolved from PATH. **Timeouts:** every spawn has a 15 s timeout and kills the process tree. With `Parallelism = 4`, the worst case for a hung CLI is ceil(checkouts / 4) × 15 s for `context` plus ceil(sources / 4) × 15 s for `list`, under the gate. Because a timeout is not cached, that repeats on every refresh (ANALYSIS-002). SQLite connections use `DefaultTimeout = 5`. File I/O is local and has no timeout. |

## Findings

### ANALYSIS-001: Every catalog refresh re-canonicalizes every Markdown path, including the whole archive

- Severity: warning
- Related task / requirement: 5.1, 5.2; Decision 5 (cheap tier and its risk note "Stat of every Markdown file … The analyzer measures it during review"); work-dashboard "Idle refresh cost"
- Locations: `src/WorkLog/Dashboard/SourceCatalog.cs:154-177` and `:256-263` (`AddChange`); `src/WorkLog/Dashboard/SourcePath.cs:7-19` (`Canonical`), `:21-31` (`Within`), and `:33-49` (`MarkdownFiles`). The source is the working tree at identity `diff=73aa0085eaa0` over `bf65aeb`.
- Observation: The cheap tier runs on every catalog refresh (at most every 2 s, and in practice every 2–4 s while a tab is visible). For every change directory, active and archived, `SourcePath.MarkdownFiles` calls `Within(root, …)` for each directory and for each file. `Within` runs `Canonical` twice, once on the root and once on the candidate. Each run walks every path component with `Directory.Exists` and a `LinkTarget` lookup. The root is therefore re-canonicalized for every file, and each file costs about 2 × (path depth) filesystem calls before the added `FileStamp` stat. Measured in process:
  - 60 files: 23 ms per refresh.
  - 1,280 files: 746 ms median per refresh. The canonicalizing walk is 653 ms of that; plain recursive enumeration with the same stat is 23 ms.

  The walk predates this change. It used to be hidden behind the CLI spawns and is now the dominant idle cost. Archived changes are effectively immutable but only accumulate, so the cost grows without bound. At 15–30 refreshes per minute, a large archive keeps one core busy 19–37% of the time while idle (derived from the measured per-refresh time and the source-derived rate), and the work runs under `gate`, so comment saves and search wait behind it. The design's premise that this is "still far cheaper than one CLI spawn" holds at this repository's size (84 Markdown files, about 30 ms extrapolated) but not at about 1,000 files or more across sources.
- Proposed improvement and benefit: keep the escape check but make it proportional.
  1. Canonicalize `source.Root` once per discovery and pass it in, instead of recomputing it per file.
  2. Enumerate with `FileSystemEnumerable` or `EnumerationOptions`, which supply attributes, length, and last-write time from the directory entry without an extra stat. Fully canonicalize only entries whose attributes include `ReparsePoint`, and treat other descendants of a canonical directory as canonical.
  3. Optionally, refresh archived changes on the 60 s tier, or when the archive directory's entry set changes. Active changes keep the per-request stamp.

  Expected result: the per-refresh cost approaches the plain-enumeration figure (about 23 ms at 1,280 files), roughly a 30× reduction.
- Behavior-preservation risks: `SourcePath.Within` is the defense against symlinks and junctions that escape the source, so the reparse-point path must still reject escapes, including a junction placed at an intermediate directory level. Document order, `ContentStamp`, and `TaskStamp` must stay byte-identical, or the revision changes once after upgrade (harmless, but note it). Option 3 delays reflection of edits to archived documents by up to 60 s.
- Evidence type: measured result (in-process harness, fake CLI, Release build). The CPU share is derived.
- Verification / measurement plan: rerun `artifacts/analysis-fdr/bench` (or an equivalent opt-in test) at 60 and 1,280 files and compare the cheap-tier median and the `MarkdownFiles` share. Keep the existing `SourcePath` link-escape tests, and add one with a junction or symlink at a nested directory below a change.
- Relationship: new. Answers the measurement the design asked for under "Risks / Trade-offs".

### ANALYSIS-002: A CLI that times out is spawned again on every refresh, and each refresh then blocks 15 s under the gate

- Severity: warning
- Related task / requirement: 5.1; Decision 5; developer deviation 2 ("timed-out commands are not cached"); work-dashboard "Idle refresh cost" (Idle dashboard: "at most one round of OpenSpec CLI resolution per source")
- Locations: `src/WorkLog/Dashboard/SourceCatalog.cs:51-60` (`CachedAsync`, line 57); `src/WorkLog/Dashboard/LocalCommand.cs` (15 s timeout); `SourceCatalog.cs:38-49` (the gate)
- Observation: when `openspec` or `git` hangs (a slow `npx` shim, a locked store, or a network path), `CachedAsync` removes the entry, so the next refresh spawns again. The harness measured 10 `context` spawns over 10 refreshes. In real time each refresh waits the full 15 s for that phase while holding `gate`. The catalog, document poll, search, and comment requests therefore all queue for 15–30 s. Discovery repeats as soon as the client polls again, so the dashboard keeps a hung process alive per affected checkout almost continuously. The behavior is the same as at baseline (no cache at all), but it leaves the requirement's "at most one round per minute" unbounded in this failure mode, and Decision 5 was meant to bound exactly that.
- Proposed improvement and benefit: cache the timeout result with a shorter lifetime, for example 15–30 s or an exponential backoff capped at `ExpensiveLifetime`, keyed and stamped as today. Keep surfacing the timeout as the source error or notice, so the user sees why the source is unavailable. This bounds spawns to one per backoff window and removes most of the 15 s stalls.
- Behavior-preservation risks: a transient hang now delays recovery by up to the backoff period instead of the next poll. A change to the input stamp (such as a `config.yaml` edit) should still bypass the negative entry, which happens naturally with the current key/stamp comparison.
- Evidence type: measured with a fake command (counts). Wall-clock stall duration is a source observation based on the 15 s timeout.
- Verification / measurement plan: a unit test with a fake `LocalCommand` returning `(true, -1, …)` and the test clock. Assert one spawn across refreshes inside the backoff window, and one more after it.
- Relationship: new. Assesses deviation 2.

### ANALYSIS-003: The 256-entry FIFO cache misses on every document when a search scope is larger than the cache

- Severity: suggestion
- Related task / requirement: 5.4; Decision 7; openspec-browser "Scoped document search"
- Locations: `src/WorkLog/Dashboard/DocumentService.cs:25-28`, `:59-60`, `:67-73` (`Order` is set only on insert; eviction uses `MinBy(Order)`); `:203-227` (the search loop)
- Observation: `Order` records insertion, not use, so eviction is FIFO. A search that reads N > 256 documents in a fixed order evicts each entry before its next use. Measured at 1,280 documents with a query that matched nothing: +1,179 and then +1,280 hash reads over unchanged files, about 0.9 s per search. The 5.4 test covers only two documents plus a direct-read loop, so this case is untested. Searches now re-run only when `catalog.revision` changes. However, while an agent edits any Markdown file in any source, the revision changes almost every poll, so a dashboard with search text and `scope = all` re-reads and re-hashes the whole scope every few seconds. Queries that reach the 50-match limit stop early and are unaffected (measured +0).
- Proposed improvement and benefit: either bound the cache by bytes (for example 32–64 MiB) instead of 256 entries, so typical repositories fit fully; or keep a lightweight search-only text cache keyed by path and the catalog's existing length/mtime stamp. Either way, refresh `Order` on a hit so the open document is not evicted by a scan. This gives 0 re-reads for unchanged files in realistic scopes, which is the intent of Decision 7.
- Behavior-preservation risks: memory use rises up to the chosen byte budget. Revisions must still come from content hashes for comment conflict checks; the write paths use `fresh: true` and are unaffected.
- Evidence type: measured result (in-process harness).
- Verification / measurement plan: extend the 5.4 test, or the harness, to more than 256 documents and a query with no matches. Assert that a second search over unchanged files does not increase `HashReads`.
- Relationship: new.

### ANALYSIS-004: The expensive-tier round runs inline under the catalog gate, so every request stalls once a minute

- Severity: suggestion
- Related task / requirement: 5.1; Decision 5; the proposal's motivation ("delays document refresh and comment saves")
- Locations: `src/WorkLog/Dashboard/SourceCatalog.cs:38-49` (gate plus inline `DiscoverAsync`), `:109-110`, `:124-125`, `:140`, `:180-186` (the three expensive phases); `src/WorkLog/Dashboard/DocumentService.cs:123`, `:151` (comment paths call `catalog.GetAsync`)
- Observation: all CLI entries are created in one round, so they expire together at 60 s. The next request runs `worktree` (parallel), then `rev-parse`, then `context` (parallel), then `list` (parallel), all while holding `gate`. Using design.md's timings (about 1.0 s for `context` and 0.9 s for `list`), the catalog poll, document poll, search, and any comment save made in that window wait about 2 s once a minute, longer with more than 4 checkouts. Idle cost is bounded; the issue is latency.
- Proposed improvement and benefit: stale-while-revalidate. When only expensive entries have expired, return the cheap-tier snapshot built from the previous CLI results, and start one background refresh, de-duplicated by a single in-flight task. Alternatively, stagger entry lifetimes. Comment saves and polls then never wait on CLI spawns.
- Behavior-preservation risks: the catalog may show CLI-derived data (task counts, roots) up to one refresh round older. A concurrency test is needed so a background refresh and an incoming request cannot both publish. `force: true` must remain synchronous for tests.
- Evidence type: performance hypothesis (wall time not measured with the real CLI); the structure is a source observation.
- Verification / measurement plan: with a fake `LocalCommand` that delays 1 s, measure `GetAsync` latency at t = 61 s before and after the change.
- Relationship: new.

### ANALYSIS-005: The unchanged-document answer compares only the text, so the client has to infer archive moves from the catalog

- Severity: suggestion
- Related task / requirement: 5.6; Decision 6; developer deviation 4
- Locations: `src/WorkLog/Dashboard/DocumentService.cs:9`, `:99-106`; `dashboard/src/app.ts:1165-1167`, `:648`, `:655-660`
- Observation: `ReadAsync` returns `DocumentUnchanged` when the SHA-256 of the file text matches, although `Archived`, `CanComment` and `Location` can change without a text change. The client therefore omits `knownRevision` when exactly one catalog candidate has a different `archived` value. That puts part of the server's cache-validity rule into the client, depends on candidate matching (with zero or several candidates the check is skipped), and leaves the older "same revision" branch at `app.ts:657-660` reachable only through this path.
- Proposed improvement and benefit: make the validity check server-side. Either compare a token that covers `(revision, archived, canComment, location)`, or include `archived` and `canComment` in `DocumentUnchanged` so the client updates the flags without text. The client then always sends `knownRevision`, the catalog cross-check goes away, and the API stays backward compatible (an old client never sends `knownRevision`).
- Behavior-preservation risks: an opaque token changes what `knownRevision` contains, and the e2e and Angular tests assert on `revision`. Keeping the field as the text revision and adding the flags to the unchanged answer avoids that.
- Evidence type: source observation.
- Verification / measurement plan: a .NET test that archives a change with identical text and checks the unchanged answer carries `archived: true` and `canComment: false`. An Angular test that the poll updates the read-only state without re-rendering.
- Relationship: new. Assesses deviation 4.

### ANALYSIS-006: Notices are built twice, and the "no source" rule is duplicated between the catalog and search

- Severity: suggestion
- Related task / requirement: 4.1; Decision 4; developer deviation 3
- Locations: `src/WorkLog/Dashboard/SourceCatalog.cs:18-20` (`[JsonIgnore] SourceNotices`), `:151`, `:174-175`, `:269-270`; `src/WorkLog/Dashboard/DocumentService.cs:198-202`
- Observation: each source-bound problem is added twice, as a string to `notices` and as a `SourceNotice` with the same text. `SearchAsync` then recomputes "repository has no source" with the same message literal that `SourceCatalog.cs:151` already produces. If either text or predicate changes in one place only, the catalog banner and the search incompleteness notice diverge.
- Proposed improvement and benefit: keep one typed list in the snapshot, for example `Notice(string Text, string? SourceId, string? RepositoryId)`. Project it to `string[] Notices` for the API, and let search filter it by `SourceId`/`RepositoryId` scope. That removes the parallel list, the duplicate predicate, and the duplicate literal, and makes Decision 4(a)–(c) plus deviation 3 one filter.
- Behavior-preservation risks: the `/api/catalog` `notices` order and text must stay the same. Search `unavailable` ordering (repositories, then errored sources, then source notices, then per-document failures) should be preserved or deliberately changed with the test updated.
- Evidence type: source observation.
- Verification / measurement plan: the existing scoped-search test (`SearchReportsOnlyUnavailableSourcesWithinTheRequestedRepositories`) and the catalog notice tests pass unchanged.
- Relationship: new. Assesses deviation 3.

### ANALYSIS-007: The CLI result cache keeps entries for checkouts and sources that are gone

- Severity: suggestion
- Related task / requirement: 5.1; Decision 5
- Locations: `src/WorkLog/Dashboard/SourceCatalog.cs:29`, `:51-60`
- Observation: `commands` is keyed by `worktree|`, `launch|`, `context|<checkout>` and `list|<sourceId>`, and is never pruned. A long-running dashboard with worktree churn (one per branch, created and removed) keeps every `openspec context` and `openspec list` output it ever saw. The growth is small per entry but has no bound over the process lifetime.
- Proposed improvement and benefit: at the end of `DiscoverAsync`, remove keys not used in this round. Alternatively, build a new dictionary each round and carry over only the entries that were used. This bounds the cache to the current topology.
- Behavior-preservation risks: a checkout that disappears and reappears within 60 s triggers one extra CLI round. That is negligible.
- Evidence type: source observation.
- Verification / measurement plan: a unit test that removes a worktree line from the fake `git worktree list` output and asserts the cache key count drops (needs an internal count accessor, or verification through spawn counts on reappearance).
- Relationship: new.

### ANALYSIS-008: A failed `config.json` reload is reported for only one snapshot, while the stale settings stay in use

- Severity: suggestion
- Related task / requirement: 5.1; Decision 5(b); developer deviation 1 (addresses handoff ANALYSIS-009)
- Locations: `src/WorkLog/Dashboard/SourceCatalog.cs:97-105`
- Observation: when the reload throws, the notice is added to that discovery's `notices`, but `configStamp` is still advanced to the new stamp. Later discoveries see no stamp change, so they neither retry nor repeat the notice. The message is visible in one catalog response (2–4 s) and then disappears, while the dashboard keeps using the previous repository contexts without telling the user. Apart from this, the reload design is sound and cheap: one stat per refresh, and `config.json` is rewritten only when its content changes (`RepositoryRegistry.GetContext`).
- Proposed improvement and benefit: keep the last reload error in a field, and add it to `notices` on every discovery until a reload succeeds. Optionally, leave `configStamp` unchanged on failure so the next refresh retries; the read is cheap.
- Behavior-preservation risks: if retrying every refresh while `config.json` stays invalid, the parse is repeated every 2–4 s (small). The notice wording is user-visible.
- Evidence type: source observation.
- Verification / measurement plan: a unit test that writes invalid JSON to `config.json`, then asserts the notice is present on two consecutive refreshes 3 s apart (test clock), and absent after a valid write.
- Relationship: new. Assesses deviation 1.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| — | — | Not a recheck assignment | not performed |

## Assessment and limitations

- **Coverage.** All key areas named in the assignment were analyzed: catalog tiers and stamps, parallelism and the 15 s timeout, `journalSnapshot`, the client poll, `knownRevision`/`unchanged`, the 256-entry eviction, bounded preference maps and debounce, `MarkdownFence`, range and boundary code, and the four deviations.
- **Decisions 1–8 as implemented.** They are structurally sound, and the idle path is now proportional to change on the client and in CLI use. These parts are good and need no action: the shared `MarkdownFence`, `range.ts`, the preference caps, the `persist()` deduplication, revision- and snapshot-gated refresh, and the unchanged-document answer.
- **The developer's idle figures.** I did not remeasure them. They are consistent with the source:
  - 60 requests per minute = 30 polls × (catalog + document).
  - 2,880 bytes = 30 × 96-byte unchanged answers.
  - 0 preference writes.
  - About 8 OpenSpec and about 4 git spawns = one expensive round per 60 s for the fixture topology.
- **Remaining per-request cost.** The `MAX(rowid)` read is negligible (≤ 1 ms, measured). The unchanged-document request is cheap (≤ 0.8 ms server time, measured). The real remaining cost is the cheap tier's per-file path canonicalization (ANALYSIS-001), which scales with archive size and reached 0.75 s per refresh at 1,280 files.
- **Recommended priority.**
  1. ANALYSIS-001: the largest idle cost, and it grows over time.
  2. ANALYSIS-002: the failure-mode bound in the spec.
  3. ANALYSIS-003 and ANALYSIS-004: latency and churn while agents edit.
  4. ANALYSIS-005 to ANALYSIS-008: simplifications and small correctness polish of the deviations.
- **Limitations.**
  - No dashboard process, port, browser, or real CLI was used.
  - Wall-clock CLI latencies are taken from design.md.
  - The fixture's path depth (16 components) is greater than typical, so the absolute ANALYSIS-001 figures are pessimistic for shallow checkouts. The ratio of the canonicalizing walk to plain enumeration (about 28×) is the robust result.
  - I did not rerun the repository test suites or `dotnet format`; QA covers those.
- **Writes.** The client refused the write to the reserved report path, so this report exists only as this inline text. Disposable files were written under ignored `artifacts/analysis-fdr/`: a binary snapshot, the harness, fixtures under `run/`, and `bench-run-*.txt`. Nothing else was written, and the review identity is unchanged.
- **Decisions needed from the orchestrator.** Whether ANALYSIS-001 and ANALYSIS-002 are fixed in this change, given that they predate it but undercut the "Idle refresh cost" requirement at scale or under failure, or are recorded as follow-ups.

---

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.311 (subagent, auto mode) / claude-opus-5-5
task / finding IDs: tasks 1.1-1.5, 2.1-2.2, 4.1-4.3, 5.1-5.6 and deviations 1-4; new findings ANALYSIS-001 (warning), ANALYSIS-002 (warning), ANALYSIS-003 to ANALYSIS-008 (suggestions); ANALYSIS-009 to ANALYSIS-040 unused
changed or report paths: reserved D:\Code\ZeusApps.WorkLog\openspec\changes\fix-dashboard-reliability\reports\analysis-001.md was NOT written (write refused; report is inline above); disposable ignored evidence in D:\Code\ZeusApps.WorkLog\artifacts\analysis-fdr\ (snapshot\, bench\, run\, bench-run-1.txt, bench-run-2.txt, bench-run-3.txt)
checks: in-process benchmark (dotnet artifacts/analysis-fdr/bench/out/WorkLog.Tests.dll, fake CLI, copied Release worklog.dll) -> passed (results are in the report); dotnet test / npm test / dotnet format -> not run (QA scope)
target identity: before head=bf65aeb baseline=bf65aeb files=18 diff=73aa0085eaa0 untracked=4/ee2ea065ab53; after head=bf65aeb baseline=bf65aeb files=18 diff=73aa0085eaa0 untracked=4/ee2ea065ab53 (identical)
limitations / decisions needed: the report write was refused by the client with "Subagents should return findings as text, not write report files. Include this content in your final response instead."; I did not retry, so please save the inline text to the reserved path. No probe dashboard, port, real CLI, or user data was used, and no process was left running. Decision needed: fix ANALYSIS-001 and ANALYSIS-002 in this change, or record them as follow-ups (both predate this change).
next: orchestrator saves the report, triages ANALYSIS-001 to ANALYSIS-008 in findings.md, and assigns any corrections to the developer.
```
