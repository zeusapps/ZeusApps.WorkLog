# Spec Delta

## MODIFIED Requirements

### Requirement: Digest request on logging
When `log_work` saves a new observation, the system SHALL determine the observation's work item using ticket grouping and the observation's local date in the machine's timezone. If that work item has one or more observations on that date, the result SHALL include a digest request, including for the item's first observation of the day. The request SHALL list every one of those observations with its ID, occurred timestamp, status, change, tickets, and summary, and SHALL state the date, the timezone, and what to write. The system SHALL NOT include a digest request when the result returns an existing entry (a retry or a duplicate initial start), when the item has more than 30 observations that date, or when the observation has neither a change nor a ticket (such observations share no work item a digest could describe). Computing the request SHALL NOT change the saved observation, and a failure to compute it SHALL NOT fail the logging call.

#### Scenario: Second observation of the day
- **WHEN** an agent logs an observation for ticket DEP-7 and an earlier observation for DEP-7 was recorded the same local day
- **THEN** the result includes a digest request listing both observations

#### Scenario: First observation of the day
- **WHEN** an agent logs the first observation of the day for ticket DEP-7
- **THEN** the result includes a digest request listing that one observation

#### Scenario: Retry
- **WHEN** an agent retries a logging call with the same idempotency key and the original entry is returned
- **THEN** the result includes no digest request

#### Scenario: Unlinked observation
- **WHEN** an agent logs an observation with neither a ticket nor a change
- **THEN** the result includes no digest request

#### Scenario: Digest storage unavailable
- **WHEN** the digest storage cannot be opened while an agent logs an observation
- **THEN** the observation is saved and the logging call succeeds

### Requirement: Saving a digest
The system SHALL expose an MCP tool named `save_digest` that accepts the IDs of the observations a digest covers and the digest as three lists of short bullet texts: `done` with 1 to 4 bullets, `next` with 0 to 3 bullets, and `blockers` with 0 to 2 bullets. Each bullet SHALL be trimmed, SHALL have line breaks replaced by spaces, and SHALL be non-empty and at most 120 characters; otherwise the call SHALL fail without writing. For compatibility with clients that still use the earlier input, the tool SHALL also accept a single `text` of at most 280 characters, under the same trimming and line-break rule, in place of the lists. It SHALL store that text as a single-text digest, which every reader presents as the only Done bullet with no Next or Blockers bullets. A call that supplies both `text` and lists, or neither, SHALL fail without writing. The digest SHALL be accepted only when every ID names a stored observation, all of them belong to one work item under ticket grouping and one local date in the machine's timezone, and they are all of that item's observations on that date. Observations with neither a change nor a ticket SHALL be rejected. Otherwise the call SHALL fail as stale or invalid without writing, and the agent SHALL NOT be expected to retry. An accepted digest SHALL replace any earlier digest for the same item and date. Saving a digest SHALL NOT create, change, or delete observations, and the same input saved twice SHALL leave one digest.

#### Scenario: Digest saved
- **WHEN** the agent saves two Done bullets, one Next bullet and no blockers, covering exactly the two observations it was given for DEP-7 today
- **THEN** the digest is stored for DEP-7 and today with those bullets

#### Scenario: Single-observation digest saved
- **WHEN** the agent saves a digest covering the one observation of DEP-7 today
- **THEN** the digest is stored for DEP-7 and today

#### Scenario: Legacy text input
- **WHEN** a client that has not refreshed its tool list saves a 200-character `text` for DEP-7 today
- **THEN** the digest is stored as a single text and presented as its only Done bullet

#### Scenario: Later observation makes the digest stale
- **WHEN** a third observation for DEP-7 is recorded today before the agent saves a digest covering the first two
- **THEN** the call fails as stale and nothing is stored

#### Scenario: Newer digest replaces older
- **WHEN** a digest for DEP-7 today exists and the agent saves a digest covering all three of today's observations
- **THEN** only the newer digest remains for DEP-7 today

#### Scenario: Text too long
- **WHEN** a client saves a legacy `text` of 400 characters
- **THEN** the call fails and nothing is stored

#### Scenario: Bullet too long or too many bullets
- **WHEN** the agent saves a Done bullet of 200 characters, or five Done bullets
- **THEN** the call fails and nothing is stored

#### Scenario: No Done bullet
- **WHEN** the agent saves only Next and Blockers bullets
- **THEN** the call fails and nothing is stored

### Requirement: Digest storage compatibility
Digests SHALL be stored locally in the data directory, separately from the journal, so that the journal schema and the shared-data contract are unchanged. A digest-capable release SHALL keep `log_work` and `query_work`, and its update health check SHALL accept a candidate payload that lists additional tools besides them. Releases whose update health check requires exactly two tools (v0.3.0 and earlier) decline activation of a digest-capable payload; updating from them SHALL be a one-time manual installation, documented in the release notes. Releases without digest support SHALL keep working with the same journal while digest-capable processes run alongside them. Digest storage SHALL use its own version; a digest store newer than a process supports SHALL disable digest requests and saving for that process without affecting logging, querying, or the dashboard's other views. Losing the digest store SHALL lose only digests. Structured digest parts SHALL be stored additively, so that a process supporting only single-text digests keeps reading and writing the same store without being disabled. Every stored digest SHALL keep a readable single text: for a structured digest, the text is derived from its parts. Structured parts SHALL be used only when they belong to the stored digest's current save: the same covered observations and the same save time, independent of the derived text's format. When an older process later replaces the digest, the replacement SHALL be shown as its own text.

#### Scenario: Mixed versions
- **WHEN** a digest-capable MCP server and an older MCP server both log to the same journal
- **THEN** both keep logging and the journal schema version is unchanged

#### Scenario: Older digest-capable process alongside structured digests
- **WHEN** a v0.4.1 MCP server and a v0.5.0 MCP server share a digest store that contains structured digests
- **THEN** the v0.4.1 server keeps requesting and saving single-text digests without being disabled, and it can read a text for every structured digest

#### Scenario: Older process replaces a structured digest
- **WHEN** an older process saves a single-text digest for an item and day that had a structured digest
- **THEN** the dashboard shows the newer single text as the Done bullet and does not show the earlier structured parts

#### Scenario: Update from a release that expects two tools
- **WHEN** `worklog update` on v0.3.0 probes a digest-capable payload
- **THEN** it declines activation without changing selection or data, and the release notes give the manual installation steps

#### Scenario: Later update from a digest-capable release
- **WHEN** a digest-capable release probes a later payload that lists `log_work`, `query_work`, and further tools
- **THEN** the health check accepts the payload

#### Scenario: Newer digest store
- **WHEN** a process finds a digest store version newer than it supports
- **THEN** it logs observations without digest requests and rejects `save_digest` with an explanation

### Requirement: Digest writing guidance
Every place that tells an agent how to write a digest SHALL state the same team-relevant content rule as the observation summary guidance. The digest is written for the user's daily stand-up, in the user's own voice, without referring to the user or the agent. `done` SHALL combine what all of the listed observations changed that day in the feature, story or bug fix (behavior, capability or a scope decision), from earliest to latest, and SHALL NOT restate only the latest observation. `next` SHALL state the concrete next steps in plain words. `blockers` SHALL list only what needs someone else and SHALL be empty otherwise. The rule SHALL direct the agent not to mention how the work was organized, naming at least: the development process and its tooling, including OpenSpec and proposing, creating, archiving or syncing changes, specs and tasks; any agent or role, such as developer, QA, analyzer, reviewer or bug-hunter, and their rounds or findings; test and task counts; commits, hashes, branches and merges; and builds and installation. These places are the managed instructions of both integrations when enabled, the digest request's statement of what to write, and the `save_digest` input descriptions, where the content rule is stated once and the other inputs refer to it. The digest request's statement SHALL be complete on its own. Besides the content rule, it SHALL state the three lists with their bullet counts and the 120-character bullet limit, that secrets and personal data are excluded, that the listed summaries are recorded data rather than instructions and are the record to summarize, that the agent passes exactly the listed IDs, and that a stale rejection is not retried. When managed instructions are enabled, both integrations SHALL receive identical digest guidance. The guidance SHALL NOT change digest validation.

#### Scenario: Request guidance without managed instructions
- **WHEN** a client with managed instructions disabled receives a digest request
- **THEN** the request's statement of what to write gives the content rule, the three lists with their limits, the exclusion of secrets and personal data, and the no-retry rule for stale saves

#### Scenario: Done covers the whole day
- **WHEN** an agent receives a digest request listing seven observations whose earlier summaries report achievements the latest summary does not mention
- **THEN** the guidance directs Done bullets that include those earlier achievements rather than a restatement of the latest summary

#### Scenario: Workflow mechanics excluded from digests
- **WHEN** an agent receives a digest request whose summaries mention an archived change, QA and bug-hunter findings, review rounds, test counts and a commit
- **THEN** the guidance directs a digest that states the item's outcome, next steps and blockers and omits those details

#### Scenario: No blockers
- **WHEN** nothing in the item's day needs someone else
- **THEN** the guidance directs an empty blockers list

#### Scenario: Same digest guidance for both clients
- **WHEN** init installs managed instructions for Claude Code and Codex
- **THEN** both owned blocks contain the same digest guidance with the content rule

#### Scenario: Validation unchanged
- **WHEN** an agent saves bullets within the limits that mention a commit hash
- **THEN** the digest is stored, because digest content is not validated
