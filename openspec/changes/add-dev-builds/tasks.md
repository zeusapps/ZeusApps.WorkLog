# Tasks

Apply through the orchestrator workflow. Run every test against disposable installations and data under `artifacts/`. No step may switch, roll back, or retire on the maintainer's real installation unless the user authorizes that specific operation.

## 1. Version rules and marker

- [x] 1.1 Add the dev-range rule, the optional `build` manifest record, and `EffectiveVersion` (Decisions 1–2); verify with .NET tests for range boundaries (8999, 9000), marked and unmarked base versions, and a manifest with `build` read by the 0.3.0 manifest options without error
- [x] 1.2 Use the effective version in `UpdateDiscovery`, `ReleaseInfo` offers, `PrepareDownloadedAsync`, and `ActivateAsync`, and reject dev-range offers; verify with .NET tests that stable `0.3.1` is offered and activates over selected dev `0.3.9001` (marked and unmarked), that `0.2.x` is not offered, and that a published `0.3.9005` is ignored
- [x] 1.3 Add `VersionDisplay` and use it in `--version`, `update --status`, and the dashboard footer; verify with tests for stable, marked dev, dirty dev, and unmarked dev labels, and that the first token of `--version` is the bare version

## 2. Packaging

- [x] 2.1 Add `package.ps1 -Dev [-DevVersion] [-PlanOnly]` with next-number selection, the marker, `-dev+<commit>[.dirty]` informational version, and stable-range refusal of dev versions (Decision 3); verify by packaging into a disposable output directory twice (numbers 9001 then 9002) and checking `-PlanOnly` writes nothing

## 3. Installer tool and script

- [x] 3.1 Add `tools/WorkLog.DevInstall` and `scripts/install-dev.ps1` with Preflight and Switch (Decision 4); verify against a disposable installation built from a stable package: preflight changes nothing and refuses stale or unmarked packages; switch selects the dev build with the original as previous
- [x] 3.2 Add Rollback and Retire; verify that rollback restores the original selection and refuses after a later selection change, and that retire refuses selected, previous, stable, in-use (a running payload process), and staging-referenced payloads, and removes an unmarked dev-range payload
- [x] 3.3 Verify the end-to-end path on disposable data: stable `0.0.1` → dev `0.0.9001` → stable `0.0.2` through the product's `PayloadInstaller.DownloadAsync` and `ActivateAsync`, served by a local release fixture (`worklog update` can reach only the fixed GitHub endpoint) → rollback returns to the dev build; record results in `docs/mcp-validation.md`

## 4. Skill and docs

- [x] 4.1 Add the identical `install-dev-build` skill to `.claude/skills/` and `.agents/skills/`, and a parity test in `tests/WorkLog.Tests`; verify that the test fails when the copies differ
- [x] 4.2 Document development builds (range, marker, label, update behavior, procedure, recovery, the manual `0.3.9001` payload) in `docs/releases.md`, link from `README.md`; verify the links resolve

## 5. Validation

- [x] 5.1 Run the full validation set (restore, build, test, `npm test`, packaged e2e, packaged MCP and update smoke, `package-update-fixtures.ps1`) and record results
- [x] 5.2 Run `openspec update` in a disposable copy of the repository and verify the `install-dev-build` skill directories survive unchanged
