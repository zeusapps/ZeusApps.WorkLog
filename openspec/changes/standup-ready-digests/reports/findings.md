# Findings: standup-ready-digests

Orchestrator-owned ledger.

## Current state (resume here)

- **Orchestrator:** Claude Code. Implementation authorized 2026-11-12 on branch `change/standup-ready-digests`. Base `main` at `f6ea04e`. Target release: v0.5.0, together with the earlier dashboard work.
- **User decisions (2026-11-12, explore):**
  - Keep the Today/Yesterday framing, because the user keeps working after an end-of-day stand-up.
  - Digests become structured Done/Next/Blockers bullet lists.
  - Request a digest from the first observation of the day as well.
  - Team-relevant content only: no OpenSpec or process mechanics, and no agents or roles (developer, QA, analyzer, reviewer, bug-hunter or any other).
  - Daily page shows Today and Yesterday side by side, with Today first.
- **Evidence:** the user's stored digest for 2026-11-09 covered all 7 entries, yet restated only the latest one. The input is complete; the prompt and summaries cause the recency bias.

### Results (2026-11-12)

- Tasks 1.1–5.1 are checked (7/8). 5.2 awaits the user-approved commit.
- Implementation: server side, then dashboard and docs. Deviations DEV-001..004 were accepted. DEV-001's text comparison was later superseded by the ANALYSIS-001 save stamp. DEV-005 (mcp-validation doc) and DEV-006 (structured-digest fixture plus browser test) are fixed.
- Milestone: `qa-001` passes, with QA-001..003 suggestions fixed. `analysis-001` (fallback saved) raised ANALYSIS-001 (warning) and 002..009. All were fixed except 003, which became moot. The user-facing spec and design wording was updated for 004 (single-text legacy save) and 005 (rule stated once).
- Recheck: `analysis-002` (fallback saved) verifies all 11. The QA-001 and ANALYSIS-008 tests fail on the previous code, judged from source.
- Final checks by the orchestrator: Release build 0 warnings; .NET 500/500; dashboard 377/377; full e2e 60/60; hygiene clean; strict validation passes. The `save_digest` listing shrank from 3898 to 2740 characters.
- Dev build `0.4.9005 dev f6ea04e dirty` is installed at the user's request. Roll back to 0.4.9004 with operation `3fca2d75…`.

### Reservations

- Milestone review (2026-11-12): `qa-001` with QA-001..029; `analysis-001` with ANALYSIS-001..029. DEV-001..006 used.
- Next free IDs: reports `qa-002` and `analysis-002`; findings QA-030+, ANALYSIS-030+, ORCH-001+, DEV-007+.
