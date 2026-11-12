# Spec Delta

## ADDED Requirements

### Requirement: Keyboard focus during Retry updates
Activating Retry updates from the keyboard SHALL keep focus within the same surface (the reader when Retry is in the reader, otherwise the page status area or main content) for the whole recovery attempt, including when the Retry control is disabled or removed while recovery runs. Focus SHALL NOT fall to the document body. When focus was elsewhere at activation, for example in an unsaved comment draft, Retry SHALL NOT move it.

#### Scenario: Keyboard Retry in the reader
- **WHEN** a keyboard user presses Enter on Retry updates in the open reader while updates are paused
- **THEN** focus stays inside the reader throughout recovery, and the reader stays usable without pointer input

#### Scenario: Keyboard Retry on the page
- **WHEN** a keyboard user activates Retry updates in the page-level status bar
- **THEN** focus moves to a focusable element of the page instead of the document body while recovery runs

#### Scenario: Focus elsewhere is preserved
- **WHEN** Retry updates is triggered by pointer while keyboard focus is in an unsaved comment draft
- **THEN** focus and the draft text remain where they were

### Requirement: Monotonic pending live delivery
For each browser connection, the dashboard SHALL keep at most one undelivered live update, and that update SHALL only move forward while the server instance is unchanged. A late-arriving older update SHALL merge into the pending one without lowering any domain revision or dropping pending change hints or a pending checkpoint. An update from a new server instance SHALL replace the pending one. A browser SHALL NOT have to wait for a later checkpoint to receive a newer revision that was already offered.

#### Scenario: Older update arrives after a newer one
- **WHEN** an update with a newer journal revision is pending for a connection and an update with an older journal revision is then offered for the same server instance
- **THEN** the delivered update carries the newer journal revision, the union of both updates' change hints, and the highest pending checkpoint

#### Scenario: Server instance changes
- **WHEN** an update from a different server instance is offered while an older instance's update is pending
- **THEN** the new instance's update replaces the pending one
