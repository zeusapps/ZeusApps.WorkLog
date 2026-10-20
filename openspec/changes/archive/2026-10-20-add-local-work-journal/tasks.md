# Tasks

## 1. Application foundation

- [x] 1.1 Create the .NET 10 solution, executable and test projects, pin SDK/dependencies, and add init/serve/--version dispatch; verify restore/build and command help/version output.
- [x] 1.2 Implement per-user data paths, explicit data-directory override, private configuration, and repository identity registration with sanitized remotes and local fallback IDs; verify isolated temporary-directory tests and credential removal.

## 2. Durable work journal

- [x] 2.1 Implement the initial SQLite schema, version checks, transactional migration runner and consistent pre-migration backups; verify fresh creation, supported upgrade, rollback after failed migration, and unchanged rejection of a newer schema.
- [x] 2.2 Implement validated activity persistence and retrieval of complete stored entries; verify statuses, optional metadata, offset timestamps, restart persistence, ticket-only work, and change-only work.
- [x] 2.3 Implement idempotency keys, conflicting-reuse errors and atomic initial-start uniqueness; verify retry behavior and simultaneous writes from two connections/processes without duplicate starts.
- [x] 2.4 Implement configurable prefix classification and repository ticket extraction with explicit override precedence; verify feature/bugs/hotfix defaults, unknown prefixes, historical snapshots, unqualified extracted keys, Jira scopes and explicit Azure DevOps references.

## 3. Structured reporting and MCP

- [x] 3.1 Implement explicit period validation and optional filters; verify inclusive/exclusive bounds, occurrence versus recording time, invalid ranges, empty results, and each filter.
- [x] 3.2 Implement schema-versioned entries and daily work grouping with deterministic ordering; verify Europe/Warsaw daylight-saving boundaries on Windows, multiple statuses per day, feature switching, and absence of invented activity.
- [x] 3.3 Expose log_work and query_work through the official C# SDK stdio server with repository context; verify protocol discovery and end-to-end logging/querying with an MCP client, including clean stdout and structured errors.

## 4. Private initialization and client adapters

- [x] 4.1 Implement initialization planning, client selection, dry-run, owned-content manifest, atomic file replacement and failure reporting; verify no-write dry-run, malformed-config preservation, interrupted setup, and repeatable reruns.
- [x] 4.2 Implement private Git exclusions with worktree-aware paths and tracked-target checks; verify generated local paths stay untracked while shared files and unrelated exclusions remain unchanged.
- [x] 4.3 Implement Claude Code local-scope MCP registration and managed CLAUDE.local.md instructions; verify existing settings/content survive reruns and AGENTS.md discovery remains effective with the supported client.
- [x] 4.4 Implement Codex private .codex/config.toml MCP registration and additive managed developer instructions; verify preserving unrelated TOML, existing user/project instructions, server-name conflicts, and actual loading in the supported client.
- [x] 4.5 Render common session logging guidance for both clients; verify delivered instructions cover successful change creation, real progress, feature switches, contextual completion, delayed archive, retry keys, and visible logging failures.
- [ ] 4.6 Run live smoke checks with both clients against an isolated journal/repository; verify both discover tools and can log/query the same journal after initialization, with restart/trust requirements documented.

## 5. Versioned distribution

- [x] 5.1 Add self-contained win-x64 packaging, version metadata and SHA-256 checksum generation; verify a published executable runs without an installed runtime and its version matches package metadata.
- [x] 5.2 Add GitHub Actions pull-request and primary-branch validation for restore/build/tests and Windows publish smoke checks; verify workflow structure and run the equivalent commands locally.
- [x] 5.3 Add a version-tag release workflow gated by validation and tag/version agreement, with scoped publication permissions; verify packaging locally and inspect workflow conditions for failed checks and mismatched versions.
- [x] 5.4 Document Windows installation, init/client setup, mappings and ticket context configuration, period-query examples, stable-path upgrades and backup restore; verify all documented commands against the packaged executable.
- [x] 5.5 Run a release-candidate acceptance scenario spanning change creation, progress on Y, switch to Z, Z completion and later Y resumption; verify the resulting daily report contains only recorded work and upgrades preserve the journal.

## 6. Code organization review

- [x] 6.1 Split application entry, CLI parsing, MCP hosting, journaling, SQLite storage/migrations, reporting, configuration and repository handling into focused namespaces; verify the release build succeeds with zero warnings.
- [x] 6.2 Separate Codex/Claude integration adapters from initialization planning, private Git exclusions, managed blocks and instruction templates; mirror the organization in tests and smoke tooling, and verify all 21 existing tests pass.
- [x] 6.3 Document the architecture and verify the packaged executable with isolated smoke checks plus an original-versus-refactored comparison of MCP schemas, reports, retry identity and bidirectional journal reads.

## 7. CLI and MCP documentation review

- [x] 7.1 Validate C# MCP hosting, stdio, tool responses, annotations and errors against official documentation; record findings and add output schemas, structured content, accurate annotations and server identity, verified through SDK client tests.
- [x] 7.2 Replace manual CLI parsing/dispatch with System.CommandLine 2.0.13 command definitions, generated help and validation; verify invalid options cannot execute commands and host cancellation is forwarded.
- [x] 7.3 Verify text/structured response equivalence, recovery after validation errors, clean shutdown on stdin closure, and the packaged executable; all 32 automated tests and isolated package smoke checks pass.

- [x] 7.4 Describe response records and properties, verify descriptions appear in nested MCP output schemas, and rebuild the installed server for fresh-session validation; all 32 automated tests pass. Private Codex registration now points at .worklog/bin/0.1.0/worklog.exe; direct discovery against that executable confirms output descriptions.

## Verification status

Release build passes with zero warnings; 41 automated tests pass. The self-contained 0.1.0 package passes isolated Codex configuration/instruction discovery, Claude local-scope MCP connection, native structured tool results, feature-switch reporting, and journal-upgrade acceptance. SDK tests validate actual responses against advertised JSON Schemas, tool annotations, useful tool errors, continued operation after errors, and clean shutdown on stdin closure. Pagination tests cover all views, byte limits, timestamp ties and snapshot isolation. System.CommandLine tests verify command-specific help and validation before side effects. MCP documentation validation is recorded in docs/mcp-validation.md. The journal schema remains compatible; report schema version 1 deliberately changes the reporting contract.

Task 4.6 is partially verified: on 2026-10-20, the restarted Codex session discovered the actual WorkLog MCP tools, saved an in_progress entry for this change, and retrieved it through query_work with Europe/Warsaw daily grouping (entry a1a33342ca6a4400a1a9a359f523abfb). Claude Code live logging/query verification remains pending because the user currently has no active Claude Code access. Earlier automatic approval review rejected spawning authenticated live agent sessions; no such subprocess test was run. GitHub workflows are delivered but have not run on GitHub; no remote repository was created.

Archive decision (2026-10-20): after confirming validation passed, the user explicitly deferred live Claude testing and approved archiving. Task 4.6 remains unchecked to preserve the verification gap; it is not an archive blocker. Main capability specs are synced as part of archiving.

## 8. Independent MCP audit corrections

- [x] 8.1 Emit explicit nulls for required nullable result properties and validate real successful tool results against advertised schemas, including retries, full/minimal entries, empty reports, every view and continuation pages.
- [x] 8.2 Resolve inferred ticket context from the effective repository, preserve unresolved IDs without guessed scope, and reject conflicting configurations.
- [x] 8.3 Normalize ticket fields consistently, return indexed nested validation errors, reject nonnumeric Azure DevOps IDs and unknown input properties, and preserve exact retries of pre-normalization request fingerprints.
- [x] 8.4 Group ticket-only work by a stable sorted qualified ticket set independent of input ordering.
- [x] 8.5 Deliver user-approved compact paginated reports by default, explicit legacy/flat/grouped views, selectable summaries, SQL filters, snapshot continuation and a 64 KiB structured-payload limit; update design/specs/docs for report schema version 1.
- [x] 8.6 Rerun the isolated audit and package smoke checks, record verification, and refresh private Codex registration to .worklog/bin/0.1.0-audit/worklog.exe for fresh-session testing.
- [x] 8.7 Return actionable unknown-field paths before SDK argument deserialization; verify entry, nested ticket and period typos, no partial writes and continued connection usability. Correct the extracted-ticket description to document explicit nulls. All 41 tests pass.

Audit rerun artifacts: artifacts/mcp-audit-20261020/run-1792526091760 (44 probes; 19 successful response schemas checked for missing required fields, zero failures). Volume probe: 25 entries, 60,844 structured bytes and 122,890 total tool-result bytes including the JSON text fallback. Full schema evaluation is additionally exercised by the automated MCP tests. Packaged smoke artifacts: artifacts/smoke/7a583481f9d24f439b74583da0663c4e. Context discovery, auditable corrections, search and schema enums are follow-up scope.
