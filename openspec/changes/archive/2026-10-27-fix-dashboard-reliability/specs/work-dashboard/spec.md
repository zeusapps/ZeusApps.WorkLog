# Spec Delta

## MODIFIED Requirements

### Requirement: Ongoing work and history navigation
The dashboard SHALL provide Ongoing work and History views. Ongoing work SHALL list discovered active OpenSpec changes even when no activity was logged, plus work items whose latest recorded status is not done and that have an observation within the last 14 days. History SHALL expose recorded activity and associated active or archived documents, and SHALL default to the last 30 days, the configured reporting timezone (or the system timezone when none is configured), and ticket grouping. History SHALL offer relative ranges of the last 7, 30, and 90 days and a custom range. A relative range SHALL be evaluated against the current date in the selected timezone each time History loads or refreshes, including after midnight while the dashboard stays open. A custom range SHALL keep its explicit dates. The selected range kind, grouping, and scale SHALL be remembered. The timezone SHALL be remembered only after the user explicitly changes it. Focusing History on a single activity day SHALL be temporary, SHALL NOT replace the remembered range, and SHALL offer a control that returns to it. Invalid timezones and ranges whose start follows their end SHALL be rejected with an inline explanation, without being remembered or sent as queries. A remembered range saved by an earlier version without a range kind SHALL be treated as the default relative 30-day range. Archive state, task checkbox progress, and latest recorded work status SHALL be presented separately. An unlogged change SHALL NOT acquire activity dots or an inferred elapsed work span from its presence on disk.

#### Scenario: Review an unlogged proposal
- **WHEN** an active OpenSpec change has documents but no journal observations
- **THEN** it appears in Ongoing work with available documents and no fabricated work history

#### Scenario: Recent work without an OpenSpec change
- **WHEN** a ticket has an in_progress observation from three days ago and no associated OpenSpec change
- **THEN** it appears in Ongoing work with its recorded status and no document links

#### Scenario: Default history view
- **WHEN** the user opens History without saved history preferences
- **THEN** it shows the last 30 days in the configured reporting timezone grouped by ticket

#### Scenario: Relative range rolls forward
- **WHEN** the user opened History with the default range on one day and reopens the dashboard three days later
- **THEN** History shows the 30 days ending on the new current date, including observations recorded since the first visit

#### Scenario: Custom range is kept
- **WHEN** the user selects explicit From and Through dates and later reopens the dashboard
- **THEN** History shows the same explicit dates

#### Scenario: Day focus is temporary
- **WHEN** the user focuses History on one activity day and then reloads the dashboard
- **THEN** History shows the previously remembered range, and before the reload a visible control returns to that range

#### Scenario: Invalid timezone
- **WHEN** the user enters a timezone name that the browser cannot resolve
- **THEN** the dashboard explains the problem next to the field, keeps showing the previous results, and does not remember the invalid value

#### Scenario: Day without a midnight
- **WHEN** the selected timezone skips 00:00 on a day at a range boundary
- **THEN** the range starts or ends at the first instant of that local day, without including an hour of the neighboring day

#### Scenario: Legacy remembered range
- **WHEN** the saved preferences contain explicit From and Through dates without a range kind
- **THEN** History uses the default relative 30-day range

#### Scenario: Archived work without recorded completion
- **WHEN** a change is archived but its last journal observation is in_progress
- **THEN** its document state is archived and its work state remains labeled as the latest recorded in_progress observation

## ADDED Requirements

### Requirement: Bounded dashboard preferences
Dashboard preferences SHALL remain within their storage limit regardless of how many documents the user opens. Per-document reading positions and remembered document selections SHALL retain only a bounded number of most recently used entries and discard older ones. A preference save that fails SHALL be reported once as a non-blocking notice and SHALL NOT produce a repeated error for each later scroll or interaction. A failed save SHALL NOT discard the in-memory preferences of the current visit.

#### Scenario: Many documents are read
- **WHEN** the user opens and scrolls through more documents than the retention bound
- **THEN** preferences continue to save successfully, and the most recently read documents keep their reading positions

#### Scenario: Preference save fails
- **WHEN** the preferences endpoint rejects a save
- **THEN** one notice explains that preferences could not be saved, and continued scrolling produces no further error banners

### Requirement: Idle refresh cost
While the dashboard is visible and neither OpenSpec documents nor the journal change, background refresh SHALL NOT repeat document search, timeline queries, or OpenSpec CLI invocations on every poll. OpenSpec root resolution and task progress SHALL be refreshed when their inputs change or after a bounded refresh interval of at most one minute. The selected document SHALL still reflect an external edit within a few seconds. Search results and History SHALL refresh when document contents or recorded observations change. CLI invocations needed for discovery SHALL NOT be serialized across independent sources. Refreshing an unchanged selected document SHALL NOT transfer its full contents again, and an idle dashboard SHALL NOT rewrite its saved preferences.

#### Scenario: Idle dashboard
- **WHEN** the dashboard stays open for one minute with no document or journal changes
- **THEN** it performs no repeated searches or timeline queries, does not resend the unchanged open document, does not rewrite saved preferences, and performs at most one round of OpenSpec CLI resolution per source

#### Scenario: Agent edits the open document
- **WHEN** an agent saves a change to the open design while the dashboard is idle
- **THEN** the reader shows the update within a few seconds

#### Scenario: Agent logs work
- **WHEN** a new observation is recorded while History is visible
- **THEN** History reflects it on the next refresh without the user reloading the page
