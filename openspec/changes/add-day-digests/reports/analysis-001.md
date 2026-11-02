<!-- Saved by the orchestrator. Reviewer: B1-AN code-analyzer subagent; client Claude Code 2.1.312, background subagent mode; model claude-sonnet-5-5 (recorded override). Case: the client refused the reviewer's write. Exact refusal message: "Subagents should return findings as text, not write report files. Include this content in your final response instead." Report text below is verbatim as returned. -->

# Analysis report: add-day-digests / analysis-001

## Review target

- Reviewer / role: B1-AN / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (version not exposed to the agent) / light review, docs-only, read-only analysis / claude-sonnet-5-5 (recorded override per workflow Model selection)
- Date: 2026-10-29 (UTC time of day not available)
- Change root / assignment: `openspec/changes/add-day-digests` / B1-AN
- Task and requirement references: task 0.1 in [tasks.md](../tasks.md); [design.md](../design.md) Decision 6 (reference wording)
- Scope and relevant neighboring paths: `.agents/roles/project-facts.md` (Safety bullet on `--live-agents`), `.agents/workflows/openspec-delegation.md` (new bullet in "Assignment and reservation"); neighbors `AGENTS.md`, `docs/agent-workflow.md`, `.agents/roles/*.md`, `.agents/templates/*.md`
- Reading limits and paths read outside scope: docs-only correctness and safety review. Outside the two targets I also read `tools/WorkLog.Smoke/Program.cs` and `tools/WorkLog.Smoke/LiveAgentChecks.cs` (to confirm what "isolation flags" refers to) and `tasks.md` task 5.2. I did not touch `src/WorkLog`, tests, or `docs/mcp-validation.md`.
- Baseline commit: 5a73ab9
- Implementation identity (before): head=5a73ab94bdc5a567eb6169e38f338711bd58f05a, baseline same, manifest `M .agents/roles/project-facts.md`, `M .agents/workflows/openspec-delegation.md`, tracked diff SHA-256 c9790646939cf5c1e33b406f01d65e9169eceee7e56d1a265e2886c9c679521e, no untracked files. Summary: `head=5a73ab9 baseline=5a73ab9 files=2 diff=c9790646939c untracked=0/e3b0c44298fc`
- Before/after identity: after the analysis the summary line is identical (`head=5a73ab9 baseline=5a73ab9 files=2 diff=c9790646939c untracked=0/e3b0c44298fc`). It matches the expected value, with no unexpected mutation.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Match to Decision 6 | Diff of both files against design.md lines 74-77 | `project-facts.md:50` is Decision 6's Safety text, reworded from "live agents" to "`tools/WorkLog.Smoke` with `--live-agents`". All elements are kept: user accounts and prompts to services, planned-evaluation precondition with example, assignment names clients/sessions/budget, disposable repositories and data directories, never real repositories/data directory/installation/client settings, no secrets or personal data in prompts, record versions/models/redacted prompts and outputs, no assignment means no live agents, default isolated smoke run always allowed. `openspec-delegation.md:32` matches the second Decision 6 bullet (in `tasks.md`; assignment repeats clients, session count, budget; parity with Claude Code and Codex unless a restriction is evidenced). | Matches |
| Contradiction with other policy | Grep for `live-agent`, `live agent`, `--live`, `forbid`, `never run` over `AGENTS.md`, `docs/agent-workflow.md`, `CLAUDE.md`, `.agents/**`, `.claude/**`, `.codex/**` | The only remaining mentions of `--live-agents` are the two edited lines. No other text says live agents are forbidden. `.agents/roles/qa.md` and `qa-report.md` use "live" for live-installation observation (a different concept, covered by the unchanged Safety bullet "A check that requires the live installation ... is recorded as not performed"). | No contradiction |
| Single home per rule | Compared the two edits with the `AGENTS.md` parity section and role files | The safety/permission rule lives only in `project-facts.md` (read by developer, QA, analyzer). The planning rule (tasks.md entry, repeated assignment fields) lives only in the orchestrator-only workflow, which does not restate the safety conditions. Parity is stated once in `AGENTS.md`; the workflow bullet applies it in one clause without duplicating it. | No duplication |
| Other safety rules weakened | Diff review of the full Safety section | The diff changes only the `--live-agents` line. The rules on real data directory, dashboard port 47831, stopping user processes, `init`/`update`/installer (dry-run only), personal client config, review comments, probe lifetimes, live-installation checks, and publishing are unchanged. The new text keeps "never use the user's real repositories, data directory, installation, or client settings". | Not weakened |
| Default smoke run | Read the Smoke `Program.cs` | The default run does not execute live agents unless `--live-agents` is passed (`Program.cs:87`). The rule keeps the default isolated run explicitly allowed. | Still allowed |
| Consistency with the tool | Read `LiveAgentChecks.cs` | The "smoke tool's isolation" is built into `LiveAgentChecks`, not a CLI option (see ANALYSIS-001). Codex runs with `--ignore-user-config --ephemeral -s read-only`; Claude with `--no-session-persistence --setting-sources local --strict-mcp-config --tools ""` and `--max-budget-usd 1`. | See ANALYSIS-001 |
| Task 0.1 verify clause | Checked each clause of tasks.md 0.1 | Wording matches Decision 6; default run stays allowed; no other safety rule weakened. | Satisfied |
| Performance / complexity / architecture | Not applicable | Docs-only target with no executable behavior. | Not applicable |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | None; two Markdown policy files | source observation | Not applicable: no timer, poll, or loop. |
| Cache lifetime versus interval and refresh duration | None | source observation | Not applicable: no cache. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | None | source observation | Not applicable: no accumulating state. |
| Repeated full reads where a change check would do | None | source observation | Not applicable. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | None | source observation | Not applicable: no tool resolution in the target. |

## Findings

### ANALYSIS-001: Per-session budget and "isolation flags" in the rule are not what the tool provides

- Severity: suggestion (does not block task 0.1)
- Related task / requirement: 0.1, 5.2; design Decision 6
- Locations: `.agents/roles/project-facts.md:50`; `tools/WorkLog.Smoke/LiveAgentChecks.cs:18-24`; `tools/WorkLog.Smoke/Program.cs:87`
- Observation: The rule says the assignment names the per-session budget and that the run uses "the smoke tool's isolation flags". In the tool, isolation is fixed in code (not selectable flags), and the only budget control is a hard-coded `--max-budget-usd 1` on the Claude invocation. The Codex invocation has no budget cap, and `--live-agents` is a single switch that runs the full default smoke first and then both clients. So the assignment's budget cannot change what the tool does, and the 1 USD cap does not apply to Codex. The wording is the same as Decision 6, so the edit is faithful; the gap is between the policy and the tool. Task 5.2 already plans USD 1 per session, so the practical risk is small.
- Proposed improvement and benefit: Either leave as is (the planned numbers equal the tool's behavior), or when 5.2 is implemented have the developer state how the Codex session is bounded (for example a turn limit, or a note that Codex has no budget flag), and optionally tighten the rule wording to "the isolation built into `LiveAgentChecks`". The benefit is that the assignment budget is a real control rather than a statement.
- Behavior-preservation risks: None for the docs edit. If the wording is changed, keep the "never real repositories/data directory/installation/client settings" clause intact.
- Evidence type: source observation
- Verification / measurement plan: When task 5.2 is reviewed, check the Codex invocation for a bound and that the evidence in `docs/mcp-validation.md` states the actual cost or limit per client. No measurement is needed for the current docs-only target.
- Relationship: none

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| None (first report) | n/a | n/a | n/a |

## Assessment and limitations

The two edits match Decision 6, satisfy all three verification clauses of task 0.1, do not contradict `AGENTS.md`, `docs/agent-workflow.md`, or any role/template file, and keep each rule in one place. No other Safety rule is weakened and the default isolated smoke run remains allowed. One non-blocking suggestion (ANALYSIS-001) concerns how closely the rule's budget and isolation wording maps to the smoke tool; it is relevant to review of task 5.2 rather than to this edit. Target identity was unchanged before and after. No unexpected writes occurred; my one write attempt (this report) was refused by the client. No measurements were applicable. The orchestrator owns triage and ledger status.
