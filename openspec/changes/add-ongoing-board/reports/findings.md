# Findings: add-ongoing-board

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

Advisory marker; resume and hand-off rules are in the [workflow](../../../../.agents/workflows/openspec-delegation.md#mixed-client-operation).

- Client / version: Claude Code 2.1.312
- Session identity: 1570e404-00dd-4689-9952-af0e082ed1e3
- Since (UTC): 2026-10-28T21:00Z
- Previous owner: none

## Scope decisions

- 2026-10-28, prerequisite check: `add-ticket-and-repository-links` is archived. The quiet indication (`dashboard/src/quiet.ts`), ticket links (`dashboard/src/links.ts`), and `tickets` on client rows exist. No acceptance change.
- 2026-10-28, coordination: the sequencing note asking `add-daily-prep` to adopt the shared priority order is already recorded in that change (commit `607df5f`), which now consumes `buildBoard` and `byPriority` from `dashboard/src/board.ts`. This change must keep those exports.
- 2026-10-28, A1 developer deviations, accepted provisionally by the orchestrator for reviewers to check:
  1. `EntryFactory.Prepare` rejects new entries with neither a change nor a ticket, so ad-hoc cards (and extracted-ID-only cards) arise only from legacy journal rows. Tests and the e2e fixture insert such rows via SQLite. No behavior changed to work around it. Planning note for the user.
  2. Order pruning on save keeps ranks of every active catalog change, including those outside the current repository selection; other off-board keys are dropped first. This differs from the spec's literal "cards no longer on the board" and needs a user decision or spec update.
  3. Change cards are withheld until `/api/activity/changes` has loaded for the current selection, to avoid a flash of everything in Proposed.
  4. Change-grouped rows without tickets whose change is neither active nor archived, and the nameless bucket row, produce no card (Decision 2).
  5. Quiet chip only on In-progress change cards; "No recorded activity" chip kept on any change card without activity.
  6. Stats row: "Other ongoing work" became "In progress" (board count); "Recent work items" now says 7 days.
  7. `App.changeRow()` removed, with its one `app.spec.ts` assertion.
  8. Existing tests adapted: `.ongoing-panel` → `.board`, e2e heading "Ongoing board", History row count 68 → 74 (fixture gained `board-demo`), `app.reviews.spec` sets the activity scope.
  9. With an active filter, Move up/down skips hidden cards and ranks the whole column.
  10. Theme e2e scenario writes screenshots when `WORKLOG_BOARD_SCREENSHOT_DIR` is set.
  11. Developer evidence: `dotnet test` 357/357; `npm test` 178/178; e2e 30/30 with the pre-existing declared-store scenario (QA-007 from `fix-dashboard-reliability`) excluded, and 31/31 with `WORKLOG_DASHBOARD_NO_NODE=true`. Packaged e2e not run. Drag and drop not exercised in a real browser. One transient "reading positions stay bounded" failure that passed on rerun.
- Baseline commit: `607df5f` on branch `change/add-ongoing-board` (created from `main`).

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A1 implementation | developer | Claude Code / role default | none: role default | tasks 1.1–4.2; baseline `607df5f` | implementation paths only | none | done 2026-10-28, model claude-sonnet-5-5; identity `head=607df5f baseline=607df5f files=19 diff=8af72419df0b untracked=6/6493d633bf13` (paths: src/WorkLog tests tools/WorkLog.Smoke dashboard/src dashboard/e2e README.md docs/dashboard-validation.md openspec/changes/add-ongoing-board/screenshots), confirmed by the orchestrator |
| A2 review QA | qa | Claude Code / role default | none: role default | all tasks 1.1–4.2; identity of A1 | `qa-001.md`; screenshots `../screenshots/qa-001-*.png` | QA-001 – QA-040 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report; QA-001..004 new (005–040 unused, retired); packaged e2e 31/31; real-browser drag and drop checked; identity tracked diff unchanged, only QA's own screenshot added (`files=20 untracked=7/b6c6131afc68`); a stray `python3.13` process from QA was left running (stop refused to the subagent), surfaced to the user |
| A3 review analysis | code-analyzer | Claude Code / role default | none: role default | all tasks 1.1–4.2; identity of A1 | `analysis-001.md` | ANALYSIS-001 – ANALYSIS-040 | done 2026-10-28, model claude-opus-5-5; write refused by the client ("Subagents should return findings as text"), saved by orchestrator under a header stating that case; ANALYSIS-001..009 new (010–040 unused, retired); identity unchanged |

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ANALYSIS-001 | [analysis-001](analysis-001.md#analysis-001-a-change-present-in-several-sources-yields-cards-with-identical-keys) | 2.1–3.3; Board priority order | warning | yes: duplicate keys break reordering and tracking | verified | orchestrator | [analysis-002](analysis-002.md#re-verification) |
| ANALYSIS-002 | [analysis-001](analysis-001.md#analysis-002-a-full-500-entry-order-can-push-preferences-past-the-64-kib-cap) | 2.2, 3.1; Bounded dashboard preferences | warning | yes: breaks the storage-limit requirement | verified | orchestrator | [analysis-004](analysis-004.md#re-verification) (with ANALYSIS-061) |
| ANALYSIS-003 | [analysis-001](analysis-001.md#analysis-003-quiet-indication-rule-lives-in-the-template-not-in-the-board-model) | 2.1, 3.2; add-daily-prep Decision 3 | warning | yes: the board contract add-daily-prep relies on lacks the quiet fact | verified | orchestrator | [analysis-002](analysis-002.md#re-verification) |
| QA-001 | [qa-001](qa-001.md) | 2.2; Bounded dashboard preferences (deviation 2) | warning | yes: implementation differs from spec wording | verified | orchestrator | [qa-002](qa-002.md) |
| QA-002 | [qa-001](qa-001.md) | 2.1; Ongoing board expiry | suggestion | no: severity gate | deferred | none | n/a |
| QA-003 | [qa-001](qa-001.md) | 2.1; Ongoing board ad-hoc cards (deviation 1) | suggestion | no: planning note | verified | orchestrator | orchestrator: spec and design note (planning text) |
| QA-004 | [qa-001](qa-001.md) | 2.2 | suggestion | no: duplicate | duplicate | none | see ANALYSIS-002 |
| ANALYSIS-004 | [analysis-001](analysis-001.md#analysis-004-orphaned-helper-and-styles-from-the-removed-ongoing-ui) | 3.2 | suggestion | no: severity gate | deferred | none | n/a |
| ANALYSIS-005 | [analysis-001](analysis-001.md#analysis-005-the-unlinked-endpoint-decodes-the-whole-journal-history-to-return-7-days) | 1.1 | suggestion | no: severity gate | deferred | none | n/a |
| ANALYSIS-006 | [analysis-001](analysis-001.md#analysis-006-deviation-2-keeps-off-selection-change-ranks-but-drops-off-selection-ticket-and-ad-hoc-ranks) | 2.2, 3.3 | suggestion | no: related to QA-001 | duplicate | none | see QA-001 |
| ANALYSIS-007 | [analysis-001](analysis-001.md#analysis-007-a-third-ticket-identity-key-with-a-weaker-separator) | 2.1 | suggestion | no: severity gate | deferred | none | n/a |
| ANALYSIS-008 | [analysis-001](analysis-001.md#analysis-008-dragover-writes-a-new-drop-mark-object-on-every-event) | 3.3 | suggestion | no: severity gate | deferred | none | n/a |
| ANALYSIS-009 | [analysis-001](analysis-001.md#analysis-009-architecture-reference-does-not-mention-the-new-endpoint) | 1.1, 4.2 | suggestion | no: severity gate | deferred | none | n/a |
| ANALYSIS-041 | [analysis-002](analysis-002.md#analysis-041-a-merged-change-card-takes-its-column-task-count-and-documents-from-whichever-copy-sorts-first) | 2.1, 3.2; Ongoing board | warning | yes: column from copy order | verified | developer (A7) | [analysis-003](analysis-003.md#re-verification) |
| ANALYSIS-042 | [analysis-002](analysis-002.md#analysis-042-the-copy-chooser-tracks-copies-by-variant-alone-and-keeps-a-stale-copypath) | 3.2 | suggestion | no: severity gate | deferred | none | n/a |
| QA-041 | [qa-002](qa-002.md) | 4.2 | suggestion | yes: task 4.2 requires recorded results | verified | developer (A7) | [qa-003](qa-003.md), [qa-004](qa-004.md) |
| ANALYSIS-061 | [analysis-003](analysis-003.md#analysis-061-the-client-fits-the-order-to-its-own-byte-count-which-can-be-smaller-than-the-servers-count) | 2.2; Bounded dashboard preferences | warning | yes: fitted save can be rejected | verified | developer (A10, user-approved extra cycle) | [analysis-004](analysis-004.md#re-verification) |

## Finding details and history

- 2026-10-28: all findings triaged. Proposed resolutions put to the user before a single fix round:
  - ANALYSIS-001: one card per change key; copies via the existing copy chooser.
  - ANALYSIS-002 (canonical; QA-004 duplicate): lower the order cap to 200, shorten ticket keys to a row-hash prefix, add a full-budget size test, and amend the spec's 500.
  - ANALYSIS-003: export a pure `isQuiet` from `board.ts` and use it in the template.
  - QA-001 (canonical; ANALYSIS-006 related): keep the implementation, and amend the spec to state that ranks of active changes outside the selection are kept and short-lived ticket and ad-hoc ranks outside the selection may be dropped.
  - QA-003: keep ad-hoc cards as legacy support and note it in the spec and design.
- 2026-10-28: user approved the proposed resolutions. Orchestrator amended the spec (200 cap, budget fit, pruning rule, one card per change, legacy note), design Decision 4, task 2.2, and `add-daily-prep` design Risks. ANALYSIS-001, 002, 003 and QA-001 (test only) assigned to the developer as A4; QA-003 resolved by the spec note (verified by orchestrator: planning text only). Recheck: QA for QA-001 and regression, analyzer for ANALYSIS-001..003.
- 2026-10-29: A4 done (claude-sonnet-5-5); identity `head=607df5f baseline=607df5f files=20 diff=6a4edc95043c untracked=7/274b8c8b917f`, confirmed by the orchestrator. ANALYSIS-001..003 and QA-001 → awaiting-verification. Developer noted one unexplained "reorders with the keyboard" e2e failure in NO_NODE mode and stale test counts in `docs/dashboard-validation.md`. Rechecks assigned: A5 QA (`qa-002.md`, QA-041–060), A6 analysis (`analysis-002.md`, ANALYSIS-041–060).
- 2026-10-29: A6 done (claude-opus-5-5); write refused, saved as `analysis-002.md` under the fallback header. ANALYSIS-001, 002, 003 verified by analysis-002. New: ANALYSIS-041 (warning, blocking: merged card's column, tasks and documents depend on copy order; candidate for fix round 2), ANALYSIS-042 (suggestion: chooser tracking and stale `copyPath`), plus an ANALYSIS-002 leftover key-length risk (candidate: prune the order by bytes before save). ANALYSIS-043–060 unused, retired. `add-daily-prep` design now names `isQuiet`. Awaiting A5 QA before fix round 2.
- 2026-10-29: A5 done (claude-sonnet-5-5); QA wrote `qa-002.md`; QA-001 verified; regression passed (dotnet 357, npm 184, e2e 30/30, packaged 31/31, keyboard reorder 11/11). New QA-041 (stale counts and packaged-run note in `docs/dashboard-validation.md`; gated because task 4.2 requires recorded results). QA-042–060 unused, retired.
- 2026-10-29: fix round 2 (A7, last allowed cycle) assigned to the developer: ANALYSIS-041, the ANALYSIS-002 leftover (byte-bounded pruning), QA-041. Recheck: analyzer ANALYSIS-041/002, QA QA-041 plus regression.
- 2026-10-29: A7 done (claude-sonnet-5-5); identity `head=607df5f baseline=607df5f files=20 diff=8d6264af661d untracked=7/a623002741a1`, confirmed by the orchestrator. ANALYSIS-041, the ANALYSIS-002 leftover and QA-041 → awaiting-verification. The validation doc cites a packaged run made on the previous target; QA asked to re-run it. Rechecks: A8 QA (`qa-003.md`, QA-061–080), A9 analysis (`analysis-003.md`, ANALYSIS-061–080).
- 2026-10-29: A9 done (claude-opus-5-5); write refused, saved as `analysis-003.md` under the fallback header. ANALYSIS-041 verified. ANALYSIS-002 leftover partly verified. New ANALYSIS-061 (warning: client byte count is smaller than the server's escaped count, so a fitted document can still be rejected with 413). Two fix cycles reached: ANALYSIS-061 escalated to the user (awaiting-user-decision), recommendation: client-side margin (fit to 60 KiB), one-line change plus a test. ANALYSIS-062–080 unused, retired.
- 2026-10-29: A8 done (claude-sonnet-5-5); QA wrote `qa-003.md`; QA-041 verified; regression passed (npm 189, dotnet 357, e2e 30/30, NO_NODE 31/31, fresh packaged 31/31); hygiene passed; no new findings (QA-061–080 unused, retired); identity unchanged.
- 2026-10-29: user chose the recommended resolution for ANALYSIS-061 (client fits the order to a 60 KiB margin, plus a test with `&` and non-ASCII text measured by the server's escaping). Assigned to the developer as A10, a user-approved extra cycle beyond the two-cycle limit. Recheck: analyzer for ANALYSIS-061 (`analysis-004.md`, ANALYSIS-081–090), QA light regression (`qa-004.md`, QA-081–090).
- 2026-10-29: A10 done (claude-sonnet-5-5); identity `head=607df5f baseline=607df5f files=20 diff=a0c7514b1a9c untracked=7/c84e67b6aec0`, confirmed by the orchestrator. ANALYSIS-061 → awaiting-verification. Rechecks: A11 analysis (`analysis-004.md`), A12 QA (`qa-004.md`).
- 2026-10-29: A12 done (claude-sonnet-5-5); QA wrote `qa-004.md`; npm 190, dotnet 357, e2e 30/30, fresh packaged 31/31; validation doc matches; hygiene passed; no findings (QA-081–090 unused, retired); identity unchanged.
- 2026-10-29: A11 done (claude-opus-5-5); write refused, saved as `analysis-004.md` under the fallback header. ANALYSIS-061 verified (server-encoder probe: fitted body 61,426 server bytes). No findings (ANALYSIS-081–090 unused, retired). Two test-robustness notes (the escaping test's `lastRoute` is overwritten by `router.url`; the 6-byte discrimination margin; `"` and U+007F not modeled) deferred as follow-ups.
- Deferred suggestions (QA-002, ANALYSIS-042, the analysis-004 test notes, ANALYSIS-004, 005, 007, 008, 009): follow-up destination is a later dashboard polish change; reconsider if the board proves noisy or slow.

## Completion assessment

- 2026-10-29: current QA (`qa-004.md`) and analysis (`analysis-004.md`) reports cover target `diff=a0c7514b1a9c untracked=7/c84e67b6aec0`. Every finding is triaged; all blocking findings are verified. Acceptance evidence: dotnet 357, npm 190, e2e 30/30 (pre-existing declared-store QA-007 scenario excluded), packaged NO_NODE e2e 31/31, real-browser drag and drop (qa-001), hygiene passed. Not performed: touch input, a live save near 64 KiB, the copy chooser in a browser. Fix cycles: two, plus one user-approved extra (A10). No `<!-- REVIEW` markers checked yet (archive precondition). Commit: `573a538` on `change/add-ongoing-board` (not pushed).
