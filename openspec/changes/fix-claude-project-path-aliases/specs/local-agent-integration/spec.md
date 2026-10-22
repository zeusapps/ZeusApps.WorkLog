# Spec Delta

## MODIFIED Requirements

### Requirement: Repeatable safe initialization
Initialization SHALL preserve unrelated settings and content, update or remove only owned blocks or keys, avoid duplicate registrations, and describe applied changes, effective instruction mode, and restart or trust requirements. Equivalent Windows Claude project-path aliases SHALL contain consistent owned server definitions; retaining those existing project objects SHALL NOT count as installing multiple servers within one project. Unsupported client configuration and malformed files SHALL produce actionable errors without overwriting them or claiming complete success. A dry-run option SHALL describe planned changes, including instruction removal, project-alias reconciliation, and preference changes, without writes.

#### Scenario: Initialize twice
- **WHEN** init runs twice for the same repository and clients
- **THEN** one effective server registration remains per selected client project, with consistent definitions under equivalent Windows Claude project aliases, one owned instruction block when enabled and none when disabled

#### Scenario: Existing configuration conflict
- **WHEN** an unrelated server already occupies the intended registration name
- **THEN** initialization reports the conflict and preserves that registration

#### Scenario: Preview instruction removal
- **WHEN** init runs with `--no-instructions --dry-run` against an initialized repository
- **THEN** it describes owned-block removal and the persisted opt-out without changing client settings, repository files, or stored preferences

#### Scenario: Malformed owned block
- **WHEN** instruction removal encounters incomplete or duplicate WorkLog markers
- **THEN** initialization reports the conflict without changing client configuration or persisting the new instruction preference

## ADDED Requirements

### Requirement: Windows Claude project-path reconciliation
On Windows, Claude initialization SHALL treat existing full project keys equal to the selected checkout's Git root after slash normalization and ordinal case-insensitive comparison as aliases. It SHALL validate configuration shape and WorkLog ownership under every matching alias before applying any initialization writes. It SHALL install the same owned `worklog` server definition under each existing alias and the forward-slash Git-root key, preserving existing key spellings, unrelated project fields, and other server names independently. A conflicting or malformed alias SHALL abort initialization without changing client settings, repository files, exclusions, or saved and in-memory preferences. Initialization SHALL NOT enumerate unseen case variants, merge distinct checkout paths, or change project trust. Outside Windows project-key matching SHALL remain case-sensitive. Alias reconciliation SHALL apply regardless of managed instruction preference, and a dry-run SHALL identify affected aliases without writes.

#### Scenario: Existing lowercase key
- **WHEN** Windows Git reports `C:/Code/AnalyticsPlatform` and Claude already has a project object at `C:/Code/analyticsplatform` with no conflicting WorkLog registration
- **THEN** successful init configures the same owned server under both keys and preserves unrelated content under the existing key

#### Scenario: Multiple owned aliases
- **WHEN** several existing case or slash variants match the selected Windows checkout and their WorkLog registrations pass ownership validation
- **THEN** init reconciles their server definitions, retains each object's unrelated fields and servers, and a repeated init adds no further aliases

#### Scenario: Conflict hidden under an alias
- **WHEN** one equivalent alias contains an unowned worklog server, an externally changed owned command, or malformed project/server configuration
- **THEN** init reports the offending project key and preserves every initialization target without partially updating another alias

#### Scenario: Separate checkout or platform
- **WHEN** another project key identifies a distinct worktree path, or differs only in case on a non-Windows platform
- **THEN** init does not reconcile or modify that project as an alias of the selected checkout

#### Scenario: Disabled instructions and preview
- **WHEN** Windows Claude init uses disabled instructions and is previewed with dry-run before being applied
- **THEN** preview identifies alias reconciliation without writes, and applying configures the aliases while retaining disabled instructions and unrelated content
