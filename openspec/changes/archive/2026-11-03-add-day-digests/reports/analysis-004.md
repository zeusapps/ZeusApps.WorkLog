<!-- Saved by the orchestrator. Reviewer: G2 code-analyzer subagent; client Claude Code 2.1.312, background subagent mode; model claude-opus-5-5 (role default). Case: the client refused the reviewer's write. Exact refusal message: "Subagents should return findings as text, not write report files. Include this content in your final response instead." Report text below is verbatim as returned (the separate return block is recorded in findings.md, not here). -->

# Analysis report: add-day-digests / analysis-004

## Review target

- Reviewer / role: G2 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 (version from the findings ledger; not exposed to the agent directly) / background subagent, read-only analysis / claude-opus-5-5
- Date: 2026-10-29T16:23Z
- Change root / assignment: `openspec/changes/add-day-digests` / G2
- Task and requirement references: tasks 5.2 (code part only: the digest scenario in `LiveAgentChecks` and the `--live-digest-only` mode) and 5.3 in [tasks.md](../tasks.md); [design.md](../design.md) Decisions 6 and 7; ledger row F1 and history in [findings.md](findings.md); rechecks of ANALYSIS-001 ([analysis-001](analysis-001.md)) and ANALYSIS-020 ([analysis-002](analysis-002.md))
- Scope and relevant neighboring paths: `tools/WorkLog.Smoke/LiveAgentChecks.cs`, `tools/WorkLog.Smoke/Program.cs`, `README.md`, `docs/architecture.md`, `docs/releases.md`, `docs/installation.md`, `docs/mcp-validation.md`, `docs/dashboard-validation.md`. Neighbors read to check claims: `tools/WorkLog.Smoke/SmokeProcessRunner.cs`, `tools/WorkLog.Smoke/SmokeWorkspace.cs`, `tools/WorkLog.Smoke/ClientRegistrationChecks.cs`, `tools/WorkLog.Smoke/WorktreeAcceptanceChecks.cs` (lines 110-140), `src/WorkLog/Integration/Clients/ClaudeIntegration.cs`, `src/WorkLog/Integration/Clients/CodexIntegration.cs`, `src/WorkLog/Integration/AgentInstructions.cs` (diff), `src/WorkLog/Journaling/DigestService.cs` (constants), `src/WorkLog/Persistence/DigestStore.cs` (availability state), `src/WorkLog/Dashboard/SourceCatalog.cs` and `dashboard/src/app.ts` (`digestRevision` only)
- Reading limits and paths read outside scope: no live agents, as assigned. Outside the listed scope I read only the neighbors above. Local `codex --version`, `codex exec --help` and `claude --help` ran with `CODEX_HOME` and `CLAUDE_CONFIG_DIR` pointed at an empty scratch directory outside the repository. None of them starts a session or sends a prompt.
- Baseline commit: 5a73ab9
- Implementation identity (before): HEAD 5a73ab94bdc5a567eb6169e38f338711bd58f05a, baseline the same; manifest of 33 modified and 9 untracked files under `src/WorkLog tests tools/WorkLog.Smoke dashboard/src dashboard/e2e docs README.md`; tracked diff SHA-256 fddd761a0fd26837f3f282da4e8a8d25603661469fed7962dcd2a9c2789ace43. Summary: `head=5a73ab9 baseline=5a73ab9 files=42 diff=fddd761a0fd2 untracked=9/3fedf13b4948`
- Before/after identity: after the analysis the summary line is identical (`head=5a73ab9 baseline=5a73ab9 files=42 diff=fddd761a0fd2 untracked=9/3fedf13b4948`). It matches the assignment's expected value, with no unexpected mutation.
- Prior reports / rechecked findings: [analysis-001](analysis-001.md) (ANALYSIS-001), [analysis-002](analysis-002.md) (ANALYSIS-020)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| `--live-digest-only` cannot run more sessions | Read `Program.cs` and the fall-through path | `Program.cs:89-97` matches only the exact list `["--live-digest-only", exe]` and returns after `RunDigestAsync`. That method makes exactly two `RunDetailedAsync` calls (`LiveAgentChecks.cs:51-52`), with no retry and no loop. A malformed variant (missing executable, or with an extra `--live-agents`) falls through to the default path, where the executable becomes the flag text. `ClientRegistrationChecks.CheckSelfContainedVersionAsync` (`ClientRegistrationChecks.cs:16`) then fails to start it before any client session. The existing live scenario runs only with `--live-agents`, after the whole default smoke passes (`Program.cs:113-116`). `Initialize()` runs the clients only with `--version` (`AgentClientValidator.cs:18`). Codex retries inside one `exec` (recorded in mcp-validation) stay within one session. | Cannot exceed one session per client |
| Isolation preserved | Compared the argument arrays of both scenarios with the runner environment | Both scenarios go through `SmokeProcessRunner.RunDetailedAsync`, which always sets `CODEX_HOME`, `CLAUDE_CONFIG_DIR` and `WORKLOG_DATA_DIR` to the disposable workspace (`SmokeProcessRunner.cs:35-38`). The digest Codex arguments equal the existing ones plus `--json`. The Claude arguments equal the existing ones plus `--output-format stream-json --verbose` and `mcp__worklog__save_digest`. `--tools ""`, `--strict-mcp-config`, `--setting-sources local`, `--max-budget-usd 1`, `-s read-only`, `--ignore-user-config` and `--ephemeral` are all kept. This matches Decision 6 ("keeps every other flag"). | Preserved; the flags now exist in two copies (ANALYSIS-074) |
| Instructions reach both clients under those flags | Read both adapters | Claude Code's owned block is `CLAUDE.local.md` in the repository (`ClaudeIntegration.cs:36-43`), the local memory source that `--setting-sources local` keeps. Codex's owned block and MCP registration are both in the project `.codex/config.toml` (`CodexIntegration.cs:11-33`), loaded through the trust override. Registration and instructions come from the same file, so the existing scenario's working registration implies the instructions load too. | Both clients receive the digest paragraph; this is inferred from source and was not observed in a session |
| Parity of scenario shapes | Compared prompts, flags and `Summarize` per client | Both use the same prompt template with distinct tickets (DEP-777 and DEP-778) and the same run order: transcript, then the stored digests. The evidence differs by client: tool results are printed for Codex only, the model for Claude only (ANALYSIS-073). The stored-digest read is not filtered by client (ANALYSIS-071). | Gaps in the evidence, not in the client invocation |
| Duplication with the existing live scenario | Side-by-side read of `RunAsync` and `RunDigestAsync` | `LiveAgentChecks.cs:15-18` and `46-49` are identical (the `mcp.json` build). The Codex arguments (`21` vs `51`) and the Claude arguments (`26-27` vs `53-54`) differ only by additions. | ANALYSIS-074 |
| Codex session bound (ANALYSIS-001) | Read `SmokeProcessRunner.cs`; local `codex exec --help` (0.165.0); Context7 `/openai/codex` (`exec_events.rs`); local `claude --help` | `codex exec` 0.165.0 lists `-m/--model` and `--json` but no budget, turn or timeout option. The runner's `CancellationTokenSource(TimeSpan.FromMinutes(3))` with `Kill(entireProcessTree: true)` (`SmokeProcessRunner.cs:50-59`) covers each session. Claude Code lists `--max-budget-usd`. | See Re-verification and ANALYSIS-072 |
| Docs coherence and one home per fact | Read the diffs of README, architecture, releases, installation and both validation docs; checked them against the code | The architecture paragraph matches the code, including `digestRevision` (`SourceCatalog.cs:21`, `app.ts:2041`), with two exceptions (ANALYSIS-075). README and architecture both state the matching rule and the timezone caveat because task 5.3 requires both; they agree. `releases.md` repeats the probe explanation from `installation.md` and links to it; the two agree, so nothing is reported. The dashboard-validation "Final validation" points to the mcp-validation table rather than restating it. Anchors `#day-digests-add-day-digests-server-side`, `#full-validation-set-task-53` and `#installing-the-release-with-day-digests-from-v030-and-earlier` match their headings. | ANALYSIS-075; no other drift |
| Architecture drift | Judged against `docs/architecture.md` | The new paragraph follows the neighboring dense-prose style and adds no new component boundary. "Authenticated live-agent prompts remain opt-in" (line 53) is still accurate. | No drift |
| Content hygiene | Grep of the docs and smoke diffs for account and home paths, email addresses and the secret patterns | The only hit is the word "bearer" inside the quoted Codex 401 message (`mcp-validation.md`, Codex row). It contains no credential. | Clean (one false positive) |
| Formatting | `dotnet format --verify-no-changes` | Not run, to avoid contending with QA G1's concurrent build | not performed |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Live digest scenario (one-shot); docs | Source observation | Not applicable: the target adds no timer, poll or server loop. The dashboard's `digestRevision` poll came from D1 and was reviewed in [analysis-003](analysis-003.md). |
| Cache lifetime versus interval and refresh duration | None in the target | Source observation | Not applicable: no cache. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `artifacts/smoke/<guid>/` workspaces and per-process logs | Source observation | Each run leaves one ignored workspace with two client logs. The output is bounded per run and accumulates across runs, as for every smoke mode. Nothing new. |
| Repeated full reads where a change check would do | `ObserveAsync` reads all of `digests.db` | Source observation | The read is a few rows in a disposable store, so cost does not matter. The full read is still a correctness problem for attribution (ANALYSIS-071). |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | `codex` / `claude` launched by `SmokeProcessRunner` | Source observation (pre-existing) | The runner searches PATH for `codex.cmd` on Windows (`SmokeProcessRunner.cs:22`), which only an npm-style shim provides, and for `claude` as named. This lookup is shared with all smoke checks and is unchanged by this target. Waiting for process exit is bounded at three minutes. Draining stdout after exit has no timeout (`SmokeProcessRunner.cs:62-63`); a descendant that kept the inherited stdout open would hang the runner (hypothesis; `--no-daemon` likely avoids it for Codex). |

## Findings

### ANALYSIS-071: Each client's stored-digest report prints every digest in the store, so Claude Code's section repeats Codex's digest as its own

- Severity: warning
- Related task / requirement: 5.2; Decision 6 (per-client observations)
- Locations: `tools/WorkLog.Smoke/LiveAgentChecks.cs:51-52` (Codex runs first, then Claude Code, against the same `workspace.Data`); `:92` (`SELECT item, day, covers, text FROM digests` with no filter); `:96` (prints `{client}: stored digest ...` but not the selected `item`)
- Observation: The two sessions share one data directory and one `digests.db`. After the Claude Code session, `ObserveAsync("claude", ...)` lists every row, including the digest Codex saved for DEP-777, under the label `claude: stored digest`. The `item` column is read but not printed, and `covers` holds only opaque entry IDs, so the output does not show which client owns a row. Suppose Codex saves a digest and Claude Code does not. The recorded evidence then reads as "the save succeeded" for both clients. Task 5.2 records per-client outcomes, and a rerun spends the user's quota, so a misread result is costly.
- Proposed improvement and benefit: Print only the rows that belong to the client's session. Either snapshot the `(item, saved)` pairs before each session and print new or changed rows, or select the rows whose covered IDs are the client's own entries (journal entries with `Agent == client`). Also print `item`. Each client's section then states that client's result.
- Behavior-preservation risks: The scenario is observation-only, so no gate changes. Keep the read-only, non-pooled open so the read does not hold the store.
- Evidence type: source observation
- Verification / measurement plan: Without live agents, call the private observation step with a fixture `digests.db` that holds one row per ticket (or extract it into a testable helper), and check that each client's section lists only its own row.
- Relationship: new

### ANALYSIS-072: A session stopped by its bound leaves the least evidence: a timeout loses the transcript and the log, and any non-zero exit skips the stored-digest check

- Severity: suggestion
- Related task / requirement: 5.2; ANALYSIS-001 (session bounds)
- Locations: `tools/WorkLog.Smoke/SmokeProcessRunner.cs:56-60` (on timeout it kills the tree and throws before reading stdout or writing the log); `tools/WorkLog.Smoke/LiveAgentChecks.cs:62-80` (catches the exception, prints only `NOT COMPLETED. TimeoutException: ...`, and returns before the `digests.db` read on any non-zero exit)
- Observation: For Codex the only bound is the three-minute kill. When it fires, the run keeps no transcript, no `turn.completed` usage, no log file and no stored-digest check, although the session may already have logged, saved a digest and spent tokens. A Claude Code session that stops at `--max-budget-usd` or fails after a save exits non-zero (expected; not verified for 2.1.312). The transcript is then printed but the digest the session may already have saved is never reported. These are the cases in which the user's quota was spent.
- Proposed improvement and benefit: In `ObserveAsync`, read `digests.db` whatever the exit status, and print `result.LogPath`. For the timeout case, have the runner drain stdout and stderr after the kill (with a short bound) and write the log before it throws, or return a timed-out result. `RunAsync` keeps throwing on timeout. A bounded session then still produces usable evidence.
- Behavior-preservation risks: `SmokeProcessRunner` is shared by every smoke check. Keep `RunAsync`'s throw-on-timeout contract, and bound the drain so a killed tree cannot hang the runner.
- Evidence type: source observation
- Verification / measurement plan: Run the runner against a local stand-in that writes output and sleeps past a temporarily shortened timeout. Check that a log file exists and that `ObserveAsync` reports stored digests after a non-zero exit. No live agent is needed.
- Relationship: related to ANALYSIS-001

### ANALYSIS-073: The scenario captures different evidence per client: tool results only for Codex, the model only for Claude Code

- Severity: suggestion
- Related task / requirement: 5.2 (record "the client versions and models" and, per client, whether the IDs matched and the save succeeded); AGENTS.md parity
- Locations: `tools/WorkLog.Smoke/LiveAgentChecks.cs:112-142` (`Summarize`)
- Observation: For Codex, `item.completed` items other than reasoning are printed raw (`:136-137`). This includes `mcp_tool_call` items with their `result` and `error`, so the `digestRequest` IDs and any `save_digest` rejection are visible. For Claude Code, only `assistant` text and `tool_use` blocks are printed. Tool results arrive as `user` events with `tool_result` blocks and are dropped. The requested IDs and a rejection reason (stale, mixed items) are therefore invisible in Claude Code's summary. The model is the reverse case. Claude Code's `system` init event yields it (`:123-124`). The Codex `--json` stream carries none: Context7 `/openai/codex` `exec_events.rs` shows `thread.started` with a thread ID only, plus turn and item events. So the "models" field that 5.2 requires has no source for Codex. The F1 record says "no model is reported" for Codex because no turn ran; even after a successful run the scenario would not report one. Neither scenario captures client versions; F1 recorded them by hand.
- Proposed improvement and benefit: Print Claude Code's `tool_result` content (truncated like the rest) so both summaries show the request and the save outcome. For the model, either pass explicit models (`-m` for Codex, which 0.165.0 supports, and `--model` for Claude Code) as named in the assignment, or state in the evidence how the Codex default was determined. Print `codex --version` and `claude --version` at the start of the scenario; neither starts a session. Both clients' records then answer the same questions from the same kind of source.
- Behavior-preservation risks: Pinning models changes what is evaluated (a chosen model rather than the client default), so that choice belongs to the assignment. Printing tool results may show synthetic summaries only; the prompts are synthetic.
- Evidence type: source observation; Codex event schema from current documentation (not verified against 0.165.0 output)
- Verification / measurement plan: Feed `Summarize` a recorded or hand-written Claude `stream-json` sample with a `tool_result` event, and a Codex JSONL sample with an `mcp_tool_call` item. Check that both outputs contain the request IDs and the save outcome.
- Relationship: new; it affects how ANALYSIS-001's per-client cost record is read (usage is printed for both: `turn.completed` for Codex, `total_cost_usd` for Claude Code)

### ANALYSIS-074: The digest scenario repeats the existing scenario's MCP configuration and isolation flags instead of sharing them

- Severity: suggestion
- Related task / requirement: 5.2; Decision 6 ("keeps every other flag"); project facts Safety (isolation flags)
- Locations: `tools/WorkLog.Smoke/LiveAgentChecks.cs:15-18` and `:46-49` (identical `mcp.json` build); `:21` and `:51` (Codex arguments); `:26-27` and `:53-54` (Claude Code arguments)
- Observation: The isolation flags are the safety-relevant part of both scenarios, and they now exist in two hand-maintained copies. The digest copy differs only by additions (`--json`; `--output-format stream-json --verbose`; one allowed tool). A later fix to one scenario's isolation, such as a renamed client flag or an extra restriction, can miss the other. The second scenario would then send prompts with weaker isolation, and nothing would fail.
- Proposed improvement and benefit: Add private helpers: `WriteMcpConfig()`, `CodexArguments(string prompt, bool json)` and `ClaudeArguments(string prompt, bool streamJson, params string[] extraTools)`. Both scenarios then use one definition of isolation. The change is small and local.
- Behavior-preservation risks: Argument order and content must stay exactly the same for both scenarios. The runner logs the argument JSON per process, which allows a direct comparison.
- Evidence type: source observation
- Verification / measurement plan: Default isolated smoke (no live agents) for regression. Compare the argument arrays either with a unit-style check of the helpers or by reading the code.
- Relationship: new

### ANALYSIS-075: Three doc statements disagree with the code or with each other

- Severity: suggestion
- Related task / requirement: 5.3; ANALYSIS-001 (budget wording)
- Locations: `README.md:88`; `docs/architecture.md:51`; `docs/mcp-validation.md:226` versus `:228`
- Observation:
  1. `README.md:88` says that when an item "has two or more observations on a local day, the agent that logs another one is asked". This reads as a request on the third observation. The code asks on the second (`DigestService.cs:14,31`, `MinimumEntries = 2`), as stated in the README's own MCP section ("2 to 30 observations"), the architecture paragraph and the `DigestTests` row. The 30 cap is also missing from this sentence.
  2. `docs/architecture.md:51` says "Any store failure turns digests off for that call". A newer store version disables digests for the rest of the process (`DigestStore.cs:41,64`), as `mcp-validation.md` states ("newer store version unavailable for the process").
  3. `docs/mcp-validation.md:226` describes the scope as "a budget of USD 1 per session" for both clients. The next paragraph (`:228`) correctly says that Codex has no budget control and is bounded only by time. The first sentence overstates the planned figure as applying to Codex.
- Proposed improvement and benefit:
  1. README: say that each newly logged observation that is the item's second to thirtieth that local day brings a request.
  2. Architecture: "...for that call (a newer store version for the rest of the process)".
  3. mcp-validation: "the planned USD 1 per session, enforced for Claude Code; Codex bounded as below".
  Each fact then has one correct statement.
- Behavior-preservation risks: none (documentation).
- Evidence type: source observation
- Verification / measurement plan: Review of the corrected sentences against `DigestService` and `DigestStore`.
- Relationship: item 3 relates to ANALYSIS-001

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | F1 identity `head=5a73ab9 ... files=42 diff=fddd761a0fd2 untracked=9/3fedf13b4948` | The reconsideration asked F1 to state how each client's session is bounded. The code comment (`LiveAgentChecks.cs:41-42`) and `docs/mcp-validation.md:228` both state it. I checked the facts independently: local `codex exec --help` 0.165.0 has no budget, turn or timeout option, and `-m` exists; the runner kills the process tree after three minutes (`SmokeProcessRunner.cs:50-59`); `claude --help` lists `--max-budget-usd`. The three-minute kill is a real wall-clock bound on the local process tree, applied per session and to both clients. It is not a spend bound: no USD figure follows from it. The phrase "single-prompt `exec` mode" limits user turns, not the model calls inside the turn. The evidence prints usage for both clients, which is the right cost record. The residual tool gap (no cost control for Codex) is real and verified, not missing work. Remaining issues: the opening sentence still implies USD 1 for both (ANALYSIS-075 item 3), and the bound, when it fires, discards the session's evidence (ANALYSIS-072). | Addressed in substance: the bound is real and its mechanism is stated accurately. Residual wording and evidence issues are in ANALYSIS-072 and ANALYSIS-075; status is the orchestrator's decision |
| ANALYSIS-020 | same identity | `docs/mcp-validation.md` no longer contains "open compatibility finding" or "not as a verified workaround" (grep). The paragraph after the digest table now cites Decision 7, the relaxed probe, `HealthProbeToolListingTests` (5 of 5) and the releases and installation docs. The test count is 405, matching F1's final run and the full-validation table. QA-013 (same scope) is covered by the same rewrite. | verified |

## Assessment and limitations

Scope covered: the code part of task 5.2 (`RunDigestAsync`, `ObserveAsync`, `Summarize`, the prompt, `--live-digest-only`) and the task 5.3 docs, at the F1 identity.

- **Implementation.** `--live-digest-only` cannot run more than one session per client, including through malformed arguments, and isolation is unchanged from the existing live scenario. Under those flags both clients receive the managed digest paragraph (inferred from where the adapters write it). The docs match the implementation except for the three statements in ANALYSIS-075. The docs keep one statement per fact, apart from task-mandated pairs that agree.
- **Main concern: ANALYSIS-071 (warning).** The per-client digest report is not filtered by client, so a future paid run could credit Codex's digest to Claude Code. ANALYSIS-072 and ANALYSIS-073 concern the same evidence path: what survives a bounded session, and parity of what each client's record shows. ANALYSIS-074 removes the second copy of the isolation flags.
- **Decision.** All five findings concern the scenario that a rerun of 5.2 will use. Fixing ANALYSIS-071 (and preferably 072 and 073) before the authenticated rerun would make that run's evidence complete and attributable. The orchestrator owns triage.
- **Not performed.**
  - Live agents: not run, as assigned.
  - `dotnet format`, build and tests: not run, to avoid contending with QA G1.
  - Codex event schema: taken from current documentation, not from 0.165.0 output.
  - Claude Code's exit code at the budget cap: not verified.
- **Local commands and writes.** The local CLI help commands used an empty scratch client home outside the repository, and none starts a session. No dashboard or MCP server was started. The user's data directory, port 47831, installation and client settings were not touched. The only attempted repository write was this report, and the client refused it.
