---
name: install-dev-build
description: Build, preview, install, verify, roll back and retire a local WorkLog development build on the maintainer's versioned installation. Use when the user asks to install, try, test, roll back or remove the current dev build, or says "install-dev-build". Switch, Rollback and Retire need the user's explicit authorization for each operation.
---

# Install a WorkLog development build

Maintainer operations for trying an unreleased build on the real versioned installation, with a way back. This skill is operations guidance only. It does not replace the repository's delegated OpenSpec workflow: code changes still go through that workflow.

Run everything from the repository root in PowerShell 7. Paths below use the default per-user installation (`%LOCALAPPDATA%\Programs\WorkLog`) and data directory (`%LOCALAPPDATA%\ZeusApps.WorkLog`).

## Rules that always apply

1. **Explicit authorization for every change.** Build and Preflight are safe to run when asked. `Switch`, `Rollback` and `Retire` change the maintainer's installation, so run each of them only after the user clearly authorizes that specific operation. Earlier approval for a different build, version or action does not carry over. Show the Preflight result first (version, commit, dirty flag, the currently selected version).
2. **Never stop or restart user processes.** The scripts stop and restart nothing. Running MCP servers, dashboards and agent sessions keep the payload they started on and adopt a new selection at their next normal restart. Do not stop terminals, Claude Code or Codex processes, the dashboard or MCP servers, and do not touch the user's dashboard port. If `Retire` reports that a payload is in use, report it and stop; do not close the process.
3. **No integration changes.** Never edit personal client configuration (`~/.claude`, `~/.codex`) and never run `worklog init`, `worklog update` or the installer against the real installation as part of this procedure. `worklog update --check` and `--status` are read-only and fine. The one exception is `worklog update --rollback` as a recovery step, only with the user's explicit authorization for that specific step (rule 6).
4. **No manual payload or launcher edits.** Do not change files under `versions\`, `current.json` or the launcher by hand, unless the user explicitly authorizes that specific recovery step (rule 6). Do not use the disposable test switches (`-InstallationRoot`, `-DataDirectory`, `-SkipProbe`) against the real installation; the script refuses them there. `-Workspace` must always be under `artifacts\`.
5. **Report honestly.** State what ran, the version now selected, where the operation record is, and how to roll back. Say plainly if a step failed or was skipped.
6. **Stop on a refusal or failure.** When Rollback or Retire refuses or fails, stop and report the message. Do not run `worklog update --rollback` or any manual recovery (editing `current.json`, restoring `current.previous.json`, deleting or renaming a `.retired-*` folder or anything under `versions\`) unless the user explicitly authorizes that specific step.

## 1. Build

```powershell
dotnet build -c Release
./scripts/package.ps1 -Dev
```

`package.ps1 -Dev` picks the next free dev number (`MAJOR.MINOR.9001` and up, above anything already under `artifacts\dev-builds\` and in the installation's `versions\`) and writes `artifacts\dev-builds\worklog-<version>-win-x64.zip` plus its `.sha256`. It prints `Packaged <path>`. The package manifest records the dev channel, the source commit, whether the working tree was dirty, the build time and the stable base version.

Preview the plan without building or writing anything: `./scripts/package.ps1 -Dev -PlanOnly`.

## 2. Preflight (read-only)

```powershell
./scripts/install-dev.ps1 -Action Preflight
```

Uses the newest package under `artifacts\dev-builds\` (or pass `-Package <zip>`). It checks the package hash and manifest, requires a marked dev build, refuses a stale build (the package commit differs from HEAD, or a build input, a build-input directory or `git status` over the build inputs changed after the package was built), checks the contracts against the installation, and writes an operation record under `artifacts\dev-builds\operations\`. It prints JSON with `operationManifest`. Keep that path. The installation and the data directory stay unchanged. If it refuses as stale, build again.

## 3. Switch (needs authorization)

```powershell
./scripts/install-dev.ps1 -Action Switch -OperationManifest <operationManifest from Preflight>
```

Verifies the package again, then publishes the payload next to the existing ones and selects it for new processes through the product's own update code, recording the original selection as `previous`. It stops without changing anything if the selection changed since Preflight. It does not stop or restart any process.

## 4. Verify

```powershell
$launcher = "$env:LOCALAPPDATA\Programs\WorkLog\worklog.exe"
& $launcher --version          # 0.3.9001 dev 50df7b8 (2026-10-28 09:12 UTC)
& $launcher update --status    # Running ...; selected 0.3.9001 dev ... for new processes
& $launcher update --check     # a later stable release is still offered
```

The dashboard footer shows the same label after the dashboard is restarted by the user. A stable build shows only its plain version. Dev builds compare stable releases with their base version, so a later stable release is offered and installable over the dev build.

## 5. Roll back (needs authorization)

```powershell
./scripts/install-dev.ps1 -Action Rollback -OperationManifest <same operationManifest>
```

Restores the selection from immediately before the switch (one level) for new processes. Journal, configuration and data are untouched. It refuses if the selection changed since the switch, for example after a stable update. Then stop and report (rule 6); `worklog update --rollback` and the recovery steps in `docs/releases.md` are manual recovery that needs the user's explicit authorization for that specific step.

## 6. Retire an old dev payload (needs authorization)

```powershell
./scripts/install-dev.ps1 -Action Retire -Version 0.3.9001
```

Removes a development-range payload only if it is not selected, not recorded as previous, not the recovery selection in `current.previous.json`, not the image of a running process and not needed by an unfinished update. Stable payloads are never removed. Unmarked dev-range payloads, such as a manually installed `0.3.9001`, can be retired. Every selection change records the outgoing payload as previous and recovery selection, so right after a Rollback or a stable update the dev payload is still refused. It becomes retirable after one further selection change to another payload (a Switch or a stable update). Explain this to the user instead of working around the refusal. A failed delete leaves a `.retired-<version>-<id>` folder: report it and stop (rule 6). The payload's file hashes are verified, then the directory is renamed and deleted, so a failure leaves it recoverable.

## Where things are

- Packages and operation records: `artifacts\dev-builds\` and `artifacts\dev-builds\operations\`.
- Procedure, version rules and recovery: `docs/releases.md` ("Development builds").
- Both Claude Code (`.claude/skills/`) and Codex (`.agents/skills/`) carry this identical file; a test fails if the copies differ.
