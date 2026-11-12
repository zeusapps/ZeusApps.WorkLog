# Repository instructions

## Claude Code and Codex parity

WorkLog integrates with Claude Code and Codex through the same local MCP server. Keep that integration matched between clients by default.

- `worklog init`, managed logging and update-hint instructions, MCP tool contracts, and launcher behavior must support both clients unless a verified hard provider or platform restriction prevents it. A report originating from one client does not limit the implementation scope to that client.
- Keep instruction text and policy shared (for example `src/WorkLog/Integration/AgentInstructions.cs`), with thin per-client adapters under `src/WorkLog/Integration/Clients`.
- Accept an exception only with evidence of a hard restriction: the exact operation, client version and mode, primary documentation or a reproducible probe, the supported fallback, and regression coverage. Missing implementation or investigation is unfinished work, not proof of a restriction.
- The dashboard is client-neutral; parity review applies to it only where it changes integration or MCP behavior.

## Required checks are authorized

When implementation is authorized, the builds, tests, and validation required by the applicable workflow are included in that authorization. Run them without asking the user for conversational permission or confirmation, and pass this authorization on in specialist assignments. If a check is blocked, report the concrete limitation and continue independent work. Follow tool-enforced sandbox permissions; never disguise or bypass a required approval.

## Delegated OpenSpec workflow

The main agent is the orchestrator, in either Claude Code or Codex: read `.agents/workflows/openspec-delegation.md` before repository work, delegate implementation to the developer role, and have implementations reviewed by QA and/or code-analyzer roles at the depth the workflow's review tiers set (both roles at milestones and completion). Specialist subagents (`.claude/agents/`, `.codex/agents/`) follow their role file under `.agents/roles/` and `.agents/roles/project-facts.md`, and do not read the workflow. Keep the selected OpenSpec skill's authorization boundaries: exploration and proposal requests never start implementation automatically, and if required delegation is unavailable, report the exact limitation and request a decision rather than silently perform the specialist's work in the main agent.

How-to: [docs/agent-workflow.md](docs/agent-workflow.md).
