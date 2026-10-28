# Spec Delta

## ADDED Requirements

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
