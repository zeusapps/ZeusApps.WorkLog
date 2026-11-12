# Repository agents

How to run WorkLog's delegated OpenSpec workflow from Claude Code or Codex. This page is a how-to; the rules live in the files it links.

| What | Where |
| --- | --- |
| Orchestrator policy (assignment, triage, completion, archive preconditions, commits, models, mixed-client) | [.agents/workflows/openspec-delegation.md](../.agents/workflows/openspec-delegation.md) |
| Specialist rules (developer, QA, code analyzer) and the shared [project-facts.md](../.agents/roles/project-facts.md) (commands, layout, parity, safety, hygiene) | [.agents/roles/](../.agents/roles) |
| Report and ledger formats | [QA](../.agents/templates/qa-report.md), [analysis](../.agents/templates/analysis-report.md), [findings](../.agents/templates/findings.md) |
| Parity policy and entry point | [AGENTS.md](../AGENTS.md) |
| Architecture reference for reviews | [architecture.md](architecture.md) |

The workflow is ported from Bell Tower (`zeusapps/ZeusApps.BellTower` at `aadbfb3`) and adapted to WorkLog. Compared with Bell Tower, it adds the QA "time, growth, input focus, scope, residue" checks, the analyzer's "steady-state cost and bounds" checks, and an archive precondition for leftover review comments.

## Start and activate

Use the OpenSpec skills: explore or propose for planning, apply for authorized implementation, verify for evidence, and archive only when you ask. Planning never starts apply. For example: `Apply <change> using the developer, QA, and code-analyzer roles.`

| Client | Role definitions | Activate after adding or editing them |
| --- | --- | --- |
| Claude Code | [.claude/agents/](../.claude/agents) | Start a new session in the repository root. Project subagents are discovered only at session start; a running session does not pick up new or edited files. Claude Code 2.1.311 removed the `/agents` wizard, so check discovery by asking the orchestrator to launch a role: an unknown role fails with the list of available agents. |
| Codex | [.codex/agents/](../.codex/agents) | Start or reload a trusted project session |

Edits to `AGENTS.md` and the role adapters are read at session start, so start a new session before expecting the orchestrator or subagents to see them.

Each definition is a thin adapter that points to its role file, `project-facts.md`, and, for reviewers, its report template. A listed role does not prove execution; check the client, version, mode, and model recorded in the reports.

`.codex/config.toml` also contains blocks owned by `worklog init` (`worklog:begin`, `worklog:server:begin`). The role adapters live in separate files under `.codex/agents/` and never touch those blocks.

## Models

Defaults are declared only in the adapters, so nothing personal needs to change:
- Developer and QA use a cost-efficient model (`sonnet` in Claude Code).
- The code analyzer uses the strongest reasoning model with high effort (`opus` in Claude Code).
- The Codex model names are copied from Bell Tower and are documented but not yet verified natively. The comment beside each value in [.codex/agents/](../.codex/agents) says so.

To use a different model for one assignment, the orchestrator passes the client's per-invocation model setting and records the reason in the findings ledger. Reports record the resolved model ID.

## Entry point rule for Claude Code

Claude Code reads `AGENTS.md` only while no `CLAUDE.md`, `.claude/CLAUDE.md`, or `CLAUDE.local.md` exists. If you add one, begin it with `@AGENTS.md`, or Claude stops loading the shared policy.

`worklog init` creates a private, Git-excluded `CLAUDE.local.md` when managed Claude instructions are enabled. Its owned block already includes an `@AGENTS.md` import for this reason, so the shared policy keeps loading. Keep that import if you edit the file.

## Switch clients on a change

1. Finish or pause the current orchestrator's assignments. Prefer a stable handoff with no running specialists.
2. In the other client, ask the new orchestrator to resume the change. It reads the `Current orchestrator` block in the change's `reports/findings.md`, the reservations, the latest reports, and the current target.
3. If the block names a different session that may still be active, the new orchestrator asks you to confirm before taking over. The marker is advisory, not a lock.
4. It updates the block, then continues report numbers and finding IDs without reuse. Earlier reports from either client stay as they are.

Two clients can work on different changes at the same time. Do not have two orchestrators edit one change's ledger.

## Targets, commits, and archive

- Identify a dirty working tree with the Review identity command in [project-facts.md](../.agents/roles/project-facts.md). It only reads Git state and prints a manifest and hashes.
- Specialists never commit. The orchestrator makes local checkpoint commits on a change branch after each milestone review passes, and a final commit at completion. Pushing, tagging, and publishing need your separate approval.
- Before archiving, the orchestrator lists any `<!-- REVIEW` markers left in the change and asks whether to resolve them or archive anyway.
- Reviewers' write limits and the no-nesting rule are behavioral instructions in the role files. Whether a client enforces them natively is documented only after a recorded probe.

## Known client restrictions

Recorded versions: Claude Code 2.1.311 and Codex CLI 0.164.0 on the development machine.

- **Analyzer report write (observed here, 2026-10-27):** on Claude Code 2.1.311, auto mode, the `code-analyzer` project subagent's Write of its assigned report was refused with "Subagents should return findings as text, not write report files. Include this content in your final response instead." It returned the report as text, and the orchestrator saved it under the fallback header and ledger record described in the [workflow](../.agents/workflows/openspec-delegation.md#reports-and-evidence). This matches Bell Tower's observation on 2.1.309.
- **Other roles (same session):** the `qa` subagent wrote its own report and screenshot, and the `developer` subagent launched and loaded its role files. Project subagents are discovered only by sessions started after the files exist.
- **Probe cleanup (same session):** the `qa` subagent's attempt to stop a probe process it had started was refused by the permission system. See the probe-lifetime rule in [project-facts.md](../.agents/roles/project-facts.md).
- Codex has no verified restriction; its native role behavior is not yet verified.
