# Spec Delta

## MODIFIED Requirements

### Requirement: Structured daily output
The tool SHALL return response schema version 1, resolved bounds, timezone, and a bounded page of observations ordered by occurrence and ID. The default compact view SHALL contain entries once and groups by local calendar date and work identity referencing entry IDs. Work identity SHALL follow the selected grouping mode, defaulting to existing change-first behavior when the selector is omitted. Ticket-set identity SHALL use the repository and sorted distinct set of qualified tickets independent of input order. Groups SHALL preserve multiple observations rather than replacing them with a final status, and MAY span pages. All matching entries SHALL remain retrievable through explicit continuation without silent truncation.

#### Scenario: Reordered ticket references
- **WHEN** observations without a change contain the same qualified ticket set in different orders
- **THEN** they share a work identity and daily group

#### Scenario: Complete one feature and start another
- **WHEN** feature A has done and feature B has started on the same reporting day using default grouping
- **THEN** both activities appear under that date with their respective work identities

#### Scenario: Native structured MCP response
- **WHEN** a client discovers query_work and retrieves a report
- **THEN** the tool advertises an output schema and returns the report in structuredContent with equivalent JSON text content

#### Scenario: Same feature changes status
- **WHEN** a feature has started and done entries on one day
- **THEN** both observations remain available in daily output

## ADDED Requirements

### Requirement: Selectable change or ticket grouping
The query tool SHALL accept optional `period.groupBy` with values `change` and `ticket`, defaulting to `change`. Omitted and explicit `change` SHALL produce the existing change-first identity with its qualified ticket-set fallback. In `ticket` mode, observations with qualified tickets SHALL group by repository and their complete sorted distinct provider/scope/ID ticket set regardless of change, and observations without qualified tickets SHALL retain their existing identity. The tool SHALL preserve each observation's original change, tickets, and unresolved extracted ID; it SHALL NOT infer qualification from current settings. Each observation SHALL belong to exactly one daily group per returned page; overlapping but different ticket sets SHALL remain distinct. Null, blank, and unsupported selector values SHALL return actionable validation errors. The selector SHALL apply to every view containing daily groups, leave flat observations unchanged, and participate in continuation matching. Changing it during continuation SHALL be rejected. Existing filtering, snapshots, ordering, page limits, byte budgets, and output shape SHALL remain in force.

#### Scenario: Historical and new observations for one ticket
- **WHEN** two same-day observations in one repository have the same qualified ticket, one without a change and one with change `repair-links`, and the query selects ticket grouping
- **THEN** they share one daily group with both observations retained, while default or explicit change grouping keeps their existing separate identities

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
