# Spec Delta

## Purpose

Let users review OpenSpec documents, primarily designs after agent edits finish, by writing comments directly into active change documents for the agent to read.

## ADDED Requirements

### Requirement: Inline review comments
The reader SHALL allow the user to select a rendered passage of an active change document, write a comment, and save it. The supported review workflow SHALL assume the agent or other editor has finished editing the document before the user saves review comments. Saving SHALL insert a standalone REVIEW Markdown comment, separated by a blank line, immediately after the selected block. New markers SHALL contain a unique comment identifier, an explicit reference to the preceding passage, a bounded quoted excerpt, and a separate comment body so the association is clear in the raw file. Marker contents SHALL be escaped so they cannot terminate the marker early. Within that workflow, saving SHALL preserve other content of the latest reread file and SHALL NOT change task checkboxes, journal observations, or an external conversation. Commenting SHALL remain available for other active change Markdown documents without requiring a task-centered review workflow.

#### Scenario: Comment on a design passage
- **WHEN** the user selects a design paragraph, writes a comment, and saves
- **THEN** the design file contains a REVIEW marker with that comment directly after the paragraph and is otherwise unchanged

#### Scenario: Comment contains marker syntax
- **WHEN** the comment text contains `-->`
- **THEN** the inserted marker remains a single well-formed Markdown comment containing the full comment text

### Requirement: Review comment writes with best-effort conflict detection
The dashboard SHALL write review comments only into Markdown files inside discovered active changes of available sources, after canonical containment checks. Archived change documents SHALL be read-only. A save SHALL reread the document and be rejected as a conflict when its revision changed and the selected block no longer matches its source lines; the user's comment text SHALL be retained for retry after the document reloads. An unrelated earlier edit that leaves the block unchanged at its source lines SHALL be retained by inserting into the latest reread contents. The write SHALL replace the file atomically and preserve its encoding and line endings. Documentation SHALL state that an external edit after the final reread can still be overwritten and that simultaneous document editing is outside the supported workflow. The dashboard SHALL NOT claim concurrent-write protection or require agent-idle detection or an editor locking protocol.

#### Scenario: Agent edits the document before saving
- **WHEN** an agent rewrites the selected section after the user opened the document and before the save's final reread
- **THEN** the save is rejected without modifying the file, the document reloads, and the typed comment remains available

#### Scenario: Earlier unrelated edit is retained
- **WHEN** an agent finishes editing another part of the file before the save rereads it and the selected block still matches its source lines
- **THEN** the saved file includes that earlier edit and the inserted review comment

#### Scenario: Review workflow limitation is documented
- **WHEN** the user reads the dashboard's review documentation
- **THEN** it explains reviewing after agent edits finish and the remaining race between rereading and replacing a document, without promising simultaneous-edit safety

#### Scenario: Archived change
- **WHEN** the user views a document in an archived change
- **THEN** the reader offers no comment action and the service rejects comment writes for that document

#### Scenario: Write outside a change
- **WHEN** a request targets a path outside a discovered active change, including through encoded traversal or filesystem links
- **THEN** the service rejects it without writing any file

### Requirement: Display existing review comments
The reader SHALL render existing REVIEW markers as visible notes next to the passage they follow, in both rendered and source views, including legacy plain markers. Notes in active documents SHALL expose keyboard-accessible Edit and Delete actions. Editing SHALL preload the existing body and update only that marker; deleting SHALL require a local confirmation and remove only the chosen marker. The service SHALL reread and reject mutations when the selected marker changed, disappeared, or its identifier is ambiguous, retaining unsaved edits for retry. Unrelated earlier edits, encoding, and line endings SHALL be retained. Legacy markers SHALL remain editable and gain an explicit passage reference when edited. Code-fence examples SHALL NOT become editable comments. Archived notes SHALL remain read-only. Review resolution tracking and agent notification remain outside scope.

#### Scenario: Read the raw reference
- **WHEN** the agent opens a newly reviewed raw Markdown file
- **THEN** the marker explicitly identifies the preceding passage and quotes its content separately from the feedback

#### Scenario: Edit or delete a saved note
- **WHEN** the user edits an existing active-document note or confirms its deletion
- **THEN** only that marker is replaced or removed, and the refreshed reader reflects the change

#### Scenario: Marker changed before mutation
- **WHEN** the agent edits or removes the selected marker before an edit or delete request rereads the file
- **THEN** the operation reports a conflict without changing the file and retains the user's edited text

#### Scenario: Agent reads the review
- **WHEN** a REVIEW marker was saved and the agent later reads the document
- **THEN** the comment appears in the file at the reviewed passage without any dashboard involvement

#### Scenario: Reopen a reviewed document
- **WHEN** the user opens a document containing REVIEW markers
- **THEN** each comment is shown as a note beside its passage
