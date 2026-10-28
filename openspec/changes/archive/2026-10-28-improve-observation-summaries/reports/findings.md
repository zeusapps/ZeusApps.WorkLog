# Findings: improve-observation-summaries

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

Advisory marker; resume and hand-off rules are in the [workflow](../../../../.agents/workflows/openspec-delegation.md#mixed-client-operation).

- Client / version: Claude Code 2.1.312
- Session identity: d15752ac-86bb-4d2b-a9d8-0ee05afbe30e
- Since (UTC): 2026-10-28T14:35Z
- Previous owner: none

## Scope decisions

- Baseline commit: `4e1c50b` on branch `change/improve-observation-summaries` (created from `main`).
- 2026-10-28: design.md is intentionally omitted (tasks.md says so); the apply state is `ready`.
- 2026-10-28: task 1.3 runs `worklog init` only with `--dry-run`, on a disposable repository with an isolated `--data-dir`, per project facts.
- 2026-10-28, A1 developer deviations, provisionally accepted for reviewers to check:
  1. `./scripts/package.ps1` refused the default output (immutable `worklog-0.3.0-win-x64` already exists); packaged with `-OutputDirectory artifacts/releases/improve-observation-summaries-validation`.
  2. Task 1.3 ran on a freshly `git init`-ed disposable repository, so the dry run shows first-time planned writes of both owned blocks, not a refresh of previously installed text. A2 QA is asked to attempt a refresh preview with `--dry-run` on a disposable copy of an already initialized repository.

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A1 implementation | developer | Claude Code / role default | none: role default | tasks 1.1–2.2; baseline `4e1c50b` | implementation paths only | none | done 2026-10-28, model claude-sonnet-5-5; identity `head=4e1c50b baseline=4e1c50b files=7 diff=93757b78458f untracked=0/e3b0c44298fc` (paths: README.md docs/installation.md docs/mcp-validation.md src/WorkLog/Integration/AgentInstructions.cs src/WorkLog/Journaling/LogRequest.cs tests/WorkLog.Tests/Integration/InstructionOptionsTests.cs tests/WorkLog.Tests/Mcp/McpTests.cs) |
| A2 review QA | qa | Claude Code / role default | none: role default | tasks 1.1–2.2; identity of A1 | `qa-001.md` | QA-001 – QA-020 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report; QA-001, QA-002 new (003–020 unused, retired); identity unchanged |
| A3 review analysis | code-analyzer | Claude Code / role default | none: role default | tasks 1.1–2.2; identity of A1 | `analysis-001.md` | ANALYSIS-001 – ANALYSIS-020 | done 2026-10-28, model claude-opus-5-5; write refused, saved by orchestrator under a header quoting the refusal; ANALYSIS-001..003 new (004–020 unused, retired); identity unchanged |

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| QA-001 | [qa-001](qa-001.md) | 1.3, 2.2 | suggestion | no: severity gate | deferred | unassigned | n/a |
| QA-002 | [qa-001](qa-001.md) | 1.1 | suggestion | no: duplicate | duplicate of ANALYSIS-002 | unassigned | n/a |
| ANALYSIS-001 | [analysis-001](analysis-001.md) | 1.1, 1.2; Summary guidance in the logging tool | suggestion | no: severity gate | deferred | unassigned | n/a |
| ANALYSIS-002 | [analysis-001](analysis-001.md) | 1.1 | suggestion | no: severity gate | deferred | unassigned | n/a |
| ANALYSIS-003 | [analysis-001](analysis-001.md) | 2.1 | suggestion | no: severity gate | deferred | unassigned | n/a |

## Finding details and history

### QA-001: mcp-validation.md says a dry run cannot show a refresh

- Sources and evidence: [qa-001](qa-001.md), row "1.3 refresh preview, Claude Code"
- Triage 2026-10-28: suggestion; the validation note understates dry-run capability (a dry run lists an instruction file only when its owned block would change). Deferred per severity gate. Follow-up: narrow the sentence and optionally record the seeded-repository method, at the user's request.

### QA-002: 1.1 test checks the rest of the block only partially

- Sources and evidence: [qa-001](qa-001.md)
- Triage 2026-10-28: duplicate of ANALYSIS-002 (same test, same lines); both reports preserved.

### ANALYSIS-001: Summary guidance is two unlinked literals

- Sources and evidence: [analysis-001](analysis-001.md)
- Triage 2026-10-28: suggestion; wording is consistent today, only future drift is unguarded. Deferred per severity gate. Follow-up: one alignment assertion between the instruction clause and the `LogRequest.Summary` description, at the user's request or the next change touching either text.

### ANALYSIS-002: Brittle "restore prior block" test assertions

- Sources and evidence: [analysis-001](analysis-001.md)
- Triage 2026-10-28: suggestion; test-only maintainability. Deferred per severity gate. Follow-up: drop the line-count pin or replace it with one full expected-text comparison, at the user's request or the next instruction-template edit.

### ANALYSIS-003: Installation note says rerun refreshes "both clients"

- Sources and evidence: [analysis-001](analysis-001.md)
- Triage 2026-10-28: suggestion; docs wording, `--clients` defaults to both. Deferred per severity gate. Follow-up: reword to "the selected clients (both by default)", at the user's request.

## Completion assessment

2026-10-28, target identity `head=4e1c50b baseline=4e1c50b files=7 diff=93757b78458f untracked=0/e3b0c44298fc`:

- Current reports: [qa-001](qa-001.md) and [analysis-001](analysis-001.md), both on this identity.
- All findings triaged: no critical or warning findings; QA-001, ANALYSIS-001..003 deferred suggestions; QA-002 duplicate.
- Acceptance evidence: 1.1 unit test (both clients, shared renderer); 1.2 McpTests plus packaged `tools/list` (QA); 1.3 packaged `init --dry-run` on a seeded disposable clone with isolated data: the old-text owned block is planned as an update for Claude Code and Codex, no unexpected paths, nothing written. Limitation: the no-op case after refresh was demonstrated for Claude Code only; QA's hand-built Codex seed still planned an update (cause not isolated, not shown to be a product defect). 2.1 docs inspected; 2.2 restore/build/test (340 passed) and packaged isolated smoke (`SMOKE PASSED`), independently reproduced by QA.
- Hygiene: QA's check passed over the change; the orchestrator re-ran the search over files written afterwards (this ledger, analysis-001.md, tasks.md) before committing.
- Commit: `de11214` on branch `change/improve-observation-summaries`.
- Archive 2026-10-28, on the user's request: no `<!-- REVIEW` lines in the change root; design.md intentionally omitted (status `ready`, not done); delta synced into `openspec/specs/local-agent-integration/spec.md` and verified (`openspec validate --specs` passed).
