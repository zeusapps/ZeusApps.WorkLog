# Spec Delta

## MODIFIED Requirements

### Requirement: Refresh while preserving reading context
While a change is open, the dashboard SHALL detect external document updates and refresh the reader, outline, task progress, and subsequent search results. Refresh SHALL preserve the selected document and reading position when its target still exists, and SHALL preserve an unsaved review comment being typed. An archive move SHALL resolve the same selected change to its new directory. If a document disappears, changes while being read, or exceeds the document limit, the UI SHALL identify the condition and allow navigation or retry without presenting partial content as a complete file. Live document, review-inbox, and read-indicator updates SHALL coalesce to the latest authoritative revision while preserving source-qualified copy selection, routes, rendered/source mode, focus, positions, and drafts. Updates deferred during navigation or comment saving SHALL remain pending and refresh after that operation settles. A reconnect, visibility resume, or notification gap SHALL reconcile the selected document and affected search/review/read state, including archive state changes that leave document text unchanged. Neither a stale response nor an old notification SHALL replace a newer explicit selection, and the existing conflict checks for review mutations SHALL remain authoritative.

#### Scenario: Agent updates a document
- **WHEN** the agent changes a design while the user reads a heading that remains present
- **THEN** the reader refreshes the content and retains that heading and approximate reading position

#### Scenario: Change is archived while open
- **WHEN** the selected active change moves into the source's archive directory
- **THEN** the reader continues to resolve that change, updates its archive state, and makes the documents read-only for review comments

#### Scenario: Switch documents during background refresh
- **WHEN** a background refresh overlaps a document selection or successive file selections finish out of order
- **THEN** the latest explicit selection determines the visible document, including nested spec files, without reverting to the earlier file

#### Scenario: A burst arrives while saving a comment
- **WHEN** external edits and an own-write notification arrive while a review save is in flight
- **THEN** deferred updates remain pending, the saved comment and subsequent latest text appear after the save settles, and neither a draft nor a confirmed conflict is silently discarded

#### Scenario: A disconnected change is archived
- **WHEN** a selected change is archived while the browser is disconnected without changing its document text
- **THEN** recovery resolves the archived copy, updates comment permissions, and retains the logical document route, readable text, and any unsaved draft

#### Scenario: Search and review state change
- **WHEN** a document or REVIEW marker changes in the selected repository and active/archive scope
- **THEN** affected search results, inbox counts, and changed-since-read indicators refresh consistently with current content, without notices from unrelated sources making the selection incomplete

#### Scenario: Reading continues during an outage
- **WHEN** a live connection or document refresh fails while the reader contains an unsaved comment
- **THEN** the current text, view mode, keyboard focus, reading position, and comment remain usable, with truthful freshness and retry information

#### Scenario: Reconciliation finds a missing document
- **WHEN** a selected document is deleted or becomes unreadable while disconnected
- **THEN** recovery identifies the missing or unavailable target, retains the user's draft, and allows navigation among remaining documents without presenting the old snapshot as current

#### Scenario: An older refresh completes after newer navigation
- **WHEN** a reconnect refresh for one source/document completes after the user has selected another copy or document
- **THEN** it cannot replace the latest selection, move keyboard focus back to the old document, or apply an old document's draft to the new target
