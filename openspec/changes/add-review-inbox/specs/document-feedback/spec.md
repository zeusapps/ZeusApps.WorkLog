# Spec Delta

## ADDED Requirements

### Requirement: Open review comments overview
The dashboard SHALL list the REVIEW markers currently present in change documents within the selected repositories, defaulting to active changes, with an explicit scope of active, archived, or all changes. Each entry SHALL identify repository, change, document, archive state, the quoted passage when the marker has one, and the comment text, and SHALL open the document at the marker's position. Entries SHALL be grouped by change and document. Change cards and the reader SHALL show the number of open comments for that change and document. A comment is open while its marker is present in the file; the dashboard SHALL NOT keep a separate resolution state. Markers inside code examples SHALL NOT be listed. Sources or documents that could not be read SHALL be reported as unavailable instead of being counted as having no comments. The overview SHALL refresh when document contents change.

#### Scenario: Comments across changes
- **WHEN** two active changes in the selected repository contain three REVIEW markers in total
- **THEN** the overview lists all three under their changes and documents, and each change card shows its count

#### Scenario: Open a listed comment
- **WHEN** the user selects a listed comment
- **THEN** the reader opens that document scrolled to the comment's note

#### Scenario: Agent removes a marker
- **WHEN** the agent deletes a REVIEW marker while the overview is visible
- **THEN** the comment disappears from the overview and the counts decrease after the next refresh

#### Scenario: Comments left in an archived change
- **WHEN** the user switches the overview to archived scope and an archived change still contains a marker
- **THEN** the marker is listed with its archived state and offers no edit or delete action

#### Scenario: Code example is not a comment
- **WHEN** a document shows REVIEW marker syntax inside a fenced code block
- **THEN** the overview does not list it
