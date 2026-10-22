# work-journal Specification

## Purpose

Keep a durable local record of actual development activity across repositories and agent sessions for reliable work reporting.

## Requirements

### Requirement: Record local activity
The system SHALL expose a stdio MCP tool named `log_work` that persists an entry with an ID, occurred timestamp, recorded timestamp, repository identity, branch when available, status, optional OpenSpec change, optional ticket references, optional summary, optional session and agent identifiers, and resolved classification. Status SHALL be started, in_progress, or done. Entries SHALL identify at least an OpenSpec change or a ticket. Omitted occurred timestamps SHALL default to the current instant; supplied timestamps SHALL include an offset. Invalid entries SHALL fail without writing.

#### Scenario: Personal work
- **WHEN** an agent logs started for an OpenSpec change without a ticket
- **THEN** the entry is persisted and returned with its ID and resolved fields

#### Scenario: Invalid status
- **WHEN** an entry supplies an unsupported status
- **THEN** the tool returns a validation error without persisting it

#### Scenario: Unknown input field
- **WHEN** a tool request misspells a field, including within a ticket reference
- **THEN** it returns a validation error identifying the unknown field's full request path, without persisting an entry

### Requirement: Preserve activity rather than infer attendance
The system SHALL retain entries as historical observations without manufacturing daily activity, requiring a single active feature, or deriving done from archive time. Further in_progress entries after done SHALL be accepted as resumed work.

#### Scenario: Switching features
- **WHEN** feature Y is unfinished and only feature Z has entries on a later day
- **THEN** no activity for Y is created on that day

### Requirement: Infer and retain classification
The system SHALL resolve classification from an explicit override, then a configurable exact branch prefix before the first slash, then unclassified. Defaults SHALL map feature to feature and bugs, bugfix, and hotfix to bug. Prefix matching SHALL be case-insensitive. Existing configurations missing the bugfix alias SHALL receive that default for future observations, while explicit bugfix mappings SHALL take precedence. Each entry SHALL preserve its branch and resolved classification independently of future mapping changes; upgrading defaults SHALL NOT reclassify stored entries.

#### Scenario: Hotfix is a bug
- **WHEN** hotfix/DEP-2231-failing-account-requests is logged with default mappings
- **THEN** classification is bug and the full branch is retained

#### Scenario: Override and history
- **WHEN** an entry specifies classification maintenance and mappings subsequently change
- **THEN** the stored classification remains maintenance

#### Scenario: Bugfix alias on an existing installation
- **WHEN** an existing configuration has feature, bugs, and hotfix mappings but no bugfix mapping and a new observation uses bugfix/DEP-12-repair
- **THEN** the new observation is classified as bug and previously stored unclassified observations remain unchanged

#### Scenario: Explicit bugfix mapping
- **WHEN** configuration explicitly maps BUGFIX to maintenance and a new observation uses bugfix/DEP-12-repair
- **THEN** classification is maintenance rather than the default bug alias

### Requirement: Record ticket references without service connections
The system SHALL support Jira and Azure DevOps references with provider, scope, ID, and optional URL without contacting ticket services or requiring credentials. Configurable repository context SHALL allow ticket ID extraction from branches. Explicit references SHALL take precedence; inferred IDs SHALL NOT imply a provider or scope absent configuration. Unresolved extracted IDs SHALL be returned as metadata rather than fabricated qualified references.

#### Scenario: Configured Jira inference
- **WHEN** feature/DEP-1232-adding-verify-links is logged in a repository configured for a Jira scope and matching extraction pattern
- **THEN** DEP-1232 is stored with that provider and scope without a network request

#### Scenario: Azure numeric identifier
- **WHEN** a session supplies an Azure DevOps reference with organization/project scope and numeric ID
- **THEN** the reference is recorded unchanged without querying Azure DevOps

#### Scenario: Effective repository override
- **WHEN** an entry overrides the initialized repository identity
- **THEN** ticket inference uses only configuration matching that effective identity; unknown context retains an unresolved ID rather than the initialized repository's scope

#### Scenario: Ticket normalization and nested validation
- **WHEN** ticket fields contain surrounding whitespace
- **THEN** fields are trimmed consistently before storage, retry hashing and filtering
- **WHEN** a ticket element is null or an Azure DevOps ID is nonnumeric
- **THEN** validation identifies the offending ticket field without writing an entry

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

### Requirement: Shared repository context across worktrees
Git worktrees sharing the same common Git directory SHALL resolve to one configured repository identity, ticket provider, ticket scope, and extraction pattern, independently of checkout path and whether resolution is triggered by init or server startup. Independent clones SHALL NOT share settings solely because their remote URLs match. Existing path-keyed configurations SHALL remain readable. Compatible legacy values SHALL be preserved when establishing shared context; conflicting nonempty identities or ticket settings SHALL produce an actionable reconciliation error without silently selecting a scope, rewriting historical observations, or persisting a replacement identity.

#### Scenario: Worktree inherits configured tickets
- **WHEN** a main checkout has configured identity and Jira context and another worktree starts a server or runs init
- **THEN** a matching branch ticket is qualified with the same identity, provider, scope, and extraction pattern

#### Scenario: Repository without an origin
- **WHEN** a repository without an origin has a saved local identity and a new worktree resolves its configuration
- **THEN** the worktree uses that saved identity rather than generating another local identity

#### Scenario: Worktree is initialized first
- **WHEN** a worktree is the first checkout initialized for a repository and its main checkout later resolves configuration
- **THEN** both use the identity and ticket context established by the first initialization

#### Scenario: Legacy incomplete worktree record
- **WHEN** compatible legacy checkout records share an identity and only one has provider and scope configured
- **THEN** shared resolution retains the configured provider and scope rather than replacing them with missing values

#### Scenario: Legacy context conflict
- **WHEN** legacy records for worktrees of the same repository contain different nonempty ticket scopes or different saved identities
- **THEN** resolution identifies the conflicting configuration and requests explicit reconciliation without selecting one silently or rewriting observations

#### Scenario: Separate clone with the same remote
- **WHEN** another independent clone uses the same origin URL but a different common Git directory
- **THEN** its ticket settings are not inherited automatically from the first clone

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
