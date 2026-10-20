# Spec Delta

## Purpose

Return structured journal facts for explicit reporting periods so assistants can produce accurate daily and periodic work reports.

## ADDED Requirements

### Requirement: Explicit period queries
The system SHALL expose a stdio MCP tool named `query_work` accepting inclusive from and exclusive to timestamps with offsets and an explicit reporting timezone. It SHALL filter by occurred timestamp, reject invalid ranges or timezones, and support optional repository, change, ticket, status, and classification filters.

#### Scenario: Boundary handling
- **WHEN** entries occur exactly at from and to
- **THEN** the entry at from is included and the entry at to is excluded

#### Scenario: Invalid period
- **WHEN** to is not later than from or the timezone is unknown
- **THEN** a validation error is returned

### Requirement: Structured daily output
The tool SHALL return a response schema version, resolved bounds, timezone, ordered entries with IDs and stored metadata, and groups by local calendar date and work identity. Groups SHALL preserve multiple statuses and summaries in occurrence order instead of replacing them with only the latest status. Ties SHALL have deterministic ordering. The response SHALL include all matching entries without silent truncation.

#### Scenario: Complete one feature and start another
- **WHEN** feature A has done and feature B has started on the same reporting day
- **THEN** both activities appear under that date with their respective work identities

#### Scenario: Native structured MCP response
- **WHEN** a client discovers query_work and retrieves a report
- **THEN** the tool advertises an output schema and returns the report in structuredContent with equivalent JSON text content

#### Scenario: Same feature changes status
- **WHEN** a feature has started and done entries on one day
- **THEN** both observations remain available in daily output

### Requirement: Report facts only
The tool SHALL return an empty result for a period without entries and SHALL NOT infer activity from unfinished changes, contact ticket services, or generate narrative claims beyond recorded facts.

#### Scenario: Inactive unfinished change
- **WHEN** an unfinished change has no entries in the requested period
- **THEN** it contributes no daily activity
