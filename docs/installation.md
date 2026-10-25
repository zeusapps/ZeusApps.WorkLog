# Windows installation and upgrades

1. Download `worklog-VERSION-win-x64.zip` and its `.sha256` file from [zeusapps/ZeusApps.WorkLog Releases](https://github.com/zeusapps/ZeusApps.WorkLog/releases).
2. Compare `Get-FileHash .\worklog-VERSION-win-x64.zip -Algorithm SHA256` with the checksum file.
3. Extract into a stable directory such as `%LOCALAPPDATA%\Programs\WorkLog` and add that directory to your user PATH, or invoke worklog.exe by its full path. Keep data outside that installation directory.
4. Run `worklog --version`, then `worklog init --repository D:\Code\MyProject --dry-run` to review setup.
5. Run the same init command without `--dry-run`. Restart Codex/Claude Code and use their normal trust prompts. Init never grants trust or changes permission policies.

Windows x64 is the supported release target. The packaged executable includes .NET and SQLite native dependencies. Building from source requires the SDK selected by global.json, Git, and NuGet access.

## Private client integration

`worklog init` initializes the current Git repository; `--repository` selects another. `--clients both|codex|claude` defaults to both. Both client CLIs must be installed when selecting both; minimum tested versions are Codex 0.162.1 and Claude Code 2.1.289.

- Claude Code: local-scope MCP registration under the project path in your private `~/.claude.json`. With managed instructions enabled, a block in `CLAUDE.local.md` and explicit imports preserve existing ancestor AGENTS.md instructions.
- Codex: a private `.codex/config.toml` registration and, when enabled, additive `developer_instructions`, preserving existing user/project instructions. This does not replace model base instructions.
- `.worklog/integration.json` records integration ownership/version. Generated project files are excluded via local Git info/exclude, including worktree-aware paths. Team CLAUDE.md, AGENTS.md, .mcp.json, .gitignore, and OpenSpec skills/configuration are not edited.

An already tracked private target that init would modify, malformed settings, or an unowned worklog server registration is a conflict: init reports it without overwriting it. An untouched tracked CLAUDE.local.md is allowed when instructions are disabled and no owned block needs removal. Do not untrack team settings automatically; resolve the conflict or select the other client. Init backs up changed existing content under the data directory's integration-backups folder. Partial failures list applied steps; fix the problem and rerun with the intended options. Keep backups private because client settings can contain personal information.

Init can be rerun after an application upgrade to update managed instruction blocks. Preserve the ownership manifest. Journal data is shared across initialized repositories on this machine only. Repositories without origin receive a persisted local identity; explicitly set identity in private config when independent checkouts should refer to the same repository. Use distinct OpenSpec change names for new work rather than recycling completed names.

### Windows Claude project-path aliases

On Windows, init reconciles existing Claude project entries whose full paths match the selected checkout after slash normalization and case-insensitive comparison. It updates the owned `worklog` registration under those existing spellings and Git's root spelling, preserving each entry's other settings and servers, including `worklog-legacy`. It does not copy trust settings between entries. A malformed entry, an unowned `worklog`, or an owned command changed outside WorkLog stops initialization before any setup changes are applied; the error names the conflicting project key. Preserve the ownership manifest and resolve that specific conflict before retrying.

Preview the affected project keys, apply, and restart Claude normally:

```powershell
worklog init --repository D:\Code\MyProject --clients claude --dry-run
worklog init --repository D:\Code\MyProject --clients claude
```

Omitting instruction options preserves the saved preference, including `--no-instructions`. The [recorded Windows acceptance](mcp-validation.md#windows-claude-project-alias-validation) verifies terminal-client discovery and connection from mixed-case and lowercase launch paths. Init reconciles known entries; it does not generate every possible spelling. If Claude later creates another spelling, rerun init to reconcile it. Matching remains case-sensitive outside Windows. Filesystem links and case-sensitive Windows directory configurations are outside this alias rule. For a distinct worktree, follow the [verified setup procedure](#verified-terminal-client-worktree-setup); inherited discovery differs between clients.

### Existing applications and ticket context

For an application that already has logging rules, review and apply this setup:

```powershell
worklog init --repository D:\Code\MyProject --clients claude --no-instructions --ticket-provider jira --ticket-scope company.atlassian.net --dry-run
worklog init --repository D:\Code\MyProject --clients claude --no-instructions --ticket-provider jira --ticket-scope company.atlassian.net
```

`--no-instructions` removes only WorkLog-owned blocks, including imports inside those blocks, and remembers the disabled mode per repository and selected client. MCP registration stays enabled. It creates no Claude instruction file or imports, and does not copy global Codex instructions into new local instructions. Existing files, unrelated instructions, and empty values left by removal are retained. Later init runs, including upgrade refreshes, preserve the opt-out. `--instructions` explicitly restores one managed block and saves enabled mode; the two flags cannot be combined. Clients without a saved preference default to enabled.

The ticket flags must be supplied together. Providers are `jira` and `azure-devops`; scopes are a Jira site or Azure organization/project. Values are trimmed, and blank/unsupported/incomplete values fail before writes. Omit both flags on later runs to retain context. A custom ticket extraction pattern is preserved; Azure numeric branch identifiers need a suitable pattern in the active shared context, or explicit ticket references. Restart client/MCP processes after setup so they load the new configuration. Existing unresolved observations stay unresolved but become searchable by bare ticket ID; qualified queries still require a stored qualified reference.

### Worktree context and legacy configuration

Init and server startup resolve the common Git directory to share one identity, ticket context, and per-client instruction preferences across worktrees. The first initialized checkout can be either the main checkout or a worktree. Separate clones retain their own settings. Client discovery in a fresh worktree is a separate concern: explicit init in that worktree configures its private registration; universal automatic discovery is not promised here.

On first resolution, compatible legacy `repositories` records are promoted into `repositoryContexts` without deleting the old records. Missing provider/scope values do not erase known settings, and adding an origin does not replace a saved local identity. Removed or inaccessible checkout records remain untouched. Once promoted, edit the shared entry's `repository` values; its `legacyPaths` records which old copies are superseded. Instruction mode is stored in that entry's `instructions` map. Use init flags to change mode so file cleanup happens too.

Different nonempty identities, scopes, providers, or extraction patterns in member records stop promotion with the conflicting paths/field. Back up config.json, identify the intended identity and ticket context, reconcile those legacy records explicitly, and rerun init. Keep journal.db intact: this change does not rewrite existing observations or regroup earlier worktree identities. Do not resolve an identity conflict by blindly replacing every saved identity with a remote-derived one.

Changing instruction preference updates files only in the selected checkout. Previously initialized sibling worktrees keep their existing blocks until init next runs there, at which point the shared preference applies. Restart affected sessions to discard already loaded instructions.

For a configuration rollback, stop affected clients and restore the corresponding private config/client files from backups together before using an older binary. Retain a copy of the new settings and journal; an older binary will not apply the new shared-context behavior. No journal schema migration is introduced by these fixes.

### Verified terminal-client worktree setup

For each new worktree, preview and apply explicit initialization using the same WorkLog launcher and data directory as the main checkout:

```powershell
& D:\Apps\WorkLog\worklog.exe init --repository D:\Code\MyProject-feature --data-dir D:\Private\WorkLog --clients both --dry-run
& D:\Apps\WorkLog\worklog.exe init --repository D:\Code\MyProject-feature --data-dir D:\Private\WorkLog --clients both
```

Select `--clients claude` or `--clients codex` if only one is installed. Preserve the main checkout's ownership manifest and any existing worktree manifest. Omit ticket and instruction options to retain the shared context and saved preferences. Init writes checkout-local setup; it does not copy the main checkout's generated files into the worktree. Restart affected clients normally and follow their repository trust flow. In Codex, an untrusted project does not load its project configuration; the acceptance fixture grants trust only inside its disposable profile.

The [2026-10-25 acceptance matrix](mcp-validation.md#terminal-client-worktree-startup-validation) tested a manually created external Git worktree with Claude Code 2.1.310 and Codex CLI 0.164.0 on Windows. Claude discovered and connected through the main checkout's private registration before worktree init, and continued selecting that main registration afterward. Codex discovered no WorkLog registration before init, then discovered its worktree registration afterward. Both preserved shared repository identity, qualified ticket context and journal history; main-checkout discovery and connections still worked after setup.

If WorkLog is missing, run init for that worktree, confirm its trust/configuration, and reconnect. A discovered server that fails to connect requires checking its command, arguments and stderr separately. `claude mcp get worklog` provides native connection health; `codex mcp list --json` reports discovery/configuration, not proof of connection. A stdio server starts when its client connects; these results do not require a machine-wide background service. Terminal health checks do not verify loaded session instructions or authenticated agent tool use, and do not establish Desktop or Claude-managed `--worktree` behavior. Other client versions may differ.

## Versioned installation

The root `worklog.exe` is a stable self-contained launcher. `current.json` selects a payload under `versions/VERSION/`; `launcher.json` records the independently versioned launcher contract. Each payload has an immutable `release.json` with compatibility declarations and file hashes. Keep the entire extracted layout together and keep private data outside it. Client registrations use the launcher even when init runs directly from a retained payload.

New packages require Windows x64 with the .NET 10 supported OS baseline and Windows job-list process attributes (Windows 10 or newer). They include their runtimes; no separately installed .NET is needed. The launcher preserves arguments, working directory, environment, standard streams and exit status. Stdio diagnostics remain on stderr. Its child job ties server lifetime to the client-owned launcher; normal stdin closure shuts the server down, and forced launcher termination does not leave its payload detached.

## One-time migration from the legacy layout

Pre-launcher v0.1.1 and earlier replace application files in place. They cannot establish this layout while keeping their old installation running. The owner accepts a one-time maintenance window; a separate legacy status-reader race fix is not shipped as a prerequisite.

1. Stop Codex/Claude sessions and all WorkLog processes using the old installation. Keep the old application and private configuration for recovery; keep a consistent journal backup, following the database guidance below.
2. Download and checksum-verify the new bootstrap ZIP. Extract into a clean stable installation directory, preserving its launcher, metadata and version folders. Do not overlay it onto a legacy/partial directory or replace the private data directory. If the stable path must remain the same, preserve the old application elsewhere first while it is stopped.
3. Run the new launcher with `--version`. Preview `worklog init --repository PATH --data-dir DATA --dry-run`, then apply the same command without `--dry-run` for each intended repository/client. Preserve `.worklog/integration.json` so init can prove ownership; an unowned registration is a conflict, not permission to overwrite it. Omitted ticket/instruction options preserve existing settings and opt-outs.
4. Restart clients normally, follow their trust flow, and verify the saved repository identity, existing observations, stable launcher registration and instruction preferences. This migration does not claim to preserve an old live conversation.

Changing the launcher or shared-data contract in the future uses another explicit maintenance installation. Supported mixed-version behavior begins with the first launcher-based release; keeping a legacy server running beside it is outside that contract.

## Compatible updates and application rollback

```powershell
worklog update --check
# Existing conversations can keep running for a compatible payload update.
worklog update
worklog update --status
# Explicitly select the previous verified compatible payload if needed:
worklog update --rollback
```

`--check` reports running, selected and published candidate versions without downloading a package. A release candidate is not yet a compatibility approval. `--status` reads local selection/outcomes offline. `--rollback` is also offline. These flags are mutually exclusive. All modes accept `--data-dir PATH`; use the same directory for cache and operation receipts. Selection belongs to the installation, so another custom data directory still launches the selected version, while journals remain separate.

Plain update verifies the official ZIP/checksum, bounded nested archive paths, payload file hashes, compiled version/runtime and launcher/shared-data contracts. A disposable MCP health probe uses separate synthetic data. Ordinary updates require package format 2, launcher contract 1, shared-data contract 1 and journal schema 2. Incompatible declarations, legacy/source layouts, overlapping data/install paths, unsafe links, partial conflicting payload folders or permanent access failures cause actionable refusal. No ordinary update migrates private data, replaces the launcher or terminates sessions.

`staged` means verification completed but selection has not committed. The command then serializes publishers, rechecks the original selection generation, publishes a complete immutable payload and atomically selects it. `activated` means new processes use that selection. Existing MCP servers keep their PID/version/connection and continue logging and querying; they adopt the new code at their next normal reconnect/restart. Rerun init when owned instructions need refreshing. Installation does not imply that every live conversation is running the new version.

Concurrent status commands do not block replacement of a running executable because ordinary activation replaces no running executable. A competing publisher waits at most 30 seconds, with cancellation. A stale operation cannot select a lower version or silently overwrite an intentional rollback. Status reconciles receipts with the committed selection; an interrupted final receipt cannot undo a completed activation. Failed operations preserve the prior valid selection and retained application files, with actionable recovery details.

Rollback verifies and selects the recorded previous compatible payload for new processes. It preserves journal/configuration data and existing server connections, and refuses a missing, corrupt or incompatible target. The first release retains all published payloads, including unused ones. Expect disk use to grow; automatic cleanup is deferred. Perform any manual payload maintenance only while relevant processes are stopped, preserving current/previous/recovery versions. Never remove folders merely because a process scan happens to look idle.

Automatic checks run independently using the fixed public stable-release feed, without credentials or project/journal data, at most once per 24 hours per data directory. Valid cached notices expire after seven days. Successful tools can include `updateHint` with running `installedVersion` and selected `selectedVersion`. A cached candidate already selected is suppressed even in an older server. Managed instructions ask assistants to mention an offer once per version per session and explain activation versus adoption; a hint authorizes no installation or process termination. Notice presentation remains assistant-dependent.

To disable automatic checks/notices, merge `"updates": { "enabled": false }` into private config while preserving other settings and restart affected servers. Explicit update commands still work. Supported settings writers coordinate current-state merges and preserve unknown nested fields; live reads otherwise retain their startup configuration snapshot. Cache, download staging and receipts live under the private data directory's `updates` folder. Payload publishing/recovery metadata stays in the installation. SHA-256 detects corruption; authenticity also depends on HTTPS and control of the official repository.

## Selection recovery and database restore

Malformed/missing selection metadata or a missing/tampered selected payload makes the launcher fail on stderr rather than execute an arbitrary fallback. If its application cannot start, its update CLI is unavailable too. Stop affected clients for manual recovery, retain the damaged metadata and payloads, verify a known-good retained payload's manifest/file hashes, then restore the verified `current.previous.json` selection copy as `current.json`, or reinstall a checksum-verified bootstrap package into a clean directory. Keep the data directory unchanged. Rerun version/status checks and owned init as needed before restarting. Do not invent hashes, edit contracts to bypass validation, or restore journal data as part of application-selection repair.

Journal upgrades run transactionally. Before upgrading an existing schema, WorkLog creates a consistent `journal.db.vN.TIMESTAMP.bak` in the data directory. A binary encountering a newer unsupported schema refuses to migrate it.

To roll back a schema upgrade: stop **all** WorkLog processes; keep a separate copy of the current journal and any `-wal`/`-shm` files for recovery; restore the chosen consistent `.bak` as `journal.db` in a clean data directory with no stale WAL/SHM files; copy the private config there; run the older executable with `--data-dir` pointing to that restored directory. Entries recorded after the backup are absent from that restored snapshot. Reinitialize client registrations if the data-directory path changes. Never copy only a live database file while its WAL may contain committed work.

## Troubleshooting

Run `worklog --help` for command syntax. Diagnostics go to stderr so MCP stdout remains valid protocol traffic. Verify client versions, executable path, repository trust, and private config syntax if tools do not appear. Use an explicit `worklog serve --repository D:\Code\MyProject --data-dir D:\Private\WorkLog` command in a stdio MCP client to isolate startup issues. A server without repository context requires explicit repository identities in logging calls.
