# Spec Delta

## ADDED Requirements

### Requirement: Changes since last read
The dashboard SHALL remember, per document, the content the user last marked as read. Opening a document with no remembered content SHALL record its current content as read. When the current content differs from the remembered content, the reader SHALL indicate that the document changed since it was last read, highlight added and changed passages in rendered and source views, show the positions of removed passages with their removed text available, and allow keyboard navigation between changes. The reader SHALL list REVIEW markers present in the remembered content but absent from the current content as removed since last read, showing their quote and comment. A "Mark as read" action SHALL make the current content the remembered content. A review comment added, edited, or deleted through the dashboard SHALL NOT appear as a change when the document was read immediately before that save. Change cards SHALL indicate documents updated since read. Remembered content SHALL be stored only in the dashboard's private local data, SHALL be bounded in the number of documents and total size by discarding the least recently read entries, and SHALL be removable without affecting the journal, configuration, or documents. Indicators SHALL be labeled as differences from the user's last read, not as recorded work.

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
