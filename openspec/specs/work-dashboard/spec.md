# work-dashboard Specification

## Purpose

Provide a private local dashboard for following ongoing changes and understanding recorded work over time through elapsed spans and activity dates.

## Requirements

### Requirement: Local dashboard lifecycle
The application SHALL provide `worklog dashboard` with repository, data-directory, port, and no-browser-open options. It SHALL serve bundled assets and journal-backed views on a loopback address, open the browser by default, print its local URL, and release its listener on cancellation. The packaged Windows dashboard SHALL work without internet access, Node.js, or a separately installed .NET runtime. Launching while a dashboard for the same data directory is already running SHALL reuse it, print its local URL, and exit successfully without another listener, opening the browser unless --no-open was supplied. The --no-open option SHALL suppress browser opening on both initial and repeat launches. A port held by any other process SHALL produce an actionable error and SHALL NOT be trusted. Browser authorization SHALL persist across dashboard restarts for the same data directory. Dashboard startup SHALL NOT launch, replace, or terminate stdio MCP servers or write activity observations.

#### Scenario: Launch offline
- **WHEN** the user launches the packaged dashboard with network access unavailable
- **THEN** its interface and available local data load, and an existing MCP conversation remains usable

#### Scenario: Manual browser opening and shutdown
- **WHEN** the user launches with browser opening disabled and later cancels the command
- **THEN** the command displays the local URL without opening a browser and releases its listener on cancellation

#### Scenario: Port or browser failure
- **WHEN** the requested port is occupied by a process that is not this data directory's running dashboard, or the default browser cannot be opened
- **THEN** the command reports an actionable port error or retains a working server with a copyable URL, respectively

#### Scenario: Launch while already running
- **WHEN** the user runs the dashboard command without --no-open while a dashboard for the same data directory is running
- **THEN** the browser opens on the running dashboard and the second command exits successfully without starting another listener

#### Scenario: Reuse without opening a browser
- **WHEN** the user runs the dashboard command with --no-open while a dashboard for the same data directory is running
- **THEN** the command prints the running dashboard's local URL and exits successfully without opening a browser or another listener

#### Scenario: Restart keeps the browser authorized
- **WHEN** the dashboard is stopped and started again at the same endpoint
- **THEN** a previously authorized browser can open copied dashboard links without a new launch URL

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

### Requirement: Dashboard appearance
The dashboard SHALL use a dark theme by default and provide a keyboard-accessible light/dark toggle. The selected theme SHALL apply to ongoing work, history, document reading, source views, and review controls, and SHALL be restored on later visits. Its favicon SHALL be served locally from embedded assets without a remote dependency.

#### Scenario: Default and remembered appearance
- **WHEN** the user opens the dashboard without a theme preference, switches to light, and later reloads
- **THEN** the first visit uses dark and the later visit restores light

### Requirement: Shared repository selection
The dashboard SHALL provide a shared filter selecting all known repositories, one repository, or several repositories across Ongoing work, History, document search, and insights. A valid restored selection SHALL take precedence over the launch repository; without one, the launch repository SHALL be selected when known, otherwise all known repositories SHALL be selected. The dashboard SHALL remember the selection across visits, identify repositories when several are visible, and report unavailable sources without silently substituting another repository.

#### Scenario: Initial launch from a known repository
- **WHEN** no valid saved selection exists and the dashboard is launched for a configured checkout
- **THEN** both views and search initially use that repository

#### Scenario: Multi-repository selection survives a visit
- **WHEN** the user selects two repositories, changes views, and later reopens the dashboard
- **THEN** the selection is restored and each view and search uses those repositories

#### Scenario: Repository files are unavailable
- **WHEN** a selected repository has journal records but its checkout is missing
- **THEN** its recorded history remains available and document access indicates the unavailable source

### Requirement: Factual elapsed spans and activity dots
History SHALL support explicit date ranges, a reporting timezone, and ticket or change grouping consistent with existing qualified work identity rules. For displayed work identities, the dashboard SHALL derive lifecycle context from all observations preceding the range's exclusive upper bound, rather than treating the first visible observation as the start. It SHALL draw a span from the earliest recorded observation to the latest recorded observation as of that bound, clipped to the visible range with clipping indicated. Dots SHALL represent only local dates with recorded observations in the selected range, with multiple same-day observations available on drill-down. Labels SHALL distinguish first observation from a recorded started event, elapsed span from recorded activity-day counts, and recorded facts from measured working hours. Items without a recorded completion SHALL end at their latest observation and identify that no completion is recorded, without inventing activity through the current date.

#### Scenario: Gaps between activity dates
- **WHEN** an item has observations on Monday and Friday only
- **THEN** the view shows two activity dates connected by an elapsed span, without dots on the intervening dates or an hours-worked claim

#### Scenario: Start is outside the visible range
- **WHEN** an item first appears before the selected range and has an observation within it
- **THEN** its span indicates a clipped earlier start and its details retain the actual earliest recorded date

#### Scenario: Timezone boundary
- **WHEN** observations cross a reporting timezone's midnight or daylight-saving transition
- **THEN** dots and distinct activity-day counts use the selected timezone while elapsed durations use the recorded instants

#### Scenario: Later work is outside the report period
- **WHEN** an item has a completion after the range's exclusive upper bound
- **THEN** the historical view does not present that later completion as a fact already recorded within the selected period

### Requirement: Completion and subsequent work remain visible
The dashboard SHALL preserve all recorded statuses, including repeated done entries and in_progress after done. It SHALL expose the latest recorded status as of the selected bound, surface completion and subsequent-work events in item details, and retain the original dates and summaries. Neither a done observation nor an archive move SHALL hide subsequent recorded activity or imply an external ticket transition.

#### Scenario: Work resumes after completion
- **WHEN** an item has done, later in_progress, and a later done observation
- **THEN** all events remain inspectable and the latest applicable observation determines the displayed recorded status

### Requirement: Complete and bounded dashboard results
The dashboard SHALL avoid deriving final counts, spans, or status from incomplete report pages. Large histories SHALL be exposed with bounded result batches or explicit continuation, complete lifecycle aggregates for displayed items, and visible loading or partial-result states. A refresh SHALL use a consistent journal snapshot; observations added during loading SHALL appear on a later refresh. Ticket-set grouping SHALL not double-count observations, merge different ticket namespaces, or qualify unresolved historical tickets using current settings.

#### Scenario: History exceeds one report page
- **WHEN** an item has more observations than a single journal report page can return
- **THEN** its completed aggregate includes the full applicable history and drill-down provides continuation without silent truncation

#### Scenario: New observation during loading
- **WHEN** an agent logs backdated work while a history refresh is loading
- **THEN** the current snapshot remains consistent and the new observation is included on a later refresh

### Requirement: Traceable activity insights
The dashboard SHALL provide insights for longest recorded spans, recent completion observations, work recorded after completion, and distinct work items recorded per day within the selected repository and period scope. Each insight SHALL link to its underlying items or observations and identify its metric. It SHALL NOT equate observation frequency with productivity, infer working hours or blocked time, or treat a lack of observations as proof of inactivity.

#### Scenario: Inspect a long span
- **WHEN** the user opens an item from the longest-span insight
- **THEN** its detail shows the observations establishing the span and its recorded activity dates

### Requirement: Local data access boundary
Dashboard journal and document data SHALL be restricted to the local dashboard browser session and approved local sources. The service SHALL reject cross-origin data access, unexpected host names, unauthorized data requests, and requests escaping source roots, including through filesystem links. Its document writes SHALL be limited to adding, editing, and deleting review markers in active change Markdown files. Its only local launch operation SHALL be opening a discovered, contained document in one of the fixed editor presets. The service SHALL resolve the file from the document identity itself, SHALL reject unknown presets, and SHALL NOT accept file paths, command lines, or arguments from the client. Markdown content SHALL not execute scripts or trigger automatic remote resource requests. The dashboard SHALL not send journal data, documents, searches, or comments to external services; explicit external links SHALL require user navigation.

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

### Requirement: Bounded dashboard preferences
Dashboard preferences SHALL remain within their storage limit regardless of how many documents the user opens or how many cards the user ranks. Per-document reading positions and remembered document selections SHALL retain only a bounded number of most recently used entries and discard older ones. The board priority order SHALL retain at most 200 entries, and preferences with every bound at its limit SHALL fit the storage limit. When the order is saved, entries for cards no longer on the board SHALL be discarded first, except entries for active changes outside the current repository selection, which SHALL be kept; ticket and ad-hoc entries outside the selection MAY be discarded. Then the lowest-ranked entries SHALL be discarded. A preference save that fails SHALL be reported once as a non-blocking notice and SHALL NOT produce a repeated error for each later scroll or interaction. A failed save SHALL NOT discard the in-memory preferences of the current visit.

#### Scenario: Many documents are read
- **WHEN** the user opens and scrolls through more documents than the retention bound
- **THEN** preferences continue to save successfully, and the most recently read documents keep their reading positions

#### Scenario: Preference save fails
- **WHEN** the preferences endpoint rejects a save
- **THEN** one notice explains that preferences could not be saved, and continued scrolling produces no further error banners

#### Scenario: Ranked change is archived
- **WHEN** a ranked change is archived and the user later reorders another card
- **THEN** the saved priority order no longer contains the archived change

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

### Requirement: Change activity summary
The dashboard SHALL provide, for each repository and recorded change name in the selected repositories, the latest recorded observation's status and occurrence time and the first recorded observation's occurrence time, computed from all journal history without modifying it. A change name with no observations SHALL be reported as having no recorded activity. The summary SHALL use the same read-only journal access as other dashboard views and SHALL treat a missing journal as empty.

#### Scenario: Latest status for a change
- **WHEN** a change has started, in_progress, and done observations
- **THEN** the summary reports done with the done observation's time as the latest activity

#### Scenario: Missing journal
- **WHEN** no journal exists in the data directory
- **THEN** the summary is empty and views show no recorded activity

### Requirement: External ticket and repository links
Wherever the dashboard shows a recorded ticket, it SHALL offer a link to the ticket when one can be determined, and an action that copies the ticket ID. A ticket's recorded URL SHALL be used when present. Otherwise the dashboard SHALL build a link only for recognized forms: a Jira ticket whose scope is a site host name, and an Azure DevOps ticket whose scope is an organization or an organization and project. Unrecognized forms and unresolved extracted ticket IDs SHALL be shown without a link. Repository identities on recognized Git hosts (GitHub, GitLab, Bitbucket, and Azure DevOps) SHALL link to the repository's web page, and observations with a recorded branch SHALL link to that branch on the same host. Local-only repository identities SHALL be shown without a link. Links SHALL use HTTPS, SHALL open only after explicit user navigation, and SHALL NOT trigger automatic requests to the external service.

#### Scenario: Ticket with recorded URL
- **WHEN** an observation's ticket includes a URL
- **THEN** the ticket ID links to that URL

#### Scenario: Jira ticket without URL
- **WHEN** a ticket has provider jira, scope `company.atlassian.net`, and ID `DEP-12` with no URL
- **THEN** it links to `https://company.atlassian.net/browse/DEP-12`

#### Scenario: Azure DevOps ticket without URL
- **WHEN** a ticket has provider azure-devops, scope `contoso/Payments`, and ID `4211` with no URL
- **THEN** it links to that organization's and project's work item 4211 on `dev.azure.com`

#### Scenario: Unrecognized scope
- **WHEN** a ticket's scope matches no recognized form and it has no URL
- **THEN** the ticket ID is shown with a copy action and no link

#### Scenario: Branch on a recognized host
- **WHEN** an observation in repository `github.com/acme/api` recorded branch `feature/DEP-12`
- **THEN** the drill-down links the branch to that repository's branch page

#### Scenario: Local repository
- **WHEN** a repository identity starts with `local:`
- **THEN** no repository or branch link is shown

### Requirement: History ticket filter
History SHALL accept an optional ticket filter. A bare ticket ID SHALL match a stored ticket with that ID or an unresolved extracted ticket ID; a `provider:scope:id` value SHALL match only a stored ticket with that provider, scope, and ID. Surrounding whitespace SHALL be trimmed. Matching SHALL be identical to the `query_work` tool's ticket filter. The filter SHALL select work items that have at least one matching observation and SHALL keep each selected item's full lifecycle context, spans, and insights. The filter SHALL combine with the repository selection, date range, and grouping, and SHALL be shown clearly while active.

#### Scenario: Follow one ticket
- **WHEN** the user filters History by `DEP-12`
- **THEN** only work items with an observation referencing DEP-12 appear, with their full recorded spans

#### Scenario: Qualified filter excludes another site
- **WHEN** the user filters by `jira:company.atlassian.net:DEP-12` and another observation references DEP-12 on a different Jira site
- **THEN** only the work referencing the specified site appears

### Requirement: Copy a work item as Markdown
The drill-down SHALL offer an action that copies the work item as Markdown: title, tickets with links when available, repository, first and latest observation dates, latest recorded status, associated changes, and each observation in the loaded scope with date, status, and summary. When the observations span more pages than are loaded, the action SHALL load them up to a bounded maximum and SHALL state in the copied text when that maximum truncated the list. The copied text SHALL describe recorded facts only.

#### Scenario: Copy a ticket's history
- **WHEN** the user opens a ticket's drill-down and selects Copy as Markdown
- **THEN** the clipboard contains the ticket's title, link, dates, status, and observation summaries in date order

### Requirement: Quiet active changes
Ongoing work SHALL indicate active changes whose latest recorded observation is older than 14 days as quiet, showing the latest recorded date, and SHALL indicate active changes without recorded observations as having no recorded activity. Neither indication SHALL imply that the change is abandoned or complete.

#### Scenario: Change without recent activity
- **WHEN** an active change's latest observation was 20 days ago
- **THEN** its card shows that no activity was recorded in the last 14 days, with the latest recorded date

### Requirement: Ongoing board
Ongoing work SHALL show the Review inbox followed by a board with the columns Proposed, In progress, and Done, each showing its card count. Columns SHALL be derived from recorded facts and SHALL NOT be set by the user. The board SHALL contain three card kinds:
- A change card for each active OpenSpec change in the selected repositories.
- A ticket card for each ticket-grouped work item in the selected repositories that has an observation within the last 7 days and is not associated with an active change on the board.
- An ad-hoc card for each observation within the last 7 days that has neither a ticket nor a change. Current logging requires a change or ticket, so such observations come from older journal entries.

A change present in several sources SHALL appear as one card.

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

### Requirement: Stable repository color cues
The dashboard SHALL assign each repository a color from a fixed palette based on its repository identity. Sidebar repository dots, Ongoing board repository dots, and single-repository board footer tints SHALL use the same assigned color family. Assignment SHALL be independent of repository label, catalog order, repository selection, board text filtering, card rank, and board column, and SHALL remain consistent across refreshes and later visits for the same identity. Light and dark themes SHALL preserve the color family while allowing different shades. Different repository identities can share a palette color; visible names SHALL remain the authoritative identifier. The All repositories sidebar entry SHALL remain visually distinct from a specific repository.

#### Scenario: Match a card to the sidebar
- **WHEN** a single-repository card is displayed on the Ongoing board
- **THEN** its repository dot and footer tint use the same color family as that repository's sidebar dot

#### Scenario: Selection and ordering do not change identity colors
- **WHEN** the user changes repository selection, filters or reorders board cards, or refreshes a catalog with repositories added, removed, renamed, or reordered
- **THEN** each remaining repository identity retains its assigned color family

#### Scenario: Reload and column movement
- **WHEN** a card moves to a different board column following recorded work and the dashboard is later reopened
- **THEN** its repository color family matches the assignment before the move and reload

### Requirement: Repository-colored Ongoing board footers
Every change, ticket, and ad-hoc card on the Ongoing board SHALL show a colored dot beside each repository name. A card associated with exactly one distinct repository SHALL have a softly tinted, full-width footer in that repository's color family. The main card body SHALL remain neutral and SHALL NOT gain a repository-colored edge stripe. A card associated with several distinct repositories SHALL keep a neutral footer and show a corresponding dot beside every repository name. A card with no usable repository identity SHALL keep a neutral footer without an invented repository color. Color cues SHALL preserve the card's existing content, footer layout, actions, and board behavior.

#### Scenario: All card kinds receive color cues
- **WHEN** change, ticket, and ad-hoc cards for a repository are displayed across the board columns
- **THEN** each single-repository card has the repository-colored footer and a matching dot beside its repository name

#### Scenario: Shared change has several repositories
- **WHEN** a change card is associated with two distinct repositories
- **THEN** its footer is neutral and each repository name has its own matching dot, without choosing one repository as the owner

#### Scenario: Duplicate repository references
- **WHEN** a card repeats the same repository identity in its repository associations
- **THEN** it is treated as a single-repository card for footer tinting and shows that repository label and dot once

#### Scenario: Repository identity is unavailable
- **WHEN** a card has no usable repository identity
- **THEN** it retains its neutral footer and existing fallback identification without a fabricated color assignment

### Requirement: Readable repository accents in both themes
Repository accents SHALL work in the dashboard's light and dark themes. All footer text SHALL retain a contrast ratio of at least 4.5:1 against its rendered footer background. Repository colors SHALL supplement visible repository names and SHALL NOT replace existing text or status cues. Repository dots SHALL be decorative for assistive technology. Recorded-status dots and status badges SHALL retain their existing meanings and color assignments, independently of repository colors.

#### Scenario: Change theme
- **WHEN** the user switches between light and dark themes while viewing the board
- **THEN** footer tints adapt to the theme, footer text meets the required contrast, and repository dots continue to match the sidebar color families

#### Scenario: Identify work without color
- **WHEN** repository colors cannot be distinguished by the user
- **THEN** repository names and recorded-status text still identify the repository and work status, without extra repository-dot announcements
