# Design

## Context

See [proposal.md](proposal.md) and the [handoff](../../../../docs/handoffs/2026-10-22-session-preserving-updates.md). Source baseline is `fa48ada` / v0.1.1. The owner confirmed there are no other real users and accepted a one-time maintenance migration, deferred automatic cleanup, and supersession of the standalone F9 implementation. This design does not promise coexistence with a still-running pre-launcher binary.

Observed integration points:

| Area | Current behavior | Required adaptation |
| --- | --- | --- |
| `Program.cs`, `Updates/UpdateInstallation.cs` | Installation is the process directory; packaged serve/init share an installation lock | Resolve stable installation separately from running payload; remove the global idle requirement for compatible activation |
| `UpdateInstaller`, `UpdateHelper`, `ApplicationReplacement` | Copy helper, wait for parent, verify idle, replace managed files | Publish an immutable payload and atomically select it; ordinary activation can finish synchronously |
| `ReleasePackage`, `scripts/package.ps1` | Flat allowlist, three-field release manifest, one packaged executable | Validate a bounded versioned package and independently version the launcher contract |
| `UpdateDiscovery`, `UpdateCommand`, `UpdatePlan` | Compare to running version; outcomes keyed using process directory | Read current selection, use stable installation identity, reconcile persisted outcomes with actual selection |
| `Integration/ServerRegistration`, `RepositoryInitializer` | Register `Environment.ProcessPath`, including the executing binary | Resolve and register the stable launcher while preserving ownership and source behavior |
| `Configuration/LocalSettings`, `Repositories/RepositoryRegistry` | Load once, then serialize whole typed configuration; repository creation can save during server startup | Preserve unknown fields and coordinate current-state mutations and first-use identity creation |
| `Integration/InitializationPlan` | Detect changes since planning before applying files; incomplete runs report already applied paths | Integrate settings coordination without losing dry-run, conflict, ownership, backup, and partial-failure behavior |
| `JournalSchema`, `JournalDatabase`, `Reporting/ReportCursor` | Schema 2, initialization mutex, SQLite transactions/WAL, cursor query hash and row snapshot | Keep formats stable and prove genuine cross-process behavior across supported versions |
| Windows workflows and `tools/WorkLog.Smoke/UpdateAcceptanceChecks.cs` | Exercise same-binary helper replacement and active-server refusal | Exercise distinct packaged versions through a launcher while the original MCP stream stays connected |

## Goals / Non-Goals

**Goals:** Establish a small installation contract, preserve normal stdio lifetime and private integration, and make activation a bounded local commit after package verification. Compatibility is a declared and tested property, rather than an assumption inferred from application version or an unchanged SQL schema.

**Non-Goals:** Legacy in-place updater repairs, a seamless first migration, live stream transfer, services or sockets, client configuration rewrites during ordinary update, automatic deletion, arbitrary schema migration while old writers run, or transparent launcher self-update. Application rollback changes selection only.

## Decisions

### 1. Stable launcher and an explicit installation layout

Add a small self-contained .NET 10 Windows console launcher project, with no MCP, database, CLI-framework, or network dependencies. Reuse the repository toolchain instead of adding a native-language build stack. Its externally visible contract is separate from the payload application version.

```text
Installation/
  worklog.exe                         stable launcher
  launcher.json                       launcher version and supported contract
  current.json                        current + previous selection, generation, operation ID
  current.previous.json               recovery copy of the last valid selection document
  versions/
    A/worklog.exe                     immutable payload
    A/release.json                     immutable payload manifest and file hashes
    B/worklog.exe
    B/release.json
  README.md, INSTALL.md

Private data/
  journal.db, config.json
  updates/                            release cache, bounded operation outcomes, download staging
```

The package ZIP includes an initial selection for fresh extraction, the launcher metadata, exactly one payload, and documentation. Each payload manifest declares `packageFormat: 2`, application version, `win-x64`, self-contained status, `launcherContract: 1`, `sharedDataContract: 1`, `journalSchema: 2`, and hashes of owned payload files. The selection pins the payload manifest hash. Bootstrap metadata records the actual launcher version and supported contract; an ordinary payload update never replaces it or the launcher. Root documentation may remain the bootstrap copy; current version-specific instructions are also available with the payload/release.

The shared-data contract is a deliberately narrow compatibility epoch, not a general range solver: the initial implementation accepts exact contract equality and schema 2. A different required launcher contract or shared-data epoch is a maintenance refusal, even with no observed live process. This covers all retained supported payloads and custom data directories without relying on incomplete process enumeration. Pre-launcher v0.1.1 is not a member of this epoch.

Validate bounded metadata, normalized semantic-version directory names, required files, manifest/executable agreement, hashes, containment, and absence of reparse points. Resolve the installation from the fixed relationship of an installed payload to `versions/<version>` and the validated root, not an unvalidated environment override. Never follow an arbitrary executable path from `current.json`. Source invocation keeps its existing development path; unsupported layouts decline self-update.

### 2. Transparent forwarding with a supervised child lifetime

The launcher resolves one validated selection snapshot, starts that payload, waits for it, and returns its exit code. Use an absolute executable path, direct process creation, structured argument forwarding, the caller's working directory and environment, and inherited standard streams. The launcher never parses MCP messages or writes normal startup messages to stdout. A pointer failure is an actionable stderr failure, not an automatic launch of an arbitrary fallback.

Keep child lifetime tied to the launcher on Windows, including forced client termination, using a launcher-owned child lifetime/job mechanism without touching unrelated processes. Do not detach the payload. Graceful stdin closure must reach the server and allow both processes to exit. Normal terminal cancellation must preserve expected CLI behavior. Prevent handle inheritance from retaining pipe write ends that would suppress EOF. A job setup failure must not leave an orphan server; contain or terminate only the launcher-owned child and report failure. Verify compatibility with already-jobbed client environments in acceptance.

Inherited handles avoid maintaining an extra full-duplex byte relay. If .NET's standard launch path cannot satisfy the required Windows pipe/lifetime behavior, use a narrow Windows process-launch adapter within this launcher; preserve the same behavioral contract. This is a specific implementation verification gate, not permission to introduce another transport.

Documentation checked during planning: [.NET argument forwarding](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.argumentlist?view=net-10.0), [direct launch and working directory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute?view=net-10.0), [MCP stdio framing](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports), and [MCP initialization/shutdown](https://modelcontextprotocol.io/specification/2025-11-25/basic/lifecycle). These establish argument/transport requirements, not proof of the Windows handle or client behavior; disposable packaged tests supply that proof. Retain the SDK's negotiated protocol behavior rather than upgrading protocol or SDK as a side effect. Context7's process lookup returned largely unrelated historical snippets, so the official API pages were consulted directly.

### 3. Publish complete payloads, then commit selection

Use a separate installation publisher lock with bounded acquisition (at most 30 seconds, cancellable). Launchers and status readers never hold it for process lifetime. Download and verify outside that lock, using the existing fixed public feed, bounded downloads, ZIP/checksum verification and no-reparse checks. Do not weaken verification to accommodate the nested package layout: derive an exact bounded allowlist from the candidate version and package format; reject duplicate/case-colliding entries, traversal, links and unexpected files.

After extraction, check the bootstrap/launcher compatibility declarations, payload file hashes and actual executable version, and run an isolated initialize/tools-list health probe against a disposable data directory. Health probes never open or migrate the user's journal. Existing data must already satisfy the supported contract; selection itself opens no journal and saves no private configuration.

Acquire the publisher lock, reread the original selection and operation generation, and recheck target containment, immutable content, candidate compatibility, and strict newness against running and selected versions. A changed generation is never silently overwritten: report already selected/superseded when appropriate, otherwise refuse the stale operation and ask for a fresh explicit command. This also protects intentional rollback from a previously staged update. Compare semantic versions, not strings.

Copy verified payload files into a new temporary directory under the installation's `versions` directory, revalidate, then rename to its final version directory on the same volume. Downloads may be on another volume; directory publication does not assume a cross-volume rename is atomic. A preexisting final directory is reusable only after full immutable-content verification; differing or partial content is a conflict, never overwritten.

Write the recovery selection copy, then atomically replace `current.json` with the complete new selection including previous payload, incremented generation, and operation ID. Treat this replacement as the activation commit. A launcher that read the previous pointer remains safe because old payloads are retained. Hold no lifetime-wide installation lease and perform no global idle scan for this path. Finish synchronously with `activated`; no copied helper or parent-exit wait is needed for ordinary activation.

Status is read-only, bounded, offline, and keyed to the stable installation. The selection is authoritative if a crash occurs after commit but before the outcome file is saved. Report the observed selection plus any operation-specific outcome, and never present an old data-directory outcome as the current installation state. Lock contention, read-only installation paths, health failure and interrupted publication yield actionable failure/recovery information with prior payloads retained. Crash consistency does not claim invulnerability to arbitrary storage corruption; invalid pointer metadata fails closed and uses documented recovery.

### 4. Preserve shared data with compatible writers

`sharedDataContract: 1` covers schema 2, stored observation fields, retry normalization/hashing, first-start uniqueness, query/cursor encoding, configuration semantics, and discovery/outcome formats. It also covers the byte-budget and JSON-fallback behavior of MCP results. Future releases must maintain those semantics to reuse the epoch. A new feature such as F8 must separately assess old-writer and cursor compatibility; this change does not implement or pre-approve it.

Keep raw configuration JSON alongside its typed projection, and express mutations as changes to known paths instead of reserializing the typed object over the whole file. Preserve unrecognized root and nested fields, including inside repository context/repository/update objects. Arrays or known leaves deliberately changed by the user keep their documented replacement semantics. Do not use .NET 11-only JSON extension-data APIs in this .NET 10 project. [.NET's overflow guidance](https://github.com/dotnet/docs/blob/main/docs/standard/serialization/system-text-json/handle-overflow.md) explains why typed round trips alone do not preserve unknown members.

Introduce a per-canonical-data-directory settings writer lock with bounded acquisition. All supported writers participate, including server-side repository creation, `LocalSettings.Save`, and `RepositoryInitializer`'s planned config write. Under the lock reload the latest document, resolve the intended repository against current state, merge nonconflicting known-path edits and preserve everything else, then atomically save and adopt the saved projection. For edits derived from an earlier snapshot, compare changed leaves with their original values; permit equal/idempotent or independent edits, reject conflicting edits. A lock around serialization of an already stale snapshot is insufficient.

For init, perform external client availability checks first, then hold the settings writer lock across refresh, context/preference planning, file preflight and application. Retain `InitializationPlan`'s before-content checks, backups, and explicit partial-failure reporting; no automatic retries that replay already applied client writes. Dry runs and validation failures do not persist settings. Concurrent initial context creation must resolve once under this same lock so all processes adopt one local identity. Other initialization files retain their existing conflict protections; unrelated client-settings transaction redesign is outside scope.

The initializer lock and journal initialization lock serve different purposes. Release the settings lock before opening/initializing the journal and avoid inverted acquisition order. Existing live servers may continue their documented startup configuration snapshot for reads; each subsequent supported settings write reloads and merges current disk state. Ordinary activation itself changes neither settings nor journal. Cross-process SQLite/retry tests, not just the existing migration mutex or WAL setting, establish mixed-version support.

### 5. Stable identity in registration, hints and CLI

Expose one validated installation context: stable root/launcher path, running payload version/path, selected payload version/generation, and launcher/shared-data contracts. Registration and ownership manifests use the stable launcher; internal version checks and health probes can pin a payload deliberately. This applies to init run from a retained payload too. Preserve existing executable overrides used by isolated development fixtures, and validate production resolution instead of relying on those overrides.

`--version` continues reporting the running payload version. `update --check` and `update --status` additionally report the selected version and adoption meaning. Use `staged`, `activated`, and `failed` for the new flow; documentation must not treat legacy `installed` outcomes as proof of current selection. Add mutually exclusive `update --rollback` alongside check/status/install to select the previous compatible payload locally. It is explicit, offline, and uses the same publisher lock, health/compatibility checks, generation commit and recovery model; it restores no private data. If the selected application cannot run at all, document offline selection-file recovery from the verified recovery copy rather than pretending its CLI is available.

Keep `installedVersion` in MCP hints as the running version for existing consumers; add bounded `selectedVersion` context. Construct a correctly quoted explicit command using the validated stable launcher and the same data-directory context. Compare a cached offer to both running and selected versions on each hint construction; do not cache selection for the lifetime of a server. If selection cannot be read safely, omit the optional hint rather than risk a downgrade offer or block journal work. Preserve schemas, fallback JSON, and the 64 KiB report limit. Managed instructions explain that activation can happen now while adoption occurs at normal reconnect/restart; no hint authorizes installation.

### 6. Retention, rollback and maintenance boundaries

Retain every published payload in this first version. No cleanup command, background pruning, or deletion of published version folders is introduced, so launch-to-cleanup leases and process-enumeration heuristics are unnecessary here. Temporary operation directories can be left for manual recovery; do not delete a concurrently used staging directory. Disk usage is an accepted tradeoff for the sole user. A future cleanup change must define coordinated launch/use/rollback leases before adding deletion.

Compatibility equality makes application-selection rollback safe within the declared epoch; it does not make arbitrary binary downgrades safe. Reject rollback across contracts and document database backup restore separately. Launcher replacement and future epoch/schema changes require stopping relevant sessions and a deliberate maintenance installation. No automatic maintenance fallback is attempted by `update`.

The owner accepted superseding `fix-update-status-read-race`: its old in-place replacement/readiness solution is not a prerequisite and its F9 tasks remain unimplemented. The new installed-status acceptance replaces the relevant user-facing regression in the versioned architecture. Do not merge its delta into the main spec or claim its tests passed. This new change records the decision; retiring or otherwise disposing of the existing F9 change requires its own authorized workflow. F6, F7 and F8 artifacts are untouched.

### 7. Packaged proof and release gates

Replace the updater smoke's false-version manifest fixture: it currently copies the same executable into both versions. Build two actual launcher-compatible application versions A and B with different compiled versions and hashes; query `--version` and MCP serverInfo for each. Bootstrap testing cannot use legacy v0.1.1 as the supported old payload. Initial acceptance builds two separate fixture versions using the new contract; later releases must also test the pinned first supported published baseline against the candidate. Test-only version builds must not edit the working tree's version or weaken production tag/version agreement.

Keep one real A MCP stream open through activation, log/query before and after through that stream, then start B through the identical launcher. Record launcher/payload PIDs and executable paths so a new wrapper process cannot be mistaken for preservation of the original payload. Test cross-version replay/conflicting reuse/first-start races, cursor continuation snapshots, independent and conflicting settings writes, nested unknown fields, shared cache/outcome behavior, and a second custom data directory using the same installation. Demonstrate old-process offer suppression after B becomes selected.

Coordinate simultaneous publishers, launches, status readers and rollback deterministically using internal fixture seams rather than arbitrary sleeps. Inject interruptions before publish, before selection, and after commit before outcome; verify selection truth and retained recovery. Cover checksum/hash/manifest/version failures, traversal/reparse entries, corrupted selection, missing/partial version folders, unwritable roots, offline check/status behavior, incompatible contracts/schema, launcher lifetime termination and EOF, and retained A after further activation/rollback. Only disposable fixture-owned processes and paths can be touched.

Update `scripts/package.ps1`, the solution/build inputs and Windows validation workflow so the launcher is packaged and real mixed-version acceptance gates release. Use current isolated client smoke to confirm stable registration, instruction enable/disable behavior and native discovery/connection where probes are available; retain exact client versions and logs and do not infer Desktop behavior. Required native/client checks stay pending when prerequisites are unavailable. No authenticated agent prompts, production client homes, real journal, local installation, or publication are test fixtures.

## Risks / Trade-offs

- A supervising launcher adds a process and Windows pipe/job complexity -> require packaged EOF, cancellation, forced-parent-exit, full-duplex and client smoke evidence before completion.
- A self-contained managed launcher increases package size -> reuse .NET tooling, retain explicit archive/extraction bounds, and change limits only if measured payload size justifies it.
- Compatibility declarations can lie -> gate releases with genuine packaged-version concurrency and preserve a pinned baseline once one exists.
- Every settings writer must participate -> route repository creation and init through the same current-state coordinator; test two independent processes, not one in-memory object.
- Atomic file replacement does not solve arbitrary disk loss -> retain verified payloads/recovery metadata, fail closed on corrupt selection, document manual recovery.
- Retaining all payloads grows disk use -> accept manual maintenance initially; never add opportunistic cleanup inside this implementation.
- The old v0.1.1 updater cannot understand the new layout or notice semantics -> use the confirmed one-time manual migration rather than promising legacy helper fixes.

## Migration Plan

1. Implement and validate only in disposable installations, profiles and data directories. Run release build/tests, packaging, mixed-version Windows acceptance, and isolated client checks. Required missing validation keeps the change in progress.
2. Document the sole user's maintenance procedure: stop WorkLog clients, retain a recoverable copy of the old application/configuration and a consistent journal backup, extract the checksum-verified bootstrap ZIP into a clean installation directory, preserve the separate data directory, and run init dry-run then init for owned registrations with the same data/ticket/instruction context. Never overlay an old/new package blindly.
3. Restart normally and verify launcher path, actual payload version, shared repository identity, existing observations and instruction preferences. Actual migration, version bump, GitHub tagging and release publication remain separate explicit operational actions.
4. Subsequent compatible releases use explicit payload activation while conversations remain open. Users may reconnect normally when they want the new implementation; success means selection committed, not universal adoption.
5. Use explicit compatible selection rollback for application regressions. Recover corrupt selection offline using the retained verified copy if necessary. Restore journal backups only during separate maintenance when data compatibility actually requires it.
