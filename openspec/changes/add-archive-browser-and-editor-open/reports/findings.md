# Findings: add-archive-browser-and-editor-open

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

Advisory marker; resume and hand-off rules are in the [workflow](../../../../.agents/workflows/openspec-delegation.md#mixed-client-operation).

- Client / version: Claude Code 2.1.312
- Session identity: 71203fbc-e13e-4788-a000-9f38eec29037
- Since (UTC): 2026-10-28
- Previous owner: none

## Scope decisions

- 2026-10-28, ordering: the user asked to apply `add-ticket-and-repository-links`, but it requires this change's `/api/activity/changes`. The user chose to apply this change first (handoff order 4 before 5).
- 2026-10-28, prerequisite check: `fix-dashboard-reliability` and `add-review-inbox` are archived; `/api/reviews` accepts `scope: "archived"` (`src/WorkLog/Dashboard/DocumentService.Reviews.cs:36`).
- 2026-10-28, A1 developer deviations, accepted provisionally by the orchestrator for reviewers to check:
  1. The activity summary is one window-function query instead of the two queries sketched in Decision 2.
  2. The Playwright recording launcher is a launch-log file selected by `WORKLOG_DASHBOARD_TEST_LAUNCH_LOG`, not an in-memory recorder, so no test-only HTTP endpoint exists.
  3. Leftover comment counts reuse the existing `/api/reviews` call with `scope: "all"` instead of a separate `scope: "archived"` call.
  4. The task 1.1 test lives in `ArchiveAndOpenTests`, not `SourceCatalogTests`.
  5. Search results and review entries are wrapped in rows so the editor action is not nested inside a button.
  6. Cursor's `cursor://file/<path>:<line>:<col>` format is not confirmed by vendor documentation (community reports only); VS Code's is confirmed. Recorded as unconfirmed in `docs/dashboard-validation.md`; open for a user decision.
  7. Developer evidence: the Release-build e2e fails only at the known QA-007 declared-store scenario (CLI on PATH); 20/20 others pass; packaged e2e 21/21.
- Baseline commit: `c53e647` on branch `change/add-archive-browser-and-editor-open` (created from `main`).

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A1 implementation | developer | Claude Code / role default | none: role default | tasks 1.1–2.2, 3.1, 3.3 (3.2 reserved for the user's live check); baseline `c53e647` | implementation paths only | none | done 2026-10-28, model claude-sonnet-5-5; identity `head=c53e647 baseline=c53e647 files=24 diff=5d5c0e7ce0dd untracked=5/c6a442c792da` (paths: README.md dashboard/e2e dashboard/src docs/architecture.md docs/dashboard-validation.md docs/installation.md src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard), confirmed by the orchestrator |
| A2 review QA | qa | Claude Code / role default | none: role default | tasks 1.1–2.2, 3.1, 3.3; identity of A1 | `qa-001.md`; screenshots `../screenshots/qa-001-*.png` | QA-001 – QA-040 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report; QA-001 new (002–040 unused, retired); deviations 1–5 judged equivalent; identity unchanged |
| A3 review analysis | code-analyzer | Claude Code / role default | none: role default | tasks 1.1–2.2, 3.1, 3.3; identity of A1 | `analysis-001.md` | ANALYSIS-001 – ANALYSIS-040 | done 2026-10-28, model claude-opus-5-5; write refused, saved by orchestrator under a header quoting the refusal (one absolute repository path in the return block normalized to repository-relative); ANALYSIS-001..006 new (007–040 unused, retired); deviation 1 measured acceptable; identity unchanged |
| A4 fix round 1 | developer | Claude Code / role default | none: role default | ANALYSIS-001, ANALYSIS-002; identity of A1 | implementation paths only | none | done 2026-10-28, model claude-sonnet-5-5; identity `head=c53e647 baseline=c53e647 files=24 diff=4735315bfc54 untracked=5/cdf4bc4eaa5a`, confirmed by the orchestrator; reported `DevInstallTests.PreflightRefusesStaleUnmarkedAndCorruptPackagesAndChangesNothing` failing (passed in qa-001) and that the symlink test returns early when links cannot be created |
| A5 recheck QA | qa | Claude Code / role default | none: role default | ANALYSIS-002 (functional), ANALYSIS-001 (functional) plus regression; DevInstallTests investigation; identity of A4 | `qa-002.md`; screenshots `../screenshots/qa-002-*.png` | QA-041 – QA-060 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report; ANALYSIS-001/002 verified (functional, Chromium); mutation run proves the link test exercises the check; DevInstallTests failure environmental (fails at clean baseline too); no new findings (041–060 unused, retired); side effect: worktree removal emptied ignored `dashboard/node_modules`, restored by QA with `npm ci` and confirmed present by the orchestrator; identity unchanged |
| A7 validation docs | developer | Claude Code / role default | none: role default | tasks 3.2 (record the user's live observation), 3.3 (packaged e2e rerun); identity of A4 | `docs/dashboard-validation.md` only | none | done 2026-10-28, model claude-sonnet-5-5; packaged e2e 22/22; identity `head=c53e647 baseline=c53e647 files=24 diff=5143e137bb4a untracked=5/cdf4bc4eaa5a`, confirmed by the orchestrator |
| A8 light review QA | qa | Claude Code / role default | none: role default | docs-only A7 delta; identity of A7 | `qa-003.md` | QA-061 – QA-070 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report; no findings (061–070 unused, retired); hygiene passed over the doc and the change root; identity unchanged |
| A9 light review analysis | code-analyzer | Claude Code / sonnet | docs-only target: cost-efficient override permitted by the workflow | docs-only A7 delta; identity of A7 | `analysis-003.md` | ANALYSIS-061 – ANALYSIS-070 | done 2026-10-28, model claude-sonnet-5-5; write refused, saved by orchestrator under a header quoting the refusal; ANALYSIS-061..064 new (065–070 unused, retired); identity unchanged |
| A10 fix round 2 | developer | Claude Code / role default | none: role default | user-reported Archive view padding/margins (task 2.1); ANALYSIS-061; identity of A7 | implementation paths only | none | done 2026-10-28, model claude-sonnet-5-5; `styles.css` archive panel padding/spacing and the ANALYSIS-061 rewording; identity `head=c53e647 baseline=c53e647 files=24 diff=7a620beeba44 untracked=5/cdf4bc4eaa5a`, confirmed by the orchestrator |
| A11 recheck QA | qa | Claude Code / role default | none: role default | Archive spacing (functional, visual) and ANALYSIS-061 plus regression; identity of A10 | `qa-004.md`; screenshots `../screenshots/qa-004-*.png` | QA-071 – QA-080 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report and six screenshots; Archive spacing verified at 1440 and 480 px; ANALYSIS-061 verified; no new findings (071–080 unused, retired); identity unchanged |
| A12 recheck analysis | code-analyzer | Claude Code / role default | none: role default | ANALYSIS-061 and the CSS delta plus regression; identity of A10 | `analysis-004.md` | ANALYSIS-071 – ANALYSIS-080 | done 2026-10-28, model claude-opus-5-5; write refused (reviewer did not quote the refusal text this round), saved by orchestrator under a header saying so; ANALYSIS-061 verified (analysis side); CSS delta: no findings (071–080 unused, retired); identity unchanged |
| A6 recheck analysis | code-analyzer | Claude Code / role default | none: role default | ANALYSIS-001, ANALYSIS-002 plus regression; identity of A4 | `analysis-002.md` | ANALYSIS-041 – ANALYSIS-060 | done 2026-10-28, model claude-opus-5-5; write refused, saved by orchestrator under a header quoting the refusal; ANALYSIS-001 verified, ANALYSIS-002 verified at source/unit level (browser evidence pending qa-002); no new findings (041–060 unused, retired); symlink probe shows the link test's assertions run here; identity unchanged |

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| QA-001 | [qa-001](qa-001.md) | 3.1; boundary requirement | suggestion | no | deferred | — | follow-up: unit test for `RecordingEditorLauncher.FromEnvironment` with the variable set, unset, and empty (currently verified by inspection) |

| ANALYSIS-001 | [analysis-001](analysis-001.md) | 1.3; Local data access boundary | warning | yes: launch-boundary safety; a `.md`-named link to a `.cmd`/`.exe` inside a source would be shell-executed by the `system` preset; clear in-scope fix | verified | A4 developer | [analysis-002](analysis-002.md) and [qa-002](qa-002.md): 400 and zero launches for all presets through a `.md`→`.cmd` link; mutation run fails the test without the check; identity diff=4735315bfc54 |
| ANALYSIS-002 | [analysis-001](analysis-001.md) | 2.2; Open documents in a local editor (current reading position) | warning | yes: source-view position is wrong after wrapped lines, breaking the agreed reading-position acceptance; clear in-scope fix | verified | A4 developer | [qa-002](qa-002.md) e2e (Chromium): wrapped paragraph scrolled 8 rows records `:3:1`; [analysis-002](analysis-002.md) source/unit; identity diff=4735315bfc54 |
| ANALYSIS-003 | [analysis-001](analysis-001.md) | 2.1 | suggestion | no | deferred | — | follow-up: index activity by change name once per activity update (measured 9 ms at 600×4,799) |
| ANALYSIS-004 | [analysis-001](analysis-001.md) | 1.2 | suggestion | no | deferred | — | follow-up: shared `RepositoryFilter` helper in `DashboardJournal`; reconsider when `add-ticket-and-repository-links` adds a third copy |
| ANALYSIS-005 | [analysis-001](analysis-001.md) | 2.1, 2.2 | suggestion | no | deferred | — | follow-up: one editor-select template and a `documentLabel` helper |
| ANALYSIS-006 | [analysis-001](analysis-001.md) | 3.1 | suggestion | no | deferred | — | follow-up: print a startup line when the test launcher is active; pair with QA-001 |

| ANALYSIS-061 | [analysis-003](analysis-003.md) | 3.3 | warning | yes: the task's required validation record states a contradictory cause for the DevInstallTests failure; clear docs fix | verified | A10 developer | [analysis-004](analysis-004.md) and [qa-004](qa-004.md); identity diff=7a620beeba44 |
| ANALYSIS-062 | [analysis-003](analysis-003.md) | 3.2 | suggestion | no | deferred | — | follow-up: name identity `diff=4735315bfc54` and the opened document in the live record; the restart is implied by the user's report (a running dashboard keeps its payload) |
| ANALYSIS-063 | [analysis-003](analysis-003.md) | 3.3 | suggestion | no | deferred | — | follow-up: label the packaged 22/22 run as "after the fix round" |
| ANALYSIS-064 | [analysis-003](analysis-003.md) | 3.3; deviation 6 | suggestion | no | deferred | — | follow-up: soften the unregistered-handler sentence in `docs/installation.md` to a hypothesis and mark Cursor unconfirmed |

## History

- 2026-10-28, ANALYSIS-001: reported by analysis-001 (measured path-resolution probe). Gated as a safety warning; assigned to A4 to reject non-`.md` canonical targets on the launch path for all presets. Tightening the read path is outside this change's scope and not taken.
- 2026-10-28, ANALYSIS-002: reported by analysis-001 (source observation). Gated: the spec requires the current reading position; assigned to A4.
- 2026-10-28, ANALYSIS-003..006: suggestions, deferred under the severity gate.
- 2026-10-28, tie-break note in analysis-001: equal timestamps are decided by `id DESC` as task 1.2 states; ids are random GUIDs, so the winner is deterministic but not insertion order. Accepted as specified; no finding.

- 2026-10-28, QA-001: reported by qa-001; suggestion, deferred under the severity gate.
- 2026-10-28, task 3.2 live observation (user): the user installed dev build `0.3.9002 dev c53e647 dirty` (identity `diff=4735315bfc54`) on their installation through `install-dev.ps1` Switch (operation record `artifacts/dev-builds/operations/c3b7fc0ae7294601932fa33ddf3b4129.json`; previous 0.3.9001 recorded for rollback) and reported that Open in editor from the reader and the heading pencil action both open VS Code at the expected position. Source-view, Cursor, and System default app were not reported. Cursor's format stays recorded as unconfirmed (deviation 6) unless the user tests it.
- 2026-10-28, ANALYSIS-001, ANALYSIS-002: fixed by A4; verified by analysis-002 and qa-002 on identity `diff=4735315bfc54 untracked=5/cdf4bc4eaa5a`. One fix cycle used.
- 2026-10-28, `DevInstallTests.PreflightRefusesStaleUnmarkedAndCorruptPackagesAndChangesNothing`: fails depending on working-tree mtime ordering (directory newer than the newest included file); qa-002 reproduced the failure on a clean `c53e647` worktree. Pre-existing and outside this change (`tools/WorkLog.DevInstall`); not a finding of this change. Follow-up destination: a separate fix to `add-dev-builds`' staleness test.
- 2026-10-28, deviation 6 / task 1.4 (user decision): the user does not use Cursor and accepts the Cursor preset with its format recorded as unconfirmed by vendor documentation and not live-checked. VS Code's format is vendor-confirmed. Task 1.4 closed on that basis.
- 2026-10-28, user feedback on the live dev build: (1) the Archive view lacks padding and margins — an in-scope task 2.1 presentation defect, routed to the developer after the A8/A9 light reviews finish; (2) the reader shows a change's documents as a horizontal tab row at the top, hard to read with many files; the user suggests a vertical list on the left. The tab row predates this change (present at `c53e647`), so this is a scope question put to the user.
- 2026-10-28, user decision on feedback (2): the vertical document list goes into a separate small change after this one is committed; not in this change's scope.
- 2026-10-28, ANALYSIS-061: gated (inaccurate required validation record); assigned to A10 with the user-reported Archive padding fix. This is the change's second and last fix cycle under the workflow limit. ANALYSIS-062..064: suggestions, deferred.
- 2026-10-28, user-reported Archive spacing (task 2.1): fixed by A10, verified by qa-004 (computed styles and screenshots, fixture) and analysis-004 (selector scoping); identity `diff=7a620beeba44 untracked=5/cdf4bc4eaa5a`. ANALYSIS-061 verified by both. Second and final fix cycle complete; no open blocking findings.

## Completion assessment

- 2026-10-28: all 9 tasks complete. Current reports on the final identity `head=c53e647 baseline=c53e647 files=24 diff=7a620beeba44 untracked=5/cdf4bc4eaa5a`: [qa-004](qa-004.md) and [analysis-004](analysis-004.md), building on qa-001..003 and analysis-001..003 for unaffected areas (rounds 3–4 touched only `styles.css` archive rules and `docs/dashboard-validation.md`).
- Every finding triaged: ANALYSIS-001, 002, 061 verified; QA-001, ANALYSIS-003..006 and 062..064 deferred as suggestions with follow-ups; no blocking finding open; no unapproved scope deferral.
- Acceptance evidence: .NET tests (the one failing DevInstallTests case is pre-existing and environmental), dashboard unit tests 94/94, fixture e2e 21/21 excluding the pre-existing declared-store scenario, packaged e2e 22/22, and the user's live observation of task 3.2 on dev build 0.3.9002, plus the user's confirmation of the Archive spacing on dev build 0.3.9003.
- User decisions: apply before `add-ticket-and-repository-links`; Cursor format accepted as unconfirmed; vertical document list moved to a separate change.
- Hygiene: re-run by the orchestrator over the change root and changed docs after qa-004; only hits are pattern-name lists inside QA reports. No `<!-- REVIEW` markers in the change root.
- Follow-ups outside this change: the DevInstallTests mtime-ordering failure; the QA-007 declared-store e2e scenario; the reader's vertical document list.
- Commit: pending.
