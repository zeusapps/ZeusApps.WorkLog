# Spec Delta

## ADDED Requirements

### Requirement: Vertical change document navigation
The reader SHALL present a vertical document navigator beside the document on desktop. It SHALL group available proposal, design, and tasks documents under Overview, nested spec documents under Specs, and other Markdown artifacts under Other documents. Entries SHALL remain distinguishable by their relative paths, including repeated file basenames in different directories. A case-insensitive name/path filter SHALL make long document collections navigable without hiding the identity of the currently open document. The selected document SHALL be visibly indicated. The reader SHALL retain heading and task navigation in a collapsible On this page section and keep rendered/source view controls available. On narrow screens it SHALL provide an accessible document selector without forcing horizontal page scrolling. Navigation SHALL work for active and archived change documents; a source-reference document without a selected change SHALL remain readable with truthful navigation context. Existing default-document selection, source-qualified routes, remembered reading positions, drafts, and history navigation SHALL be preserved.

#### Scenario: Many documents with repeated basenames
- **WHEN** a change contains 50 Markdown documents including several nested files named spec.md and other Markdown artifacts
- **THEN** every available document is reachable through the grouped navigator, repeated basenames remain distinguishable by path, and filtering by a unique path identifies the intended document

#### Scenario: Navigate within a filtered collection
- **WHEN** the user filters the document list and selects a nested spec
- **THEN** the selected spec opens with its full relative identity, the current-document indication remains available, and filtering has not changed the stored document content or source selection

#### Scenario: Navigate on a narrow viewport
- **WHEN** the user opens the reader at a 390px-wide viewport
- **THEN** a labeled document selector exposes the same documents, the selected document stays readable, and neither the page nor its navigation controls force horizontal overflow

#### Scenario: Retain document context
- **WHEN** the user switches between documents and later returns while a background refresh occurs
- **THEN** the latest explicit selection remains authoritative, remembered positions and unsaved comments are preserved, and headings and task links still navigate within the selected document

#### Scenario: Read archived and source-reference documents
- **WHEN** the reader displays an archived change or a source-reference document with no selected change
- **THEN** it preserves the correct read-only or source-reference context and exposes only document choices that actually belong to that context

### Requirement: Source-qualified document copy selection
When several sources contain a change, every offered document copy SHALL have a unique source-qualified identity and SHALL open the copy the user selected. An explicit document path SHALL apply only to the navigation that supplied it. A later chooser opened from a route without that path SHALL use that route's target or the normal default-document rules, without inheriting a path from an earlier chooser. Closing or replacing the chooser SHALL preserve applicable reader drafts and meaningful browser history.

#### Scenario: Same variant in different sources
- **WHEN** a checkout and worktree expose copies with the same variant identifier
- **THEN** the chooser presents distinct selectable copies and each selection opens the intended source

#### Scenario: A previous document path does not leak
- **WHEN** the user opens a chooser for a particular document, dismisses it, and later opens a different chooser from a route
- **THEN** the later selection follows the route or default-document rules instead of the previous document path

### Requirement: Accurate read-status outcomes
Marking a document or a change read SHALL report successful marks, documents that genuinely changed during the operation, and failures to read or store a baseline accurately. Only a confirmed revision conflict SHALL be described as a document changing during marking. A storage, connectivity, or authorization failure SHALL receive an actionable explanation without claiming that the document changed or that an unsuccessful mark succeeded. Batch marking SHALL preserve successful marks and summarize partial outcomes without one repeated notice per document.

#### Scenario: A document changes during marking
- **WHEN** the server rejects a read mark because the document revision changed
- **THEN** the reader identifies a conflict and permits review or reload before another mark

#### Scenario: Baseline storage fails
- **WHEN** a change-wide read operation encounters a storage or connectivity failure
- **THEN** it reports that marking failed for the affected documents, retains any successful marks, and does not describe those failures as document changes

#### Scenario: Authorization is lost
- **WHEN** a read-status action is rejected because the browser is no longer authorized
- **THEN** the dashboard explains how to regain authorization and retains the readable context and unsaved comment without repeated batch failures

## MODIFIED Requirements

### Requirement: Keyboard-accessible reading and navigation
Repository filters, change selection, document navigation, heading navigation, search results, review comment controls, and link-copy controls SHALL be keyboard operable with visible focus. Escape SHALL dismiss the innermost active interaction first. While focus is in a text input, the review composer, or a delete confirmation, Escape SHALL cancel or leave that interaction and SHALL NOT close the reader or discard typed text. Browser back and forward navigation SHALL preserve meaningful document routes, and timeline facts SHALL remain available as readable text without relying exclusively on color, hover, or pointer interactions. The narrow-screen document selector SHALL be keyboard operable; dismissing it SHALL return focus to its opener without closing the reader or discarding a draft.

#### Scenario: Navigate without a pointer
- **WHEN** the user selects a change, opens a document, searches, and follows a result using only the keyboard
- **THEN** each action is available, focus remains visible, and the matching document location is reachable

#### Scenario: Escape while writing a comment
- **WHEN** the user is typing a review comment and presses Escape
- **THEN** the reader stays open, the typed text is retained, and focus leaves the composer; a further Escape with focus outside inputs closes the reader

#### Scenario: Dismiss the narrow document selector
- **WHEN** the document selector is open on a narrow viewport and the user dismisses it with Escape
- **THEN** only the selector closes, focus returns to its opener, and the current document and unsaved comment remain available

### Requirement: Changes since last read
The dashboard SHALL remember, per document, the content the user last marked as read. Opening a document with no remembered content SHALL record its current content as read. When the current content differs from the remembered content, the reader SHALL indicate that the document changed since it was last read, highlight added and changed passages in rendered and source views, show the positions of removed passages with their removed text available, and allow keyboard navigation between changes. The reader SHALL list REVIEW markers present in the remembered content but absent from the current content as removed since last read, showing their quote and comment. A "Mark as read" action SHALL make the current content the remembered content. A review comment added, edited, or deleted through the dashboard SHALL NOT appear as a change when the document was read immediately before that save. Change cards SHALL indicate documents updated since read. Remembered content SHALL be stored only in the dashboard's private local data, SHALL be bounded in the number of documents and total size by discarding the least recently read entries, and SHALL be removable without affecting the journal, configuration, or documents. Indicators SHALL be labeled as differences from the user's last read, not as recorded work. Comparison work SHALL be bounded so heavily rewritten documents do not cause prolonged synchronous interface stalls. When exact differences cannot be computed within that bound, the reader SHALL keep the current text readable, identify the comparison as incomplete, visibly mark the current document as changed in rendered and source views, and retain usable change navigation. It SHALL NOT present an incomplete comparison as an unchanged document.

#### Scenario: Agent edits a reviewed design
- **WHEN** the user marked a design as read, and an agent later rewrites one section and adds another
- **THEN** reopening the design highlights the rewritten and added sections and shows that the document changed since last read

#### Scenario: Addressed comment
- **WHEN** a REVIEW marker present at the last read has since been removed by the agent
- **THEN** the reader lists that comment with its quote as removed since last read

#### Scenario: Mark as read
- **WHEN** the user selects Mark as read
- **THEN** the highlights and the updated-since-read indicator clear until the document changes again

#### Scenario: Own comment is not a change
- **WHEN** the user reads a document and then saves a new review comment in it
- **THEN** the reader does not highlight that comment as a change since last read

#### Scenario: First open
- **WHEN** the user opens a document for the first time
- **THEN** no changes are highlighted and the current content becomes the read baseline

#### Scenario: Retention bound reached
- **WHEN** remembered content exceeds the retention bound
- **THEN** the least recently read documents lose their baseline and are treated as first opens on their next visit

#### Scenario: Exact comparison exceeds its computation bound
- **WHEN** a heavily rewritten document cannot be compared exactly within the allowed computation bound
- **THEN** the reader shows an incomplete-comparison explanation and a visible document-level changed indication in rendered and source views while remaining responsive

#### Scenario: Normal comparison remains precise
- **WHEN** a document has a small edit that can be compared within the bound
- **THEN** the reader retains precise added, rewritten, and removed passage navigation rather than replacing it with a whole-document indication
