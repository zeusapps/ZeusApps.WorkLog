<!-- Saved by the orchestrator under the reviewer-text fallback. Reviewer: code-analyzer (AN-R1). Client: Claude Code 2.1.317 subagent, auto mode. Model: claude-opus-5-5. Case: the client refused the reviewer's write; exact refusal message: "Subagents should return findings as text". Text below is verbatim. -->

# Analysis report: standup-oriented-summaries / round 1

## Review target

- Reviewer / role: AN-R1 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (subagent; the subagent cannot see the exact CLI version; the orchestrator ledger records 2.1.317) / auto mode / claude-opus-5-5
- Date: 2026-11-03T16:59:55Z
- Change root / assignment: `openspec/changes/standup-oriented-summaries` / AN-R1
- Task and requirement references:
  - Tasks 1.1, 2.1-2.4, 3.1-3.3, 4.1 and 5.1a ([tasks.md](../tasks.md)).
  - [design.md](../design.md), Decisions section.
  - Delta spec [local-agent-integration](../specs/local-agent-integration/spec.md): "Session-driven OpenSpec logging instructions" and "Summary guidance in the logging tool".
  - Delta spec [day-digests](../specs/day-digests/spec.md): "Digest writing guidance".
- Scope and relevant neighboring paths:
  - In scope: `src/WorkLog/Journaling/LoggingGuidance.cs`, `src/WorkLog/Integration/AgentInstructions.cs`, `src/WorkLog/Journaling/LogRequest.cs`, `src/WorkLog/Journaling/DigestService.cs`, `src/WorkLog/Mcp/JournalTools.cs`, `tests/WorkLog.Tests/Integration/InstructionOptionsTests.cs`, `tests/WorkLog.Tests/Mcp/DigestTests.cs`, `tests/WorkLog.Tests/Mcp/McpTests.cs`, `tools/WorkLog.Smoke/Program.cs`, `tools/WorkLog.Smoke/LiveWordingChecks.cs`, `README.md`.
  - Neighbors read: `tools/WorkLog.Smoke/LiveAgentChecks.cs`, `SmokeWorkspace.cs`, `SmokeProcessRunner.cs`, `DigestAcceptanceChecks.cs`, `JournalAcceptanceChecks.cs` (Period only), `src/WorkLog/Persistence/DigestStore.cs`, `src/WorkLog/Journaling/DigestRequest.cs`, `src/WorkLog/Mcp/McpServerHost.cs` (repository resolution line), `src/WorkLog/WorkLog.csproj` (InternalsVisibleTo), and `docs/architecture.md` (module table).
- Reading limits and paths read outside scope: the assignment set no limits beyond its scope. I also read the orchestrator ledger [findings.md](findings.md) to relate findings to QA-001; I did not modify it.
- Baseline commit: 6d23796
- Implementation identity (before), from `./scripts/review-identity.ps1 -Baseline 6d23796 -Paths src/WorkLog tests/WorkLog.Tests tools/WorkLog.Smoke README.md`:
  - HEAD: 6d237966c838b8270b023ab5ba3757bb3449e585
  - Manifest, untracked: `src/WorkLog/Journaling/LoggingGuidance.cs`, `tools/WorkLog.Smoke/LiveWordingChecks.cs`.
  - Manifest, modified: `README.md`, `src/WorkLog/Integration/AgentInstructions.cs`, `src/WorkLog/Journaling/DigestService.cs`, `src/WorkLog/Journaling/LogRequest.cs`, `src/WorkLog/Mcp/JournalTools.cs`, `tests/WorkLog.Tests/Integration/InstructionOptionsTests.cs`, `tests/WorkLog.Tests/Mcp/DigestTests.cs`, `tests/WorkLog.Tests/Mcp/McpTests.cs`, `tools/WorkLog.Smoke/Program.cs`.
  - Tracked diff SHA-256: dda65f72cd811fbc9cc8b52494327ba4c1b0189125c911eb16ea460cecc0671e
  - Untracked SHA-256: 3afa9f6bfa2f924a16f1978d9581ffc37539342fa9bd61f5ecefa01297d917ba (LoggingGuidance.cs), bfb9cfdb95f69a3662d6fac62975fa3f2f4187bc5eee5b12c315523c18204748 (LiveWordingChecks.cs)
  - Summary: `head=6d23796 baseline=6d23796 files=11 diff=dda65f72cd81 untracked=2/04fb85d66245`, which matches the assignment.
- Before/after identity: see Assessment and limitations.
- Prior reports / rechecked findings: none for this role. Related: QA-001 ([qa-001](qa-001.md), via the ledger).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Duplication: guidance wording in production | Grep over `src`, `tests`, `tools` and `docs` for the old wording (`narrate process`, `Outcomes, not process`, `remaining step`, `one-line instruction`) and for uses of `LoggingGuidance.` | All five places now come from `LoggingGuidance`: `AgentInstructions.cs:16,21`, `LogRequest.cs:10`, `DigestService.cs:17` and `JournalTools.cs:29`. No old wording remains in production code. The `save_digest` method description (`JournalTools.cs:26`) is still written out in place, but it describes the tool's purpose, the IDs and the stale rule, not the content rule, so it does not compete with the shared rule | Meets the design intent. No per-client copy of the policy: both clients use the same `AgentInstructions.Render`, and `Integration/Clients` is untouched |
| Duplication: the `280` limit | Grep for `280` and `MaximumTextLength` | Production code holds it twice: `DigestService.cs:16` (int, enforced) and `LoggingGuidance.cs:11` (string, used in the wording). Only the test `DigestTests.GuidanceLimitTextMatchesTheEnforcedLimit` links the two. Two tests hardcode `"at most 280 characters"`: `InstructionOptionsTests.cs:263` and `McpTests.cs:117` | ANALYSIS-001 |
| Duplication: test wording | Diff review of the three test files | `McpTests.GuidanceMechanics` holds 7 phrase fragments copied from `ContentRule`, used by both `McpTests` and `DigestTests` | ANALYSIS-002 |
| Duplication: smoke live checks | Side-by-side read of `LiveWordingChecks.cs` and `LiveAgentChecks.cs` | Copied blocks: lines 19-22 vs 12-15 (`mcp.json` derivation), 26-27 vs 17-18 (Codex isolation flags, identical) and 30-32 vs 22-24 (Claude isolation flags, differing only in the allowed tools). `ReadDigests` (84-103) reads the digest store again with raw SQL | ANALYSIS-003, ANALYSIS-004 |
| Simplification | Read through `LoggingGuidance.cs` | `DigestTextDescription = Digest` and `DigestService.Instruction = LoggingGuidance.DigestInstruction` are aliases kept for naming. `AgentInstructions.cs:21` inserts `{Digest} {DigestSaving}`, which equals `DigestInstruction`. The constants are joined with one level of `+` and read easily | No finding; the aliases cost nothing and show which place each constant serves |
| Architecture: dependency direction | `grep "^using WorkLog"` per namespace, plus a search for Journaling type names in Integration at baseline | `Journaling` imports Configuration, Dashboard, Infrastructure, Persistence, Reporting and Updates, never Integration. This change adds a new edge from Integration to Journaling (`AgentInstructions.cs:2`): at 6d23796 Integration used no Journaling type, so design.md's Context sentence "Integration already depends on Journaling" is wrong. Nothing in Journaling, Dashboard, Persistence or Reporting imports Integration, so no cycle forms | Acceptable and consistent with `docs/architecture.md`, which gives Integration the job of rendering shared instructions. Recorded for the orchestrator; no finding |
| Architecture: constants usable in attributes | Read through | Every member is a `public const string` built only from string values and other constants, and they are used in `[Description]` at `LogRequest.cs:10` and `JournalTools.cs:29` | Meets the compile-time constant requirement (I did not rebuild; DEV-1 reported a clean build) |
| Parity (Claude Code / Codex) | Read through the rendering code and the tests | Managed text: one `Render` serves both clients. Both `InstructionOptionsTests` tests check parity by comparing the two blocks with the agent name replaced. `DigestParagraphIsSharedByClaudeCodeAndCodexOwnedBlocksAndOmittedWhenDisabled` also checks the files init writes for both clients (`CLAUDE.local.md` and the Codex TOML `developer_instructions`). Tool descriptions and the digest request instruction come from the server and are the same for every client. The live check runs both clients | No parity gap in the implementation. Per-check client coverage is listed below |
| Parity: guidance each client sees in the live check | Context7 `/websites/code_claude` (memory loading per setting source) for Claude Code; inference for Codex | Claude Code loads `CLAUDE.local.md` when the `local` setting source is enabled, so `--setting-sources local` keeps the managed block. Codex reads the project `.codex/config.toml`. `LiveAgentChecks` relies on that file's `mcp_servers.worklog` entry, which suggests Codex also reads `developer_instructions` from the same file (inferred, not tested) | Both clients should see the managed instructions, the tool descriptions and the request instruction. Not confirmed with a live run (none was assigned) |
| Formatting | `dotnet format ZeusApps.WorkLog.slnx --no-restore --verify-no-changes --include src/WorkLog/Journaling/LoggingGuidance.cs tools/WorkLog.Smoke/LiveWordingChecks.cs` | exit 0 | Both new files are formatter-clean |
| Performance: text size | String lengths computed from the `LoggingGuidance.cs` constants by a throwaway script in the scratch directory (not committed) | `ContentRule` is 423 characters. `SummaryDescription` is 623 (was 270), `DigestTextDescription` 630 (was 146) and `DigestInstruction` 801 (was 189). The managed block contains `ContentRule` twice (summary sentence and digest paragraph) | ANALYSIS-005 (sizes measured; the effect on tokens and latency is a hypothesis) |
| Default smoke path | Read `Program.cs:89-116` | `--live-wording` is excluded when picking the executable argument, and that branch returns before the default suite. Without the flag the sequence is unchanged. With both `--live-agents` and `--live-wording`, only the wording check runs and the rest is silently skipped | The default path is unaffected. The comment at line 97 documents the combined-flag behavior; no finding |
| Test brittleness: line-structure test | Read `InstructionOptionsTests.cs:33-40` | The restore-and-count check still works: the summary is one line, and the digest line is filtered out by `digestRequest` | No finding |

Client coverage of each check:

- `InstructionOptionsTests.RenderedInstructionsShareSummaryGuidanceAndOtherwiseKeepPriorText`: Claude Code and Codex (rendered text).
- `InstructionOptionsTests.DigestParagraphIsSharedByClaudeCodeAndCodexOwnedBlocksAndOmittedWhenDisabled`: Claude Code (`CLAUDE.local.md`) and Codex (`.codex/config.toml`), as written by init.
- The tool-description checks in `McpTests` and the instruction checks in `DigestTests`: the server contract is the same for every client, so they cover both.
- `LiveWordingChecks`: Codex, then Claude Code, one after the other (see ANALYSIS-006 on what happens when one fails).

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | None changed. The MCP server loop and the dashboard are untouched; only constant strings changed | source observation | Not applicable: no timer, poll or loop was added or changed. Per-request work in `DigestService.Request` is the same, apart from a longer constant in the response |
| Cache lifetime versus interval and refresh duration | None in scope | source observation | Not applicable: no cache was added or changed |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Journal and digest store | source observation | Unchanged. Stored digest text stays capped at 280 by `DigestService.MaximumTextLength`, and summaries keep their existing limits. The longer instruction is returned, not stored |
| Repeated full reads where a change check would do | `LiveWordingChecks.Report` | source observation | One journal query and one full read of `digests.db` per client, in a manual opt-in run over a tiny throwaway store. Not a steady-state cost |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | `LiveWordingChecks` starting `codex` and `claude` through `SmokeProcessRunner` | source observation | Reuses the existing runner: it finds `claude`, and on Windows `codex.cmd`, on PATH. `codex.cmd` is only the npm shim name, so other Codex installs such as a native `codex.exe` are not found (this predates the change). A 3-minute timeout kills the whole process tree. No new lookups, and every call has a timeout |

## Findings

### ANALYSIS-001: The 280 limit is defined twice in production code, and two tests hardcode it outside the guard

- Severity: suggestion
- Related task / requirement: 2.3, 2.4, 3.2; day-digests "Digest writing guidance" (280-character limit)
- Locations:
  - `src/WorkLog/Journaling/DigestService.cs:16` (`MaximumTextLength = 280`)
  - `src/WorkLog/Journaling/LoggingGuidance.cs:11` (`DigestLimit = "280"`)
  - `tests/WorkLog.Tests/Mcp/DigestTests.cs:73-77` (the guard test)
  - `tests/WorkLog.Tests/Integration/InstructionOptionsTests.cs:263`
  - `tests/WorkLog.Tests/Mcp/McpTests.cs:117`
- Observation:
  - Why there are two constants: C# cannot build a `const string` from an `int`, and the `save_digest` description must be a constant. The developer therefore added a string copy, a doc comment and an equality test. That is the right approach, and it explains why the code differs from task 2.3's "(from `MaximumTextLength`)".
  - Production drift is caught: `GuidanceLimitTextMatchesTheEnforcedLimit` fails if the two values differ.
  - The tests are not consistent: `InstructionOptionsTests.cs:263` and `McpTests.cs:117` assert the literal `"at most 280 characters"`, while `DigestTests.cs:67` already builds it from `MaximumTextLength`.
  - Cost: changing the limit means editing four places in code, and the two literal tests fail with a message that does not point to the guard.
- Proposed improvement and benefit:
  - In both tests, use `$"at most {LoggingGuidance.DigestLimit} characters"`, or `DigestService.MaximumTextLength` as `DigestTests.cs:67` does.
  - Optionally, move `MaximumTextLength` into `LoggingGuidance` beside `DigestLimit` and have `DigestService.MaximumTextLength` point to it, so the two values sit on adjacent lines.
  - Result: one place to edit plus one guard, with no change in behavior.
- Behavior-preservation risks: none for the test edit, which touches tests only. The optional move keeps `DigestService.MaximumTextLength` as an alias, so validation and its error message (`DigestService.cs:62`) do not change.
- Evidence type: source observation
- Verification / measurement plan: grep for `280` in `tests/WorkLog.Tests` and confirm that only the behavior tests at `DigestTests.cs:316-322` still use the literal. Then run `dotnet test --filter "FullyQualifiedName~InstructionOptionsTests|FullyQualifiedName~McpTests|FullyQualifiedName~DigestTests"`.
- Relationship: none

### ANALYSIS-002: The list of mechanic phrases adds nothing to the constant checks, ties two test classes together, and fails on harmless rewording

- Severity: suggestion
- Related task / requirement: 3.2; both delta specs (the named mechanics)
- Locations: `tests/WorkLog.Tests/Mcp/McpTests.cs:18` (`GuidanceMechanics`), `McpTests.cs:110-119`, `tests/WorkLog.Tests/Mcp/DigestTests.cs:62-65`
- Observation:
  - Redundant: each place's test already asserts `Contains(LoggingGuidance.ContentRule, ...)`, and `DigestTests.cs:62` asserts full equality with `DigestInstruction`. The tests then also loop over seven fragments that are substrings of `ContentRule`. Once the constant is present, the loops only re-check what `ContentRule` itself says, and they do it three times.
  - Brittle: the fragments copy the current grouping word for word (`"commits, hashes, branches, merges"`, `"test or task counts"`, `"archiving or syncing"`). Rewording without changing the meaning, for example "commits, hashes, branches and merges", fails three tests.
  - Not tied to the spec: the fragments do not use the spec's own terms ("test and task counts", "archiving and syncing"), so they pin the current wording rather than the requirement.
  - Coupling: `DigestTests` takes the list from `McpTests`, which ties two unrelated test classes together.
- Proposed improvement and benefit:
  - Add one focused test, for example `LoggingGuidanceTests.ContentRuleNamesTheRequiredMechanics`. It checks `ContentRule` for each required mechanic as a single keyword: `proposals`, `specs`, `delegation`, `review rounds`, `test`, `task counts`, `commits`, `hashes`, `branches`, `merges`, `archiving`, `syncing`, `builds`, `installation`.
  - Keep each place's test to asserting that the constant is present (or equal).
  - Result: the spec requirement is checked in one test, a rewording breaks only that test, and the dependency between the two test classes goes away.
- Behavior-preservation risks: none; only tests change. Every place stays covered through its `ContentRule` assertion.
- Evidence type: source observation
- Verification / measurement plan: run the three test classes. Optionally, change "branches, merges" to "branches and merges" in a scratch build (not committed) and confirm that exactly one test fails.
- Relationship: none

### ANALYSIS-003: LiveWordingChecks copies the client start-up and isolation flags from LiveAgentChecks

- Severity: suggestion
- Related task / requirement: 5.1a ("same isolation as `LiveAgentChecks`")
- Locations: `tools/WorkLog.Smoke/LiveWordingChecks.cs:19-22,26-27,30-32`; `tools/WorkLog.Smoke/LiveAgentChecks.cs:12-15,17-18,22-24`
- Observation:
  - Shared correctly: the environment isolation is reused through `SmokeProcessRunner` and `SmokeWorkspace`. That covers the overrides for `CODEX_HOME`, `CLAUDE_CONFIG_DIR` and `WORKLOG_DATA_DIR` and the throwaway workspace.
  - Copied:
    - The Claude `mcp.json` derivation (4 lines; one copy uses `'\\'`, the other `Path.DirectorySeparatorChar`).
    - The Codex argument list, identical: `--no-daemon`, the trust override, `exec --ignore-user-config --ephemeral -s read-only`.
    - The Claude argument list, which differs only in the allowed tool names: `-p --no-session-persistence --setting-sources local --strict-mcp-config --mcp-config ... --tools "" --max-budget-usd 1`.
  - Why it matters: these flags are what keeps runs on the user's accounts isolated. A fix to one check, such as the credential decision still pending under QA-001, has to be copied to the other by hand.
- Proposed improvement and benefit: add a small internal helper in `tools/WorkLog.Smoke`, for example `LiveClients` with `WriteClaudeMcpConfig(SmokeWorkspace)`, `RunCodexAsync(prompt)` and `RunClaudeAsync(mcpPath, prompt, params string[] allowedTools)`, and use it in both checks. The isolation and budget flags are then defined once, and the QA-001 fix applies to both live checks.
- Behavior-preservation risks: the argument order and values must stay the same. `--allowedTools` takes a variable number of values, so it must stay directly before `--tools ""`. Neither live check runs in CI, so the check is a build plus a comparison of the generated argument lists (`SmokeProcessRunner` already writes a log file for each run).
- Evidence type: source observation
- Verification / measurement plan: `dotnet build tools/WorkLog.Smoke -c Release`. During an assigned live run only, compare the `Arguments:` lines in the runner logs before and after. The default smoke run is unaffected, because neither class is created without its flag.
- Relationship: related to QA-001 (same start-up path; one helper keeps its fix in one place)

### ANALYSIS-004: ReadDigests reads the digest store again with raw SQL

- Severity: suggestion
- Related task / requirement: 5.1a
- Locations:
  - `tools/WorkLog.Smoke/LiveWordingChecks.cs:76,84-103`
  - The existing reader: `src/WorkLog/Persistence/DigestStore.cs:100` (`Read(IReadOnlyCollection<string> items)`)
  - The item key: `TimelineService.IdFor(entry, "ticket")`, as used at `src/WorkLog/Journaling/DigestService.cs:28`
- Observation:
  - The smoke check opens `digests.db` itself and runs `SELECT covers,text FROM digests`. This repeats the store's table layout and JSON decoding.
  - It also skips the store's version check (`DigestStore.cs:111-115`).
  - The existing reader is reachable: `WorkLog.Smoke` already has internals access (`WorkLog.csproj:14`), and `DigestAcceptanceChecks` reads only `PRAGMA user_version`, not the table.
- Proposed improvement and benefit:
  - Get the logged observation's item with `TimelineService.IdFor(logged[0], "ticket")`, call `new DigestStore(workspace.Data).Read([item])`, and keep matching on `Covers` as now.
  - This removes about 20 lines and the `Microsoft.Data.Sqlite` and `System.Text.Json` usings from the file.
  - A future change to the digest table can then no longer silently break the live check.
- Behavior-preservation risks:
  - Like `ReadDigests`, `Read` returns an empty array when the store is missing.
  - `Read` throws on a store newer than it supports, which is the wanted signal.
  - Filtering by item instead of by overlapping IDs gives the same result here, because each client's fixture uses its own change.
- Evidence type: source observation
- Verification / measurement plan: build the smoke project. The code runs only under `--live-wording`, so a functional check needs an assigned live run, or a direct call against a seeded throwaway data directory.
- Relationship: none

### ANALYSIS-005: The new guidance text is larger than design.md estimates, and every digest request repeats it

- Severity: suggestion (decision: accept or trim)
- Related task / requirement: 1.1, 2.1, 2.3; design.md Risks ("A few hundred characters")
- Locations: `src/WorkLog/Journaling/LoggingGuidance.cs` (all constants), `src/WorkLog/Integration/AgentInstructions.cs:16,21`
- Observation (measured string lengths):
  - Sizes:

    | Text | Before | After |
    | --- | --- | --- |
    | `ContentRule` | (new) | 423 |
    | `log_work` `summary` description | 270 | 623 |
    | `save_digest` `text` description | 146 | 630 |
    | Digest request `instruction` | 189 | 801 |

  - The managed block states `ContentRule` twice, once in the summary sentence and once in the digest paragraph: about 850 characters of repeated text.
  - The request instruction comes back with every `log_work` result that qualifies for a digest, which is the item's second through thirtieth observation of a local day.
  - What the spec forces: the request must be self-contained, so its size is required. The repetition in the managed block is not required. It comes from one reading of "every place SHALL state the same stand-up content rule".
- Proposed improvement and benefit: a decision for the orchestrator or the user.
  - Accept the sizes, and correct the design's estimate the next time it is revised; or
  - In the managed digest paragraph only, refer to the rule ("by the same stand-up rule as summaries") instead of inserting `Digest`. This saves about 420 characters per managed block, at the cost of a looser reading of the spec scenario "Same digest guidance for both clients".
  - Neither option changes the tool descriptions or the request instruction.
- Behavior-preservation risks: trimming changes what `InstructionOptionsTests.cs:260` asserts and how the spec scenario is read. Accepting changes nothing.
- Evidence type: measured result (character counts). Any cost to clients in tokens, latency or attention is a performance hypothesis, not measured.
- Verification / measurement plan: if the paragraph is trimmed, compare `AgentInstructions.Render("repo", "claude").Length` before and after in a test or a scratch run. Any claim about how agents behave needs the assigned live check (5.2).
- Relationship: none

### ANALYSIS-006: The live wording run cannot tell "no digest was requested" from "the client ignored the request", and one client's failure stops the other from running

- Severity: suggestion
- Related task / requirement: 5.1a, 5.2
- Locations: `tools/WorkLog.Smoke/LiveWordingChecks.cs:26-33,44-57,62-82`; compare `LiveAgentChecks.cs:19,25` (`RequireSuccess` on `SMOKE_OK`)
- Observation:
  - (a) Missing digests are not explained. `Report` prints "(no digest stored; the client did not save one)" whenever no digest overlaps, without checking whether a digest request was due. A fixture or day-boundary problem therefore looks like the client ignoring the rule.
    - Ruled out: the repository identity matches the server's. `McpServerHost.cs:18` and `LiveWordingChecks.cs:42` both use `RepositoryRegistry.Get`.
    - Remaining gap (hypothesis, an edge case): the seeds are placed at now minus 6, 4 and 2 minutes, while digests group by the machine's local day (`DigestService.cs:98`). A run started within a few minutes after local midnight splits the seeds across two days.
  - (b) Client output is thrown away. The prompt asks the client to reply `WORDING_DONE`, but nothing checks for it, unlike `LiveAgentChecks`.
  - (c) One failure blocks both clients. Codex runs first, and `SmokeProcessRunner.RunAsync` throws on a nonzero exit code. A Codex failure, as seen under QA-001, therefore ends the run before Claude Code produces any evidence. For a parity check, one client's failure should not block the other's result.
- Proposed improvement and benefit:
  - After each client run, print whether the logged observation qualified for a digest. Either count the entries with the same item on the same local day (at least 2 qualifies), or call `new DigestService(journal, new DigestStore(workspace.Data)).Request(logged[^1])` in-process. `Request` calls `store.EnsureAvailable()`, which is acceptable in the throwaway data directory.
  - Print the runner's log path, or whether `WORDING_DONE` appeared.
  - Run each client in its own try/catch that records the failure and moves on, then fail at the end if either client failed.
  - If the midnight edge matters, place the seeds relative to the local day, for example no earlier than local midnight plus a margin.
  - Result: the 5.2 reviewer gets a clear result for each client.
- Behavior-preservation risks: this is the opt-in path only, so the default smoke run is unaffected. After a Codex failure the process must still exit with a nonzero code, so that a failed live check is never reported as complete.
- Evidence type: source observation; the midnight split is a hypothesis and was not reproduced
- Verification / measurement plan: build the smoke project. Testing the behavior needs an assigned live run, or a stand-in client executable on PATH inside an isolated workspace; both are outside this assignment.
- Relationship: related to QA-001 (item c is why QA-001 left Claude Code without evidence)

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | n/a | n/a | not applicable (first round) |

## Assessment and limitations

- The core design intent is met:
  - The stand-up rule exists once, in `WorkLog.Journaling.LoggingGuidance`, as `const string` parts.
  - All five places use it: the managed summary sentence, the managed digest paragraph, the `summary` description, the request `instruction` and the `text` description.
  - No old wording remains in production code.
- Dependencies are sound. Journaling does not depend on Integration. The edge from Integration to Journaling does not create a cycle; it is new with this change, not pre-existing as design.md's Context says.
- Parity holds. Claude Code and Codex share one rendered text, the tests assert this for both clients, and nothing was added under `Integration/Clients`. I found no parity gap in the implementation.
- `LiveWordingChecks` reuses the environment isolation (`SmokeWorkspace`, `SmokeProcessRunner`) but copies the client flag sets (ANALYSIS-003). The default smoke path is unaffected.
- The 280 limit cannot drift silently in production code. Two test literals remain outside the guard (ANALYSIS-001).
- All six findings are suggestions and none blocks the change. ANALYSIS-005 needs a decision: accept the size or trim the managed paragraph.
- Not performed:
  - Build and test runs: QA owns the suite; DEV-1 reported 0 warnings, 0 errors and 408/408 tests passing.
  - Live agents: not assigned.
  - `init`: not needed.
  - Measurement of the effect on tokens or latency.
- Writes: none in the repository. The client refused my write of this report, so the orchestrator must save it. A throwaway length-count script was written to the session scratch directory, outside the repository.
- Before/after identity: after the analysis, `./scripts/review-identity.ps1 -Baseline 6d23796 -Paths src/WorkLog tests/WorkLog.Tests tools/WorkLog.Smoke README.md` printed `head=6d23796 baseline=6d23796 files=11 diff=dda65f72cd81 untracked=2/04fb85d66245`, the same as before the analysis.
