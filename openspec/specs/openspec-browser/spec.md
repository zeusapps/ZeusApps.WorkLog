# openspec-browser Specification

## Purpose

Make local OpenSpec changes easy to discover, read, search, and reference during implementation and after their documents have been archived.

## Requirements

### Requirement: Resolve local OpenSpec sources
The dashboard SHALL associate available checkouts with saved repository identities and resolve their project-local or configured standalone-store OpenSpec roots. It SHALL discover active and archived changes within those roots without requiring journal entries. Different worktrees or clones with conflicting document copies SHALL remain explicit source choices rather than being silently merged or selected. A missing checkout, missing OpenSpec CLI, unresolved store, or unavailable root SHALL produce a source-specific explanation while other sources and journal history remain usable. Discovery SHALL not initialize OpenSpec, change repository identities, or modify repository configuration.

#### Scenario: Standalone store supplies documents
- **WHEN** a configured repository resolves its OpenSpec planning root to a registered standalone store
- **THEN** the dashboard reads the change documents from that store and identifies their source

#### Scenario: Several checkouts contain different documents
- **WHEN** two available sources associated with a repository contain different versions of the same change
- **THEN** the dashboard exposes the source choice and the selected document identifies the source being read

#### Scenario: OpenSpec tooling is unavailable
- **WHEN** the OpenSpec CLI is unavailable but a source has an unambiguous project-local openspec directory without a store declaration
- **THEN** the dashboard can browse that local directory, while a source requiring unresolved store configuration receives an explanation rather than a guessed fallback

### Requirement: Render available change documents
For each selected change, the dashboard SHALL provide available proposal, design, tasks, and recursively discovered spec Markdown documents, plus other Markdown artifacts present in the selected change directory. Without an explicit document target or valid remembered selection, it SHALL open design.md when available, then proposal.md, then the first available Markdown artifact. It SHALL render headings, lists, code blocks, tables, and checkboxes, provide section navigation and access to source text, and resolve permitted relative links within the known OpenSpec source. It SHALL show missing or unreadable documents explicitly without preventing access to the rest of the change. Ticket-grouped work associated with several change names SHALL expose each associated change's documents. Document content SHALL be identified as the current local file contents, including when viewed beside older activity.

#### Scenario: Read a nested spec
- **WHEN** a change contains specs/reporting/export/spec.md
- **THEN** the document is available with its full relative identity and rendered content

#### Scenario: Design is absent
- **WHEN** a selected change has proposal and tasks files but no design file
- **THEN** the reader exposes the available files and indicates that the design is unavailable

#### Scenario: Open a change for design review
- **WHEN** the user selects a change containing a design and tasks document without an explicit document target or remembered document selection
- **THEN** the reader opens the design while keeping tasks and other available documents accessible

#### Scenario: Ticket has multiple changes
- **WHEN** a ticket's recorded observations reference two changes
- **THEN** the user can choose either change's available documents without losing the ticket activity context

### Requirement: Separate document progress from recorded completion
The dashboard SHALL use OpenSpec-reported completed and total task counts for active changes when the read-only CLI supplies them, including counts across schema-selected tracked task files. It SHALL identify the counts as OpenSpec task progress and SHALL NOT substitute a separate renderer-based or locally filtered progress total. When authoritative counts cannot be obtained, including for archived changes absent from the CLI result, it SHALL display progress as unavailable rather than zero while retaining access to readable documents. Task positions and numbers extracted for navigation SHALL NOT drive progress totals. Task progress SHALL not update journal status, infer reporting completion, or change document contents.

#### Scenario: Task totals follow OpenSpec
- **WHEN** OpenSpec reports counts for tracked task files containing x/X markers, unfamiliar markers, or checkbox-shaped examples
- **THEN** the dashboard presents those reported counts without reinterpreting comment or code-block contents

#### Scenario: Task counts are unavailable
- **WHEN** the CLI cannot provide counts for an archived change or an active source
- **THEN** the dashboard identifies progress as unavailable and still permits reading available tasks and design files

#### Scenario: Fully checked tasks without a done observation
- **WHEN** OpenSpec reports every tracked task completed but no done observation was logged
- **THEN** the reader shows full task progress and separately indicates the recorded work status without manufacturing completion

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

### Requirement: Scoped document search
The dashboard SHALL search Markdown contents across discovered changes within the selected repositories and an explicit active, archived, or all-changes scope. Search SHALL be case-insensitive literal text search, return matching passages with repository, change, document, section when available, and archive state, and open the exact document location on selection. Section labels SHALL come from Markdown headings and SHALL NOT come from lines inside fenced code. Search SHALL return at most a bounded number of matches and indicate when that limit was reached, distinguish no matches from incomplete or unavailable sources, and refresh results when source content changes. Unavailable-source notices SHALL be limited to sources associated with the selected repositories and to documents within the searched scope; notices about repositories outside the selection SHALL NOT mark a search incomplete. It SHALL not search arbitrary files outside known OpenSpec roots.

#### Scenario: Search across selected repositories
- **WHEN** the user selects two repositories and searches for a phrase in active changes
- **THEN** only matching active-change documents associated with those repositories appear, with passages and source labels

#### Scenario: Open a result in a document without headings
- **WHEN** a match occurs in a Markdown document without headings
- **THEN** selecting the result opens the matching passage using a document position fallback

#### Scenario: Search is incomplete
- **WHEN** matching sources are unavailable or the result count exceeds one response
- **THEN** the interface identifies unavailable sources or indicates that the match limit was reached instead of presenting a complete empty result or silently truncating matches

#### Scenario: Unrelated repository lacks a source
- **WHEN** a repository outside the current selection has no available OpenSpec source and the user searches the selected repository
- **THEN** the results are not marked incomplete because of that repository

#### Scenario: Comment line inside code
- **WHEN** a match follows a fenced code block containing a line that starts with `#`
- **THEN** the match's section label is the nearest preceding Markdown heading

### Requirement: Change and document deep links
The dashboard SHALL support copying and opening links to a repository/source-qualified change, document, heading, or task. Target identity SHALL be independent of an active directory's physical archive path and SHALL preserve the document's relative path. Task targets SHALL use the current OpenSpec task number when the task has one, falling back to the task text and its occurrence within its section. Documentation SHALL describe task links as best-effort references to the current document: renumbering or number reuse can change their destination, and historical task identity is not guaranteed. Links SHALL not contain session authorization secrets. A link target SHALL be resolved after archiving and across dashboard restarts at the same endpoint; missing or ambiguous heading/task targets SHALL open the nearest available document context and identify the unresolved target. Opening a deep link outside the current repository filter SHALL explicitly reveal the linked source. Ambiguous active/archive or source matches SHALL require a source choice.

#### Scenario: Follow a link after archiving
- **WHEN** a link was copied for an active change and that change is later archived
- **THEN** the link opens the same document target in the archived change

#### Scenario: Target heading was removed
- **WHEN** a document still exists but its linked heading no longer exists
- **THEN** the link opens the document and identifies the missing section target

#### Scenario: Numbered task text is edited
- **WHEN** a link targets task 2.3 and that task's wording is later changed while its number remains
- **THEN** the link opens task 2.3 in its current wording

#### Scenario: Task number no longer resolves uniquely
- **WHEN** a copied task link's number is missing or duplicated in the current document
- **THEN** the reader opens the available section or document and identifies the missing or ambiguous task target

#### Scenario: Task numbering changes over time
- **WHEN** the user reads the task-link documentation
- **THEN** it explains that task links follow current numbering and OpenSpec numbering validation does not preserve historical task identity

#### Scenario: Link targets another repository
- **WHEN** the user opens a link to a known repository excluded by the current filter
- **THEN** the dashboard reveals and identifies that repository to display the linked content without silently dropping the navigation

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

### Requirement: Archive view
The dashboard SHALL provide an Archive view listing archived changes discovered in the sources of the selected repositories. Entries SHALL be ordered by archive date, newest first, using the date prefix of the archive directory, and SHALL show the logical change name, archive date, available documents, source, the latest recorded observation status and date for that change name and repository when the journal has one, and the number of review comments remaining in its documents. Entries without a recognizable date prefix SHALL be listed after dated entries. The view SHALL provide a case-insensitive name filter. Selecting an entry SHALL open the read-only reader for that change using the default document rules. An archived change without journal observations SHALL show that no activity was recorded rather than an inferred status.

#### Scenario: Browse recent archives
- **WHEN** the selected repository has three archived changes archived on different dates
- **THEN** the Archive view lists them newest first with their archive dates and opens the selected one read-only

#### Scenario: Archive with recorded activity
- **WHEN** an archived change's latest observation is done on a recorded date
- **THEN** its entry shows recorded done with that date, separately from its archive date

#### Scenario: Leftover comments
- **WHEN** an archived change still contains a REVIEW marker
- **THEN** its entry shows the remaining comment count

#### Scenario: Filter by name
- **WHEN** the user types part of a change name into the filter
- **THEN** only archived changes whose names contain that text remain listed

### Requirement: Open documents in a local editor
The reader SHALL offer an action that opens the current document in a local editor at the current reading position, and SHALL offer the same action for headings, search results, and review-comment entries at their positions. The editor SHALL be chosen from a fixed set of presets: VS Code, Cursor, and the system default application. The chosen preset SHALL be remembered. Line positions SHALL be honored by presets that support them; the system default MAY open the file without a position. The action SHALL be available for active and archived documents. If the editor cannot be launched, the dashboard SHALL report the failure and SHALL NOT change any file.

#### Scenario: Open at reading position
- **WHEN** the user reads a design scrolled to a section and selects Open in editor with VS Code chosen
- **THEN** VS Code opens that design file at the first line of the visible section

#### Scenario: Open a search result
- **WHEN** the user selects Open in editor on a search result
- **THEN** the chosen editor opens the matching document at the matching line

#### Scenario: Editor is unavailable
- **WHEN** the chosen editor is not installed
- **THEN** the dashboard reports that the editor could not be opened and suggests choosing another preset

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
