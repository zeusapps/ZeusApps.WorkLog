# Spec Delta

## MODIFIED Requirements

### Requirement: Today and blockers
The Daily view SHALL derive Today from the Ongoing board of the same repository selection. Today SHALL list the board's In progress cards, except cards showing the quiet indication, plus cards from any column with an observation recorded today, each card once, in the board's priority order. Proposed change cards SHALL NOT be listed under Today; the view MAY indicate how many exist. A Today card with observations recorded today SHALL show its latest recorded status and the summary of its latest observation today, or an explicit no-summary label, with its other observations from today collapsed and available on request; a Today card with no observation recorded today SHALL show its title and tickets only. A Today line SHALL contain only observations recorded today for that card's own ticket or change. An item MAY appear under both Yesterday and Today. Today and the copy action SHALL NOT be available until the board inputs for the current repository selection have loaded. The Blockers section SHALL list, for each Today line that shows a matching digest with blocker bullets, those bullets with the line's title or ticket IDs, in Today's order. When no Today line shows blocker bullets, the Blockers section SHALL say that no blockers are recorded. Blockers SHALL be derived only from digests shown on Today lines, and SHALL NOT be inferred from summaries or other recorded data.

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

#### Scenario: Blockers from today's digests
- **WHEN** DEP-9's Today line shows a matching digest with the blocker "API access from the platform team" and no other Today line has blockers
- **THEN** the Blockers section lists DEP-9 with that blocker

#### Scenario: No recorded blockers
- **WHEN** no Today line shows a digest with blocker bullets
- **THEN** the Blockers section says that no blockers are recorded

#### Scenario: Yesterday's blockers are not current
- **WHEN** a ticket's Yesterday digest has a blocker and its Today line shows a digest without blockers
- **THEN** the Blockers section does not list that blocker

### Requirement: Digests on daily lines
For each Yesterday line and each Today line with observations recorded that day, the Daily view SHALL show the stored digest whose covered observations are exactly the observations the line represents, in place of the latest summary required by "Concise yesterday digest" and "Today and blockers", and SHALL mark it as an agent digest. A structured digest SHALL be shown as labelled Done, Next and Blockers bullet lists, omitting a label whose list is empty. A digest stored as a single text SHALL be shown as that text under Done. When no digest covers exactly those observations, the line SHALL show the latest summary as a single Done bullet. The line's status and its collapsed observations SHALL be unchanged by a digest. Matching SHALL depend only on the covered observations, not on dates or timezones. The view SHALL NOT request, create, or edit digests.

#### Scenario: Matching digest
- **WHEN** DEP-7 has two observations on the last recorded day and a digest covers exactly those two with two Done bullets, one Next bullet and no blockers
- **THEN** the Yesterday line for DEP-7 shows the Done and Next lists, marked as an agent digest, without a Blockers label, with its status and collapsed observation unchanged

#### Scenario: Earlier single-text digest
- **WHEN** a digest saved by an earlier release as a single text covers exactly a line's observations
- **THEN** the line shows that text under Done, marked as an agent digest

#### Scenario: Digest made stale by a later observation
- **WHEN** a digest covers two of DEP-7's observations today and a third is recorded afterwards
- **THEN** the Today line for DEP-7 shows the third observation's summary as its Done bullet

#### Scenario: Different timezone
- **WHEN** the History timezone splits a day differently from the machine's timezone, so a line's observations differ from a digest's covered observations
- **THEN** the line shows the latest summary

### Requirement: Copy for daily
The Daily view SHALL provide an action that copies the Yesterday, Today, and Blockers sections as concise text, in a Markdown or plain-text format selected by the user, with ticket links included or omitted as selected. The format and link choices SHALL be remembered. The copied Yesterday section SHALL name the selected date. Each copied line SHALL contain only recorded facts: title or ticket IDs, recorded status, and the matching digest of that line's day when one is shown, otherwise the latest summary of that line's day. A shown structured digest SHALL be copied as its Done, Next and Blockers bullets under their labels, omitting empty lists. Both formats SHALL use bullets. A Today card without an observation recorded today SHALL be copied as its title or ticket IDs only. The Blockers section SHALL be copied as shown, including the no-blockers statement when none are recorded.

#### Scenario: Copy as plain text without links
- **WHEN** the user selects plain text without links and copies
- **THEN** the clipboard contains the three sections with ticket IDs, statuses, and bulleted digests or summaries for Yesterday lines and Today lines recorded today, and no URLs

#### Scenario: Copy as Markdown with links
- **WHEN** the user selects Markdown with links and copies
- **THEN** ticket IDs with known URLs are Markdown links and digest parts are Markdown bullet lists

#### Scenario: Copy uses the shown digest
- **WHEN** a Yesterday line shows a matching structured digest and the user copies
- **THEN** the copied line contains its Done and Next bullets under their labels instead of the latest summary, without the agent-digest mark

#### Scenario: Copy includes derived blockers
- **WHEN** the Blockers section lists DEP-9 with one blocker and the user copies
- **THEN** the copied Blockers section contains DEP-9 and that blocker

## ADDED Requirements

### Requirement: Readable stand-up layout
On wide screens, the Daily view SHALL place Today and Yesterday side by side as two columns, Today first, each with a readable line length, so that Today's items are visible without scrolling past Yesterday. On narrow screens it SHALL stack them with Today above Yesterday. Each work item SHALL be presented as a compact row with its title, tickets, repository, status and digest or summary bullets, separated from neighboring rows without nested card chrome. The Blockers section SHALL follow both columns and span the view. The layout SHALL work in light and dark themes and at 390, 768 and 1440 pixel widths, SHALL keep keyboard access to each row's collapsed observations and to the previous- and next-day controls, and SHALL NOT change which items are listed or their order.

#### Scenario: Wide screen
- **WHEN** the Daily view is shown at 1440 pixels wide with items for today and the last recorded day
- **THEN** Today and Yesterday appear side by side with Today first, and the first Today item is visible without scrolling

#### Scenario: Phone width
- **WHEN** the Daily view is shown at 390 pixels wide
- **THEN** Today appears above Yesterday in one column without horizontal scrolling

#### Scenario: Keyboard access
- **WHEN** a keyboard user moves through the Daily view
- **THEN** each row's collapsed observations and the day navigation are reachable and operable
