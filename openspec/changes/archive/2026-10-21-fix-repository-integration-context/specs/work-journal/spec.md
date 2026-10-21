# Spec Delta

## MODIFIED Requirements

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

## ADDED Requirements

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
