# Proposal

## Why

The maintainer needs to try unreleased WorkLog builds on the real versioned installation before a release. `worklog update` installs only official releases, and a local build carries the same version as the installed release. On 2026-10-28, `add-review-inbox` was installed as `0.3.9001` through a throwaway driver around `PayloadInstaller`. That build has no dev marker or label, nothing can remove it later, and while it is selected it hides every stable `0.3.x` release. Bell Tower already solves this with a dev-build workflow that has been proven on the same launcher and payload model.

## What Changes

- **Dev version range.**
  - A version whose PATCH is 9000 or higher (for example `0.3.9001`) is a development build of base `MAJOR.MINOR` plus the base version in `Directory.Build.props`.
  - Stable packaging refuses that range, and published release offers in the range are rejected.
- **Dev marker and label.**
  - `release.json` gains an optional `build` object: `channel: "dev"`, commit, dirty flag, build time, and base version.
  - `worklog --version`, `worklog update --status`, and the dashboard footer show one label, for example `0.3.9001 dev 50df7b8 (2026-10-28 09:12 UTC)`. A dev-range payload without a marker shows `dev (unmarked)`. Stable builds keep their plain version.
- **Updates over dev builds.**
  - While a dev build is running or selected, release discovery, `worklog update --check`, MCP update hints, and activation compare stable releases with the dev build's base version. An unmarked dev-range payload counts as `MAJOR.MINOR.0`.
  - So a stable `0.3.1` is offered and installable over dev `0.3.9001`, and it is not refused as a downgrade.
- **Dev build tooling.**
  - `scripts/package.ps1 -Dev` builds the next free dev version into `artifacts/dev-builds/`.
  - `scripts/install-dev.ps1 -Action Preflight|Switch|Rollback|Retire` previews, installs, rolls back, and removes dev payloads on the real installation.
  - All of these use the product's own verify, publish, and select code, never hand edits.
  - Retire removes only dev-range payloads that are not selected, not recorded as previous, not in use, and not needed by an unfinished update.
- **Agent skill.**
  - An identical `install-dev-build` skill in `.claude/skills/` and `.agents/skills/`, so Claude Code and Codex follow the same procedure. A test fails if the two copies differ.
  - Switch, Rollback, and Retire need the user's explicit authorization for each operation.
- **Docs.** The procedure, the version rules, and recovery steps go in `docs/releases.md`, linked from `README.md`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `application-distribution`:
  - Adds the "Development builds" requirement.
  - Modifies "Non-blocking stable release discovery" and "Explicit verified Windows update", so stable offers and activation compare against a dev build's base version and dev-range offers are rejected.

## Impact

- **Code:**
  - `src/WorkLog/Installation/InstallationLayout.cs`: the optional `build` manifest field and the effective-version rule.
  - `src/WorkLog/Updates`: `ReleaseInfo`, `UpdateDiscovery`, `PayloadInstaller`, `UpdateCommand`.
  - `src/WorkLog/Cli/ApplicationInfo.cs`: the version label.
  - The dashboard footer label.
  - A new maintainer tool, `tools/WorkLog.DevInstall`, run by `scripts/install-dev.ps1`.
  - `scripts/package.ps1` and `package-core.ps1`.
- **Contracts:**
  - Payload manifest: an additive optional field, so package format 2 is unchanged.
  - The launcher contract, the shared-data contract, journal schema 2, and the MCP tool schemas are unchanged.
  - The `updateHint` fields keep their meaning; only the version they are compared with changes for dev builds.
- **Clients:** the skill is shared by Claude Code and Codex. `worklog init`, managed instructions, and MCP behavior do not change.
- **Existing dev payload:** the manual `0.3.9001` payload is unmarked. Once this change ships, it reads as `dev (unmarked)`, counts as base `0.3.0` for updates, and Retire can remove it.
- **Tests:**
  - .NET tests for version rules, manifest parsing, update comparison, and Retire safety.
  - Script tests against disposable installations under `artifacts/`.
  - Skill-parity test.
  - Packaged acceptance of the dev-to-stable update.
