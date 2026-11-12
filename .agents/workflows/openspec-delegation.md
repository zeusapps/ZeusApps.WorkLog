# Orchestrator workflow for delegated OpenSpec work

This policy is for the orchestrator, the main agent in either Claude Code or Codex. Specialists (developer, QA, code analyzer) do not read it: each follows its role file and `project-facts.md` in `.agents/roles/`, with the report template for reviewers. Each rule lives in one place. This file owns assignment, triage, completion, archive preconditions, commits, model selection, and mixed-client operation.

## Planning and authorization

- Invoke the applicable OpenSpec skill and preserve its authorization boundaries, CLI-controlled state, context, and completion criteria. Use CLI-resolved change and context paths; never guess from the only active change or the current directory.
- The orchestrator owns exploration, proposal/design/spec artifacts, `tasks.md`, assignments, `reports/findings.md`, user communication, commits, and final status. Planning requests stay planning-only. An apply request authorizes developer work within that change's agreed scope.
- Delegate all implementation edits to the developer, including sources, test sources, dashboard sources, configuration, documentation, and fixes. Never implement a convenient fallback inline. If delegation cannot run, describe the exact client, version, and mode limitation, and ask how to proceed while completing independent authorized work.
- Do not edit generated OpenSpec skill copies (`.claude/skills`, `.claude/commands/opsx`, `.agents/skills`) or add a competing task tracker. Fulfill apply through developer assignments, and verify's completeness, correctness, and coherence criteria through independent QA evidence.
- Preserve user changes, explicit overrides, current sessions, and unrelated active changes. Do not change personal configuration, stop user processes (including a running dashboard or MCP servers), install builds into the user's WorkLog installation, publish, or archive merely to complete a task.

## Assignment and reservation

Before launching a specialist, supply:

| Field | Required content |
| --- | --- |
| Role and objective | Developer, QA, or code-analyzer; bounded outcome |
| Scope | Absolute resolved change root; task and requirement IDs; relevant context paths |
| Acceptance | Expected behavior, dependencies, and, for integration or MCP changes, the matched Claude Code and Codex cases |
| Review target | Baseline commit and the output of `scripts/review-identity.ps1` for the scoped paths, including relevant dirty or untracked files |
| Ownership | Permitted implementation paths, or exact reserved report and evidence paths; explicit prohibited writes |
| Reading limits | Paths the specialist must not read when isolation matters (for example a calibration answer key) |
| Finding allocation | Existing IDs to recheck and an unused ID range for new findings |
| Model | The role default, or the override and its reason (see Model selection) |
| Return | The fixed block defined in the role file, plus report paths |

- Reserve report filenames and finding IDs in `reports/findings.md` before concurrent reviewers start. Number reports per role and change, continuing across sessions and clients. Reserve `QA-001` and `ANALYSIS-001` finding IDs independently of report numbers. Never reuse an ID or an allocation after an interruption.
- Use one developer writer per shared scope or checkout and one ledger owner per change. Parallel developers need disjoint ownership or an isolated checkout that contains the assigned revision and the relevant user and untracked work. Do not assume a default-branch worktree holds the current implementation.
- Review depth follows the target tier. Start reviews on a stable developer handoff; when both roles run, they may run concurrently on distinct reserved outputs.
  - **Milestone** (a completed task group, or the final completion target): both QA and code analysis.
  - **Fix** (a gated finding's correction, with a test that failed before and passes after): one reviewer, which is QA for functional or runtime findings and the analyzer for structural ones. The reviewer reads the finding, the diff, and the failing/passing evidence, not the whole change history.
  - **Harness or docs** (test fixtures, acceptance scripts, validation docs, with no product source changed): one light QA review limited to correctness and safety. The next milestone review covers it again.
- A live-agent evaluation is planned work: it must appear in the change's `tasks.md`, and its assignment repeats the clients, session count, and per-session budget. Parity applies: evaluate Claude Code and Codex alike unless a restriction is evidenced.
- Subagents cannot take over orchestration. Route their additional delegation needs through yourself.

## Review target identity

Run the Review identity command from [project facts](../roles/project-facts.md) for a dirty-tree target. It is a stateless read-only wrapper over standard Git output and SHA-256 hashes, not a tracking tool, and stores nothing. Record its output in the assignment. Check that each reviewer's report records matching before and after identities, and treat any difference as an unexpected edit. Do not claim an enforcement mechanism that was not demonstrated; tool access does not make report-only scope an OS-enforced sandbox.

Exclude reports, screenshots, `tasks.md`, and other orchestrator tracking edits from the implementation identity. Never exclude an actual implementation file because it is untracked. Later implementation changes require the affected checks and reviews again. An older report is historical evidence, not current approval; carry forward unaffected coverage only with explicit rationale and target mapping. On resumption, read the marker, ledger, reservations, latest reports, and current target before assigning work.

## Reports and evidence

Reviewers use `.agents/templates/qa-report.md` and `analysis-report.md`; you use `findings.md`. Review evidence lives in the resolved change:

```text
<change-root>/
  reports/qa-001.md
  reports/analysis-001.md
  reports/findings.md
  screenshots/qa-001-scenario-description.png
```

If a reviewer returns its report as text instead of writing it, save that text verbatim at the reserved path under a header naming the reviewer, client, version, mode, and model, and record the fallback in the ledger. The header also states which case applies: the client refused the reviewer's write, and the header records the exact refusal message; or the reviewer made no write attempt because the client's own subagent instructions told it to return findings as text, and the header says so and that no refusal message exists. The fallback covers only these two cases; reviewers that wrote their own reports need no header. WorkLog observed this on Claude Code 2.1.311 for the code-analyzer subagent (a refused write in one run, no attempt in another); see [Known client restrictions](../../docs/agent-workflow.md#known-client-restrictions). Re-check on newer client versions.

Earlier rounds and screenshots are immutable. A recheck or factual correction is a new numbered report. A report exists even with no findings. When accepting a report, check that every check, including each heuristic row, is marked passed, failed, not performed, or not applicable with a reason; that fixtures, packaged builds, and live observations are labeled distinctly; and that analyzer performance hypotheses are not presented as measurements.

## Finding triage and fix routing

Only the orchestrator edits `reports/findings.md`. Track every finding, including duplicates, dismissals, and deferrals. The summary table links IDs, source reports, task or requirement, severity, blocking decision, status, owner, and latest verification. Each entry's history records dates, rationale, assignments, developer results, user decisions, duplicate relationships, and independent evidence.

Keep the resume cost small. `findings.md` holds the `Current orchestrator` block, a **Current state** section of at most 40 lines (open blockers, active assignments, next step, latest evidence paths), the summary table, and history only for open findings, at most five lines per entry. When a finding is verified, dismissed, or deferred, move its history to `reports/findings-archive.md`, which is not read on resumption unless a specific entry is needed. Review identities and hashes belong in assignments and reports, not repeated in ledger prose. Do not keep a separate handoff file: the Current state section is the handoff.

Severities are `critical`, `warning`, and `suggestion`; record the blocking disposition separately. Missing required behavior cannot be downgraded away by severity. States are `reported`, `assigned`, `awaiting-verification`, `verified`, `awaiting-user-decision`, `dismissed`, `duplicate`, and `deferred`. A failed recheck returns the finding to `reported`, with history retained.

- Severity gate: only `critical` findings and `warning`s that break agreed acceptance or safety get a fix round. Record every `suggestion` as a deferred follow-up in the ledger, with no fix or recheck unless the user asks.
- A gated finding with a clear fix has an expected result in the agreed acceptance criteria, stays in scope, and needs no unresolved behavior or architecture choice. Assign it directly to the developer with the finding ID and recheck expectation; do not ask the user for routine approval.
- A recheck assignment names only its finding IDs plus regression. Review depth for docs-only or config-only targets follows the review tiers above.
- After two failed fix-and-recheck cycles on the same finding, stop. Escalate that finding to the user with evidence and a recommendation instead of starting a third cycle. Other findings on the change continue.
- Before attributing or investigating a runtime failure, read the owned processes' own output: stderr/stdout logs, exit codes (for example `0xE0434352` is an unhandled .NET exception), and crash records under the fixture. Cite them in the triage entry. Do not assign theory-driven investigation of a failure whose logs have not been read.
- Every long-running check (browser suites, fault matrices, packaged runs) runs with a bounded overall timeout and captured logs. A run that produces no result within its bound is a finding about the run, not a reason to wait or retry unchanged.
- For uncertainty, request bounded investigation. Escalate remaining decisions to the user with evidence, options, recommendation, and impact. Continue independent work meanwhile; silence is not approval.
- Link duplicates to the canonical ID and preserve both reports. Record dismissal rationale. Optional deferrals need rationale and a follow-up destination or reconsideration condition. Deferring required behavior or coverage needs explicit user agreement and coherent OpenSpec updates.
- Developer fixes enter `awaiting-verification`. QA rechecks correctness and functional findings; the analyzer rechecks its own. Cross-cutting fixes may need both. Only the orchestrator marks a finding `verified`, using independent report evidence; a developer cannot approve its own fix.

## Model selection

Concrete model values and reasoning effort are declared only in the native role definitions ([.claude/agents/](../../.claude/agents), [.codex/agents/](../../.codex/agents)): developer and QA use a cost-efficient model, and the code analyzer uses the strongest reasoning model with high effort. A specialist runs on its role default unless the assignment records an override. To override one assignment, pass the client's per-invocation model setting and record the model and rationale in that assignment's ledger row. Do not change `CLAUDE_CODE_SUBAGENT_MODEL` or other personal settings. For a docs-only or config-only target, the orchestrator may run the analyzer on the client's cost-efficient default as a recorded override; both reviews remain required. Reports record the resolved model ID, not only the alias. If a client lacks per-role model configuration, its roles inherit the parent model and the gap is recorded under the restriction-evidence rules in [AGENTS.md](../../AGENTS.md).

## Mixed-client operation

Claude Code and Codex may be used alternately or concurrently. Each change has at most one active orchestrator.

- `reports/findings.md` carries a `Current orchestrator` block: client, session identity, since (UTC), previous owner. The marker is advisory, not an enforced lock.
- On resuming a change, read the marker first. If it names a different active session, do not take over the ledger silently; ask the user to confirm the hand-off before assigning work. After confirmation, or when the previous owner is finished, update the marker (recording the previous owner), then continue report numbering and finding IDs from the ledger without reuse, regardless of which client produced earlier rounds. Preserve all earlier reports and history.
- Concurrent sessions of either client on different changes keep their own reports, ledgers, and OpenSpec associations. Report paths never cross changes.

## Archive preconditions

Before archiving a change, in addition to the archive skill's own checks:

1. Search the change root's Markdown for lines starting with `<!-- REVIEW` outside fenced code.
2. If any remain, list each with file and line and ask the user whether to resolve them first or archive them as they are. Record the decision in the ledger's completion assessment.
3. Confirm the completion criteria below hold. Archiving needs the user's request; completion alone does not authorize it.

## Commits

Specialists never commit, push, create branches, or rewrite history. Your own artifacts (planning files, `tasks.md`, `reports/findings.md` including its Current state section, saved fallback reports) follow [committable content hygiene](../roles/project-facts.md#committable-content-hygiene).

- Before committing, confirm the current QA report has a passing hygiene check over the committed change files, and re-run the same search over anything written after that report.
- A hygiene finding blocks the commit. Redact it yourself in change artifacts, including earlier immutable reports and screenshots, as the one permitted edit to that history. Record the redaction in the ledger without the value. Route hits in implementation docs to the developer.
- After each milestone review passes, make a local checkpoint commit on the change branch with the scoped implementation paths and the change's artifacts, excluding unrelated user changes. Later review targets then use the checkpoint SHA as their baseline, so identities cover only the new delta. When the checkout is on the default branch, create a change branch first.
- After the completion criteria hold, make the final scoped commit the same way.
- Push and publication need separate user authorization. Record checkpoint and final commit SHAs in the ledger. Until the first commit, the review identity is the dirty-tree identity above.

## Completion

Before declaring implementation complete, require:
- both current role reports on the current target,
- every finding triaged,
- the change's required acceptance satisfied with evidence of the kind it names, and
- no unresolved blocking finding or unapproved scope deferral.

Update task checkboxes only when behavior and required evidence satisfy the controlling OpenSpec workflow; report existence and developer assertions prove nothing. Untried native client behavior remains unverified, not passed or proven restricted. Fixture success does not replace packaged or live acceptance where a task names it. Provider restrictions follow the evidence rules in [AGENTS.md](../../AGENTS.md). Keep optional follow-ups visible and report remaining gaps honestly. Archival and publication follow their own user authorization.
