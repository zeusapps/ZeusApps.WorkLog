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
The tool SHALL return response schema version 1, resolved bounds, timezone, and a bounded page of observations ordered by occurrence and ID. The default compact view SHALL contain entries once and groups by local calendar date and work identity referencing entry IDs. Ticket-only identity SHALL use the sorted distinct set of qualified tickets independent of input order. Groups SHALL preserve multiple observations rather than replacing them with a final status, and MAY span pages. All matching entries SHALL remain retrievable through explicit continuation without silent truncation.

#### Scenario: Reordered ticket references
- **WHEN** observations without a change contain the same qualified ticket set in different orders
- **THEN** they share a work identity and daily group

#### Scenario: Complete one feature and start another
- **WHEN** feature A has done and feature B has started on the same reporting day
- **THEN** both activities appear under that date with their respective work identities

#### Scenario: Native structured MCP response
- **WHEN** a client discovers query_work and retrieves a report
- **THEN** the tool advertises an output schema and returns the report in structuredContent with equivalent JSON text content

#### Scenario: Same feature changes status
- **WHEN** a feature has started and done entries on one day
- **THEN** both observations remain available in daily output

### Requirement: Bounded selectable report views
The tool SHALL default to 25 observations per page, accept page sizes from 1 through 100, and limit each structured JSON payload to 64 KiB. It SHALL expose hasMore and nextCursor, require unchanged query options during continuation, and exclude writes made after the first page. Filters SHALL apply before the page limit. It SHALL offer compact, flat, grouped and legacy views, with legacy duplicating observations in flat and daily views. includeSummaries=false SHALL omit summary content without changing stored entries. An individual observation exceeding the byte budget SHALL produce an actionable validation error.

#### Scenario: Complete a large report
- **WHEN** a client follows nextCursor until hasMore is false
- **THEN** each matching observation from the initial snapshot is retrieved once in the selected view, in deterministic order

#### Scenario: Backdated concurrent write
- **WHEN** an observation is inserted after the first page with an earlier occurred timestamp
- **THEN** it is absent from continuation pages and appears in a new query

#### Scenario: Oversized summary
- **WHEN** one observation exceeds the byte budget because of its summary
- **THEN** the error directs the client to retry with includeSummaries=false

### Requirement: Report facts only
The tool SHALL return an empty result for a period without entries and SHALL NOT infer activity from unfinished changes, contact ticket services, or generate narrative claims beyond recorded facts.

#### Scenario: Inactive unfinished change
- **WHEN** an unfinished change has no entries in the requested period
- **THEN** it contributes no daily activity
