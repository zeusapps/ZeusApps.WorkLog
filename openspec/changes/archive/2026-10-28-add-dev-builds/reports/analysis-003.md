> Saved by the orchestrator, verbatim from the reviewer's returned text.
> Reviewer: A9 code-analyzer; client Claude Code 2.1.312, subagent in auto mode; model claude-opus-5-5.
> Case: the client refused the reviewer's write with this message: "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: add-dev-builds / A9 recheck (fix round 2)

> Saved by the orchestrator. The analyzer's write was refused with: "Subagents should return findings as text, not write report files. Include this content in your final response instead."

## Review target

- Reviewer / role: code-analyzer subagent (A9) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312, subagent with role-default model, auto mode / `claude-opus-5-5`
- Date: 2026-10-28T10:56:15Z
- Change root / assignment: `openspec/changes/add-dev-builds` / A9 recheck analysis (ANALYSIS-041 plus regression from fix round 2 (A7); new IDs ANALYSIS-061 – ANALYSIS-080)
- Task and requirement references:
  - Task 3.1 ([tasks.md](../tasks.md)).
  - [Spec delta](../specs/application-distribution/spec.md) "Development builds": Preflight refuses stale packages.
  - [findings.md](findings.md) entry ANALYSIS-041.
- Scope and relevant neighboring paths:
  - Changed in A7: `tools/WorkLog.DevInstall/Staleness.cs`, `tests/WorkLog.Tests/Updates/StalenessTests.cs`, `docs/releases.md` (stale-rule bullet).
  - Neighbors read for the false-positive question: `scripts/package.ps1:50-57` (dirty flag), `scripts/package-core.ps1:20-31` (README and INSTALL copy), `src/WorkLog/Repositories/GitRepository.cs:23-52` (`Run`, argument passing), `global.json`, `.gitignore`, `.editorconfig`.
- Reading limits and paths read outside scope:
  - Outside the 33 identity paths: `src/WorkLog/Repositories/GitRepository.cs`, `global.json`, `.gitignore`, `.editorconfig`.
  - Reports: [analysis-002](analysis-002.md) and [findings.md](findings.md).
- Baseline commit: `8a7508e`
- Implementation identity: `./scripts/review-identity.ps1 -Baseline 8a7508e -Paths <the 33 assigned path arguments>`, run before the analysis:
  - HEAD `8a7508e6cd348d9ffd6ecda77ddfd9080346baab`.
  - Manifest: 14 untracked and 26 modified files.
  - Tracked diff SHA-256 `d12e556f180ae7c8c6bd485d89dd0e0bba90264074eb0e0e535711fb8ac4fd62`.
  - File hashes: `Staleness.cs` `35f0482002b4…`, `StalenessTests.cs` `eef5a2357828…`.
  - Summary: `head=8a7508e baseline=8a7508e files=40 diff=d12e556f180a untracked=14/40e6fbfee0b6`. This matches the expected value.
- Before/after identity: after the analysis the summary was `head=8a7508e baseline=8a7508e files=40 diff=d12e556f180a untracked=14/40e6fbfee0b6`. It is identical, and no target file changed.
- Prior reports / rechecked findings: [analysis-002](analysis-002.md): ANALYSIS-041.
  - ANALYSIS-042 and ANALYSIS-005 – 008 stay deferred. The fix did not make them worse: `NewestSource` and `Walk` are unchanged.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-041: the fix | Read `Staleness.cs:22-43` | `InputPaths` now returns all of `SourceFiles` and `SourceDirectories` unfiltered, then `:(glob)dashboard/tsconfig*.json`, then the unchanged exclude pathspecs (`:(exclude,glob)**/*.spec.ts` and `:(exclude,glob)**/<name>/**` for each excluded directory). `NewestSource` (`:45-62`) keeps the existence filter and the disk listing. The `repository` parameter of `InputPaths` is now unused, which is harmless | Matches the proposal in analysis-002 |
| Pathspec semantics | Probes with Git 2.51.0 (Windows) in a disposable repository under `artifacts/analysis-003-probes/`, removed afterwards. Arguments were passed as a PowerShell argument array, which does not expand wildcards. This is equivalent to `ProcessStartInfo.ArgumentList` in `GitRepository.Run`. The command was `git status --porcelain --untracked-files=normal -- <InputPaths>` with the exact list from `Staleness.cs` | P0 clean tree → empty. P1 deleted `README.md` → ` D README.md`. P2 deleted `dashboard/tsconfig.app.json` → ` D dashboard/tsconfig.app.json`. P3 untracked `dashboard/tsconfig.extra.json` → `??` reported. P4 `dashboard/sub/tsconfig.json` → not reported (with glob magic `*` does not cross `/`, which matches the top-level-only disk listing). P5 an edited tracked spec file plus a new untracked spec file → not reported. P6 untracked `src/WorkLog/obj/` and `dashboard/src/e2e/` with no `.gitignore` → not reported. P7 the whole `dashboard/scripts` deleted → ` D dashboard/scripts/stamp.mjs`. P8 deleted `global.json` → ` D global.json`. P9 edited `dashboard/tsconfig.json` → ` M`. P10 a listed file absent from both HEAD and disk → empty, exit 0. P11 the glob pathspec matches nothing → empty, exit 0 | Each pathspec carries its own magic. The `glob` positive pathspec and the plain positive pathspecs combine as a union, and the exclude pathspecs subtract from all of them, including the glob one. No combination errors. Pathspecs that match nothing exit 0 |
| New false negatives | Compared the new pathspec list with the A4 list described in analysis-002 | The new list is a superset of the old one: the same files and directories, now unfiltered, plus a glob that covers every top-level `tsconfig*.json` the disk listing could return. The exclusions are unchanged, and `NewestSource` is unchanged. P7 shows the fix also catches a deleted input directory (`src`, `dashboard/src`, `dashboard/scripts`), which the existence filter used to drop. Theoretical edge: on Windows the disk listing matches case-insensitively, while the pathspec glob matches case-sensitively, so a file named `TSConfig.json` would be missed. Angular does not read such a name | No new false negatives |
| New permanent false positives | Reproduced the `package.ps1:55-56` dirty computation verbatim in a second disposable repository (`artifacts/analysis-003-probes2/`, removed afterwards) after deleting `global.json` | `package.ps1` builds its root-file pathspecs from a disk listing (`Get-ChildItem -File`), so it never names a deleted root file: `dirty = False`. The Staleness pathspec names `global.json` and reports ` D global.json`. Each rebuild produces the same clean marker, so every Preflight refuses until the deletion is restored or committed. `README.md` and `Directory.Build.props` behave the same in Git, but their deletion already fails packaging (`package-core.ps1:27` copies `README.md` under `ErrorActionPreference = 'Stop'`, and `package.ps1:17` reads the version from `Directory.Build.props`). Inputs under `docs/` and `dashboard/` are inside directory pathspecs of `package.ps1`, so they are not affected | ANALYSIS-061 (suggestion, fails safe). The analysis-002 statement "the git status check covers a subset of the inputs that decide dirty" no longer holds for deleted root files |
| Test | Read `StalenessTests.cs:58-71` | `ADeletedListedInputMakesACleanPackageStale` covers `README.md` and `dashboard/tsconfig.json`. It asserts that the fixture file exists and deletes it. `AgeEverything()` then neutralizes the directory times, and the test expects the "uncommitted changes" message. Under the A4 code both cases yield `null`: the file is filtered out and the disk listing is empty. This matches the developer's "2 failed without the fix" | Discriminating test, as recommended in analysis-002 |
| Docs and summary claims | Read `Staleness.cs:6-13,22` and `docs/releases.md:40` | Clean-package claims are now true: `git status` "catches timestamp-preserving copies and deleted listed files such as README.md" (P1, P2, P8). "The Git check lists every input whether or not it still exists" is true. The dirty-package limitation is worded as "an individually listed root file" (docs) and "such a deletion" (summary). The limitation is slightly wider: dirty packages also miss deleted `dashboard/*` config files, `docs/installation.md` and whole input directories, because neither the repository root nor `dashboard/` is walked. The summary's "individually-listed file (README.md and similar)" covers most of these. The docs' "root" is imprecise wording without a contract effect. The analysis-002 wording notes (the `node_modules` cause and the split sentence "build it again, checks the contracts…") are unchanged | Accurate for the claims made. No finding for the wording. With ANALYSIS-061, "There is no override for a stale build: build again" does not resolve the `global.json` case |
| Integration parity | Scope of A7 | A7 touched no file under `Integration/`, `Clients/` or `Mcp/`, and neither skill copy (both hashes unchanged at `0e57f334b294…`) | No parity effect for Claude Code or Codex |
| Formatting | `dotnet format ZeusApps.WorkLog.slnx --no-restore --verify-no-changes --include tools/WorkLog.DevInstall/Staleness.cs tests/WorkLog.Tests/Updates/StalenessTests.cs` (verify-only) | Only `ENDOFLINE` diagnostics, one per line of `Staleness.cs` (76). Every new untracked `.cs` file in the change is uniformly CRLF in the working tree (`core.autocrlf=true`; tracked files are `w/lf`), so this is a property of the whole file, not of the A7 lines. `autocrlf` normalizes the files to LF when they are staged | No other diagnostics. Not an A7 regression |
| Tests | Not run | — | QA A8 owns the functional checks. `StalenessTests` writes under `artifacts/staleness-tests/`, outside the probe paths assigned to this analysis |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | `Staleness.Check`, run once per Preflight and once per Switch | Source observation | No timers or polling. A7 adds no process launches: it is still one `git rev-parse`, plus one `git status` for a clean marker. The pathspec list grows by at most a few entries. Analysis-002 measured 29-41 ms for this; it is not remeasured |
| Cache lifetime versus interval and refresh duration | None | Source observation | Not applicable: the check is computed fresh for each operator action, and correctness requires that |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | None added by A7 | Source observation | Not applicable: A7 adds no persistent state |
| Repeated full reads where a change check would do | `git status` over the inputs | Source observation | `git status` is the change check. A7 removes per-call existence probes and a directory listing from `InputPaths` |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | `git` via `GitRepository.Run` | Source observation | Unchanged: `git` is found on PATH and the call has no timeout (existing helper, noted in analysis-001 and analysis-002). The glob pathspec reaches Git verbatim through `ArgumentList`, and Git for Windows does not expand wildcards in its arguments. The new test exercises this path |

## Findings

### ANALYSIS-061: A deleted, uncommitted `global.json` makes every dev package permanently stale

- Severity: suggestion. The check fails safe, the case is narrow, and the refusal message points at `git status`.
- Related task / requirement: 3.1; "Development builds" stale refusal; regression from the ANALYSIS-041 fix.
- Locations:
  - `tools/WorkLog.DevInstall/Staleness.cs:25`: the Git pathspecs now name `global.json` (and the other root files) even when they are deleted.
  - `scripts/package.ps1:55`: the dirty flag takes its root-file pathspecs from a disk listing, so it never names a deleted root file.
  - `docs/releases.md:40`: "There is no override for a stale build: build again."
- Observation:
  - Measured with the verbatim `package.ps1` computation. After `global.json` was deleted without a commit, `package.ps1` marked the build clean (`dirty = False`), while the Staleness `git status` reported ` D global.json`.
  - Preflight therefore refuses with "build inputs have uncommitted changes". Rebuilding produces the same clean marker, so the refusal repeats until the file is restored or the deletion is committed.
  - `README.md` and `Directory.Build.props` have the same mismatch in Git, but deleting either already makes packaging fail. `global.json` is the only root input whose deletion still packages: its SDK pin is optional for the build.
  - Before A7 the existence filter hid this case. The dirty-flag inputs were then a superset of the Git-check inputs; for deleted root files they no longer are.
- Proposed improvement and benefit:
  - In `package.ps1:55`, add the root files from the stale list (`Directory.Build.props`, `global.json`, `README.md`) to `$inputs` as literal pathspecs. Duplicates and absent paths are harmless (probes P10 and P11).
  - Benefit: a deleted root input then marks the build dirty, which restores the invariant that a clean marker means `git status` over the stale inputs was empty at build time.
  - Alternative: leave the code as is, and state in `docs/releases.md:40` that a deleted, uncommitted `global.json` must be restored or committed before a dev build can be installed.
- Behavior-preservation risks:
  - The dirty flag becomes true in one more case, a deleted tracked root file. That only changes the label and skips the Git check.
  - Stable packaging is unaffected (the `-Dev` path only).
- Evidence type: measured result (disposable repository, verbatim dirty computation) plus source observation.
- Verification / measurement plan: in a disposable repository, delete `global.json` and run `package.ps1 -Dev -PlanOnly`. Expect `build.dirty = true`, and expect `Staleness.Check` with that marker to return `null` (no other changes).
- Relationship: new, caused by the ANALYSIS-041 fix. Not related to ANALYSIS-042, which concerns the directory-time rule.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-041 | `head=8a7508e baseline=8a7508e files=40 diff=d12e556f180a untracked=14/40e6fbfee0b6` | Code: `Staleness.cs:25` passes the unfiltered `SourceFiles` and `SourceDirectories` plus `:(glob)dashboard/tsconfig*.json`, and keeps the exclusions. Git probes with the exact pathspec list: a deleted `README.md`, a deleted `dashboard/tsconfig.app.json`, a deleted `global.json` and a deleted `dashboard/scripts` are all reported. Spec files and excluded folders stay out. Pathspecs that match nothing exit 0. Test: the `ADeletedListedInputMakesACleanPackageStale` theory (`README.md`, `dashboard/tsconfig.json`) discriminates the fix. Docs: the clean-package claims in `docs/releases.md:40` and `Staleness.cs:6-13` now hold. Regression: ANALYSIS-061, a narrow false positive that fails safe | verified; regression ANALYSIS-061 (suggestion) |

## Assessment and limitations

- Scope covered: ANALYSIS-041 and its regression surface (the three files A7 changed and the `package.ps1` dirty set they interact with).
- ANALYSIS-041: the fix is correct.
  - Git mixes per-pathspec `glob` magic with plain and `exclude,glob` pathspecs as intended.
  - No new false negatives were found.
  - Docs and summary claims about clean packages are accurate.
- One regression, ANALYSIS-061: a permanent but fail-safe false positive for a deleted, uncommitted `global.json`. It needs a one-line `package.ps1` fix or a docs note. The orchestrator owns triage. A7 was described as the final allowed fix cycle, so deferral alongside ANALYSIS-042 is reasonable given the severity.
- ANALYSIS-042 and ANALYSIS-005 – 008 are not worsened.
- Not performed:
  - Unit tests (QA A8 owns them; also outside the assigned probe paths).
  - A timing remeasure (A7 adds no process launches).
- Writes:
  - The probe repositories `artifacts/analysis-003-probes` and `artifacts/analysis-003-probes2` were created and removed.
  - `dotnet format --verify-no-changes` may have refreshed ignored `obj` outputs.
  - No target file changed (identity unchanged).
- Not touched: the installation, the real data directory and port 47831.
