# Spec Delta

## ADDED Requirements

### Requirement: Digests on daily lines
For each Yesterday line and each Today line with observations recorded that day, the Daily view SHALL show the stored digest whose covered observations are exactly the observations the line represents, in place of the latest summary required by "Concise yesterday digest" and "Today and blockers", and SHALL mark it as an agent digest. When no digest covers exactly those observations, the line SHALL show the latest summary as before. The line's status and its collapsed observations SHALL be unchanged by a digest. Matching SHALL depend only on the covered observations, not on dates or timezones. The view SHALL NOT request, create, or edit digests.

#### Scenario: Matching digest
- **WHEN** DEP-7 has two observations on the last recorded day and a digest covers exactly those two
- **THEN** the Yesterday line for DEP-7 shows the digest, marked as an agent digest, with its status and collapsed observation unchanged

#### Scenario: Digest made stale by a later observation
- **WHEN** a digest covers two of DEP-7's observations today and a third is recorded afterwards
- **THEN** the Today line for DEP-7 shows the third observation's summary

#### Scenario: Different timezone
- **WHEN** the History timezone splits a day differently from the machine's timezone, so a line's observations differ from a digest's covered observations
- **THEN** the line shows the latest summary

## MODIFIED Requirements

### Requirement: Copy for daily
The Daily view SHALL provide an action that copies the Yesterday, Today, and Blockers sections as concise text, in a Markdown or plain-text format selected by the user, with ticket links included or omitted as selected. The format and link choices SHALL be remembered. The copied Yesterday section SHALL name the selected date. Each copied line SHALL contain only recorded facts: title or ticket IDs, recorded status, and the matching digest of that line's day when one is shown, otherwise the latest summary of that line's day. A Today card without an observation recorded today SHALL be copied as its title or ticket IDs only. The Blockers section SHALL be copied as an empty placeholder.

#### Scenario: Copy as plain text without links
- **WHEN** the user selects plain text without links and copies
- **THEN** the clipboard contains the three sections with ticket IDs, statuses, and summaries for Yesterday lines and Today lines recorded today, and no URLs

#### Scenario: Copy as Markdown with links
- **WHEN** the user selects Markdown with links and copies
- **THEN** ticket IDs with known URLs are Markdown links

#### Scenario: Copy uses the shown digest
- **WHEN** a Yesterday line shows a matching digest and the user copies
- **THEN** the copied line contains the digest text instead of the latest summary, without the agent-digest mark
