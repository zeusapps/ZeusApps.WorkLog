# Spec Delta

## MODIFIED Requirements

### Requirement: Windows executable release
The project SHALL provide a self-contained Windows x64 release that runs without a separately installed .NET runtime and supports init, serve, update, and --version. Release packages SHALL include installation and upgrade instructions and checksums. Data SHALL be stored separately from the application installation.

#### Scenario: New Windows machine
- **WHEN** the release is extracted on a supported Windows machine without .NET installed
- **THEN** version reporting and stdio MCP startup work

#### Scenario: Discover update commands
- **WHEN** the user requests application help
- **THEN** help describes checking for and explicitly installing an update

## ADDED Requirements

### Requirement: Public release source
The project's source and versioned Windows release packages SHALL be published in the public personal-account repository `zeusapps/ZeusApps.WorkLog`. Its primary branch SHALL be `main`, consistent with push validation. Release tags SHALL match application versions and publication SHALL retain the existing validation gates. Repository creation and publication SHALL be explicit operational actions separate from application startup, initialization, and update checks. Adding the remote SHALL NOT rewrite an already saved local journal repository identity or historical observations.

#### Scenario: First publication
- **WHEN** the prepared source is published and a matching version tag passes release validation
- **THEN** the public repository contains the source and the release exposes the Windows ZIP and checksum for that version

#### Scenario: Existing local journal
- **WHEN** the GitHub remote is added to an already initialized checkout
- **THEN** its saved repository identity and existing journal observations remain unchanged

### Requirement: Non-blocking stable release discovery
The application SHALL detect strictly newer stable semantic versions from the fixed public release source without requiring user credentials. Drafts, prereleases, malformed versions, and releases lacking the supported Windows ZIP or checksum SHALL NOT be offered. Automatic checks SHALL run independently of journal operations, use a shared machine-local cache to limit checks across repository processes sharing the selected data directory to at most one attempt per 24 hours, and support disabling automatic network checks. Requests SHALL have bounded durations and SHALL NOT transmit journal entries, project identities, branch names, tickets, or private configuration.

#### Scenario: Newer compatible release
- **WHEN** the published stable version is greater than the running version and has both required assets
- **THEN** discovery makes its version and release link available for an update notice

#### Scenario: Equal, older, or unsuitable release
- **WHEN** the release is equal, older, malformed, a prerelease, or missing a required asset
- **THEN** no update is offered

#### Scenario: GitHub unavailable
- **WHEN** a release request times out, is rate limited, or returns invalid metadata
- **THEN** journal logging, querying, startup, and shutdown continue without waiting for that request and no successful update check is falsely reported

#### Scenario: Multiple repository servers
- **WHEN** several WorkLog servers share the same local update cache during a 24-hour interval
- **THEN** they coordinate to perform at most one automatic release request attempt in that interval

#### Scenario: Automatic checking disabled
- **WHEN** automatic checking is disabled
- **THEN** the server sends no automatic release requests and emits no automatic update notices

### Requirement: Bounded MCP update hints
Successful log_work and query_work responses SHALL support optional structured update-hint metadata containing the installed version, available version, release link, and explicit update command. The advertised output schemas and JSON text fallback SHALL agree with the returned payload. Hints SHALL remain separate from recorded work and SHALL NOT modify observation identity, idempotency behavior, report contents, cursor matching, or report schema version. The complete structured report including a hint SHALL remain within its existing byte budget. Tool calls SHALL use locally available discovery results without waiting for network access, and MCP stdout SHALL contain protocol traffic only.

#### Scenario: Offer update during normal work
- **WHEN** a tool succeeds and a usable cached newer release exists with automatic notices enabled
- **THEN** the result includes a bounded update hint alongside the unchanged work result unless the report byte budget requires omission

#### Scenario: Report close to byte limit
- **WHEN** including a hint would exceed the report byte budget
- **THEN** the hint is omitted while valid pagination and the bounded report are preserved

#### Scenario: Logging replay
- **WHEN** an idempotent logging retry receives an update hint
- **THEN** the returned observation still has its original ID and timestamps and no extra journal observation is created

### Requirement: Explicit verified Windows update
The application SHALL provide `worklog update --check` to perform a fresh release check without downloading or installing a package, and `worklog update` to explicitly stage and apply a strictly newer supported release. An explicit command SHALL work when automatic checking is disabled. Checks SHALL distinguish up-to-date, available, and failed states. Installation SHALL verify the package checksum, reject archive paths escaping staging, validate the packaged version and runtime, and preserve the stable executable path, private configuration, journal files, and client registrations. Source-driven or unsupported installations SHALL receive actionable manual-install guidance rather than being overwritten. A checksum, metadata, or extraction failure SHALL leave the installed version unchanged.

#### Scenario: Explicit check
- **WHEN** the user runs `worklog update --check`
- **THEN** the command reports current and available versions or a check failure without downloading a release package or changing installed files

#### Scenario: Verified package
- **WHEN** an explicit update retrieves a valid newer Windows package and checksum
- **THEN** the update is staged separately from the journal and can replace the existing supported installation at its stable path

#### Scenario: Invalid package
- **WHEN** the checksum differs, the packaged version/runtime differs from the selected release, or an archive entry escapes staging
- **THEN** installation fails before replacing any installed file and preserves the journal and settings

#### Scenario: Source invocation
- **WHEN** the updater is invoked from a source build or an unsupported installation
- **THEN** it declines replacement and explains the supported installation or manual upgrade path

### Requirement: Recoverable Windows update completion
Replacement SHALL occur only after the updater's original process exits and all other processes using the target installation have stopped. The updater SHALL NOT terminate agent sessions or active MCP servers automatically. It SHALL refuse replacement when the target is busy, unwritable, or has another update in progress, and report an actionable result. It SHALL distinguish staged, installed, and failed states, expose the eventual helper outcome through `worklog update --status`, and retain recoverable prior application files until the new executable passes a version check. Failed replacement or verification SHALL restore prior application files when possible and otherwise retain recovery files with instructions. Successful installation SHALL explain client restart and instruction-refresh steps. Existing transactional journal migrations and restore-based rollback rules SHALL continue to apply.

#### Scenario: Active MCP process
- **WHEN** another process is using the target installation
- **THEN** replacement is refused with instructions to stop affected clients and retry, without terminating those processes or changing installed files

#### Scenario: Helper has not finished
- **WHEN** the command has staged a verified update but the replacement helper has not completed
- **THEN** the command reports staging rather than successful installation and explains how to inspect the eventual outcome

#### Scenario: Replacement succeeds
- **WHEN** the idle installation is replaced and the new executable reports the selected version
- **THEN** the outcome reports installed and advises restarting MCP clients and rerunning init where managed instructions need refreshing

#### Scenario: Replacement fails
- **WHEN** replacement or post-install version verification fails
- **THEN** the outcome reports failure, attempts to restore the prior application, and leaves recovery information available without altering journal data
