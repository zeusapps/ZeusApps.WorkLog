# Findings: add-agent-workflow

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

Advisory marker; resume and hand-off rules are in `.agents/workflows/openspec-delegation.md` (Mixed-client operation). The path is repository-relative so it stays valid after archiving.

- Client / version: Claude Code 2.1.311
- Session identity: eae5a20b-6b19-45bd-b040-24cfe844c709 (active)
- Since (UTC): 2026-10-27
- Previous owner: Claude Code 2.1.311, session fd544521-71c2-4cee-8c1e-f523e911fa1c (released 2026-10-27; take-over pre-authorized by its hand-off note, no assignments were running)

## Bootstrap exception

This change creates the delegated workflow, so no developer role existed when groups 1–4 of `tasks.md` were implemented. The main agent wrote those files directly (user-authorized apply of this change, 2026-10-27). Independent review happens in group 5 through the newly added QA and code-analyzer roles. Later changes follow the workflow without this exception.

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A0 developer launch probe | developer | Claude Code / role default | none: role default | 3.1; read-only probe, no writes | none | none | done 2026-10-27: role and project facts loaded, no writes, model claude-sonnet-5-5 (self-reported from its environment) |
| A1 calibration QA | qa | Claude Code / role default | none: role default | 5.2; target `582f8bc`, dashboard scope (design Decision 6) | `qa-001.md` | QA-001 – QA-040 | done 2026-10-27, model claude-sonnet-5-5; QA wrote its own report; probe process left running (see history) |
| A2 calibration analysis | code-analyzer | Claude Code / role default | none: role default | 5.2; target `582f8bc`, dashboard scope (design Decision 6) | `analysis-001.md` | ANALYSIS-001 – ANALYSIS-040 | done 2026-10-27, model claude-opus-5-5; report write refused, saved by orchestrator (fallback) |
| A5 calibration amendments | developer | Claude Code / role default | none: role default | 5.4 and the 4.1 restriction update; workflow files at identity head=582f8bc files=18 diff=9fb5e3d6075e untracked=17/a458b79d0118 | .agents/roles/qa.md, code-analyzer.md, project-facts.md; .agents/templates/qa-report.md, analysis-report.md; docs/agent-workflow.md | none | done 2026-10-27, model claude-sonnet-5-5; awaiting review in A3/A4 |
| A3 workflow light QA | qa | Claude Code / role default | none: role default | 5.5; groups 1–4 plus 5.4 at the workflow branch identity | `qa-002.md` | QA-041 – QA-060 | done 2026-10-27, model claude-sonnet-5-5; QA wrote its own report; 1 finding |
| A4 workflow light analysis | code-analyzer | Claude Code / sonnet (override) | docs-only/config-only target; workflow Model selection allows the cost-efficient default for the analyzer | 5.5; groups 1–4 plus 5.4 at the workflow branch identity | `analysis-002.md` | ANALYSIS-041 – ANALYSIS-060 | done 2026-10-27, model claude-sonnet-5-5; no write attempted (subagent instructions), saved by orchestrator |
| A6 workflow fix round 1 | developer | Claude Code / role default | none: role default | QA-041, ANALYSIS-041; workflow files at untracked=17/9f7af19ec099 | .agents/workflows/openspec-delegation.md, .agents/roles/project-facts.md, .agents/roles/qa.md | none | done 2026-10-27, model role default; identity untracked=17/44c52416a753 |
| A7 workflow recheck QA | qa | Claude Code / role default | none: role default | recheck QA-041 plus regression | `qa-003.md` | QA-061 – QA-070 | done 2026-10-27, model claude-sonnet-5-5; QA-041 passed, no regressions |
| A8 workflow recheck analysis | code-analyzer | Claude Code / sonnet (override) | docs-only target | recheck ANALYSIS-041 plus regression | `analysis-003.md` | ANALYSIS-061 – ANALYSIS-070 | done 2026-10-27, model claude-sonnet-5-5; ANALYSIS-041 passed, no regressions; write refused, saved by orchestrator |

## Summary

| ID | Source report | Task / requirement | Severity | Blocking | Status | Destination / latest verification |
| --- | --- | --- | --- | --- | --- | --- |
| QA-001 | [qa-001](qa-001.md) | openspec-browser Scoped document search | warning | non-blocking here (no fixes in this change, design Decision 6) | deferred | `fix-dashboard-reliability` task 4.1 (key E) |
| QA-002 | [qa-001](qa-001.md) | work-dashboard history navigation | warning | non-blocking here | deferred | `fix-dashboard-reliability` tasks 1.1–1.2 (key A) |
| QA-003 | [qa-001](qa-001.md) | work-dashboard preferences | warning | non-blocking here | deferred | `fix-dashboard-reliability` tasks 2.1–2.2 (key B); canonical for ANALYSIS-004 |
| QA-004 | [qa-001](qa-001.md) | openspec-browser refresh | warning | non-blocking here | duplicate of ANALYSIS-001 | `fix-dashboard-reliability` task 5.1 (key P) |
| QA-005 | [qa-001](qa-001.md) | openspec-browser keyboard | warning | non-blocking here | deferred | `fix-dashboard-reliability` task 3.1 (key C) |
| QA-006 | [qa-001](qa-001.md) | work-dashboard timezone boundary | warning | non-blocking here | deferred | handoff follow-up list for `fix-dashboard-reliability` (new) |
| QA-007 | [qa-001](qa-001.md) | store resolution; validation record | warning | non-blocking here | deferred | handoff follow-up list for `fix-dashboard-reliability` (already listed; QA adds the validation-record claim) |
| QA-008 | [qa-001](qa-001.md) | content hygiene | warning | non-blocking here | deferred | `fix-dashboard-reliability` task 6.1 (key D) |
| QA-009 | [qa-001](qa-001.md) | work-dashboard restart authorization | suggestion | non-blocking | deferred | handoff follow-up list (optional) |
| QA-010 | [qa-001](qa-001.md) | document-feedback marker text | suggestion | non-blocking | deferred | handoff follow-up list (optional) |
| QA-011 | [qa-001](qa-001.md) | dashboard command help text | suggestion | non-blocking | deferred | handoff follow-up list (optional) |
| ANALYSIS-001 | [analysis-001](analysis-001.md) | openspec-browser refresh; archived task 3.5 | warning | non-blocking here | deferred | `fix-dashboard-reliability` task 5.1 (key P); canonical for QA-004 |
| ANALYSIS-002 | [analysis-001](analysis-001.md) | openspec-browser refresh | warning | non-blocking here | deferred | handoff follow-up list for `fix-dashboard-reliability` (new, poll cost) |
| ANALYSIS-003 | [analysis-001](analysis-001.md) | work-dashboard history navigation | warning | non-blocking here | deferred | handoff follow-up list for `fix-dashboard-reliability` (new; partly addressed by tasks 1.2, 5.3) |
| ANALYSIS-004 | [analysis-001](analysis-001.md) | work-dashboard preferences | warning | non-blocking here | duplicate of QA-003 | `fix-dashboard-reliability` tasks 2.1–2.2 (key B) |
| ANALYSIS-005 | [analysis-001](analysis-001.md) | openspec-browser search | warning | non-blocking here | deferred | `fix-dashboard-reliability` tasks 5.3–5.4 (key P) |
| ANALYSIS-006 | [analysis-001](analysis-001.md) | work-dashboard timeline | suggestion | non-blocking | deferred | `fix-dashboard-reliability` task 5.3 (key P) |
| ANALYSIS-007 | [analysis-001](analysis-001.md) | archived task 3.5 | suggestion | non-blocking | deferred | `fix-dashboard-reliability` task 5.2 (catalog revision) |
| ANALYSIS-008 | [analysis-001](analysis-001.md) | dashboard authorization | suggestion | non-blocking | deferred | optional follow-up; reconsider with the poll-cost work |
| ANALYSIS-009 | [analysis-001](analysis-001.md) | openspec-browser source resolution | suggestion | non-blocking | deferred | unconfirmed source observation; handoff follow-up list asks the fix change to confirm |
| ANALYSIS-010 | [analysis-001](analysis-001.md) | document-feedback write durability | suggestion | non-blocking | deferred | optional follow-up |
| ANALYSIS-011 | [analysis-001](analysis-001.md) | architecture: Repositories | suggestion | non-blocking | deferred | optional follow-up |
| ANALYSIS-012 | [analysis-001](analysis-001.md) | document-feedback marker parsing | suggestion | non-blocking | deferred | optional follow-up; pair with QA-010 |
| ANALYSIS-013 | [analysis-001](analysis-001.md) | client change matching | suggestion | non-blocking | deferred | optional follow-up |
| ANALYSIS-014 | [analysis-001](analysis-001.md) | client structure | suggestion | non-blocking | deferred | parked idea in the handoff (split app.ts incrementally) |
| QA-041 | [qa-002](qa-002.md) | spec: WorkLog live-instance safety ("recorded as not performed") | suggestion | blocking: missing required spec content cannot be downgraded by severity | verified | fixed by A6; verified in [qa-003](qa-003.md) at untracked=17/44c52416a753 |
| ANALYSIS-041 | [analysis-002](analysis-002.md) | 4.1; spec reports and ledger (refusal header) | suggestion | blocking: workflow contradicts the re-verified restriction recorded for task 4.1, and the header rule cannot cover a write that was not attempted | verified | fixed by A6; verified in [analysis-003](analysis-003.md) at untracked=17/44c52416a753 |
| ANALYSIS-042 | [analysis-002](analysis-002.md) | archive precondition pattern | suggestion | non-blocking | deferred | optional follow-up: align with `ReviewMarker` grammar (`^ {0,3}<!-- REVIEW`) when the workflow is next edited |
| ANALYSIS-043 | [analysis-002](analysis-002.md) | review-identity read-only claim | suggestion | non-blocking | deferred | optional follow-up: consider `GIT_OPTIONAL_LOCKS=0`; unconfirmed hypothesis |

## Finding details and history

2026-10-27, orchestrator triage of calibration rounds qa-001 and analysis-001 (session eae5a20b):
- This change makes no dashboard fixes (design Decision 6), so no finding gets a fix round here. Each finding is deferred to `fix-dashboard-reliability` or recorded as an optional follow-up. None is acceptance for this change, so the deferrals need no user decision.
- Duplicates: QA-004 duplicates ANALYSIS-001 (catalog rediscovery cost). ANALYSIS-001 is canonical because it has the per-interval measurement. ANALYSIS-004 duplicates QA-003 (unbounded preferences). QA-003 is canonical because it reproduced the user-visible failure. Both reports are preserved.
- Severities are kept as reported. QA asked for calibration of QA-003 and QA-004. Both stay warnings because they break saving and responsiveness, and `fix-dashboard-reliability` already plans both fixes.
- QA-007 matches the follow-up the orchestrator found while verifying commands. QA reproduced it independently and added that `docs/dashboard-validation.md` claims all eleven scenarios pass.
- ANALYSIS-009 is a source observation that no reviewer reproduced. It is recorded as unconfirmed, not as a defect.
- 2026-10-27, QA-041 (qa-002): the reported severity is suggestion, but the finding is a sentence that the spec requires ("A check that requires the live installation SHALL be recorded as not performed") and that is missing from project facts and the QA role. Per the triage rule that missing required behavior cannot be downgraded away by severity, it is gated as a clear in-scope fix for the developer, with a QA recheck.
- 2026-10-27, light reviews qa-002 and analysis-002 (task 5.5): no critical findings or warnings. The orchestrator gated QA-041 (above) and ANALYSIS-041. ANALYSIS-041 is gated because the fix for task 4.1 made `docs/agent-workflow.md` record the analyzer write refusal as observed, while the workflow still calls it unestablished. Also, in A4 the analyzer did not attempt the write at all, because its client-provided instructions told it not to, so no refusal message existed for the header. ANALYSIS-042 and ANALYSIS-043 are deferred under the severity gate. Fix round 1 of at most 2: A6 developer, then rechecks A7 (QA) and A8 (analysis).
- 2026-10-27, A4 fallback: the analyzer made no write attempt. The orchestrator saved `analysis-002.md` verbatim under a header recording that no write was attempted and why.
- 2026-10-27, QA-041 verified by the orchestrator, based on the independent recheck in qa-003 (the A6 fix in `project-facts.md` Safety; no regressions; identity unchanged before and after).
- 2026-10-27, ANALYSIS-041 verified by the orchestrator, based on the independent recheck in analysis-003 (refused-write fallback; saved under a header quoting the refusal). The analyzer also noted, as an observation rather than a finding, that `docs/agent-workflow.md` Known client restrictions records only the refused-write run, while the no-attempt run is recorded in the analysis-002 header. This is recorded as an optional follow-up: add the no-attempt observation to the docs page when it is next edited. Fix round 1 closed both gated findings; no second round was needed.
- Side effect reported by QA (A1): its probe dashboard on port 47951 (fixture data under the ignored `artifacts/qa-001/`) was left running, because the client refused the specialist's attempt to stop it. The orchestrator did not stop it either and reported it to the user on 2026-10-27 for a decision. On the user's instruction, the orchestrator stopped it on 2026-10-27; port 47951 was confirmed free.

## Orchestrator verification notes

Commands from `.agents/roles/project-facts.md` were run on the branch on 2026-10-27 (task 1.4). Logs are under the ignored `artifacts/agent-workflow-verify/`.

| Command | Result |
| --- | --- |
| `npm ci --prefix dashboard --no-audit --no-fund` | passed |
| `npm test --prefix dashboard` | passed (9 tests) |
| `dotnet restore --locked-mode` | passed |
| `dotnet build -c Release --no-restore` | passed |
| `dotnet test -c Release --no-build --no-restore` | passed (219 tests) |
| `dotnet format ... --verify-no-changes --include src/WorkLog/Dashboard/DashboardHost.cs` | ran; reports pre-existing whitespace and import-order violations. CI does not enforce formatting, so project facts now limit format findings to changed lines |
| `./scripts/review-identity.ps1 -Baseline HEAD -Paths src dashboard/src` | passed; `git status` unchanged |
| `./scripts/package.ps1` | passed |
| `npm run test:e2e --prefix dashboard` (Release build, OpenSpec CLI 1.13.1 on PATH) | 10 passed, 1 failed: "keeps history available when a declared store cannot be resolved" |
| `worklog init --dry-run` on a disposable fixture repository | passed; no plan touches the new workflow files |

The e2e failure is pre-existing and depends on the environment, not on this change. The fixture `store-member` contains both a `store:` declaration and a real `openspec/` directory. OpenSpec CLI 1.13.1 resolves such a directory as its own root and warns that the declaration is ignored. The dashboard follows the CLI's resolution, so the "Must not fallback" change appears. The recorded v0.3.0 validation ran with the CLI absent from PATH (`WORKLOG_DASHBOARD_NO_NODE`), where the dashboard's own fallback rule applies. The scenario's expectation needs re-deciding against current CLI behavior. It is listed in the handoff's follow-up list for `fix-dashboard-reliability` triage.

Native discovery (task 3.1), 2026-10-27: Claude Code 2.1.311 no longer has the `/agents` wizard ("The /agents wizard has been removed"). Launching `qa` and `code-analyzer` from the session that created the adapter files failed with "Agent type '<role>' not found. Available agents: claude, claude-code-guide, Explore, general-purpose, Plan, statusline-setup". This is expected, because project subagents load at session start. Discovery must be verified from a new session. The user then reported, from a new Claude Code session in this repository, that the session lists `developer`, `qa`, and `code-analyzer` as launchable subagent types. That is evidence of registration as reported by that session's assistant. Task 3.1 is checked once that session launches each role. Assignments A1 and A2 were therefore not started, and their reservations remain unused and valid. On 2026-10-27, session eae5a20b (Claude Code 2.1.311, started after the adapter files existed) launched `developer` (A0), `qa` (A1), and `code-analyzer` (A2) by name through the Agent tool; each launch was accepted.

Analyzer report-write fallback (A2), 2026-10-27: on Claude Code 2.1.311, project subagent, auto mode, the `code-analyzer` subagent's Write of `reports/analysis-001.md` was refused with "Subagents should return findings as text, not write report files. Include this content in your final response instead." The analyzer returned the full text and did not retry through another tool. The orchestrator saved it verbatim at `analysis-001.md` under a header that records the refusal. This reproduces the Bell Tower observation on WorkLog's installed client version.

The dry run also showed that overriding `USERPROFILE`/`HOME` does not redirect init's `~/.claude.json` path, because init uses the Windows profile folder. Project facts now restrict reviewer use of init to `--dry-run`.

## Calibration comparison

Compared on 2026-10-27 with the answer key in `docs/handoffs/2026-10-27-dashboard-follow-ups.md`. Only the orchestrator read the key. Both reviewers reported following the reading limits and listed the paths they read outside the target.

| Key | Defect | Result | Caught by | Heuristic that should have caught a miss |
| --- | --- | --- | --- | --- |
| A | History range freezes | caught | QA-002 (time passes, with screenshot) | n/a. The persisted day focus and the missing presets were not reported separately |
| B | Unbounded reading positions, 64 KiB failure | caught | QA-003, ANALYSIS-004 | n/a |
| C | Escape in the comment textarea closes the reader | caught | QA-005 (also found the delete-confirmation case) | n/a |
| D | Test REVIEW markers in the archived proposal | caught | QA-008 | n/a |
| E | Search `unavailable` ignores repository scope | caught | QA-001 | n/a |
| P | Poll cost (discovery, search, timeline) | caught | ANALYSIS-001, 005, 006 (measured); QA-004 | n/a |
| F1 | Unknown link source returns 503, not 404 | missed | none | QA error handling: for each endpoint in scope, request unknown or nonexistent identifiers and check the expected status code. QA probed path escapes only on `/api/document` |
| F2 | Search section labels read `#` lines inside fenced code | missed | none | QA parser edge cases: try Markdown constructs such as fenced code against each server and client parser in scope |
| F3 | Invalid timezone accepted; From after Through not validated | missed | none | QA input validation: enter invalid, empty, and reversed values in each user-editable field. QA tested valid zones exhaustively (QA-006) but no invalid input |
| F4 | Windows OpenSpec CLI lookup finds only npm-global installs | missed | none | Analyzer or QA environment variance: check how external executables are located against the supported install methods |

Result: 6 of 10 key items were caught, including every item the key marks as required (A–E and P). All four smaller items (F1–F4) were missed. Each is a defect class that no current heuristic names: error-status mapping, parser edge cases, invalid input, and external tool discovery. Task 5.4 amends the roles and templates for these classes.

New confirmed defects outside the key: QA-006 (skipped-midnight zones), ANALYSIS-002 (full document resent on every poll), ANALYSIS-003 (idle state rewrite with fsync), and the suggestions QA-009, QA-010 and QA-011. QA-007 was already known. These are routed through the handoff's follow-up list (task 5.6).

## Completion assessment

Assessed 2026-10-27 by orchestrator session eae5a20b.

- Current QA / analysis reports:
  - Calibration (task 5.2): [qa-001](qa-001.md), [analysis-001](analysis-001.md) at `582f8bc`.
  - Workflow files (task 5.5): [qa-002](qa-002.md) and [analysis-002](analysis-002.md) at untracked=17/9f7af19ec099.
  - Rechecks: [qa-003](qa-003.md) and [analysis-003](analysis-003.md) at the current identity `head=582f8bc baseline=582f8bc files=18 diff=9fb5e3d6075e untracked=17/44c52416a753`.
  - Carry-forward rationale: between those two identities, A6 changed only `.agents/workflows/openspec-delegation.md` (the Reports and evidence fallback paragraph) and `.agents/roles/project-facts.md` (one Safety bullet). Both rechecks covered those files for regression. The qa-002/analysis-002 coverage of the other 16 files is unaffected.
- Required acceptance:
  - Tasks 1.1–5.6 have evidence in the reports and the notes above.
  - Native Claude Code launch of all three roles was observed (A0, A1, A2).
  - The Codex adapters are documented as not natively verified, as the spec allows.
- Findings:
  - All 28 findings are triaged.
  - QA-041 and ANALYSIS-041 were gated, fixed, and verified.
  - Dashboard findings are deferred to `fix-dashboard-reliability` or the handoff follow-up list.
  - QA-004 and ANALYSIS-004 are duplicates.
  - ANALYSIS-042 and ANALYSIS-043 are deferred suggestions.
  - No blocking finding remains.
- Nonblocking follow-ups:
  - ANALYSIS-042: align the archive marker pattern with the `ReviewMarker` grammar.
  - ANALYSIS-043: `GIT_OPTIONAL_LOCKS=0` in the identity script.
  - Record the no-attempt fallback run in the docs Known client restrictions section.
  - The handoff follow-up list for dashboard findings.
- Evidence gaps / provider restrictions:
  - Codex adapters and Codex model IDs are not natively verified.
  - The Claude Code code-analyzer report-write restriction is confirmed on 2.1.311 (refused writes in A2 and A8; no attempt in A4).
  - The QA probe process from A1 was left running after the client refused to stop it; the orchestrator stopped it on 2026-10-27 at the user's instruction.
- Archive: requested by the user on 2026-10-27 ("archive and then commit").
  - REVIEW-marker precondition: no lines starting with `<!-- REVIEW` outside fenced code in the change root's Markdown.
  - Spec sync: the user chose to sync. `openspec/specs/repository-agent-workflow/spec.md` was created from the 9 ADDED requirements (new capability), verified identical to the delta, and `openspec validate` passed.
  - Archived to `openspec/changes/archive/2026-10-27-add-agent-workflow/`; the handoff's report links were updated to the archive path.
- Commit: authorized by the user on 2026-10-27, after the archive. The SHA is reported in the commit message history, since this file is part of that commit.
- OpenSpec task reconciliation: all 16 tasks checked.
