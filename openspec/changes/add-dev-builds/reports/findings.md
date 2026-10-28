# Findings: add-dev-builds

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

Advisory marker; resume and hand-off rules are in the [workflow](../../../../.agents/workflows/openspec-delegation.md#mixed-client-operation).

- Client / version: Claude Code 2.1.312
- Session identity: f25921b7-0c78-457f-9581-5041c8ca773d
- Since (UTC): 2026-10-28T09:37Z
- Previous owner: none

## Scope decisions

- Baseline commit: `8a7508e` on branch `change/add-dev-builds` (created from `main`).
- 2026-10-28, ANALYSIS-001 triage: the design.md Migration Plan sentence "the maintainer can roll back from it and retire it" is superseded. A dev payload is retirable once it is neither `previous` nor the `current.previous.json` selection. After a Rollback away from it, one further selection change to another payload is enough. The spec text is unchanged.
- 2026-10-28: no task may switch, roll back, or retire on the maintainer's real installation. All installer verification uses disposable installations and data under `artifacts/`.
- 2026-10-28, A1 developer deviations, accepted provisionally by the orchestrator for reviewers to check:
  1. Stale rule: the package is stale when the marker commit differs from HEAD, or when any build input is newer than `builtAt` + 1 s. Build inputs are `src/**` without bin/obj, `dashboard/src` without spec files, the dashboard config files, `Directory.Build.props`, `global.json`, `README.md`, and `docs/installation.md`. There is no override.
  2. The marker stores the full 40-character commit; the label shows 7 characters.
  3. Preflight also refuses a dev package that is not above the selected raw version (beyond the design).
  4. Operation records carry states (`preflighted`, `switched`, `rolledBack`) for one-shot semantics.
  5. Retire treats a staged outcome as blocking only while its staging directory exists.
  6. Task 3.3's stable step used the real `PayloadInstaller.DownloadAsync` and `ActivateAsync`, with a throwaway HTTP handler outside the repository serving the official URLs, because `worklog update` can reach only GitHub. Rollback used the real `worklog update --rollback`.
  7. `ReleasePackage.VerifyExecutableAsync` and the package smoke check compare only the first token of `--version`. `PayloadInstaller.RollbackAsync` gains an optional `expected` selection. `WorkLog.csproj` gains `InternalsVisibleTo WorkLog.DevInstall`.
  8. 5.1 `scripts/package-update-fixtures.ps1` failed at the launcher hidden-console CTRL+C step. The developer reproduced the failure with pre-change binaries and attributes it to the non-interactive environment. The mixed-version step passed when run directly. Unverified: this needs an interactive run.
  9. Existing personal data in `docs/mcp-validation.md` (older TRX paths, lines 67 and 184) predates this change. Raised for hygiene triage.

- 2026-10-28, user decision (task 3.3): QA independently reproduces the end-to-end path using the developer's method. The stable step uses the product's `DownloadAsync` and `ActivateAsync` against a local HTTP fixture, and rollback uses the real `worklog update --rollback`. The task 3.3 wording was changed to name that method. No product change.

- 2026-10-28, task 5.1: the maintainer ran `./scripts/package-update-fixtures.ps1` in an interactive console on this branch at identity `diff=d12e556f180a untracked=14/40e6fbfee0b6`. Results were `LAUNCHER PASSED` (including isolated CTRL+C and nested-job refusal) and `MIXED-VERSION PASSED`. This replaces the environment-limited result recorded earlier. A11 (docs-only) updates the `docs/mcp-validation.md` row.
- 2026-10-28, pre-existing observation outside this change's scope: in that run, `package-update-fixtures.ps1` wrote its fixtures under the user's home directory rather than the repository's `artifacts/`. `[System.IO.Path]::GetFullPath($OutputDirectory)` resolves against the .NET process directory, not the PowerShell location set by `Push-Location`. The script is unchanged since the baseline. Follow-up: the cleanup change (resolve against `$projectRoot`).

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A1 implementation | developer | Claude Code / role default | none: role default | tasks 1.1–5.2; baseline `8a7508e` | implementation paths only | none | done 2026-10-28, model claude-sonnet-5-5; identity `head=8a7508e baseline=8a7508e files=36 diff=2d7cc746d6de untracked=13/5ef0fa129124` (paths: README.md ZeusApps.WorkLog.slnx dashboard/e2e/dashboard.spec.ts dashboard/src/app.html dashboard/src/app.reliability.spec.ts dashboard/src/app.ts docs/mcp-validation.md docs/releases.md scripts/package-core.ps1 scripts/package.ps1 scripts/install-dev.ps1 src/WorkLog/Cli/ApplicationInfo.cs src/WorkLog/Cli/ApplicationVersionAction.cs src/WorkLog/Cli/UpdateCommand.cs src/WorkLog/Dashboard/DashboardHost.cs src/WorkLog/Installation/InstallationLayout.cs src/WorkLog/Installation/VersionDisplay.cs src/WorkLog/Updates/PayloadInstaller.cs src/WorkLog/Updates/ReleaseInfo.cs src/WorkLog/Updates/ReleasePackage.cs src/WorkLog/Updates/UpdateDiscovery.cs src/WorkLog/WorkLog.csproj tests/WorkLog.Tests/Cli/CliTests.cs tests/WorkLog.Tests/WorkLog.Tests.csproj tests/WorkLog.Tests/packages.lock.json tests/WorkLog.Tests/Integration/DevBuildSkillTests.cs tests/WorkLog.Tests/Updates/DevBuildVersionTests.cs tests/WorkLog.Tests/Updates/DevInstallTests.cs tools/WorkLog.DevInstall .claude/skills/install-dev-build .agents/skills/install-dev-build), confirmed by the orchestrator |
| A2 review QA | qa | Claude Code / role default | none: role default | tasks 1.1–5.2; identity of A1 | `qa-001.md`; screenshots `../screenshots/qa-001-*.png` | QA-001 – QA-040 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report; QA-001..003 new (004–040 unused, retired); identity unchanged |
| A4 fix round 1 | developer | Claude Code / role default | none: role default | QA-001, QA-002, ANALYSIS-001..004; bounded investigation QA-003; identity `diff=2d7cc746d6de untracked=13/5ef0fa129124` | implementation paths only | none | done 2026-10-28, model claude-sonnet-5-5; identity `head=8a7508e baseline=8a7508e files=40 diff=8b494277fff6 untracked=14/fb1d5fbfff28` (paths: README.md ZeusApps.WorkLog.slnx dashboard/e2e/dashboard.spec.ts dashboard/src/app.html dashboard/src/app.reliability.spec.ts dashboard/src/app.ts docs/mcp-validation.md docs/releases.md scripts/package-core.ps1 scripts/package.ps1 scripts/install-dev.ps1 src/WorkLog/Cli/ApplicationInfo.cs src/WorkLog/Cli/ApplicationVersionAction.cs src/WorkLog/Cli/UpdateCommand.cs src/WorkLog/Dashboard/DashboardHost.cs src/WorkLog/Installation/InstallationLayout.cs src/WorkLog/Installation/VersionDisplay.cs src/WorkLog/Updates/PayloadInstaller.cs src/WorkLog/Updates/ReleaseInfo.cs src/WorkLog/Updates/ReleasePackage.cs src/WorkLog/Updates/UpdateDiscovery.cs src/WorkLog/WorkLog.csproj tests/WorkLog.Tests/Cli/CliTests.cs tests/WorkLog.Tests/WorkLog.Tests.csproj tests/WorkLog.Tests/packages.lock.json tests/WorkLog.Tests/Integration/DevBuildSkillTests.cs tests/WorkLog.Tests/Updates/DevBuildVersionTests.cs tests/WorkLog.Tests/Updates/DevInstallTests.cs tests/WorkLog.Tests/Updates/StalenessTests.cs tools/WorkLog.DevInstall tools/WorkLog.Smoke .claude/skills/install-dev-build .agents/skills/install-dev-build), confirmed by the orchestrator |
| A5 recheck QA | qa | Claude Code / role default | none: role default | QA-001, QA-002, ANALYSIS-002 and ANALYSIS-003 (functional) plus regression; identity of A4 | `qa-002.md`; screenshots `../screenshots/qa-002-*.png` | QA-041 – QA-060 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report; QA-001, QA-002, ANALYSIS-002/003 (functional) verified; QA-003 dismissal confirmed; no new findings (QA-041..060 unused, retired); identity unchanged |
| A6 recheck analysis | code-analyzer | Claude Code / role default | none: role default | ANALYSIS-001 – ANALYSIS-004 plus regression; identity of A4 | `analysis-002.md` | ANALYSIS-041 – ANALYSIS-060 | done 2026-10-28, model claude-opus-5-5; write refused, saved by orchestrator under a header quoting the refusal; ANALYSIS-001..004 verified (002 for reported scenarios); ANALYSIS-041, 042 new (043–060 unused, retired); identity unchanged |
| A7 fix round 2 (final allowed cycle) | developer | Claude Code / role default | none: role default | ANALYSIS-041; identity `diff=8b494277fff6 untracked=14/fb1d5fbfff28` | implementation paths only | none | done 2026-10-28, model claude-sonnet-5-5; identity `head=8a7508e baseline=8a7508e files=40 diff=d12e556f180a untracked=14/40e6fbfee0b6` (same path list as A4), confirmed by the orchestrator |
| A8 recheck QA | qa | Claude Code / role default | none: role default | regression after ANALYSIS-041; identity of A7 | `qa-003.md` | QA-061 – QA-080 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report; ANALYSIS-041 and ANALYSIS-002 (functional) verified; no new findings (QA-061..080 unused, retired); identity unchanged |
| A9 recheck analysis | code-analyzer | Claude Code / role default | none: role default | ANALYSIS-041 plus regression; identity of A7 | `analysis-003.md` | ANALYSIS-061 – ANALYSIS-080 | done 2026-10-28, model claude-opus-5-5; write refused, saved by orchestrator under a header quoting the refusal; ANALYSIS-041 verified; ANALYSIS-061 new (062–080 unused, retired); identity unchanged |
| A10 verification QA | qa | Claude Code / role default | none: role default | task 3.3 independent reproduction; identity `diff=d12e556f180a untracked=14/40e6fbfee0b6` | `qa-004.md` | QA-081 – QA-100 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report; task 3.3 independently reproduced, matches the developer record; no findings (QA-081..100 unused, retired); identity unchanged |
| A11 docs fix | developer | Claude Code / role default | none: role default | docs/mcp-validation.md fixtures row only; identity `diff=d12e556f180a untracked=14/40e6fbfee0b6` | implementation paths only | none | done 2026-10-28, model claude-sonnet-5-5; identity `head=8a7508e baseline=8a7508e files=40 diff=dcb81d6747a4 untracked=14/40e6fbfee0b6`, confirmed by the orchestrator |
| A12 light review QA | qa | Claude Code / role default | none: role default | A11 docs row, correctness and hygiene; identity of A11 | `qa-005.md` | QA-101 – QA-120 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report; row accurate, hygiene clean; no findings (QA-101..120 unused, retired); identity unchanged |
| A13 light review analysis | code-analyzer | Claude Code / sonnet (cost-efficient) | docs-only target; workflow permits the cost-efficient analyzer override | A11 docs row; identity of A11 | `analysis-004.md` | ANALYSIS-081 – ANALYSIS-100 | done 2026-10-28, model claude-sonnet-5-5 (override); write refused, saved by orchestrator under a header quoting the refusal; no findings (ANALYSIS-081..100 unused, retired); identity unchanged |
| A3 review analysis | code-analyzer | Claude Code / role default | none: role default | tasks 1.1–5.2; identity of A1 | `analysis-001.md` | ANALYSIS-001 – ANALYSIS-040 | done 2026-10-28, model claude-opus-5-5; write refused, saved by orchestrator under a header quoting the refusal; ANALYSIS-001..008 new (009–040 unused, retired); identity unchanged |

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| QA-001 | [qa-001](qa-001.md) | 1.3, 5.1; Decision 2 | warning | yes: breaks Decision 2 and dev-package smoke acceptance | verified | developer A4 | [qa-002](qa-002.md) |
| QA-002 | [qa-001](qa-001.md) | hygiene | critical | yes: hygiene blocks the commit of a file this change modifies (pre-existing) | verified | developer A4 | [qa-002](qa-002.md) |
| QA-003 | [qa-001](qa-001.md) | 3.1; Preflight read-only | suggestion | no: investigation found no Preflight write | dismissed | none | A4 investigation |
| ANALYSIS-001 | [analysis-001](analysis-001.md) | 3.2, 4.2 | warning | yes: documented recovery and the skill fail on the real installation; docs-only fix | verified | developer A4 | [analysis-002](analysis-002.md) |
| ANALYSIS-002 | [analysis-001](analysis-001.md) | 3.1; stale refusal | warning | yes: spec requires refusing stale packages; measured false negatives | verified | developer A4 | [analysis-002](analysis-002.md); [qa-002](qa-002.md) |
| ANALYSIS-003 | [analysis-001](analysis-001.md) | 3.1 | warning | yes: unbounded growth of the real data directory per Switch | verified | developer A4 | [analysis-002](analysis-002.md); [qa-002](qa-002.md) |
| ANALYSIS-004 | [analysis-001](analysis-001.md) | 4.1, 4.2; per-operation authorization | warning | yes: safety of agent behavior on the real installation | verified | developer A4 | [analysis-002](analysis-002.md) |
| ANALYSIS-041 | [analysis-002](analysis-002.md) | 3.1; stale refusal | warning | yes: spec requires refusing stale packages; measured false negative for deleted root inputs | verified | developer A7 | [analysis-003](analysis-003.md); [qa-003](qa-003.md) |
| ANALYSIS-042 | [analysis-002](analysis-002.md) | 3.1 | suggestion | no: severity gate (fails safe) | deferred | unassigned | n/a |
| ANALYSIS-061 | [analysis-003](analysis-003.md) | 3.1 | suggestion | no: severity gate (fails safe); no fix cycles left | deferred | unassigned | n/a |
| ANALYSIS-005 | [analysis-001](analysis-001.md) | 3.1 | suggestion | no: severity gate | deferred | unassigned | n/a |
| ANALYSIS-006 | [analysis-001](analysis-001.md) | 1.1, 1.2 | suggestion | no: severity gate | deferred | unassigned | n/a |
| ANALYSIS-007 | [analysis-001](analysis-001.md) | 1.3, 3.1 | suggestion | no: severity gate | deferred | unassigned | n/a |
| ANALYSIS-008 | [analysis-001](analysis-001.md) | 4.2 | suggestion | no: severity gate | deferred | unassigned | n/a |


## Finding details and history

### QA-001: Packaged smoke tool rejects dev packages

- Sources and evidence: [qa-001](qa-001.md)
- Required behavior: Decision 2: in-repo `--version` consumers compare only the first token.
- Triage: blocking; clear fix within scope.
- Action / assignment: A4. Compare the first token in `tools/WorkLog.Smoke` and run the packaged smoke against a dev package.
- Duplicate of: none
- Deferral: none
- User decision: not needed
- Verification: QA A5, identity `diff=8b494277fff6 untracked=14/fb1d5fbfff28`, [qa-002](qa-002.md): packaged `--updates-only` smoke passed for stable 0.3.0 and dev 0.3.9001. Verified.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | QA A2 | qa-001 |
| 2026-10-28 | reported | assigned | orchestrator | A4 |
| 2026-10-28 | assigned | awaiting-verification | developer A4 | A4 handoff; identity `head=8a7508e baseline=8a7508e files=40 diff=8b494277fff6 untracked=14/fb1d5fbfff28` |
| 2026-10-28 | awaiting-verification | verified | orchestrator | qa-002 |

### QA-002: Pre-existing personal data in `docs/mcp-validation.md`

- Sources and evidence: [qa-001](qa-001.md); developer deviation 9
- Required behavior: committable content hygiene for files committed with this change.
- Triage: blocking for the commit. The data predates this change, but this change commits the file. It is an implementation doc, so the redaction goes to the developer.
- Action / assignment: A4. Replace the account and host segments with placeholders.
- Duplicate of: none
- Deferral: none
- User decision: not needed. The user was told it would be handled in triage and raised no objection.
- Verification: QA A5, identity `diff=8b494277fff6 untracked=14/fb1d5fbfff28`, [qa-002](qa-002.md): hygiene scan of 48 files clean. Verified.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | QA A2 | qa-001 |
| 2026-10-28 | reported | assigned | orchestrator | A4 |
| 2026-10-28 | assigned | awaiting-verification | developer A4 | A4 handoff; identity `head=8a7508e baseline=8a7508e files=40 diff=8b494277fff6 untracked=14/fb1d5fbfff28` |
| 2026-10-28 | awaiting-verification | verified | orchestrator | qa-002 |

### QA-003: First Preflight changes a payload directory's modification time

- Sources and evidence: [qa-001](qa-001.md)
- Required behavior: "Preflight SHALL be read-only toward the installation."
- Triage: suggestion; the cause is unknown. A bounded investigation runs in A4. If Preflight creates or deletes any entry, re-triage as a warning against the read-only requirement.
- Action / assignment: A4 investigation only
- Duplicate of: none
- Deferral: none (dismissed)
- User decision: not needed
- Verification: not applicable (dismissed). A4 found no write, create, delete, or rename API on the Preflight path. A FileSystemWatcher recorded zero events during Preflight. Five Preflight-only runs without observers left the directory write time unchanged. The single reproduction had a recursive enumeration and watcher around it, so this is likely NTFS lazy timestamp behavior triggered by the observer. QA A5 is asked to confirm without observers.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | QA A2 | qa-001 |
| 2026-10-28 | reported | dismissed | orchestrator | A4 investigation: no Preflight write |

### ANALYSIS-001: Retire not reachable right after Rollback or one stable update

- Sources and evidence: [analysis-001](analysis-001.md)
- Required behavior: the spec requires Retire to refuse the previous payload. The docs and skill must describe when retire becomes possible.
- Triage: blocking, docs-only fix. The safe refusal is kept, and no spec change is made. The design.md Migration Plan sentence ("roll back from it and retire it") is superseded: retire works once the payload is neither `previous` nor the `current.previous.json` selection.
- Action / assignment: A4. Correct `docs/releases.md`, the skill (both copies), and the in-tool message if it misleads. Add a disposable test: Rollback, Retire refused, a further selection change, Retire succeeds.
- Duplicate of: none
- Deferral: none
- User decision: not needed (keeps spec behavior)
- Verification: analyzer A6, identity `diff=8b494277fff6 untracked=14/fb1d5fbfff28`, [analysis-002](analysis-002.md). Verified.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | analyzer A3 | analysis-001 |
| 2026-10-28 | reported | assigned | orchestrator | A4; docs-only option chosen |
| 2026-10-28 | assigned | awaiting-verification | developer A4 | A4 handoff; identity `head=8a7508e baseline=8a7508e files=40 diff=8b494277fff6 untracked=14/fb1d5fbfff28` |
| 2026-10-28 | awaiting-verification | verified | orchestrator | analysis-002 |

### ANALYSIS-002: Stale rule misses deletes, renames, and timestamp-preserving copies

- Sources and evidence: [analysis-001](analysis-001.md)
- Required behavior: Preflight refuses stale packages.
- Triage: blocking. The minimal option was chosen: include directory modification times, and when the marker is not dirty, require a clean `git status` over the stale inputs. Keep one documented input list.
- Action / assignment: A4. Add tests for delete, rename, and old-mtime copy.
- Duplicate of: none
- Deferral: none
- User decision: not needed
- Verification: analyzer A6 and QA A5, identity `diff=8b494277fff6 untracked=14/fb1d5fbfff28`: verified for the reported delete, rename, and copy scenarios. The remaining gap for deleted root-level inputs is tracked as ANALYSIS-041.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | analyzer A3 | analysis-001 |
| 2026-10-28 | reported | assigned | orchestrator | A4 |
| 2026-10-28 | assigned | awaiting-verification | developer A4 | A4 handoff; identity `head=8a7508e baseline=8a7508e files=40 diff=8b494277fff6 untracked=14/fb1d5fbfff28` |
| 2026-10-28 | awaiting-verification | verified | orchestrator | analysis-002 and qa-002; residual gap split to ANALYSIS-041 |

### ANALYSIS-003: Switch leaves about 253 MB of staging per run

- Sources and evidence: [analysis-001](analysis-001.md)
- Required behavior: a repeatable dev loop must not grow the real data directory without limit.
- Triage: blocking; clear fix.
- Action / assignment: A4. Switch removes its own stage directory after the outcome is final, on both success and failure. Add a test.
- Duplicate of: none
- Deferral: none
- User decision: not needed
- Verification: analyzer A6 and QA A5, identity `diff=8b494277fff6 untracked=14/fb1d5fbfff28`: stage removed on success and failure; guard sound. Verified.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | analyzer A3 | analysis-001 |
| 2026-10-28 | reported | assigned | orchestrator | A4 |
| 2026-10-28 | assigned | awaiting-verification | developer A4 | A4 handoff; identity `head=8a7508e baseline=8a7508e files=40 diff=8b494277fff6 untracked=14/fb1d5fbfff28` |
| 2026-10-28 | awaiting-verification | verified | orchestrator | analysis-002 and qa-002 |

### ANALYSIS-004: Skill rules contradict its fallback steps

- Sources and evidence: [analysis-001](analysis-001.md)
- Required behavior: explicit user authorization for each operation on the real installation.
- Triage: blocking; wording fix.
- Action / assignment: A4. On a refusal or failure the agent stops and reports. `worklog update --rollback` and manual recovery each need their own explicit authorization. Make the docs consistent with this, extend `DevBuildSkillTests`, and fix the overstated `-Workspace` claim.
- Duplicate of: none
- Deferral: none
- User decision: not needed
- Verification: analyzer A6, identity `diff=8b494277fff6 untracked=14/fb1d5fbfff28`. Verified.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | analyzer A3 | analysis-001 |
| 2026-10-28 | reported | assigned | orchestrator | A4 |
| 2026-10-28 | assigned | awaiting-verification | developer A4 | A4 handoff; identity `head=8a7508e baseline=8a7508e files=40 diff=8b494277fff6 untracked=14/fb1d5fbfff28` |
| 2026-10-28 | awaiting-verification | verified | orchestrator | analysis-002 |

### ANALYSIS-041: Clean-tree `git status` check misses deleted root inputs and `tsconfig` files

- Sources and evidence: [analysis-002](analysis-002.md)
- Required behavior: Preflight refuses stale packages. The docs imply that clean packages detect deleted inputs.
- Triage: blocking; clear two-line fix within scope. This is the second and last allowed fix-and-recheck cycle for this change.
- Action / assignment: A7. Pass the unfiltered input list to `git status`, using `:(glob)dashboard/tsconfig*.json`. Keep the existence filter only for the time walk. Add clean-marker tests for a deleted `README.md` and a deleted `dashboard/tsconfig.json`.
- Duplicate of: none (the residual gap of ANALYSIS-002)
- Deferral: none
- User decision: not needed
- Verification: analyzer A9 and QA A8, identity `head=8a7508e baseline=8a7508e files=40 diff=d12e556f180a untracked=14/40e6fbfee0b6`. Code, Git pathspec probes, and the discriminating test are in [analysis-003](analysis-003.md). Functional Preflight refusals for a deleted `README.md` and for each `dashboard/tsconfig*.json` are in [qa-003](qa-003.md). Verified.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | analyzer A6 | analysis-002 |
| 2026-10-28 | reported | assigned | orchestrator | A7 |
| 2026-10-28 | assigned | awaiting-verification | developer A7 | A7 handoff; identity `head=8a7508e baseline=8a7508e files=40 diff=d12e556f180a untracked=14/40e6fbfee0b6`; new tests fail without the fix (2 failed), pass with it |
| 2026-10-28 | awaiting-verification | verified | orchestrator | analysis-003 and qa-003 |

### ANALYSIS-042: Directory-time rule marks a package stale for excluded entries; the test hides it

- Sources and evidence: [analysis-002](analysis-002.md)
- Required behavior: none broken; the rule fails safe and costs one rebuild.
- Triage: non-blocking under the severity gate.
- Action / assignment: none
- Duplicate of: none
- Deferral: the follow-up cleanup change with ANALYSIS-005 – 008. Option (a) there: correct the docs bullet and the test. Reconsider if false positives are reported in practice.
- User decision: not needed
- Verification: n/a

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | analyzer A6 | analysis-002 |
| 2026-10-28 | reported | deferred | orchestrator | severity gate |

### ANALYSIS-061: A deleted, uncommitted `global.json` makes every dev package stale

- Sources and evidence: [analysis-003](analysis-003.md)
- Required behavior: none broken. Preflight refuses, which fails safe; restoring or committing the deletion clears it.
- Triage: non-blocking under the severity gate. It is a regression from the ANALYSIS-041 fix, and the change has used both of its fix-and-recheck cycles.
- Action / assignment: none
- Duplicate of: none
- Deferral: the follow-up cleanup change with ANALYSIS-005 – 008 and ANALYSIS-042. The proposed one-line fix is to add the root stale inputs as literal pathspecs to the dirty-flag inputs in `package.ps1`.
- User decision: not needed
- Verification: n/a

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | analyzer A9 | analysis-003 |
| 2026-10-28 | reported | deferred | orchestrator | severity gate |

### ANALYSIS-005 – ANALYSIS-008: suggestions

- Sources and evidence: [analysis-001](analysis-001.md)
- Required behavior: none beyond the existing acceptance.
- Triage: non-blocking under the severity gate.
- Action / assignment: none
- Duplicate of: none
- Deferral: a follow-up cleanup change after `add-dev-builds`. Reconsider if a later change touches `PayloadInstaller` construction, version helpers, `LocalSettings`, or `docs/architecture.md`.
- User decision: not needed
- Verification: pending

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | analyzer A3 | analysis-001 |
| 2026-10-28 | reported | deferred | orchestrator | severity gate |


## Completion assessment

- Current QA / analysis reports:
  - [qa-005](qa-005.md) and [analysis-004](analysis-004.md) cover the final identity `head=8a7508e baseline=8a7508e files=40 diff=dcb81d6747a4 untracked=14/40e6fbfee0b6`. That is a docs-only change from `diff=d12e556f180a`.
  - [qa-003](qa-003.md), [analysis-003](analysis-003.md), and [qa-004](qa-004.md) cover `diff=d12e556f180a`. They build on the earlier rounds ([qa-001](qa-001.md), [qa-002](qa-002.md), [analysis-001](analysis-001.md), [analysis-002](analysis-002.md)) for areas no later round changed.
- Required acceptance: all 11 tasks are satisfied with independent evidence.
  - Task 3.3 was independently reproduced in [qa-004](qa-004.md), using the user-approved method.
  - Task 5.1 is covered by the validation set in qa-001 through qa-003, plus the maintainer's interactive `package-update-fixtures.ps1` run (both stages passed), which is recorded in Scope decisions.
- Findings:
  - Blocking, all verified: QA-001, QA-002, ANALYSIS-001, ANALYSIS-002, ANALYSIS-003, ANALYSIS-004, ANALYSIS-041.
  - Dismissed: QA-003.
  - Deferred (non-blocking): ANALYSIS-005, ANALYSIS-006, ANALYSIS-007, ANALYSIS-008, ANALYSIS-042, ANALYSIS-061.
  - No unresolved blocking finding and no unapproved scope deferral.
- Nonblocking follow-ups: ANALYSIS-005 through 008, 042, and 061, plus the pre-existing output-path behavior of `package-update-fixtures.ps1`, go to a follow-up cleanup change.
- Evidence gaps / provider restrictions:
  - Claude Code 2.1.312 refused code-analyzer report writes four times. This is the documented known restriction; the orchestrator saved the reports.
  - The launcher CTRL+C step needs an interactive console. It passed in the maintainer's interactive run.
  - Task 3.3 used the product installer API, because `worklog update` has no local fixture endpoint (user decision).
  - No switch, rollback, or retire was run on the real installation. The manual `0.3.9001` payload is untouched.
- Commit: see below.
- OpenSpec task reconciliation: 11/11 verified.
