# Tasks

## 1. Installation and package contracts

- [x] 1.1 Add bounded package-format-2, launcher-contract-1, shared-data-contract-1, schema-2, selection-generation and payload-hash models with validation; verify missing/unsupported fields, malformed versions, hash mismatch and out-of-root/reparse targets are rejected before selection or data writes.
- [x] 1.2 Add an installation resolver separating stable launcher/root, running payload and current selection; verify installed/retained-payload resolution, unsupported/legacy rejection, source invocation compatibility, stable status identity across versions and independence from unvalidated environment overrides.
- [x] 1.3 Adapt `ReleasePackage` for the exact nested bootstrap/payload allowlist and immutable manifest validation; verify checksum, entry-count/size limits, case collisions, traversal, links, runtime/version mismatch and partial existing directories with focused package tests.

## 2. Configuration and shared-data foundation

- [x] 2.1 Preserve raw JSON with typed configuration projections and known-path mutations in `LocalSettings`; verify root/nested unknown fields, unknown repository-context fields, known updates, copy/adopt and legacy settings survive round trips on .NET 10 without journal-schema changes. Review regression: retain legacy `bugfix` normalization after save, copies, adoption and repository resolution.
- [x] 2.2 Add bounded settings-writer coordination with reload/merge/conflict detection and atomic saves; verify independent edits survive, stale conflicting edits fail, equal retries are accepted and two independent processes cannot overwrite each other's latest settings.
- [x] 2.3 Route `RepositoryRegistry` first-use creation through the coordinator and adopt the committed context; verify simultaneous fresh-repository resolution yields one identity, worktrees share it, separate clones remain separate and legacy ticket-context conflicts retain their existing behavior.
- [x] 2.4 Integrate coordinated settings planning/application into `RepositoryInitializer` and `InitializationPlan`; verify dry-run/preflight failures persist nothing, init preserves opt-outs/ticket settings/unknown fields, before-content conflicts remain protected, partial failures remain actionable and settings/journal lock ordering does not deadlock.

## 3. Stable launcher

- [x] 3.1 Add the dependency-light self-contained Windows launcher project and solution/build inputs; verify it validates a selection and starts only its pinned complete payload, forwards argument boundaries/cwd/environment/exit code, and reports invalid selection on stderr with no extra MCP stdout.
- [x] 3.2 Implement inherited standard streams and launcher-owned child lifetime supervision, using a narrow Windows adapter where required; verify bidirectional pipe delivery under backpressure, stderr, EOF, cancellation, forced launcher termination, job-setup failure and already-jobbed execution without orphaning the owned child or affecting unrelated processes.

## 4. Activation, status and rollback

- [x] 4.1 Replace ordinary in-place replacement with verified download/extraction and an isolated payload initialize/tools-list health probe; verify probes use disposable data, preserve official-feed/checksum/version safeguards, and refuse incompatible launcher/data contracts or schema before selection or private-data changes.
- [x] 4.2 Implement the bounded installation publisher lock, same-volume temporary-directory publication and atomic selection commit with prior selection/generation/operation ID; verify complete old/new launches, immutable payload reuse/conflict rules, cancellation, unwritable paths, competing publishers and stale generation refusal without global server-idle checks.
- [x] 4.3 Remove the lifetime installation lease and copied-helper/parent-exit path from ordinary versioned updates while retaining actionable unsupported-layout handling; verify activation succeeds with an old server and installed status readers active and no process is stopped to permit an update.
- [x] 4.4 Rework CLI/persisted outcomes around staged/activated/failed and running/selected versions using stable installation identity; verify local offline status, same/custom data-directory behavior and post-commit/pre-outcome interruption use selection as truth without falsely claiming universal client adoption.
- [x] 4.5 Add mutually exclusive explicit `update --rollback` with compatible previous-payload verification and the same generation commit path; verify rollback preserves live connections and data, missing/corrupt/incompatible targets fail unchanged, and a stale pending update cannot overwrite the intentional rollback.

## 5. Integration and notices

- [x] 5.1 Route `ServerRegistration`, integration ownership metadata and production update commands through the validated stable launcher; verify init from both selected and retained payloads, quoted paths/custom data arguments, owned legacy-registration refresh, unrelated ownership conflicts and source/development overrides.
- [x] 5.2 Make `UpdateDiscovery` reread selection for each hint, compare offers with running and selected versions and add bounded selectedVersion context; verify activated-offer suppression in an old server, stale-offer refusal, disabled/offline/corrupt-selection behavior, shared cache throttling, schema/fallback agreement and the 64 KiB report budget.
- [x] 5.3 Refresh managed instructions and CLI help for activation versus adoption and maintenance cases; verify both clients retain once-per-version notices, explicit installation authorization, original logging policy, instruction opt-out/re-enable and no implied session shutdown for compatible activation.

## 6. Packaging and disposable acceptance

- [x] 6.1 Update `scripts/package.ps1` to package the bootstrap launcher, initial selection and one versioned payload with verified manifests/hashes/docs; verify a clean extraction runs without installed .NET, production tag/version checks remain enforced and package size/extraction bounds are measured rather than silently relaxed.
- [x] 6.2 Add isolated fixture builds for two genuinely different compiled payload versions A/B with distinct hashes and asserted executable/MCP versions; verify these do not edit `Directory.Build.props`, masquerade one binary with fake manifest versions, or treat legacy v0.1.1 as a compatible baseline, and document pinning the first supported published baseline for later release checks.
- [x] 6.3 Replace updater smoke's same-binary replacement fixture with a real A stream that survives B activation and a second server launched through the same launcher; verify original payload PID/path/version/connection, before/after logging/querying, both servers' shared data, unchanged launcher/client registrations and retained old payloads with captured Windows/package versions and logs.
- [x] 6.4 Add cross-version concurrent logging/retry/first-start and report-continuation acceptance plus configuration writer fixtures; verify original retry IDs/timestamps, conflicting reuse, snapshot preservation under backdated writes, nested unknown fields, independent/conflicting edits, first-use repository convergence and a second custom data directory.
- [x] 6.5 Add coordinated overlapping launches/publishers/installed status readers, rollback, cached old-server offers and interrupted activation cases; verify overlap is observed, selection generations cannot regress through stale operations, recovery matches actual commit state and failures leave prior payloads/data/unowned sentinels intact.
- [x] 6.6 Add packaged invalid-pointer/package/hash/manifest/version/reparse/partial-directory/incompatible-contract/read-only/offline cases and repeated-activation retention checks; verify bounded actionable failures, protocol-only stdout, no partial execution or automatic payload deletion and unchanged real profiles/installations.
- [x] 6.7 Extend isolated client smoke to exercise stable-launcher registration and connection with both clients and instruction modes; verify native discovery/connection and lifecycle where supported, retain exact client versions/logs, avoid authenticated prompts and keep required unavailable checks pending.
- [x] 6.8 Update Windows validation/release inputs to build/package the launcher and gate release on the mixed-version acceptance; verify full release build/tests, packaged acceptance and isolated client smoke pass locally and workflow commands preserve matching-tag/publication gates without publishing as a test.

## 7. Documentation and planning consistency

- [x] 7.1 Update README, architecture, installation, release and MCP-validation documentation with the layout/contracts, running/selected state, compatible activation/rollback, manual recovery, retained disk use and one-time migration; verify commands match fixture outputs and actual client claims cite versioned evidence, with actual user migration/publication left as explicit operational steps.
- [x] 7.2 Preserve the recorded F9 supersession boundary and F6-F8 independence while finalizing artifacts; verify no F9 task is marked implemented or its old delta synced merely because this design replaces it, then run strict OpenSpec validation and record actual required test evidence before declaring this feature complete.
