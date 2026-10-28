<!--
Saved by the orchestrator (Claude Code 2.1.312, session 7b4534dc-9e1e-4f7c-aa54-9b7bc061c7e7) on 2026-10-28.
Reviewer: A3 code-analyzer, Claude Code 2.1.312, agent subagent (non-interactive), claude-opus-5-5.
Case: no write attempted. The reviewer stated that its client subagent instructions say to return analysis reports as text; no refusal message exists. Text below is the reviewer's report verbatim.
-->

# Analysis report: add-ticket-and-repository-links / 001

## Review target

- Reviewer / role: A3 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 / agent subagent (non-interactive) / claude-opus-5-5
- Date: 2026-10-28T20:12Z
- Change root / assignment: `openspec/changes/add-ticket-and-repository-links` / A3
- Task and requirement references: tasks 1.1–5.2 ([tasks.md](../tasks.md)). Requirements are in [specs/work-dashboard/spec.md](../specs/work-dashboard/spec.md): External ticket and repository links, History ticket filter, Copy a work item as Markdown, Quiet active changes. Decisions 1–4 are in [design.md](../design.md). The developer's deviations 1–9 are under "Scope decisions" in [findings.md](findings.md).
- Scope and relevant neighboring paths:
  - Scope: `README.md`, `docs/dashboard-validation.md`, `dashboard/e2e/dashboard.spec.ts`, `dashboard/src` (`links.ts`, `quiet.ts`, `workitem-markdown.ts`, `app.ts`, `app.html`, `models.ts`, `styles.css`, the new specs), `src/WorkLog` (`Reporting/TicketFilter.cs`, `Dashboard/TimelineService.cs`), `tests/WorkLog.Tests` (`Reporting/TicketFilterTests.cs`, `Dashboard/TimelineTicketFilterTests.cs`), `tools/WorkLog.Smoke/DashboardFixture.cs`.
  - Neighbors read for context: `src/WorkLog/Persistence/SqliteEntryStore.cs`, `src/WorkLog/Dashboard/DashboardJournal.cs`, `src/WorkLog/Dashboard/DashboardHost.cs`, `dashboard/src/archive.ts`, `dashboard/src/api.ts`.
- Reading limits and paths read outside scope:
  - I did not read `openspec/changes/add-ongoing-board/`.
  - Outside scope I read `docs/architecture.md` (the drift reference), the change artifacts, and `.agents/roles/*` and `.agents/templates/analysis-report.md`.
- Baseline commit: `9243722`
- Implementation identity (before), from `./scripts/review-identity.ps1 -Baseline 9243722 -Paths README.md docs/dashboard-validation.md dashboard/e2e/dashboard.spec.ts dashboard/src src/WorkLog tests/WorkLog.Tests tools/WorkLog.Smoke`:
  - HEAD 9243722dda0a.
  - Manifest: 9 modified, 10 untracked.
  - Tracked diff SHA-256 `49cfb5bf5b382f8c2f1b8f6e80f7b25b5bfee4fc5c3fb996363763e4907fd556`.
  - Summary: `head=9243722 baseline=9243722 files=19 diff=49cfb5bf5b38 untracked=10/a4a3b4869b93`
- Before/after identity: after the analysis the summary was `head=9243722 baseline=9243722 files=19 diff=49cfb5bf5b38 untracked=10/a4a3b4869b93`, the same as before. `git status` showed no unexpected changes.
- Prior reports / rechecked findings: none.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Duplication: ticket predicate | Compared `TicketFilter.Matches` with the SQL in `SqliteEntryStore.ReadPage` (lines 60–68). | The C# copy is required by design Decision 2. The parity test runs 12 filters over 9 fixture entries, including padded parts, colon-in-ID, and too many segments. | The copy is intentional and the test covers it. The SQL has no back-reference to the C# copy, and the architecture doc does not mention it (ANALYSIS-004). |
| Duplication: client copy and toast paths | Inspected `app.ts` `copyLink` (1446–1465), `copyText`/`manualCopy` (1585–1595), and the `app.html` fallback panels. | There are now two clipboard-fallback signals and two `.clipboard-fallback` panels. | ANALYSIS-002 |
| Duplication: external navigation | Compared `openExternal` (1318) with `followLink` (1322). | Both call `window.open(url,"_blank","noopener,noreferrer")`, a one-line overlap. | Not reported. The overlap is trivial and the new handler is documented as the explicit entry point (deviation 7). |
| Reuse of existing helpers | Inspected `changeActivity`/`latestActivity`. | They reuse `archive.activityFor` and `api.localDate` rather than reimplementing them. | Good reuse. The template calls them more often than needed (ANALYSIS-003). |
| Complexity / simplification | Reviewed `links.ts`, `quiet.ts`, `workitem-markdown.ts`. | These are pure modules typed on `models.Ticket`, with no Angular dependency. `recognize()` centralizes host detection for repository and branch URLs. | They are suitable for the planned follow-up to reuse. One defect is in `workitem-markdown.ts` (ANALYSIS-001). |
| Correctness probe | `node -e` running the exact `escapeText` replacement on `"a [b] c"`. | Output `a [b] c`, unchanged. | Measured; ANALYSIS-001. |
| Architecture drift | Compared against `docs/architecture.md` §Dashboard (lines 23–25). | The ticket filter selects identities and then reuses the full lifecycle projection, which is consistent with "full pre-upper-bound lifecycle context". `InsightItem.Tickets` is an additive private-API field. MCP and journal schemas are unchanged. Client-side link building matches Decision 1. | No structural drift. The doc does not mention the new shared predicate or the activity endpoint's new Ongoing use (ANALYSIS-004). |
| Performance: Intl formatter construction | Node v24 microbenchmark, 20,000 iterations. | A new `Intl.DateTimeFormat` per call took about 37.8 µs; a cached formatter took about 1.0 µs. | Measured in Node, not in the browser. This feeds ANALYSIS-003 and is a hypothesis for the browser. |
| Performance: Copy as Markdown page loads | Inspected `copyMarkdown` and `TimelineService.Observations`. | Up to 9 further sequential `/api/observations` calls. Each one re-reads the journal up to `to` and re-hashes every entry's identity. | ANALYSIS-005 (hypothesis) |
| Unit tests | `npm test --prefix dashboard` | 14 files, 119 tests passed. | Passed |
| Formatting (verify-only) | `dotnet format ZeusApps.WorkLog.slnx --no-restore --verify-no-changes --include <5 changed C# files>` | `TicketFilter.cs` and both new test files are clean. ENDOFLINE diagnostics come from CRLF in the working copy (HEAD blobs are LF), so they are pre-existing and environmental. The WHITESPACE diagnostics on `TimelineService.cs` (for example the new line 47) and on `DashboardFixture.cs` flag single-line `if (...) continue;` and similar, which matches the existing pattern in those files. | No new reportable violations. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | `poll()` every 2 s; `loadActivity()` now runs on Ongoing as well as Archive, and is skipped only on History. | Source observation | **Idle:** no new work. `loadActivity` is keyed on `journalSnapshot` plus the selection, so an idle poll sends no `/api/activity/changes` request. **Active:** each journal change while Ongoing is open now adds one `POST /api/activity/changes`. That request is one SQLite window-function pass over the `entries` rows that have a `change_name`, alongside the existing `refreshData`. Before this change it happened only while Archive was open. This scales with journal changes, which is the right trigger. No processes are spawned. |
| Cache lifetime versus interval and refresh duration | Client `activityKey` / `activityScope`; there is no server-side activity cache. | Source observation | Invalidation is exact (journal snapshot or selection change), so idle polls get full relief. A day rollover alone does not refetch activity. The quiet state is recomputed from `now()` on the next render. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `activity` list, `Preferences.history.ticket`, Markdown entries, `copyToast` timer | Source observation | `activity` grows with the number of distinct (repository, change) pairs in the selected repositories. It is bounded by the journal and replaced on each load. The remembered ticket is a single string. Markdown loading is capped at 1,000 entries. The toast timer is cleared and replaced on each use and cleared on destroy. Nothing grows without bound. |
| Repeated full reads where a change check would do | Timeline ticket filter, Copy as Markdown, activity endpoint | Source observation; cost is a hypothesis | The filtered timeline still reads the whole journal up to `to`. This is by design, because lifecycle context needs every entry, and it adds an O(entries × tickets) predicate. Copy as Markdown repeats a full journal read and identity hash per 100-entry page, up to 10 times. This is a one-off user action (ANALYSIS-005). Ongoing cards recompute activity lookups and formatters on every render (ANALYSIS-003). |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | None | Source observation | Not applicable. The change spawns no executables and makes no network calls. Links are built client-side and opened only on explicit click through `window.open`. |

## Findings

### ANALYSIS-001: Markdown bracket escape is a no-op, and recorded URLs are inserted unnormalized

- Severity: warning
- Related task / requirement: task 3.1; requirement "Copy a work item as Markdown"; design Decision 3
- Locations:
  - `dashboard/src/workitem-markdown.ts:29`, used at lines 42, 46 and 52
  - `dashboard/src/links.ts:13-22` (`absoluteWebUrl` returns the raw trimmed text)
- Observation:
  - `escapeText` is `text.replace(/([\[\]])/g, "\$1")`. In a JS string literal `"\$1"` equals `"$1"`, so each bracket is replaced by itself.
  - A Node probe confirmed that `"a [b] c"` comes back unchanged.
  - As a result, a title, repository label or ticket ID containing `[` or `]` (for example `[WIP] Fix`) produces malformed link text in the copied Markdown, even though the code appears to escape it.
  - `ticketUrl` also returns a recorded URL verbatim after `new URL()` validation, not the normalized `href`. An inner space, which `new URL` accepts, therefore breaks the `[id](url)` destination.
  - The unit tests have no bracket or space case, so neither problem is caught.
- Proposed improvement and benefit:
  - Use `"\\$1"` (or `(c) => "\\" + c`).
  - Return `new URL(text).href` from `absoluteWebUrl`, or wrap Markdown destinations in `<...>`.
  - Add a `workitem-markdown.spec.ts` case with bracketed title and label text and a recorded URL containing a space.
  - Benefit: the copied Markdown is valid for any recorded text, and the helper does what its name says.
- Behavior-preservation risks: Returning `href` changes the `href` attribute of displayed ticket links for URLs that are not already normalized (for example a trailing `/` added to a bare origin). The e2e expectations use already-normalized URLs. Escaping changes only the copied text.
- Evidence type: measured result (Node probe of the exact expression) plus source observation
- Verification / measurement plan: Run the new unit cases. Re-run `npm test --prefix dashboard` and the Copy as Markdown e2e scenario.
- Relationship: This also affects correctness, so it overlaps with QA. If QA reports the same defect, treat that report as canonical.

### ANALYSIS-002: Two clipboard-fallback mechanisms coexist

- Severity: suggestion
- Related task / requirement: tasks 1.2 and 3.1; design Decision 3 ("Clipboard failure falls back to the existing copy dialog (`copyFallback`)")
- Locations:
  - `dashboard/src/app.ts:187` (`manualCopy`), `1446-1465` (`copyLink` with its own try/catch and `copyFallback`), `1580-1596` (`showToast`, `copyText`)
  - `dashboard/src/app.html`: the existing `copyFallback` panel and the new `manualCopy` panel, both `.clipboard-fallback`
- Observation:
  - The design asked the new actions to reuse `copyFallback`. Instead they add a second signal, panel and toast path (deviation 6).
  - `copyLink` keeps its own write/fallback logic. The two panels can be visible at the same time, and success messages use different channels (`notice` vs `copyToast`).
- Proposed improvement and benefit:
  - Route `copyLink` through `copyText`, which already supports single-line and multiline fallback.
  - Remove `copyFallback` and its template block.
  - Benefit: one clipboard path, one fallback panel, and no chance of overlapping fallback panels.
- Behavior-preservation risks: The "Link copied." message currently appears as the reader `notice` toast while a document is open. Keep that wording, or accept the copy toast. No spec or e2e asserts the `copyFallback` selector (grep found references only to `manualCopy`).
- Evidence type: source observation
- Verification / measurement plan: Add a unit test for the `copyLink` clipboard-rejection fallback. Re-run the existing copy-link e2e.
- Relationship: none

### ANALYSIS-003: Ongoing cards repeat activity lookups and formatter construction on every render

- Severity: suggestion
- Related task / requirement: task 4.1; requirement "Quiet active changes"
- Locations:
  - `dashboard/src/app.html:341,344,364`
  - `dashboard/src/app.ts:577-588`
  - `dashboard/src/quiet.ts:20-25`
  - `dashboard/src/api.ts:34-41` (`localDate` builds a new `Intl.DateTimeFormat` on every call)
- Observation:
  - Each change card calls `changeActivity(change)` once. That is one `JSON.stringify(selectedRepos)`, one linear `activityFor` scan with `Date.parse`, and two `localDate` calls, so two new formatters.
  - The card then calls `latestActivity(change)` up to three more times: once in the quiet chip, and twice in the footer ternary. Each is another linear scan of the whole `activity` list.
  - The `quiet` result already carries `lastAt`, but the chip ignores it and looks it up again.
  - Measured in Node v24, building the formatter costs about 38 µs against about 1 µs for a cached one.
  - The planned follow-up will reuse `quiet.ts` on more cards, so this cost grows with it.
- Proposed improvement and benefit:
  - Look up the activity once per card (`@let latest=latestActivity(change)`) and pass `latest?.lastAt` into `activityOf`, using `quiet.lastAt` and `latest.lastStatus` in the template.
  - Or expose one `computed()` map keyed by change, recomputed when `activity`, `selectedRepos` or the evaluated date changes.
  - Compute "today" once per render instead of per card, or cache formatters per zone in `localDate`.
  - Benefit: O(cards) work per render instead of O(cards × activity × 4), and simpler template expressions.
- Behavior-preservation risks: The quiet result must still change at a local-day rollover. If it is memoized, key it on `evaluatedDate` or the equivalent signal rather than on `now()`.
- Evidence type: measured result (formatter cost, Node only); the per-render total in the browser is a performance hypothesis
- Verification / measurement plan: In a browser profile of the Ongoing view with about 50 change cards and several hundred activity rows, record the change-detection time before and after. Re-run `quiet.spec.ts` and `app.links.spec.ts`.
- Relationship: none

### ANALYSIS-004: The duplicated ticket predicate is linked only by a test; the architecture doc does not mention it

- Severity: suggestion
- Related task / requirement: tasks 2.1–2.2; requirement "History ticket filter" ("Matching SHALL be identical to the `query_work` tool's ticket filter")
- Locations:
  - `src/WorkLog/Reporting/TicketFilter.cs`
  - `src/WorkLog/Persistence/SqliteEntryStore.cs:60-68` (unchanged, no back-reference)
  - `docs/architecture.md:23-25` (Dashboard and `TimelineService` paragraphs)
- Observation:
  - The ticket-filter semantics now live in two places: SQLite SQL and a C# predicate.
  - `TicketFilter` documents that it mirrors the SQL, but nothing at the SQL site says the C# copy depends on it. Someone editing the SQL would only find out when `TicketFilterTests` fails.
  - `docs/architecture.md` still describes `TimelineService` without the ticket selection or the shared predicate.
  - It also describes `/api/activity/changes` as serving the Archive view, but Ongoing now uses it too.
- Proposed improvement and benefit:
  - Add a one-line comment above the SQL ticket condition pointing to `TicketFilter` and its parity test.
  - Add a sentence to the architecture Dashboard paragraph: the timeline ticket filter selects identities via `Reporting.TicketFilter`, which is kept identical to the `query_work` SQL by a parity test; the activity endpoint also feeds the Ongoing quiet indicators.
  - Benefit: the coupling between the two filters can be found from either side, and the architecture reference stays current.
- Behavior-preservation risks: none; the changes are a comment and documentation.
- Evidence type: source observation
- Verification / measurement plan: Review the documentation diff. No test changes.
- Relationship: none

### ANALYSIS-005: Copy as Markdown repeats a full journal read for each 100-entry page

- Severity: suggestion
- Related task / requirement: task 3.1; design Decision 3
- Locations:
  - `dashboard/src/app.ts:1598-1640` (`copyMarkdown` loop)
  - `src/WorkLog/Dashboard/TimelineService.cs:91-101` (`Observations`, unchanged)
- Observation:
  - To reach 1,000 observations, the client makes up to 9 more sequential `/api/observations` requests.
  - Each request reads the journal up to `to` again and recomputes `IdFor` (identity hash) for every entry, then slices 100 entries.
  - The cost is therefore about 10 times one full journal read and hash, in series, for one click.
  - It is bounded and user-initiated, so it is not steady-state cost.
- Proposed improvement and benefit:
  - Accept as is unless a measurement shows a noticeable delay.
  - If it does, let the observations request take a bounded larger page size (for example up to 1,000) for this action, so a full copy is one request.
- Behavior-preservation risks: A page-size parameter changes the private API contract. It needs validation and a bound, and the existing 100-entry default must not change.
- Evidence type: source observation; the latency is a performance hypothesis
- Verification / measurement plan: Use an isolated fixture dashboard (non-default port, disposable `--data-dir`) with about 1,000 observations for one ticket and about 10,000 journal entries in total. Time Copy as Markdown from click to toast, and count `/api/observations` requests in the browser network log.
- Relationship: none

## Re-verification

None; this is the first analysis round.

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| — | — | — | — |

## Assessment and limitations

- **Coverage.** I covered the whole implementation for tasks 1.1–5.2, against the design decisions and the developer deviations 1–9.
  - The link builder is pure and its host recognition is in one place.
  - `links.ts`, `quiet.ts` and the `tickets` fields on `Row`/`Insight`/`Entry` can be reused by the planned follow-up without change. ANALYSIS-003 matters more once the follow-up adds cards.
  - The server ticket filter is a short identity-selection guard in `TimelineService.Project` that reuses the existing lifecycle projection, so there is no architectural drift.
  - The additive `InsightItem.Tickets` field (deviation 1) is justified, because insights can reference rows outside the current 50-row page.
- **Recommendations.**
  - ANALYSIS-001 should be fixed before acceptance. It is a real defect, but a small one.
  - ANALYSIS-002–004 are low-risk simplifications or documentation fixes.
  - ANALYSIS-005 should be measured before any change.
- **Not performed.**
  - Release build, `dotnet test`, Playwright e2e and packaged checks were left to QA.
  - I made no browser-side performance measurement. The formatter cost was measured in Node only.
  - I did not run a fixture dashboard, so no processes were left running.
- **Unexpected writes:** none. I wrote only a temporary format-output file in the system temp directory, which I deleted. The target identity was the same before and after.
- **Decisions needed:** the orchestrator should triage ANALYSIS-001–005 and save this report to the reserved path.

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.312 / claude-opus-5-5
task / finding IDs: tasks 1.1–5.2; ANALYSIS-001 (warning), ANALYSIS-002, ANALYSIS-003, ANALYSIS-004, ANALYSIS-005 (suggestions); ANALYSIS-006–040 unused
changed or report paths: none written; report text inline above, intended for D:\Code\ZeusApps.WorkLog\openspec\changes\add-ticket-and-repository-links\reports\analysis-001.md
checks: npm test --prefix dashboard -> passed (14 files, 119 tests); dotnet format --verify-no-changes on 5 changed C# files -> no new reportable violations (CRLF ENDOFLINE from the working copy and single-line-if WHITESPACE match the existing style); node probe of escapeText -> no-op confirmed; Node Intl formatter benchmark -> 37.8 µs new vs 1.0 µs cached; dotnet build/test, Playwright e2e, packaged checks -> not run (QA scope, not proportional)
target identity: before head=9243722 baseline=9243722 files=19 diff=49cfb5bf5b38 untracked=10/a4a3b4869b93; after head=9243722 baseline=9243722 files=19 diff=49cfb5bf5b38 untracked=10/a4a3b4869b93 (match)
limitations / decisions needed: report write not attempted because my client instructions say to return analysis reports as text (the "client instructions say to return findings as text" case), so the orchestrator must save it; ANALYSIS-001 overlaps with QA's correctness scope; ANALYSIS-003 browser cost and ANALYSIS-005 latency are hypotheses with measurement plans
next: orchestrator saves the report, triages ANALYSIS-001–005, and assigns developer corrections (ANALYSIS-001 recommended before acceptance)
```
