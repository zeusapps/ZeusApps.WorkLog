# Spec Delta

## Purpose

Define WorkLog's repository workflow for Claude Code and Codex. An OpenSpec orchestrator delegates implementation and independent reviews to developer, QA, and code-analyzer specialists through thin native adapters over shared role policy, with durable change-local evidence, bounded resolution of findings, and WorkLog-specific safety and review heuristics.

## ADDED Requirements

### Requirement: Orchestrator owns planning and delegation
The main agent SHALL own OpenSpec discovery, planning artifacts, task assignments, findings triage, progress, completion decisions, and commits. It SHALL delegate implementation and verification to the developer, QA, and code-analyzer specialists rather than perform their work directly. Assignments MUST identify the resolved change root, task or requirement IDs, acceptance criteria, relevant context, dependencies, permitted outputs, and the review target. Planning-only requests SHALL remain planning-only. If delegation is unavailable, the orchestrator SHALL report the exact limitation and request a decision rather than silently implement inline. Specialists SHALL NOT commit, push, create branches, or rewrite history. The orchestrator SHALL commit only after completion criteria hold, SHALL include only scoped implementation paths and the change's artifacts, and SHALL first create a change branch when the checkout is on the default branch. Pushing, publishing a release, or installing a build into the user's installation SHALL require separate user authorization.

#### Scenario: Implementation is authorized
- **WHEN** the user starts applying a change with actionable tasks
- **THEN** the orchestrator reads the change context, assigns bounded implementation work to a developer, and keeps ownership of planning and tracking

#### Scenario: Only a proposal is requested
- **WHEN** the user requests exploration or proposal artifacts
- **THEN** the orchestrator respects that workflow's write and approval boundaries and does not start implementation

#### Scenario: Delegation cannot run
- **WHEN** the installed client or session cannot launch the required subagent
- **THEN** the orchestrator records the exact limitation and asks how to proceed without claiming the delegated workflow ran

#### Scenario: Verified work is committed
- **WHEN** both current reviews exist, every finding is triaged, and no blocking finding remains
- **THEN** the orchestrator commits the scoped implementation and change artifacts on a change branch, leaves unrelated user changes uncommitted, and does not push without authorization

### Requirement: Developer owns implementation changes
The developer SHALL make implementation changes and approved review fixes, including production code, test sources, configuration, dashboard sources, and implementation documentation, within assigned paths. It SHALL preserve concurrent user changes, run proportional developer checks, and return changed paths, behavior, check results, and unresolved limitations. It SHALL return scope or design conflicts to the orchestrator and SHALL NOT approve its own fixes, close findings, edit the findings ledger, or rewrite planning artifacts.

#### Scenario: Implementation conflicts with the plan
- **WHEN** a developer discovers that the requested behavior needs a scope or design change
- **THEN** it reports the conflict to the orchestrator without silently reducing the requirement or editing planning artifacts

#### Scenario: A test gap needs a regression test
- **WHEN** QA identifies missing coverage
- **THEN** the orchestrator assigns the test change to the developer and QA independently verifies the result

### Requirement: Independent QA and code analysis
Every implementation SHALL receive both independent QA and code-analyzer review, including small fixes and documentation or configuration changes, with depth proportional to the affected behavior. QA SHALL review correctness and acceptance coverage and execute applicable functional checks, including .NET tests, dashboard unit tests, and browser acceptance when dashboard behavior changes. The analyzer SHALL inspect duplication, complexity, simplification, architectural consistency, and performance. Performance findings MUST distinguish measurements from hypotheses. Reviewers SHALL NOT edit product code, test sources, configuration, planning artifacts, the findings ledger, or each other's reports. Their repository writes SHALL be limited to their assigned change-local reports and evidence.

#### Scenario: QA discovers a defect
- **WHEN** QA reproduces incorrect behavior during review
- **THEN** it records the defect and reproduction in its report without patching the implementation

#### Scenario: Review scope is small
- **WHEN** the developer changes a small documentation or configuration item
- **THEN** both reviewers still produce reports, with justified checks or explicitly non-applicable functional scenarios

### Requirement: Reviews probe time, growth, steady-state cost, and residue
QA review of user-facing or persisted behavior SHALL include applicable checks for behavior after time passes (for example reopening on a later day, crossing midnight, or a remembered relative range), long-lived state that grows with use, keyboard interaction while focus is inside text inputs, and filters that narrow scope. QA SHALL check committed documents, including OpenSpec changes, archived changes, and main specs touched by the target, for leftover test residue such as REVIEW markers, fixture text, or manual-test notes. Code analysis of long-running or polling components SHALL report the cost of the idle steady state, including spawned processes, file reads, and journal reads per interval, with measurements when the analyzer can run them. It SHALL identify collections or persisted state without bounds. A check that a reviewer could not perform SHALL be recorded as not performed with a reason.

#### Scenario: Remembered state is reviewed
- **WHEN** a change persists a user preference or range
- **THEN** the QA report records a check of that behavior on a simulated later day and after repeated use, or records why it is not applicable

#### Scenario: Polling component is analyzed
- **WHEN** a change adds or modifies periodic polling
- **THEN** the analysis report states the per-interval work while idle, as a measurement or a labeled hypothesis

#### Scenario: Test residue is committed
- **WHEN** a committed document in the review target contains a REVIEW marker or test note that is not part of intended content
- **THEN** QA reports it with file and line as a finding

### Requirement: WorkLog live-instance safety
Specialists SHALL use isolated data directories, journals, checkouts, OpenSpec fixtures, and non-default dashboard ports for checks. They SHALL NOT read or write the user's real WorkLog data directory, connect to or stop the user's running dashboard or MCP servers, run `worklog init` or `worklog update` against the user's repositories or client settings, edit personal client settings (`~/.claude`, `~/.codex`), or write review comments into this repository's own OpenSpec documents. A check that requires the live installation SHALL be recorded as not performed.

#### Scenario: A check needs the dashboard
- **WHEN** QA needs to exercise the dashboard
- **THEN** it launches it with a disposable data directory, synthetic sources, and a non-default port, leaving the user's running dashboard and data untouched

#### Scenario: A review-comment check runs
- **WHEN** QA exercises review comment insertion
- **THEN** it writes only into fixture documents, never into repository OpenSpec documents

### Requirement: Durable change-local reports and findings ledger
Each review round SHALL produce a separately numbered QA or analysis report under `reports/` in the resolved change root, including when no findings exist. Reports SHALL identify role, client, client version, mode, resolved model, date, reviewed commit and dirty-tree identity, scope, related tasks or requirements, checks and outcomes, limitations, and stable finding IDs with severity, location, evidence, and recommendation. Screenshots SHALL be stored under the change's `screenshots/` directory and linked relatively. Earlier rounds SHALL be preserved. The orchestrator SHALL maintain `reports/findings.md` as the authoritative ledger, linking every finding to its source report, severity, blocking decision, owner, status history, and verification evidence, and preserving dismissed, duplicate, and deferred entries with rationale. If a client refuses a reviewer's report write, the reviewer SHALL return the full text and the orchestrator SHALL save it verbatim under a header that records the refusal.

#### Scenario: Review is repeated after a fix
- **WHEN** a reviewer rechecks an updated revision
- **THEN** a new numbered report references the earlier findings and the new target without overwriting the original report

#### Scenario: Work resumes in a new session
- **WHEN** another orchestrator resumes the change from a fresh context
- **THEN** it recovers outstanding findings, decisions, assignments, and verification state from the change's reports and ledger

### Requirement: Bounded triage and completion
Only critical findings and warnings that break agreed acceptance or safety SHALL receive a fix round. Suggestions SHALL be recorded as deferred follow-ups. Clear in-scope fixes SHALL be assigned to the developer without routine user approval. Unclear fixes SHALL be investigated and escalated with evidence, options, and a recommendation. A developer fix SHALL move a finding to awaiting verification, and only the orchestrator SHALL close it, using independent recheck evidence. After two fix-and-recheck cycles on one change the orchestrator SHALL escalate any remaining open findings instead of starting a third. Completion SHALL require both current role reports on the current target, triage of every finding, required acceptance evidence, and no unresolved blocking finding. Task checkboxes SHALL NOT be checked on the basis of report existence or developer assertions alone.

#### Scenario: A recheck fails
- **WHEN** independent verification reproduces the problem on the corrected revision
- **THEN** the orchestrator reopens the finding with its history preserved and routes it back through triage

#### Scenario: The fix-cycle limit is reached
- **WHEN** two fix-and-recheck cycles have run and a finding remains open
- **THEN** the orchestrator presents the open findings, evidence, and a recommendation to the user

### Requirement: Archive requires resolved review comments
Before archiving a change, the orchestrator SHALL check the change's Markdown documents for REVIEW markers. A change with remaining markers SHALL NOT be archived unless the user explicitly accepts archiving them, and that decision SHALL be recorded in the ledger.

#### Scenario: Leftover comments block archive
- **WHEN** an archive is requested and the change's design still contains a REVIEW marker
- **THEN** the orchestrator lists the remaining markers and asks the user whether to resolve them or archive anyway

### Requirement: Native adapters, shared entry point, and per-role models
`AGENTS.md` SHALL be the single repository instruction entry point for both clients. The repository SHALL NOT add a project `CLAUDE.md`, `.claude/CLAUDE.md`, or committed `CLAUDE.local.md` unless it begins with `@AGENTS.md`. Claude Code and Codex SHALL each provide the developer, QA, and code-analyzer roles through thin native definitions that point to the shared role file, the shared project facts file, and, for reviewers, the report template, without duplicating role policy. Native definitions SHALL declare per-role defaults where supported: a cost-efficient model for developer and QA, and the strongest reasoning model with high effort for the code analyzer. For Claude Code these SHALL be `sonnet` for developer and QA and `opus` for the analyzer. An assignment MAY override the model with a recorded reason. Model defaults SHALL NOT require personal settings changes. Each change SHALL have at most one active orchestrator, recorded in an advisory marker in its ledger. A resuming orchestrator SHALL continue report numbering and finding IDs without reuse. Unverified native behavior SHALL be documented as unverified, not as passed or restricted.

#### Scenario: A specialist starts an assignment
- **WHEN** a developer, QA, or analyzer subagent of either client receives an assignment
- **THEN** its role file, the shared project facts file, and its template give it the commands, safety limits, ownership rules, and return format without loading the orchestrator workflow

#### Scenario: A change is handed off between clients
- **WHEN** a change orchestrated in one client is resumed in the other
- **THEN** the new orchestrator reads and updates the marker, continues numbering from the ledger, and preserves earlier reports
