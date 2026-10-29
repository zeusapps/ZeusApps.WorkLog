# Findings: add-daily-prep

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

Advisory marker; resume and hand-off rules are in the [workflow](../../../../.agents/workflows/openspec-delegation.md#mixed-client-operation).

- Client / version: Claude Code 2.1.312
- Session identity: 6bdb0acb-6155-4fed-bab3-f1e38a98877c
- Since (UTC): 2026-10-29T08:34Z
- Previous owner: none

## Scope decisions

- 2026-10-29, prerequisite check: `journalSnapshot` is in the catalog (`SourceCatalog`), `dashboard/src/links.ts` exports `ticketUrl`, and `dashboard/src/board.ts` exports `buildBoard`, `byPriority`, and `isQuiet`. `add-ongoing-board` is archived.
- 2026-10-29, planning update before apply (user-approved in exploration): Today lines show today's recorded status and latest summary, matched to board cards (ticket card by row id, change card by repository and change name, ad-hoc card none). Reason: the user's stand-up is with US clients near the end of their workday, so Today is half the report. Overlap between Yesterday and Today is intended. Agent-written digests are the separate follow-up `add-day-digests`, which modifies `daily-preparation` after this change is archived.
- 2026-10-29, A1 developer deviations, triaged by the orchestrator for reviewers to check:
  1. `dailyToday` takes a fourth parameter `{ zone, order }`, because `isQuiet` needs the timezone and `byPriority` needs the saved order. Accepted; design Decision 3 updated.
  2. Copied change-only titles use the raw change name, following the normative plain-text example, while the view humanizes. Design Decision 4's text contradicted its example. Accepted; Decision 4 text updated.
  3. Open copy-format details: plain text with links appends `ID (url)`; several tickets are joined with ` + `; newlines in summaries become spaces; an unlinked line shows its summary once; an empty section copies as `- Nothing recorded`; Markdown puts blank lines after headings and between sections; a Today card with tickets copies as its ticket IDs, else its title. Accepted as within the design's latitude.
  4. An ad-hoc Today card has a null line (Decision 3) and shows its title and repository without status. Matches the design.
  5. A `?day=` the server rejects with 400 falls back to the last recorded day, replaces the URL, and shows a notice. Accepted; the design is silent.
  6. Route changes may save the existing `lastRoute` preference with `?day=` stripped; the view never saves `board.order` and saves nothing on render, refresh, or day stepping. Accepted; consistent with Decision 5.
  7. The e2e fixture gains the `daily-demo` repository with fixed past dates, and `tools/WorkLog.Smoke` gains a `--dashboard-log` mode that logs one observation while the dashboard runs. Accepted (fixture-only).
  8. `TimelineService.Project` gains an optional `pageSize` (default 50) so the digest takes all rows. Accepted.
  9. Developer evidence: `dotnet test` 374/374; `npm test` 231/231; e2e against Release 35/36 with the pre-existing declared-store scenario failing when Node is present (QA-007 of `fix-dashboard-reliability`); packaged e2e with `WORKLOG_DASHBOARD_NO_NODE=true` 36/36. A `dataLoaded` race found and fixed during e2e. `--repeat-each` on the copy scenario conflicts with the logging scenario's persisted observation, documented in `docs/dashboard-validation.md`. Live installation check not performed.
- Baseline commit: `cd1c0e3` on branch `change/add-daily-prep` (created from `main`). The uncommitted planning edits to this change's artifacts and the untracked `openspec/changes/add-day-digests/` are orchestrator files, not implementation.

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A1 implementation | developer | Claude Code / role default | none: role default | tasks 1.1–3.2; baseline `cd1c0e3` | implementation paths only | none | done 2026-10-29, model claude-sonnet-5-5; identity `head=cd1c0e3 baseline=cd1c0e3 files=19 diff=e68f3c756a4e untracked=5/5c2041d10040` (paths: src/WorkLog tests tools/WorkLog.Smoke dashboard/src dashboard/e2e README.md docs/dashboard-validation.md docs/architecture.md), confirmed by the orchestrator; the developer's own run without docs/architecture.md gave files=18 with the same untracked set |
| A2 review QA | qa | Claude Code / role default | none: role default | all tasks 1.1–3.2; identity of A1 | `qa-001.md`; screenshots `../screenshots/qa-001-*.png` | QA-001 – QA-040 | done 2026-10-29, model claude-sonnet-5-5; QA wrote its own report and three screenshots; QA-001 new (002–040 unused, retired); status partial: 1.1–2.3 and 3.2 verified, 3.1 not met; identity unchanged before and after; no probe processes left running |
| A4 fix | developer | Claude Code / role default | none: role default | QA-001, ANALYSIS-001; identity of A1 | implementation paths only | none | done 2026-10-29, model claude-sonnet-5-5; identity `head=cd1c0e3 baseline=cd1c0e3 files=19 diff=1406dcd25cc1 untracked=5/7d3b824553cd`, confirmed by the orchestrator; no .NET change; npm 234/234, dotnet 374/374, packaged e2e 36/36 ×3, Release e2e 35/35 with declared-store excluded |
| A5 recheck QA | qa | Claude Code / role default | none: role default | QA-001 + regression; identity of A4 | `qa-002.md`; screenshots `../screenshots/qa-002-*.png` | QA-041 – QA-060 | done 2026-10-29, model claude-sonnet-5-5; QA wrote its own report and one screenshot; QA-001 verified; no new findings (QA-041..060 unused, retired); packaged e2e 36/36 ×3; npm 234/234; dotnet 374/374; identity unchanged |
| A7 fix | developer | Claude Code / role default | none: role default | ANALYSIS-041; identity of A4 | implementation paths only | none | done 2026-10-29, model claude-sonnet-5-5; identity `head=cd1c0e3 baseline=cd1c0e3 files=19 diff=a56cce33925b untracked=5/951d6715a50a`; npm 236/236; packaged e2e 36/36 (one run); no .NET change |
| A8 recheck analysis | code-analyzer | Claude Code / role default | none: role default | ANALYSIS-041 + regression of the A7 diff; identity of A7 | `analysis-003.md` | ANALYSIS-061 – ANALYSIS-070 | done 2026-10-29, model claude-opus-5-5; write refused by the client (reviewer paraphrased the refusal), saved verbatim by the orchestrator under a header stating that case; ANALYSIS-041 verified; no new findings (061–070 unused, retired); identity unchanged |
| A9 regression QA | qa | Claude Code / role default | none: role default | regression of the A7 diff (unit tests, packaged e2e, hygiene); identity of A7 | `qa-003.md` | QA-061 – QA-070 | done 2026-10-29, model claude-sonnet-5-5; QA wrote its own report; no findings (QA-061..070 unused, retired); npm 236/236; packaged e2e 36/36 ×2; hygiene over 34 files passed; identity unchanged |
| A6 recheck analysis | code-analyzer | Claude Code / role default | none: role default | ANALYSIS-001 + regression of the A4 diff; identity of A4 | `analysis-002.md` | ANALYSIS-041 – ANALYSIS-060 | done 2026-10-29, model claude-opus-5-5; write refused by the client (same message as A3), saved verbatim by the orchestrator under a header stating that case; ANALYSIS-001 verified; ANALYSIS-041 new (042–060 unused, retired); identity unchanged before and after |
| A3 review analysis | code-analyzer | Claude Code / role default | none: role default | all tasks 1.1–3.2; identity of A1 | `analysis-001.md` | ANALYSIS-001 – ANALYSIS-040 | done 2026-10-29, model claude-opus-5-5; write refused by the client ("Subagents should return findings as text, not write report files. Include this content in your final response instead."), saved verbatim by the orchestrator under a header stating that case; ANALYSIS-001..007 new (008–040 unused, retired); identity unchanged before and after |

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ANALYSIS-001 | [analysis-001](analysis-001.md#analysis-001-a-change-cards-today-line-can-show-another-changes-work-and-can-drop-observations-recorded-today) | 2.1; Today and blockers, Copy for daily | warning | yes: Today line can carry another change's facts and omit today's observations, contrary to the spec | verified | orchestrator | [analysis-002](analysis-002.md#re-verification) |
| QA-001 | [qa-001](qa-001.md#qa-001-today-is-rendered-and-copyable-before-the-ongoing-inputs-have-loaded-and-the-daily-e2e-scenarios-fail-intermittently-against-the-packaged-build) | 2.2, 2.3, 3.1, 3.2; Today and blockers, Copy for daily | warning | yes: Today and copy can show an incomplete list; task 3.1 packaged e2e acceptance not met | verified | orchestrator | [qa-002](qa-002.md) |
| ANALYSIS-041 | [analysis-002](analysis-002.md#analysis-041-a-change-cards-today-line-now-drops-observations-recorded-today-for-its-own-tickets-when-they-were-logged-without-a-change) | 2.1; Today and blockers, Copy for daily | warning | yes: regression from the ANALYSIS-001 fix; the spec ("that card's own ticket or change") requires these entries | verified | orchestrator | [analysis-003](analysis-003.md#re-verification) |
| ANALYSIS-002 | [analysis-001](analysis-001.md#analysis-002-the-three-recorded-day-lookups-query-the-database-again-for-data-the-service-has-already-loaded) | 1.1, 1.2 | suggestion | no: severity gate | deferred | none | n/a |
| ANALYSIS-003 | [analysis-001](analysis-001.md#analysis-003-repositoryclause-was-added-but-read-and-activity-keep-their-own-copies) | 1.1 | suggestion | no: severity gate | deferred | none | n/a |
| ANALYSIS-004 | [analysis-001](analysis-001.md#analysis-004-apidaily-assumes-before-is-a-local-midnight-but-computes-every-other-boundary-itself) | 1.2 | suggestion | no: severity gate (bundled client always sends a local midnight) | deferred | none | n/a |
| ANALYSIS-005 | [analysis-001](analysis-001.md#analysis-005-endpoint-consistency-a-made-up-period-for-timezone-validation-and-no-snapshot-pinning) | 1.2, 2.2 | suggestion | no: severity gate (mismatch clears within one poll) | deferred | none | n/a |
| ANALYSIS-006 | [analysis-001](analysis-001.md#analysis-006-the-unlinked-and-ad-hoc-rules-and-the-no-summary-label-are-repeated-and-the-yesterday-title-rule-differs-from-the-rest) | 1.2, 2.1, 2.2 | suggestion | no: severity gate | deferred | none | n/a |
| ANALYSIS-007 | [analysis-001](analysis-001.md#analysis-007-cost-of-each-refresh-on-daily-performance-hypothesis) | 1.2, 2.2 | suggestion | no: unmeasured hypothesis | deferred | none | n/a |

## History

- 2026-10-29, ANALYSIS-001: gated (warning that breaks the "Today and blockers" and "Copy for daily" acceptance). Clear fix proposed by the analyzer (build a change card's line from today's entries whose `change` equals the card's change). Fix assignment waits for QA-001 so one developer round covers both reviews.
- 2026-10-29, QA-001: gated (Today and its copy render before the Ongoing ticket rows load, so they can omit ticket cards; packaged e2e failed in three full runs, so task 3.1 acceptance and the developer's 36/36 claim are not reproduced). Clear fix: gate Today and copy on the Ongoing inputs, make the e2e wait for the settled state, re-run packaged e2e repeatedly, correct `docs/dashboard-validation.md`.
- 2026-10-29, A4 assigned to the developer: QA-001 and ANALYSIS-001 in one round (cycle 1 of 2). Rechecks: QA rechecks QA-001 plus regression; the analyzer rechecks ANALYSIS-001.
- 2026-10-29, A4 developer result: QA-001 fixed with an `ongoingScope` readiness gate (Today shows a loading state and copy is disabled until the Ongoing inputs for the selection load) and an e2e `open` helper that waits for copy to be enabled. ANALYSIS-001 fixed by building a change card's line from today's entries of that change; the old matching test was rewritten because it encoded the history-wide rule. The developer's earlier packaged "36/36" (A1) was not reproducible before this fix; `docs/dashboard-validation.md` now says so.
- 2026-10-29, planning text updated by the orchestrator from the developer's proposal: design Decision 3 (change-card line from today's entries of that change; Today and copy wait for the Ongoing inputs) and spec "Today and blockers" (lines carry only the card's own observations; Today and copy unavailable until the board inputs load; two new scenarios).
- 2026-10-29, A5 and A6 assigned: rechecks of QA-001 and ANALYSIS-001 plus regression at identity `diff=1406dcd25cc1 untracked=5/7d3b824553cd`.
- 2026-10-29, ANALYSIS-001 verified by the orchestrator on the independent evidence of analysis-002 (re-verification table, Vitest cases (a) and (b), 20/20).
- 2026-10-29, ANALYSIS-041: gated (warning; regression from the ANALYSIS-001 fix). Triage: the spec's "that card's own ticket or change" controls, so the analyzer's Option 1 (also include change-less entries carrying one of the card's tickets, compared by provider, scope, and ID) is the expected result; no user decision needed. The orchestrator's narrower design wording was corrected in Decision 3. Fix waits for A5 so cycle 2 covers both reviews. The quiet-card listing gap (case 2) is pre-existing board behavior and out of scope.
- 2026-10-29, QA-001 verified by the orchestrator on the independent evidence of qa-002 (packaged e2e 36/36 in three full runs, live probe of the loading gate, docs match observation).
- 2026-10-29, A7 assigned to the developer: ANALYSIS-041 (cycle 2 of 2). Recheck: the analyzer rechecks ANALYSIS-041; QA runs a light regression (unit tests and packaged e2e).
- 2026-10-29, ANALYSIS-041 verified by the orchestrator on the independent evidence of analysis-003 (verification and exclusion cases, ANALYSIS-001 cases unchanged, Vitest 22/22).
- 2026-10-29, analysis-003 recorded, not raised: the local `ticketKey` copy in `daily.ts` (follow-up: export it from `board.ts`), and one change-less multi-ticket entry appearing on both a ticket card's line and a change card's line. The second follows design Decision 3 and the spec ("each card once"); accepted as intended for a personal recap.
- 2026-10-29, analysis-002 recorded two non-regression gate behaviors (a failed latest `loadOngoing` keeps Today loading until the next reload trigger; the digest is not tied to the selection, so a selection change can briefly pair the new board with the previous digest). Not raised as findings; noted for the completion assessment.
- 2026-10-29, ANALYSIS-002..007: suggestions, deferred by the severity gate. Follow-up destination: a later dashboard cleanup change; ANALYSIS-002 would also update design Decision 1, ANALYSIS-007 needs its measurement plan first. Reconsider if the user asks.

## Completion assessment

2026-10-29, orchestrator:
- Current target: `head=cd1c0e3 baseline=cd1c0e3 files=19 diff=a56cce33925b untracked=5/951d6715a50a`. Current role reports on it: [analysis-003](analysis-003.md) and [qa-003](qa-003.md); earlier rounds cover the unchanged parts ([qa-001](qa-001.md), [qa-002](qa-002.md), [analysis-001](analysis-001.md), [analysis-002](analysis-002.md)), and A7 changed only `dashboard/src/daily.ts`, `daily.spec.ts`, and the Vitest count in `docs/dashboard-validation.md`.
- Findings: ANALYSIS-001, QA-001, ANALYSIS-041 verified. ANALYSIS-002..007 deferred as suggestions. No blocking finding open. Two fix cycles used (A4, A7).
- Acceptance evidence: `dotnet test` 374/374 (QA, qa-002; no .NET change since); `npm test` 236/236 (qa-003); packaged e2e with isolated data and `WORKLOG_DASHBOARD_NO_NODE=true` 36/36 in two full runs (qa-003) and three runs on the A4 target (qa-002); real-browser checks and screenshots (qa-001, qa-002). Release-build e2e with Node present still stops at the pre-existing declared-store scenario (QA-007 of `fix-dashboard-reliability`); with it excluded, 35/35 (developer, A4).
- Not performed: live midnight rollover with new-day data (unit test only), state-growth measurement, anything against the live installation.
- Accepted known behaviors (not findings): a failed latest `loadOngoing` keeps Today on its loading message until the next reload trigger; a selection change can briefly pair the new board with the previous digest; one change-less multi-ticket entry can appear on both a ticket card's line and a change card's line.
- Deferred follow-ups: ANALYSIS-002..007; export `ticketKey` from `board.ts` (analysis-003).
- Hygiene: qa-003 passed over 34 files; the orchestrator viewed the four screenshots (fixture data only) and searched the files written after qa-003 (`analysis-003.md`, `findings.md`) with no hits.
- Review markers: none in the change root.
- Commit: `308eba0` on `change/add-daily-prep` (implementation and change artifacts); the `add-day-digests` proposal is the separate commit `981b62a`.
