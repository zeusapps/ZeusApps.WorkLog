# Tasks

This change creates the workflow itself, so there is no orchestrator yet to delegate to. Groups 1–4 are performed directly by the main agent and reviewed in group 5 by the newly added roles. That bootstrap exception is recorded in `reports/findings.md`.

## 1. Shared policy

- [x] 1.1 Create `AGENTS.md` with the delegated-workflow entry paragraph adapted from Bell Tower and WorkLog's narrower parity scope (design Decision 2); verify no `CLAUDE.md` or `.claude/CLAUDE.md` exists
- [x] 1.2 Port `.agents/workflows/openspec-delegation.md` from Bell Tower `aadbfb3`, replace Bell Tower names and paths, and add the archive REVIEW-marker precondition (Decision 5); verify every relative link resolves
- [x] 1.3 Port `.agents/roles/developer.md`, `qa.md`, `code-analyzer.md`; add the QA "Time, growth, input focus, scope, residue" section and the analyzer "Steady-state cost and bounds" section; verify each role links only `project-facts.md` and its template, not the workflow
- [x] 1.4 Write `.agents/roles/project-facts.md` for WorkLog (commands, layout, safety, hygiene per Decision 3); verify every listed command runs successfully on a clean checkout, or record why one cannot run

## 2. Templates and tooling

- [x] 2.1 Port `.agents/templates/qa-report.md`, `analysis-report.md`, `findings.md`, adding the heuristic rows; verify the templates contain the residue, time-passing, and steady-state rows
- [x] 2.2 Copy `scripts/review-identity.ps1`; verify `./scripts/review-identity.ps1 -Baseline HEAD -Paths src dashboard/src` prints a manifest and summary line and leaves `git status` unchanged

## 3. Native adapters

- [x] 3.1 Add `.claude/agents/developer.md`, `qa.md`, `code-analyzer.md` (sonnet, sonnet, opus with high effort); verify that a new Claude Code session started after the files exist can launch each role by name (Claude Code 2.1.311 removed the `/agents` wizard; a session started before the files existed reports the role as not found, which was observed on 2026-10-27)
- [x] 3.2 Add `.codex/agents/developer.toml`, `qa.toml`, `code-analyzer.toml` with "documented, not natively verified" comments; verify `.codex/config.toml` owned blocks are byte-identical to before
- [x] 3.3 Confirm `worklog init --dry-run` (run against a disposable copy of the repository with an isolated `--data-dir`) does not plan any change to the new files

## 4. Documentation

- [x] 4.1 Write `docs/agent-workflow.md` (how-to, file map, activation, client switching, models, known restrictions re-verified on the installed client versions); link it from `README.md`; verify links resolve

## 5. Review and calibration

- [x] 5.1 Initialize `reports/findings.md` with the orchestrator marker, the bootstrap exception, and reservations for `qa-001`, `analysis-001`, `qa-002`, `analysis-002` and finding ID ranges
- [x] 5.2 Run the calibration QA and analyzer assignments against commit `582f8bc` with the scope and reading restrictions in design Decision 6; verify both reports exist with target identity before and after
- [x] 5.3 Compare the calibration findings with the answer key in `docs/handoffs/2026-10-27-dashboard-follow-ups.md`; record caught, missed, and new defects in the ledger, with the heuristic that should have caught each missed item
- [x] 5.4 Amend role files or templates once for missed defect classes; record the amendment in the ledger
- [x] 5.5 Run light QA and analyzer reviews (`qa-002`, `analysis-002`) of the workflow files themselves (groups 1–4 plus 5.4); triage findings per the workflow
- [x] 5.6 Add any confirmed new dashboard defects to the handoff's follow-up list for `fix-dashboard-reliability`; verify the handoff lists each with a source report link
