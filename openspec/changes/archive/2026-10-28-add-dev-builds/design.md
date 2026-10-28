# Design

## Context

See proposal.md. The current code:
- `InstallationLayout` (`src/WorkLog/Installation`) parses strict `MAJOR.MINOR.PATCH` versions, reads `current.json` (current and previous selection, generation), and validates `release.json` (`PayloadManifest`, package format 2).
- `PayloadInstaller.PrepareDownloadedAsync` verifies a staged archive. It checks the checksum, extracts with an allowlist, and runs a health probe on disposable data. `ActivateAsync` then publishes the payload immutably and commits the selection atomically under the publisher lock. `RollbackAsync` selects `previous`.
- `ReleaseInfo.IsNewerThan`, `UpdateDiscovery`, and `ActivateAsync` compare plain versions.
- `ApplicationInfo` strips the informational version at `+`. The build stamps it as `<version>+<commit>`.
- `scripts/package-core.ps1`'s `New-WorkLogPackage` builds any `MAJOR.MINOR.PATCH`. `package.ps1` enforces agreement with `Directory.Build.props`.
- WorkLog has no tray. Existing processes hold no installation-wide lease and keep the payload they started on. The dashboard and MCP servers adopt a new selection when restarted.

Bell Tower (`D:\Code\ZeusApps.BellTower`, `docs/development.md` "Development builds") already implements the same model on the same launcher design, and this change ports it. Bell Tower's tray stop, restart, and monitoring snapshot have no WorkLog counterpart and are dropped.

## Goals / Non-Goals

**Goals:** a safe, repeatable way to try a local build on the real installation; a visible label; a way back; cleanup; correct stable updates over a dev build; the same procedure for Claude Code and Codex.

**Non-Goals:**
- Publishing dev builds through GitHub. Bell Tower doesn't either:
  - its dev packages never go into releases and stay under `artifacts/dev-builds/`;
  - its release workflow has no dev step;
  - its update client rejects published offers in the dev range.

  WorkLog matches this: dev builds stay local, and Decision 1 rejects dev-range offers.
- Automatic payload cleanup.
- Stopping or restarting user processes.
- Changing the launcher, the shared-data contract, the journal, or MCP schemas.
- Installing arbitrary unmarked local packages through `worklog update`.

## Decisions

### 1. Version range and effective version
- A version with PATCH ≥ 9000 is a development build. Its base version comes from `build.baseVersion` when the marker is present. Without a marker it is `MAJOR.MINOR.0`.
- `InstallationLayout.EffectiveVersion(version, manifest)` returns the base version for development builds and the version itself otherwise. All newer-than checks use it:
  - `ReleaseInfo.IsNewerThan` call sites;
  - `UpdateDiscovery` suppression;
  - `PrepareDownloadedAsync`;
  - `ActivateAsync`'s downgrade and superseded checks.
- `ReleaseInfo.IsValid` rejects dev-range release versions.
- Dev builds sort above stable releases of the same minor version only for installation, never for offers.

Alternative: a SemVer pre-release suffix such as `0.3.1-dev.1`. Rejected because installed launchers and the payload directory format accept only three numbers.

### 2. Marker and label
- `PayloadManifest` gains an optional `Build` record: `Channel`, `Commit`, `Dirty`, `BuiltAt`, `BaseVersion`.
- The packager writes it only with `-Dev`, which keeps package format 2. Older WorkLog versions ignore the unknown field; verify this with a test against the 0.3.0 `PayloadManifest` options.
- The informational version becomes `<version>-dev+<commit>[.dirty]`.
- A shared `VersionDisplay` formats `0.3.9001 dev 50df7b8 (2026-10-28 09:12 UTC)`, with `dirty` after the commit when applicable. It is used by:
  - `--version`;
  - `update --status`;
  - the dashboard footer, through an additive `version` field in an existing endpoint.
- The first token of `--version` stays the bare version, so `package-core.ps1`'s smoke check and other scripts that compare it keep working; those comparisons are updated to compare only the first token.

### 3. Packaging
- `scripts/package.ps1 -Dev [-DevVersion x.y.z] [-PlanOnly]` picks the next number above everything under `artifacts/dev-builds/` and the default installation's `versions/`. It calls `New-WorkLogPackage` with the marker parameters.
- Without `-Dev`, the script refuses the dev range.
- The dirty flag considers `src`, `tests`, `tools`, `scripts`, `dashboard`, `docs`, and the top-level build files.

### 4. Installer tool and script
- A new `tools/WorkLog.DevInstall` console project references `src/WorkLog`, like `tools/WorkLog.Smoke`. It calls `PayloadInstaller` and `InstallationLayout` so that verification, publication, selection, and outcome records are the product's own.
- `scripts/install-dev.ps1 -Action Preflight|Switch|Rollback|Retire` is a thin wrapper that builds and runs the tool.
- **Preflight:**
  - checks the package checksum and the marker;
  - refuses a stale build: the commit differs from HEAD, or sources are newer than the package;
  - checks the contracts against the installation;
  - writes an operation record under `artifacts/dev-builds/operations/<id>.json`: package hash, original selection, target version.
  - It changes nothing in the installation or data directory.
- **Switch:**
  - requires the operation record and an unchanged original selection;
  - stages the archive under `<data>/updates/staging/<guid>`, then calls `PrepareDownloadedAsync` and `ActivateAsync` with the effective running version. The verification is the same one ordinary updates use.
  - records the resulting selection, and reports that running dashboards and servers adopt it on restart.
- **Rollback:**
  - requires the current selection to equal the one Switch produced;
  - calls `RollbackAsync`, which selects `previous`, the original. This is one level.
- **Retire `-Version`:**
  - takes the publisher lock;
  - refuses stable versions, the selected version, the previous version, payloads referenced by unfinished staging or update outcomes, and payloads whose `worklog.exe` is the image of a running process;
  - verifies the payload's file hashes and then removes the directory.
  - Unmarked dev-range payloads, such as the manual `0.3.9001`, can be retired, because stable packaging can never produce that range.
- Disposable switches (`-InstallationRoot`, `-DataDirectory` under `artifacts/`, `-SkipProbe`) exist for tests only and are refused for the default installation.

Alternative: a hidden `worklog update --from-package`. Rejected because it would ship a local-package install path to every user.

### 5. Skill and docs
- `install-dev-build/SKILL.md` lives in both `.claude/skills/` and `.agents/skills/`, with identical text. A test in `tests/WorkLog.Tests` compares the two copies.
- These directories also hold generated OpenSpec skills. The new directory has a different name, and the OpenSpec CLI's managed set must leave it alone; verify this with `openspec update` in a disposable copy.
- The skill requires explicit user authorization per Switch, Rollback, and Retire, and forbids stopping user processes.
- `docs/releases.md` gets a "Development builds" section, and `README.md` links to it.

## Risks / Trade-offs

- **Unmarked dev payloads from older code.** A `0.3.0` server reading a dev manifest ignores the marker and shows the plain version. This is cosmetic.
- **A stable update while a dev build is selected records the dev build as previous.** Rollback then returns to the dev build. This is intended and documented.
- **Retire relies on process image paths to detect use.** A process that loaded the payload through another path would be missed. Mitigation: Windows refuses to delete a running executable anyway, and Retire deletes only after a successful hash verification, renaming the directory first so a failure leaves it recoverable.
- **The skill files share directories with generated OpenSpec skills.** Risk: a future `openspec update` might prune unknown directories. Covered by the disposable-copy check in task 5.2.

## Migration Plan

Additive. Existing installations keep their selection. The manual `0.3.9001` payload is treated as unmarked dev with base `0.3.0`. After this change ships, the maintainer can roll back from it and retire it.
