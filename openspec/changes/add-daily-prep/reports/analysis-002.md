<!-- Saved by the orchestrator. Reviewer: independent code-analyzer subagent (assignment A6). Client: Claude Code 2.1.312, subagent mode, auto permission mode. Model: claude-opus-5-5. Case: the client refused the reviewer's write; exact refusal message: "Subagents should return findings as text, not write report files. Include this content in your final response instead." The text below is the reviewer's returned report, verbatim. -->

# Analysis report: add-daily-prep / round 2 (A6, recheck)

## Review target

- Reviewer / role: independent code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 (`claude --version`), subagent spawned by the orchestrator, auto permission mode, model `claude-opus-5-5` (role default)
- Date: 2026-10-29T09:40Z
- Change root / assignment: `openspec/changes/add-daily-prep`, assignment A6 (recheck of ANALYSIS-001 plus regression of the A4 diff)
- Task and requirement references: task 2.1 ([tasks.md](../tasks.md)); [design.md](../design.md) Decision 3 (updated after A4); [spec.md](../specs/daily-preparation/spec.md) requirement "Today and blockers" (updated after A4, including the scenarios "A ticket used with two changes" and "Board still loading") and "Copy for daily"; [findings.md](findings.md) History entries for A4 and the planning update
- Scope and relevant neighboring paths:
  - A4 changes:
    - `dashboard/src/daily.ts` and `dashboard/src/daily.spec.ts`
    - `dashboard/src/app.ts` (`ongoingScope`, `ongoingReady`, `dailyTodayView`, `loadOngoing`) and `dashboard/src/app.daily.spec.ts`
    - `dashboard/src/app.html` (Today section and copy button, read for the gate)
    - `dashboard/e2e/dashboard.spec.ts` (the Daily `open` helper)
    - `docs/dashboard-validation.md` (Daily section)
  - Neighbors read for context:
    - `dashboard/src/board.ts` (`changeCards`, `ticketCards`, `isQuiet`)
    - `src/WorkLog/Dashboard/DailyService.cs` (`Items`)
    - `src/WorkLog/Dashboard/TimelineService.cs` (`Project`, and how `changes` is computed)
    - `src/WorkLog/Dashboard/DashboardJournal.cs` (`Activity`, keyed by change name)
    - `dashboard/src/models.ts` (`Entry`, `DailyItem`)
    - `dashboard/package.json` and `dashboard/angular.json`
- Reading limits and paths read outside scope: `reports/qa-002.md` was not read. Outside the listed scope I read only the neighbors named above and [analysis-001.md](analysis-001.md).
- Baseline commit: `cd1c0e3`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline cd1c0e3 -Paths src/WorkLog tests tools/WorkLog.Smoke dashboard/src dashboard/e2e README.md docs/dashboard-validation.md docs/architecture.md` printed:
  - HEAD `cd1c0e3395a71ee37c70d76ac117180200803597`
  - Manifest: 5 untracked files (`dashboard/src/app.daily.spec.ts`, `dashboard/src/daily.spec.ts`, `dashboard/src/daily.ts`, `src/WorkLog/Dashboard/DailyService.cs`, `tests/WorkLog.Tests/Dashboard/DailyServiceTests.cs`) and 14 modified files
  - Tracked diff SHA-256 `1406dcd25cc126182512a3fa6c5801bd63bb6aa31fb3a36738d8fb0d796fd085`
  - Untracked hashes: `app.daily.spec.ts` `03e9354b…6379`, `daily.spec.ts` `dfc72a71…a8a3`, `daily.ts` `261ee794…cec0`, `DailyService.cs` `a8b3abe2…6e47`, `DailyServiceTests.cs` `663ff818…e355`
  - `Summary: head=cd1c0e3 baseline=cd1c0e3 files=19 diff=1406dcd25cc1 untracked=5/7d3b824553cd`, which matches the assignment
- Before/after identity: after the analysis the same command printed tracked diff `1406dcd25cc126182512a3fa6c5801bd63bb6aa31fb3a36738d8fb0d796fd085` and `Summary: head=cd1c0e3 baseline=cd1c0e3 files=19 diff=1406dcd25cc1 untracked=5/7d3b824553cd`. Both match the values before the analysis. No unexpected mutation.
- Prior reports / rechecked findings: [analysis-001.md](analysis-001.md), ANALYSIS-001

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-001: what a change card's line contains | Read `lineFor` (`dashboard/src/daily.ts:31-52`) against design Decision 3 and the spec | For a change card, `lineFor` takes `[item.primary, ...item.others]` from today's items whose `repository` is in `card.repositories`. It keeps the entries with `entry.change === card.title` and sorts them newest first, breaking ties by entry ID. The newest entry becomes `primary` and gives the line its status. `item.changes` is no longer used. Tickets come from the chosen entries, or else from `card.tickets`. Each of today's entries belongs to exactly one `DailyItem` (`DailyService.cs:74-83`), so nothing is counted twice. | Verified (see Re-verification) |
| ANALYSIS-001: Vitest cases (a) and (b) | Read `daily.spec.ts:104-140`; ran `npx vitest run src/daily.spec.ts --reporter=verbose` from `dashboard/` | (a) `daily.spec.ts:117-125`: an item with `changes: ["alpha","beta"]` whose entries today are all `beta` gives the `alpha` card a null line. (b) `:127-140`: one change split across a ticket item and a change-only item gives one line: primary `t1`, status `done`, `others` `["c1","t0"]`, tickets `[DEP-12]`. The earlier case at `:104-115` now also asserts that an entry of another change (`export`) and an entry from another repository are left out. The run passed 20 of 20. | Verified |
| Regression: what a change card's line can lose | Traced `ticketCards` (`board.ts:134-167`), `changeCards` (`board.ts:89-128`) and `Project` (`TimelineService.cs:55`); ran a throwaway Vitest probe outside the repository (see ANALYSIS-041) | A ticket row whose history includes an active change has no card of its own; it is folded into that change card. An entry logged today for that ticket without a change does not have `change === card.title`, so no line uses it. | ANALYSIS-041 |
| Readiness gate: selection change | Read `ongoingReady` (`app.ts:652-656`), `dailyTodayView` (`app.ts:306-312`), every place that writes `selectedRepos` (`app.ts:403`, `684-691`, `695`, `1148`), and `scopeChanged` and `refreshData` (`app.ts:698-712`) | `ongoingReady` compares the loaded scope with `JSON.stringify(selectedRepos() ?? [])`, so a selection change closes the gate immediately. After start-up, every write to `selectedRepos` calls `refreshData()` (directly at `:1153`, or through `scopeChanged`), which calls `loadOngoing()` with the new selection. The start-up write is followed by the first `refreshData()` (`:442`). The stored scope is built from the same `selectedRepos()` value that the request used, so array order cannot cause a permanent mismatch. `app.daily.spec.ts:117-134` asserts four things: the loading state, the disabled copy button, the gate opening after `loadOngoing`, and the gate closing after a selection change. | The gate does not get stuck after a selection change |
| Readiness gate: race with an older `loadOngoing` | Read `loadOngoing` (`app.ts:811-851`) | Each call increments `ongoingEpoch`. A superseded call either returns inside the paging loop (`:827`) or skips the assignment block (`:841`). An older response therefore cannot overwrite newer rows or set `ongoingScope` to an older selection, and only the most recently started call can open the gate. No unit test covers the race; this result comes from reading the code. | An older response cannot open the gate for the wrong selection or leave it stuck |
| Readiness gate: residual cases that are not regressions of A4 | Read `loadOngoing`'s catch (`:848-850`), `poll()` (`app.ts:2023-2027`), `routeChanged` (`app.ts:1112-1121`) and `scopeChanged` | (1) If the latest `loadOngoing` fails, `ongoingScope` keeps the earlier selection. Today then stays on "Reading today's work…" with the error banner until something triggers a reload: a journal snapshot change, a date rollover, a selection change, or a page reload. Navigating to `/daily` does not call `loadOngoing`. Before A4, the same failure silently showed an incomplete Today, which is what QA-001 rejected, and the spec requires Today to stay unavailable until its inputs load. (2) `dailyData` is not tied to a selection, and `scopeChanged` does not clear it. If the new Ongoing rows arrive before the new digest, Today and the copy briefly combine the new board with the previous selection's `today` items, and Yesterday shows the previous selection until `loadDaily` returns. This was already true in A1, and A4 did not change it. | Not raised as findings: a recheck raises no new suggestions, and neither case is a regression of the A4 diff. Recorded for the orchestrator. |
| Duplication and complexity in the A4 diff | Read `daily.ts` | `lineFor` adds about 20 lines and one helper, `distinctTickets` (`:21-25`). Like the existing `ticketIds` used for copying (`:120-125`), `distinctTickets` removes duplicates by ticket ID only, while the board compares provider, scope and ID (`board.ts:70-72`). Two tickets with the same ID from different providers would therefore merge on a Today line. That is an edge case, and no defect was observed. The client compares `Date.parse` values, which have millisecond precision, before falling back to the ID. The server sorts by the full-precision `OccurredAt` (`DailyService.cs:79`). Two entries of one change recorded within the same millisecond could therefore be ordered by ID rather than by time. | No regression findings. Both notes are below the threshold for a recheck and are not raised. |
| E2E and docs | Read the Daily `open` helper (`dashboard/e2e/dashboard.spec.ts`, Daily describe) and the Daily section of `docs/dashboard-validation.md` | `open` waits until `copy-daily` is enabled. In the app that means `dailyTodayView()` is non-null, so the helper waits on the gate itself. The doc records that the earlier packaged 36/36 could not be reproduced before the gate. | No defect found. QA (A5) owns the e2e runs, so I did not repeat them. |
| Architecture | Compared with `docs/architecture.md` | The line rule stays in the client's `daily.ts`, as the design says: the server supplies `today` and the client builds the lines. A4 adds no endpoint and changes no server code. | No drift |
| Formatting and .NET checks | not run | A4 changed no .NET file, and QA runs builds concurrently. | Not applicable |

## Steady-state cost and bounds

A4 adds no timer, poll, cache, request or saved state. The rows below restate the round-1 answers ([analysis-001.md](analysis-001.md#steady-state-cost-and-bounds)) for the parts A4 touched.

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Client poll (`app.ts`, every 2 s); the `dailyTodayView` computed signal | source observation | Unchanged by A4. Each idle interval makes one `GET /api/catalog` request, and the Daily view does no work while the journal snapshot and the date stay the same. `dailyTodayView` is recomputed only when its input signals change. `lineFor` is a pure function that runs once per listed change card over today's entries, so its cost is the number of cards times the number of today's entries; both are limited to one day and the board. Hypothesis, not measured: this cost is negligible next to the requests. |
| Cache lifetime versus interval and refresh duration | `ongoingScope`, `activityScope`, `dailyData` | source observation | A4 adds no cache. `ongoingScope` only records which selection has loaded and is replaced on each successful `loadOngoing`. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `ongoingScope` (one JSON string); the Today lines | source observation | Bounded: one string per App instance. The lines are rebuilt from the current inputs on every computation. |
| Repeated full reads where a change check would do | `loadOngoing`, `loadDaily` | source observation | Unchanged by A4. It adds no reads; the gate only delays rendering. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | none in the A4 diff | source observation | Not applicable: A4 starts no external tools. The e2e `dotnet` call reviewed in round 1 is unchanged. |

## Findings

### ANALYSIS-041: A change card's Today line now drops observations recorded today for its own tickets when they were logged without a change

- Severity: warning
- Related task / requirement: 2.1; design Decision 3; spec "Today and blockers" ("A Today card with observations recorded today SHALL show its latest recorded status and the summary of its latest observation today"; "A Today line SHALL contain only observations recorded today for that card's own ticket or change") and "Copy for daily"
- Locations:
  - `dashboard/src/daily.ts:33-39`: the `entry.change === card.title` filter
  - `dashboard/src/board.ts:105-108`: a change card's tickets come from rows whose history includes the change
  - `dashboard/src/board.ts:138-143`: such rows produce no ticket card
  - `src/WorkLog/Dashboard/TimelineService.cs:55`: a row's `changes` cover its whole history
- Observation: a ticket row whose history includes an active change gets no card of its own; the board shows the ticket on the change card instead. Suppose that ticket is logged today without a change, for example a follow-up fix on DEP-7 while change `alpha` is still active. The entry goes into the DEP-7 `today` item with `change: null`. A4's filter keeps only entries whose `change` equals the card's change, so no card uses this entry. I confirmed this with a throwaway Vitest probe kept outside the repository. It imports `buildBoard`, `dailyToday` and `dailyText` from `dashboard/src` at the reviewed identity.
  1. Setup: `alpha` is In progress, its last observation was two days ago, the row for DEP-7 has `changes: ["alpha"]`, and today's DEP-7 item has a primary entry that is `in_progress` with `change: null` and the summary "Fixed DEP-7 regression". Result: the `alpha` card is listed with `line: null`, and the copied Today section reads `- DEP-7`, with no status or summary. Before A4, the `alpha` card's line was the DEP-7 item, so the summary was shown and copied. This is a behavior regression in the A4 diff.
  2. Same setup, but `alpha` is quiet (its last observation was 20 days ago). Result: Today shows "Nothing recorded", although DEP-7 has an observation today. This also happened before A4, because the board decides which cards are listed, so it is not a regression. It shows the same gap from the listing side.

  The spec's wording ("that card's own ticket or change") allows these entries, and the board presents DEP-7 as alpha's ticket. Design Decision 3 now says "whose `change` equals `card.title`". The code therefore follows the design text, and the gap comes from the rule that ANALYSIS-001 proposed.
- Proposed improvement and benefit: two options; the orchestrator needs to choose, because design Decision 3 and the spec wording disagree on this case.
  - Option 1, widen the filter: in `lineFor`, also keep an entry with `entry.change === null` when one of its tickets is on the card (`card.tickets`, compared by provider, scope and ID, as the board's `ticketKey` does). Entries of another change are still left out, so ANALYSIS-001 case (a) is unaffected. Each entry still belongs to one item, so no line counts anything twice. If a ticket is shown on two change cards, its change-less entry appears on both, which fits "that card's own ticket". Benefit: Today and the copied daily show the work recorded today on a change card's ticket, instead of a bare ticket ID.
  - Option 2, document the rule: keep the current filter and add to design Decision 3 and the spec that observations of a card's tickets logged without a change are not shown under Today. This accepts that they are missing from the daily copy.
- Behavior-preservation risks: option 1 changes lines only for change cards that have entries logged today without a change for one of their tickets. Ticket and ad-hoc cards are unchanged. The quiet case (2) would still not be listed, because listing comes from the board's change activity, which is keyed by change name (`DashboardJournal.cs:60-80`). Fixing that would change the board, which is outside this change unless the orchestrator wants it.
- Evidence type: source observation, confirmed by a throwaway unit probe (not committed)
- Verification / measurement plan: add a Vitest case to `daily.spec.ts`. Setup: an `alpha` card whose board rows include DEP-7 with `changes: ["alpha"]`, and a today item for DEP-7 whose primary entry has `change: null` and ticket DEP-7. Expected: the `alpha` line's primary is that entry, its status is `in_progress`, and the copied line includes the summary. Cases (a) and (b) stay unchanged and keep passing.
- Relationship: regression introduced by the ANALYSIS-001 correction; ANALYSIS-001 itself is verified.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | `head=cd1c0e3 baseline=cd1c0e3 files=19 diff=1406dcd25cc1 untracked=5/7d3b824553cd` | From reading `dashboard/src/daily.ts:31-52`. Wrong attribution, now closed: a change card's line is built only from today's entries (`today` items hold only entries in `[before, tomorrow)`, `DailyService.cs:37,74`) whose `change` equals the card's change and whose item's repository is one of the card's repositories, so another change's work can no longer appear. Missing observations, now closed: all of today's entries for that change are gathered from every item, both ticket and change-only, so the collapsed observations are complete. Entries are sorted newest first with ties broken by entry ID, the same order the server uses for `others` within an item. Vitest cases (a) `daily.spec.ts:117-125` and (b) `:127-140` exist and assert exactly the acceptance criteria; `npx vitest run src/daily.spec.ts` passed 20 of 20. A selection change cannot leave the readiness gate closed, and an older `loadOngoing` response cannot open it or keep it closed (see the Analysis table). | Verified; regression ANALYSIS-041 raised for entries of a change card's tickets logged without a change |

## Assessment and limitations

ANALYSIS-001 is corrected as specified. A change card's Today line holds only today's entries for that card's change, gathered completely across ticket and change-only items, and both requested Vitest cases exist and pass. The `ongoingScope` gate is sound for selection changes and for races with older `loadOngoing` responses: only the most recently started call can set the scope, and every selection change after start-up starts that call.

One regression in the A4 diff is raised: ANALYSIS-041 (warning). The new rule drops observations recorded today for a change card's own tickets when they were logged without a change, which A1 showed. It needs an orchestrator decision between widening the filter and documenting the omission, because design Decision 3 and the spec wording disagree on this case.

Two remaining gate behaviors are recorded in the Analysis table and not raised, because a recheck raises no new suggestions and neither is a regression of the A4 diff:
- A failed latest `loadOngoing` leaves Today on its loading message until the next reload trigger.
- The digest is not tied to the selection, so a selection change can briefly pair the new board with the previous digest.

The orchestrator may assign either one.

Not performed: Angular unit tests through `ng test`, .NET tests, e2e and formatting. These belong to QA (A5), which runs concurrently. I ran only `daily.spec.ts` directly through Vitest, plus a throwaway probe spec kept in the system temp directory outside the repository. Vitest's normal cache under `dashboard/node_modules` is the only output inside the repository. No performance measurement was made; the steady-state statements about `lineFor` are hypotheses. Claude Code and Codex parity does not apply, because A4 changed only the dashboard, which is client-neutral.

The client refused the write of this report; the text above is returned inline for the orchestrator to save.
