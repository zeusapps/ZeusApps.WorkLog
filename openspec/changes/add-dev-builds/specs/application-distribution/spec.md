# Spec Delta

## ADDED Requirements

### Requirement: Development builds
The application SHALL treat a version whose PATCH component is 9000 or higher as a development build of base `MAJOR.MINOR` and SHALL keep the strict `MAJOR.MINOR.PATCH` format used by launchers and payload directories. Stable packaging SHALL refuse versions in that range. A development payload's manifest MAY carry a `build` marker naming the dev channel, source commit, dirty state, build time, and base version; the marker SHALL be optional and additive to package format 2. The running and selected versions SHALL be displayed with one development label (version, `dev`, short commit, `dirty` when applicable, and build time in UTC) by `worklog --version`, `worklog update --status`, and the dashboard, and a development-range payload without a marker SHALL be labeled `dev (unmarked)`. Stable builds SHALL display their plain version.

Maintainer tooling SHALL package the next unused development version from the current checkout and SHALL install it into a versioned installation only through the product's verification, immutable publication, and atomic selection path, recording the previously selected payload for rollback. Preflight SHALL be read-only toward the installation and SHALL refuse unmarked or stale packages (package commit different from the checkout, or sources newer than the built package). Switching SHALL NOT stop or restart MCP servers, dashboards, or agent sessions; it SHALL report that running processes adopt the selection at their next normal restart. Rollback SHALL restore only the selection from immediately before the switch and SHALL refuse when the selection changed since. Retiring SHALL remove only a development-range payload that is not selected, not recorded as previous, not in use by a running process, and not referenced by an unfinished update, and SHALL never remove a stable payload. Switch, rollback, and retire SHALL require the user's explicit authorization for each operation. The procedure SHALL be available to Claude Code and Codex as the same agent skill text.

#### Scenario: Package a development build
- **WHEN** the maintainer packages a development build from a checkout whose base version is `0.3.0`
- **THEN** the package version is the next unused `0.3.9001`-or-higher version above existing dev packages and installed payloads, and its manifest records the dev channel, commit, dirty state, build time, and base version

#### Scenario: Stable packaging refuses the range
- **WHEN** stable packaging is asked for version `0.3.9001`
- **THEN** it refuses before building

#### Scenario: Switch to a development build
- **WHEN** the maintainer authorizes a switch to a fresh, marked development package
- **THEN** the payload is verified, published beside existing payloads, and selected for new processes with the original selection recorded as previous, while running servers and the dashboard keep their current payload

#### Scenario: Stale package
- **WHEN** the package commit differs from the checkout or sources are newer than the build
- **THEN** preflight refuses and the installation is unchanged

#### Scenario: Development label
- **WHEN** a marked development build is running
- **THEN** `worklog --version` prints its version followed by `dev`, the short commit, and the UTC build time

#### Scenario: Roll back a switch
- **WHEN** the maintainer authorizes rollback and the selection is still the one the switch produced
- **THEN** the original selection is restored for new processes and journal, configuration, and data are unchanged

#### Scenario: Retire a development payload
- **WHEN** the maintainer authorizes retiring a development payload that is selected, recorded as previous, or in use
- **THEN** retire refuses and removes nothing; when none of these holds, it removes only that payload directory

#### Scenario: Skill parity
- **WHEN** the Claude Code and Codex copies of the development-build skill differ
- **THEN** the repository test suite fails

## MODIFIED Requirements

### Requirement: Non-blocking stable release discovery
The application SHALL detect strictly newer stable semantic versions from the fixed public release source without requiring user credentials. For versioned installations, offered versions SHALL be newer than both the running payload and the currently selected payload. When the running or selected payload is a development build, its base version SHALL be used for that comparison; an unmarked development-range payload SHALL count as `MAJOR.MINOR.0`. Drafts, prereleases, development-range versions, malformed versions, and releases lacking the supported Windows ZIP or checksum SHALL NOT be offered. Automatic checks SHALL run independently of journal operations, use a shared machine-local cache to limit checks across repository processes sharing the selected data directory to at most one attempt per 24 hours, and support disabling automatic network checks. Requests SHALL have bounded durations and SHALL NOT transmit journal entries, project identities, branch names, tickets, or private configuration. Available release metadata SHALL NOT by itself establish package or shared-data compatibility; activation SHALL perform those checks separately.

#### Scenario: Newer compatible release
- **WHEN** the published stable version is greater than the running and selected versions and has both required assets
- **THEN** discovery makes its version and release link available as a candidate for explicit verified installation

#### Scenario: Equal, older, or unsuitable release
- **WHEN** the release is not newer than both versions, is malformed, is a prerelease, is in the development range, or lacks a required asset
- **THEN** no installation offer is emitted

#### Scenario: Stable release over a development build
- **WHEN** development build `0.3.9001` with base `0.3.0` is selected and stable `0.3.1` is published
- **THEN** discovery offers `0.3.1`

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

### Requirement: Explicit verified Windows update
The application SHALL provide `worklog update --check` to perform a fresh release check without downloading or installing a package, and `worklog update` to explicitly stage and activate a strictly newer supported release for a versioned installation. When the running or selected payload is a development build, "newer" SHALL be decided against its base version, so a stable release newer than that base is not refused as a downgrade. An explicit command SHALL work when automatic checking is disabled. Checks SHALL distinguish up-to-date, available, and failed states and report the running and selected versions. Installation SHALL verify the package checksum, reject archive paths escaping staging and unsafe links, validate the packaged version, runtime and compatibility declarations, and preserve the stable executable path, private configuration, journal files, and client registrations. Verified payloads SHALL be installed into separate immutable version directories; ordinary activation SHALL NOT overwrite the launcher or an existing published payload. Source-driven, pre-launcher, or unsupported installations SHALL receive actionable manual-install guidance rather than being overwritten. A checksum, metadata, extraction, or compatibility failure SHALL leave the selected version and existing payloads unchanged.

#### Scenario: Explicit check
- **WHEN** the user runs `worklog update --check`
- **THEN** the command reports running, selected, and available versions or a check failure without downloading a release package or changing application selection

#### Scenario: Verified package
- **WHEN** an explicit update retrieves a valid newer Windows package with compatible declarations and a verified executable
- **THEN** it publishes a new payload and selects it for subsequent invocations through the unchanged stable launcher

#### Scenario: Stable update over a development build
- **WHEN** development build `0.3.9001` with base `0.3.0` is selected and the user explicitly updates to verified stable `0.3.1`
- **THEN** `0.3.1` is published and selected, and the development payload is recorded as previous

#### Scenario: Invalid package
- **WHEN** checksum, version, runtime, manifest, archive containment, or compatibility validation fails
- **THEN** activation fails before selection changes and preserves existing payloads, journal, and settings

#### Scenario: Source invocation
- **WHEN** the updater is invoked from a source build, legacy layout, or unsupported installation
- **THEN** it declines activation and explains the supported manual installation or maintenance path

#### Scenario: Existing version directory
- **WHEN** an update encounters an existing directory for the candidate version
- **THEN** it verifies that directory before reuse or refuses it without overwriting it, and never treats a partial directory as a published payload
