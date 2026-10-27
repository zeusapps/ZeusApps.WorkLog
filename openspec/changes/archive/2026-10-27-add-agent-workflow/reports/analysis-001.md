<!--
Saved by the orchestrator (fallback per .agents/workflows/openspec-delegation.md, Reports and evidence).
- Reviewer: code-analyzer subagent, assignment A2
- Client / version / mode: Claude Code 2.1.311, project subagent, auto mode
- Model: claude-opus-5-5 (role default)
- Refused operation: Write tool on openspec/changes/add-agent-workflow/reports/analysis-001.md
- Exact refusal message: "Subagents should return findings as text, not write report files. Include this content in your final response instead."
- Saved verbatim from the subagent's final response on 2026-10-27 by orchestrator session eae5a20b-6b19-45bd-b040-24cfe844c709. Only this header was added.
-->

# Analysis report: add-agent-workflow / analysis-001 (calibration review of the v0.3.0 dashboard)

## Review target

- Reviewer / role: code-analyzer subagent (assignment A2) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.311, subagent of orchestrator session eae5a20b-6b19-45bd-b040-24cfe844c709, auto mode; model `claude-opus-5-5` (role default, no override)
- Date: 2026-10-27T10:44:47Z (start) to 2026-10-27T10:56:54Z (end)
- Change root / assignment: `openspec/changes/add-agent-workflow`, assignment A2 (calibration code analysis)
- Task and requirement references:
  - Task 5.2 of this change.
  - Capabilities `work-dashboard`, `openspec-browser`, `document-feedback` (main specs under `openspec/specs/`).
  - Archived change `openspec/changes/archive/2026-10-26-add-work-dashboard` (design.md; tasks.md 3.4, 3.5, 4.4).
- Scope and relevant neighboring paths:
  - `src/WorkLog/Dashboard/*`, `src/WorkLog/Cli/DashboardCommand.cs`
  - `dashboard/src/*`, `docs/dashboard-validation.md`
  - `openspec/specs/{work-dashboard,openspec-browser,document-feedback}`
  - `openspec/changes/archive/2026-10-26-add-work-dashboard`
  - Judged against `docs/architecture.md`.
- Reading limits and paths read outside scope:
  - Limits followed: nothing read under `openspec/changes/` except the archived dashboard change and my own report path. No `docs/handoffs/`. No Git history after 582f8bc.
  - Paths read outside the target paths:
    - Role and reference files: `.agents/roles/code-analyzer.md`, `.agents/roles/project-facts.md`, `.agents/templates/analysis-report.md`, `docs/architecture.md`.
    - Read in full or in part: `src/WorkLog/Infrastructure/AtomicFile.cs`, `src/WorkLog/Infrastructure/ContentHash.cs`, `src/WorkLog/Repositories/GitRepository.cs`, `src/WorkLog/Configuration/LocalSettings.cs` (lines 1-80), `src/WorkLog/Journaling/LogRequest.cs`.
    - Grep hits only: `src/WorkLog/Repositories/RepositoryRegistry.cs`, `RepositoryIdentity.cs`, `src/WorkLog/Persistence/JournalSchema.cs`, `src/WorkLog/Mcp/JournalTools.cs`, `McpServerHost.cs`, `src/WorkLog/Cli/ServeCommand.cs`, `src/WorkLog/Configuration/WorkLogConfiguration.cs`, `src/WorkLog/Installation/InstallationLayout.cs`.
    - Tests: `tests/WorkLog.Tests/Dashboard/DashboardTests.cs` (grep and lines 125-145); grep hits in `ReviewCommentTests.cs` and `SourceCatalogTests.cs`.
    - Artifacts: `artifacts/release-v0.3.0-local/worklog-0.3.0-win-x64/versions/0.3.0/release.json`, and a directory listing of `artifacts/`.
- Baseline commit: 582f8bc
- Implementation identity (before):
  - Command: `./scripts/review-identity.ps1 -Baseline 582f8bc -Paths <8 scoped paths>`.
  - HEAD `582f8bccb593285c628b58ece69d0ec6703ed17a`, empty manifest, tracked diff SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`, no untracked files.
  - Summary: `head=582f8bc baseline=582f8bc files=0 diff=e3b0c44298fc untracked=0/e3b0c44298fc`. This matches the assignment.
- Before/after identity: the after run returned the identical summary line. No unexpected mutation.
- Prior reports / rechecked findings: none. New IDs ANALYSIS-001 to ANALYSIS-014 came from the reserved range 001-040.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Performance (idle client) | Ran the real Angular client in an isolated headless Edge profile for 62-64 s with one 740 KiB document open. Counted requests with a Chromium net log, OpenSpec spawns with a counting shim, and state-file replacements with a 100 ms stat watcher. | 32 `GET /api/catalog`; 32 `POST /api/document` (24,518,592 bytes); 21 `POST /api/timeline`; 7 `PUT /api/state`; 32 OpenSpec spawns (16 discoveries); 6 `dashboard-state.json` replacements | Measured. Basis for ANALYSIS-001 to 003. |
| Performance (request level) | Node driver against the same isolated dashboard: catalog plus document at a 2 s cadence (15 polls), repeated document reads, timeline over 1, 30 and 420 days, full 420-day pagination, observations, search (active and all), and a 64 KiB state probe | Catalog miss 587-812 ms with 2 shim spawns; hit 2-3 ms; misses and hits alternate. Document 7-33 ms, 766,206 bytes every time. Timeline 13-52 ms at 3000 entries; 420-day pagination 6 pages / 120 ms. Search active 170-184 ms, all 629-1458 ms. About 578 entries exceed the state cap, then every PUT returns 400. | Measured. Basis for ANALYSIS-001, 002, 004, 005, 006. |
| Performance (spawn cost) | Timed single spawns in the fixture: shim `context`; `git worktree list`; `git rev-parse`; the installed real OpenSpec CLI `list --json` and `context --json` (read-only, `DO_NOT_TRACK=1`) | Shim 122-159 ms; git 72-93 ms; real OpenSpec 458-532 ms | Measured. A real-CLI discovery takes about 1.4 s per source (derived; a hypothesis on other machines). |
| Performance (source) | Inspected `SourceCatalog`, `DocumentService`, `DashboardJournal`, `TimelineService`, `DashboardAuthorization`, `DashboardState`, and the `app.ts` poll, remember and search paths | See findings | Source observation, labeled per finding |
| Duplication | Compared Dashboard code with `Repositories`, `Infrastructure`, and the server and client marker and change-matching logic | ANALYSIS-010 to 013. Minor items not raised as findings: `TimelineService` converts to local dates twice (`Day` local function at line 38, and lines 94-95); comment-length validation is repeated (`DocumentService.cs:98` and `125-126`); `DashboardState.Write` serializes twice (`DashboardState.cs:28,32`). | Source observation |
| Simplification | Looked for dead computation and redundant state | The catalog `Revision` is computed on every discovery and nothing consumes it (ANALYSIS-007) | Source observation (grep across `src/WorkLog/Dashboard` and `dashboard/src`) |
| Complexity | Size and responsibility review | `dashboard/src/app.ts` (1124 lines) and `app.html` (864 lines) form one component (ANALYSIS-014). `DashboardHost.Create` is dense but cohesive (not raised). | Source observation |
| Architecture | Compared against `docs/architecture.md` and the archived design.md and tasks.md | Catalog change detection, claimed by task 3.5 and design.md:102, is not implemented (ANALYSIS-001). The review write bypasses Infrastructure atomic replacement (ANALYSIS-010). Git context resolution is duplicated outside Repositories (ANALYSIS-011). | Source observation. Other documented decisions match: read-only journal adapter, 2 MiB reads, 50-match search, separate state file. |
| Spec consistency | `diff` of main specs against archived delta specs (headers stripped) | For all three capabilities, only the delta header and Purpose block differ | Passed |
| Validation doc | Read `docs/dashboard-validation.md` | It records functional, package-size and browser evidence, but no idle or steady-state cost | Not a defect in itself. It explains why ANALYSIS-001 to 006 went unnoticed. |
| Formatting | `dotnet format --verify-no-changes` | Not performed: the target adds or changes no lines against the baseline (empty diff), and the command restores into `obj`, which the concurrent QA reviewer owns | Not applicable |
| Parity (Claude Code / Codex) | Checked whether the target touches integration or MCP | The dashboard is client-neutral; no init, instruction or MCP contract changes | Not applicable |

Measurement setup:
- Payload: copied to scratch from `artifacts/release-v0.3.0-local/.../versions/0.3.0/worklog.exe` (version 0.3.0, SHA-256 `90d9e6e50d8d...`). This is the release-validation package named in `docs/dashboard-validation.md`. It was not rebuilt from 582f8bc, so its equivalence to the reviewed source is assumed, not proven.
- Data: a disposable scratch data directory and a scratch Git fixture with 20 active and 30 archived changes, 200 Markdown files, and one 740 KiB design. The journal was seeded with 3000 synthetic observations through isolated `serve` stdio.
- Tooling: an OpenSpec CLI shim placed first on `PATH`.
- Port and cleanup: the dashboard ran on port 48713 with `--no-open`. Port 47831, the user data directory and QA build outputs were not touched. The dashboard and the Edge processes were stopped afterwards; no listener was left on 48713 and no Edge process remained on the scratch profile.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Client `setInterval(poll, 2000)` (`app.ts:211`, `979-1017`); server `SourceCatalog`, `DocumentService`, `TimelineService`, `DashboardAuthorization`, `DashboardState` | Measured: requests, OpenSpec spawns, state writes. Source observation: git spawns, file reads, journal reads. | **Every 2 s poll (visible tab, document open):** 1 catalog GET and 1 document POST. **Every second poll (about 4 s):** a full catalog rediscovery (ANALYSIS-001), which does: 2 node/OpenSpec spawns (measured: 32 in 62 s); 2 git spawns (source: 1 `worktree list` per saved context and 1 `rev-parse` for the launch directory); 1 unindexed `SELECT DISTINCT repository` journal scan; a recursive walk with per-component canonicalization of every change file (200 here); and a JSON encode plus SHA-256 of the change list. **Every document poll:** a stat and canonicalization; the full text resent (766,206 bytes, measured); a task-regex pass over the whole text (ANALYSIS-002). With search text present, every poll also does a fresh read and SHA-256 of every in-scope document (ANALYSIS-005). **Every 5th poll (10 s):** 1 history and 1-2 ongoing timeline requests (21 in 64 s, measured), each reading and JSON-decoding every journal entry before `to` (ANALYSIS-006). Then a `PUT /api/state` causes an fsync'ed atomic rewrite of an unchanged file (6 writes in 62 s, measured; ANALYSIS-003). **Every API request:** a reread of `dashboard.secret` (93 API requests in 64 s, so about 1.5 reads/s; ANALYSIS-008). **Per hour, one visible tab:** about 1800 catalog and 1800 document requests (about 1.38 GB of loopback JSON for this document), about 900 discoveries (about 1800 node and 1800 git spawns), about 1080 full-journal reads, and 360 state-file writes. **How often the inputs change:** documents when an agent edits them (minutes to hours); config and worktrees rarely; the journal on each `log_work` (minutes). A hidden tab does nothing (`document.hidden` guard). Each additional visible tab multiplies the work (hypothesis). |
| Cache lifetime versus interval and refresh duration | Catalog TTL cache (`SourceCatalog.cs:29`, 2 s); document metadata cache (`DocumentService.cs:24-25,56,67`, 64 entries); client revision compare (`app.ts:545`) | Measured (catalog alternation); source observation (document cache) | **Catalog:** the TTL equals the poll interval and starts when discovery *ends*. The next poll arrives about 1.4 s later and hits; the one after arrives at 2.6 s or more and misses. Measured as alternating misses and hits over 15 polls. Relief is at most 50%, and there is none once discovery takes 2 s or more (real CLI: about 1.4 s per source, so 2 or more sources), when discovery runs back to back. The gate (`SourceCatalog.cs:26`) also serializes document, search and comment requests behind discovery. **Document cache:** gives real relief for disk reads and hashing (metadata first, as designed). However, search inserts every read and clears the cache at 64 entries, which evicts the open document (ANALYSIS-005). It gives no relief for transfer, because the unchanged text is still sent (ANALYSIS-002). |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `Preferences.positions` and `Preferences.documents` (`app.ts:576-578`, `846-850`), persisted in `dashboard-state.json`; client `drafts` (`app.ts:106-109`, `514-522`); `bootstraps` (`DashboardAuthorization.cs:59-60`); document cache (`DocumentService.cs:67`) | Measured (cap); source observation (others) | **Missing bound:** `positions` grows by about 113 bytes per document scrolled, and `documents` by about 86 bytes per change opened. Neither is ever pruned, and the keys include the 64-character source ID, so each worktree copy adds its own entries. At about 578 entries the 64 KiB server cap is exceeded, and **every later preference save is rejected with 400**, including theme and filter changes (measured; ANALYSIS-004). The client `drafts` map gains one entry, possibly empty, per document visited, until the page reloads (minor, not raised). **Bounded:** bootstrap tokens (2-minute expiry, at most 100); the document cache (64 entries, clear-all); search (50 matches); observation pages (100); timeline pages (50). The journal is not written. |
| Repeated full reads where a change check would do | Catalog, document GET, search, timeline/ongoing, state | Measured and source observation | **Catalog:** full CLI and filesystem rediscovery about every 4 s, where a check of directory and config mtimes would do (001). **Document:** the full body every poll, where a revision check would do (002). **Search:** a full read and hash of every in-scope document every poll (005). **Timeline:** all entries before `to` decoded every 10 s, and again per ongoing page, where an unchanged `MAX(rowid)` would show nothing is new (006). **State:** an unchanged file rewritten with fsync every 10 s (003). |

## Findings

### ANALYSIS-001: Catalog rediscovery spawns OpenSpec and Git about every 4 s while idle; the 2 s TTL equals the poll interval

- Severity: warning
- Related task / requirement:
  - Archived tasks.md 3.5 ("modification-time-first change detection with hashing only on change, for the catalog and selected document").
  - design.md:102.
  - `openspec-browser` "Refresh while preserving reading context".
- Locations (@582f8bc):
  - `src/WorkLog/Dashboard/SourceCatalog.cs:24-35` (TTL and gate).
  - `SourceCatalog.cs:48`, `54`, `68`, `84`, `126-127`, `142-151` (journal scan, Git and OpenSpec spawns, directory walk).
  - `src/WorkLog/Dashboard/DashboardJournal.cs:37` (the `DISTINCT` scan).
  - `src/WorkLog/Dashboard/LocalCommand.cs:30-31` (15 s timeout per call).
  - `dashboard/src/app.ts:211`, `990` (client poll).
- Observation:
  - The client fetches the catalog every 2 s. The server cache lives 2 s from the end of discovery, so about every other poll rediscovers.
  - Measured with the real client: 32 OpenSpec spawns in 62 s from one source, which is 16 discoveries.
  - Measured with the driver: 587-812 ms misses (shim) alternating with 2-3 ms hits.
  - Each discovery runs 2 node and 2 git spawns (source), a full `DISTINCT` journal scan (the only index is `entries(occurred, id)`), and canonicalization of every change file. All of it is serial under one gate, and document, search and comment requests wait on that gate.
  - The real CLI costs 458-532 ms per spawn (measured), so one source takes about 1.4 s. With 2 or more sources, discovery exceeds the 2 s poll and runs continuously (hypothesis). A hung CLI can hold the gate for 15 s per call.
  - The inputs change rarely. tasks.md 3.5 is checked for the catalog, but no catalog change detection exists; only the document has it.
- Proposed improvement and benefit:
  - Cache per-checkout `openspec context` and `git worktree list` results keyed by a cheap fingerprint (mtimes of `config.json`, `.git/worktrees` and `openspec/config.*`) or by a long TTL (for example 60 s).
  - Run `openspec list` only when the change-directory fingerprint changes.
  - Spawn per source in parallel.
  - Cache the journal repository list until `MAX(rowid)` changes.
  - Benefit: idle spawns drop from about 3600/h to near zero, and document and comment latency stops waiting on discovery.
- Behavior-preservation risks:
  - Task-count freshness: ticking a task changes file content, not directory mtimes. Include `tasks.md` mtimes or keep a slower CLI refresh.
  - Archive-move detection depends on directory mtimes.
  - Adding or removing a worktree must invalidate the cache.
  - Store resolution changes when the OpenSpec config changes.
- Evidence type: measured result. The multi-source continuous discovery is a performance hypothesis.
- Verification / measurement plan:
  - Rerun the isolated headless-Edge idle run (60 s, counting shim and net log). Expect about 0 OpenSpec spawns after warm-up.
  - Edit `tasks.md`, archive a change and add a worktree; confirm each shows up within the chosen bound.
- Relationship: canonical. Related to ANALYSIS-007.

### ANALYSIS-002: The unchanged open document is resent in full and rescanned on every 2 s poll

- Severity: warning
- Related task / requirement: tasks.md 3.5; design.md:102; `openspec-browser` "Refresh while preserving reading context"
- Locations: `src/WorkLog/Dashboard/DocumentService.cs:84-94`; `dashboard/src/app.ts:543-548`, `1008`
- Observation:
  - The server metadata cache avoids the reread and the hash, but every poll still splits the whole text, runs `TaskLine` on every line, and serializes the full text.
  - The client then parses the response and discards it when `revision` matches.
  - Measured: 766,206 bytes per poll, and 24,518,592 bytes in 64 s. That is about 1.38 GB/h for one 740 KiB document.
  - At the 2 MiB limit this would be about 3.8 GB/h, plus a client-side JSON parse every 2 s (hypothesis).
- Proposed improvement and benefit:
  - Send the known revision (or `If-None-Match` with an ETag equal to the revision).
  - When metadata and revision match, return 304 or `{unchanged:true}` without the text.
  - Cache task positions with `Cached`.
  - Benefit: idle transfer and parsing drop to a few hundred bytes per poll.
- Behavior-preservation risks:
  - `canComment` and archive state can change without the text changing (`app.ts:545` already compares `canComment`), so keep returning metadata.
  - Keep the per-poll containment check and archive-move resolution.
- Evidence type: measured result
- Verification / measurement plan: In the idle net-log run, total `POST /api/document` bytes over 60 s should be small. The existing switch and refresh browser scenarios should still pass.
- Relationship: none

### ANALYSIS-003: An idle History refresh rewrites `dashboard-state.json` with fsync every 10 s

- Severity: warning
- Related task / requirement: tasks.md 4.4; `work-dashboard` "Ongoing work and history navigation"
- Locations: `dashboard/src/app.ts:352`, `959-978`, `1011`; `src/WorkLog/Dashboard/DashboardState.cs:25-34`; `src/WorkLog/Infrastructure/AtomicFile.cs` (`Flush(flushToDisk: true)`)
- Observation:
  - The chain is: every 5th poll, `refreshData()` → `loadHistory()` → `remember()` → PUT of the full preferences → atomic rewrite with a flush to disk, even though nothing changed.
  - Measured: 7 PUTs and 6 file replacements in 62 s idle, about every 10 s. That is 360 fsync'ed writes per hour.
- Proposed improvement and benefit:
  - Call `remember()` only for user-initiated changes, or skip the PUT when the serialized preferences equal the last saved value.
  - Benefit: no idle disk writes.
- Behavior-preservation risks: `lastRoute` and history values must still be saved after navigation and control changes (`controlsChanged`, `routeChanged`).
- Evidence type: measured result
- Verification / measurement plan: In an idle run of 60 s or more, the watcher should see 0 writes after startup. A theme toggle should produce exactly 1 write.
- Relationship: none

### ANALYSIS-004: Preference maps grow without bound, and at about 578 entries every later preference save fails

- Severity: warning
- Related task / requirement: tasks.md 4.4; design.md:86; `work-dashboard` "Dashboard appearance" and "Shared repository selection"
- Locations: `dashboard/src/app.ts:576-578`, `843-851`, `976`; `dashboard/src/models.ts:138-139`; `src/WorkLog/Dashboard/DashboardState.cs:29`
- Observation:
  - `positions` grows by about 113 bytes per document scrolled, and `documents` by about 86 bytes per change opened.
  - Nothing prunes either map: not on archive, not on worktree removal, not when a source ID changes.
  - Measured: 578 synthetic positions are rejected with `400 Dashboard preferences exceed 64 KiB`, and a later PUT that only changes the theme is also rejected.
  - Because the client always PUTs the whole object, theme, filter, history controls and last route all stop persisting permanently. A recurring error banner appears until the state is reset by hand.
  - Reaching this over weeks of reviewing across worktrees is plausible (hypothesis).
- Proposed improvement and benefit:
  - Bound both maps by recency, for example 100 entries each.
  - Optionally prune entries for sources that are absent from the catalog.
  - Have the server trim these maps instead of rejecting the whole save.
  - Benefit: preference saving keeps working indefinitely.
- Behavior-preservation risks: Older reading positions are forgotten (acceptable for resettable UI state). The schema stays version 1 if the shape is kept.
- Evidence type: measured result (cap and failure). The rate of growth is a hypothesis.
- Verification / measurement plan: A unit test that fills past the bound and asserts the size stays under 64 KiB. A service test that the theme can still be saved after many documents.
- Relationship: none

### ANALYSIS-005: Active search reruns on every poll with fresh reads and hashes of every in-scope document, and thrashes the document cache

- Severity: warning
- Related task / requirement: tasks.md 3.4, 3.5; `openspec-browser` "Scoped document search"
- Locations: `dashboard/src/app.ts:1010` (`void this.search()`, not awaited); `src/WorkLog/Dashboard/DocumentService.cs:183` (`fresh: true`), `64-68` (stores each read, clears at 64)
- Observation:
  - Each poll with search text starts a search that is not awaited, so the `polling` guard does not cover it and searches can overlap.
  - Each search reads, decodes and hashes every in-scope document. It also inserts each one into the 64-entry cache, which is cleared when full. With more than 64 documents, the open document is evicted and rehashed on the next poll (source).
  - Measured on 200 documents: scope `all` takes 629-1458 ms; scope `active` (80 documents) takes 170-184 ms.
  - Cost grows with the archive.
- Proposed improvement and benefit:
  - Rerun search only when the catalog fingerprint or a document revision changes.
  - Search through the metadata cache, or with non-caching reads that skip SHA-256, and never let search evict cache entries.
  - Give search its own in-flight guard.
  - Benefit: no idle search I/O, and the document cache stays effective.
- Behavior-preservation risks: Results must still refresh on edits, additions and archive moves, and `unavailable` must stay accurate.
- Evidence type: measured result (latency). Source observation (rerun on every poll, eviction, overlap).
- Verification / measurement plan: With more than 64 documents and search text set, `HashReads` should be 0 across idle polls (service test). Time a search over 500 or more documents.
- Relationship: none

### ANALYSIS-006: Timeline refreshes decode the entire journal before `to` every 10 s, once per ongoing page

- Severity: suggestion
- Related task / requirement: tasks.md 1.2, 1.3; `work-dashboard` "Complete and bounded dashboard results"
- Locations: `src/WorkLog/Dashboard/DashboardJournal.cs:44-77`; `src/WorkLog/Dashboard/TimelineService.cs:29-34`, `36-85` (SHA-256 `IdFor` per entry), `87-98`; `dashboard/src/app.ts:322-324`, `360-389`, `1011`
- Observation:
  - Lifecycle context loads every entry before `to`, for all identities, including those with no visible activity, and repeats this for each 50-row page.
  - The client refreshes history and ongoing every 10 s whether or not the journal changed (21 requests in 64 s, measured). `MAX(rowid)`, which is already computed, would show that nothing changed.
  - Measured at 3000 entries: 13-52 ms per request, and 6 pages / 120 ms for a 300-row traversal.
  - The cost is linear in journal size and in rows/50 (hypothesis: a 100k-entry journal would cost about 30 times as much).
- Proposed improvement and benefit:
  - Skip a background refresh when the snapshot is unchanged.
  - Optionally return all ongoing rows in one response (the window is 14 days), or select visible identities first.
  - Benefit: idle journal work shrinks to one `MAX(rowid)` per interval.
- Behavior-preservation risks:
  - Backdated inserts still increase rowid, so they are still detected.
  - The 14-day ongoing window moves with time; refresh it on a slower cadence.
- Evidence type: measured result (3000 entries). Performance hypothesis for scaling.
- Verification / measurement plan: Time the timeline and ongoing traversal on 50k synthetic entries. In an idle run with no new observations, expect 0 timeline requests.
- Relationship: none

### ANALYSIS-007: The catalog `Revision` is computed on every discovery but never used; the client replaces the catalog each poll

- Severity: suggestion
- Related task / requirement: tasks.md 3.5
- Locations: `src/WorkLog/Dashboard/SourceCatalog.cs:156`; `dashboard/src/app.ts:990-991`; `dashboard/src/models.ts:28`
- Observation:
  - Every discovery JSON-encodes and hashes all changes, but grep finds no consumer on the server or the client.
  - The client calls `catalog.set` on every poll, which recomputes `filteredChanges`, `activeChanges` and `ongoingTickets` (O(rows x changes)) and re-renders.
- Proposed improvement and benefit:
  - Use the revision to skip the catalog update, the document reload and the search when nothing changed, and to answer "unchanged" on the server. Otherwise, remove it.
  - Benefit: this is the natural trigger for ANALYSIS-002 and 005.
- Behavior-preservation risks: The revision excludes source errors and notices (line 156); add them if the client relies on it to update notices.
- Evidence type: source observation
- Verification / measurement plan: Grep for consumers. Add a client unit test that an unchanged revision does not replace the signal.
- Relationship: supports ANALYSIS-001, 002, 005

### ANALYSIS-008: Every API request rereads `dashboard.secret` from disk

- Severity: suggestion
- Related task / requirement: tasks.md 4.3
- Locations: `src/WorkLog/Dashboard/DashboardAuthorization.cs:47-53`, `65`; `src/WorkLog/Dashboard/DashboardHost.cs:114-116`
- Observation:
  - `Valid` calls `SecretExists`, which does a `File.ReadAllBytes` and a constant-time compare on every `/api/*` request.
  - That is 93 API requests in 64 s idle (measured), or about 5,000 reads per hour.
  - Revocation on deletion needs only a metadata check or a watcher.
- Proposed improvement and benefit: Compare cached length and last-write time and reread only when they change, or use a `FileSystemWatcher`. The benefit is small.
- Behavior-preservation risks: Keep the revoke-on-delete and replace semantics; compare the bytes whenever the metadata changes.
- Evidence type: source observation, plus the measured request rate
- Verification / measurement plan: The existing revocation test (delete the secret, expect 401) still passes.
- Relationship: none

### ANALYSIS-009: Settings are loaded once at dashboard start; repositories saved later never appear until restart

- Severity: suggestion
- Related task / requirement: tasks.md 2.1; `openspec-browser` "Resolve local OpenSpec sources"
- Locations: `src/WorkLog/Cli/DashboardCommand.cs:20`; `src/WorkLog/Dashboard/DashboardHost.cs:97`; `src/WorkLog/Dashboard/SourceCatalog.cs:50`, `58-60`, `74-75`; `src/WorkLog/Configuration/LocalSettings.cs:17-24`
- Observation:
  - `LocalSettings` reads `config.json` only in its constructor, and the catalog reuses that instance for the life of the process. It respawns the CLIs about every 4 s but never rereads this one cheap input.
  - A repository first logged or initialized after the dashboard starts appears only from the journal, with "no available OpenSpec source", until the dashboard restarts.
- Proposed improvement and benefit: Reload settings when the `config.json` metadata changes (part of the fingerprint in ANALYSIS-001).
- Behavior-preservation risks: Reads are safe against concurrent atomic writes. The dashboard only reads, so saved identities are unaffected.
- Evidence type: source observation. The user-visible behavior is a hypothesis for QA to confirm.
- Verification / measurement plan: In isolation, start the dashboard, then run `serve --repository <new fixture>` with `log_work` against the same disposable data directory, and check whether the source appears without a restart.
- Relationship: none (QA may hold the correctness angle)

### ANALYSIS-010: The review-comment write reimplements atomic replacement without the Infrastructure flush

- Severity: suggestion
- Related task / requirement: `document-feedback` "Review comment writes with best-effort conflict detection"; the architecture table (Infrastructure: atomic file replacement)
- Locations: `src/WorkLog/Dashboard/DocumentService.cs:148-165`; `src/WorkLog/Infrastructure/AtomicFile.cs`
- Observation:
  - `WriteReviewAsync` writes a temporary file with `File.WriteAllBytesAsync` (no flush to disk), then `File.Move`. `AtomicFile.Write` flushes to disk but accepts only UTF-8 strings.
  - As a result, user documents get weaker durability than UI preferences, and the replacement logic is duplicated.
- Proposed improvement and benefit: Add an `AtomicFile` overload that takes bytes and a pre-commit callback (for the containment recheck), and use it in `DocumentService`.
- Behavior-preservation risks: Keep the recheck between write and move, and the exact encoding and BOM bytes. Flushing adds a few milliseconds per save.
- Evidence type: source observation
- Verification / measurement plan: The existing `ReviewCommentsPreserveBytesEncodingAndEarlierEdits` and marker mutation tests pass unchanged.
- Relationship: none

### ANALYSIS-011: Git context resolution and path comparison are duplicated outside `Repositories`

- Severity: suggestion
- Related task / requirement: tasks.md 2.1; the architecture table (`Repositories` resolves shared worktree context through the common Git directory)
- Locations: `src/WorkLog/Dashboard/SourceCatalog.cs:40`, `59`, `68-78`, `75`; `src/WorkLog/Dashboard/SourcePath.cs:5`, `37`; `src/WorkLog/Repositories/GitRepository.cs:10-21`, `13-14`; `src/WorkLog/Repositories/RepositoryRegistry.cs:21`, `29`
- Observation:
  - `SourceCatalog` resolves `--git-common-dir` and matches it to `RepositoryContexts` keys. This duplicates `RepositoryRegistry` and `GitRepository.GetCommonDirectory`, which trims trailing separators where `SourceCatalog` does not.
  - The OS-dependent comparer is written inline 5 times, even though `GitRepository.PathComparer` exists.
  - There are two process runners: `LocalCommand` is async with a timeout; `GitRepository.Run` is synchronous with no timeout.
  - The architecture assigns worktree discovery to `SourceCatalog`, but the context lookup itself belongs to Repositories.
- Proposed improvement and benefit:
  - Add a non-persisting `RepositoryRegistry.TryFindContext(commonDirectory)` and a shared path comparer or comparison.
  - Keep `LocalCommand` for its timeouts.
  - Benefit: a single matching rule.
- Behavior-preservation risks: The helper must not persist anything (discovery must not modify configuration).
- Evidence type: source observation
- Verification / measurement plan: The existing `SourceCatalogTests` pass unchanged.
- Relationship: none

### ANALYSIS-012: The review-marker format is parsed by two independent implementations

- Severity: suggestion
- Related task / requirement: tasks.md 11.1, 11.2; `document-feedback` "Display existing review comments"
- Locations: `src/WorkLog/Dashboard/ReviewMarker.cs:22-65`; `dashboard/src/markdown.ts:29-73`
- Observation:
  - The prefix, standalone-close rule, structured-body regex and `-&#45;` decoding are duplicated in C# (mutations) and TypeScript (display, and the `markerText` sent back on edit).
  - They already differ: the server derives a legacy quote, the client does not. Fence handling also works by a different mechanism on each side.
  - A future format change needs two edits, and a mismatch would surface as a 409 on edit. This is a drift risk, not a current defect.
- Proposed improvement and benefit: Use a shared marker-case fixture that both test suites assert against, or have the server return parsed markers with the document.
- Behavior-preservation risks: Returning markers would be an additive change to the API shape.
- Evidence type: source observation
- Verification / measurement plan: Add the shared fixture to both suites in the developer's change.
- Relationship: none

### ANALYSIS-013: Change-candidate matching is repeated three times in the client and again on the server

- Severity: suggestion
- Related task / requirement: tasks.md 3.3
- Locations: `dashboard/src/app.ts:400-405`, `465-474`, `1001-1006`; `src/WorkLog/Dashboard/SourceCatalog.cs:178-185`
- Observation:
  - The same predicate (source ID, plus name or directory name, plus an optional variant) is written three times in the client with variations: `openChange` ignores the variant, while `routeChanged` and `poll` apply it.
  - So ambiguity handling depends on the entry path.
- Proposed improvement and benefit: One `matchingChanges(catalog, sourceId, name, variant?)` helper in the client, kept consistent with `SourceCatalog.Resolve`.
- Behavior-preservation risks: `openChange` intentionally counts copies across variants; call the helper without a variant there.
- Evidence type: source observation
- Verification / measurement plan: Rerun the existing source-choice and archive-link unit and e2e scenarios.
- Relationship: none

### ANALYSIS-014: A single 1124-line component combines polling orchestration with every view

- Severity: suggestion
- Related task / requirement: design.md goal "Separate the Angular client, local transport, timeline projection, and document catalog so each can be tested independently"
- Locations: `dashboard/src/app.ts` (1124 lines); `dashboard/src/app.html` (864 lines); `dashboard/src/app.spec.ts` (141 lines)
- Observation:
  - One component, with about 60 members and 6 epoch counters, holds polling, `remember`, preferences, routing, the reader and review editing, search, drill-down and timeline geometry.
  - ANALYSIS-001 to 005 all originate in the poll and remember wiring, which cannot be unit-tested in isolation.
- Proposed improvement and benefit:
  - Extract only a refresh coordinator (cadence, change triggers, in-flight guards) and a preferences store (debounce, dedupe, bounds). This is not a general refactor.
  - Benefit: the fixes for ANALYSIS-002 to 005 become unit-testable.
- Behavior-preservation risks: The epoch and out-of-order protections (tasks.md 10.1) must move intact.
- Evidence type: source observation
- Verification / measurement plan: Rerun the Angular unit tests and the browser scenarios for delayed polls and document switches.
- Relationship: enables ANALYSIS-002, 003, 004, 005

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | not applicable | First round; no IDs were assigned for recheck | not performed |

## Assessment and limitations

**Scope covered:** all target paths, judged against `docs/architecture.md` and the archived design and tasks. There are 14 findings: 5 warnings (ANALYSIS-001 to 005) and 9 suggestions.

**Main conclusion:** each request is correct, but the idle dashboard does far more work than its inputs need:
- catalog rediscovery with node and git spawns about every 4 s;
- the full document resent every 2 s;
- journal-wide decodes every 10 s;
- an fsync'ed preferences rewrite every 10 s;
- search rerun on every poll.

Separately, the preference maps grow until the 64 KiB cap permanently breaks saving.

**Suggested order:**
1. ANALYSIS-004.
2. ANALYSIS-001 together with 007.
3. ANALYSIS-002, 003 and 005.
4. The rest.

QA may want to confirm the behavior described in ANALYSIS-009.

**Limitations of the measurements:**
- They ran in disposable scratch locations: a copied packaged payload, a scratch data directory, a Git fixture, a counting OpenSpec shim and a headless Edge profile, on port 48713.
- The payload is the v0.3.0 release-validation package, not a rebuild of 582f8bc; its equivalence to the source is assumed.
- Git spawn and file-read counts come from source.
- The following are hypotheses built from measured parts: timeline scaling beyond 3000 entries, multi-source real-CLI discovery durations, and the rate at which preferences grow.
- The isolated `serve` seeding created an `updates` folder in its disposable data directory, so an update check may have run there.

**Writes and side effects:**
- The repository write of this report was refused by the client; no repository file was written.
- No build, test, restore or npm command ran in this checkout. Nothing was committed and no branch was created.
- The isolated processes were stopped.

**Identity after the analysis:** `head=582f8bc baseline=582f8bc files=0 diff=e3b0c44298fc untracked=0/e3b0c44298fc`, identical to before.

**Decisions needed (orchestrator):**
- Triage of ANALYSIS-001 to 014.
- Whether archived tasks.md 3.5 (catalog change detection) counts as incompletely delivered.
