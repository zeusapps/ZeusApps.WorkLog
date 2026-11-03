# Findings: standup-oriented-summaries

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

Advisory marker; resume and hand-off rules are in the [workflow](../../../../.agents/workflows/openspec-delegation.md#mixed-client-operation).

- Client / version: Claude Code 2.1.317
- Session identity: claude-code main session, apply of standup-oriented-summaries
- Since (UTC): 2026-11-03T16:49:28Z
- Previous owner: none

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| DEV-1 | developer | Claude Code / claude-sonnet-5-5 | none: role default | 1.1, 2.1-2.4, 3.1-3.3, 4.1, 5.1a; baseline 6d23796 | n/a | n/a | returned (build 0/0, 408/408 tests) |
| QA-R1 | qa | Claude Code 2.1.317 / claude-sonnet-5-5 | none: role default | 5.1, 5.2 and review of DEV-1; identity head=6d23796 baseline=6d23796 files=11 diff=dda65f72cd81 untracked=2/04fb85d66245 | reports/qa-001.md | QA-001 to QA-020 (QA-001 used) | returned (partial: 5.2 blocked) |
| AN-R1 | code-analyzer | Claude Code 2.1.317 / claude-opus-5-5 | none: role default | review of DEV-1; same identity | reports/analysis-001.md (saved by orchestrator, refused write) | ANALYSIS-001 to ANALYSIS-020 (001-006 used) | returned |
| DEV-2 | developer | Claude Code, role default | none: role default | remove 5.1a smoke flag; ANALYSIS-001, ANALYSIS-002; task 4.2 | n/a | n/a | returned (build 0/0, 422/422 tests) |
| QA-R2 | qa | Claude Code 2.1.317 / claude-sonnet-5-5 | none: role default | recheck DEV-2 + regression; identity head=6d23796 baseline=6d23796 files=11 diff=28b2dff190cb untracked=2/789d00f904e0 (paths incl. docs/mcp-validation.md) | reports/qa-002.md | QA-021 to QA-030 (none used) | returned (422/422; no findings) |
| AN-R2 | code-analyzer | Claude Code 2.1.317 / claude-sonnet-5-5 | override: light recheck of test-only and docs-only correction | recheck ANALYSIS-001, ANALYSIS-002; same identity | reports/analysis-002.md (saved by orchestrator, refused write) | ANALYSIS-021 to ANALYSIS-030 (none used) | returned |

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| QA-001 | [qa-001](qa-001.md) | 5.1a, 5.2 | warning | no: 5.2 replaced by user-observed acceptance | dismissed | orchestrator | [qa-001](qa-001.md#qa-001-the-live-wording-check-cannot-authenticate-in-its-isolated-client-homes-so-acceptance-52-produced-no-output) |
| ANALYSIS-001 | [analysis-001](analysis-001.md), [analysis-002](analysis-002.md) | 2.3, 2.4, 3.2 | suggestion | no; user asked to fix (2026-11-03) | verified | DEV-2 | [analysis-002](analysis-002.md#re-verification) |
| ANALYSIS-002 | [analysis-001](analysis-001.md), [analysis-002](analysis-002.md) | 3.2 | suggestion | no; user asked to fix (2026-11-03) | verified | DEV-2 | [analysis-002](analysis-002.md#re-verification) |
| ANALYSIS-003 | [analysis-001](analysis-001.md) | 5.1a | suggestion | no | dismissed | orchestrator | n/a: 5.1a code removed |
| ANALYSIS-004 | [analysis-001](analysis-001.md) | 5.1a | suggestion | no | dismissed | orchestrator | n/a: 5.1a code removed |
| ANALYSIS-005 | [analysis-001](analysis-001.md) | 1.1, 2.1, 2.3 | suggestion | no | dismissed | orchestrator | n/a: size accepted by user |
| ANALYSIS-006 | [analysis-001](analysis-001.md) | 5.1a, 5.2 | suggestion | no | dismissed | orchestrator | n/a: 5.1a code removed |

## Finding details and history

### QA-001: Live wording check cannot authenticate in isolated client homes

- Sources and evidence: [qa-001](qa-001.md)
- Required behavior: 5.2 needs one judged summary and digest per client.
- Triage: blocking for 5.2. The smoke runner sets `CODEX_HOME` and `CLAUDE_CONFIG_DIR` to the disposable home for every child process (pre-existing in `SmokeProcessRunner.cs`), so isolated clients have no credentials; Codex 0.165.0 returned 401 before any tool call and the run aborted before Claude. The fix needs a credential-source decision, which is the user's.
- Action / assignment: 2026-11-03 escalated to the user with four options (real-use observation, reusable one-time login home, API-key passthrough, drop the check).
- Duplicate of: none
- Deferral: none
- User decision: 2026-11-03, "Watch it in real use": task 5.2 becomes user-observed acceptance on an installed dev build; no credential work in the smoke tool. Dismissed as a finding because the isolated check is no longer the acceptance route; tasks.md and design.md updated.

### ANALYSIS-001 and ANALYSIS-002: test literals and copied phrase fragments

- Sources and evidence: [analysis-001](analysis-001.md)
- Triage: suggestions (non-blocking). 2026-11-03 the user chose to fix both now.
- Action / assignment: DEV-2, tests only: use the shared constants for the 280 limit in the two tests; replace `GuidanceMechanics` with one keyword test on `ContentRule`.
- User decision: 2026-11-03 "Fix both now".
- History: 2026-11-03 DEV-2 corrected both (tests only); AN-R2 verified both on identity diff=28b2dff190cb untracked=2/789d00f904e0 (focused tests 52/52).

### ANALYSIS-003, ANALYSIS-004, ANALYSIS-006: live wording smoke code

- Sources and evidence: [analysis-001](analysis-001.md); related to QA-001.
- Triage: dismissed. 2026-11-03 the user chose to remove the `--live-wording` flag and its code (task 5.1a dropped), so these findings no longer apply.
- Action / assignment: DEV-2 removes `tools/WorkLog.Smoke/LiveWordingChecks.cs` and the flag handling in `Program.cs`.
- User decision: 2026-11-03 "Remove it".

### ANALYSIS-005: guidance text size

- Sources and evidence: [analysis-001](analysis-001.md)
- Triage: dismissed. 2026-11-03 the user accepted the size; design.md Risks corrected with the measured figures.
- User decision: 2026-11-03 "Accept the size".

### Orchestrator note: design.md dependency sentence

- analysis-001 found design.md Context wrongly said Integration already depended on Journaling. Corrected in design.md on 2026-11-03; not a finding.

## Completion assessment

- 2026-11-03: implementation tasks 1.1-4.2 and acceptance 5.1 satisfied. Current reports on the current target (diff=28b2dff190cb untracked=2/789d00f904e0): [qa-002](qa-002.md) (422/422, no findings) and [analysis-002](analysis-002.md) (no findings). Task 5.1 carried forward from [qa-001](qa-001.md): `src/` was outside DEV-2 ownership, `LoggingGuidance.cs` hash is unchanged, and qa-002 found no `src/` change after qa-001.
- All findings triaged: QA-001 and ANALYSIS-003 to 006 dismissed by user decisions; ANALYSIS-001 and 002 verified.
- 2026-11-03: the user could check real use only after release and asked for a probe with fresh agents instead. [probe-001](probe-001.md) passed on claude-opus-5-5 and claude-sonnet-5-5. User decision: accept the Claude probe as task 5.2; Codex models not covered (recorded parity limitation of the acceptance evidence, not of the implementation, which is shared text for both clients).
- Completion: all 12 tasks satisfied, both current role reports on the current target, every finding triaged, no blocking finding or unapproved deferral. Implementation complete; commit on `change/standup-oriented-summaries`. No archive or push.
