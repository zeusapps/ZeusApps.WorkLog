<!-- Saved by the orchestrator. Reviewer: A3 code-analyzer subagent; client Claude Code 2.1.312; mode: subagent under the orchestrator session, auto mode; model claude-opus-5-5 (role default). Case: the client refused the reviewer's write with the exact message "Subagents should return findings as text, not write report files. Include this content in your final response instead." The text below is the reviewer's returned report, saved verbatim. -->

# Analysis report: improve-observation-summaries / analysis-001

## Review target

- Reviewer / role: A3 code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 / subagent under the orchestrator session, auto mode / claude-opus-5-5 (role default)
- Date: 2026-10-28T14:43Z
- Change root / assignment: `openspec/changes/improve-observation-summaries` / A3 review analysis
- Task and requirement references: tasks 1.1, 1.2, 1.3, 2.1, 2.2 ([tasks.md](../tasks.md)); requirements "Session-driven OpenSpec logging instructions" (modified) and "Summary guidance in the logging tool" (added) in [specs/local-agent-integration/spec.md](../specs/local-agent-integration/spec.md)
- Scope and relevant neighboring paths: the seven target paths listed below; neighbors inspected read-only: `src/WorkLog/Integration/Clients/ClaudeIntegration.cs`, `src/WorkLog/Integration/Clients/CodexIntegration.cs` (both call the shared renderer), `src/WorkLog/Journaling/Entry.cs` (output-schema summary description), `src/WorkLog/Mcp/JournalTools.cs`, `src/WorkLog/Cli/InitCommand.cs` (`--clients` default), `docs/architecture.md`
- Reading limits and paths read outside scope: proposal, spec delta, tasks, `reports/findings.md` (read only), `docs/architecture.md`; design.md intentionally absent. The neighboring source files above were read to judge parity and docs accuracy. A repository-wide text search for the old and new summary sentence also listed matches under ignored `artifacts/` (smoke and QA copies); their content was not otherwise read.
- Baseline commit: `4e1c50b`
- Implementation identity: `./scripts/review-identity.ps1 -Baseline 4e1c50b -Paths README.md docs/installation.md docs/mcp-validation.md src/WorkLog/Integration/AgentInstructions.cs src/WorkLog/Journaling/LogRequest.cs tests/WorkLog.Tests/Integration/InstructionOptionsTests.cs tests/WorkLog.Tests/Mcp/McpTests.cs` before the analysis: HEAD `4e1c50b54b9b1607b01b3b7d73a1c07604bc3194`, manifest `M` for all seven paths, tracked diff SHA-256 `93757b78458f6ec2c54d24107987bd15427d415e99dfb731b342fe6cb5ddc8c9`, no untracked files. Summary: `head=4e1c50b baseline=4e1c50b files=7 diff=93757b78458f untracked=0/e3b0c44298fc`
- Before/after identity: after the analysis the same command printed `head=4e1c50b baseline=4e1c50b files=7 diff=93757b78458f untracked=0/e3b0c44298fc`; identical to the expected and before identity, no unexpected mutation.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Duplication (guidance text) | Inspection of `git diff 4e1c50b` and full files | The core guidance appears as two production literals: `src/WorkLog/Integration/AgentInstructions.cs:15` and `src/WorkLog/Journaling/LogRequest.cs:10`, plus a third copy as a test constant (`tests/WorkLog.Tests/Integration/InstructionOptionsTests.cs:15`). The literals differ only by their lead-in ("Include a summary with every observation:" against "Optional") and one separator (`;` against `:` before "for in_progress"). Nothing ties the two production literals together. | Wording is consistent today; drift is unguarded although the added requirement says "the same ... guidance". ANALYSIS-001 |
| Duplication (contradictory rules) | Comparison of the instruction ("Include a summary with every observation") with the schema ("Optional ...") and `Entry.Summary` output description ("Optional factual summary of the work performed.") | Protocol-optional versus instructed-always matches the proposal's intent (required by policy, not by validation). The output description is about a stored field, not authoring guidance. | No contradiction; no finding |
| Simplification / architecture | Inspection against `docs/architecture.md` ("render shared instructions"; "Managed instruction blocks and templates are shared between clients") | Both adapters still call `AgentInstructions.Render` (`ClaudeIntegration.cs:40`, `CodexIntegration.cs:66`); the schema description rides on the existing `[Description]` convention used by the other `LogRequest` and `Entry` properties. Codex quoting (`JsonSerializer.Serialize` into TOML) is unaffected because the new sentence adds no quotes or control characters. | No drift. Parity holds by construction for Claude Code and Codex; the MCP schema change is client-neutral |
| Complexity / test robustness | Inspection of the new test code | `InstructionOptionsTests.RenderedInstructionsShareSummaryGuidanceAndOtherwiseKeepPriorText`: the `Contains`, `DoesNotContain`, and agent-normalized `Equal` assertions are meaningful parity guards. The "restore the prior block" part hard-codes the retired sentence and `Assert.Equal(15, lines.Length)`. `McpTests` checks two description fragments and that `summary` is not in `required`, which is loose and meaningful coupling. | ANALYSIS-002 for the restore and line-count part; the McpTests additions are proportionate |
| Docs accuracy | Inspection of the README, installation, and mcp-validation diffs, plus the `--clients` default in `src/WorkLog/Cli/InitCommand.cs:13-18` | `docs/installation.md` now says a rerun of `worklog init` "refreshes the WorkLog-owned instructions for both clients". `--clients` defaults to `both` and is not inferred from the previous run. The README text matches the shipped wording, adding "readable in a stand-up report", which the rendered sentence does not contain (it is spec wording; QA owns that conformance question). | ANALYSIS-003 |
| Formatting | `dotnet format ZeusApps.WorkLog.slnx --no-restore --verify-no-changes --include` on the four changed .cs files (verify-only) | Reported issues are at `InstructionOptionsTests.cs` lines 106 and 178 and import ordering at line 1 in both test files. None of these lines were added or changed by the target. | Pre-existing; no finding |
| Performance | Inspection | The change adds about 290 characters to the `tools/list` input schema and about 140 net characters to the managed instruction block, with no runtime code paths. | Hypothesis, not measured: negligible per-session token cost. Not applicable otherwise |
| Functional tests | Not run | The developer recorded 340 passing tests and `SMOKE PASSED` in `docs/mcp-validation.md`. QA (A2) owns functional verification, and a concurrent Release build could contend with QA's run. | Not performed by this analysis |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | None in scope | source observation | Not applicable. The change edits a static string template, an attribute string, tests, and docs, and adds no timer, poll, or loop. |
| Cache lifetime versus interval and refresh duration | None in scope | source observation | Not applicable. No cache is added or touched. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Journal `summary` values | source observation | Unchanged. The summary length stays within existing limits, as the spec requires. The guidance only steers agents toward about 140 characters. |
| Repeated full reads where a change check would do | None in scope | source observation | Not applicable. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | None in scope | source observation | Not applicable. No new process launches or lookups. |

## Findings

### ANALYSIS-001: Shared summary guidance is two unlinked literals, so the "same guidance" requirement is not enforced

- Severity: suggestion
- Related task / requirement: 1.1, 1.2; "Summary guidance in the logging tool" ("SHALL carry a description stating the same concise, result-focused guidance as the managed instructions")
- Locations: `src/WorkLog/Integration/AgentInstructions.cs:15`, `src/WorkLog/Journaling/LogRequest.cs:10` at `4e1c50b` plus the working tree (diff `93757b78458f`)
- Observation: The core clause "one short sentence (about 140 characters) stating the concrete outcome, decision, or remaining step ... Do not narrate process, repeat the status, or include secrets or personal data." is typed twice. The copies already differ in one separator (`; for in_progress` against `: for in_progress`). The instruction test pins only the instruction copy (full sentence), and the MCP test pins only two fragments of the schema copy. A later edit to one copy (for example changing the length target) passes all tests while the spec's "same guidance" silently stops holding. The duplication itself is not a defect today. The finding is about the unenforced requirement.
- Proposed improvement and benefit: Pick one of two bounded options. (a) Minimal: add one assertion that the clause after the instruction's lead-in equals the schema description after "Optional ", with the separator normalized or unified. This needs no production change. (b) Declare the clause once as an `internal const string`. `[Description("Optional " + X)]` accepts a constant concatenation, and the raw interpolated template can embed `{X}`. Place it in a neutral spot such as `Journaling`, next to `LogRequest`, because `Integration` does not currently import `Journaling` and the reverse dependency would be worse. Either option makes the cross-client and cross-mode equality checkable. Option (a) is enough for a change this small.
- Behavior-preservation risks: Option (b) must leave the rendered block byte-identical apart from any deliberate separator unification. Otherwise the owned-block refresh text changes, which is harmless but adds churn. The attribute must stay a compile-time constant.
- Evidence type: source observation
- Verification / measurement plan: Run the existing `InstructionOptionsTests` and `McpTests`, plus the new alignment assertion. Confirm it fails when one copy alone is edited.
- Relationship: new

### ANALYSIS-002: The "restore prior block" assertions turn a one-time diff check into a permanent, brittle pin

- Severity: suggestion
- Related task / requirement: 1.1
- Locations: `tests/WorkLog.Tests/Integration/InstructionOptionsTests.cs:27-34` (working tree, diff `93757b78458f`)
- Observation: Task 1.1 asks to verify that the rest of the block is unchanged apart from the sentence. The test does this by substituting the retired sentence "Include a small factual summary when useful." back. It then asserts `Assert.Equal(15, lines.Length)` and the presence of three specific lines. Every future edit to the shared template, including adding or removing a line or rewording either of the two checked lines, breaks this test. The test name then describes a historical constraint ("OtherwiseKeepPriorText"). At the same time, the check is only a partial proof of "unchanged": the line count and three lines do not establish that the other twelve lines are intact. The baseline diff (`git diff 4e1c50b -- src/WorkLog/Integration/AgentInstructions.cs`, one line replaced by two) already proves the property for this review.
- Proposed improvement and benefit: Keep the `Contains`, `DoesNotContain("small factual summary")`, and agent-normalized `Assert.Equal` parity assertions, which are the durable guarantees. Drop lines 27-34, and record the "rest unchanged" evidence through the diff in the QA or validation notes. If a permanent pin of the whole block is wanted, use one full expected-text comparison, which proves the property instead of approximating it. Benefit: future instruction edits touch one obvious place instead of a hard-coded line count.
- Behavior-preservation risks: Test-only change; there is no product behavior. Parity coverage stays intact through the retained `Equal` assertion.
- Evidence type: source observation
- Verification / measurement plan: Run `dotnet test -c Release --no-build --no-restore --filter FullyQualifiedName~InstructionOptionsTests` after the edit. Then inspect the baseline diff of `AgentInstructions.cs` once to show that only line 15 changed.
- Relationship: new

### ANALYSIS-003: The installation note implies that a plain rerun refreshes "both clients", but `--clients` defaults to both rather than to the previous selection

- Severity: suggestion
- Related task / requirement: 2.1
- Locations: `docs/installation.md:56` (working tree); `src/WorkLog/Cli/InitCommand.cs:13-18` at `4e1c50b`
- Observation: The new sentence reads "Rerunning `worklog init` refreshes the WorkLog-owned instructions for both clients". `--clients` has `DefaultValueFactory = _ => "both"`. A user who initialized with `--clients codex` and follows this sentence literally plans a Claude Code registration and block too, or fails when the Claude CLI is not installed. The same paragraph already says, more precisely, that "Init can be rerun after an application upgrade to update managed instruction blocks", and an earlier paragraph advises rerunning "with the intended options". The new sentence restates the first and blurs the second.
- Proposed improvement and benefit: Change the sentence to say "for the selected clients (rerun with the same `--clients` and instruction options; both by default)", or fold the summary-guidance example and the `--dry-run` plus restart advice into the existing first sentence. This avoids an unintended second-client setup.
- Behavior-preservation risks: Docs only.
- Evidence type: source observation
- Verification / measurement plan: Re-read the paragraph against the `InitCommand` option defaults. Optionally run `worklog init --dry-run --clients codex --data-dir <disposable>` on a disposable repository to confirm that only Codex targets are planned.
- Relationship: new

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | n/a | n/a | n/a |

## Assessment and limitations

The change is small, correctly placed, and keeps Claude Code and Codex parity by construction: one shared renderer, called unchanged by both adapters, plus a client-neutral schema description. It introduces no architectural drift against `docs/architecture.md` and no runtime or steady-state cost beyond a few hundred characters of prompt and schema text. That cost is a hypothesis and was not measured. All three findings are suggestions, and none blocks the change.

- ANALYSIS-001: duplicating the text is acceptable for two short literals. The real gap is that nothing enforces the spec's "same guidance". A single alignment assertion is the proportionate fix, and a shared constant is optional.
- ANALYSIS-002: the restore and line-count assertions are brittle and prove less than their name claims. The parity assertions in the same test are good.
- ANALYSIS-003: one docs sentence could lead a single-client user to set up both clients.

Observations for the orchestrator or QA, not raised as findings: the spec says "suitable for reading in a stand-up report" and the README says "readable in a stand-up report", while the rendered sentence and the schema description do not mention stand-ups. Whether "short, concrete outcome" satisfies that clause is a QA spec-conformance call. The developer's recorded deviations (alternate package output directory; dry run on a fresh repository rather than a refresh) do not affect this analysis.

Not performed: functional test runs (owned by QA A2) and any check against the live installation. Writes: none. The Write of this report was refused by the client (exact message quoted at the top of this response), and the verify-only `dotnet format` run produced only normal ignored build outputs. The target identity was unchanged before and after.
