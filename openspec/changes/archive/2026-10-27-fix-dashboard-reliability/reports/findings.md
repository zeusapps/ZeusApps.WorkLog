# Findings: fix-dashboard-reliability

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

Advisory marker; resume and hand-off rules are in the [workflow](../../../../../.agents/workflows/openspec-delegation.md#mixed-client-operation).

- Client / version: Claude Code 2.1.311
- Session identity: eae5a20b-6b19-45bd-b040-24cfe844c709
- Since (UTC): 2026-10-27
- Previous owner: none

## Scope decisions

- 2026-10-27, user decision: add two confirmed calibration findings from the `add-agent-workflow` handoff to this change. QA-006 (skipped-midnight `boundary()`) became task 1.5. ANALYSIS-002 (unchanged document resent every poll) became task 5.6, using a `knownRevision` request field rather than GET with ETag/304 (user choice). ANALYSIS-003 (idle preference rewrites) is covered by tasks 1.2 and 5.3; an idle no-write check was added to 5.3 and 5.5. The proposal, design, work-dashboard spec delta and tasks were updated accordingly. QA-007 (store-fallback browser scenario) stays in the handoff follow-up list.
- 2026-10-27, developer deviations accepted by the orchestrator as within agreed scope, for reviewers to check:
  - `SourceCatalog` reloads `config.json` when its write time changes. Design Decision 5(b) names config changes as an invalidation input, which needs the config to be re-read; this also addresses handoff item ANALYSIS-009.
  - The `openspec context` cache also invalidates on the checkout's `openspec/config.yaml`/`config.yml`; timed-out commands are not cached.
  - Search keeps source-bound problems (enumeration or change-read failures) for sources in the requested repositories, in addition to Decision 4's rules, so a failed in-scope source is not shown as a complete empty search. This follows the spec's "distinguish no matches from incomplete or unavailable sources".
  - The client omits `knownRevision` when the catalog's archived state differs, so archive moves refresh `archived`/`canComment`.
  - `loadDocument` no longer posts the whole document object (including text) on each poll.
- Baseline commit: `bf65aeb` on branch `change/fix-dashboard-reliability`.

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A1 implementation | developer | Claude Code / role default | none: role default | tasks 1.1–6.3 except 6.1; baseline `bf65aeb` | implementation paths only | none | done 2026-10-27, model claude-sonnet-5-5; identity `head=bf65aeb baseline=bf65aeb files=18 diff=73aa0085eaa0 untracked=4/ee2ea065ab53` (paths: src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard/DashboardReliabilityTests.cs dashboard/src dashboard/e2e docs/dashboard-validation.md), confirmed by the orchestrator |
| A2 review QA | qa | Claude Code / role default | none: role default | all tasks, incl. 6.1; identity of A1 | `qa-001.md`; screenshots `qa-001-*.png` | QA-001 – QA-040 | done 2026-10-27, model claude-sonnet-5-5; QA wrote its own report; identity unchanged |
| A3 review analysis | code-analyzer | Claude Code / role default | none: role default | all tasks; identity of A1 | `analysis-001.md` | ANALYSIS-001 – ANALYSIS-040 | done 2026-10-27, model claude-opus-5-5; write refused, saved by orchestrator under a header quoting the refusal; identity unchanged |
| A4 fix round 1 | developer | Claude Code / role default | none: role default | QA-001, ANALYSIS-001 (items 1–2), ANALYSIS-002; identity `diff=73aa0085eaa0 untracked=4/ee2ea065ab53` | implementation paths only | none | done 2026-10-27, model claude-sonnet-5-5; identity `head=bf65aeb baseline=bf65aeb files=19 diff=b77f6ffa0bf9 untracked=4/a29f65ccc20f` (same paths), confirmed by the orchestrator |
| A5 recheck QA | qa | Claude Code / role default | none: role default | QA-001 plus regression | `qa-002.md` | QA-041 – QA-060 | done 2026-10-27, model claude-sonnet-5-5; QA wrote its own report; QA-001 verified, no new findings; identity unchanged |
| A6 recheck analysis | code-analyzer | Claude Code / role default | none: role default | ANALYSIS-001, ANALYSIS-002 plus regression | `analysis-002.md` | ANALYSIS-041 – ANALYSIS-060 | done 2026-10-27, model claude-opus-5-5; write refused, saved by orchestrator; ANALYSIS-001/002 verified; ANALYSIS-041..044 new; identity unchanged |
| A7 fix round 2 | developer | Claude Code / role default | none: role default | ANALYSIS-041, ANALYSIS-043, ANALYSIS-044; identity `diff=b77f6ffa0bf9 untracked=4/a29f65ccc20f` | implementation paths only | none | done 2026-10-27, model claude-sonnet-5-5; identity `head=bf65aeb baseline=bf65aeb files=19 diff=30cbc7228716 untracked=4/b53893c88cdb`, confirmed by the orchestrator |
| A8 recheck QA | qa | Claude Code / role default | none: role default | ANALYSIS-043 functional check plus regression | `qa-003.md` | QA-061 – QA-080 | interrupted 2026-10-27: the subagent stalled (no progress for 600 s, stream watchdog) and returned no report; it left five screenshots `qa-003-*.png` (fixture data only, checked by the orchestrator), no processes or listeners, and an unchanged identity. Allocation retired, not reused |
| A10 recheck QA (replaces A8) | qa | Claude Code / role default | none: role default | ANALYSIS-043 functional check plus regression; identity `diff=30cbc7228716 untracked=4/b53893c88cdb` | `qa-004.md`; screenshots `qa-004-*.png` | QA-081 – QA-100 | done 2026-10-27, model claude-sonnet-5-5; QA wrote its own report; ANALYSIS-043 functionally verified, no new findings; identity unchanged |
| A9 recheck analysis | code-analyzer | Claude Code / role default | none: role default | ANALYSIS-041, ANALYSIS-043, ANALYSIS-044 plus regression | `analysis-003.md` | ANALYSIS-061 – ANALYSIS-080 | done 2026-10-27, model claude-opus-5-5; write refused, saved by orchestrator; ANALYSIS-041/043/044 verified; ANALYSIS-061 new; identity unchanged |

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| QA-001 | [qa-001](qa-001.md) | 1.2; work-dashboard "Invalid timezone" scenario | warning | yes: breaks the agreed scenario (an invalid zone is remembered and every query fails) | verified | A4 developer | [qa-002](qa-002.md), browser UI against packaged build, identity untracked=4/a29f65ccc20f |
| QA-002 | [qa-001](qa-001.md) | 5.4; Decision 7 | suggestion | no | duplicate | — | duplicate of ANALYSIS-003 |
| ANALYSIS-001 | [analysis-001](analysis-001.md) | 5.1, 5.2; Decision 5 risk note; Idle refresh cost | warning | yes, items 1–2 only: Decision 5 made the cheap tier's acceptance depend on this measurement, which shows the per-file canonicalizing walk is the dominant idle cost (746 ms per refresh at 1,280 files) and holds the request gate | verified | A4 developer | [analysis-002](analysis-002.md) on Windows; residual ANALYSIS-041, ANALYSIS-042 |
| ANALYSIS-002 | [analysis-001](analysis-001.md) | 5.1; Idle refresh cost ("at most one round of OpenSpec CLI resolution per source") | warning | yes: a timed-out CLI is respawned on every refresh, unbounding the requirement in that failure mode | verified | A4 developer | [analysis-002](analysis-002.md) |
| ANALYSIS-003 | [analysis-001](analysis-001.md) | 5.4; Decision 7 | suggestion | no | deferred | — | follow-up: byte-bounded or use-ordered document cache; canonical for QA-002 |
| ANALYSIS-004 | [analysis-001](analysis-001.md) | 5.1; Decision 5 | suggestion | no | deferred | — | follow-up: stale-while-revalidate for the expensive tier |
| ANALYSIS-005 | [analysis-001](analysis-001.md) | 5.6; deviation 4 | suggestion | no | deferred | — | follow-up: carry `archived`/`canComment` in the unchanged answer |
| ANALYSIS-006 | [analysis-001](analysis-001.md) | 4.1; deviation 3 | suggestion | no | deferred | — | follow-up: one typed notice list |
| ANALYSIS-007 | [analysis-001](analysis-001.md) | 5.1 | suggestion | no | deferred | — | follow-up: prune CLI cache to current topology |
| ANALYSIS-008 | [analysis-001](analysis-001.md) | 5.1; deviation 1 | suggestion | no | deferred | — | follow-up: keep the config reload error visible until a reload succeeds |
| ANALYSIS-041 | [analysis-002](analysis-002.md) | 5.1, 5.2; ANALYSIS-001 fix expectation (escape rejection preserved) | warning | yes: containment regression introduced by the ANALYSIS-001 fix on non-Windows (latent; Windows probed safe; content reads still protected) | verified | A7 developer | [analysis-003](analysis-003.md): Windows probes measured; Unix closed by design (hypothesis, not run); residual test gap ANALYSIS-061 |
| ANALYSIS-042 | [analysis-002](analysis-002.md) | 5.2 | suggestion | no; user asked to address it | verified | orchestrator (planning edit) | design.md Risks: hard-link staleness documented 2026-10-27 |
| ANALYSIS-043 | [analysis-002](analysis-002.md) | 1.2; QA-001 fix | suggestion | yes, by user decision 2026-10-27 | verified | A7 developer | [qa-004](qa-004.md) (browser, packaged build, journal locked → 503 at start) and [analysis-003](analysis-003.md) (design) |
| ANALYSIS-044 | [analysis-002](analysis-002.md) | 5.1; docs | suggestion | yes, by user decision 2026-10-27 | verified | A7 developer | [analysis-003](analysis-003.md) |
| ANALYSIS-061 | [analysis-003](analysis-003.md) | 5.1, 5.2; ANALYSIS-041 regression coverage | suggestion | no: the hop counter was verified by probe; test-only gap. Both fix rounds are used, so a third cycle would need the user | deferred | — | follow-up: add an unprivileged Windows hop-limit test (41 self-junction components) and a dangling linked-parent case; build the loop test with junctions |

## Finding details and history

- 2026-10-27, round 1 reviews: [qa-001](qa-001.md) (QA wrote it; 2 findings) and [analysis-001](analysis-001.md) (write refused; saved by the orchestrator under a header quoting the refusal; 8 findings). Both reviewed identity `head=bf65aeb baseline=bf65aeb files=18 diff=73aa0085eaa0 untracked=4/ee2ea065ab53` and report it unchanged after review.
- QA-001 fix expectation: a timezone is remembered only after it is accepted by both the browser and the server. A zone the server rejects ("Unknown reporting timezone.") is shown inline next to the field, previous results stay, nothing is saved, and the previous zone stays in effect; covered by a unit test with a server-rejected zone (for example `+02:00`).
- ANALYSIS-001 fix scope: items 1–2 of the proposal only (canonicalize the source root once per discovery; take attributes, length and write time from the directory entry and fully canonicalize only reparse points). Escape rejection must be preserved, including a junction or symlink at a nested directory below a change. Item 3 (archive on the 60 s tier) is not included because it changes freshness of archived documents.
- ANALYSIS-002 fix expectation: a timed-out CLI result is cached for a bounded backoff (at most the 60 s expensive lifetime) under the same key and input stamp, the timeout stays visible as a source error or notice, and an input stamp change bypasses it; covered by a unit test with the test clock.
- 2026-10-27, rechecks: [qa-002](qa-002.md) verified QA-001 through the browser UI against a packaged build (npm 29, dotnet 230, packaged e2e 16/16 reproduced; no new findings). QA observation, not a finding: with an escaping junction added after a successful listing, the change stayed listed with its earlier documents plus the escape notice; no outside content was exposed. [analysis-002](analysis-002.md) verified ANALYSIS-001 (Windows) and ANALYSIS-002 and raised ANALYSIS-041..044 as regressions of the fix. ANALYSIS-041 is gated (safety regression of an agreed expectation). ANALYSIS-043 and ANALYSIS-044 are suggestions by severity but are regressions or new claims introduced by fix round 1, so the user is asked whether to include them in round 2 (the last allowed round).
- 2026-10-27, user decision: include ANALYSIS-043, ANALYSIS-044 and ANALYSIS-042 in fix round 2 together with the gated ANALYSIS-041. ANALYSIS-042 is resolved as the documentation option by an orchestrator edit to design.md's Risks section (planning artifact); no code change. Fix round 2 of 2: A7 developer, then rechecks A8 (QA) and A9 (analysis).
- 2026-10-27, A7 developer fix returned (ANALYSIS-041, ANALYSIS-043, ANALYSIS-044 → awaiting-verification). Developer-reported: dotnet 232, npm 31, packaged e2e 16/16; cheap tier unchanged within machine variance. The Unix symlink form of the ANALYSIS-041 test could not run on this Windows host, and the loop test may skip without symlink privilege.
- 2026-10-27, [analysis-003](analysis-003.md) (A9, final analyzer recheck; write refused, saved verbatim): ANALYSIS-041 verified on Windows by probes against a verbatim baseline copy (only stricter differences: dangling targets through a linked parent now rejected; paths with more than 40 link components refused), Unix closed by design but not executed; cheap-tier gain kept. ANALYSIS-043 design verified (functional recheck by QA A10 pending). ANALYSIS-044 verified. New ANALYSIS-061 (suggestion, test gap) deferred.
- 2026-10-27, [qa-004](qa-004.md) (A10): ANALYSIS-043 verified in a real browser against a packaged build; npm 31, dotnet 232, packaged e2e 16/16 reproduced; task 6.1 grep clean; content hygiene passed over the changed implementation files, docs, change root and qa-004 screenshots. No new findings.
- Suggestions are deferred under the severity gate; QA-002 is a duplicate of ANALYSIS-003 (same FIFO eviction issue; ANALYSIS-003 has the measurement).
- 2026-10-27, A4 developer fix returned (QA-001, ANALYSIS-001 items 1–2, ANALYSIS-002 → awaiting-verification). Developer-reported: npm 29, dotnet 230, packaged e2e 16/16; cheap tier 727 → 17.5 ms median at 1,280 files (fixture). Additions for reviewers to check: `start()` drops a saved zone the server rejects (covers values saved before the fix); timeout backoff set to 30 s.
- Fix round 1 of at most 2: A4 developer, then rechecks A5 (QA) and A6 (analysis).

## Completion assessment

Assessed 2026-10-27 by orchestrator session eae5a20b.

- Current QA / analysis reports: [qa-004](qa-004.md) and [analysis-003](analysis-003.md), both at identity `head=bf65aeb baseline=bf65aeb files=19 diff=30cbc7228716 untracked=4/b53893c88cdb` (confirmed by the orchestrator after both returned).
  - Carry-forward rationale: fix round 2 changed only `SourcePath.cs`, `app.ts`, `app.reliability.spec.ts`, `DashboardReliabilityTests.cs` and `docs/dashboard-validation.md`. analysis-003 reviewed those changes and their callers; analysis-001/002 coverage of the other implementation files is unaffected. qa-004 re-ran the full validation set (unit, .NET, package, packaged e2e) and hygiene over the whole change.
- Required acceptance:
  - Every task's verify clause has evidence: Angular unit tests (31), .NET tests (232), packaged e2e 16/16 including the new rolling-range, bounded-positions, scoped-search, day-focus and Escape scenarios ([qa-001](qa-001.md), [qa-004](qa-004.md)).
  - Task 5.5 idle measurement before/after is recorded in `docs/dashboard-validation.md` (fixture measurements; source-consistent per [analysis-001](analysis-001.md)).
  - Task 6.1 by the orchestrator; verified in [qa-001](qa-001.md) and [qa-004](qa-004.md).
  - Known, out of scope: the Release-dll e2e scenario "keeps history available when a declared store cannot be resolved" fails with OpenSpec CLI 1.13.1 on PATH (QA-007 of `add-agent-workflow`, in the handoff follow-up list); `docs/dashboard-validation.md` records it as failing.
- Findings: 15 total. Verified: QA-001, ANALYSIS-001, ANALYSIS-002, ANALYSIS-041, ANALYSIS-042 (documentation), ANALYSIS-043, ANALYSIS-044. Duplicate: QA-002 → ANALYSIS-003. Deferred suggestions: ANALYSIS-003 to ANALYSIS-008, ANALYSIS-061. No blocking finding remains. Two fix rounds were used.
- Nonblocking follow-ups: ANALYSIS-003 (byte-bounded/use-ordered document cache), ANALYSIS-004 (stale-while-revalidate for the CLI tier), ANALYSIS-005 (flags in the unchanged answer), ANALYSIS-006 (one typed notice list), ANALYSIS-007 (prune CLI cache), ANALYSIS-008 (keep config reload error visible), ANALYSIS-061 (hop-limit test coverage).
- Evidence gaps: Unix link behavior (ANALYSIS-041) is closed by design but not executed (no Unix .NET environment; CI is Windows-only). The 400-with-another-message path is covered by unit tests only. Interrupted assignment A8 left five unreviewed screenshots `qa-003-*.png` (fixture data only, checked by the orchestrator).
- Archive: requested by the user on 2026-10-27.
  - REVIEW-marker precondition: no lines starting with `<!-- REVIEW` outside fenced code in the change root's Markdown.
  - Spec sync: chosen by the user. `work-dashboard` (1 modified, 2 added requirements) and `openspec-browser` (2 modified) were merged into the main specs, verified identical to the deltas, and `openspec validate` passed for both.
  - Archived to `openspec/changes/archive/2026-10-27-fix-dashboard-reliability/`; relative links from this ledger were adjusted for the new depth.
- Commit: authorized by the user on 2026-10-27, after the archive, on branch `change/fix-dashboard-reliability`. The SHA is in the commit history, since this file is part of that commit.
- OpenSpec task reconciliation: all 20 tasks checked on this evidence.
