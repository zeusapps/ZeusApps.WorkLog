# Design

## Context

This repository contains generated OpenSpec integrations for Codex and Claude Code, but no application source, tests, CI, or durable capability specs. See proposal.md for motivation. The design is needed because persistence, concurrent MCP processes, private client setup, and release migrations introduce cross-cutting behavior.

## Goals / Non-Goals

**Goals:** One installable executable, predictable local data ownership, common logging semantics across clients, and explicit reporting facts.
**Non-Goals:** A background tracking daemon, timesheets/duration estimation, remote synchronization, issue-service clients, narrative generation, and modification of team OpenSpec workflows.

## Decisions

### Application and transport

Use C# on .NET 10 LTS, the official C# MCP SDK, and Microsoft.Data.Sqlite. Pin dependency versions during implementation. Keep one executable with init, serve, and --version; separate command orchestration, journal/query logic, and client configuration adapters within a small solution. Use stdio rather than HTTP to avoid a separately operated service. Protocol output alone goes to stdout; diagnostics go to stderr. Publish self-contained win-x64 initially; avoid Native AOT until dependency compatibility and a concrete benefit are established. Node/Python were considered; .NET matches the user's environment and runtime-independent Windows distribution.

### Component boundaries after code review

Keep the executable in one project with namespaces matching responsibility. Cli owns parsing/dispatch; Mcp owns transport and tool bindings; Journaling owns entry preparation; Persistence owns SQL, transactions and migrations; Reporting owns query validation and daily grouping; Configuration and Repositories own private settings and repository registration. Integration coordinates initialization through client-specific adapters and a shared InitializationPlan. Infrastructure contains the small shared serialization and file utilities. Program only handles process exit and errors.

Tests mirror these areas and share an isolated TestWorkspace fixture. The smoke tool separates client registration checks, journal acceptance, process execution, and explicitly opt-in live-agent checks. See docs/architecture.md for the component map. This refactor preserves CLI/MCP contracts, serialized configuration and journal schema; comparison against the original executable verifies schemas, report output, retry hashes and bidirectional entry compatibility.

### Local persistence and identity

Default to %LOCALAPPDATA%/ZeusApps.WorkLog with journal.db and configuration outside installation directories. Provide a data-directory override for isolation/testing. Use platform application-data APIs rather than hardcoded separators for future portability. Each MCP process opens the same SQLite database with foreign keys, WAL, bounded busy timeout, and transactional writes.

Store entries with GUID IDs, UTC occurrence and recording instants, original supplied offset, stable repository ID, branch snapshot, change name, ticket references, classification snapshot, optional summary/agent/session IDs, and optional idempotency key plus normalized request hash. Store repository registration and change-to-work identity separately from local checkout paths. Prefer a sanitized canonical origin identity, excluding embedded credentials; repositories without a remote receive a persisted local ID. Allow explicit repository identity to handle forks and different remote spellings. Branch is context, not a feature identity.

Use unique constraints for idempotency and the first started event per repository/change. Retry normalization excludes server-generated timestamps when omitted by the caller. Conflicting key reuse fails. A second initial-start request returns the first record; progress entries remain independent. Ticket-only work uses a qualified ticket as its work identity. When a change also has tickets, group by repository/change and preserve attached ticket references. Do not infer time spent between observations.

SQLite is preferred over JSON append files for concurrent agents, period filtering, and transactional schema upgrades. It remains entirely local.

### Metadata and configuration

Persist configurable prefix mappings and repository ticket-inference settings in private local configuration. Match the exact segment before the first slash using ordinal case-insensitive comparison; explicit classification wins. Default feature=feature, bugs=bug, hotfix=bug. Unknown or missing branches resolve to unclassified. Resolve once and store the result.

An optional repository extraction regex captures a ticket ID; provider/scope come only from explicit input or repository configuration. A default Jira-style key extractor can identify DEP-1232 but cannot establish its provider. Retain an unresolved extracted key separately if provider/scope are unknown. Azure DevOps references are accepted explicitly; numeric branch extraction requires repository configuration. No ticket network calls.

### MCP contract and reporting

After documentation validation, enable the C# SDK's UseStructuredContent option on both tools: advertise output schemas and return structuredContent while retaining JSON text for older consumers. Mark tools as local/closed-world, logging as additive/non-destructive but not universally idempotent, and querying as read-only/idempotent. Explicitly identify the server as worklog with its application version. The CLI uses System.CommandLine 2.0.13 with separate init and serve command definitions, generated help and validation, and cancellation passed to the server host. See docs/mcp-validation.md for findings and verification.

log_work accepts the entry fields specified in work-journal and returns the persisted entry plus created/existing outcome. Validate before beginning a write. Both repository context and timestamps can be supplied explicitly; init registration supplies repository context to serve so process working-directory assumptions are unnecessary.

query_work requires offset-bearing from/to timestamps and an IANA reporting timezone, normalizes bounds to UTC, and filters [from,to). Verify IANA handling on Windows in automated tests. Return schemaVersion=1, resolved bounds, timezone, all matching entries ordered by occurred time then ID, and daily groups. Groups contain ordered observations rather than only final status. Include no inactive dates or invented activity. Initial queries return complete results; pagination is deferred until journal sizes justify a contract extension.

### Private initialization

Use explicit per-repository initialization, defaulting to both clients. Build a complete mutation plan and validate destination formats/ownership before applying changes. Offer dry-run. Use atomic replacement per file, backups for changed existing files, and an installation manifest recording owned keys/blocks and installed integration version. On partial failure, report applied and unapplied steps and permit safe rerun; never claim success for both clients when one failed.

For Claude Code, register using its local MCP scope (private project registration in user configuration), preferably via its supported CLI, and maintain a marked block in CLAUDE.local.md. Preserve existing instruction discovery: if introducing CLAUDE.local.md would suppress AGENTS.md loading, use a local supported setting or explicit import to retain those instructions, verified against the supported client version.

For Codex, use private .codex/config.toml containing the worklog server registration and additive developer_instructions. Merge existing project developer instructions and preserve effective user instructions rather than silently shadowing them; use a managed block with provenance so updates replace only owned content. Do not use model_instructions_file, which replaces base instructions, or invent AGENTS.local.md behavior. Verify the supported client version loads these project settings and document trust requirements.

Exclude only generated private paths through git's local info/exclude (resolve with git rev-parse --git-path info/exclude for worktrees). Reject already tracked targets rather than changing tracked shared files. Do not edit .gitignore, shared agent documents, or generated OpenSpec skills. Scope private registrations to initialized repositories; availability of a server outside an initialized repository must not imply automatic logging there.

### Session integration semantics

Distribute a common instruction template rendered for each client. After successful change creation, log started, including creation through propose. On meaningful implementation/planning work or switching away after progress, log actual activity with the relevant branch and change. The current session decides whether to report in_progress or done; migrations and testing block done only when the session identifies them as required. No archive listener or automatic all-tasks-checked completion rule.

This is agent instruction integration, not a deterministic OpenSpec hook: the MCP cannot observe local workflow events by itself. The agent must report logging failures and preserve idempotency keys during retries. Multiple entries per feature/day are legitimate; reports retain the evidence.

### CI and releases

Implement GitHub Actions for pull requests and primary-branch pushes: restore, build, tests, and Windows publish smoke checks. A vMAJOR.MINOR.PATCH tag triggers validation and self-contained packaging, verifies tag/application version agreement, and publishes a ZIP plus SHA-256 checksums as a GitHub release. Include a manifest/version and installation instructions. CI has read permissions by default; release publication alone receives contents write. Do not require credentials for normal application operation.

Create workflows and documentation here; remote repository creation, naming/visibility, push, and release execution are separate deployment actions. macOS/Linux builds remain optional future work. Install to a stable executable location so an upgrade does not require re-registering every MCP client; rerun init to update instruction versions.

## Risks / Trade-offs

- Agent instructions can be missed or sessions terminated abruptly -> document the limitation, surface logging errors, and support explicit tool calls.
- Client configuration formats evolve -> pin/document tested client versions, validate support in init, and test adapters with fixtures plus live smoke checks.
- Private instruction files can change instruction discovery -> retain existing AGENTS.md behavior and validate loading in both clients.
- Concurrent writes or retries can duplicate events -> database uniqueness, request hashes, and concurrency tests.
- A locally complete journal is not a cross-machine aggregate -> make machine-local scope explicit in installation and reporting docs.
- A single started event cannot represent reused OpenSpec names -> initial version treats repository/change names as stable work identities; use distinct names for new work.
- Self-contained publishing includes the runtime -> publish updated releases when runtime servicing requires it.

## Migration Plan

1. First run creates the local data directory and initial versioned schema; init registers selected repositories.
2. Before schema changes, take a consistent SQLite backup, then apply migrations in a transaction with schema-version validation.
3. Reject unknown newer schemas without mutation; rollback means restoring the documented backup and previous executable.
4. Updating the executable preserves journal/configuration. Re-running init updates only managed integration content.

## References

- Official C# MCP SDK: https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html
- .NET single-file deployment: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- Codex configuration reference (developer_instructions and MCP configuration): https://developers.openai.com/codex/config-reference/
- Claude private instructions: https://code.claude.com/docs/en/memory
- Claude local MCP scope: https://code.claude.com/docs/en/mcp
