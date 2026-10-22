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

Review follow-up on 2026-10-22 fixed legacy `bugfix` normalization across settings refresh, copy and adoption. Nine new test cases cover save, first-use/existing repository resolution, copies, and case-insensitive custom mappings including `unclassified`; the initialization regression also now uses legacy branch settings. Five new cases failed before the fix. After the fix, the Release build passed with zero warnings/errors, all 158 tests passed (`tests/WorkLog.Tests/TestResults/ezagv_LVIV_2026-10-22_21_43_23.trx`), and formatting verification passed. The history fixture now writes legacy JSON directly instead of relying on current `Save()` to emulate an older writer. The packaged acceptance evidence above predates this follow-up; those packages were not rebuilt for this settings-only fix.

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

## Sources

- [Official C# SDK: tools, structured content, errors and annotations](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/tools/tools.md)
- [Official C# SDK: request filters](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/filters.md)
- [Official C# SDK: stdio server setup and stderr logging](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md)
- [MCP tools specification](https://modelcontextprotocol.io/specification/2025-11-25/server/tools)
- [System.CommandLine parsing and invocation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-parse-and-invoke)
- [System.CommandLine validation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-customize-parsing-and-validation)
- [Windows process attribute lists, including inherited handle and job lists](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute)
- [Windows job lifetime limits](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_limit_information)
