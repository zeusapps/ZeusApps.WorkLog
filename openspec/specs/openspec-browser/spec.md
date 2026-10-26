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
While a change is open, the dashboard SHALL detect external document updates and refresh the reader, outline, task progress, and subsequent search results. Refresh SHALL preserve the selected document and reading position when its target still exists, and SHALL preserve an unsaved review comment being typed. An archive move SHALL resolve the same selected change to its new directory. If a document disappears, changes while being read, or exceeds the document limit, the UI SHALL identify the condition and allow navigation or retry without presenting partial content as a complete file.

#### Scenario: Agent updates a document
- **WHEN** the agent changes a design while the user reads a heading that remains present
- **THEN** the reader refreshes the content and retains that heading and approximate reading position

#### Scenario: Change is archived while open
- **WHEN** the selected active change moves into the source's archive directory
- **THEN** the reader continues to resolve that change, updates its archive state, and makes the documents read-only for review comments

#### Scenario: Switch documents during background refresh
- **WHEN** a background refresh overlaps a document selection or successive file selections finish out of order
- **THEN** the latest explicit selection determines the visible document, including nested spec files, without reverting to the earlier file

### Requirement: Scoped document search
The dashboard SHALL search Markdown contents across discovered changes within the selected repositories and an explicit active, archived, or all-changes scope. Search SHALL be case-insensitive literal text search, return matching passages with repository, change, document, section when available, and archive state, and open the exact document location on selection. Search SHALL return at most a bounded number of matches and indicate when that limit was reached, distinguish no matches from incomplete or unavailable sources, and refresh results when source content changes. It SHALL not search arbitrary files outside known OpenSpec roots.

#### Scenario: Search across selected repositories
- **WHEN** the user selects two repositories and searches for a phrase in active changes
- **THEN** only matching active-change documents associated with those repositories appear, with passages and source labels

#### Scenario: Open a result in a document without headings
- **WHEN** a match occurs in a Markdown document without headings
- **THEN** selecting the result opens the matching passage using a document position fallback

#### Scenario: Search is incomplete
- **WHEN** matching sources are unavailable or the result count exceeds one response
- **THEN** the interface identifies unavailable sources or indicates that the match limit was reached instead of presenting a complete empty result or silently truncating matches

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
Repository filters, change selection, document tabs, heading navigation, search results, review comment controls, and link-copy controls SHALL be keyboard operable with visible focus. Browser back and forward navigation SHALL preserve meaningful document routes, and timeline facts SHALL remain available as readable text without relying exclusively on color, hover, or pointer interactions.

#### Scenario: Navigate without a pointer
- **WHEN** the user selects a change, opens a document, searches, and follows a result using only the keyboard
- **THEN** each action is available, focus remains visible, and the matching document location is reachable
