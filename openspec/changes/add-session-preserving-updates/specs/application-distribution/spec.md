# Spec Delta

## MODIFIED Requirements

### Requirement: Non-blocking stable release discovery
The application SHALL detect strictly newer stable semantic versions from the fixed public release source without requiring user credentials. For versioned installations, offered versions SHALL be newer than both the running payload and the currently selected payload. Drafts, prereleases, malformed versions, and releases lacking the supported Windows ZIP or checksum SHALL NOT be offered. Automatic checks SHALL run independently of journal operations, use a shared machine-local cache to limit checks across repository processes sharing the selected data directory to at most one attempt per 24 hours, and support disabling automatic network checks. Requests SHALL have bounded durations and SHALL NOT transmit journal entries, project identities, branch names, tickets, or private configuration. Available release metadata SHALL NOT by itself establish package or shared-data compatibility; activation SHALL perform those checks separately.

#### Scenario: Newer compatible release
- **WHEN** the published stable version is greater than the running and selected versions and has both required assets
- **THEN** discovery makes its version and release link available as a candidate for explicit verified installation

#### Scenario: Equal, older, or unsuitable release
- **WHEN** the release is not newer than both versions, is malformed, is a prerelease, or lacks a required asset
- **THEN** no installation offer is emitted

#### Scenario: Existing server after activation
- **WHEN** a running older server sees a cached release already selected for new processes
- **THEN** it suppresses the redundant installation offer without requiring a network request or restarting the server

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
Successful log_work and query_work responses SHALL support optional structured update-hint metadata containing the running version, available version, release link, and explicit stable-entry-point update command. Versioned installations SHALL also identify the selected version when a hint is returned; the existing installedVersion field SHALL continue to represent the running process version. The advertised output schemas and JSON text fallback SHALL agree with the returned payload. Hints SHALL remain separate from recorded work and SHALL NOT modify observation identity, idempotency behavior, report contents, cursor matching, or report schema version. The complete structured report including a hint SHALL remain within its existing byte budget. Tool calls SHALL use locally available discovery and installation-selection results without waiting for network access, and MCP stdout SHALL contain protocol traffic only.

#### Scenario: Offer update during normal work
- **WHEN** a tool succeeds and a usable cached release is newer than both running and selected versions with automatic notices enabled
- **THEN** the result includes a bounded hint alongside the unchanged work result unless the report byte budget requires omission

#### Scenario: Report close to byte limit
- **WHEN** including a hint would exceed the report byte budget
- **THEN** the hint is omitted while valid pagination and the bounded report are preserved

#### Scenario: Logging replay
- **WHEN** an idempotent logging retry receives an update hint
- **THEN** the returned observation still has its original ID and timestamps and no extra journal observation is created

#### Scenario: Activated release in an older conversation
- **WHEN** the advertised release is already selected while this conversation uses an older running payload
- **THEN** the tool emits no installation hint for that release and continues serving the conversation normally

### Requirement: Explicit verified Windows update
The application SHALL provide `worklog update --check` to perform a fresh release check without downloading or installing a package, and `worklog update` to explicitly stage and activate a strictly newer supported release for a versioned installation. An explicit command SHALL work when automatic checking is disabled. Checks SHALL distinguish up-to-date, available, and failed states and report the running and selected versions. Installation SHALL verify the package checksum, reject archive paths escaping staging and unsafe links, validate the packaged version, runtime and compatibility declarations, and preserve the stable executable path, private configuration, journal files, and client registrations. Verified payloads SHALL be installed into separate immutable version directories; ordinary activation SHALL NOT overwrite the launcher or an existing published payload. Source-driven, pre-launcher, or unsupported installations SHALL receive actionable manual-install guidance rather than being overwritten. A checksum, metadata, extraction, or compatibility failure SHALL leave the selected version and existing payloads unchanged.

#### Scenario: Explicit check
- **WHEN** the user runs `worklog update --check`
- **THEN** the command reports running, selected, and available versions or a check failure without downloading a release package or changing application selection

#### Scenario: Verified package
- **WHEN** an explicit update retrieves a valid newer Windows package with compatible declarations and a verified executable
- **THEN** it publishes a new payload and selects it for subsequent invocations through the unchanged stable launcher

#### Scenario: Invalid package
- **WHEN** checksum, version, runtime, manifest, archive containment, or compatibility validation fails
- **THEN** activation fails before selection changes and preserves existing payloads, journal, and settings

#### Scenario: Source invocation
- **WHEN** the updater is invoked from a source build, legacy layout, or unsupported installation
- **THEN** it declines activation and explains the supported manual installation or maintenance path

#### Scenario: Existing version directory
- **WHEN** an update encounters an existing directory for the candidate version
- **THEN** it verifies that directory before reuse or refuses it without overwriting it, and never treats a partial directory as a published payload

## ADDED Requirements

### Requirement: Recoverable Windows payload activation
Compatible payload activation SHALL succeed while existing MCP servers and other clients use retained payloads. The updater SHALL NOT terminate agent sessions, transfer live MCP connections, or require all installation processes to exit for ordinary activation. Publishers SHALL serialize selection changes and recheck the current selection before committing so stale operations cannot downgrade it. The application SHALL distinguish staged, activated-for-new-processes, and failed outcomes, expose local offline status through `worklog update --status`, and report running and selected versions. Existing connections SHALL retain their original payload and remain usable after activation; subsequent launches SHALL select the complete old or complete new payload. Failed validation or activation SHALL preserve or recover a valid selection and retain actionable recovery information without altering journal data. Successful activation SHALL explain that existing clients adopt the selected payload at their next normal reconnect or restart and that instruction refresh is a separate init operation.

#### Scenario: Active MCP process
- **WHEN** a compatible update activates while a packaged MCP server remains connected
- **THEN** that server retains its PID, version, and connection, continues logging and querying, and a new server launched through the same entry point uses the activated version

#### Scenario: Package only staged
- **WHEN** a verified package exists but selection has not committed
- **THEN** the command reports staged rather than activated and leaves new launches on the prior selected payload

#### Scenario: Activation succeeds
- **WHEN** the new payload passes validation and selection commits
- **THEN** status reports activation for new processes without claiming all existing clients have adopted it

#### Scenario: Status overlaps activation
- **WHEN** installed `update --status` processes overlap publishing and selection
- **THEN** they report a consistent local outcome or a bounded actionable read error, do not block activation solely by running, and never require stopping an existing MCP server

#### Scenario: Concurrent stale updater
- **WHEN** another updater has already selected a version equal to or newer than a stale candidate
- **THEN** the stale operation leaves the selection unchanged and reports that candidate as already selected or superseded

#### Scenario: Activation interruption
- **WHEN** publishing or selection is interrupted before completion
- **THEN** subsequent launches resolve a complete valid selection or fail with recovery guidance, never execute a partial payload, and preserve prior application payloads and journal data

### Requirement: Stable launcher and immutable payload selection
The self-contained Windows package SHALL provide a stable launcher entry point, validated selection metadata, and versioned application payloads. Launching through that entry point SHALL preserve argument boundaries, working directory, environment, stdin, stdout, stderr, and exit status. Stdio MCP stdout SHALL remain protocol-only. Selection resolution SHALL reject malformed, unsupported, missing, or out-of-installation targets and unsafe links with diagnostics on stderr. Ordinary updates SHALL retain all published payloads, including those used by live processes and prior selections; automatic deletion is outside this release.

#### Scenario: Launch after selection changes
- **WHEN** the selected payload changes between two invocations
- **THEN** the same entry point launches the selected complete payload for each invocation without changing existing processes

#### Scenario: Transparent command forwarding
- **WHEN** a command contains spaced or non-ASCII arguments and uses redirected standard streams
- **THEN** the payload receives the original arguments, directory and environment, bidirectional stream delivery and EOF work, and its exit status reaches the caller

#### Scenario: Invalid selection
- **WHEN** selection metadata is corrupt, incompatible with the launcher, points outside the installation, or references a missing or unverifiable payload
- **THEN** startup fails with actionable stderr guidance without executing that target or emitting non-protocol stdout

#### Scenario: Retained live and prior versions
- **WHEN** multiple updates succeed or activation fails
- **THEN** published payloads remain available, including the payload of a live server and the prior rollback selection, with no automatic cleanup race

### Requirement: Explicit mixed-version compatibility gate
Release metadata SHALL declare the supported launcher contract, shared-data contract, and journal schema independently of semantic application version. The first versioned release SHALL retain journal schema 2. Ordinary activation SHALL require the candidate to match the installation's supported launcher and shared-data contracts and to retain the supported journal schema and interoperable journal, configuration, retry, report-continuation, discovery-cache, and update-outcome formats. Missing or incompatible declarations SHALL cause refusal before selection changes or private data is migrated, even if no old process is observed. The supported concurrent-version baseline SHALL begin with the first launcher-based release; pre-launcher releases are excluded. Compatibility SHALL be supported by packaged tests using genuinely different application versions.

#### Scenario: Compatible old and new payloads
- **WHEN** two retained payload versions declare the same supported contracts and journal schema
- **THEN** supported concurrent logging, querying, settings writes, and retry behavior preserve shared local data and historical observations

#### Scenario: Incompatible candidate
- **WHEN** a candidate lacks declarations, changes the journal schema, or requires another launcher or shared-data contract
- **THEN** ordinary activation refuses it with maintenance instructions before changing selection or data

#### Scenario: Older supported payload remains running
- **WHEN** the selected version changes while an earlier supported payload is active
- **THEN** both processes continue operating within the declared shared-data contract and a stale update request cannot regress the selection

### Requirement: Explicit maintenance and selection rollback
Migration from the pre-launcher installation SHALL use documented manual stop, verified reinstall, owned-integration refresh, and restart steps that preserve the separate private data directory and repository identity. Updating the launcher or changing the shared-data contract SHALL remain explicit maintenance work. A local explicit rollback operation SHALL permit selecting the retained previous verified compatible payload, report adoption semantics, and leave journal/configuration data intact. Rollback SHALL NOT silently cross data-contract boundaries or restore a database backup. Initial installation and maintenance SHALL NOT be triggered by MCP hints.

#### Scenario: One-time legacy migration
- **WHEN** the user follows the documented maintenance procedure for a legacy installation
- **THEN** the launcher layout and owned registrations are established without replacing journal data, changing repository identities, or claiming existing conversations survived that migration

#### Scenario: Compatible selection rollback
- **WHEN** the user explicitly requests rollback and the previous payload is present, verified, and compatible
- **THEN** new launches select that payload, existing servers continue, and journal entries and configuration are not reverted

#### Scenario: Unsafe rollback
- **WHEN** the previous payload is absent, corrupt, or incompatible with the current contracts
- **THEN** rollback refuses selection changes and explains recovery or maintenance requirements

## REMOVED Requirements

### Requirement: Recoverable Windows update completion
**Reason**: Its idle-installation, parent-exit and in-place replacement contract is replaced by Recoverable Windows payload activation for the new launcher layout. The legacy F9 readiness implementation is superseded rather than shipped.
**Migration**: Use the documented one-time manual stop/reinstall/init/restart procedure to establish the launcher layout. Subsequent compatible releases activate new immutable payloads while existing servers continue. Launcher or shared-data-contract changes remain explicit maintenance operations; legacy releases have no seamless update guarantee.
