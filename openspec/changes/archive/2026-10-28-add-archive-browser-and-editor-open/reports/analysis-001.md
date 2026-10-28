<!-- Saved by the orchestrator. Reviewer: code-analyzer subagent (assignment A3); client Claude Code 2.1.312, subagent, auto mode; model claude-opus-5-5. Case: the client refused the reviewer's write. Refusal message as quoted by the reviewer: "Subagents should return findings as text". The text below is the reviewer's returned report, verbatim. -->

# Analysis report: add-archive-browser-and-editor-open / 001

## Review target

- Reviewer / role: code-analyzer subagent (assignment A3) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 (`claude --version`), subagent, auto mode / claude-opus-5-5
- Date: 2026-10-28T15:42:31Z (start) to 2026-10-28T15:50:03Z (end)
- Change root / assignment: `openspec/changes/add-archive-browser-and-editor-open` / A3
- Task and requirement references: tasks 1.1, 1.2, 1.3, 2.1, 2.2, 3.1, 3.3 ([tasks.md](../tasks.md)); design Decisions 1–5 ([design.md](../design.md)); [openspec-browser delta](../specs/openspec-browser/spec.md) (Archive view, Open documents in a local editor); [work-dashboard delta](../specs/work-dashboard/spec.md) (Local data access boundary, Change activity summary); developer deviations 1–7 in [findings.md](findings.md) Scope decisions
- Scope and relevant neighboring paths: `src/WorkLog/Dashboard/{EditorLauncher,DashboardHost,DashboardJournal,DocumentService,SourceCatalog}.cs`; `dashboard/src/{archive,app,models,main}.ts`, `app.html`, `styles.css`, `app.archive.spec.ts`, `archive.spec.ts`; `dashboard/e2e/{server.mjs,dashboard.spec.ts}`; `tests/WorkLog.Tests/Dashboard/ArchiveAndOpenTests.cs`; `docs/architecture.md`, `docs/dashboard-validation.md`, `docs/installation.md`, `README.md`. Neighbors read for context: `src/WorkLog/Dashboard/SourcePath.cs`, `DocumentService.LinkAsync` (same file), `src/WorkLog/Persistence/JournalSchema.cs`, `src/WorkLog/Journaling/EntryFactory.cs` (id format), `dashboard/playwright.config.*`.
- Reading limits and paths read outside scope: no limits beyond scope. Outside scope (read only): `SourcePath.cs`, `JournalSchema.cs`, `EntryFactory.cs` (one line), `playwright.config`, plus a grep that also hit copies under `artifacts/` (no content used).
- Baseline commit: `c53e647`
- Implementation identity (before): HEAD `c53e6477e859e2a9c4d24ba5b4d2be2f76c2631f`; manifest of 24 paths (5 untracked: `dashboard/src/app.archive.spec.ts`, `dashboard/src/archive.spec.ts`, `dashboard/src/archive.ts`, `src/WorkLog/Dashboard/EditorLauncher.cs`, `tests/WorkLog.Tests/Dashboard/ArchiveAndOpenTests.cs`; 19 modified); tracked diff SHA-256 `5d5c0e7ce0dd12d825801cb98883579ca5e273087c0e1da514da164123d249db`; untracked hashes `b35024d1…`, `f1638f66…`, `4aabc057…`, `a1d73904…`, `1dfb4c21…`. Summary: `head=c53e647 baseline=c53e647 files=24 diff=5d5c0e7ce0dd untracked=5/c6a442c792da`
- Before/after identity: after: `head=c53e647 baseline=c53e647 files=24 diff=5d5c0e7ce0dd untracked=5/c6a442c792da`. Identical to the before summary and to the expected summary. No unexpected mutation.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Performance: activity query (Decision 2) | Synthetic schema-2 journals of 50,000 rows in a disposable temp directory. `DashboardJournal.Activity` was timed through the Release `worklog.dll` by reflection, 21 runs each. `EXPLAIN QUERY PLAN` was also run. | Appendix A | **Measured:** 101.6 ms median for all repositories (4,799 groups), 33.7–63 ms filtered; full table scan plus two temp B-tree sorts. Acceptable at the call rate described below. No index needed. |
| Deviation 1 (single window query) | The same data, comparing the implemented query with the first step of the two-query sketch | Appendix A | **Measured:** the GROUP BY step alone takes 55.9 ms, and the sketch would still need a latest-status pass on top. The single query is not slower. Deviation is fine from the analysis side. |
| Tie-break semantics | Source: `ORDER BY occurred DESC, id DESC`; `EntryFactory` ids are `Guid.NewGuid().ToString("N")` | `DashboardJournal.cs:78`, `EntryFactory.cs:38` | This follows the design and matches the `(occurred, id)` ordering used elsewhere. On equal timestamps the winner is deterministic but not insertion order. Not a finding. Noted for the orchestrator, since tasks 1.2 says "ordered by id". |
| Launch boundary (1.3) | Source trace from client to `Process.Start`, plus a link probe through `SourcePath.Within` (Appendix C) | `EditorLauncher.cs:44-52`, `DocumentService.cs:36,52-58`, `SourcePath.cs:29` | The client sends no path or command. Preset validation runs before any filesystem access. However, the server-resolved target can be a non-Markdown file. See **ANALYSIS-001**. |
| Test switch (3.1, deviation 2) | Source and grep for `WORKLOG_DASHBOARD_TEST_LAUNCH_LOG` and `RecordingEditorLauncher` | `EditorLauncher.cs:23-35`, `DashboardHost.cs:101`, `server.mjs:44-47`, `playwright.config` `reuseExistingServer: false` | It can only be enabled by that exact environment variable in the dashboard process. No HTTP path, CLI flag, or setting enables it. All .NET open tests inject a `RecordingLauncher`. e2e uses a fresh `artifacts/dashboard-e2e/<uuid>` root and never reuses a server. When active, it is silent (204, no notice). See **ANALYSIS-006**. |
| Duplication: `ResolveFileAsync` vs `Resolve` | Diff inspection | `DocumentService.cs:52-58` | No duplicated containment logic. It delegates to `Resolve` and adds only the existence check and message, which match `Read` (`:65`). Not a finding. |
| Duplication: repository filter SQL | Diff inspection | `DashboardJournal.cs:65-75` vs `:102-112` | A second copy of the IN-clause builder. See **ANALYSIS-004**. |
| Duplication / growth: client | `wc -l`, diff | `app.ts` 1,669 → 1,755 lines (+93/−7); `app.html` +97 lines; new pure module `archive.ts` (75 lines) | The pure logic is properly extracted into `archive.ts`. `app.ts` growth is moderate. The template repeats the same editor `<select>` three times and copies the document-label ternary. See **ANALYSIS-005**. |
| Correctness-adjacent: position mapping (2.2) | Source and CSS inspection | `app.ts:482-489`, `styles.css:1573-1575` (`white-space: pre-wrap; overflow-wrap: anywhere`) | Source-view line = `scrollTop / lineHeight`, which ignores wrapping and offset. See **ANALYSIS-002**. |
| Per-poll / idle cost | Source trace of `poll()`, `loadActivity()`, `routeChanged()`, selection change | `app.ts:442-456, 562-563, 803, 1581, 1610-1614` | Activity is fetched only when the view is Archive and the journal snapshot or selection key changed. No refetch on idle polls. See the steady-state table. |
| Client recomputation | Node benchmark of the bundled `archive.ts` (Appendix B) | `archive.ts:25-35`, `app.ts:205-212, 1581` | **Measured:** 0.1 ms at 100×300 and 9.2 ms at 600×4,799. Recomputed every 2 s poll while the Archive view is visible. See **ANALYSIS-003**. |
| Deviation 3 (reviews scope "all") | `git show c53e647:dashboard/src/app.ts` | Baseline already calls `/api/reviews` with `scope: "all"` (line 409) | Zero added requests. Fine. |
| Deviations 4, 5 | Inspection | `ArchiveAndOpenTests.cs:30`, `app.html` `*-row` wrappers | Neutral for analysis. |
| Architecture | Compared with `docs/architecture.md:21-25` | The SQL stays in the read-only `DashboardJournal` adapter, containment stays in `DocumentService`, and the launch is behind a seam. The architecture paragraph was updated. | No drift, apart from ANALYSIS-001, which is a gap in the launch boundary the document describes ("resolves the file through `DocumentService`"). |
| Checks | `dotnet test -c Release --no-build --no-restore --filter FullyQualifiedName~ArchiveAndOpenTests` | 10/10 passed | Existing Release build. |
| Checks | `npm test --prefix dashboard` | 93/93 in 10 files passed | |
| Format | `dotnet format ZeusApps.WorkLog.slnx --no-restore --verify-no-changes --include <5 server files + ArchiveAndOpenTests.cs>` | New files `EditorLauncher.cs` and `ArchiveAndOpenTests.cs`: clean. Changed lines: WHITESPACE at `DashboardJournal.cs:63` and `DocumentService.cs:56`. ENDOFLINE is reported on every line of every touched file, which comes from the CRLF checkout (pre-existing, whole file). | Style only. CI does not enforce it. Not a finding. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Client `poll()` every 2 s; `loadActivity()` | Source observation (not measured live; no dashboard was started) | The change adds **no requests to an idle poll**. `loadActivity` returns early unless `view()==='archive'`, and its key is `journalSnapshot + selection`. It is called from `poll()` only inside the `journalSnapshot`/day-roll branch, and also on route entry and selection change. With the Archive view open and the journal changing, each journal change costs one `/api/activity/changes` request (one read-only connection, a full scan: 34–102 ms measured at 50k rows) on top of the existing `refreshData`. No processes are spawned. Client side: `catalog.set(snapshot)` (`app.ts:1581`) makes a new object every poll, so `archiveList` is recomputed every 2 s while the Archive view is visible (0.1–9 ms measured, ANALYSIS-003). The open endpoint does work only on a user click. |
| Cache lifetime versus interval and refresh duration | `activityKey` (client); no server cache | Source observation | The client key is invalidated only by a journal-snapshot or selection change, so idle polls get full relief. There is no server-side activity cache, and none is needed, because the client already deduplicates by snapshot. One query (≤ ~100 ms at 50k rows) is well under the 2 s interval. A request already in flight does not set the key until it completes, so a route change and a selection change racing can issue at most one superseded duplicate (dropped by the epoch). |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `activity` signal / response; Archive DOM list; launch log; `preferences.editor` | Measured (sizes) / source observation | `/api/activity/changes` returns every (repository, change name) ever recorded in the selected repositories, active ones included. It grows with history, at about 156 B per group (753 KB at 4,799 groups measured). There is no cap, but it grows slowly in a personal journal. The Archive list renders every archived change without paging; it is bounded by the catalog. `RecordingEditorLauncher` appends without a bound, but only while the test switch is set, and the e2e root is fresh per run. `editor` is a single enum value in bounded preferences. `ActivityRequest.Repositories` becomes one SQL parameter per distinct repository: an authorized client sending more than SQLite's parameter limit gets a 503. Negligible. |
| Repeated full reads where a change check would do | `Activity` | Measured | Every call scans the whole `entries` table regardless of the repository filter (27.7 ms even for an unknown repository). The client already gates calls on the `MAX(rowid)` snapshot, which is the cheap change check, so no further action is needed. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | `ShellEditorLauncher` (`Process.Start` with `UseShellExecute`) | Source observation; behavior on Windows is a **hypothesis** | No PATH or install-location lookup. It relies on the registered `vscode:`/`cursor:` protocol handlers and the `.md` file association, so it covers all install methods that register a handler. Portable installs fall back to `system`, as the design says. The call is synchronous with no timeout. Hypothesis, not observed: .NET sets `SEE_MASK_FLAG_NO_UI` when `ErrorDialog=false`, so an unregistered scheme should fail fast with a `Win32Exception` (mapped to 503) rather than show the Windows "get an app" prompt. Whether the error is reported or a prompt appears should be confirmed during the task 3.2 live check. No real editor was launched in this analysis. |

## Findings

### ANALYSIS-001: The `system` preset can shell-execute a non-Markdown file reached through a `.md`-named link

- Severity: warning
- Related task / requirement: 1.3; work-dashboard "Local data access boundary" ("Its only local launch operation SHALL be opening a discovered, contained document"); design Decision 3 risk "Shell-executing a URI could be abused…"
- Locations: `src/WorkLog/Dashboard/DocumentService.cs:36` (the `.md` check applies to `request.Path` only), `:52-58` (`ResolveFileAsync` returns the canonical file); `src/WorkLog/Dashboard/SourcePath.cs:29` (`Canonical` follows links to the final target); `src/WorkLog/Dashboard/EditorLauncher.cs:49,52` (`system` passes `file` as is) and `:16` (`UseShellExecute = true`). Reviewed identity `diff=5d5c0e7ce0dd untracked=5/c6a442c792da`.
- Observation: containment is checked on the canonical target, but the extension is checked on the requested name. A file symbolic link `proposal.md → tool.cmd` inside the source root passes both checks. `ResolveFileAsync` then returns `…\tool.cmd`. **Measured** with a disposable probe: `SourcePath.Within(root, "change/proposal.md")` returned extension `.cmd`, existing (Appendix C). With the `system` preset, `ShellEditorLauncher` would shell-execute that `.cmd` or `.exe` instead of opening a Markdown file. Reaching this needs a repository that contains such a link (for example a cloned repository checked out with Git symlink support), plus the user choosing Open in editor with System default app. The request itself still has to be authorized and same-origin. `LinkAsync` already applies the stricter rule to the canonical target (`DocumentService.cs:263`: `if (!safe.EndsWith(".md", …)) throw …`).
- Proposed improvement and benefit: in `ResolveFileAsync`, or in `EditorOpener.OpenAsync` before launching for every preset, reject a canonical file whose extension is not `.md` (case-insensitive), with the same `ArgumentException` → 400 path. This is a one-line check that reuses the `LinkAsync` precedent. It makes the launch boundary match the spec ("a contained document") whatever links exist in the source. Leave `Resolve` unchanged for reads, unless the orchestrator wants the read rule tightened too (a separate decision).
- Behavior-preservation risks: a legitimate `.md` link to another `.md` file inside the source still works. A `.md` link to a non-`.md` target would no longer open (it was never a Markdown document). Reads are unaffected if the check stays on the launch path.
- Evidence type: measured result (path resolution probe); the launch itself is a source observation (not executed).
- Verification / measurement plan: add a .NET test in `ArchiveAndOpenTests` that creates a file symlink `x.md → x.cmd` inside the fixture change. Skip when symlink creation is not permitted, or use the existing junction helper pattern for the directory variant. Assert 400 and that `RecordingLauncher.Targets` is empty for all three presets.
- Relationship: none

### ANALYSIS-002: The source-view reading line ignores wrapped lines and the text's offset

- Severity: warning
- Related task / requirement: 2.2; design Decision 4 ("Source view uses the first visible line"); openspec-browser "Open documents in a local editor" (current reading position)
- Locations: `dashboard/src/app.ts:482-489`; `dashboard/src/styles.css:1573-1575`; `dashboard/src/app.archive.spec.ts:119-121` (only `scrollTop = 0` is tested)
- Observation: `readerLine()` computes `floor(scroller.scrollTop / lineHeight)`. `.source-text` is `white-space: pre-wrap; overflow-wrap: anywhere`, and OpenSpec documents commonly hold long single-line paragraphs (requirement text). Once the reader scrolls past wrapped lines, the estimate runs ahead of the true first visible line by the number of extra visual rows. It also counts any content above the `<pre>` inside the scroller as lines. Open in editor from source view therefore lands later in the file than what the user sees.
- Proposed improvement and benefit: derive the line from the text at the scroller's top edge. Use `document.caretPositionFromPoint` (or `caretRangeFromPoint`) at the scroller's top-left inside `.source-text`, then count `\n` characters in the text before that offset across the `sourceParts` spans, with the current division kept as a fallback. Alternatively, render one element per source line with `data-start`, which would let the existing `firstVisibleLine` serve both views and remove the special case. Either gives the correct position.
- Behavior-preservation risks: the read-since-last-view highlighting spans in source view must be preserved, and the caret APIs need a fallback in test environments (jsdom).
- Evidence type: source observation (not measured in a browser)
- Verification / measurement plan: Playwright: open a fixture document with a long first paragraph in source view, scroll so a later line is at the top, click Open in editor, and assert that the launch-log line matches. Unit: test the newline-counting helper with a stubbed caret offset.
- Relationship: this overlaps the QA correctness scope. The orchestrator may merge it with a QA finding.

### ANALYSIS-003: The Archive list re-matches all activity on every poll with an O(changes × activity) scan

- Severity: suggestion
- Related task / requirement: 2.1
- Locations: `dashboard/src/archive.ts:25-35` (`activityFor`, with `Date.parse` per comparison), `dashboard/src/app.ts:205-212` (`archiveList` computed), `:1581` (`catalog.set(snapshot)` makes a new object every 2 s poll)
- Observation: `archiveEntries` calls `activityFor` once per archived change, and each call scans the whole activity array. Because the catalog signal receives a fresh object on every poll, the computed re-runs every 2 s while the Archive view is rendered, and on every filter keystroke. **Measured** (Appendix B): 0.10 ms at 100×300, 1.1 ms at 300×1,200, 9.2 ms at 600×4,799, 33.7 ms at 1,000×10,000.
- Proposed improvement and benefit: add a `computed` that indexes `activity()` once into a `Map<changeName, ChangeActivity[]>` (or keeps the latest per name and repository), and pass a lookup into `archiveEntries`. Matching becomes O(changes + activity), and the poll-driven recompute stops depending on activity size. Comparing the ISO `lastAt` strings, or parsing once at index time, removes the repeated `Date.parse`. This is low priority: costs are negligible at personal sizes.
- Behavior-preservation risks: keep the "newest lastAt across the change's repositories" rule and the `null` for no activity. `archive.spec.ts:55` already covers this.
- Evidence type: measured result (Node microbenchmark of the shipped function); the poll-driven recompute is a source observation.
- Verification / measurement plan: the existing `archive.spec.ts` cases, plus a rerun of the Appendix B benchmark.
- Relationship: none

### ANALYSIS-004: The repository IN-clause builder is duplicated in `DashboardJournal`

- Severity: suggestion
- Related task / requirement: 1.2
- Locations: `src/WorkLog/Dashboard/DashboardJournal.cs:65-75` (`Activity`) and `:102-112` (`Read`)
- Observation: `Activity` copies the distinct-repositories, `$r{i}` parameters, and `" AND repository IN (…)"` construction from `Read`, with slightly different shape: it materializes `distinct` first. The proposal says later changes reuse the activity summary (`add-ticket-and-repository-links`), so a third copy is likely.
- Proposed improvement and benefit: extract a `private static string RepositoryFilter(SqliteCommand command, string[] repositories)` that adds the parameters and returns the clause (empty for none), and use it in both methods. This gives one place for the parameter naming and distinct rule.
- Behavior-preservation risks: parameter names must not collide with `$snapshot`/`$to` in `Read`. The current `$r` prefix is safe.
- Evidence type: source observation
- Verification / measurement plan: existing `ArchiveAndOpenTests` (repository filter, duplicates, unknown) and the timeline tests.
- Relationship: none

### ANALYSIS-005: Template duplication: three identical editor selectors and a copied document-label mapping

- Severity: suggestion
- Related task / requirement: 2.1, 2.2
- Locations: `dashboard/src/app.html:192-194, 274-276, 780-782` (identical `<select class="editor-select" …>` blocks); `:351` and `:431` (the same `doc.label==='design'?'Design':…` ternary, now in the Ongoing card and the Archive entry); the three `row-action` "Open in editor" buttons
- Observation: the change adds about 97 template lines, a third of them repeated markup. A future preset or label change has to be made in three places (selector) or two (labels), and the copies can diverge.
- Proposed improvement and benefit: one `<ng-template #editorSelect>` used through `ngTemplateOutlet` in the three places, and a `documentLabel(label)` method (or a map in `archive.ts` next to `editorPresets`) for the label ternary. This is small, with no new abstraction layer.
- Behavior-preservation risks: keep the `aria-label` values that Playwright selectors depend on.
- Evidence type: source observation
- Verification / measurement plan: `npm test --prefix dashboard` and the two new e2e scenarios.
- Relationship: none

### ANALYSIS-006: An active test launcher is silent

- Severity: suggestion
- Related task / requirement: 3.1 (deviation 2)
- Locations: `src/WorkLog/Dashboard/EditorLauncher.cs:28-29`, `src/WorkLog/Dashboard/DashboardHost.cs:101`; `docs/installation.md:39`
- Observation: the switch is narrow. Only the exact environment variable in the dashboard's own process enables it, and nothing reachable from the client does. The packaged build honors it by design, because the packaged e2e run needs it. If it is ever set outside tests (an inherited shell environment), Open in editor returns 204 and quietly appends to a file. The user sees success and nothing opens, with no way to diagnose it from the UI.
- Proposed improvement and benefit: when `FromEnvironment` selects the recording launcher, print one line to the dashboard's console output at startup (for example "Test editor launcher active: editor targets are recorded, not opened"). This makes accidental activation visible at no runtime cost. Optional.
- Behavior-preservation risks: e2e and tests that parse the dashboard's startup output must tolerate the extra line. Check `server.mjs`, which reads the printed URL.
- Evidence type: source observation
- Verification / measurement plan: run the e2e suite and confirm `server.mjs` still parses startup output. One-line inspection of console output with the variable set.
- Relationship: none

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | - | - | not applicable (first round) |

## Assessment and limitations

Scope covered: duplication, complexity, architecture, and performance for tasks 1.1, 1.2, 1.3, 2.1, 2.2, 3.1, and 3.3. The implementation is well factored overall:
- The Archive logic is a pure module.
- The launch sits behind a seam with enum validation before any I/O.
- The activity fetch adds nothing to idle polls.
- The single window query (deviation 1) performs acceptably: about 100 ms worst case at 50k rows, run only on a journal change while the Archive view is open, so no index change is warranted.

The one boundary concern is ANALYSIS-001, a canonical-target extension gap on the launch path, with a one-line fix and an existing precedent in `LinkAsync`. ANALYSIS-002 affects position accuracy in source view only.

Not performed:
- No dashboard was started, so per-poll request counts are traced from source, not counted.
- No real editor was launched, so ShellExecute behavior for an unregistered scheme remains a hypothesis for task 3.2.
- Full `dotnet test` and e2e suites were not rerun (QA scope); only the new .NET class and the dashboard unit suite were run.

Writes:
- No writes in the repository.
- Disposable data (the synthetic journals and the link probe) lived under the session scratchpad in the system temp folder and was deleted. The throwaway probe projects remain in the scratchpad only.
- The client refused the report and evidence file writes, so this text is returned inline.

Decisions for the orchestrator: triage of ANALYSIS-001 (and whether the read path should get the same canonical extension rule); the tie-break-by-random-GUID semantics noted in the table; the Cursor format (deviation 6) remains a user decision and is not analyzed here.

### Appendix A: activity query measurements (evidence)

Environment: Windows 11, .NET SDK 10.0.113, the Release `worklog.dll` from the reviewed tree (built after every changed server file), Python 3.13 / sqlite 3.50.4 for generation, Node 24.13.0.

Generator: the schema 2 DDL from `JournalSchema.cs` (both migrations, `user_version=2`), 50,000 rows, GUID ids, `occurred` spread over one year in ticks, 80 % of rows with a change name, statuses 3:1 `in_progress`:`done`, and the first row per (repository, change) set to `started` (unique index). Payload about 715 B, file about 46 MB. Dataset A: 8 repositories × 600 names (4,799 groups). Dataset B: 3 × 60 (180 groups).

Plan: `SCAN entries` → `USE TEMP B-TREE FOR ORDER BY` (window) → `USE TEMP B-TREE FOR ORDER BY` (final). No index is used.

`DashboardJournal.Activity` through reflection, 2 warm-up calls and 21 timed calls:

| Dataset | Call | Groups | Median | Min | Max |
| --- | --- | --- | --- | --- | --- |
| A | all repositories | 4,799 | 101.6 ms | 87.7 | 131.3 |
| A | 2 repositories | 1,200 | 42.6 ms | 40.0 | 55.7 |
| A | 1 repository | 600 | 33.7 ms | 32.2 | 38.5 |
| B | all repositories | 180 | 81.6 ms | 79.8 | 86.4 |
| B | 2 repositories | 120 | 63.3 ms | 61.8 | 65.5 |
| B | unknown repository | 0 | 27.7 ms | 25.7 | 41.6 |
| A | `Snapshot()` reference | - | 0.51 ms | | |

Query shapes (Python, dataset A, median of 10): implemented window query 93.2 ms; GROUP BY MIN/MAX only 55.9 ms; bare filtered count 24.9 ms. Response size for dataset A in the API shape: 753,289 B (about 156 B per group).

### Appendix B: client `archiveEntries` benchmark (evidence)

`dashboard/src/archive.ts` was bundled with the workspace `esbuild` and called in Node with an empty filter, median of 25: 100×300 → 0.10 ms (max 1.07); 300×1,200 → 1.12 ms (3.27); 600×4,799 → 9.22 ms (9.86); 1,000×10,000 → 33.70 ms (39.35).

### Appendix C: link containment probe (evidence)

A disposable `root/change/` held `tool.cmd` and a file symlink `proposal.md → tool.cmd` (created without elevation). Reflection call to `SourcePath.Within(root, "change/proposal.md")` in the Release `worklog.dll`:

```
requested: change/proposal.md (ends with .md: True)
resolved extension: .cmd file name: tool.cmd exists: True
```

Nothing was launched.

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.312 / claude-opus-5-5
task / finding IDs: tasks 1.1, 1.2, 1.3, 2.1, 2.2, 3.1, 3.3; new findings ANALYSIS-001 (warning), ANALYSIS-002 (warning), ANALYSIS-003, ANALYSIS-004, ANALYSIS-005, ANALYSIS-006 (suggestions); ANALYSIS-007 – ANALYSIS-040 unused
changed or report paths: none written (client refused the write); intended report path openspec/changes/add-archive-browser-and-editor-open/reports/analysis-001.md (evidence folded into Appendices A–C; intended evidence file analysis-001-measurements.md not written)
checks: dotnet test -c Release --no-build --no-restore --filter FullyQualifiedName~ArchiveAndOpenTests -> passed (10/10); npm test --prefix dashboard -> passed (93/93); dotnet format --verify-no-changes --include <changed C# files> -> new files clean, 2 WHITESPACE nits on changed lines, whole-file ENDOFLINE pre-existing (not a finding); activity query 50k-row measurement -> performed (Appendix A); full dotnet test / e2e -> not run (QA scope, existing developer evidence)
target identity: before: head=c53e647 baseline=c53e647 files=24 diff=5d5c0e7ce0dd untracked=5/c6a442c792da; after: head=c53e647 baseline=c53e647 files=24 diff=5d5c0e7ce0dd untracked=5/c6a442c792da (identical)
limitations / decisions needed: the client refused my report write ("Subagents should return findings as text"), so the full report text is inline for the orchestrator to save as analysis-001.md (evidence is in the appendices). No dashboard was started and no editor was launched, so idle-poll cost is a source trace and unregistered-scheme ShellExecute behavior is a hypothesis for task 3.2. Disposable data was deleted; throwaway probe projects remain only in the session scratchpad. Decisions: triage ANALYSIS-001 (and whether reads get the same canonical .md rule); acceptance of the random-GUID tie-break for equal timestamps.
next: orchestrator saves the report, triages ANALYSIS-001..006 into findings.md, and routes the accepted ones to the developer; ANALYSIS-002 may be merged with an overlapping QA finding.
```
