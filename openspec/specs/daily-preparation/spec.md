# daily-preparation Specification

## Purpose

Give the user a fast, factual digest of their last recorded working day and of today's work so far for daily stand-up preparation, built only from recorded observations and copyable as concise text.

## Requirements

### Requirement: Last recorded day
The Daily view SHALL select, by default, the most recent local date before the current date in the selected timezone that has at least one recorded observation in the selected repositories. It SHALL identify that date and label it as the last recorded day. It SHALL allow moving to the previous and next recorded days, skipping dates without observations, and SHALL NOT move past the day before the current date. When no earlier observations exist, it SHALL say so. Day boundaries SHALL use the selected timezone.

#### Scenario: Monday morning
- **WHEN** the user opens the Daily view on Monday and the latest earlier observations were recorded on Friday
- **THEN** the view shows Friday as the last recorded day

#### Scenario: Day off
- **WHEN** no observations were recorded on Wednesday and Thursday and the user opens the Daily view on Friday
- **THEN** the view shows Tuesday as the last recorded day

#### Scenario: Step to an earlier day
- **WHEN** the user selects the previous recorded day
- **THEN** the view shows the nearest earlier date with observations

#### Scenario: Empty journal
- **WHEN** no observations were recorded before today in the selected repositories
- **THEN** the view states that no earlier recorded work exists

### Requirement: Concise yesterday digest
For the selected day, the Daily view SHALL show one line per work item with observations on that day, using ticket grouping. Each line SHALL show the item title, its tickets with links when available, associated change names, its latest recorded status as of the end of that day, and the summary of its latest observation on that day, or an explicit no-summary label. Other observations of that item on that day SHALL be collapsed and available on request. An observation with neither a ticket nor a change SHALL be shown as its own line, titled with its summary or an explicit no-summary label, with no collapsed observations. Lines SHALL be ordered by status (done, in progress, started), then by latest observation time, newest first. The digest SHALL NOT infer working hours, completion, or progress beyond recorded observations, and SHALL NOT treat a later observation as already recorded on the selected day.

#### Scenario: Several observations for one ticket
- **WHEN** a ticket has three observations on the selected day, the last of which is done
- **THEN** the digest shows one done line with the last observation's summary and the other two available on request

#### Scenario: Later completion
- **WHEN** a ticket was in progress at the end of the selected day and recorded done the next day
- **THEN** the digest for the selected day shows it as in progress

#### Scenario: Unlinked work on the selected day
- **WHEN** two observations with neither a ticket nor a change were recorded on the selected day
- **THEN** the digest shows two lines, each titled with its own summary

### Requirement: Today and blockers
The Daily view SHALL derive Today from the Ongoing board of the same repository selection. Today SHALL list the board's In progress cards, except cards showing the quiet indication, plus cards from any column with an observation recorded today, each card once, in the board's priority order. Proposed change cards SHALL NOT be listed under Today; the view MAY indicate how many exist. A Today card with observations recorded today SHALL show its latest recorded status and the summary of its latest observation today, or an explicit no-summary label, with its other observations from today collapsed and available on request; a Today card with no observation recorded today SHALL show its title and tickets only. A Today line SHALL contain only observations recorded today for that card's own ticket or change. An item MAY appear under both Yesterday and Today. Today and the copy action SHALL NOT be available until the board inputs for the current repository selection have loaded. The Blockers section SHALL be empty with a prompt for the user and SHALL NOT be derived from recorded data.

#### Scenario: Work in progress continues
- **WHEN** a ticket's latest observation, recorded yesterday, is in_progress
- **THEN** it appears under Today

#### Scenario: Completed work does not continue
- **WHEN** a ticket's latest observation is done, or an active change has all of its tasks ticked
- **THEN** it does not appear under Today unless it has an observation recorded today

#### Scenario: Today follows the priority order
- **WHEN** two In-progress cards are ranked on the board with the second above the first
- **THEN** Today lists them in that ranked order

#### Scenario: Quiet work is left out
- **WHEN** an active change's latest observation is in_progress and was recorded 20 days ago
- **THEN** it does not appear under Today

#### Scenario: Work recorded today shows its outcome
- **WHEN** a ticket listed under Today has two observations recorded today, the latest in_progress with a summary
- **THEN** its Today line shows in progress and that summary, with the earlier observation available on request

#### Scenario: Continuing work with nothing recorded today
- **WHEN** an In-progress card listed under Today has no observation recorded today
- **THEN** its Today line shows its title and tickets only

#### Scenario: A ticket used with two changes
- **WHEN** a ticket was logged with change alpha last week and with change beta today, and both changes are In progress
- **THEN** beta's Today line shows today's summary and alpha's Today line shows its title only

#### Scenario: Board still loading
- **WHEN** the Daily view opens and the board inputs have not loaded yet
- **THEN** Today shows a loading state and Copy for daily is unavailable

#### Scenario: Work spanning both days
- **WHEN** a ticket has observations recorded on the last recorded day and today
- **THEN** it appears under Yesterday with that day's latest summary and under Today with today's latest summary

### Requirement: Copy for daily
The Daily view SHALL provide an action that copies the Yesterday, Today, and Blockers sections as concise text, in a Markdown or plain-text format selected by the user, with ticket links included or omitted as selected. The format and link choices SHALL be remembered. The copied Yesterday section SHALL name the selected date. Each copied line SHALL contain only recorded facts: title or ticket IDs, recorded status, and the latest summary of that line's day. A Today card without an observation recorded today SHALL be copied as its title or ticket IDs only. The Blockers section SHALL be copied as an empty placeholder.

#### Scenario: Copy as plain text without links
- **WHEN** the user selects plain text without links and copies
- **THEN** the clipboard contains the three sections with ticket IDs, statuses, and summaries for Yesterday lines and Today lines recorded today, and no URLs

#### Scenario: Copy as Markdown with links
- **WHEN** the user selects Markdown with links and copies
- **THEN** ticket IDs with known URLs are Markdown links

### Requirement: Shared scope and responsiveness
The Daily view SHALL use the shared repository selection and the History timezone, without a separate scope setting. It SHALL load without invoking agents, external services, or OpenSpec CLI commands beyond the dashboard's existing catalog refresh, and SHALL refresh when new observations are recorded.

#### Scenario: Work spans two repositories
- **WHEN** the selection includes all repositories and the last recorded day has observations in two repositories
- **THEN** the digest shows lines from both, each identifying its repository

#### Scenario: New observation recorded
- **WHEN** an agent records an observation for today while the Daily view is open
- **THEN** Today reflects it, including its summary, on the next refresh
