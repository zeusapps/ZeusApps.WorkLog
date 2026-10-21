# Design

## Context

See proposal.md for motivation and the three delta specs for behavior. InitCommand currently exposes repository, clients, data directory, and dry-run only. RepositoryRegistry keys RepositoryConfig by checkout root, generating a new local identity when no origin exists. EntryFactory already qualifies branch IDs from configured provider/scope and rejects ticket-only observations without qualified references. That validation remains intact.

ClaudeIntegration always writes CLAUDE.local.md and generated instruction imports. CodexIntegration always merges developer_instructions and the owned block into local TOML. IntegrationManifest records ownership but no instruction preference. InitializationPlan preflights changes, backs up existing files, and writes the manifest first to make partial failures recoverable. SqliteEntryStore currently searches only the tickets array. WorkLogConfiguration deserializes a stored mapping dictionary over its initializer, so adding a new default alone does not upgrade existing dictionaries.

## Goals / Non-Goals

**Goals:** Keep repository context consistent across checkout paths, separate server registration from instruction management, and make existing unresolved observations discoverable while retaining their original meaning. Keep initialization reviewable through dry-run and preserve existing ownership/conflict handling.

**Non-Goals:** Rewriting journal history, accepting an unqualified ID as a ticket-only work identity, contacting issue services, discovering a Jira site from a branch or origin, automatically editing every worktree, or redesigning client server discovery. F6 belongs to the separate verification change.

## Decisions

### 1. Add explicit ticket flags and a three-state instruction choice

Add paired string options `--ticket-provider` and `--ticket-scope`, with provider allowlisting, trimmed nonempty values, and all-or-neither validation before planning writes. Both absent means preserve context; both present means replace provider/scope while retaining TicketPattern. Azure DevOps still requires a suitable numeric extraction pattern or explicit ticket references; these flags do not silently change the existing pattern.

Model instruction selection as unspecified, enabled, or disabled rather than a false-default Boolean. Mutually exclusive `--instructions` and `--no-instructions` choose enabled/disabled; unspecified uses the saved per-client preference or the legacy default enabled. Scope the preference to the logical repository and selected clients, independently of any machine-wide instructions. Include effective mode and planned preference changes in init output.

A one-invocation skip was rejected because rerunning init would recreate the unwanted instructions. Disabling instructions by removing all developer_instructions was rejected because it would erase application/user rules.

### 2. Resolve repository context using Git's common directory

Use GitRepository to resolve the checkout root and canonical common Git directory. Add an optional private configuration collection keyed by the latter, holding the authoritative RepositoryConfig plus per-client instruction preferences. Keep existing path-keyed Repositories readable as legacy inputs rather than duplicating independent authoritative contexts per worktree. Runtime lookup in RepositoryRegistry, init, server startup, and EntryFactory's effective-identity lookup must use the same resolution rules. Preserve current ambiguity errors for explicit repository identities that match incompatible contexts.

On first resolution, inspect relevant saved checkout roots belonging to that common directory. Promote compatible saved values, retaining a saved local identity even when an origin was added later. Missing provider/scope values do not erase known values. Nonempty disagreements in identity, provider, scope, or extraction pattern are a conflict with the paths/fields and reconciliation guidance in the error; do not pick a winner or rewrite old observations. A missing legacy checkout path is not evidence that it belongs to another repository: retain it and do not silently migrate it based on a matching remote. Reuse a resolvable member's saved context; unresolved records remain untouched.

When no saved member exists, establish one shared context from the current repository using existing identity derivation. Use platform-appropriate canonical path comparison, including Windows case-insensitivity, and ensure main-first and worktree-first initialization converge. Independent clones have different common directories, even when their origin-derived identity strings match; never import their provider/scope merely by matching remote URL.

Store new context/preference changes through InitializationPlan during init, preserving dry-run and preflight behavior. Server startup may persist missing shared context through the existing registry persistence boundary. Existing path records and journal entries need no destructive conversion.

Copying main-checkout settings into each new worktree was rejected because subsequent updates would diverge. Remote URL was rejected as the configuration-sharing key because unrelated clones can legitimately use different settings.

### 3. Make instruction management conditional in each client adapter

Pass resolved per-client instruction mode into ClientSetup. Always plan the selected client's owned server registration. With instructions enabled, retain existing rendering, imports, inheritance, and update-hint guidance. With instructions disabled:

- Claude: remove only the well-formed WorkLog managed block if present, including imports inside that owned block. Do not create CLAUDE.local.md, add imports, or touch unrelated content. Leave a pre-existing file in place even if removing the block empties it.
- Codex: remove only the managed block from an existing developer_instructions value. Preserve its remaining value and other TOML content. Do not copy global instructions into a local value or introduce developer_instructions when it was absent. An empty existing value may remain; do not reconstruct unknown pre-init content.

Add marker-validated removal alongside ManagedBlock.Replace. Malformed or duplicate markers fail during planning before any file updates. PrivateGitFiles must validate actual mutation destinations: an untouched tracked CLAUDE.local.md is allowed in disabled mode; a tracked file whose owned block would be removed remains a clear conflict. Codex's server configuration destination still requires its usual private-file checks. Exclude only generated/modified private targets and retain existing local exclusions.

Persist per-client preferences in the shared context rather than only the checkout-local ownership manifest, so a newly initialized worktree inherits the opt-out. Manifests retain ownership of registrations in their individual checkouts. Changing the preference updates only the selected checkout now; other initialized worktrees lose stale blocks when init is next run there. Do not silently sweep sibling directories or globally registered instructions.

### 4. Extend ticket filtering without changing stored facts

Keep the existing tickets-array EXISTS condition and OR it with exact comparison to `$.extractedTicketId` for a bare ticket filter. Parenthesize the entire alternative so repository, period, status, classification, snapshot, and continuation predicates still constrain it. A provider-qualified filter retains the existing qualified-reference path and never consults current repository configuration. Keep SQL parameters, normalization, ordering, and pre-pagination filtering.

Update QueryRequest's advertised description and README examples so assistants know a bare ID can return unresolved observations. No new report fields, schema version, grouping rules, or migration are required. Historical observations rejected before persistence cannot be recovered by a query change.

### 5. Add the branch alias during configuration normalization

Normalize fresh and deserialized configuration so a case-insensitive missing `bugfix` key gets `bug`. Preserve every explicit mapping, including `BUGFIX`, and document an explicit `bugfix: unclassified` value for users who want that result. Do not rewrite existing classifications or replace the user's dictionary with defaults. This normalization is effective for new server instances; persist it only through ordinary configuration saving, not a surprise read-time rewrite.

## Risks / Trade-offs

- Legacy worktree records can already disagree in identity or scope -> fail with reconciliation guidance; do not merge historical groups or silently reassign tickets.
- Client settings formats and inheritance differ -> isolate server edits from instruction edits, preserve surrounding content, and test both adapters with existing instructions, no instructions, repeated init, and partial failure.
- A shared opt-out cannot remove blocks already loaded in other sessions -> describe the checkout-local cleanup and client restart requirements explicitly.
- An older binary will not understand shared-context settings -> retain legacy data and private backups, and require a compatible binary for worktree-aware behavior. Do not promise automatic safe downgrade after new settings have changed.
- Bare IDs can collide across provider scopes -> retain explicit repository and qualified filters; unresolved metadata stays visibly unresolved.
- The existing unarchived release change adds update-hint instruction requirements -> when reconciling specs, apply that change first and retain its guidance only for integrations with managed instructions enabled. Do not recreate opted-out blocks during an upgrade refresh.

## Migration Plan

1. Introduce additive shared context and per-client preference fields with old-config loading tests, conflict checks, and dry-run coverage. Preserve local identities and legacy records.
2. Implement flags, adapter separation, block removal, classification normalization, and ticket-filter changes with focused regression tests.
3. Update installation, configuration, and query documentation, including existing-app setup and restart instructions. Validate enabled-mode behavior still preserves the current update-hint guidance.
4. Run Release build/tests and isolated packaged integration checks. Validate worktrees with and without origin, both initialization orders, legacy incomplete records, and conflicts.
5. For rollback, stop relevant clients and restore the backed-up configuration/integration files together before returning to an older binary. Journal records remain unchanged; retain any observations recorded after upgrade.
