<!-- Saved by the orchestrator (Claude Code, claude-opus-5-5) verbatim from the reviewer's returned text. Reviewer: code-analyzer subagent, Claude Code 2.1.322, delegated subagent mode, model claude-opus-5-5. Case: the client refused the reviewer's write to this path; the reviewer reported the refusal and did not retry. Exact refusal message not supplied by the reviewer. -->

# Analysis report: realtime-dashboard-updates / ANALYSIS-RECHECK-033 (fix tier)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.322 / delegated subagent / claude-opus-5-5
- Date: 2026-11-11T20:19Z
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-033
- Task and requirement references: ANALYSIS-700, 701, 702 from [analysis-032](analysis-032.md); DEV-042 corrections
- Scope and relevant neighboring paths: `src/WorkLog/Dashboard/DashboardLiveConnections.cs`, `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs`, `dashboard/src/app.ts`, `dashboard/src/app.reviews.spec.ts`, `dashboard/src/app.reliability.spec.ts`, `dashboard/src/live-updates.ts` (RefreshQueue), `dashboard/src/api.ts`, `dashboard/src/board.ts` (`buildBoard`), `dashboard/src/app.html`, `dashboard/e2e/dashboard.spec.ts` (`authorize`, `openHistory`, :1608)
- Reading limits and paths read outside scope: source/no-host only. Outside the scoped paths I read `artifacts/dev042/e2e-full-1.log` (failure text only) and the analysis-032 probe under `artifacts/realtime-dashboard-updates/reviews/`.
- Baseline commit: 0d77685
- Implementation identity (before): `head=0d77685 baseline=0d77685 files=25 diff=c3d698f31bd3 untracked=13/d972d1bec3c4` (tracked diff `c3d698f3…7dd0`)
- Before/after identity: after = `head=0d77685 baseline=0d77685 files=25 diff=c3d698f31bd3 untracked=13/d972d1bec3c4`. Identical, so no mutation occurred.
- Prior reports / rechecked findings: [analysis-032](analysis-032.md) ANALYSIS-700, 701, 702

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| 700 fix | Inspection of `DashboardLiveConnections.cs:132-139,185-190,192-234` | `Cancel` is wrapped in a catch for `ObjectDisposedException`, and `TryComplete` runs unconditionally after it. All other `Stop` uses (`Token`, linked CTS creation) run inside the send loop before its own `finally` dispose. `IsCancellationRequested` does not throw after dispose. The `Add` duplicate path disposes a slot that was never registered. | Every ObjectDisposed path is covered. |
| 700 tests | `dotnet test tests/WorkLog.Tests/WorkLog.Tests.csproj -c Release --filter "FullyQualifiedName~DashboardLiveCoordinatorTests\|FullyQualifiedName~DashboardShutdownTests"` | 28 passed. The reflection harness reinserts slots after their loops dispose the tokens, then exercises Remove, revocation and StopAsync. With the fix reverted, Remove throws and BeginStop stops early, so `ActiveConnectionCount == 0` fails. | Meaningful. |
| 701 fix | Inspection of `app.ts:316-318,1806-1911` and the other `renderDocument` callers (`:897,2004,2044`) | `renderEpoch` is bumped only by `loadDocument`'s own render. The deferred callback skips when destroyed, after a newer load render, after a close (`document()` null) or after navigation to another key. The baseline, mark-read and save re-renders re-render the same key without bumping, so the pending callback still runs on the newer same-key DOM. Its attach, sync, anchor scroll and focus are idempotent there, which is intended. | Correct. See the residual-ordering note below. |
| 701 tests | `npm test --prefix dashboard` | 22 files, 327 tests passed. The ANALYSIS-701 test holds the explicit load, queues a Documents revision, and serves the poll `unchanged`. It asserts `changeCount > 0` and the anchor scroll. The close test asserts that no scroll happens. | Meaningful. On revert, `changeCount` and the scroll assertion fail. |
| 702 fix | Inspection of `joined` (`app.ts:806-821`) and all eight `…Now` bodies | Every publication is still guarded by its loader epoch, and only the return value changed. The map is set after `run()` returns, and no body calls `newest()` before its first `await`. The chain moves only forward: latest is the newest call, and when latest is the call itself the result is `whenAlone`. A cycle is therefore impossible. `newest()` checks `destroyed`. A wait is bounded by the newer call's own fetch. `api()` has no timeout, which is pre-existing, and the queue would have waited for that call anyway. | No deadlock and no stale publication. Two residual gaps follow (ANALYSIS-740, 741). |
| 702 tests | Same dashboard run | (1) Reviews held, `selectAll`, release, then Live with nothing pending: fails on revert. (2) A newer 500 still pauses, so failure is not masked. (3) A superseded search joins a newer 401 and ends unauthorized. The tests do not cover a superseded call that settles before the newer one, but that path is the same `return newest()`. | Adequate guard for the reported symptom. |
| Daily untouched | Re-ran the analysis-030 probe on current source: `node artifacts/realtime-dashboard-updates/reviews/analysis-033/analysis-030-probe-rerun.mjs` | Exit 0, `passed: true`. `loadDaily` and `queueDailyRefresh` do not use `joined`. | ANALYSIS-580 retained. |
| :1608 attribution | Inspection of `dashboard.spec.ts:30-44,1392-1395`, `app.html:14-40,368-378`, `app.ts:348-370,540-552,1236-1243`, `board.ts:218-229`, and baseline `git show 0d77685:dashboard/e2e/dashboard.spec.ts` | See the Assessment section. | Pre-existing ambiguous locator. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Send loop and revocation timer; browser loaders | Source observation | Unchanged by DEV-042. `joined` adds no requests. A superseded call issues no new request and only awaits an existing promise. |
| Cache lifetime versus interval and refresh duration | none new | Source observation | Not applicable to these fixes. Analysis-032 is unchanged. |
| Unbounded state | `newestLoads` map | Source observation | Bounded at 8 fixed keys. Each holds one settled or in-flight promise. |
| Repeated full reads where a change check would do | none new | Source observation | Not applicable. |
| External tool discovery | none new | Source observation | Not applicable. |

## Findings

### ANALYSIS-740: A live History refresh joined to a "Load more" call is acknowledged against the old snapshot

- Severity: suggestion
- Related task / requirement: 4.1, 5.1; the ANALYSIS-702 fix
- Locations: `dashboard/src/app.ts:781` (live `loadHistory()`), `:1342-1370` (`loadHistoryNow`; `more` pins `snapshot: this.timeline()?.snapshot` at `:1354`), `app.html:837` (`loadHistory(true)`)
- Observation:
  - A journal revision runs `loadHistory()`. If the user clicks Load more while that call is in flight, the live call is superseded and joins the `more` call's result.
  - The `more` call reads the next page of the old snapshot and appends it to the old rows. It returns true, so the journal revision is marked applied.
  - History then shows pre-revision rows while the status is Live. Before DEV-042 this case returned false and showed "Updates paused".
  - This is the one loader whose newer call is not a fresh read of current state.
- Proposed improvement and benefit: in `loadHistory`, join only calls of the same kind. For example, use `newest` only when the newer call is not `more`, or give `more` calls their own join name so that the live call returns `whenAlone = false`. The journal revision then stays pending instead of being silently acknowledged.
- Behavior-preservation risks: a `more` call superseded by a live reload must still drop its append, which the epoch already ensures.
- Evidence type: source observation, not reproduced
- Verification / measurement plan: a unit test that holds a live `timeline` request, calls `loadHistory(true)`, and releases. Assert the journal domain is still pending, or that a fresh first page was read.
- Relationship: residual of ANALYSIS-702

### ANALYSIS-741: A superseded 401 is still swallowed when no newer call reports it (search cleared)

- Severity: suggestion
- Related task / requirement: 5.4; ANALYSIS-702 "keep 401 precedence"
- Locations: `dashboard/src/app.ts:2401-2405` (`searchChanged` bumps `searchEpoch` with no call), `:2413` (`whenAlone = true`), `:2430` (catch: `epoch !== searchEpoch` → `newest()` before any 401 check); the same order applies in the catalog, reviews, activity, history, ongoing and document catches
- Observation:
  - When the query is cleared while a live search is in flight and that request returns 401, `newest()` yields `true` and `report()` never runs.
  - For the other loaders, a superseded 401 returns either the newer call's result or false. Because the newer call uses the same cookie, it normally reports the 401 itself.
  - So this is not a regression. Search previously returned true in this case too. But ANALYSIS-702's 401 precedence was not added.
  - In a live reconcile, sibling catalog or reviews requests surface the 401, so the user impact is low.
- Proposed improvement and benefit: in each joined catch, test `error instanceof ApiError && error.status === 401` first, then `report(error)` and return false, before the supersession branch. This matches what `loadReadSummaryNow` already does at `:1093`. A 401 is session-wide, so reporting it from a superseded call is always correct.
- Behavior-preservation risks: none to data. `report` is idempotent through `invalidateAuthorizationWork`.
- Evidence type: source observation
- Verification / measurement plan: extend the third ANALYSIS-702 test. Hold a search, clear the query, release a 401, and assert `unauthorized()`.
- Relationship: residual of ANALYSIS-702

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-700 | `diff=c3d698f31bd3 untracked=13/d972d1bec3c4` | Inspection: all disposed-token paths are covered and `TryComplete` is unconditional. The 3 new tests are meaningful, and 28/28 filtered .NET tests passed. | Verified |
| ANALYSIS-701 | same | Inspection: stale scroll and focus are cancelled by a newer load render, a close or a navigation key change. Same-key baseline and mark-read re-renders keep the pending work, which is intended and idempotent. The 2 new tests are meaningful, and 327/327 dashboard tests passed. | Verified |
| ANALYSIS-702 | same | Inspection: no infinite wait, no cycle, a bounded map and intact epoch protection. Daily is untouched and the analysis-030 probe passed. A genuine newer failure still fails. Residual gaps: a History `more` join acknowledges old data (ANALYSIS-740), and superseded-401 precedence was not added (ANALYSIS-741). | Verified for the reported false-pause symptom, with two residual suggestions |

## Assessment and limitations

**:1608 attribution: pre-existing ambiguous locator, not caused by DEV-042.**

- **Locator.** `openHistory` uses `getByRole("button", { name: "Work history", exact: false })` (`dashboard.spec.ts:1394`). The baseline has the same line at 0d77685:1260. That name also matches the empty-board button "Explore work history →" (`app.html:376-378`), which renders whenever `boardTotal() === 0`.
- **When the board is empty.** `authorize` returns as soon as the "Ongoing board" heading is visible (`:41-43`), which is after `ready.set(true)` but before activity and ongoing rows land. `buildBoard` drops change cards until activity is ready (`board.ts:220`). The empty state therefore exists from startup until the newest activity or ongoing call publishes. The "All repositories" click also reloads both.
- **What DEV-042 changed.** `joined` alters only the return value of superseded calls. Publication is still done by the newest call, at the same time as before. The only timing effect is that startup's `await this.refreshData()` (`app.ts:549`) now settles with the newer call, which delays `refreshOverview` and `startLiveUpdates`. Neither feeds the board, so if anything DEV-042 reduces request contention in that window.
- **Conclusion.** DEV-042 does not lengthen the window, from source; this has not been measured. Other callers use the same unscoped locator (`:654,888,928`) and pass only because their data has already landed.
- **Minimal fix (test-only).** Scope the locator to the main nav at all four sites: `page.getByRole("navigation", { name: "Main navigation" }).getByRole("button", { name: "Work history" })` (`app.html:14` has `aria-label="Main navigation"`). An `exact: true` name is less robust, because the accessible name includes the icon glyph ("◔ Work history").

**Residual ordering note (701, no finding).** A baseline re-render that lands before the load's deferred callback can capture the pre-scroll position and later restore it over a remembered-position scroll. Its anchor scrolls are protected by `readerScrollEpoch`. This behavior is unchanged from before DEV-042, and in practice the baseline awaits a fetch, so it runs after the timer.

**Limitations.**

- No host, browser, e2e or smoke run.
- Commands run: the identity script (before and after), `npm test --prefix dashboard` (passed), the filtered `dotnet test` (passed; it rebuilt Release outputs, which are ignored), and the copied analysis-030 probe (passed).
- Writes: ignored scratch under `artifacts/realtime-dashboard-updates/reviews/analysis-033/` (the copied probe, `no-host-results.json`, stdout and stderr). The client refused the report write itself; this text is returned inline for the orchestrator to save.
- No product, test, doc, ledger or prior-report edits were made.
- ANALYSIS-740 and 741 are source observations. The orchestrator owns triage.
