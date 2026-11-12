# Spec Delta

## ADDED Requirements

### Requirement: Cross-process live dashboard updates
The dashboard SHALL detect committed observation and digest changes from independent compatible writer processes, changes to approved document sources, repository configuration changes, and review/read-state changes. A visible connected browser SHALL refresh affected views from authoritative local data without a manual reload or periodic catalog/document polling. Delivery SHALL preserve complete snapshot and pagination semantics and the current repository, ticket, date, and archive scopes. Changes arriving during an in-flight refresh SHALL remain pending until the affected state is reconciled. Duplicate or out-of-order notifications SHALL NOT replace newer accepted state. After a notification gap, detection failure, connection restart, or visibility resume, the dashboard SHALL reconcile authoritative state rather than assume that every intermediate notification was delivered. Detection SHALL NOT create or migrate a journal or require a particular agent client or a change to existing writer contracts.

#### Scenario: Another process records work
- **WHEN** an independent compatible MCP server saves an observation while History or Ongoing work is visible
- **THEN** the affected views update without a page reload, using recorded facts and complete lifecycle context, regardless of which agent integration initiated logging

#### Scenario: Only a digest changes
- **WHEN** another process saves a digest without adding a journal observation
- **THEN** the affected Daily summary refreshes without inventing observation changes or needlessly reloading unrelated document content

#### Scenario: Repository configuration or availability changes
- **WHEN** repository configuration changes, a configured source disappears or recovers, or a configuration reload warning changes while document contents remain unchanged
- **THEN** the catalog and scoped notices update truthfully, retaining the last valid configuration when required and allowing unaffected sources and journal views to remain usable

#### Scenario: A burst overlaps a refresh
- **WHEN** additional committed changes arrive while the first refresh of the same resource is in flight
- **THEN** refresh work is coalesced, at most one background refresh for that resource runs at a time, and the final visible state includes the latest committed change

#### Scenario: Notifications are duplicated or lost
- **WHEN** notifications arrive twice, arrive out of order, or are missed during a detection interruption
- **THEN** the browser does not regress its visible state and reconciliation eventually establishes the current authoritative state with a truthful stale indication until it succeeds

#### Scenario: Another browser marks a document read
- **WHEN** an authorized second browser changes shared read state
- **THEN** read indicators refresh in the first browser without overwriting its unsaved comment, document selection, or local preferences

### Requirement: Live connection health and recovery
The dashboard SHALL expose accessible connection states labeled Live, Reconnecting, or Updates paused, and identify the last successful refresh when available. Live SHALL mean that a delivery connection is established and the affected visible resources have successfully reconciled, not merely that a connection opened. Initial connection failure and exhaustion of reconnect attempts SHALL expose a useful Retry action with bounded automatic attempts and no accumulated duplicate notices. Retry SHALL be single-flight. Refresh failures SHALL retain the last readable data, preserve pending changes, and expose recovery without falsely reporting Live. A confirmed authorization failure SHALL stop automatic connection and protected refresh attempts and explain how to authorize the browser again; it SHALL NOT discard drafts. After reconnect or server restart, the browser SHALL establish the current authoritative state before returning to Live.

#### Scenario: Initial live connection fails
- **WHEN** initial data is readable but the update connection cannot start
- **THEN** the snapshot remains readable, Updates paused explains that automatic freshness is unavailable, and Retry can establish updates without duplicate connections or subscriptions

#### Scenario: The connection is interrupted
- **WHEN** an established update connection is lost
- **THEN** the dashboard shows Reconnecting during bounded recovery attempts and Updates paused with Retry if those attempts are exhausted, retaining readable data and typed text

#### Scenario: Reconnect after disconnected writes
- **WHEN** documents, observations, or digests change while disconnected and the connection is then re-established
- **THEN** authoritative visible state is reconciled within five seconds in the documented representative local acceptance workload and Live is shown only after that reconciliation succeeds

#### Scenario: The dashboard server restarts
- **WHEN** a browser reconnects to a restarted dashboard serving the same data directory and endpoint
- **THEN** it recognizes the new server state, discards obsolete notification ordering, and refreshes affected snapshots without replaying an old state over the new one

#### Scenario: A refresh fails while connected
- **WHEN** the update connection is usable but an affected data refresh fails
- **THEN** the dashboard retains its last readable state and pending refresh, indicates Updates paused with actionable recovery, and returns to Live only after a successful refresh

#### Scenario: Authorization is lost
- **WHEN** a protected connection or reconciliation request confirms that browser authorization is no longer valid
- **THEN** automatic connection and protected refresh retries stop, relaunch guidance is visible, and the current document and unsaved text remain available

### Requirement: Time-driven view freshness
Time passing SHALL update the dashboard independently of file, journal, or connection notifications. Visible Ongoing cards SHALL expire at their existing seven-day boundary, and quiet indications SHALL follow their existing fourteen-day rule. History relative ranges and Daily day boundaries SHALL advance using the selected reporting timezone, including midnight and daylight-saving changes; custom History dates SHALL remain explicit. Returning from a suspended or hidden tab SHALL reevaluate elapsed time before presenting the view as current. Time-only transitions SHALL NOT fabricate observations, rewrite user preferences, or require catalog/document polling.

#### Scenario: No writes occur at board expiry
- **WHEN** a ticket or ad-hoc card crosses its seven-day expiry boundary while the connected dashboard remains open with no data writes
- **THEN** the card leaves Ongoing work and any dependent Daily Today presentation updates without inventing a recorded completion

#### Scenario: An active change becomes quiet
- **WHEN** the latest observation of an in-progress change becomes older than fourteen days without another recorded write
- **THEN** the quiet indication and recorded date update without changing the change's recorded status

#### Scenario: Midnight and daylight-saving transition
- **WHEN** the selected timezone crosses midnight or a daylight-saving transition while the dashboard stays open
- **THEN** relative History and Daily boundaries use that timezone's current day, preserve their existing date semantics, and leave a custom History range unchanged

#### Scenario: Resume after time has passed
- **WHEN** a hidden or suspended dashboard becomes visible after an expiry or local-day boundary
- **THEN** time-dependent views and authoritative data reconcile before the dashboard reports Live, preserving the chosen route and user input

## MODIFIED Requirements

### Requirement: Idle refresh cost
While the dashboard is visible, connected, and current, background refresh SHALL use change notifications instead of periodic browser catalog or document reads. Five idle connected minutes without data changes, user actions, or time-boundary transitions SHALL produce zero periodic catalog/document REST reads, repeated document searches, or timeline queries; transport keep-alives SHALL be accounted for separately. OpenSpec root resolution and task progress SHALL be refreshed when their inputs change or after a bounded server discovery interval of at most one minute, with at most one idle discovery round per source in that interval. Independent source discovery SHALL NOT be serialized. Normal committed local document, journal, and digest changes SHALL become visible with p95 latency at most one second in the documented representative local acceptance workload. Unchanged reconciliation SHALL NOT retransmit complete open-document content or rewrite preferences. Burst queues and retry work SHALL be bounded; slow or unavailable sources SHALL remain explicit rather than blocking unrelated readable views. Server detection and reconciliation work SHALL be measured separately from browser request reductions.

#### Scenario: Idle dashboard
- **WHEN** the dashboard stays open for one minute with no document or journal changes
- **THEN** it performs no repeated searches or timeline queries, does not resend the unchanged open document, does not rewrite saved preferences, and performs at most one round of OpenSpec CLI resolution per source

#### Scenario: Agent edits the open document
- **WHEN** an agent saves a change to the open design while the dashboard is idle
- **THEN** the reader shows the update without manual reload, meeting the documented local-change freshness target

#### Scenario: Agent logs work
- **WHEN** a new observation is recorded while History is visible
- **THEN** History reflects it through event-triggered authoritative refresh without the user reloading the page

#### Scenario: Five connected idle minutes
- **WHEN** the documented idle fixture remains visible and connected for five minutes without writes, user actions, or time-boundary transitions
- **THEN** browser traces show zero periodic catalog/document REST reads or repeated search/timeline queries, with keep-alive traffic and bounded server reconciliation reported separately

#### Scenario: An unchanged document is reconciled
- **WHEN** reconnect recovery or a coalesced notification requires validating an open document that has not changed
- **THEN** its unchanged content is not transferred again and the validation does not rewrite preferences

### Requirement: Local data access boundary
Dashboard journal and document data SHALL be restricted to the local dashboard browser session and approved local sources. The service SHALL reject cross-origin data access, unexpected host names, unauthorized data requests, and requests escaping source roots, including through filesystem links. Its document writes SHALL be limited to adding, editing, and deleting review markers in active change Markdown files. Its only local launch operation SHALL be opening a discovered, contained document in one of the fixed editor presets. The service SHALL resolve the file from the document identity itself, SHALL reject unknown presets, and SHALL NOT accept file paths, command lines, or arguments from the client. Markdown content SHALL not execute scripts or trigger automatic remote resource requests. The dashboard SHALL not send journal data, documents, searches, or comments to external services; explicit external links SHALL require user navigation. Live negotiation, connection establishment, and notifications SHALL retain the same authorization, expected Host, and same-origin browser boundary. Existing live connections SHALL stop receiving private notifications when their authorization is revoked, without depending on a later HTTP request. Notification targets SHALL be limited to approved sources, and notification content SHALL NOT contain document text, observation summaries, authorization material, or arbitrary filesystem paths supplied by a browser.

#### Scenario: Unrelated website attempts journal access
- **WHEN** a request from an unrelated web origin attempts to read dashboard data
- **THEN** the service rejects the request without returning private content

#### Scenario: Unexpected host name
- **WHEN** a request reaches the listener with a Host header other than the dashboard's loopback address
- **THEN** the service rejects it without returning private content

#### Scenario: Missing authorization
- **WHEN** a request without a valid dashboard session requests journal or document data
- **THEN** the service rejects it and the client explains how to launch the dashboard

#### Scenario: Document request escapes its source
- **WHEN** a document request uses encoded traversal, an absolute path, or a junction or symbolic link pointing outside its source root
- **THEN** the service rejects it without reading the outside file

#### Scenario: Unsafe document content
- **WHEN** a document contains executable markup or an automatically loaded remote image
- **THEN** the rendered view executes no document script and makes no automatic remote request

#### Scenario: Launch request names an unknown preset or escapes its source
- **WHEN** an open-in-editor request names an editor outside the fixed presets, or targets a document outside a discovered source through traversal or a filesystem link
- **THEN** the service rejects it without launching any process

#### Scenario: Unauthorized launch request
- **WHEN** an open-in-editor request arrives without a valid dashboard session or from another origin
- **THEN** the service rejects it without launching any process

#### Scenario: Unauthorized or cross-origin live connection
- **WHEN** negotiation or a browser connection upgrade uses missing authorization, an unexpected Host, or a foreign Origin
- **THEN** it is rejected without private notifications, even when an unrelated website can address the loopback endpoint

#### Scenario: An established connection loses authorization
- **WHEN** the dashboard authorization secret is removed or replaced while the browser has an established live connection
- **THEN** the server prevents subsequent private notifications and terminates that connection through bounded revocation detection, and protected reconnects are refused

#### Scenario: A notification subscription names an unknown source
- **WHEN** a browser attempts to target an unknown source or an arbitrary external filesystem location
- **THEN** it cannot cause a watcher, read, notification payload, or local process to access that location
