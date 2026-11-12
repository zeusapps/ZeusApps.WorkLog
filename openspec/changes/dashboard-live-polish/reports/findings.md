# Findings: dashboard-live-polish

Orchestrator-owned ledger.

## Current state (resume here)

- **Orchestrator:** Claude Code. Branch to be created from `main` (`f7dbb50`) when implementation starts. Implementation authorized 2026-11-12 on branch `change/dashboard-live-polish`.
- **Scope (user decision 2026-11-12):** only real items, ANALYSIS-705 (Retry keyboard focus) and ANALYSIS-703 (monotonic pending delivery), plus housekeeping. Dropped, not deferred: freshness p95, server-work capture, the 100-cycle matrix, ANALYSIS-704, QA-600, QA-601, QA-602 and QA-651. QA-652 is already resolved by ANALYSIS-782.
- **Origin:** archived change `2026-11-12-realtime-dashboard-updates` (reports `analysis-032.md`, `qa-029.md`).
- **Design:** skipped (optional artifact; small single-concern fixes with no architectural decision).

### Results (2026-11-12)

- Tasks 1.1, 2.1, 3.1 checked (3/4); 3.2 awaits the user-approved commit.
- 1.1 (ANALYSIS-705): light QA `qa-001` passes all three scenarios. QA-001 (suggestion: page Retry lacks the mousedown guard) is accepted without change, because drafts exist only in the reader.
- 2.1 (ANALYSIS-703): `analysis-001` (fallback saved by the orchestrator) verifies the merge, concurrency and ORCH-011a safety, and confirms DEV-001 (the union-`Dirty` assertion) is correct. ANALYSIS-001 (post-`Take` residual: the browser does not regress, and only harness timing is affected) is recorded and not actioned, per the user's real-items-only scope. ANALYSIS-002 (test gap) is fixed: a new test fails under a mutation, `using` cleanup, and the comment now cites the spec. The orchestrator verified the diff.
- ORCH-001: `DashboardChangeSourceTests.RecursivePhysicalWatcher…` failed 3/3 in full runs on the branch and passed alone; the baseline f7dbb50 passed 461/461. Cause: the assertion was stricter than the test's purpose, and a legitimate ancestor directory-timestamp hint (change-root/specs) arrived late under load. Fixed test-only, with the file-event attribution and outside-junction checks kept. Full suite 464/464 x3, then 465/465 after ANALYSIS-002.
- Final checks: dashboard unit 355/355; full e2e 55/55 (before the test-only follow-ups); .NET 465/465; hygiene clean; strict validation passes.

### Reservations

- Review of 1.1/2.1 (2026-11-12): `qa-001` with QA-001..019 (task 1.1); `analysis-001` with ANALYSIS-001..019 (task 2.1). DEV-001 used.
- Next free IDs: reports `qa-002` and `analysis-002`; findings QA-020+, ANALYSIS-020+, ORCH-001+, DEV-002+.
