# Handoff: Update WorkLog without stopping existing MCP sessions

Captured on 2026-10-22 while planning Bell Tower's GitHub distribution and updater. The user explicitly requested a WorkLog handoff for the same versioned-installation approach. This document records investigation and proposed follow-up work; no WorkLog implementation, client reconfiguration, application update, or publication has been performed.

## Outcome and important distinction

Users should be able to install and activate a compatible WorkLog release while existing Claude/Codex conversations and their MCP servers remain running. Existing MCP servers continue executing their original version; new server processes use the newly selected version. Existing clients obtain the new implementation on their next normal reconnect/restart. Do not describe this as transferring a live stdio MCP connection into a new server process.

Bell Tower's tray can close and reconnect to independently running session infrastructure. WorkLog's stdio server is the endpoint of an existing client connection. Keeping that server alive is the practical equivalent; transparent hot replacement of an active MCP stream is a separate architecture and is outside this proposed follow-up.

## Observed baseline

- `docs/releases.md` describes the public `zeusapps/ZeusApps.WorkLog` repository, `main`, Windows validation, version tags, ZIP/checksum publication, and packaged updater acceptance.
- `.github/workflows/validate.yml` validates and packages; `release.yml` calls validation before publishing a matching tag.
- `src/WorkLog/Updates/UpdateInstaller.cs`, `UpdateHelper.cs`, `UpdateInstallation.cs`, and `ApplicationReplacement.cs` stage an archive, wait for the initiating process, require an idle installation, and replace application files in place. `Program.cs` also coordinates packaged `serve`/`init` processes with `.worklog-update.lock`.
- `src/WorkLog/Integration/ServerRegistration.cs` defaults registrations to `Environment.ProcessPath`. A version-directory payload must not accidentally register itself instead of the stable launcher.
- `src/WorkLog/Mcp/McpServerHost.cs` owns each stdio server and background update discovery. Existing servers can remain on an older version independently of a new server.
- `src/WorkLog/Persistence/JournalSchema.cs` currently supports schema version 2, rejects newer schemas, backs up before upgrades, and applies transactional migrations. `JournalDatabase.cs` coordinates initialization and uses WAL. WAL and migration transactions alone do not prove that old/new application versions can safely share data.
- Existing update tests are under `tests/WorkLog.Tests/Updates/`; packaged acceptance is in `tools/WorkLog.Smoke/UpdateAcceptanceChecks.cs`.

Re-read these files before implementation: this is a source snapshot, not a promise that the pending changes below have shipped.

## Proposed installation and activation model

```text
WorkLog/
  worklog.exe                   stable launcher used in client registrations
  current.json                  atomically selected, verified payload
  versions/
    0.1.1/worklog.exe            retained while old MCP processes use it
    NEXT/worklog.exe             new CLI/server invocations use this version

Private data directory/
  journal.db, config.json, ...   preserved independently of installation
  updates/                      cache, staging, outcomes, recovery metadata
```

1. Keep the launcher small and independent of ordinary payload releases. Resolve only a validated version directory beneath this installation. Preserve exact arguments, working directory, environment, exit code, stdin, stdout, and stderr. MCP stdout must remain protocol-only. Prefer inherited standard handles where practical; if relaying streams, test full-duplex delivery, EOF, cancellation, and backpressure against the current MCP SDK/client contract.
2. Record the stable installation identity separately from `Environment.ProcessPath`, which will point into `versions/`. Registrations, managed instructions, user-visible update commands, and diagnostics must use the stable entry point. Internal helpers may deliberately pin their own version.
3. Download and verify into a new, unpublished version directory. Retain official-feed, bounded-download, checksum, path traversal/reparse, manifest, executable-version, and downgrade checks. Do not modify a published payload in place.
4. Serialize publishers per installation. Validate compatibility and payload health, then atomically switch the selected-version metadata. Concurrent launches must choose either the complete old payload or the complete new payload. Existing servers stay running and keep their stdio connections; do not require a global installation-idle check for ordinary payload activation.
5. Distinguish staged, activated-for-new-processes, and failed outcomes. Report both the running process version and selected version. Old servers must not repeatedly urge installation of a version already activated, and an old server must never downgrade the selected version by applying its stale cached offer.
6. Retain the prior known-good selection for application rollback. Do not roll back journal data implicitly. Cleanup must not remove payloads with live users, in-progress launches, rollback references, or recovery needs; failed cleanup is retryable. Define a launch/cleanup lease to close the race between resolving a version and starting it, rather than relying on one process enumeration.

## Compatibility policy is a release gate

The first implementation should leave the journal schema unchanged and explicitly support old/new processes sharing that schema, settings, journal payloads, update outcomes, and cache metadata. Preserve unknown configuration fields when a supported older writer saves; otherwise stop that mixed-version path before activation. Prove logging, queries, idempotent retries, and cross-process updates under actual concurrency.

Package metadata must declare the relevant data/protocol compatibility range independently of the application semantic version. Old servers continue writing after activation, so a new binary's ability to read old data is insufficient. Reject or defer incompatible activation before changing selection or data, and explain the required maintenance step. Future breaking journal/configuration changes may require a planned client restart window; never promise uninterrupted updates across arbitrary schema migrations.

## Migration and launcher maintenance

The existing installed `worklog.exe` is a full application, and client registrations point directly to it. It cannot be replaced with a launcher while existing processes lock that same file. Plan and document a one-time migration window to establish the stable launcher/versioned layout and refresh owned registrations. An alternate new installation path plus explicit registration migration could avoid replacing that locked file, but is a separate option to validate, not an automatic rewrite of user settings.

Future ordinary updates switch payloads only. The launcher itself can remain loaded for the lifetime of forwarded MCP servers; replacing it may still require maintenance. Keep its version contract backward-compatible, version it separately, and describe bootstrap updates honestly. Do not conceal this limitation behind claims of fully hot updates.

## Relationship to pending work

`openspec/changes/fix-update-status-read-race` is planning-only at inspection time. It addresses transient status readers conflicting with the old in-place replacement helper. Versioned payloads remove that conflict from normal version activation, but do not fix legacy installs or bootstrap migration automatically. Reconcile the two plans explicitly; preserve or finish legacy protection where still needed, and do not silently mark its tasks complete.

Other pending changes (`fix-claude-project-path-aliases`, `add-ticket-report-grouping`, `verify-worktree-server-startup`) are independent. Preserve existing client registration ownership, instruction opt-outs, repository identities, historical entries, and ticket semantics. Do not bundle unrelated fixes or add a Bell Tower dependency.

## Suggested fresh-session work

1. Inspect `git status` and current OpenSpec context. Read this handoff, the current distribution spec, the archived release/update design, and the pending status-race artifacts. Create a separate OpenSpec proposal for session-preserving versioned updates; this handoff alone does not authorize implementation.
2. Design the launcher/installation resolver and data compatibility contract before editing updater code. Update distribution requirements that currently require all installation processes to stop, retaining all package verification and preservation requirements.
3. Adapt packaging, version selection, update discovery/status messages, registration path resolution, cleanup coordination, rollback, and packaged acceptance. Keep the first supported mixed-version release schema-neutral.
4. Run disposable Windows acceptance with a real packaged old server connected over stdio, activate a genuinely different packaged version, continue logging/querying through the original connection, then open a second server through the stable launcher. Assert the original PID/version/connection survives, the new server runs the new version, both see the intended shared data, and retries remain idempotent.
5. Exercise simultaneous launches/updates/cleanup, corrupt pointer/package/hash/manifest, interrupted activation, read-only paths, stale older updater attempts, offline operation, incompatible data declarations, rollback, and live old-version retention. Include status reads during activation. Retain exact versions and logs; same-binary copies alone do not prove mixed-version compatibility.
6. Validate client commands and stdio behavior against current official MCP/client documentation using the repository's documentation lookup instructions. Use isolated client homes and data; do not terminate real sessions, change production registrations, migrate the user's journal, or publish releases as a test fixture.

## Acceptance boundary

Ordinary compatible updates must succeed without stopping existing MCP servers or agent conversations. Existing connections continue on their original version; new connections use the activated version. A successful activation is distinct from every live client having adopted the new code. Incompatible data changes and bootstrap migration remain explicit maintenance cases with actionable guidance.
