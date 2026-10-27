# Proposal

## Why

The v0.3.0 dashboard shipped with several defects that independent review would have caught: a History range that stops rolling forward, preferences that grow until saves fail, a 2-second poll that keeps spawning OpenSpec CLI processes, and test review comments committed into an archived change. WorkLog has no delegated workflow, so one agent plans, implements, and judges its own work. The sibling Bell Tower repository (`zeusapps/ZeusApps.BellTower`) already uses an orchestrator with developer, QA, and code-analyzer specialists for Claude Code and Codex. WorkLog should adopt that workflow, adjusted to catch the kinds of defects found here, before the dashboard follow-up changes are applied.

## What Changes

- Add a shared `AGENTS.md` entry point that makes the main agent the orchestrator for OpenSpec apply work, in both Claude Code and Codex.
- Add an orchestrator workflow (`.agents/workflows/openspec-delegation.md`), role files for developer, QA, and code-analyzer, a shared WorkLog `project-facts.md`, and report/ledger templates. These are ported from Bell Tower and adapted to WorkLog's stack (.NET 10, Angular dashboard, Playwright, packaged Windows release), safety boundaries (the user's real data directory, dashboard port, MCP sessions, and OpenSpec documents), and parity scope.
- Add thin native role adapters for both clients: `.claude/agents/*.md` and `.codex/agents/*.toml`. Keep the WorkLog-owned blocks in `.codex/config.toml` untouched.
- Add `scripts/review-identity.ps1`, a read-only identity of the review target that works on dirty trees.
- Add WorkLog-specific review heuristics. QA checks behavior after time passes, long-lived and growing state, keyboard paths inside inputs, and leftover test residue in committed documents. The analyzer measures idle steady-state cost and unbounded collections.
- Add an archive precondition: a change is not archived while REVIEW markers remain in its documents, unless the user explicitly accepts them.
- Add `docs/agent-workflow.md`, a how-to page.
- Run a calibration review of the current v0.3.0 dashboard with fresh QA and code-analyzer sessions, compare the results with the known defect list, and tune the role files for anything the reviewers missed.

## Capabilities

### New Capabilities

- `repository-agent-workflow`: Orchestrator-led delegated OpenSpec workflow for Claude Code and Codex, covering developer, QA, and code-analyzer roles, change-local reports and the findings ledger, bounded fix loops, per-role models, mixed-client hand-off, WorkLog safety boundaries, and review heuristics.

### Modified Capabilities

None. Product behavior is unchanged.

## Impact

- New files: `AGENTS.md`, `.agents/workflows/`, `.agents/roles/`, `.agents/templates/`, `.claude/agents/`, `.codex/agents/`, `scripts/review-identity.ps1`, `docs/agent-workflow.md`. Calibration reports go under this change's `reports/`.
- `README.md` links to the workflow page.
- No product source, journal schema, MCP contract, or release package changes. The existing generated OpenSpec skills under `.claude/skills`, `.claude/commands`, and `.agents/skills` are not edited.
- `worklog init` must never manage or overwrite these files. It already writes only owned blocks, so the new files are outside its scope.
- Follow-up changes `fix-dashboard-reliability`, `add-review-inbox`, `add-archive-browser-and-editor-open`, `add-ticket-and-repository-links`, `add-daily-prep`, and `improve-observation-summaries` are applied through this workflow. See `docs/handoffs/2026-10-27-dashboard-follow-ups.md`.
