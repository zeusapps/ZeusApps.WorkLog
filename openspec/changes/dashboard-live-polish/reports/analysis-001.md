# Analysis report: dashboard-live-polish / task 2.1 review

> Saved by the orchestrator as a reviewer-refused-write fallback (the client refused the reviewer's write). Condensed only in formatting; verdicts, findings, locations and evidence are unchanged.

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model: Claude Code 2.1.322 / subagent, auto mode / claude-opus-5-5
- Date: 2026-11-12T19:39:28Z
- Assignment: task 2.1; requirement "Monotonic pending live delivery"; background ANALYSIS-703 (archived analysis-032); developer note DEV-001
- Scope: `git diff f7dbb50 -- src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard`. Neighbors read: `DashboardLiveCoordinator.cs` (snapshot, publish sites, `DirtyNames`), `DashboardHost.cs:113-115`, `scripts/c06-acceptance-invariants.mjs:25-45`, `scripts/c06-fault-matrix.mjs:905-920`, `dashboard/src/live-updates.ts:35-70`, `dashboard/src/app.ts:740-755`; grep of `DashboardChangeSourceTests.cs`. QA report not read.
- Baseline commit: `f7dbb50`
- Identity before: `head=f7dbb50 baseline=f7dbb50 files=7 diff=e96cd8fcbc7d untracked=0/e3b0c44298fc`
- Identity after: `head=f7dbb50 baseline=f7dbb50 files=8 diff=cc54cc82fdfe untracked=0/e3b0c44298fc`. The only difference is `tests/WorkLog.Tests/Dashboard/DashboardChangeSourceTests.cs` (+4/-1), modified at 19:39:03Z by another actor; the seven assigned files still hash to `e96cd8fcbc7d`. Orchestrator note: that edit is the developer's ORCH-001 test-only fix, assigned by the orchestrator during this review.

## Analysis performed

| Area | Evidence | Outcome |
| --- | --- | --- |
| Merge semantics | `DashboardLiveConnections.cs:39-62`: same instance takes `Math.Max` per revision, ordinal `Union` of `Dirty`, pending checkpoint wins only if strictly greater; different instance replaces | Matches requirement and scenarios, and matches the client's `mergeRevisionSnapshots` |
| Concurrency | `latest` written under `gate` before `TryWrite`; Bounded(1)+DropWrite; loop reads a signal then `Take`s under the same lock | Sound; no lost or duplicated wakeup; unchanged Signal handling |
| Take/Offer gap | After `Take`, a late older same-instance offer is delivered with lower revisions | Pre-existing; ANALYSIS-001 |
| Different instance and ORCH-011a | `Instance` is per process; a restart creates new slots, so the branch is unreachable in a fault cycle | Dropping the old tag cannot break ORCH-011a; comment at `:43-44` misdescribes the counter (per process, never restarts) — behavior still correct |
| `Dirty` growth | Values from five fixed names; `Union` dedupes; `Take` resets | Bounded at 5 |
| DEV-001 | Spec forbids dropping pending hints and requires the union | New `["Journal"]` expectation is correct; over-report is conservative |
| Test guarding | Reasoned against baseline `Offer` | All three new tests should fail on the baseline (not run against it); one combination uncovered (ANALYSIS-002) |
| Tests run | Release build 0 warnings; `DashboardLiveCoordinatorTests` 29/29; 5 merge tests x5 passed | Passed |
| Formatting | `dotnet format --verify-no-changes` on the 2 files | Only CRLF `ENDOFLINE` from autocrlf; nothing introduced |
| Watcher test | `DashboardChangeSourceTests` does not use the live connection or `Offer` | Delta cannot plausibly affect it |

## Steady-state cost and bounds

No new timer, request, process or file read; one record copy and a small union per pending slot per publish; one vector per slot; `Dirty` bounded and cleared on `Take`; no caches, reads or external tools.

## Findings

### ANALYSIS-001 (suggestion): A late older offer after `Take` is still delivered with lower revisions

- Locations: `DashboardLiveConnections.cs:45,59,64-72`; publish sites `DashboardLiveCoordinator.cs:178,217,367,444,508`
- Observation: the merge runs only while a vector is pending. If the loop takes N+1 before a late N arrives, N is delivered after N+1. The browser max-merges and does not regress. In the drop cycle a narrow window can leave a tagged checkpoint carrying a generation below the dropped one, so recovery waits for the next 5 s tick against the 6000 ms ORCH-011a gate. Pre-existing at the baseline.
- Proposal: a per-slot floor of the last taken same-instance revisions, set in `Take`, applied with `Math.Max` in `Offer` when nothing is pending (revisions only); reset on instance change.
- Evidence: source observation; frequency and gate impact are hypotheses.
- Relationship: residual of ANALYSIS-703.

### ANALYSIS-002 (suggestion): No test covers an older checkpoint-tagged offer merging into a newer untagged pending vector

- Locations: `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs:1025-1093`
- Observation: the checkpoint-loop case (an older tagged `CurrentSnapshot` while a newer untagged lane vector is pending) is untested; applying `Math.Max` only for untagged offers, or letting a tagged offer replace outright, would pass every current test. The three new tests dispose `connections` and `box` manually rather than with `using`, leaking on assertion failure.
- Proposal: one test with `ConnectionWithHeldFirstSendAsync`: publish `(5,…,["Journal"])`, then `(4,…,[])` with `checkpoint: true`; assert revisions 5, `Dirty == ["Journal"]`, `Checkpoint == CheckpointCount`. Switch the new tests to `using` declarations, keeping `StopAsync` first.
- Risk: none; test-only.

## Re-verification

| Finding | Outcome |
| --- | --- |
| ANALYSIS-703 (archived) | Verified for the pending-vector case; post-`Take` residual raised as ANALYSIS-001 |

## Assessment and limitations

Task 2.1 implements the requirement correctly; the merge is atomic with respect to `Take`; `Dirty` is bounded; the different-instance branch cannot affect ORCH-011a. DEV-001 is correct. The comment at `DashboardLiveConnections.cs:43-44` should cite the spec rather than a checkpoint-sequence restart. Fail-first established by reasoning, not by running against the baseline. Full .NET suite and Playwright not run by this reviewer.
