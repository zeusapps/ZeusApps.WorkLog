# local-agent-integration Specification

## Purpose

Initialize private repository integrations that let Codex and Claude Code record actual session work through the same local MCP tools.

## Requirements

### Requirement: Private repository initialization
The application SHALL provide `worklog init` with repository and client selection for Codex, Claude Code, or both. Omitted repository SHALL mean the current repository and omitted clients SHALL mean both. It SHALL register the installed executable as a stdio MCP server and, when managed instructions are enabled for that repository and client, install logging instructions using supported private settings. It SHALL NOT edit shared CLAUDE.md, AGENTS.md, .mcp.json, OpenSpec configuration, or generated OpenSpec skills. Created repository-local files SHALL be excluded through local Git exclusions. Already tracked destinations that initialization would modify SHALL cause a clear conflict instead of being modified.

#### Scenario: Claude local setup
- **WHEN** Claude Code integration is initialized with managed instructions enabled
- **THEN** MCP registration uses private local scope and instructions use a managed block in CLAUDE.local.md while existing instructions remain available

#### Scenario: Codex local setup
- **WHEN** Codex integration is initialized with managed instructions enabled
- **THEN** its private repository configuration adds the MCP server and additive instructions without replacing base instructions or shared AGENTS.md

#### Scenario: Existing application supplies instructions
- **WHEN** either client is initialized with `--no-instructions`
- **THEN** the private MCP registration is configured without adding WorkLog instructions or instruction imports, and unrelated instructions remain unchanged

#### Scenario: Unmodified instruction target is tracked
- **WHEN** Claude initialization has instructions disabled and a tracked CLAUDE.local.md has no owned block to remove
- **THEN** initialization leaves that file unchanged and does not reject it as a write destination

### Requirement: Repeatable safe initialization
Initialization SHALL preserve unrelated settings and content, update or remove only owned blocks or keys, avoid duplicate registrations, and describe applied changes, effective instruction mode, and restart or trust requirements. Unsupported client configuration and malformed files SHALL produce actionable errors without overwriting them or claiming complete success. A dry-run option SHALL describe planned changes, including instruction removal and preference changes, without writes.

#### Scenario: Initialize twice
- **WHEN** init runs twice for the same repository and clients
- **THEN** one effective server registration remains per selected client, with one owned instruction block when enabled and none when disabled

#### Scenario: Existing configuration conflict
- **WHEN** an unrelated server already occupies the intended registration name
- **THEN** initialization reports the conflict and preserves that registration

#### Scenario: Preview instruction removal
- **WHEN** init runs with `--no-instructions --dry-run` against an initialized repository
- **THEN** it describes owned-block removal and the persisted opt-out without changing client settings, repository files, or stored preferences

#### Scenario: Malformed owned block
- **WHEN** instruction removal encounters incomplete or duplicate WorkLog markers
- **THEN** initialization reports the conflict without changing client configuration or persisting the new instruction preference

### Requirement: Session-driven OpenSpec logging instructions
When managed instructions are enabled, both integrations SHALL instruct the agent to log started after successful change creation, including openspec-propose; log in_progress after meaningful actual work; and log done when the session establishes reporting completion independently of archive. Instructions SHALL allow feature switching and multiple features in a session, avoid activity on idle days, preserve a retry key for retries, and avoid guessing unknown ticket providers or completion. They SHALL explain that logging is agent-driven rather than a guaranteed lifecycle hook and that failures must be reported. Disabling managed instructions SHALL suppress WorkLog-owned instructions without modifying rules maintained by the application or user.

#### Scenario: Delayed archive
- **WHEN** a session establishes completion but archives later
- **THEN** enabled managed instructions direct done to be recorded at completion rather than archive

#### Scenario: Required migration pending
- **WHEN** the session identifies a migration as required before reporting completion
- **THEN** enabled managed instructions direct in_progress with optional pending-work summary rather than automatic done

#### Scenario: Application-owned logging policy
- **WHEN** managed instructions are disabled and the application already contains logging rules
- **THEN** init preserves those rules without replacing or duplicating them

### Requirement: Update hint handling instructions
When managed instructions are enabled, both private client integrations SHALL instruct the assistant to surface an update hint from a successful WorkLog tool result at most once per available version within the assistant session. Instructions SHALL identify the explicit update command and client restart requirement, and SHALL NOT treat a hint or an ordinary logging/reporting request as permission to install an update or terminate processes. They SHALL explain that notice presentation depends on the assistant, rather than guaranteeing an MCP client notification or dedicated UI. Rerunning init SHALL refresh these owned instructions using the existing preservation rules when enabled, and SHALL preserve the absence of owned logging and update-hint guidance when disabled.

#### Scenario: First update hint
- **WHEN** the assistant receives a hint for an available version it has not announced during the session and managed instructions are enabled
- **THEN** instructions direct it to briefly tell the user the version, update command, and need to restart clients

#### Scenario: Repeated hint
- **WHEN** later tool results contain the same available version during that session and managed instructions are enabled
- **THEN** instructions direct the assistant to avoid repeating the announcement

#### Scenario: User continues journal work
- **WHEN** an update hint appears during a logging or reporting request and managed instructions are enabled
- **THEN** instructions direct the assistant to complete that request without starting installation or stopping processes based only on the hint

#### Scenario: Refresh private integration
- **WHEN** init is rerun after upgrading to a version containing these instructions with managed instructions enabled
- **THEN** the owned blocks gain update-hint guidance while unrelated private and shared instructions remain preserved

#### Scenario: Refresh opted-out integration
- **WHEN** init is rerun after upgrading with managed instructions disabled
- **THEN** owned logging and update-hint instructions remain absent while the MCP server registration remains configured

### Requirement: Explicit repository ticket setup
Initialization SHALL accept `--ticket-provider` and `--ticket-scope` as a pair to configure repository ticket context. Providers SHALL be `jira` or `azure-devops`; scope SHALL be nonempty after trimming. Supplying only one flag, an unsupported provider, or a blank value SHALL fail before any initialization writes. Omitting both flags SHALL preserve existing context, including a custom extraction pattern, without inferring provider or scope from branch names or Git remotes. Supplied settings SHALL apply to subsequent server instances across worktrees of the same repository.

#### Scenario: Initialize Jira ticket context
- **WHEN** init succeeds with `--ticket-provider jira --ticket-scope company.atlassian.net`
- **THEN** a subsequently started server can qualify a matching branch ticket with that provider and scope without a ticket-service request

#### Scenario: Preserve existing settings
- **WHEN** init is rerun without ticket flags
- **THEN** configured provider, scope, and extraction pattern remain unchanged

#### Scenario: Invalid or partial ticket settings
- **WHEN** only a provider is supplied, only a scope is supplied, the provider is unsupported, or a supplied value is blank
- **THEN** init returns an actionable validation error without modifying configuration or client files

### Requirement: Persistent per-client instruction preference
Initialization SHALL accept mutually exclusive `--no-instructions` and `--instructions` options for selected clients. `--no-instructions` SHALL remove existing WorkLog-owned instruction blocks, preserve all unrelated instructions, and persist disabled mode for the repository and selected clients. Later init runs without either option SHALL retain that mode, including from another worktree of the same repository. `--instructions` SHALL explicitly re-enable managed instructions. A repository/client without a saved preference SHALL default to enabled. Changes to one client's preference SHALL NOT change another client's preference. An invocation SHALL modify instruction files only in its selected checkout and client configuration; preferences SHALL govern later init runs in other worktrees without silently rewriting those worktrees' existing files. Disabled mode SHALL suppress WorkLog-owned logging and update-hint guidance; enabled mode SHALL retain both under their existing behavior requirements.

#### Scenario: Disable and rerun
- **WHEN** Claude integration contains an owned block and init runs with `--clients claude --no-instructions`, followed by plain init for Claude
- **THEN** both runs leave the MCP server registered, the owned block remains absent, unrelated content remains, and disabled mode remains stored

#### Scenario: Client-specific preference
- **WHEN** Claude instructions are disabled while Codex instructions remain enabled
- **THEN** initializing both clients preserves those separate preferences

#### Scenario: Explicit re-enable
- **WHEN** init runs with `--instructions` for a previously disabled client
- **THEN** managed instructions are installed once and enabled mode is persisted for that client

#### Scenario: Inherit opt-out in a fresh worktree
- **WHEN** a repository has disabled instructions for Claude and Claude integration is initialized in a new worktree without instruction flags
- **THEN** the new worktree receives the server registration without a WorkLog instruction block or generated instruction imports

#### Scenario: Conflicting instruction options
- **WHEN** init receives both `--instructions` and `--no-instructions`
- **THEN** it fails before any initialization writes
