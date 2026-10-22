# Spec Delta

## MODIFIED Requirements

### Requirement: Retry-safe shared local storage
The system SHALL persist one journal per local user across repositories and restarts, support concurrent client processes from releases declaring the same supported shared-data contract, and keep machines independent. A supplied idempotency key SHALL return the original entry for the same normalized request and reject conflicting reuse. Concurrent starts for the same repository and OpenSpec change SHALL yield one initial started entry; resumed activity SHALL use in_progress. Selecting a compatible application payload SHALL NOT change stored observations, retry identities, ticket semantics, repository identities, or journal schema. Supported cross-version report continuations SHALL preserve their snapshot and filter semantics.

#### Scenario: Both agents log creation
- **WHEN** Codex and Claude Code both log started for the same repository and change
- **THEN** exactly one initial started entry exists and both receive its identity

#### Scenario: Retry a progress entry
- **WHEN** the same normalized request is retried with its original idempotency key
- **THEN** the existing entry is returned without duplication

#### Scenario: Cross-version retry and concurrent starts
- **WHEN** older and newer supported servers concurrently log or retry the same work against the same journal
- **THEN** original IDs and timestamps are retained for matching retries, conflicting key reuse is rejected, and duplicate initial starts are prevented

#### Scenario: Cross-version report continuation
- **WHEN** a continuation issued by one supported version is submitted to another with the same query options and journal
- **THEN** it preserves the original snapshot without losing or duplicating observations or incorporating later backdated writes

## ADDED Requirements

### Requirement: Preserve shared configuration across supported writers
Supported concurrent payload versions SHALL preserve unknown configuration fields at every extensible object level when saving known settings. Writers SHALL coordinate updates against the latest saved state so edits to different repository contexts or settings are not silently overwritten by a stale snapshot. A stale conflicting edit SHALL fail actionably without overwriting the conflicting state. Repository-context creation SHALL converge on the same saved identity for concurrent first use of a repository. Dry-run initialization and rejected preflight SHALL retain existing no-write behavior; existing partial-initialization reporting SHALL remain actionable. Existing instruction preferences, ticket settings, branch mappings, and legacy context reconciliation rules SHALL continue to apply.

#### Scenario: Older supported writer saves extended settings
- **WHEN** a newer compatible process stores extra root or nested configuration fields and an older supported writer updates a known setting
- **THEN** the unknown fields and unrelated known settings remain intact

#### Scenario: Independent concurrent edits
- **WHEN** two supported processes update different repository contexts or settings after loading the same configuration
- **THEN** both changes survive without dropping unrelated fields or replacing one writer's updates with the other's earlier snapshot

#### Scenario: Conflicting edit
- **WHEN** a planned setting is concurrently changed to a conflicting value before commit
- **THEN** the stale writer reports the conflict without overwriting the newer setting or claiming complete initialization

#### Scenario: Concurrent first repository resolution
- **WHEN** different supported processes first resolve the same common Git directory at the same time
- **THEN** they adopt one saved repository identity and compatible context rather than persisting divergent local identities

#### Scenario: Dry-run or rejected initialization
- **WHEN** initialization is a dry run or fails preflight validation
- **THEN** no configuration preference or client-file changes are persisted and unknown fields remain intact
