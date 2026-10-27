# Spec Delta

## Purpose

Give the user a fast, factual digest of their last recorded working day and current work for daily stand-up preparation, built only from recorded observations and copyable as concise text.

## ADDED Requirements

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
For the selected day, the Daily view SHALL show one line per work item with observations on that day, using ticket grouping. Each line SHALL show the item title, its tickets with links when available, associated change names, its latest recorded status as of the end of that day, and the summary of its latest observation on that day, or an explicit no-summary label. Other observations of that item on that day SHALL be collapsed and available on request. Lines SHALL be ordered by status (done, in progress, started), then by latest observation time, newest first. The digest SHALL NOT infer working hours, completion, or progress beyond recorded observations, and SHALL NOT treat a later observation as already recorded on the selected day.

#### Scenario: Several observations for one ticket
- **WHEN** a ticket has three observations on the selected day, the last of which is done
- **THEN** the digest shows one done line with the last observation's summary and the other two available on request

#### Scenario: Later completion
- **WHEN** a ticket was in progress at the end of the selected day and recorded done the next day
- **THEN** the digest for the selected day shows it as in progress

### Requirement: Today and blockers
The Daily view SHALL list under Today the work items whose latest recorded status is not done and that have an observation within the last 14 days, plus work items with observations recorded today, without duplicates, ordered by latest observation time. Active OpenSpec changes without observations SHALL NOT be listed individually under Today; the view MAY indicate how many exist. The Blockers section SHALL be empty with a prompt for the user and SHALL NOT be derived from recorded data.

#### Scenario: Work in progress continues
- **WHEN** a ticket's latest observation, recorded yesterday, is in_progress
- **THEN** it appears under Today

#### Scenario: Completed work does not continue
- **WHEN** a ticket's latest observation is done
- **THEN** it does not appear under Today unless it has an observation recorded today

### Requirement: Copy for daily
The Daily view SHALL provide an action that copies the Yesterday, Today, and Blockers sections as concise text, in a Markdown or plain-text format selected by the user, with ticket links included or omitted as selected. The format and link choices SHALL be remembered. The copied Yesterday section SHALL name the selected date. Each copied line SHALL contain only recorded facts: title or ticket IDs, recorded status, and the latest summary. The Blockers section SHALL be copied as an empty placeholder.

#### Scenario: Copy as plain text without links
- **WHEN** the user selects plain text without links and copies
- **THEN** the clipboard contains the three sections with ticket IDs, statuses, and summaries, and no URLs

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
- **THEN** Today reflects it on the next refresh
