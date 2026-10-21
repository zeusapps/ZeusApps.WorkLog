# Spec Delta

## MODIFIED Requirements

### Requirement: Explicit period queries
The system SHALL expose a stdio MCP tool named `query_work` accepting inclusive from and exclusive to timestamps with offsets and an explicit reporting timezone. It SHALL filter by occurred timestamp, reject invalid ranges or timezones, and support optional repository, change, ticket, status, and classification filters. A bare ticket-ID filter SHALL match either a stored qualified ticket's ID or an unresolved extracted ticket ID. A fully qualified provider:scope:id filter SHALL match only a stored reference with that provider, scope, and ID; it SHALL NOT qualify an unresolved ID using current configuration. Surrounding filter whitespace SHALL be trimmed. Ticket filtering SHALL combine with all other filters before pagination and SHALL leave stored entries unchanged.

#### Scenario: Boundary handling
- **WHEN** entries occur exactly at from and to
- **THEN** the entry at from is included and the entry at to is excluded

#### Scenario: Invalid period
- **WHEN** to is not later than from or the timezone is unknown
- **THEN** a validation error is returned

#### Scenario: Find unresolved historical ticket
- **WHEN** a stored observation has change repair-links, extractedTicketId DEP-7483, and no qualified tickets, and a query filters ticket by DEP-7483
- **THEN** the observation is returned with its unresolved metadata and empty ticket array intact

#### Scenario: Keep qualified filters strict
- **WHEN** the same unresolved observation is queried with jira:company.atlassian.net:DEP-7483
- **THEN** it is excluded even if the repository is now configured with that provider and scope

#### Scenario: Qualified reference still matches
- **WHEN** an observation has a qualified Jira reference for DEP-7483
- **THEN** both a bare DEP-7483 query and a query with its exact provider, scope, and ID can return it

#### Scenario: Combine unresolved matching with other filters
- **WHEN** matching unresolved IDs exist in different repositories, statuses, or periods and a query narrows those fields with a small page size
- **THEN** only observations satisfying every filter appear, and continuation retrieves every matching observation from the initial snapshot without duplicates
