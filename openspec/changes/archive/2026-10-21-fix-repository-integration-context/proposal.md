# Proposal

## Why

Integrating WorkLog into an existing application currently requires manual ticket configuration and introduces mandatory instructions even when the application already supplies its own logging rules. Checkout-specific configuration, an incomplete ticket filter, and a missing branch alias also cause lost context and incomplete reports.

## What Changes

- F1: Add `init --ticket-provider` and `--ticket-scope` to set explicit repository ticket context, with validation and preservation when omitted.
- F2: Match stored unresolved `extractedTicketId` values in bare ticket-ID queries while keeping qualified filters strict.
- F3: Share repository identity, ticket context, and instruction preferences across Git worktrees using their common Git directory; retain legacy configuration compatibility and report conflicts instead of guessing.
- F4: Add `bugfix` as a bug classification alias for fresh and existing configurations, preserving explicit mappings and historical observations.
- F5: Add `--no-instructions` to remove WorkLog-owned instruction blocks for selected clients and persist the opt-out. Keep MCP registration enabled and unrelated instructions intact. Provide explicit `--instructions` to re-enable managed instructions.
- Keep default instruction installation for repositories without an explicit preference. Preserve the rule against guessing ticket provider/scope.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `local-agent-integration`: Explicit ticket setup, shared worktree context, and persistent per-client instruction control during safe initialization.
- `work-journal`: Shared repository context and the `bugfix` classification alias, including existing configurations.
- `period-reporting`: Bare ticket filters include unresolved extracted IDs without fabricating qualified references.

## Impact

Changes affect InitCommand, RepositoryInitializer, repository/configuration resolution, integration manifests and client adapters, private-target validation, AgentInstructions usage, SqliteEntryStore filtering, focused tests, and installation/configuration documentation. Configuration additions must accept existing files; no journal schema migration or historical entry rewrite is planned. Existing MCP processes need restarting to load new configuration. F6 startup verification is tracked separately in `verify-worktree-server-startup`; this change does not promise automatic client registration in every fresh worktree.
