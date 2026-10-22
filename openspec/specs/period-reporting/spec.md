# period-reporting Specification

## Purpose

Return structured journal facts for explicit reporting periods so assistants can produce accurate daily and periodic work reports.

## Requirements

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

### Requirement: Structured daily output
The tool SHALL return response schema version 1, resolved bounds, timezone, and a bounded page of observations ordered by occurrence and ID. The default compact view SHALL contain entries once and groups by local calendar date and work identity referencing entry IDs. Work identity SHALL follow the selected grouping mode, defaulting to ticket grouping when the selector is omitted. Ticket-set identity SHALL use the repository and sorted distinct set of qualified tickets independent of input order. Groups SHALL preserve multiple observations rather than replacing them with a final status, and MAY span pages. All matching entries SHALL remain retrievable through explicit continuation without silent truncation.

#### Scenario: Reordered ticket references
- **WHEN** observations without a change contain the same qualified ticket set in different orders
- **THEN** they share a work identity and daily group

#### Scenario: Complete one feature and start another
- **WHEN** feature A has done and feature B has started on the same reporting day using explicit change grouping
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

### Requirement: Selectable change or ticket grouping
The query tool SHALL accept optional `period.groupBy` with values `change` and `ticket`, defaulting to `ticket`. Omitted and explicit `ticket` SHALL produce identical ticket grouping and continuation matching. Explicit `change` SHALL produce the existing change-first identity with its qualified ticket-set fallback. In `ticket` mode, observations with qualified tickets SHALL group by repository and their complete sorted distinct provider/scope/ID ticket set regardless of change, and observations without qualified tickets SHALL retain their existing identity. The tool SHALL preserve each observation's original change, tickets, and unresolved extracted ID; it SHALL NOT infer qualification from current settings. Each observation SHALL belong to exactly one daily group per returned page; overlapping but different ticket sets SHALL remain distinct. Null, blank, and unsupported selector values SHALL return actionable validation errors. The selector SHALL apply to every view containing daily groups, leave flat observations unchanged, and participate in continuation matching. Changing it during continuation SHALL be rejected. Existing filtering, snapshots, ordering, page limits, byte budgets, and output shape SHALL remain in force.

#### Scenario: Historical and new observations for one ticket
- **WHEN** two same-day observations in one repository have the same qualified ticket, one without a change and one with change `repair-links`, and the query omits the selector or selects ticket grouping
- **THEN** they share one daily group with both observations retained, while explicit change grouping keeps their existing separate identities

#### Scenario: Preserve ticket namespaces
- **WHEN** observations have equal bare ticket IDs but differ in repository, provider, or scope
- **THEN** ticket grouping keeps their identities separate

#### Scenario: Multiple tickets without duplication
- **WHEN** same-day observations reference sets `{A,B}`, `{B,A,A}`, `{A}`, and `{A,C}` in one repository with the same qualified namespaces
- **THEN** the first two share a ticket-set identity, the other sets remain separate, and no observation is assigned to multiple groups

#### Scenario: Unresolved historical reference
- **WHEN** an observation has a change and only an unresolved extracted ticket ID, and another has a matching bare ID in a qualified reference
- **THEN** ticket grouping retains the unresolved observation's change identity without qualifying or merging it based on current settings

#### Scenario: Continue a ticket-grouped report
- **WHEN** a bounded ticket-grouped report spans pages and the client follows its cursors with unchanged options
- **THEN** every matching snapshot observation remains retrievable exactly once, group fragments retain the same date and work identity, and switching to change grouping rejects the cursor

#### Scenario: Views and invalid values
- **WHEN** clients use ticket grouping with compact, grouped, legacy, or flat views
- **THEN** group-containing views use ticket identities, flat entries remain unchanged, structured content matches its schema and text fallback, and invalid selector values fail without changing journal data
