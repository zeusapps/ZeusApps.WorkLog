# Design

## Context

See proposal.md for motivation and the two delta specs for behavior. This design is needed because discovery crosses CLI, MCP, configuration, and private agent integration, while replacing a running Windows executable requires a separate process.

The checkout currently uses `master`, has no remote, and reports application version `0.1.0` from Directory.Build.props. Existing workflows validate pushes to `main` and publish matching version tags only after Windows validation. scripts/package.ps1 produces a self-contained single-file executable, release.json, documentation, a ZIP, and a separate SHA-256 checksum. Installed clients register the executable's stable absolute path.

MCP tools return typed LogResult and Report objects with output schemas and a matching JSON text fallback. ReportPager enforces a 64 KiB structured payload limit. Neither response currently carries hints. WorkLogConfiguration has no update settings. RepositoryRegistry returns an already saved repository configuration before considering origin, so adding a remote must preserve that behavior.

## Goals / Non-Goals

**Goals:** Keep ordinary journal calls independent of external availability; make notices useful to both clients; support deliberate, recoverable updates of the existing Windows ZIP installation; finish publication with an observed working release.

**Non-Goals:** Automatic installation from MCP calls, new update MCP tools, terminating running clients, guaranteed client UI notifications, private-release authentication, prerelease channels, installers/package-manager distribution, or macOS/Linux support. Application-file recovery does not replace the documented database restore procedure.

## Decisions

### 1. Publish through the existing workflow

Use the confirmed public personal-account repository `zeusapps/ZeusApps.WorkLog` and `main`. During implementation, check authenticated account access and whether that repository already exists, inspect the outgoing tracked files/history for private material, rename the local primary branch without rewriting history, create the repository if absent, add origin, and push the prepared source. An existing unexpected repository or origin is a conflict to resolve, not an overwrite target. Preserve saved local journal identities; do not rewrite historical records to the GitHub identity.

Retain the tag/version agreement and validation gates. The first release is `v0.1.0` if that tag does not exist and the version remains unpublished; never move an existing published tag. Publish only after the update feature, docs, and packaged acceptance checks are complete. Observe remote validation and release jobs and independently inspect the uploaded ZIP/checksum before declaring publication complete. This reuses the working release structure rather than introducing a second distribution channel.

### 2. Separate discovery from journal work

Add focused release-discovery and update components inside the existing executable project, outside Journaling and Persistence. Use the public GitHub REST latest-release endpoint for the fixed repository; compare parsed semantic versions, not tag strings, and reject unsuitable metadata/assets. Stable releases only; no credentials or configurable arbitrary download feed.

Reference: [GitHub release API](https://docs.github.com/en/rest/releases/releases#get-the-latest-release). Revalidate the endpoint's current semantics and HTTP requirements during implementation using current documentation.

Add `updates.enabled`, default true. Keep cache and updater state in an `updates` subdirectory under the selected local data directory, separate from config.json and journal.db. A short-lived cross-process file lock plus an atomically persisted attempt timestamp permits only one automatic attempt per 24 hours for servers sharing that directory. Release the lock before the HTTP request; recording the attempt first prevents restart storms after a crash. Independent custom data directories intentionally have separate caches. Retry on the next interval; an explicit check can bypass the interval with bounded requests.

Run automatic discovery as cancellable background work. Initialization and calls only read a bounded local snapshot; they never await network activity. Use a short request timeout, bound metadata size, honor rate-limit retry guidance, and persist the last valid release and optional ETag atomically. Reject corrupt cache data and expire cached notices after seven days without a successful validation. Cache writes and failures are nonfatal, and shutdown cancels outstanding requests. Requests contain application identification and HTTP metadata only.

Synchronous per-call checks were rejected because they add latency and make journaling depend on GitHub. A startup-only notice was rejected because assistants may not surface startup diagnostics and servers can remain running for days.

### 3. Add bounded typed hints without changing journal meaning

Add an optional `updateHint` object to successful MCP LogResult and Report payloads with `installedVersion`, `availableVersion`, `releaseUrl`, and `command` (`worklog update`). Construct it from validated local release metadata and the running application version. Keep it in the MCP/application boundary; never persist it as an observation. The generated output schema and JSON text fallback must include exactly the same metadata. Errors retain their actionable error content and receive no hint.

When enriching a report, measure the complete serialized structured response. If a bounded hint would exceed the existing budget, omit that hint; do not reduce page entries, change cursors, or increment report schemaVersion. Update checks and explicit downloads live outside the local journal tool operations, preserving their annotations and local read/write semantics.

Update AgentInstructions for both clients to announce a version once per assistant session, finish the user's current task, and explain the explicit command/restart. The server can repeat hint metadata in later successful results so clients do not miss it; assistant instructions suppress repeated user-facing notices. This is best-effort presentation, not guaranteed delivery or a machine-wide dismissal mechanism. Rerunning init refreshes owned blocks only.

### 4. Stage explicitly, then replace from an external helper

Register `worklog update` beside init and serve. `--check` refreshes metadata and prints available/up-to-date/failed without downloading assets. `--status` reads the latest target-specific outcome without network access. Plain update checks for a newer stable version, then downloads the selected ZIP and checksum over HTTPS into a unique staging directory. Explicit commands remain available when automatic checks are disabled; an unavailable check exits unsuccessfully rather than claiming up-to-date.

Only supported packaged Windows installations qualify for replacement: validate the adjacent release manifest, platform, canonical installation path, and executable layout. Refuse source/dotnet-host invocations and unsafe or unwritable targets with manual instructions. Serialize updates per canonical installation path even when callers use different data directories.

Verify the ZIP checksum before extraction; bound download/extraction sizes, reject traversal or reparse/link entries, and validate the staged release manifest plus `worklog --version`. Preserve unowned files and prevent release contents from targeting journal/configuration locations. The checksum detects corruption; authenticity depends on HTTPS and control of the fixed release repository.

Because the original updater executable is loaded on Windows, copy the verified helper executable to a separate staging location and start its restricted internal apply mode with structured arguments, no shell-built script, and no visible extra window. It accepts only a validated target-bound update plan, rechecks the package and target, waits for the original updater process to exit, and refuses replacement if another target process or file lock remains. It must not launch the replacement until no target process is active, and must not terminate any process. Hidden helper completion is recorded atomically so `worklog update --status` reports the actual result.

Before handing off, the command prints `staged`, the target version, how to query status, and restart requirements. The helper uses a bounded application-file backup and replacement plan, retaining prior files until the new executable passes its version check. On replacement/verification failure it restores prior files where possible, otherwise leaves the backup/staging locations and precise recovery instructions in the outcome. Replacement never opens the journal or runs schema migrations; ordinary next startup retains the existing transactional migration/backup rules.

Silent self-installation was rejected because a hint does not authorize installation and active clients hold the executable. Download-only guidance was rejected because users explicitly want a supported update path; the existing manual upgrade remains the fallback.

## Risks / Trade-offs

- GitHub outages or rate limits: use shared throttling, bounded requests, and independent journal operations; explicit commands report failure honestly.
- MCP clients may omit optional fields: retain the matching JSON fallback and private assistant instructions, and document best-effort notice presentation.
- Long-running Windows processes or antivirus can block replacement: refuse without terminating clients, preserve files, and leave a retry/recovery outcome.
- A helper can finish after the invoking command exits: clearly distinguish staged and installed, and expose persisted status rather than claiming immediate success.
- Several files are replaced: rollback and failure injection tests are required; a multi-file update is not assumed to be a single atomic filesystem operation.
- New repository clones derive the GitHub identity while this initialized checkout retains its local identity: document the difference and avoid automatic journal regrouping.

## Migration Plan

1. Implement additive config and response fields, the checker, updater, instructions, tests, and documentation. Existing config files remain valid; journal schema stays unchanged.
2. Validate the packaged executable with mocked release metadata and an isolated prior-install fixture, including locked-file and recovery cases. Keep tests independent of live GitHub availability.
3. Publish the reviewed source to the confirmed repository on main, observe CI, push the matching release tag, and verify the downloadable artifacts. Do not mark completion while required remote validation or release checks remain pending.
4. Bootstrap existing development installations using the published ZIP and current manual instructions. Later packaged installations can use the updater. Restart clients and rerun init for repositories needing refreshed instructions.
5. If application replacement fails, restore prior application files from the update backup. If a later application startup migrates the journal, follow the existing consistent database-backup restore instructions rather than assuming a binary downgrade alone is safe.
