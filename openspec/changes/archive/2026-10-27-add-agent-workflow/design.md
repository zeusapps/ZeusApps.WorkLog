# Design

## Context

See proposal.md for motivation. WorkLog currently has generated OpenSpec skills (`.claude/skills`, `.claude/commands/opsx`, `.agents/skills`) and a `.codex/config.toml` whose `worklog:begin`/`worklog:server:begin` blocks are owned and rewritten by `worklog init`. It has no `AGENTS.md`, no `CLAUDE.md`, no `.claude/agents`, no `.codex/agents`, and no `docs/development.md`.

The reference implementation is Bell Tower (`https://github.com/zeusapps/ZeusApps.BellTower`, reviewed at commit `aadbfb3`; a sibling checkout is usually at `../ZeusApps.BellTower`). Port from these files:

| Bell Tower source | WorkLog destination | Port mode |
| --- | --- | --- |
| `AGENTS.md` (delegated-workflow section only) | `AGENTS.md` | Adapt: drop Bell Tower product parity text, keep the orchestrator paragraph |
| `.agents/workflows/openspec-delegation.md` | same path | Copy, then rename Bell Tower references and add the archive precondition |
| `.agents/roles/developer.md`, `qa.md`, `code-analyzer.md` | same paths | Copy, then replace Bell Tower specifics and add the heuristics below |
| `.agents/roles/project-facts.md` | same path | Rewrite for WorkLog (see Decision 3) |
| `.agents/templates/qa-report.md`, `analysis-report.md`, `findings.md` | same paths | Copy; add heuristic rows to the QA and analysis templates |
| `.claude/agents/*.md`, `.codex/agents/*.toml` | same paths | Copy unchanged apart from names in descriptions |
| `scripts/review-identity.ps1` | same path | Copy verbatim (it is repository-agnostic) |
| `docs/agent-workflow.md` | same path | Adapt; known client restrictions are re-verified, not inherited |
| `openspec/specs/repository-agent-workflow/spec.md` | this change's delta | Already adapted in this change's spec |

If the sibling checkout is unavailable, fetch the files from GitHub at that commit. Do not invent their content. The spec delta in this change is authoritative wherever Bell Tower differs.

## Goals / Non-Goals

**Goals:**
- One shared policy, thin native adapters, and identical behavior from Claude Code and Codex.
- Role files that are self-contained for a fresh specialist: commands, safety, ownership, and return format.
- Heuristics that target the defect classes found in v0.3.0.

**Non-Goals:**
- Bell Tower's product parity policy, VS Code extension checks, tray, or audio safety rules.
- Editing generated OpenSpec skills, or teaching `worklog init` about these files.
- Automated enforcement of reviewer write limits. They stay behavioral, as in Bell Tower.

## Decisions

### 1. Port the workflow rather than design a new one
Bell Tower's workflow has been exercised across both clients, including a recorded Claude Code restriction where the analyzer subagent's report write was refused and the orchestrator saved the text instead. Reusing it keeps the two ZeusApps repositories consistent for the same user. Alternative: a lighter "single reviewer" loop. Rejected because the v0.3.0 defects fall in both correctness (QA) and cost (analyzer) categories.

### 2. Product parity scope
WorkLog's `local-agent-integration` capability supports Codex and Claude Code. Its parity rule is narrower than Bell Tower's: integration instructions, `init`, and MCP behavior must stay matched across both clients. The dashboard is client-neutral. `project-facts.md` states this scope. Reviewers flag parity gaps only for integration and MCP changes.

### 3. WorkLog project facts
`project-facts.md` contains:
- Commands, taken from `.github/workflows/validate.yml`:
  - `npm ci --prefix dashboard --no-audit --no-fund`, `npm test --prefix dashboard`
  - `dotnet restore --locked-mode`, `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-build`
  - `./scripts/package.ps1`
  - Browser acceptance: `npm run test:e2e --prefix dashboard`, with `WORKLOG_DASHBOARD_EXECUTABLE` pointing at a packaged `worklog.exe`. Without it, the Playwright config's own server is used.
  - Format verification: `dotnet format ZeusApps.WorkLog.slnx --no-restore --verify-no-changes --include <files>`. The developer runs it without `--verify-no-changes` only on its own changed files.
  - Review identity: `./scripts/review-identity.ps1 -Baseline <commit> -Paths <paths>`.
- Layout:
  - `src/WorkLog`: Cli, Configuration, Dashboard, Infrastructure, Integration, Journaling, Mcp, Persistence, Reporting, Repositories, Updates.
  - `dashboard/` (Angular client), `tests/WorkLog.Tests`, `tools/WorkLog.Smoke`, `tools/WorkLog.LauncherProbe`.
  - `docs/architecture.md` is the architecture reference. Judge against it, and do not restate it.
- Safety (mirrors the spec requirement):
  - Never use `%LOCALAPPDATA%\ZeusApps.WorkLog`. Always pass `--data-dir` to a disposable directory.
  - Never bind or connect to port 47831. Use `--port` with a free port and `--no-open`.
  - Never run `init` or `update` against real repositories or client settings.
  - Never edit `~/.claude` or `~/.codex`.
  - Never insert review comments into repository OpenSpec documents.
  - Never stop user processes, including running dashboard or MCP servers.
  - Fetch library documentation through the user's documentation workflow (`ctx7`) when depending on a library API.
- Committable content hygiene: as in Bell Tower, plus the residue patterns `<!-- REVIEW`, `test comment`, `foo bar`, and `lorem` in committed Markdown under `openspec/`.

### 4. Heuristic checklists live in the role files and templates
QA gets a "Time, growth, input focus, scope, residue" section, and the QA report template gets matching rows that must be marked passed, failed, not performed, or not applicable. The analyzer gets a "Steady-state cost and bounds" section, and the analysis template gets a row for per-interval idle work and unbounded state. Putting the checks into the templates forces an explicit answer instead of relying on reviewer memory.

### 5. Archive precondition in the orchestrator workflow
The workflow file adds an archive step: search the change root for `<!-- REVIEW` outside fenced code. If any remain, list them and ask the user. This is the orchestrator's rule, because the archive skill itself is generated and must not be edited.

### 6. Calibration run
After the files exist, the orchestrator runs one QA and one analyzer assignment against the v0.3.0 dashboard. The target is commit `582f8bc`, scoped to `src/WorkLog/Dashboard`, `src/WorkLog/Cli/DashboardCommand.cs`, `dashboard/src`, `docs/dashboard-validation.md`, `openspec/specs/{work-dashboard,openspec-browser,document-feedback}`, and `openspec/changes/archive/2026-10-26-add-work-dashboard`. Reports go to this change's `reports/qa-001.md` and `reports/analysis-001.md`.

The assignment does not mention the known defects. Reviewers are told to read only the scoped paths, and not `openspec/changes/*` active changes or `docs/handoffs/2026-10-27-dashboard-follow-ups.md`, which contain the answer key. The orchestrator then compares the findings with the answer key in the handoff. For each missed defect it records which heuristic should have caught it, and it amends the role or template once. No fixes are made in this change: confirmed new defects are added to `fix-dashboard-reliability`'s scope through the handoff's follow-up list, or proposed separately if they are out of its scope.

## Risks / Trade-offs

- Reviewers may read the answer key, which contaminates calibration. → The assignment restricts reading paths, and reports record what was read. The limitation is recorded rather than claimed as an isolated experiment.
- Claude Code may refuse analyzer report writes, as observed in Bell Tower. → Keep the verbatim-save fallback, and re-verify on the installed version instead of inheriting the claim.
- The Codex model names in the `.toml` adapters may not exist in the installed Codex. → Mark them "documented, not natively verified", as Bell Tower does, until a trial records the resolved model.
- Process overhead for small changes. → Light reviews for docs-only or config-only targets, a fix gate limited to critical findings and acceptance-breaking warnings, and a two-cycle cap.

## Migration Plan

Additive files only. Restart Claude Code and Codex sessions after adding the adapters so that `AGENTS.md` and the agents are discovered. Roll back by deleting the added files; product behavior is unaffected.
