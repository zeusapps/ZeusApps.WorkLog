> Saved by the orchestrator, verbatim from the reviewer's returned text.
> Reviewer: A3 code-analyzer; client Claude Code 2.1.312, subagent in auto mode; model claude-opus-5-5.
> Case: the client refused the reviewer's write with this message: "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: add-dev-builds / analysis-001

## Review target

- Reviewer / role: A3 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 (`claude --version`) / subagent, auto mode / `claude-opus-5-5` (role default)
- Date: 2026-10-28T10:15Z
- Change root / assignment: `openspec/changes/add-dev-builds` / A3 review analysis
- Task and requirement references: tasks 1.1–5.2 ([tasks.md](../tasks.md)); [spec delta](../specs/application-distribution/spec.md) "Development builds", "Non-blocking stable release discovery", "Explicit verified Windows update"; [design](../design.md) Decisions 1–5; developer deviations 1–9 in [findings.md](findings.md) "Scope decisions"
- Scope and relevant neighboring paths: the 36 identity paths below. Neighbors read for context: `src/WorkLog/Updates/ReleaseClient.cs`, `src/WorkLog/Updates/UpdateInstaller.cs` (grep only), `src/WorkLog/Repositories/GitRepository.cs`, `src/WorkLog/Configuration/LocalSettings.cs`, `src/WorkLog/Integration/*` and `src/WorkLog/Mcp/*` (grep for version use), `dashboard/scripts/stamp.mjs`, `docs/architecture.md`
- Reading limits and paths read outside scope: Bell Tower was read only, as allowed: `docs/development.md` "Development builds", and grep of `scripts/install-dev.ps1` and `scripts/package.ps1`. Developer run output under `artifacts/dev-it/` and `artifacts/dev-builds/` was read for directory listings and sizes only (ignored outputs). No other out-of-scope paths were read.
- Baseline commit: `8a7508e`
- Implementation identity (before): `scripts/review-identity.ps1 -Baseline 8a7508e -Paths <the 31 assigned path arguments>` → HEAD `8a7508e6cd34`, 13 untracked and 23 modified files, tracked diff SHA-256 `2d7cc746d6de84bba085e4525c93486f115f693a1a725ec5b72958d11a82bdb3`; both `SKILL.md` copies hash `1f8e0bc50d21…`. Summary: `head=8a7508e baseline=8a7508e files=36 diff=2d7cc746d6de untracked=13/5ef0fa129124` (matches the expected value)
- Before/after identity: after: `head=8a7508e baseline=8a7508e files=36 diff=2d7cc746d6de untracked=13/5ef0fa129124`. It is identical to the before value, and no target files changed.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Correctness: effective-version call sites | Grepped `IsNewerThan`, `ParseVersion(`, `.Current.Version` and `ApplicationInfo.Version` in `src/`, and read every changed comparison | `ReleaseInfo.IsValid` rejects the dev range, and `ReleaseClient.CheckAsync` returns only `IsValid()` releases, so a dev offer never reaches the cache or `UpdateCommand`. `UpdateDiscovery.GetHint` (both branches), `UpdateCommand` (running and selected), `PrepareDownloadedAsync` (running and selected) and `ActivateAsync` (superseded, downgrade, running) all use `EffectiveVersion`. `InstalledApplication`/`UpdateInstallation` compare the manifest with the bare `ApplicationInfo.Version`, and the bare version is preserved by `Split('-')`. `UpdateInstaller`/`UpdateHelper` are legacy pre-launcher paths that never apply to versioned dev payloads | No missed call site found. One case improved: a concurrent switch to dev during a stable update used to be "superseded" (silently not installed) and now raises "Selection changed". The API shape carries a latent hazard (ANALYSIS-006) |
| Correctness: Retire safety | Read `DevInstaller.RetireAsync`, `PayloadInstaller.Commit` and `RollbackAsync`, and the Retire tests | Checks and rename/delete run under the publisher lock. Selected, previous and `current.previous.json` Current are refused. In-use detection is fail-closed (`Win32Exception` refuses). Renaming first blocks a mapped image. Hashes are verified before the rename | The lock, in-use check and rename-first are sound. Retire is not reachable after Rollback or a single stable update, which the docs claim it is (ANALYSIS-001). A `.retired-*` residue has no tool path (ANALYSIS-004) |
| Safety: disposable switches | Read `DevTargets.Resolve` and `Program.cs`, and traced alias cases | `--skip-probe` with the default root is refused. A custom root or data directory must be under `<repo>/artifacts`. 8.3 short names and other spellings fail `Same` and then fail `Within`, so they fail closed. Junctions are refused by `EnsureNoReparsePoints` | The guard is robust. The surface is larger than needed (ANALYSIS-005) |
| Robustness: stale rule | Read `Staleness.cs`, compared it with `package.ps1` dirty inputs and `dashboard/scripts/stamp.mjs`, and ran a Windows mtime probe in the scratchpad (outside the repo, removed afterwards) | Probe: `Copy-Item` and `Rename-Item` kept a 3-day-old `LastWriteTimeUtc`. A deleted file leaves nothing to compare. The parent directory mtime did update | Measured gap (ANALYSIS-002) |
| Duplication: scripts | Diffed `package.ps1` and `package-core.ps1`, read `install-dev.ps1` | The version regex appears 3× in PowerShell. `DevPatchStart` is mirrored in PowerShell and C# with a cross-reference comment. The default installation path is defined in `package.ps1` and `DevTargets` | Acceptable across languages. Path duplication is folded into ANALYSIS-007 |
| Duplication: DevInstaller vs product | Read `DevInstaller.InspectPackage` and `Preflight`, and compared with `PayloadInstaller` and `ReleasePackage` | Preflight reads `release.json` straight from the zip. It does not extract, which keeps it read-only. Prepare, Activate and Rollback are the product's own code. The contract check is a read-only preview; the product check stays authoritative | Justified duplication, no finding |
| Duplication: manifest reading | Compared `ApplicationInfo.ComputeDisplay` with `InstallationLayout.TryReadManifest` | Same read, version match and exception guard, but the filters differ (`ArgumentException` appears only in `TryReadManifest`) | ANALYSIS-007 |
| Architecture | Judged against `docs/architecture.md` | Lines 35 and 37 state "Previous/all published payloads remain…" and that hints compare with running and selected versions. The document has no mention of the dev tool, effective versions or Retire | ANALYSIS-008 |
| Unbounded state | Listed the developer's disposable run under `artifacts/dev-it/data/updates/staging` (`du -sh`) | 3 retained stage folders of 253 MB each, one per Switch (zip plus extracted package). The dev package is 82 MB zipped and 175 MB unpacked | Measured on-disk sizes (ANALYSIS-003) |
| Skill text and parity | Read `SKILL.md` and `docs/releases.md`, and compared hashes | Both copies are byte-identical (same SHA-256). Rule 3 conflicts with step 5, and rule 4 conflicts with the linked recovery steps | ANALYSIS-004 |
| Integration parity | `git diff 8a7508e --stat` over the target; grep of `src/WorkLog/Integration` and `src/WorkLog/Mcp` | No changes under `Integration/`, `Clients/` or `Mcp/`. MCP `ServerInfo.Version` and `AgentInstructions` still use the bare `ApplicationInfo.Version`. `UpdateHint` fields are unchanged | `worklog init`, managed instructions and MCP contracts are unchanged for both Claude Code and Codex. Note: `init` run from a dev payload would stamp `v0.3.9001` into managed instructions. This is pre-existing behavior keyed on version, and the skill forbids `init` |
| Formatting | `dotnet format ZeusApps.WorkLog.slnx --no-restore --verify-no-changes --include <8 new .cs files>` | exit 0 | Passed |
| Performance | Source observation | No new timers. `GetHint` runs per `log_work` and `query_work` call and, for dev payloads only, adds ≤2 manifest reads (≤16 KB each). `DisplayVersion` is cached per process. Retire enumerates processes once | Hypothesis: negligible (sub-millisecond per hint). Not measured |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | `UpdateDiscovery.RunAsync` (unchanged 15 min loop); `GetHint` per MCP tool call; dashboard `/api/info` once per page load | source observation | No new interval work. For a selected or running dev payload, `GetHint` adds up to 2 `release.json` reads (`TryReadManifest`) per tool call. Stable payloads add 0. Hypothesis: negligible next to the existing `ReadSelection` read |
| Cache lifetime versus interval and refresh duration | `ApplicationInfo.display` static | source observation | Computed once per process. The running payload is immutable, so a lifetime cache is correct. Selected-payload labels are reread per command, which is correct after a switch |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `<data>/updates/staging/<guid>` per Switch; `artifacts/dev-builds/operations/*.json`; dev packages; `.retired-*` residue; published dev payloads | measured (on-disk sizes of the developer's disposable run) / source observation | Each Switch leaves about 253 MB in the data directory's staging folder, and nothing removes it (ANALYSIS-003). Operation records are small, grow without limit, and live in the ignored `artifacts/`. Dev packages cost about 82 MB zipped plus 175 MB unpacked each in the ignored `artifacts/`. Published dev payloads stay until someone retires them by hand, which is the intended design. `.retired-*` folders stay only after a failed delete |
| Repeated full reads where a change check would do | `Staleness.NewestSource` walks `src/` and `dashboard/src` per Preflight/Switch; Retire hashes the whole payload | source observation | Once per operator command. Acceptable. Retire's full-hash verification is deliberate (identity before delete) |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | `git` via `GitRepository.Run` (Staleness); `dotnet build`/`dotnet run` in `install-dev.ps1`; `git` in `package.ps1` | source observation | `git` and `dotnet` are resolved from PATH only. `GitRepository.Run` waits without a timeout (pre-existing helper), once per Preflight or Switch. A missing `git` raises `Win32Exception`, which `Program.cs`'s catch filter does not cover, so the tool exits with a stack trace rather than a message (minor; not raised separately) |

## Findings

### ANALYSIS-001: Retire cannot remove a dev payload right after Rollback or one stable update, but the docs and design say it can

- Severity: warning
- Related task / requirement: 3.2, 4.2; "Development builds" (Retire); design Migration Plan
- Locations: `docs/releases.md:46` ("After a stable update or a Rollback away from it, `Retire -Version 0.3.9001` removes it."); `openspec/changes/add-dev-builds/design.md:100` ("the maintainer can roll back from it and retire it"); `tools/WorkLog.DevInstall/DevInstaller.cs:137-143`; `src/WorkLog/Updates/PayloadInstaller.cs` `Commit` (`new PayloadSelection(target, current.Current, …)` and `current.previous.json` ← `current`)
- Observation: Every selection change (`Commit`) records the outgoing payload as `Previous` and writes the outgoing selection, whose `Current` is that payload, to `current.previous.json`. After a Rollback away from dev `0.3.9001`, or after a stable update over it, `0.3.9001` is both `previous` and `current.previous.json` Current. Retire refuses both (lines 139-143). Retire becomes possible only after a second selection change. The developer's own disposable run shows the same thing: "Retire refused … the previous `0.3.9003`" (`docs/mcp-validation.md`). The documented cleanup of the manual `0.3.9001` payload, and the skill's implied Switch → Rollback → Retire flow, therefore fail with a refusal.
- Proposed improvement and benefit: correct `docs/releases.md:46` and the skill's step 6 to say that a dev payload can be retired after it stops being both `previous` and the `current.previous.json` selection, which usually takes two later selection changes. Give the concrete sequence for the manual `0.3.9001`. Record in the ledger that design.md:100 is superseded. If the orchestrator wants a one-step cleanup, that is a design decision. One option is to let Retire remove a dev-range `previous` after explicit authorization by clearing `Previous` under the lock. That changes selection state and the `worklog update --rollback` target, so it needs spec wording.
- Behavior-preservation risks: a docs-only fix keeps the current, safe refusal. The alternative weakens the rollback target and the recovery pointer.
- Evidence type: source observation, plus the developer's recorded disposable run
- Verification / measurement plan: a disposable test: Switch, then `RollbackAsync`, then `RetireAsync(dev)` is refused. A further Switch or Activate to another version makes `RetireAsync(dev)` succeed.
- Relationship: none

### ANALYSIS-002: Stale rule misses deleted, renamed, and timestamp-preserving copied source files

- Severity: warning
- Related task / requirement: 3.1; "Development builds" ("SHALL refuse … stale packages (… sources newer than the built package)"); deviation 1
- Locations: `tools/WorkLog.DevInstall/Staleness.cs:13-54`; `scripts/package.ps1` (dirty-flag `$inputs`, near line 53)
- Observation: freshness means "commit equals HEAD and no file's `LastWriteTimeUtc` is later than `builtAt` + 1 s". The marker's `dirty` flag is not consulted. The Windows probe in the scratchpad measured that `Copy-Item` and `Rename-Item` keep the old file mtime, and a deleted file leaves nothing to compare. Meanwhile the parent directory's mtime did update. Three changes after packaging therefore pass Preflight and Switch at the same HEAD:
  - deleting a `.cs` file;
  - moving or renaming a dashboard source;
  - copying in a file with an old timestamp.

  In each case the installed build differs from the checkout. A second, broader definition of "build input" in `package.ps1` (the dirty flag, which includes `tests`, `tools`, `scripts`, `docs`) disagrees with the stale set. The dashboard already has a content-hash freshness check (`dashboard/scripts/stamp.mjs`).
- Proposed improvement and benefit: two bounded options.
  1. Minimal: in `Walk`, also consider directory `LastWriteTimeUtc`, which catches deletes, renames and adds. When `marker.Dirty` is false, also require `git status --porcelain --untracked-files=normal -- <stale inputs>` to be empty.
  2. Stronger: record a content digest of the stale input set in the marker at packaging time, as `stamp.mjs` already does, and compare it in Preflight. This also removes the false positives from `git checkout`/`stash` touching files.

  Either option closes the measured false negatives. Keep one input list, shared by the PowerShell and C# code or documented in one place.
- Behavior-preservation risks: directory mtimes also change when editors write temp files, so false positives can rise (and the result stays conservative). A digest is an additive marker field; older readers ignore it.
- Evidence type: measured result (mtime probe) plus source observation
- Verification / measurement plan: add Staleness tests in a disposable git repository under `artifacts/`: package with a fixed `builtAt`, then (a) delete a `src` file, (b) rename one, (c) `File.Copy` one with a preserved old mtime. Each must return a stale message.
- Relationship: none

### ANALYSIS-003: Each Switch leaves about 253 MB of staging in the real data directory, and nothing removes it

- Severity: warning
- Related task / requirement: 3.1; "Development builds" (repeatable use); steady-state unbounded state
- Locations: `tools/WorkLog.DevInstall/DevInstaller.cs:89-107`; `src/WorkLog/Updates/PayloadInstaller.cs` `PrepareDownloadedAsync` (extracts to `<stage>/package`) and `ActivateAsync` (does not delete the stage)
- Observation: Switch copies the 82 MB zip into `<data>/updates/staging/<guid>`, and Prepare extracts the package beside it. Neither the tool nor the product removes the stage after activation or after a failed Prepare. The developer's disposable run retained three stages of 253 MB each (measured with `du -sh artifacts/dev-it/data/updates/staging/*`). Product updates retain stages too (pre-existing), but they are rare. Dev switches are an iteration loop and land in `%LOCALAPPDATA%\ZeusApps.WorkLog`. For example, 20 iterations cost about 5 GB.
- Proposed improvement and benefit: Switch owns its stage GUID. Once `ActivateAsync` returns, delete that stage directory. When Prepare or Activate throws, delete it as well, after the outcome record is final. This does not change product code. Deviation 5 already treats a stage as unreferenced once its directory is gone, and `update --status` matches only the outcome's `OperationId`, not the directory.
- Behavior-preservation risks: deletion must happen only after the outcome record is written, and only for the stage this operation created (exact GUID path, `EnsureNoReparsePoints`). Retire's staged check stays correct.
- Evidence type: measured result (on-disk sizes of existing disposable data)
- Verification / measurement plan: in the disposable `DevInstallTests`, assert that `<data>/updates/staging` holds no directory for the operation after `SwitchAsync`, for both success and an injected probe failure.
- Relationship: none

### ANALYSIS-004: Skill rules contradict its own fallback steps and the recovery procedure it links to

- Severity: warning
- Related task / requirement: 4.1, 4.2; "Development builds" (explicit authorization per operation); parity (both copies are identical, so the issue applies to Claude Code and Codex alike)
- Locations: `.claude/skills/install-dev-build/SKILL.md:16` (rule 3: never run `worklog update` against the real installation; only `--check`/`--status` are allowed), `:17` (rule 4: no manual edits under `versions\` or `current.json`), `:64` (step 5: "use `worklog update --rollback` or the recovery steps in `docs/releases.md`"); `docs/releases.md:47` ("restore `current.json` from `current.previous.json`", "delete it by hand or rename it back" for `.retired-*`); `tools/WorkLog.DevInstall/DevInstaller.cs:169`. The same lines apply in `.agents/skills/install-dev-build/SKILL.md`.
- Observation: when Rollback refuses, the skill routes the agent to an operation that rule 3 forbids (`worklog update --rollback`) and to hand edits that rule 4 forbids. Nothing says these fallbacks need their own explicit user authorization. A failed Retire delete leaves a `.retired-<version>-<id>` folder that no tool action can remove. The error message tells the operator to delete it by hand, against rule 4. An agent following the skill gets contradictory instructions on the maintainer's real installation, at the moment when caution matters most.
- Proposed improvement and benefit: in the skill, say that when Rollback or Retire refuses or fails, the agent stops, reports, and does not run `worklog update --rollback` or any manual recovery unless the user authorizes that specific step. Give rule 3 an explicit exception for `--rollback` with per-operation authorization. Optionally let Retire also remove leftover `.retired-<version>-*` folders for dev versions under the lock, which removes the hand-delete step.
- Behavior-preservation risks: wording only, unless the optional Retire sweep is taken. The sweep must accept only `.retired-` folders with a dev-range version and no reparse points.
- Evidence type: source observation
- Verification / measurement plan: extend `DevBuildSkillTests` to assert that the authorization text covers `worklog update --rollback` and manual recovery. Read the skill and the linked docs for consistency.
- Relationship: none

### ANALYSIS-005: `InternalsVisibleTo` and `--skip-probe` exist only to reach one internal constructor

- Severity: suggestion
- Related task / requirement: 3.1; deviation 7; design Decision 4 (disposable switches)
- Locations: `src/WorkLog/WorkLog.csproj:15`; `src/WorkLog/Updates/PayloadInstaller.cs:20` (`internal PayloadInstaller(Func<…> health)`); `tools/WorkLog.DevInstall/DevInstaller.cs:22`; `tools/WorkLog.DevInstall/Program.cs:12`; `tools/WorkLog.DevInstall/DevTargets.cs:7,14,23-27`; `scripts/install-dev.ps1:28,40`; `tests/WorkLog.Tests/Updates/DevInstallTests.cs:220`
- Observation: everything else DevInstall uses is already public: `AcquirePublisherAsync`, `GitRepository.Run`, `Json`, `AtomicFile`, `ReleasePackage.VerifyChecksum`, `UpdateInstaller.ReadOutcome`. The grant exposes all of WorkLog's internals (for example `ObserveStage`) to a tool that runs against the real installation. Because the tool takes a `--skip-probe` CLI switch, the change also needs a default-installation guard, script documentation and a test. `IsDefaultInstallation` is used only by a test. The script header and skill say `-Workspace` is "refused for the default installation". It is not; it only has to be under `artifacts/`. That is harmless, but the docs overstate it.
- Proposed improvement and benefit: give `DevInstaller` an optional `PayloadInstaller` constructor parameter that defaults to `new()`. The test fixture (which already has `InternalsVisibleTo` to WorkLog) passes `new PayloadInstaller(noop)`. Then remove `--skip-probe` and `-SkipProbe`, `DevTargets.SkipProbe`, the DevInstall `InternalsVisibleTo`, and `IsDefaultInstallation`. That leaves fewer switches to guard and a smaller trust surface. Script-level runs on disposable installations would then run the real health probe, which only touches disposable data and is more faithful.
- Behavior-preservation risks: disposable script runs get slower because of the probe. The tests are unchanged apart from the constructor.
- Evidence type: source observation
- Verification / measurement plan: build and run `DevInstallTests` after the refactor. Grep confirms no `internal` WorkLog member is used from `tools/WorkLog.DevInstall`.
- Relationship: none

### ANALYSIS-006: The two `EffectiveVersion` overloads take the same string type in opposite order, and the comparison is repeated at 9 sites

- Severity: suggestion
- Related task / requirement: 1.1, 1.2; Decision 1
- Locations: `src/WorkLog/Installation/InstallationLayout.cs:43` (`EffectiveVersion(string version, PayloadManifest? manifest = null)`) and `:54` (`EffectiveVersion(string? root, string version)`); call sites `src/WorkLog/Updates/PayloadInstaller.cs:27-28,61-62,89,96-97`, `UpdateDiscovery.cs:31,43`, `src/WorkLog/Cli/UpdateCommand.cs:71-72`
- Observation: a transposed call such as `EffectiveVersion(selected, root)` compiles against the `(root, version)` overload. For a dev `selected`, that call returns the root path, `ParseVersion` yields null, and `IsNewerThan` silently returns false, which suppresses offers. Each site also repeats `ParseVersion(EffectiveVersion(root, x))` or `IsNewerThan(EffectiveVersion(root, x))`.
- Proposed improvement and benefit: rename the root-based overload to `EffectiveInstalledVersion(string? root, string version)`, or have it return `Version` (for example `ComparableVersion(root, version)`). Optionally add `ReleaseInfo.IsNewerThanInstalled(root, version)`. This makes misuse a compile error and shortens the 9 sites.
- Behavior-preservation risks: none; the semantics do not change.
- Evidence type: source observation
- Verification / measurement plan: existing `DevBuildVersionTests` and update tests pass unchanged.
- Relationship: none

### ANALYSIS-007: Manifest-read and default-path logic is duplicated, with exception filters that differ

- Severity: suggestion
- Related task / requirement: 1.3, 3.1
- Locations: `src/WorkLog/Cli/ApplicationInfo.cs:19-34` vs `src/WorkLog/Installation/InstallationLayout.cs:61-73` (`TryReadManifest`); `tools/WorkLog.DevInstall/DevTargets.cs:9-12` vs `src/WorkLog/Configuration/LocalSettings.cs:19-21`; `scripts/package.ps1:10` vs `DevTargets.DefaultInstallationRoot`
- Observation: `ComputeDisplay` copies `TryReadManifest`'s steps (read `release.json`, match the version, swallow I/O and JSON errors) but leaves out `ArgumentException` from its catch filter. The `WORKLOG_DATA_DIR` → `%LOCALAPPDATA%\ZeusApps.WorkLog` resolution exists in both `LocalSettings` and `DevTargets`. The installation default exists in PowerShell and C#. If the product's data-directory rule changes later, the dev tool's "is this the default data directory" guard could drift from it.
- Proposed improvement and benefit: add `InstallationLayout.TryReadManifestFile(string path, string version)` and use it from both places. Expose a static `LocalSettings.DefaultDataDirectory` and reuse it in `DevTargets`. This gives one rule and consistent failure handling.
- Behavior-preservation risks: none expected. `ComputeDisplay` would additionally tolerate `ArgumentException`.
- Evidence type: source observation
- Verification / measurement plan: existing label tests (`LabelOfAnInstalledPayloadReadsItsManifest`, `StableBuildDisplaysItsPlainVersion`) and `DisposableSwitchesAreRefused…` pass unchanged.
- Relationship: none

### ANALYSIS-008: `docs/architecture.md` not updated for effective versions, the dev tool, or the first path that deletes published payloads

- Severity: suggestion
- Related task / requirement: 4.2; architecture reference
- Locations: `docs/architecture.md:35` (hints compare with running and selected versions), `:37` ("Previous/all published payloads remain, so launches that read an older selection stay valid without a cleanup race"), `:51-53` (lists tools and probes, but not `tools/WorkLog.DevInstall`)
- Observation: Retire is the first code path that deletes a published payload. That narrows the line-37 invariant, which the guards rely on: the payload must not be selected, previous, or the recovery pointer, and must not be in use, and the folder is renamed first. Hints and activation now compare against effective versions. `VersionDisplay` is a new shared component under `Installation`.
- Proposed improvement and benefit: add one sentence each in the `Updates` and `Installation` rows or paragraphs, covering effective versions, `VersionDisplay`, and the Retire exception with its guards, and list `tools/WorkLog.DevInstall` with the test tools. This keeps the reference that reviewers judge drift against accurate.
- Behavior-preservation risks: none (docs).
- Evidence type: source observation
- Verification / measurement plan: read-through.
- Relationship: none

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | n/a | n/a | not applicable (first analysis round) |

## Assessment and limitations

I covered every in-scope path. The effective-version rule is applied consistently. ReleaseClient filters dev-range offers at the source, so no path lets a dev offer through or lets a dev payload block a stable update. `worklog init`, managed instructions, per-client adapters and MCP contracts are unchanged for both Claude Code and Codex, and the two skill copies are byte-identical. Retire's locking, in-use detection and rename-first order are sound, and the disposable-switch guard fails closed for alias paths. The tool is far simpler than Bell Tower's 1199-line script, because WorkLog has no tray. That simplification is justified.

Recommendations in priority order:
1. ANALYSIS-001 and ANALYSIS-004 (docs and skill accuracy on the real installation).
2. ANALYSIS-003 (unbounded growth of the real data directory).
3. ANALYSIS-002 (stale false negatives).
4. Suggestions ANALYSIS-005 through ANALYSIS-008.

Unmeasured items, labeled as hypotheses: the per-call cost of `GetHint` for dev payloads, and the time `install-dev.ps1` spends rebuilding the tool on each call. Not performed: build, test and packaging runs (QA owns functional checks), and anything against the default installation, the real data directory, or port 47831.

Unexpected writes: none to the repository. The mtime probe used a scratchpad folder outside the repository and deleted it. Developer deviations 1, 5 and 7 were checked: deviation 1 → ANALYSIS-002, deviation 5 is consistent with ANALYSIS-003, deviation 7 → ANALYSIS-005. Deviations 3, 4 and 6 have no analysis concern. Deviations 8 and 9 belong to QA and hygiene triage.

Decision needed (orchestrator): for ANALYSIS-001, decide between a docs-only fix and a spec change that allows retiring a dev-range `previous`.
