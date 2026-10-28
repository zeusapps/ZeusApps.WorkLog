# Spec Delta

## MODIFIED Requirements

### Requirement: Ongoing work and history navigation
The dashboard SHALL provide Ongoing work and History views. Ongoing work SHALL present a board that lists discovered active OpenSpec changes even when no activity was logged, plus ticket and ad-hoc work items with an observation within the last 7 days, as defined by the Ongoing board requirement. History SHALL expose recorded activity and associated active or archived documents, and SHALL default to the last 30 days, the configured reporting timezone (or the system timezone when none is configured), and ticket grouping. History SHALL offer relative ranges of the last 7, 30, and 90 days and a custom range. A relative range SHALL be evaluated against the current date in the selected timezone each time History loads or refreshes, including after midnight while the dashboard stays open. A custom range SHALL keep its explicit dates. The selected range kind, grouping, and scale SHALL be remembered. The timezone SHALL be remembered only after the user explicitly changes it. Focusing History on a single activity day SHALL be temporary, SHALL NOT replace the remembered range, and SHALL offer a control that returns to it. Invalid timezones and ranges whose start follows their end SHALL be rejected with an inline explanation, without being remembered or sent as queries. A remembered range saved by an earlier version without a range kind SHALL be treated as the default relative 30-day range. Archive state, task checkbox progress, and latest recorded work status SHALL be presented separately. An unlogged change SHALL NOT acquire activity dots or an inferred elapsed work span from its presence on disk.

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

### Requirement: Bounded dashboard preferences
Dashboard preferences SHALL remain within their storage limit regardless of how many documents the user opens or how many cards the user ranks. Per-document reading positions and remembered document selections SHALL retain only a bounded number of most recently used entries and discard older ones. The board priority order SHALL retain at most 500 entries; when it is saved, entries for cards no longer on the board SHALL be discarded first, then the lowest-ranked entries. A preference save that fails SHALL be reported once as a non-blocking notice and SHALL NOT produce a repeated error for each later scroll or interaction. A failed save SHALL NOT discard the in-memory preferences of the current visit.

#### Scenario: Many documents are read
- **WHEN** the user opens and scrolls through more documents than the retention bound
- **THEN** preferences continue to save successfully, and the most recently read documents keep their reading positions

#### Scenario: Preference save fails
- **WHEN** the preferences endpoint rejects a save
- **THEN** one notice explains that preferences could not be saved, and continued scrolling produces no further error banners

#### Scenario: Ranked change is archived
- **WHEN** a ranked change is archived and the user later reorders another card
- **THEN** the saved priority order no longer contains the archived change

## ADDED Requirements

### Requirement: Ongoing board
Ongoing work SHALL show the Review inbox followed by a board with the columns Proposed, In progress, and Done, each showing its card count. Columns SHALL be derived from recorded facts and SHALL NOT be set by the user. The board SHALL contain three card kinds:
- A change card for each active OpenSpec change in the selected repositories.
- A ticket card for each ticket-grouped work item in the selected repositories that has an observation within the last 7 days and is not associated with an active change on the board.
- An ad-hoc card for each observation within the last 7 days that has neither a ticket nor a change.

A change card SHALL be in Done when its task progress is known and all tasks are ticked, or when its latest recorded status is done. Otherwise it SHALL be in In progress when it has any recorded observation or at least one ticked task. Otherwise it SHALL be in Proposed. When task progress and the latest recorded status disagree, the card SHALL show both without resolving the disagreement. A change card SHALL remain on the board until the change is archived, and an In-progress change card SHALL show the quiet indication when its latest observation is older than 14 days.

Ticket and ad-hoc cards SHALL be in Done when their latest recorded status is done, and in In progress otherwise. They SHALL leave the board 7 days after their latest observation. A ticket card SHALL be labeled as having no OpenSpec change, or as a follow-up to the archived change it is associated with. An ad-hoc card SHALL be titled with the observation's summary, or with an explicit no-summary label.

Every card SHALL show its repository and the date of its latest recorded observation, or that no activity is recorded. Change cards SHALL keep their task progress, review-comment and updated-since-read indications, and document links. A text filter SHALL narrow all columns, case-insensitively, to cards whose change name, ticket IDs, or title contain the entered text, SHALL show when it is active, and SHALL NOT be remembered. The board SHALL use the shared repository selection and refresh with the rest of Ongoing work.

#### Scenario: Agent logged work but ticked no tasks
- **WHEN** an active change has 0 of 9 tasks ticked and an in_progress observation from yesterday
- **THEN** its card is in In progress and shows 0 / 9 tasks and the recorded in_progress status

#### Scenario: Untouched proposal
- **WHEN** an active change has no observations and no ticked tasks
- **THEN** its card is in Proposed with no recorded activity

#### Scenario: Recorded done with unticked tasks
- **WHEN** an active change has 3 of 9 tasks ticked and its latest observation is done
- **THEN** its card is in Done and shows both 3 / 9 tasks and the recorded done status

#### Scenario: Finished change not yet archived
- **WHEN** all tasks of an active change were ticked 30 days ago and nothing was recorded since
- **THEN** its card remains in Done until the change is archived

#### Scenario: Ad-hoc work expires
- **WHEN** an observation with no ticket and no change was recorded as done 6 days ago, and another 8 days ago
- **THEN** the first appears as its own Done card titled with its summary and the second is not on the board

#### Scenario: Follow-up after archive
- **WHEN** a ticket has an in_progress observation from two days ago associated only with an archived change
- **THEN** a ticket card in In progress is labeled as a follow-up to that change

#### Scenario: Filter by ticket
- **WHEN** the user types `DEP-12` into the board filter
- **THEN** only cards whose title, change name, or tickets contain DEP-12 remain, in every column

### Requirement: Board priority order
The dashboard SHALL keep a single user-controlled priority order of board cards. Within each column, cards SHALL be shown with unranked cards first, ordered by latest recorded observation (newest first, then cards without activity by name), followed by ranked cards in priority order. A card SHALL keep its rank when it moves to another column. The user SHALL be able to move a card within its column by drag and drop and by keyboard-accessible actions that move it up, down, or to the top of the column. Moving a card SHALL rank every card in that column in its new displayed order without changing the relative order of cards in other columns. Cards SHALL NOT be dragged between columns. The priority order SHALL be saved in dashboard preferences and restored on later visits and in other browsers authorized for the same data directory. Ranks SHALL identify a change by its repositories and name, a ticket card by its work identity, and an ad-hoc card by its observation. Rendering the board SHALL NOT save preferences.

#### Scenario: Rank survives a column change
- **WHEN** the user ranks a Proposed change above all others and an agent later records work on it
- **THEN** the card appears in In progress above the other ranked cards there

#### Scenario: New work appears first
- **WHEN** a change was just created and has no rank
- **THEN** it appears above the ranked cards of its column

#### Scenario: Keyboard reordering
- **WHEN** the user focuses a card and uses the move-up action
- **THEN** the card moves one position up in its column and the order is saved

#### Scenario: Order restored
- **WHEN** the user reorders cards and reopens the dashboard later
- **THEN** the board shows the same order
