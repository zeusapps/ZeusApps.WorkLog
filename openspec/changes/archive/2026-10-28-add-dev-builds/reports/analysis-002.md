> Saved by the orchestrator, verbatim from the reviewer's returned text.
> Reviewer: A6 code-analyzer; client Claude Code 2.1.312, subagent in auto mode; model claude-opus-5-5.
> Case: the client refused the reviewer's write with this message: "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: add-dev-builds / analysis-002

## Review target

- Reviewer / role: A6 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 (`claude --version`) / subagent, auto mode / `claude-opus-5-5` (role default)
- Date: 2026-10-28T10:47Z
- Change root / assignment: `openspec/changes/add-dev-builds` / A6 recheck analysis (ANALYSIS-001 – ANALYSIS-004 plus regression; new IDs ANALYSIS-041 – ANALYSIS-060)
- Task and requirement references:
  - Tasks 3.1, 3.2, 4.1, 4.2, and 1.3/5.1 (Smoke) ([tasks.md](../tasks.md)).
  - [Spec delta](../specs/application-distribution/spec.md) "Development builds".
  - Triage and scope decisions in [findings.md](findings.md).
- Scope and relevant neighboring paths:
  - Scope: the 33 identity path arguments (40 files).
  - Focus:
    - `tools/WorkLog.DevInstall/Staleness.cs` and `tools/WorkLog.DevInstall/DevInstaller.cs`
    - `tests/WorkLog.Tests/Updates/StalenessTests.cs`, `tests/WorkLog.Tests/Updates/DevInstallTests.cs`, `tests/WorkLog.Tests/Integration/DevBuildSkillTests.cs`
    - both `SKILL.md` copies
    - `docs/releases.md`, `docs/mcp-validation.md`
    - `scripts/install-dev.ps1`, `scripts/package.ps1`, `scripts/package-core.ps1`
    - `tools/WorkLog.Smoke/*Checks.cs`
  - Neighbors read for context:
    - `src/WorkLog/Repositories/GitRepository.cs` (`Run`)
    - `src/WorkLog/Updates/PayloadInstaller.cs` (prepare and activate; in scope)
    - `src/WorkLog/Installation/InstallationLayout.cs` (`EnsureNoReparsePoints`; in scope)
    - `tools/WorkLog.Smoke/Program.cs`
    - `dashboard/scripts/stamp.mjs`
    - the dashboard targets in `src/WorkLog/WorkLog.csproj`
- Reading limits and paths read outside scope:
  - `tools/WorkLog.Smoke/Program.cs` is inside the scoped `tools/WorkLog.Smoke`.
  - Read outside scope: `src/WorkLog/Repositories/GitRepository.cs` (lines 23-52), `dashboard/scripts/stamp.mjs`, and the prior reports [analysis-001](analysis-001.md), [qa-001](qa-001.md) (QA-001 section) and [findings.md](findings.md).
- Baseline commit: `8a7508e`
- Implementation identity: `./scripts/review-identity.ps1 -Baseline 8a7508e -Paths <the 33 assigned path arguments>`, run before the analysis:
  - HEAD `8a7508e6cd348d9ffd6ecda77ddfd9080346baab`.
  - Manifest: 14 untracked and 26 modified files.
  - Tracked diff SHA-256 `8b494277fff6aa2d19a5b7223676658c777270623a452b4c503d2baa993c94f8`.
  - File hashes: both `SKILL.md` copies `0e57f334b294…`, `Staleness.cs` `789df2fa097e…`, `DevInstaller.cs` `cd9eb09f3497…`.
  - Summary: `head=8a7508e baseline=8a7508e files=40 diff=8b494277fff6 untracked=14/fb1d5fbfff28` (matches the expected value).
- Before/after identity: after the analysis the summary was `head=8a7508e baseline=8a7508e files=40 diff=8b494277fff6 untracked=14/fb1d5fbfff28`. It is identical, and no target file changed.
- Prior reports / rechecked findings: [analysis-001](analysis-001.md): ANALYSIS-001, ANALYSIS-002, ANALYSIS-003 and ANALYSIS-004. ANALYSIS-005 – ANALYSIS-008 are deferred, and the fixes did not make them worse (see the assessment).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-001: Retire after Rollback (docs only) | Read the `DevInstaller.RetireAsync` messages (`DevInstaller.cs:163-167`), `docs/releases.md:43,46-47`, skill step 6, `install-dev.ps1:11` and the new test `RetireRefusesTheRolledBackPayloadUntilAFurtherSelectionChange`. Traced `Commit` through Switch → Rollback → Switch, and through the manual `0.3.9001` sequence | The refusals are unchanged. The messages, docs, skill and script header all give the same rule: a payload must be neither `previous` nor the `current.previous.json` selection, and one further selection change to another payload frees it. The traced selections agree, including "a Rollback in between does not help" | Verified |
| ANALYSIS-002: stale rule | Read `Staleness.cs` and `StalenessTests.cs`. Compared `InputPaths` with `package.ps1:55-56`. Ran probes in a throwaway Git repository under `artifacts/analysis-002-devbuilds/` (removed afterwards) | Five probes: (1) `git status -- nosuch.md src` exits 0, so a pathspec that matches nothing is harmless. (2) A deleted tracked `README.md` is reported only when `README.md` is in the pathspec; with the list filtered to existing paths, nothing is reported. (3) A deleted `dashboard/tsconfig.app.json` is missing from a disk listing of `tsconfig*.json`, but the Git pathspec `dashboard/tsconfig*.json` reports it. (4) `Copy-Item -Force` over an existing file kept the source's old time and left the parent directory's time unchanged, while `git status` reported ` M`. (5) Creating a `.spec.ts` file or an `obj` folder inside a directory changes that directory's time | The reported scenarios are fixed. Remaining false negative: ANALYSIS-041. New kind of false positive: ANALYSIS-042 |
| ANALYSIS-002: input list | Compared `Staleness.cs` with `docs/releases.md:40` and `package.ps1:53-56` | There is one documented list: the `Staleness` summary and the docs bullet agree. Inside `Staleness.cs` the directory list appears twice (`SourceDirectories` on line 19 and a literal on line 58), and so does the `tsconfig*.json` listing (lines 26 and 60) | Acceptable within one file; no finding |
| ANALYSIS-002: false positives from the marker | Compared the `dirty` input set in `package.ps1` (`src tests tools scripts dashboard docs` plus root files, untracked files included) with `InputPaths` | The `git status` check covers a subset of the inputs that decide `dirty`. A clean marker therefore cannot meet a `git status` hit that already existed when the package was built | No permanent false positive; no regression |
| ANALYSIS-002: false positives from the build itself | Read `package.ps1:57` (`builtAt` is recorded before `New-WorkLogPackage` runs), `package-core.ps1` (publishes to `-o`), the `WorkLog.csproj` dashboard targets, and `install-dev.ps1:33` (builds the tool before each action) | During packaging, writes go to `bin` and `obj` (excluded), `dashboard/dist`, `.angular` and `node_modules`; `dashboard/` itself is not walked. A walked directory changes only when `obj` or `bin` is created for the first time, which happens on a fresh clone. Skill step 1 (`dotnet build -c Release`) creates them before `builtAt` | The fresh-clone case is documented. Wording issues in `docs/releases.md:40`, no finding: it names `node_modules` as a cause although `node_modules` is not walked, and the inserted sentence splits the Preflight action list ("build it again, checks the contracts…") |
| ANALYSIS-003: stage removal | Read `DevInstaller.SwitchAsync` and `DeleteStage` (`DevInstaller.cs:89-132`), `PayloadInstaller.PrepareDownloadedAsync` and `ActivateAsync` (`:52-145`), `EnsureNoReparsePoints` (`InstallationLayout.cs:180-185`), and the tests `SwitchRemovesItsStageDirectoryOnSuccess` and `…WhenActivationFails` | Path and timing: the stage is a fresh GUID folder under `<data>/updates/staging`. The reparse-point check covers the whole ancestor chain and runs before the `try`, so a refused path never reaches `finally`. `finally` runs after `ActivateAsync` has written its outcome and after the operation record is written. Delete step: it checks reparse points again and only warns if the delete fails. Activation copies the payload into `versions/.staging-*` and moves it, so nothing uses the stage afterwards. The test shows an unrelated stage folder is kept | Verified. The guard is sound |
| ANALYSIS-004: skill and docs | Read both `SKILL.md` copies (rules 3, 4 and 6; steps 5 and 6), `docs/releases.md:44,47`, `install-dev.ps1:14-15`, `DevTargets.Resolve` and `DevBuildSkillTests` | Rules: rule 3 has the explicit exception for `worklog update --rollback`, and rule 4 the recovery exception. Rule 6 makes the agent stop on a refusal or failure, and every manual recovery step (including `.retired-*` folders) needs its own authorization. Steps 5 and 6 point to rule 6. The `-Workspace` claim now matches `DevTargets.cs:28`. `DevInstaller.cs:193` still tells the operator to "Delete it manually or rename it back". That is a message for the operator, and rule 6 covers the agent | Verified |
| Skill parity | `cmp` and the identity hashes | Both copies are byte-identical (`0e57f334b294…`). `ClaudeAndCodexCopiesOfTheInstallDevBuildSkillAreIdentical` enforces this | Same text for Claude Code and Codex |
| Smoke comparison (QA-001 fix) | `git diff 8a7508e -- tools/WorkLog.Smoke`. Read the `Program.cs` dispatch and `docs/mcp-validation.md:101-102` | First-token comparison: `UpdateAcceptanceChecks:28` and `WorktreeAcceptanceChecks:53-56` now compare only the first token, and `WorktreeAcceptanceChecks:241` correctly compares the bare MCP version with it. Moved assertion: the stable "plain version only" check has left the smoke, but `package-core.ps1` still enforces it when packaging. Full run: `ClientRegistrationChecks:21` compares with the smoke tool's own `ApplicationInfo.Version`, the source base (for example `0.3.0`), so a full smoke run against a dev package still fails there by design. Only `--updates-only` is an acceptance path for dev packages | No regression. `docs/mcp-validation.md:102` names all three check classes, but only `UpdateAcceptanceChecks` ran for the dev package; the line itself says `--updates-only` |
| Integration parity | `git diff 8a7508e --stat` over the target | The fix round touched no file under `Integration/`, `Clients/` or `Mcp/` | `init`, managed instructions and MCP contracts are unchanged for Claude Code and Codex |
| Formatting | Not run | — | Not run: QA A5 is building at the same time, and the `dotnet format` design-time build writes `obj`. Changed lines follow the surrounding style on inspection |
| Tests | Not run | — | QA A5 owns functional checks. The developer recorded 337 passed in `docs/mcp-validation.md` |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | `Staleness.Check` (once per Preflight and once per Switch). The fix round adds no timers | Measured in this checkout, 5 runs | No idle or interval work. Each operator action spawns `git rev-parse`, plus one `git status` over the inputs when the marker is clean: 29-41 ms. The directory walk reads the times of 118 entries in about 21 ms. A Preflight-then-Switch cycle runs this twice, which is negligible |
| Cache lifetime versus interval and refresh duration | None added | Source observation | Not applicable. The stale check is computed fresh for each action, which correctness requires |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `<data>/updates/staging/<guid>`, one per Switch | Source observation, plus the developer's throwaway run (`docs/mcp-validation.md`: "Switch left no directory under `<data>/updates/staging`") | Now bounded: Switch deletes exactly its own stage on success and on failure. A leftover stage appears only if the process is killed mid-Switch (Retire's staged check still works then) or if the delete fails (a warning names the path). The `versions/.health-<id>` folders left by rollbacks predate this change and are out of scope |
| Repeated full reads where a change check would do | `NewestSource` walk; `git status` | Measured | The walk plus `git status` is itself the change check. Nothing rereads data per interval |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | `git` via `GitRepository.Run`, now two or three calls per action | Source observation | `git` is found only on PATH. `GitRepository.Run` has no timeout (existing helper), and the new `git status` adds one more such call. `git status` runs without `--no-optional-locks`, so it may refresh `.git/index` in the checkout, which is normal Git behavior. A missing `git` still ends in an uncaught `Win32Exception` (noted in analysis-001, unchanged) |

## Findings

### ANALYSIS-041: The clean-tree `git status` check misses deleted root inputs and deleted dashboard `tsconfig` files

- Severity: warning. The scenario is narrow, but a guarantee the docs make is wrong.
- Related task / requirement: 3.1; "Development builds" stale refusal; the ANALYSIS-002 fix.
- Locations:
  - `tools/WorkLog.DevInstall/Staleness.cs:25-26`: `InputPaths` keeps only paths for which `File.Exists` or `Directory.Exists` is true, and lists `tsconfig*.json` from disk.
  - `tools/WorkLog.DevInstall/Staleness.cs:41`: the `git status` call.
  - `tools/WorkLog.DevInstall/Staleness.cs:12` and `docs/releases.md:40`: both say "a dirty package cannot detect the deletion of an individually listed root file", which implies a clean package can.
- Observation:
  - A deleted file is no longer on disk, so `InputPaths` drops its pathspec and `git status` never reports the deletion.
  - Measured in a throwaway repository. With the pathspec, `git status` printed ` D README.md`; with the list filtered to existing paths, it printed nothing (probe 2). A deleted `dashboard/tsconfig.app.json` is missed the same way (probe 3).
  - The repository root and `dashboard/` are not walked, so no directory time catches these deletions either.
  - Result, for clean and dirty packages alike: deleting any of these files after packaging passes Preflight and Switch at the same HEAD:
    - `Directory.Build.props`, `global.json`, `README.md`, `docs/installation.md`
    - `dashboard/package.json`, `dashboard/package-lock.json`, `dashboard/angular.json`
    - any `dashboard/tsconfig*.json`
  - Probe 1 shows the existence filter is not needed: Git accepts a pathspec that matches nothing and exits 0.
- Proposed improvement and benefit:
  - Pass `SourceFiles` and `SourceDirectories` to `git status` without filtering.
  - Replace the disk listing of `tsconfig*.json` with the pathspec `:(glob)dashboard/tsconfig*.json`.
  - Keep the filter and the disk listing in `NewestSource` only.
  - The two documented claims then hold for clean packages.
- Behavior-preservation risks: none for existing trees. When nothing was deleted, the `git status` output is the same.
- Evidence type: measured result (Git pathspec probe) plus source observation.
- Verification / measurement plan: add `StalenessTests` cases with a clean marker. Delete `README.md` and, in a second case, `dashboard/tsconfig.json` (both exist in the fixture), call `AgeEverything()`, and expect the "uncommitted changes" message.
- Relationship: the remaining gap from ANALYSIS-002 (the delete scenario, for root-level inputs).

### ANALYSIS-042: The directory-time rule marks a package stale for excluded changes, and the test hides it

- Severity: suggestion. This is a false positive that fails safe, introduced by the ANALYSIS-002 fix.
- Related task / requirement: 3.1; the ANALYSIS-002 fix; `docs/releases.md:40` ("`dashboard/src` without spec files").
- Locations:
  - `tools/WorkLog.DevInstall/Staleness.cs:69`: `consider(directory)` runs for every walked directory.
  - `tests/WorkLog.Tests/Updates/StalenessTests.cs:58-69`: `ChangesOutsideTheStaleInputsAndSpecFilesDoNotMakeThePackageStale` calls `AgeEverything()` before the check.
- Observation:
  - Creating or deleting a `.spec.ts` file, or an excluded folder (`obj`, `bin`, `TestResults`, `e2e`), inside a walked directory changes that directory's time (probe 5).
  - Preflight then refuses with "Source directory … changed after the package was built", although the docs and the input list exclude those entries.
  - The test that says such changes do not make a package stale calls `AgeEverything()` first, which resets the directory times. It asserts a property the real check does not have.
  - In practice, adding a spec test after packaging forces a rebuild. The check fails closed and costs one rebuild; it is not a safety issue.
- Proposed improvement and benefit: two options.
  - (a) State in the docs and the `Staleness` summary that adding or removing any entry in a walked directory counts as a change, including spec files and build folders. Drop `AgeEverything()` from the test and assert the stale message instead.
  - (b) Count a directory's time only when its set of non-excluded entries differs from the committed tree. This is more code, and the deferred digest option would cover it.

  Option (a) is enough unless these false positives turn out to be frequent.
- Behavior-preservation risks: none for (a), which changes only docs and a test.
- Evidence type: measured result (directory-time probe) plus source observation.
- Verification / measurement plan: for (a), rewrite the test to assert the stale message for an added spec file and an added `obj` folder, and check that the docs bullet describes that behavior.
- Relationship: a regression from the ANALYSIS-002 fix. Analysis-001 expected more false positives in general ("directory mtimes also change when editors write temp files"); this is the specific case of excluded entries, plus the test that hides it.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | `diff=8b494277fff6 untracked=14/fb1d5fbfff28` | Code: the refusals are kept (`DevInstaller.cs:161-167`), and the messages explain the rule that a payload must be neither `previous` nor the recovery selection. Docs: `docs/releases.md:43` gives the sequence Switch → Rollback → Switch or stable update, and `:46` gives the three-step sequence for the manual `0.3.9001`. Skill step 6 and `install-dev.ps1:11` agree. Tracing `Commit` confirms each step. Test: a throwaway test covers Rollback → refused → `Activate("0.2.0")` → removed. The design.md sentence is recorded as superseded in the ledger | verified |
| ANALYSIS-002 | same | Directory times (`Staleness.cs:69`) catch deletes, renames and added copies in walked directories, and `git status` (`:41`) catches them for clean markers. The theory test covers all three changes for both marker states. The input list is documented once (`Staleness.cs:6-13`, `docs/releases.md:40`). Remaining issues: (1) ANALYSIS-041: deleted root-level inputs and `tsconfig` files are missed. (2) Measured: a dirty package misses an overwrite of an existing file that keeps the old timestamp (probe 4). This follows from the accepted minimal option, and the docs limit the copy guarantee to clean packages. (3) ANALYSIS-042: a new false positive that fails safe | verified for the reported scenarios; remaining gap ANALYSIS-041 (warning) |
| ANALYSIS-003 | same | Deletion: `SwitchAsync` deletes exactly its own GUID stage in `finally`, after the outcome and operation records are written. Safety: reparse points are checked on the full ancestor chain before creating and before deleting, and a failed delete only warns and never hides the result. Tests: success (an unrelated stage is kept) and an activation failure; a failure during prepare takes the same `finally` path. One theoretical case remains: the outcome stays `staged` if the hash check after prepare (`DevInstaller.cs:99`) fails. That needs the package to change between two reads, and Retire then ignores it because the stage is gone | verified |
| ANALYSIS-004 | same | Rules 3, 4 and 6 no longer contradict steps 5 and 6. Every fallback needs authorization for that specific step: `worklog update --rollback`, restoring `current.json`, and deleting or renaming a `.retired-*` folder. `docs/releases.md:47` says the same. The `-Workspace` claim matches `DevTargets.cs:28`. `DevBuildSkillTests` asserts the text, and both copies are byte-identical | verified |

## Assessment and limitations

ANALYSIS-001, ANALYSIS-003 and ANALYSIS-004 are verified. ANALYSIS-002 is verified for the three measured scenarios, but its fix leaves one narrow false negative and adds one false positive that fails safe:
- ANALYSIS-041 (warning): the `git status` pathspec drops deleted files.
- ANALYSIS-042 (suggestion): spec files and build folders mark a package stale, and the test hides it.

Other regression checks:
- **Stage deletion guard:** safe. It uses an exact, fresh GUID path, checks the full ancestor chain for reparse points before creating and before deleting, keeps unrelated stages, and cannot hide the operation's own error.
- **Cost of `git status`:** measured at about 30 ms per call, once or twice per operator action, with no interval work.
- **Smoke change:** correct for `--updates-only`. The full smoke run still compares with the tool's own version, which by design excludes dev packages.

The fixes did not make the deferred ANALYSIS-005 – ANALYSIS-008 worse:
- No wider `InternalsVisibleTo` and no new switches.
- No new `EffectiveVersion` call sites and no new copies of the manifest read.
- `docs/architecture.md` is untouched. ANALYSIS-008 stays deferred; the deletion paths are now Retire and stage deletion.

Not performed:
- Build, tests and `dotnet format`: QA A5 runs at the same time and owns functional checks.
- Anything against the default installation, the real data directory or port 47831.

Probe hygiene: the throwaway probes ran only in `artifacts/analysis-002-devbuilds/`, which I removed and confirmed absent. The probe scripts stayed outside the repository. The cost measurement ran a read-only `git --no-optional-locks status` in this checkout. There were no unexpected writes, and the identity is unchanged.

Decisions for the orchestrator:
- ANALYSIS-041: a two-line fix plus a test.
- ANALYSIS-042: fixing the docs and test wording is enough.
