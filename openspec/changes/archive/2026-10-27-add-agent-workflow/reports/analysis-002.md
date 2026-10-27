<!--
Saved by the orchestrator (fallback per .agents/workflows/openspec-delegation.md, Reports and evidence).
- Reviewer: code-analyzer subagent, assignment A4 (light review)
- Client / version / mode: Claude Code 2.1.311, project subagent, auto mode
- Model: claude-sonnet-5-5 (orchestrator override of the opus default; docs/config-only target)
- Write outcome: no write attempted. The subagent reported that its client-provided subagent instructions told it not to write report files and to return findings as text, so no refusal message exists. Compare analysis-001.md, where the same client refused an attempted write with "Subagents should return findings as text, not write report files. Include this content in your final response instead."
- Saved verbatim from the subagent's final response on 2026-10-27 by orchestrator session eae5a20b-6b19-45bd-b040-24cfe844c709. Only this header was added.
-->

# Analysis report: add-agent-workflow / analysis-002

Intended path: `openspec/changes/add-agent-workflow/reports/analysis-002.md`. I did not attempt the write, so no file exists there and the orchestrator must save this text. My subagent instructions say not to write report files and to return findings as text, and that overrides the role file's "attempt once" step. There is no refusal message to quote. ANALYSIS-041 below covers this gap.

## Review target

- Reviewer / role: code-analyzer (assignment A4, light review) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.311 (as stated in the assignment, not observed by me) / subagent / `claude-sonnet-5-5`. The role default is opus. The orchestrator overrode it to sonnet because the target is docs and config only.
- Date: 2026-10-27 (UTC)
- Change root / assignment: `openspec/changes/add-agent-workflow` / A4
- Task and requirement references: tasks 1.1–1.4, 2.1–2.2, 3.1–3.3, 4.1, 5.4 in [tasks.md](../tasks.md). All requirements in [spec.md](../specs/repository-agent-workflow/spec.md), especially "Native adapters, shared entry point, and per-role models", "Archive requires resolved review comments", and "WorkLog live-instance safety".
- Scope and relevant neighboring paths:
  - Target paths: `AGENTS.md`, `.agents/roles/`, `.agents/templates/`, `.agents/workflows/`, `.claude/agents/`, `.codex/agents/`, `scripts/review-identity.ps1`, `docs/agent-workflow.md`, and the README.md link line.
  - Neighbors checked: `src/WorkLog/Integration/Clients/ClaudeIntegration.cs`, `src/WorkLog/Integration/RepositoryInitializer.cs`, `src/WorkLog/Dashboard/ReviewMarker.cs`.
- Reading limits and paths read outside scope:
  - I did not read `docs/handoffs/`.
  - Outside the target paths I read the change's proposal.md, design.md, spec.md and tasks.md.
  - I grepped `reports/findings.md` for reservations and the model override. I did not read analysis-001.md or qa-001.md.
  - I read the neighbor source files listed above.
  - I fetched Claude Code subagent frontmatter documentation via `ctx7` (library `/websites/code_claude`).
- Baseline commit: 582f8bc
- Implementation identity (before):
  - Command: `./scripts/review-identity.ps1 -Baseline HEAD -Paths AGENTS.md .agents/roles .agents/templates .agents/workflows .claude/agents .codex/agents scripts/review-identity.ps1 docs/agent-workflow.md README.md`
  - Manifest: 17 untracked files plus `M README.md`.
  - Tracked diff SHA-256: `9fb5e3d6075eacc1dc967881c98e26f9a06f709d2bf172bca7530021fa062563`
  - Summary: `head=582f8bc baseline=582f8bc files=18 diff=9fb5e3d6075e untracked=17/9f7af19ec099`
- Before/after identity: the after-run summary line is identical to the before line, and every untracked hash matched. No unexpected mutation. `.git/index` mtime was unchanged (2026-10-26T15:32:05Z) after my two extra script runs. `git status --short` showed the same 16 entries as at session start.
- Prior reports / rechecked findings: none. analysis-001 (calibration) is unrelated to this target and was not rechecked.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Rule duplication or contradiction across files | Read AGENTS.md, workflow, 3 role files, project-facts, 3 templates, docs page, spec and design; cross-compared ownership, fallback, fix-cycle, severity-gate, model and archive rules. | The report-write fallback matches across role files, workflow, spec and docs. The two-cycle cap, severity gate, finding states and the "specialists do not read the workflow" rule each have one owner (the workflow). | Two contradictions found: ANALYSIS-041 (fallback header) and ANALYSIS-042 (archive pattern). Nothing else contradictory. |
| Adapter duplicates role policy | Compared the 3 `.claude/agents/*.md` and 3 `.codex/agents/*.toml` files with the role files. | Each adapter is a description plus one "Follow `.agents/roles/<role>.md` and `project-facts.md`" line. Only reviewer adapters name a template. The description text restates "writes only the assigned change-local report", which matches the role file and spec. | No policy is duplicated. Claude and Codex adapters carry identical instruction text. The only per-client differences are model and effort keys. |
| Single-owner rule split | Traced model values, fallback, archive and parity-exception rules. | Model values live only in the adapters; the workflow says so. The docs page repeats the aliases `sonnet` and `opus` as a how-to, which is consistent with the adapters. The parity-exception evidence rule lives in AGENTS.md and is cross-referenced from workflow lines 72 and 107. | No defect. Both parity sections of AGENTS.md and project-facts agree. |
| Adapter frontmatter validity (Claude) | `ctx7` docs for `/websites/code_claude`. | `model` and `effort` (`low`..`max`) are documented subagent fields, so `effort: high` on the analyzer is valid. The `sonnet` override took effect on this run. | Valid. Task 3.1's session-start discovery behavior was not re-tested by me. |
| Adapter validity (Codex) | Source inspection. | Model IDs `gpt-6.1-sol` and `gpt-6-luna` are marked "Documented, not yet natively verified". docs/agent-workflow.md says so, and design.md lists it as an accepted risk. | Documented as unverified, as the spec requires. Not verified by me because no Codex is available. If Codex rejects those IDs, the role launch will fail. No fallback is documented. |
| Entry point and `CLAUDE.md` rule | `ls CLAUDE.md .claude/CLAUDE.md CLAUDE.local.md`; grep of `src` for `AGENTS.md`. | None of the three files exists. `ClaudeIntegration.FindInstructionImports` (lines 91–105) emits `@AGENTS.md` imports, which backs the docs claim about the `CLAUDE.local.md` owned block. | Passed by source observation. The generated block was not executed. |
| `worklog init` dry-run safety text | Read project-facts line 47; grep of `dryRun` in `RepositoryInitializer.cs`. | `plan.Describe(dryRun)` precedes the `if (!dryRun) plan.Apply(...)` guard, and the dry-run message reads "Dry run: no files written." | The text is consistent with the source. A dry run still reads the real `~/.claude.json`, so the output needs the redaction that the hygiene rules require. Not executed by me. |
| Archive marker gate vs the dashboard's marker grammar | Compared workflow line 86 with `ReviewMarker.Parse` (`ReviewMarker.cs`, line 46: `^ {0,3}<!-- REVIEW(?::\|(?=\s))`) and the fence handling. | The workflow says "lines starting with `<!-- REVIEW`", which does not match markers indented 1–3 spaces. A grep with `^\s*<!-- REVIEW` over this change root returned no hits. | ANALYSIS-042. |
| `scripts/review-identity.ps1` read-only claim and cost | Read the whole script and measured one run. `.git/index` mtime before and after. | The script runs only `git rev-parse`, `ls-files`, `ls-tree`, `diff` and `diff --name-status`. It writes only a temp error file in the system temp directory, which it deletes in `finally`. There are no writes to the checkout or index, and no `git add`, `status` or `checkout`. Hashing uses `Get-FileHash` and `SHA256` over a streamed `git diff`. Stdout is read synchronously and stderr asynchronously, so there is no pipe deadlock. | Read-only for content, tracked state and the working tree. ANALYSIS-043 notes the one unproven edge (index stat refresh). Cost is covered in the table below. |
| Hygiene of target files | Grep of scoped files for account, path, email and secret patterns. | Only the pattern definitions in project-facts lines 61–62 matched. No real values. No line-start review markers in the change root. | Passed. The full hygiene check belongs to QA. |
| README link line | `git diff -- README.md`. | One sentence added to the existing architecture line, linking `docs/agent-workflow.md`. | Correct. The target exists. |
| Relative links in scoped docs | Visual check plus `ls`. | `docs/architecture.md`, `src/WorkLog/Integration/AgentInstructions.cs` and `src/WorkLog/Integration/Clients/` exist. The anchors `#known-client-restrictions`, `#reports-and-evidence`, `#committable-content-hygiene`, `#mixed-client-operation` and `#finding-triage-and-fix-routing` each match a heading. | Passed. |
| Performance / complexity | Not applicable beyond the script. | The target is static documents. | Not applicable. |

## Steady-state cost and bounds

The target has no timer, poll loop, cache or long-running component. Only `scripts/review-identity.ps1` executes, once per invocation.

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Not applicable: static documents, no timer, poll or loop. The script is invoked on demand. | n/a | Not applicable. Per invocation the script spawns about 6 `git` processes: `rev-parse --show-toplevel`, `rev-parse HEAD`, `rev-parse --verify`, `diff --name-status`, `ls-files --others` and the streamed `diff --binary`. The per-path `ls-files` and `ls-tree` probes are skipped when `Test-Path` succeeds. Per segment it also does one `Get-ChildItem` listing, and it hashes every untracked file in scope. Measured once: 0.35 s for the 9-path target on a warm machine. Single measurement, so a sample, not a benchmark. |
| Cache lifetime versus interval and refresh duration | Not applicable: no caches. | n/a | The script stores nothing. |
| Unbounded state | Not applicable: no collections or persisted state. | n/a | Output and untracked-file hashing grow with scope size only. The default scope `.` hashes all untracked files, bounded by the working tree. The script creates one temp file per git call and deletes it in `finally`. |
| Repeated full reads | Not applicable: no repeated requests. | n/a | A default-scope run reads the entire tracked diff and all untracked files. The assignments pass narrow paths. |
| External tool discovery | `git` resolved from PATH; no timeout. | source observation | `git` is found via PATH only, which is fine for this repository. None of the `git` calls has a timeout. A stuck `git` would hang the script, but there is no prior evidence of that. Not a finding. |

## Findings

### ANALYSIS-041: Fallback header requires an "exact refusal message" that a subagent may never receive

- Severity: suggestion
- Related task / requirement: 1.2, 4.1; spec "Durable change-local reports and findings ledger" (the refusal-header sentence)
- Locations:
  - `.agents/workflows/openspec-delegation.md` line 52 (header must name "the exact refusal message"; says to treat the restriction as "a possibility to verify... not an established WorkLog restriction").
  - `docs/agent-workflow.md` line 65 (records the refusal as observed on 2.1.311, 2026-10-27).
  - `.agents/roles/qa.md` line 16 and `.agents/roles/code-analyzer.md` line 12 ("attempt your own write ... once").
- Observation:
  - The recorded message reads "Subagents should return findings as text, not write report files...". My own subagent instructions say the same thing up front, so on this client the restriction can act as an instruction instead of a tool refusal. A reviewer that follows it makes no write attempt, so no refusal message exists. The workflow's header requirement then cannot be met as written.
  - The workflow still calls the restriction unestablished, while the docs page records it as observed. An orchestrator reading only the workflow may not plan the fallback.
  - Impact: the fallback is a safe, defined path. The risk is a thin or missing audit record for the saved report, plus a wasted attempt.
- Proposed improvement and benefit: in the workflow, let the header record either the exact refusal message or a statement that the client instructs subagents to return text and no write was attempted. Update line 52 to point to the docs entry as observed on 2.1.311. The role-file "attempt once" can stay unchanged.
- Behavior-preservation risks: none. The fallback text is only relaxed. Role ownership and the single-attempt rule are unchanged.
- Evidence type: source observation, plus this run's behavior (no write attempted because the subagent instructions forbid it).
- Verification / measurement plan: the next orchestrator-saved fallback report uses the revised header wording.
- Relationship: none.

### ANALYSIS-042: Archive precondition search pattern is narrower than the dashboard's marker grammar

- Severity: suggestion
- Related task / requirement: 1.2; spec "Archive requires resolved review comments"; design Decision 5
- Locations:
  - `.agents/workflows/openspec-delegation.md` line 86 ("lines starting with `<!-- REVIEW`").
  - `.agents/roles/project-facts.md` line 63 (the residue patterns say the same).
  - `src/WorkLog/Dashboard/ReviewMarker.cs` line 46 (`^ {0,3}<!-- REVIEW(?::|(?=\s))`, fence-aware).
- Observation: the dashboard treats a marker with up to 3 leading spaces as a real marker, but the workflow's wording requires `<!-- REVIEW` at column 0. A literal reading misses an indented marker. Markers the dashboard writes start at column 0, so the practical risk is limited to hand-edited or re-indented markers. This weakens the gate the change adds, in a rare case.
- Proposed improvement and benefit: use the dashboard's pattern, `^ {0,3}<!-- REVIEW` outside fenced code, in both places. The gate then agrees with what the dashboard shows as a marker.
- Behavior-preservation risks: none for generated markers. The new pattern may also match intentionally indented examples outside code fences, which the user decides at the archive prompt anyway.
- Evidence type: source observation, comparing the two patterns. Not executed.
- Verification / measurement plan: run the revised pattern with `rg` over a fixture containing a 2-space-indented marker and a fenced example.
- Relationship: none.

### ANALYSIS-043: The "only reads Git state" claim leaves one unproven edge, the index stat refresh

- Severity: suggestion
- Related task / requirement: 2.2; `.agents/roles/project-facts.md` line 21 ("It only reads Git state"); the script's own synopsis
- Locations: `scripts/review-identity.ps1` lines 45, 140, 154 (the `git diff` calls) and the `$gitOptions` array at line 45, which does not set `GIT_OPTIONAL_LOCKS`.
- Observation: the script is read-only for file content, branches, refs and the working tree. I believe `git diff` against the working tree may opportunistically refresh the index (stat cache) and briefly take `index.lock`. Setting `GIT_OPTIONAL_LOCKS=0` prevents this. I did not confirm that in Git source or documentation. In two runs here the index mtime did not change. A concurrent run, such as the QA reviewer's, can only skip the refresh silently. Any stat refresh would not alter content, so the risk is a lock racing with the orchestrator's own `git add` or `git commit`.
- Proposed improvement and benefit: set `$env:GIT_OPTIONAL_LOCKS = '0'` at the top of the script, or soften the wording to "does not modify files, refs or index content". This makes the claim strictly true and removes any lock contention with the orchestrator.
- Behavior-preservation risks: none. The output does not depend on the index stat refresh.
- Evidence type: performance hypothesis for the lock behavior; the index-mtime observation is measured but not conclusive.
- Verification / measurement plan: after setting the variable, run the script while holding a held index lock, such as `git update-index --really-refresh` under a lock. Do this only in a disposable clone, not in this checkout.
- Relationship: none.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | n/a | This round covers only new findings. | not performed (no assigned IDs) |

## Assessment and limitations

- Coverage: the light correctness and safety review of the delegated-workflow files was completed over the full scope. Three suggestions were found. No critical or warning findings. No cross-file contradictions of the single-owner kind, and no adapter duplicates role policy. Safety rules in project-facts are consistent with the spec's live-instance requirement and with the `init` dry-run source. `effort: high` is a valid documented field. The `sonnet` override took effect on this run.
- Not done:
  - The Codex adapters and Codex model IDs were not verified (no Codex available), as documented.
  - I did not re-run the project-facts commands (task 1.4) or the `init --dry-run` check (task 3.3). Those belong to QA.
  - The script's `git` index-refresh behavior is unconfirmed (ANALYSIS-043).
  - The Claude Code session-start discovery of the agents (task 3.1) was not re-tested.
- Unexpected writes: none. I made no writes to the checkout. The only side effects were the `ctx7`/`npx` cache outside the repository and the script's temporary files, which it removed itself.
- Decisions for the orchestrator:
  - Triage ANALYSIS-041 to ANALYSIS-043. Suggestions are recorded as deferred under the severity gate unless you choose to fix them.
  - Save this text as `analysis-002.md` under a header saying the write was not attempted because the subagent instructions forbid writing report files.
  - The ANALYSIS-041 point would apply to this very header.

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.311 / claude-sonnet-5-5
task / finding IDs: 5.5 (groups 1-4 plus 5.4); ANALYSIS-041, ANALYSIS-042, ANALYSIS-043 (all suggestion)
changed or report paths: none written; report text inline above, intended path D:\Code\ZeusApps.WorkLog\openspec\changes\add-agent-workflow\reports\analysis-002.md
checks: ./scripts/review-identity.ps1 (before and after) -> passed; git status and .git/index mtime comparison -> passed (no mutation); ctx7 Claude Code frontmatter docs -> passed (effort field valid); init --dry-run and project-facts commands -> not run (QA scope); Codex adapter model IDs -> not run (no Codex available)
target identity: before head=582f8bc baseline=582f8bc files=18 diff=9fb5e3d6075e untracked=17/9f7af19ec099; after identical
limitations / decisions needed: report write NOT attempted because the subagent instructions say not to write report files (no refusal message exists); orchestrator must save the inline text. Orchestrator to triage ANALYSIS-041 to 043. Codex model IDs unverified. Index-refresh claim unconfirmed.
next: orchestrator saves the report as analysis-002.md and records the fallback in the ledger; QA-002 and analysis-002 triage; task 5.5 can close if no gated findings.
```
