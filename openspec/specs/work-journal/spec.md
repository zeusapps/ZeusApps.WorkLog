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
The system SHALL resolve classification from an explicit override, then a configurable exact branch prefix before the first slash, then unclassified. Defaults SHALL map feature to feature, bugs to bug, and hotfix to bug. Each entry SHALL preserve its branch and resolved classification independently of future mapping changes.

#### Scenario: Hotfix is a bug
- **WHEN** hotfix/DEP-2231-failing-account-requests is logged with default mappings
- **THEN** classification is bug and the full branch is retained

#### Scenario: Override and history
- **WHEN** an entry specifies classification maintenance and mappings subsequently change
- **THEN** the stored classification remains maintenance

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
The system SHALL persist one journal per local user across repositories and restarts, support concurrent client processes, and keep machines independent. A supplied idempotency key SHALL return the original entry for the same normalized request and reject conflicting reuse. Concurrent starts for the same repository and OpenSpec change SHALL yield one initial started entry; resumed activity SHALL use in_progress.

#### Scenario: Both agents log creation
- **WHEN** Codex and Claude Code both log started for the same repository and change
- **THEN** exactly one initial started entry exists and both receive its identity

#### Scenario: Retry a progress entry
- **WHEN** the same normalized request is retried with its original idempotency key
- **THEN** the existing entry is returned without duplication
