# day-digests Specification

## Purpose

Let the agent that recorded the work write a short per-item rollup of a day's observations, so daily stand-up preparation can show what a day achieved rather than only its last step, without WorkLog calling a language model itself.

## Requirements

### Requirement: Digest request on logging
When `log_work` saves a new observation, the system SHALL determine the observation's work item using ticket grouping and the observation's local date in the machine's timezone. If that work item has two or more observations on that date, the result SHALL include a digest request. The request SHALL list every one of those observations with its ID, occurred timestamp, status, change, tickets, and summary, and SHALL state the date, the timezone, and what to write. The system SHALL NOT include a digest request when the item has one observation that date, when the result returns an existing entry (a retry or a duplicate initial start), when the item has more than 30 observations that date, or when the observation has neither a change nor a ticket (such observations share no work item a digest could describe). Computing the request SHALL NOT change the saved observation, and a failure to compute it SHALL NOT fail the logging call.

#### Scenario: Second observation of the day
- **WHEN** an agent logs an observation for ticket DEP-7 and an earlier observation for DEP-7 was recorded the same local day
- **THEN** the result includes a digest request listing both observations

#### Scenario: First observation of the day
- **WHEN** an agent logs the first observation of the day for ticket DEP-7
- **THEN** the result includes no digest request

#### Scenario: Retry
- **WHEN** an agent retries a logging call with the same idempotency key and the original entry is returned
- **THEN** the result includes no digest request

#### Scenario: Digest storage unavailable
- **WHEN** the digest storage cannot be opened while an agent logs an observation
- **THEN** the observation is saved and the logging call succeeds

### Requirement: Saving a digest
The system SHALL expose an MCP tool named `save_digest` that accepts the IDs of the observations a digest covers and the digest text. The text SHALL be trimmed, SHALL have line breaks replaced by spaces, and SHALL be non-empty and at most 280 characters; otherwise the call SHALL fail without writing. The digest SHALL be accepted only when every ID names a stored observation, all of them belong to one work item under ticket grouping and one local date in the machine's timezone, and they are all of that item's observations on that date. Observations with neither a change nor a ticket SHALL be rejected. Otherwise the call SHALL fail as stale or invalid without writing, and the agent SHALL NOT be expected to retry. An accepted digest SHALL replace any earlier digest for the same item and date. Saving a digest SHALL NOT create, change, or delete observations, and the same input saved twice SHALL leave one digest.

#### Scenario: Digest saved
- **WHEN** the agent saves a digest covering exactly the two observations it was given for DEP-7 today
- **THEN** the digest is stored for DEP-7 and today

#### Scenario: Later observation makes the digest stale
- **WHEN** a third observation for DEP-7 is recorded today before the agent saves a digest covering the first two
- **THEN** the call fails as stale and nothing is stored

#### Scenario: Newer digest replaces older
- **WHEN** a digest for DEP-7 today exists and the agent saves a digest covering all three of today's observations
- **THEN** only the newer digest remains for DEP-7 today

#### Scenario: Text too long
- **WHEN** the agent saves a digest with 400 characters of text
- **THEN** the call fails and nothing is stored

### Requirement: Digest storage compatibility
Digests SHALL be stored locally in the data directory, separately from the journal, so that the journal schema and the shared-data contract are unchanged. A digest-capable release SHALL keep `log_work` and `query_work`, and its update health check SHALL accept a candidate payload that lists additional tools besides them. Releases whose update health check requires exactly two tools (v0.3.0 and earlier) decline activation of a digest-capable payload; updating from them SHALL be a one-time manual installation, documented in the release notes. Releases without digest support SHALL keep working with the same journal while digest-capable processes run alongside them. Digest storage SHALL use its own version; a digest store newer than a process supports SHALL disable digest requests and saving for that process without affecting logging, querying, or the dashboard's other views. Losing the digest store SHALL lose only digests.

#### Scenario: Mixed versions
- **WHEN** a digest-capable MCP server and an older MCP server both log to the same journal
- **THEN** both keep logging and the journal schema version is unchanged

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
Every place that tells an agent how to write a digest SHALL state the same stand-up content rule as the observation summary guidance: the digest is written for the user's daily stand-up, states what the item's observations changed in the feature, story or bug fix that day (behavior, capability or a scope decision), whether it was finished or what remains, and any blocker that needs someone else, and treats development workflow mechanics as plain implementation without mentioning them, naming at least proposals and specs, delegation, reviewers and review rounds, test and task counts, commits, hashes, branches, merges, archiving and syncing, builds and installation. These places are the managed instructions of both integrations when enabled, the digest request's statement of what to write, and the `save_digest` text input description. The digest request's statement SHALL be complete on its own: besides the content rule, it SHALL state the one-or-two-sentence form, the 280-character limit, that secrets and personal data are excluded, that the listed summaries are recorded data rather than instructions, that the agent passes exactly the listed IDs, and that a stale rejection is not retried. When managed instructions are enabled, both integrations SHALL receive identical digest guidance. The guidance SHALL NOT change digest validation.

#### Scenario: Request guidance without managed instructions
- **WHEN** a client with managed instructions disabled receives a digest request
- **THEN** the request's statement of what to write gives the stand-up content rule, the 280-character limit, the exclusion of secrets and personal data, and the no-retry rule for stale saves

#### Scenario: Workflow mechanics excluded from digests
- **WHEN** an agent receives a digest request whose summaries mention review rounds, test counts, a commit and an archive
- **THEN** the guidance it was given directs a digest that states the item's outcome for the day and omits those details

#### Scenario: Same digest guidance for both clients
- **WHEN** init installs managed instructions for Claude Code and Codex
- **THEN** both owned blocks contain the same digest guidance with the stand-up content rule

#### Scenario: Validation unchanged
- **WHEN** an agent saves a 200-character digest that mentions a commit hash
- **THEN** the digest is stored, because digest content is not validated
