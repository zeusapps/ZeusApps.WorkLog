# MCP implementation validation

Reviewed against the official C# SDK documentation, the installed ModelContextProtocol 2.2.0 API documentation, and the MCP tools specification. The host remains on the official SDK; no custom JSON-RPC implementation was introduced.

| Area | Finding and resulting behavior | Verification |
| --- | --- | --- |
| Transport and discovery | Existing AddMcpServer, stdio transport and WithTools registration match SDK guidance. Server identity is explicitly worklog plus the application version. | SDK client initialization and tools/list assertions |
| Standard output | Existing logging to stderr was correct. Successful serve operation reserves stdout for protocol messages. | End-to-end stdio tests and raw JSON-RPC lifecycle test |
| Tool results | Original POCO returns provided JSON text only. Enable UseStructuredContent to advertise outputSchema and populate structuredContent while retaining text content. | Assert schemas, structured results, and semantic equality with fallback JSON text |
| Annotations | Logging is local, additive, non-destructive, and not unconditionally idempotent because its retry key is optional. Querying is local, read-only and idempotent. | Assert advertised annotations |
| Tool failures | Existing McpException mapping is appropriate for recoverable application validation failures. Add explicit null guards for tool request objects. SDK retains responsibility for protocol errors and argument deserialization. | Invalid status returns isError with a useful message; subsequent calls still work |
| Lifecycle | SDK owns stdio session lifetime. Serve forwards the CLI cancellation token to the host. | Closing client stdin after initialization exits cleanly with code zero and no stray stdout |
| Input guidance | Describe timestamp bounds, status meanings, classification overrides and retry-key semantics in generated input schemas. | Tool discovery through the SDK client |
| Output guidance | Describe response models and their fields, including work versus save timestamps, replay outcomes, classification snapshots, ticket scopes and duplicate flat/grouped views. | Assert descriptions in tools/list output schemas, including nested entry, ticket and daily-group schemas |

System.CommandLine 2.0.13 replaces the manual CLI parser. Init and serve have separate option sets; generated help and validation run before command actions. Regression cases cover missing values, unsupported clients, unknown options, foreign-command options, help and version behavior.

Init regression cases also cover paired ticket provider/scope options and mutually exclusive instruction controls. Integration tests exercise block removal, persistent per-client opt-out, re-enable, dry-run/preflight preservation, tracked destinations, and shared worktree context. Reporting tests cover unresolved-ID matches with combined filters, pagination/snapshots, and strict qualified matching. Classification tests verify old configurations receive the bugfix alias while custom mappings and historical observations remain intact.

The journal schema remains unchanged. Report schema version 1 deliberately adds compact pagination and selectable views. CLI help/error text comes from System.CommandLine, and options that never applied to serve are rejected.

## Audit corrections

The independent 2026-10-20 audit found that nullable required output properties were omitted. Earlier tests checked schema presence, descriptions and text equivalence but did not validate complete results against the schemas. Dedicated MCP serialization now emits explicit nulls. Tests use JsonSchema.Net to evaluate actual stdio responses against tools/list schemas, including empty results and every report view; a deliberately incomplete response verifies that validation fails.

Regression tests cover effective-repository ticket inference, order-independent ticket-set grouping, trimmed ticket fields and filters, indexed errors for null tickets, numeric Azure DevOps IDs, and missing period fields. Unknown input fields are rejected rather than silently ignored. Reports use SQL filtering and snapshot keyset pagination, with a 64 KiB structured-payload budget, default 25 observations, selectable views and optional summary exclusion. Tests traverse timestamp ties, concurrent backdated writes, Unicode summaries and every projection without losing entries.

An SDK call-tool request filter checks unknown field names before argument deserialization, using the same serializer metadata as the tool bindings. Errors expose the offending path, such as entry.sumary, entry.tickets[0].urll or period.pagesizee, without exposing internal exception details. Protocol tests verify these errors, absence of partial writes and successful queries after rejection. Other deserialization and domain validation remain with the SDK and journal respectively.

Context discovery, auditable corrections, search tools and schema-level status/provider enums remain potential follow-ups; they are not implemented by these fixes.

This verifies the implementation and local protocol behavior. It does not claim complete conformance to every optional MCP feature or replace the pending live Claude Code verification.

## Versioned activation validation

The launcher-layout implementation is an unreleased source change; it does not alter the installed MCP or published v0.1.1. The old F9 in-place readiness plan is superseded, with its tasks unimplemented. Tests use disposable installations, profiles, data and synthetic observations. No authenticated agent prompts are required.

Repeatable commands:

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
./scripts/package.ps1 -OutputDirectory artifacts/releases/FRESH-RUN
./scripts/package-update-fixtures.ps1 -NativeClients
```

Omit `-NativeClients` on CI without installed agent CLIs. The script builds different compiled payload versions 0.0.1/0.0.2, verifies their executable/MCP versions and hashes, publishes a test-only launcher probe, then runs launcher and mixed-version acceptance. A production ZIP can also be tested as the newer candidate with `dotnet run --project tools/WorkLog.Smoke -c Release --no-build -- --versioned-native OLD_LAUNCHER NEW_ZIP`. Production packaging still requires the configured application/tag version. Pin the first independently verified launcher-based public release as an additional old-version baseline once it exists.

Recorded local checks on Windows 10.0.26300.0:

| Evidence | Observed result |
| --- | --- |
| `artifacts/update-smoke/628ebc0e310c4aa788bfe3893de34b8a/evidence.json` | Actual 0.0.1/0.0.2 versions and different hashes; original payload PID/start time and MCP stream survive activation; new connection adopts selection; retries, first starts, cursor snapshots, independent settings processes, custom data directories, status/publisher/launch overlap, rollback, interrupted activation and retained versions pass |
| `artifacts/launcher-smoke/facf8bab3795402bb9929e92a10b0535/evidence.json` and `5dcb08941ebc42279c0ba9e59f362d1b/evidence.json` | Empty, spaced, quoted, backslash and Unicode arguments, Unicode cwd/environment, 1,146,880 echoed bytes with backpressure/EOF, stderr and exit code 7 preserved; isolated CTRL+C returns child exit 23 inside a client job; restricted nested-job creation fails without detaching a payload |
| `artifacts/smoke/ee1ce600ace4452f89de632a99fbaf4c/` | Packaged stable launcher passes native Codex discovery/instruction loading and Claude connection with instructions enabled/disabled; packaged ticket/context/opt-out regression and journal upgrade checks pass |
| Settings/updater regression tests | Preserve nested unknown JSON, merge independent stale edits, reject conflicting leaves, converge first repository identities, reject unsafe/missing metadata and archives, refuse stale activation/unsafe rollback, honor cancellation and permanent access failures, suppress activated offers and preserve protocol budgets |

Final validation passed locked restore, a Release build with zero warnings/errors, and all 149 tests. The final 66,615,624-byte review package is `artifacts/releases/session-preserving-verified/worklog-0.1.1-win-x64.zip`; its version is the unchanged source version, not a new public v0.1.1 release. Final native/client smoke is recorded under `artifacts/smoke/9c093a90b99949f08db47e565dacff27/`, with bootstrap evidence under `artifacts/update-smoke/673afd0a283241b996d4e27ccb07f711/`.

The final mixed-version run (`artifacts/update-smoke/758fcaa9fb6f45bdb6d8edad1c1991ba/`) activated compiled 0.1.1 over compiled 0.0.1 while original payload PID 1496 and its stream survived. It also exercised settings writes and first-use repository identity from both compiled versions, retained-payload init preserving the stable launcher and opt-out, and native Codex 0.163.0 discovery plus Claude Code 2.1.306 connection. Ten packaged rejection logs cover checksum, package manifest, runtime, version, executable hash, shared-data contract, journal schema, traversal, reparse entry and case collision. Previously published payloads remain intact after those failures. `evidence.json` records both executable hashes, original process start time and final selection; `native-clients.json` records exact CLI versions. This is local disposable acceptance, not a remote CI run or production rollout.

Native checks establish terminal CLI behavior only. They do not establish Desktop or every worktree launch mode. Codex loads project configuration only for trusted projects; the disposable fixture persists its own project trust in its isolated home, matching [official configuration guidance](https://learn.chatgpt.com/docs/config-file/config-basic). Production init does not grant trust. The extra retained-version probe records exact CLI versions in `native-clients.json` alongside its protocol logs.

A separate extra probe under `native repo with spaces żółć` failed during Git-backed init even with direct payload invocation (no launcher). That path is not a claimed native-init result. The native registration fixture uses a path with spaces; Unicode argument/cwd/environment forwarding is independently proved by the launcher probe. Resolving the Git-path behavior is outside this updater change, alongside the independent F6-F8 follow-ups.

Review follow-up on 2026-10-22 fixed legacy `bugfix` normalization across settings refresh, copy and adoption. Nine new test cases cover save, first-use/existing repository resolution, copies, and case-insensitive custom mappings including `unclassified`; the initialization regression also now uses legacy branch settings. Five new cases failed before the fix. After the fix, the Release build passed with zero warnings/errors, all 158 tests passed (`tests/WorkLog.Tests/TestResults/<account>_<host>_2026-10-22_21_43_23.trx`), and formatting verification passed. The history fixture now writes legacy JSON directly instead of relying on current `Save()` to emulate an older writer. The packaged acceptance evidence above predates this follow-up; those packages were not rebuilt for this settings-only fix.

## Development build validation

Validation of the development-build change (`add-dev-builds`) on Windows 10.0.26300.0 from a working tree on top of commit `8a7508e`, always on disposable installations and data directories under `artifacts/` (nothing touched the default installation, the real data directory or port 47831). The MCP tool schemas, `updateHint` fields, `worklog init` and the managed instructions are unchanged; the MCP server version stays the bare `MAJOR.MINOR.PATCH`, and the health probe compares it with the package version. Evidence directories are under `artifacts/dev-it/` (installer actions against a stable 0.3.0 package) and `artifacts/dev-it33/` (the end-to-end path below).

End-to-end path (task 3.3): stable `0.0.1` installed from a package, dev `0.0.9001` (marker base `0.0.1`) installed with `scripts/install-dev.ps1`, stable `0.0.2` installed through the update path against a local release fixture, then `worklog update --rollback`. The fixture serves the packaged `0.0.2` ZIP and checksum for the official release URLs to the real `PayloadInstaller.DownloadAsync` (including `ReleaseInfo.IsValid` and the checksum, extraction and health-probe verification), because the update command itself can only reach the fixed GitHub endpoint. Every launcher and payload below is a real packaged build and the real health probe ran for each activation.

| Step | Observed result |
| --- | --- |
| Start | Launcher `--version` prints `0.0.1`; selection `0.0.1`, no previous, generation 1 |
| `Preflight` of the `0.0.9001` package | Prints the operation record path and label `0.0.9001 dev <commit> dirty (<UTC build time>)`; selection unchanged |
| `Switch` | Selection `0.0.9001`, previous `0.0.1`, generation 2; `--version` prints `0.0.9001 dev <commit> dirty (<UTC build time>)` (first token `0.0.9001`); `update --status` prints the same label for running and selected; no process stopped |
| Stable `0.0.2` over dev `0.0.9001` | The offer is valid and newer than the effective selected version `0.0.1`; staged and activated; selection `0.0.2`, previous `0.0.9001`, generation 3; `--version` prints the plain `0.0.2` |
| `worklog update --rollback` | Selection `0.0.9001`, previous `0.0.2`, generation 4; `--version` prints the dev label again |

Installer action checks (task 3.1 and 3.2) with a stable `0.3.0` package as the installation and dev packages `0.3.9001` to `0.3.9004` built by `package.ps1 -Dev` (numbers 9001 then 9002 and so on, and `-PlanOnly` writing nothing):

- `Preflight` left the installation file hashes unchanged (and the data directory absent), refused a stable package ("not a development-range version"), and refused a package after a build-input file was touched ("changed after the package was built"). Unit tests additionally cover a different commit, an unmarked package, and a corrupt checksum; `-SkipProbe` and a custom data directory were refused for the default installation.
- `Switch` selected `0.3.9002` with `0.3.0` as previous, then `0.3.9003` and `0.3.9004` with the preceding dev build as previous. `Rollback` of the latest switch restored `0.3.9003`; a second `Rollback` of the same operation was refused ("not 'switched'"), and `Rollback` of an earlier switch after a later selection change was refused with the selection unchanged.
- `Retire` refused stable `0.3.0`, the selected `0.3.9004`, and the previous `0.3.9003`; with a live process whose image was that payload's `worklog.exe` it refused and left the process running (PID reported); after the process exited on its own it removed the payload, which had its marker stripped to emulate the manual unmarked build. Staging-referenced, tampered and finished-staging payloads are covered by unit tests.
- The `PayloadInstaller` dev-range behavior (stable `0.3.1` offered and activated over dev `0.3.9001`, marked and unmarked; `0.2.x`, `0.3.0` and a published `0.3.9005` refused or ignored) is covered by `DevBuildVersionTests`.

Observation outside this change: a rollback creates its health-probe data folder as `versions/.health-<id>` and leaves it behind; Retire ignores it.

### Validation set (task 5.1)

| Command | Result |
| --- | --- |
| `dotnet restore --locked-mode` | passed (new tool project and test project lock files included) |
| `dotnet build -c Release --no-restore` | passed, 0 warnings, 0 errors |
| `dotnet test -c Release --no-build --no-restore` | 337 passed, 0 failed (after the fix round: stale-rule tests in a disposable Git repository, Switch stage cleanup, Retire after Rollback, skill authorization text) |
| `npm test --prefix dashboard` | 83 passed |
| `./scripts/package.ps1 -OutputDirectory artifacts/releases/add-dev-builds-validation` then `npm run test:e2e --prefix dashboard` with `WORKLOG_DASHBOARD_EXECUTABLE` and `WORKLOG_DASHBOARD_NO_NODE=true` | stable package `0.3.0`: 19 of 19 browser scenarios passed, including the footer version label; the first scenario also passed against the packaged dev build `0.3.9004` (label accepted by the same pattern) |
| `dotnet run --project tools/WorkLog.Smoke -c Release --no-build -- --updates-only <packaged worklog.exe>` for the packaged stable `0.3.0` | passed (bootstrap launcher, MCP protocol, offline status) |
| the same command for the packaged dev build `0.3.9001` (`./scripts/package.ps1 -Dev`; launcher prints `0.3.9001 dev <commit> dirty (<UTC build time>)`) | passed after the smoke tool compared only the first `--version` token (`UpdateAcceptanceChecks`, `ClientRegistrationChecks`, `WorktreeAcceptanceChecks`); it failed with "Bootstrap launcher version mismatch" before. Repeat it for every dev-build change |
| disposable `scripts/install-dev.ps1` flow on a copy of the packaged stable installation, real health probe (Preflight, Switch, Rollback, Retire, Switch of `0.3.9002`, Retire) | Switch left no directory under `<data>/updates/staging`; Retire of the rolled-back `0.3.9001` was refused with the explanation (still previous), and succeeded after the further Switch |
| `./scripts/package-update-fixtures.ps1` | passed both stages when the maintainer ran it in an interactive console on 2026-10-28 (branch `change/add-dev-builds`, current working tree, Windows NT 10.0.26300.0). `LAUNCHER PASSED`: exact argv/cwd/environment, concurrent byte streams/backpressure/EOF/stderr/exit, isolated CTRL+C and nested-job refusal. `MIXED-VERSION PASSED`: original PID/stream survived, new version adopted, retries/starts/cursors/settings/status/rollback/recovery/retention and launcher lifetime verified. An earlier non-interactive agent run had failed only the CTRL+C step, which needs an interactive console |

### Skill directories under `openspec update` (task 5.2)

`openspec update --force` (OpenSpec CLI 1.13.1) in a disposable copy of the repository under `artifacts/` refreshed the generated OpenSpec skills for Claude Code and Codex and left both `install-dev-build` directories (each one `SKILL.md`) byte-identical to the originals. The generated skill, command and Codex configuration files were also unchanged by that update.

## Observation summary guidance validation

The `improve-observation-summaries` change replaces the managed-instruction sentence about summaries with one shared sentence (one short result-focused sentence of about 140 characters; `in_progress` states what was achieved and what remains, `done` what was completed; no process narration, status repetition, secrets or personal data) and adds the same guidance as the description of the `log_work` `summary` input. The summary stays optional and is not validated beyond existing limits.

| Check | Clients covered | Result |
| --- | --- | --- |
| `InstructionOptionsTests`: rendered blocks contain the identical summary sentence, differ only by the agent name, and restore the previous 15-line block when the sentence is substituted back | Claude Code and Codex (one shared renderer) | passed |
| `McpTests`: the stdio `tools/list` input schema for `log_work` exposes the `summary` description and does not require it; the first observation in the protocol test omits the summary and is saved | both (client-neutral tool contract) | passed |
| `worklog init --dry-run` on a disposable Git repository with an isolated `--data-dir` | both (`--clients both` default) | planned the Claude Code block in `CLAUDE.local.md` and the Codex block in `.codex/config.toml` with managed instructions enabled, plus the normal registration and ownership files; wrote nothing |
| `dotnet restore --locked-mode`, `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-build --no-restore` | n/a | passed, 0 warnings, 0 errors, 340 tests passed |
| `./scripts/package.ps1 -OutputDirectory artifacts/releases/improve-observation-summaries-validation` | n/a | passed (stable `0.3.0` package) |
| `dotnet run --project tools/WorkLog.Smoke -c Release --no-build -- <packaged worklog.exe>` (default isolated run, no live agents) | both | `SMOKE PASSED` |

A dry run on a fresh repository lists the owned blocks as planned writes; it cannot show a refresh of previously installed text without a non-dry-run init, which would modify the real Claude configuration and was not run. Rerunning `worklog init` on an initialized repository replaces the owned block through the same renderer.

## Stand-up summary guidance validation

The `standup-oriented-summaries` change moves the wording for observation summaries and day digests into one shared source (`LoggingGuidance`). It tells agents to write for the daily stand-up (what changed in the feature, story or bug fix, and blockers that need someone else) and to leave out development workflow mechanics. The same text feeds the managed instructions, the `log_work` `summary` and `save_digest` `text` input descriptions, and the digest request.

| Check | Clients covered | Result |
| --- | --- | --- |
| `InstructionOptionsTests`: rendered blocks contain the shared content rule and digest paragraph, with the digest limit built from the shared constant, and differ only by the agent name | Claude Code and Codex (one shared renderer) | passed |
| `McpTests`: the stdio `tools/list` descriptions for `log_work` `summary` and `save_digest` `text` contain the shared content rule and digest limit; `summary` stays optional | both (client-neutral tool contract) | passed |
| `DigestTests`: the digest request instruction equals the shared digest instruction and contains the content rule; the digest limit equals the service maximum | both | passed |
| `LoggingGuidanceTests`: the content rule names each excluded mechanic (proposals, specs, delegation, review rounds, test, task counts, commits, hashes, branches, merges, archiving, syncing, builds, installation) | both | passed |
| `dotnet restore --locked-mode`, `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-build --no-restore` | n/a | passed, 0 warnings, 0 errors, 422 tests passed |
| `worklog init --dry-run` with an isolated `--data-dir` | both (`--clients both` default) | passed; a dry run prints planned paths only, so the block text was verified from a non-dry init inside the smoke tool's disposable home (QA report qa-001) |
| Isolated live wording check with real agents | Claude Code and Codex | not usable: isolated client homes have no credentials (Codex 0.165.0 returned HTTP 401). Replaced by user-observed acceptance on an installed development build, pending |

The unit tests prove the shared text reaches every surface for both clients; they do not prove how a live model phrases a summary, which is why the user-observed acceptance remains open.

## Windows Claude project-alias validation

On Windows 10.0.26300.0 with Git 2.51.0.windows.2 and Claude Code 2.1.306, the isolated baseline reproduced F7: with a registration stored only under `aliasrepo`, `claude mcp get worklog` connected from that lowercase launch path but reported no server from the `AliasRepo` launch path. The exact commands, launch directories and outputs are retained under `artifacts/smoke/f33059acba494ad2a9bd8fecbabe5fbb/`; `claude-aliases/baseline.json` records versions and both results. This is versioned native evidence, not a documented guarantee about every Claude release.

The fixed review package, `artifacts/releases/claude-aliases-review/worklog-0.1.1-win-x64.zip` (66,614,923 bytes), passed focused native acceptance under `artifacts/smoke/4ffdd49def6444ad94cd3da4386898b4/`. Its `claude-aliases/evidence.json` records successful discovery and connection from both launch spellings through the stable launcher, actual command/arguments, preserved unrelated project/server settings, unchanged dry-run snapshots, repeat-init stability, persistent instruction opt-out and explicit re-enable. The package retains the source version 0.1.1 for local review; no release or installed-client migration is implied.

The full packaged smoke passed under `artifacts/smoke/6855a1e3fc6140eea57e6e5c91e92eaf/`, including native Codex discovery/instruction loading, Claude connections with instructions enabled/disabled, journal/context acceptance and the alias fixture. Bootstrap acceptance passed under `artifacts/update-smoke/9cb06b2b2d6c44f2ad8a0b6d2a2e004d/`. After formatting and the final real-worktree regression fixture, the Release build passed with zero warnings/errors and all 176 tests passed (`tests/WorkLog.Tests/TestResults/claude-alias-final.trx`). Changed C# files pass targeted formatting verification; repository-wide formatting verification still reports unrelated pre-existing issues.

The final 66,615,902-byte review package, `artifacts/releases/claude-aliases-verified/worklog-0.1.1-win-x64.zip`, includes the updated installation guide. Its native alias acceptance passed under `artifacts/smoke/68bdc19aaa774add820d7dcc569eabf9/claude-aliases/`; `baseline.json` and `evidence.json` capture before/after launch-path results. Strict OpenSpec validation passed all seven active changes/specifications.

Repeat the isolated alias check against a freshly packaged launcher:

```powershell
dotnet run --project tools/WorkLog.Smoke -c Release --no-build -- --claude-aliases-only PATH_TO_PACKAGED_LAUNCHER
```

The default packaged smoke also runs this fixture. It uses disposable repositories, data and `CLAUDE_CONFIG_DIR`; native probes use `mcp get` without authenticated agent prompts. Each `.log` includes the invoked command, arguments, launch directory, exit code and output. The pre-reconciliation probes record discovery independently before the fixture deliberately introduces stale owned arguments and reruns init. Regression tests additionally cover all aliases failing preflight together, malformed registration shapes, slash variants, canonical-only/lowercase-only/multiple entries, separate checkout paths, trust/settings preservation and non-Windows exact-case matching.

These results cover known aliases in terminal Claude Code. Previously unseen spellings require rerunning init after their entries exist. They do not establish automatic registration in a fresh worktree (F6), Desktop behavior, symlink resolution or case-sensitive Windows directories. The official [MCP quickstart](https://code.claude.com/docs/en/mcp-quickstart), retrieved through Context7 on 2026-10-22, documents local-scope storage, `CLAUDE_CONFIG_DIR` and `mcp get` diagnostics; the casing claim above comes from the fixture.

## Selectable ticket-grouping validation

`query_work` accepts optional `period.groupBy` with `ticket` as the default for complete qualified ticket-set grouping and `change` for the previous change-first behavior. Generated input and daily-identity descriptions explain the selector. Null, blank and unsupported values are rejected before reading a page, including flat view; unknown fields such as `period.groupByy` retain path-specific errors. Request field names retain the existing serializer's case-insensitive matching.

Reporting regressions exercise historical observations without a change alongside multiple changes for the same qualified ticket, omitted/explicit ticket equivalence and exact legacy identities under explicit change, unchanged metadata, reordered/deduplicated ticket sets, overlapping sets without duplicate assignments, namespace/repository separation and unresolved-ID fallback after context configuration. Both modes traverse timestamp ties, concurrent backdated writes and all four views within the 64 KiB budget. A ticket-mode fixture combines every filter with page size 1 across a Warsaw midnight and merges fragments by `(date, workIdentity)`. Continuation accepts omitted/explicit ticket interchangeably and rejects switching modes. Flat observations stay unchanged; groups remain page fragments rather than whole-period totals or final statuses.

The stdio protocol fixture validates tools/list guidance, schema-valid output and JSON text equivalence across both modes, all views and summary inclusion/exclusion. Invalid selectors and misspelled fields are followed by a successful query verifying the original observation count. The packaged smoke uses a separate disposable journal with historical/new same-ticket observations, verifies omitted/change/ticket modes through native tool calls with page size 1, and compares each original observation's complete JSON metadata. It records `ticket-grouping/evidence.json` under the smoke run directory.

Repeatable checks:

```powershell
dotnet restore --locked-mode
dotnet test tests/WorkLog.Tests/WorkLog.Tests.csproj -c Release --filter 'FullyQualifiedName~Reporting|FullyQualifiedName~Mcp'
dotnet test -c Release
./scripts/package.ps1 -OutputDirectory artifacts/releases/ticket-default-review
dotnet run --project tools/WorkLog.Smoke -c Release -- artifacts/releases/ticket-default-review/worklog-0.1.1-win-x64/worklog.exe
```

Keep all options unchanged during continuation, including grouping. Consumers must retrieve every page and merge matching date/identity fragments. After upgrading, restart the MCP client for refreshed tool discovery and begin a fresh query; older cursors may be rejected because the request fingerprint now includes grouping. The journal and response schema version remain unchanged. Older binaries reject the new request member; omit it after rollback. Grouping never rewrites stored data or resolves an unqualified historical ID.

Final local validation on 2026-10-22 passed locked restore, a Release build with zero warnings/errors, all 32 focused reporting/MCP tests and all 196 tests without skips. Results are retained in `tests/WorkLog.Tests/TestResults/ticket-default-focused.trx` and `ticket-default-full.trx`. Changed C# files pass targeted whitespace-format verification. Strict change and main-spec validation pass. Review strengthened the work-switch regression to check explicit change groups and the combined default ticket group; protocol coverage also proves omitted/explicit ticket continuation equivalence and rejects switching to change mode.

The 66,616,885-byte ticket-default review package is `artifacts/releases/ticket-default-review/worklog-0.1.1-win-x64.zip` (SHA-256 `fe71569e639784502fd266e315b6a2d74e8bbdd62335d5c3fa28ad80745bd50e`). The source version remains 0.1.1; this is a local review build, not a published release or installed-client update. Full packaged smoke passed under `artifacts/smoke/aa0a234e04f34e5180686eb13104a58d/`, including Codex discovery/instruction modes, Claude connections, initialization/context acceptance and Windows path-alias checks. Its `ticket-grouping/evidence.json` records three observations, page size 1, preserved metadata and the default ticket-grouped journal snapshot after all mode checks. Bootstrap protocol/EOF and offline-status acceptance passed under `artifacts/update-smoke/3d57fb97c5584274a7a15ca770c0829d/`. Native checks used disposable profiles/data without authenticated agent prompts; the alias fixture records Windows 10.0.26300.0, Git 2.51.0.windows.2 and Claude Code 2.1.306.

The same local run used Codex CLI 0.163.0 and .NET SDK 10.0.113.

## Terminal-client worktree startup validation

The `verify-worktree-server-startup` acceptance uses a committed disposable main repository, initializes it with the packaged launcher, and then creates an external sibling worktree with Git. It probes the fresh worktree before init or generated-file copying. Each client is initialized explicitly in that worktree, checked again, and checked from the main checkout afterward. Both homes, trust entries, data and observations are isolated; no authenticated agent prompts or real client profiles are used.

Recorded on 2026-10-25 with Windows 10.0.26300.0, Git 2.51.0.windows.2, Claude Code 2.1.310, Codex CLI 0.164.0 and the unreleased launcher-based WorkLog source version 0.1.1:

| Stage | Claude discovery and native health | Codex discovery | Separate direct MCP/context check |
| --- | --- | --- | --- |
| Main checkout initialized | Found main registration; connected | Found main registration | Both passed |
| Fresh external worktree, before init | Found inherited main registration; connected | WorkLog absent | Claude passed; Codex skipped because no registration was discovered |
| Worktree after explicit per-client init | Found main registration; connected, despite a correctly written worktree definition | Found worktree registration | Both passed |
| Main checkout after worktree init | Found main registration; connected | Found main registration | Both passed |

Evidence is retained under `artifacts/smoke/557c8535c8454ff4b634a21da5ac46ae/worktree-startup/evidence.json`, with command, cwd, exit-code and output logs referenced relative to that smoke run directory. Acceptance checks the definition written by init separately from the client's effective selection: Claude can continue preferring the inherited definition. It records the effective repository/data arguments and verifies shared context through that actual connection. The passing run includes all eight stages and seven direct connections; Codex's pre-init absence is a finding rather than a baseline/setup failure.

Claude uses `claude mcp get worklog` for native discovery and health. Its displayed command and complete argument string are matched to the fixture's private definitions. Codex uses `codex -c 'projects."CHECKOUT".trust_level="trusted"' mcp list --json`, with trust also persisted only in the isolated `CODEX_HOME`. Codex's JSON supplies the exact stdio command/arguments; it establishes configuration discovery, not native connection health. For every discovered definition, a separate official SDK connection uses that command, arguments, launch cwd and isolated environment. It records process launch, MCP initialization, both named tools with output schemas, and server version separately from native results. Synthetic logging verifies one repository identity and qualified ticket scope from main and worktree, and queries verify the complete shared history.

The bounded 64 KiB summary distinguishes discovery, process-launch, MCP-initialization/tool-discovery, repository-context and native-health failures. Missing prerequisites and failed baselines/setup remain acceptance failures. Native pre-init absence does not abort the explicit-init checks. The fixture checks that generated WorkLog files remain absent throughout the pre-init probes. Process startup and health probes are bounded, and all artifacts remain local.

Repeat against a packaged launcher:

```powershell
dotnet build -c Release --no-restore
dotnet run --project tools/WorkLog.Smoke -c Release --no-build -- --worktrees-only PATH_TO_PACKAGED_LAUNCHER
```

The default full packaged smoke also includes this matrix, exercising the runner's original working-directory behavior alongside explicit checkout paths. The installation guide provides the [worktree setup procedure](installation.md#verified-terminal-client-worktree-setup). Desktop, Claude-managed `--worktree` sessions, loaded session instructions and authenticated agent tool use remain untested by this fixture. The observed inheritance is version-specific; no runtime registration redesign or global server was introduced.

Current documentation was checked on 2026-10-25. Context7 retrieved the official [Claude MCP commands/scopes](https://code.claude.com/docs/en/mcp), [MCP quickstart](https://code.claude.com/docs/en/mcp-quickstart) and [configuration isolation guidance](https://code.claude.com/docs/en/debug-your-config). The official [Claude worktree guide](https://code.claude.com/docs/en/worktrees) describes shared project behavior, but this private-registration selection claim comes from the native fixture. OpenAI's [MCP configuration guide](https://learn.chatgpt.com/docs/extend/mcp?surface=cli), [configuration precedence/trust guide](https://learn.chatgpt.com/docs/config-file/config-basic) and [CLI MCP command reference](https://learn.chatgpt.com/docs/developer-commands?surface=cli) document project configuration, trust and JSON discovery commands. The installed official MCP SDK 2.2.0 XML documentation supplies the working-directory, environment and transport lifecycle APIs used by the separate protocol probe.

Final local validation passed the Release build with zero warnings/errors, all 196 regression tests without skips (`tests/WorkLog.Tests/TestResults/<account>_<host>_2026-10-25_18_15_28.trx`), targeted formatting verification and strict OpenSpec validation. Full packaged smoke passed under `artifacts/smoke/6df1811ac385445cba1e0d7b5841b4a3/`, repeating the eight-stage worktree matrix with the same results alongside existing client/instruction, journal/migration, ticket/context and alias checks. Bootstrap protocol/EOF/offline-status acceptance passed under `artifacts/update-smoke/510a11a3d90d4450a714566e46f69295/`. The 68,897,144-byte review ZIP is `artifacts/releases/worktree-startup-20261025/worklog-0.1.1-win-x64.zip`, SHA-256 `33fa58c32c1636d58bdf53c81bdc7141ba31a77b51beb6f2125c6077438480dc`; it includes the updated installation guide. This remains a local unreleased review package. No runtime implementation, installed application, real client configuration or journal schema was changed by this verification.

## Day digests (`add-day-digests`, server side)

> Superseded in part: the statements below that a digest is requested from the second observation and that `save_digest` takes only a text of at most 280 characters describe the earlier release. See "Stand-up-ready digests (standup-ready-digests)" for the current behavior.

`log_work` may now return an optional `digestRequest`, and a third tool, `save_digest`, stores the agent-written day digest. The tool contract is client-neutral, so every check below covers Claude Code and Codex alike; the managed instruction paragraph is shared text in `AgentInstructions.Render` and is checked for both clients' owned blocks in the unit tests.

| Check | Result |
| --- | --- |
| `DigestStoreTests`: create on first use with `user_version` 1, replace on second save, two concurrent writers, missing store read as empty without creating it, newer store version unavailable for the process | passed |
| `journal.db` `user_version` after digest use | 2 (asserted in `DigestStoreTests` and in the packaged smoke); the shared-data contract is unchanged |
| `DigestTests` (`log_work`): request on the second observation with entries oldest first; none for a first observation, a retry replay, another item, another day, or 31 observations; Europe/Warsaw midnight boundary; a broken digest store (a directory named `digests.db`) and a newer store version leave the logging result and `query_work` intact | passed |
| `DigestTests` (`save_digest`): saved digest, line breaks normalized, stale set after a later observation, newer set replaces, mixed items, mixed days, unknown ID, empty and over-long text, duplicate IDs, repeated identical save leaves one digest, unavailable store explained | passed |
| `DigestContractTests` over a stdio server: three tools; `save_digest` annotations (not read-only, not destructive, idempotent, closed world) and input schema `{entryIds: string[], text: string}`; `log_work` and `query_work` inputs unchanged and `log_work` output gains only the optional `digestRequest`; schema-valid results; stale and unknown-field rejections | passed |
| `InstructionOptionsTests`: Claude Code (`CLAUDE.local.md`) and Codex (`.codex/config.toml`) owned blocks written by `init` both contain the same digest paragraph and differ only in the agent name; `--no-instructions` removes it from both | passed |
| `worklog init --dry-run --clients both` on a disposable repository with a disposable `--data-dir` | reports managed instructions enabled (planned) for `claude` and `codex` and writes nothing; dry-run output does not print the instruction text, so the text itself is covered by the unit test above |
| `DailyDigestResponseTests`: `POST /api/daily` returns `digests` for items of the selected day and of today, leaves an unrelated item out, and returns an empty list for a missing or unreadable store | passed |
| `dotnet run --project tools/WorkLog.Smoke -c Release --no-build -- <packaged worklog.exe>` (default isolated run, no live agents) with a locked-restore Release build and `./scripts/package.ps1` output | `SMOKE PASSED`; the new `digests` stage in a disposable data directory logged two observations (digest request with both entries), saved a digest, logged a third (request over three), rejected a repeated save of the first two as stale, and found `journal.db` `user_version` 2 and `digests.db` `user_version` 1. Evidence: `digests/evidence.json` under the smoke run directory |
| `./scripts/package-update-fixtures.ps1` (launcher and mixed-version acceptance) | passed after the health probe accepted additional tools (see below) |
| `HealthProbeToolListingTests` (task 2.4): the relaxed update health probe accepts the two journal tools alone, accepts an additional tool with an output schema, rejects a listing missing `log_work` or `query_work`, and rejects a tool without an output schema | 5 of 5 passed |
| `dotnet test -c Release --no-build --no-restore` | 406 passed, 0 failed (final run of task 5.3, below) |

`PayloadInstaller`'s post-install health probe used to require exactly two tools. From this release it requires `log_work` and `query_work` plus an output schema on every tool and accepts additional tools, so later tool additions update normally (`HealthProbeToolListingTests`, above). Releases v0.2.0 and v0.3.0 keep the exact-two rule in their own `worklog update` and decline a candidate that lists `save_digest`. Design Decision 7 resolves this as a one-time manual installation for users on v0.3.0 or earlier, documented in `docs/releases.md` and `docs/installation.md`; the smoke checks that asserted exactly two tools now assert the current listing while still requiring both journal tools. No workaround for the old probe was attempted.

### Live-agent evaluation (dropped)

The planned live-agent evaluation of digests was dropped by user decision after the smoke tool's isolated client profiles proved unauthenticated (Codex CLI: 401 Unauthorized; Claude Code: "Not logged in"; no model turn ran and no cost was incurred). Real-client behavior with digests (whether the clients follow the managed instructions and the `digestRequest`, and the quality of the text) is therefore unverified; see design Decision 6. The server side is covered by the isolated tests and smoke above. No live-agent scenario or mode exists for digests; the pre-existing `--live-agents` scenario is unchanged.

### Full validation set (task 5.3)

Recorded in one run on 2026-11-02 on Windows 10.0.26300.0 (.NET SDK 10.0.114, selected by `global.json` with `latestPatch`) from the working tree on top of `5a73ab9`, with a fresh package under `artifacts/releases/`. The three `packages.lock.json` files (`src/WorkLog`, `src/WorkLog.Launcher`, `tools/WorkLog.LauncherProbe`) were refreshed for the SDK patch (`Microsoft.NET.ILLink.Tasks` 10.0.13 to 10.0.14, nothing else changed) so that the locked restore passes. The Git Bash failure of the launcher fixtures noted in earlier rounds on 2026-10-29 was not rerun here.

| Command | Result |
| --- | --- |
| `dotnet restore --locked-mode` | passed |
| `dotnet build -c Release --no-restore` | passed, 0 warnings, 0 errors |
| `dotnet test -c Release --no-build --no-restore` | 406 passed, 0 failed |
| `npm ci --prefix dashboard --no-audit --no-fund`, `npm test --prefix dashboard` | 487 packages installed; 245 of 245 tests in 18 files passed |
| `./scripts/package.ps1 -OutputDirectory artifacts/releases/<new folder>` | passed (version 0.3.0) |
| `npm run test:e2e --prefix dashboard` with `WORKLOG_DASHBOARD_EXECUTABLE` = the packaged `worklog.exe`, `WORKLOG_DASHBOARD_NO_NODE=true` | 39 of 39 passed |
| `dotnet run --project tools/WorkLog.Smoke -c Release --no-build -- <packaged worklog.exe>` (default isolated run, no live agents) | `SMOKE PASSED` |
| `pwsh ./scripts/package-update-fixtures.ps1`, started from PowerShell | `LAUNCHER PASSED`, `MIXED-VERSION PASSED` |

Packaging and the fixtures rewrote no lock file beyond the refresh above.

## Optional digest failure diagnostics (improve-dashboard-reading-and-reliability)

Optional digest work happens after the observation write and must not turn a saved observation into a failed `log_work` result. `McpServerHost` supplies the existing `DigestService` diagnostic path through the stdio server's stderr logger. Diagnostics use bounded fixed categories and safe messages, with at most one diagnostic per unchanged category per minute, and do not include observation text, private settings, or raw exceptions. A diagnostic sink exception is contained. The tool schemas are unchanged and stdout remains protocol-only.

| Check | Result |
| --- | --- |
| Shared-server protocol fixtures labeled for Claude Code and Codex | Passed. Both labels exercise the same MCP server protocol path; these are fixture checks, not native-client connection or live-agent evaluations. |
| Saved observation when optional digest processing fails | Passed; the write remains successful and available to subsequent queries. |
| Diagnostic suppression and sink failure | Passed; repeated unchanged categories are coalesced and a throwing sink does not fail the tool call. |
| Protocol stdout | Passed; successful calls remain parseable as MCP messages while diagnostics are written to stderr. |
| Independent backend QA | 72/72 checks passed. The separate code-analyzer review reported no production blocker. Its stderr-delivery test-maintenance suggestion was deferred by severity decision; it did not identify a production defect. |

The optional Daily response status is private and additive: missing or null `digestIssue` means no failure, while an unavailable digest read preserves observations and fallback summaries and produces one inline dashboard indication. The Daily API does not repair the store. Dashboard component and browser acceptance are recorded in [Dashboard validation](dashboard-validation.md#reader-navigation-and-bounded-comparisons-improve-dashboard-reading-and-reliability).

The final Release build completed with zero warnings and errors, the full .NET suite passed 427/427 without skips, and the dashboard unit suite passed 270/270 across 20 files. The fresh self-contained 0.4.1 review package passed the full no-Node dashboard browser suite, 44/44 in 2.3 minutes, with no declared-store exclusion; the full normal Release browser suite passed 44/44 in 4.8 minutes. Source version 0.4.1 remains unchanged and this review candidate was not published. The packaged `--updates-only` smoke passed `BOOTSTRAP PASSED`, checking launcher/version, MCP protocol/EOF and offline status. QA-020 regression coverage verifies that a document 404 and read-state storage 503 produce neutral refresh/retry guidance while retaining successful marks and drafts. These checks do not turn the paired shared-server protocol fixtures into native-client or live-agent evidence. See this change's review reports and completion ledger for the current integrated QA and code-analyzer disposition.

## Realtime dashboard update MCP parity fixtures (`realtime-dashboard-updates`)

The live-dashboard change does not alter MCP tool schemas. Its compatibility check uses the same two isolated stdio fixtures, labelled Claude Code and Codex, against both the preserved v0.4.1 package and the current Release executable. Run after the Release build and coordinated acceptance slot:

```powershell
node scripts/c06-mcp-compat.mjs
```

The script verifies the preserved executable SHA-256 `cf11ef5faae749ca76259ee05745ed8f4e8aa42590bd3a0f4f8c951e5d5e7f46`, runs each fixture against baseline and current executables, enforces a 10-minute default deadline (bounded to 1–20 minutes), and checks that neither executable changed during the run. Raw results are written under ignored `artifacts/realtime-dashboard-updates/mcp-compat/`. These fixtures establish protocol-level parity only; they do not represent authenticated Claude Code or Codex sessions.

A historical four-fixture run on 2026-11-09 passed dashboard and digest operations for baseline/current executables under the Claude Code/Codex fixture labels. Its raw record is `artifacts/realtime-dashboard-updates/mcp-compat/2026-11-09T08-52-11-830Z-839b460f.json`. A later independent review found the recorded per-child signal/error metadata still read from the global Node process in that runner version. Treat that run as historical; the corrected runner has since been executed separately.

The corrected runner passed all four fixtures on 2026-11-09 in `artifacts/realtime-dashboard-updates/mcp-compat/2026-11-09T15-51-32-139Z-57010dca.json`. All 12 owned wrappers exited with code 0, no signal and no timeout; each preserved/current executable passed dashboard MCP and digest checks under both protocol labels. Baseline and current executable hashes matched before and after the run. The current apphost SHA-256 was `1d1316d4af203c7a20feba4e3abebcd9ed1ccc6a8d4e08b051d2bd26c6b47fb9`, paired with managed `worklog.dll` SHA-256 `37dc9e451ec42b94a0a58ef89679566fcc41ebd5ff489852a3f1ca2d57445f84` and `WorkLog.Smoke.dll` SHA-256 `f9650189267bdbd8dfb6d9226d1ed91b4d0db432847536d49e99417531c8b749`. The preserved package SHA-256 remains `cf11ef5faae749ca76259ee05745ed8f4e8aa42590bd3a0f4f8c951e5d5e7f46`, and the corrected runner SHA-256 is `dd007a7e664720cd8fcb2f32e0e6717f5c3471ed055b284b81e1e56dc7bc2484`. The ignored paired identity manifest is `artifacts/realtime-dashboard-updates/mcp-compat/dev027-identities-20261109.json`. These results establish protocol-fixture compatibility only; no authenticated native Claude Code or Codex sessions were run.

## Stand-up-ready digests (`standup-ready-digests`)

Server-side evidence. This section supersedes the "second observation" and text-only statements in the day-digest sections above. Both clients use the same MCP server and the same `LoggingGuidance` constants, so the results apply to Claude Code and Codex alike; no live agent was run.

- **Request from the first observation.** `DigestService.Request` asks for a digest from an item's first observation of the day. A retry replay, an unlinked entry, more than 30 observations and an unavailable store still give none (`DigestTests`).
- **Structured `save_digest`.** The input is `entryIds` (1 to 30), `done` (1 to 4), `next` (0 to 3) and `blockers` (0 to 2) bullets of at most 120 characters after trimming and replacing line breaks, or the legacy `text` (at most 280), which is stored as the readable text only and shown by the dashboard as the only Done bullet. Exactly one form is required; both or neither fail without writing. The content rule is stated once, on the `done` input description. Stale, unknown, mixed-item and mixed-day sets are still rejected. The stored `text` is the derived `Done: a; b. Next: c. Blockers: d.` form.
- **Schema shape.** Only `entryIds` is required. The optional `done`, `next`, `blockers` and `text` are advertised as nullable types (`["array","null"]`, `["string","null"]`). `UnknownFieldValidation` allows those keys and rejects any other. The `SavedDigest` output is unchanged. The over-stdio contract test saves a structured digest and rejects a call with both forms.
- **`digest_parts` compatibility.** The additive `digest_parts` table is created if absent and `user_version` stays 1, so a v0.4.1 process keeps reading and writing `digests` without being disabled. A structured save writes both rows in one transaction. A read returns the parts only when `digest_parts.covers` equals `digests.covers` and `digest_parts.saved` equals the `digests` row's `saved` stamp (the earlier text comparison was removed); an older process that rewrites the digest, even with the same covers, therefore supersedes the parts and its own text is shown. A legacy `text` save writes no parts row, and removes earlier parts of that item and day, and repeated identical saves leave one `digests` row and one `digest_parts` row. The dashboard's read-only reader copes with a store that has no `digest_parts` table.
- **Daily API.** `DailyService` returns each digest as `{ covers, text, done?, next?, blockers? }` in camelCase, with the lists omitted unless the parts match.

| Test class | Passed |
| --- | --- |
| `DigestTests` | 29 passed |
| `DigestContractTests` | 3 passed |
| `DigestStoreTests` | 16 passed |
| `LoggingGuidanceTests` | 29 passed |
| `InstructionOptionsTests` | 19 passed |
| `McpTests` | 3 passed |
| `DailyDigestResponseTests` | 4 passed |

Final run on 2026-11-12 (Release build, 0 warnings): the full .NET suite passed 500/500. The packaged update smoke (`--updates-only`) was not run for this section.

## Sources

- [Official C# SDK: tools, structured content, errors and annotations](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/tools/tools.md)
- [Official C# SDK: request filters](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/filters.md)
- [Official C# SDK: stdio server setup and stderr logging](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md)
- [MCP tools specification](https://modelcontextprotocol.io/specification/2025-11-25/server/tools)
- [System.CommandLine parsing and invocation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-parse-and-invoke)
- [System.CommandLine validation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-customize-parsing-and-validation)
- [Windows process attribute lists, including inherited handle and job lists](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute)
- [Windows job lifetime limits](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_limit_information)
