<!-- Saved by the orchestrator (Claude Code 2.1.316, session 47967d0e). Reviewer: code-analyzer subagent, assignment N2; client Claude Code 2.1.316, subagent mode, light review; model claude-sonnet-5-5 (recorded override: lock-file and docs-only target). Case: the client refused the reviewer's write of this report. Exact refusal message as returned by the reviewer: "Subagents should return findings as text, not write report files". The text below is the reviewer's returned report, saved verbatim. -->

# Analysis report: add-day-digests / analysis-008 (M1 lock-file refresh and task 5.3 records, light review)

## Review target

- Reviewer / role: assignment N2 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.316, subagent, light review (correctness and safety), claude-sonnet-5-5
- Date: 2026-11-02
- Change root / assignment: `openspec/changes/add-day-digests` / N2
- Task and requirement references: [tasks.md](../tasks.md) 5.3 and 5.4, [proposal.md](../proposal.md) Impact (Build bullet), [qa-008](qa-008.md) (QA-121, QA-122), [analysis-007](analysis-007.md) residual notes 1 and 2
- Scope and relevant neighboring paths: `src/WorkLog/packages.lock.json`, `src/WorkLog.Launcher/packages.lock.json`, `tools/WorkLog.LauncherProbe/packages.lock.json`; `docs/mcp-validation.md` ("Full validation set (task 5.3)"); `docs/dashboard-validation.md` ("Final validation (task 5.3)"). Neighbors: `global.json`, `.github/workflows/validate.yml`, `reports/findings.md` (ledger lines on the Git Bash note and M1).
- Reading limits and paths read outside scope: `reports/qa-009.md` not read, per the assignment. Outside the listed scope I also read `scripts/package.ps1` (grep for lock/restore only) and the local NuGet cache listing for `microsoft.net.illink.tasks` (package versions and `.nupkg.sha512` files only).
- Baseline commit: `5a73ab9`
- Implementation identity (before): `Summary: head=5a73ab9 baseline=5a73ab9 files=45 diff=386e43c851b1 untracked=9/32fc7d8f428a`
- Before/after identity: after the analysis the same command printed `head=5a73ab9 baseline=5a73ab9 files=45 diff=386e43c851b1 untracked=9/32fc7d8f428a`. It is identical to the expected value and to the "before" run. I saw no unexpected mutation, and no change from the concurrent QA run.
- Prior reports / rechecked findings: [analysis-007](analysis-007.md) residual notes 1 and 2 (no formal IDs). The [qa-008](qa-008.md) QA-121 and QA-122 are context only, not rechecked.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Lock-file minimality | `git diff 5a73ab9 -- <the three lock files>`; `git diff 5a73ab9 --stat -- '*packages.lock.json'` | 3 files, 9 insertions, 9 deletions. In each file only the `Microsoft.NET.ILLink.Tasks` entry changed: `requested` `[10.0.13, )` to `[10.0.14, )`, `resolved` 10.0.13 to 10.0.14, and the `contentHash`. No other package, target or dependency line changed. `git status` shows no other lock file modified, including `dashboard/package-lock.json`. | Minimal. The Launcher and LauncherProbe diffs are byte-identical, consistent with identical project shapes. |
| Consistency with the stated SDK | `dotnet --version` and `dotnet --list-sdks`; `global.json`; local NuGet cache | `global.json` is `{ "sdk": { "version": "10.0.113", "rollForward": "latestPatch" } }`. Installed SDKs are 9.0.320 and 10.0.114, so 10.0.114 is selected. The cache holds ILLink.Tasks 10.0.11 to 10.0.14, and the generated `obj/*.nuget.g.props` of the three projects import 10.0.14. This matches the lock files and the docs' "SDK 10.0.114" and "10.0.13 to 10.0.14". | Correct for SDK 10.0.114. The `requested` value is the SDK-supplied implicit reference. No `.csproj`, props or script I searched contains a `PackageReference` to ILLink, so the lock must follow whichever SDK patch resolves. |
| `contentHash` | Compared the lock `contentHash` with the cached `.nupkg.sha512` | The lock hash differs from the cached nupkg SHA-512 for both 10.0.13 (baseline) and 10.0.14, so the two values are different kinds of hash and cannot be compared this way. | Not independently verified by me. NuGet's locked restore checks the content hash, and the developer's record that restore passed is the evidence. I did not run restore (QA owns execution). |
| CI consistency | Read `.github/workflows/validate.yml` | `actions/setup-dotnet@v4` with `global-json-file: global.json`, `cache: true`, `cache-dependency-path: '**/packages.lock.json'`, then `dotnet restore --locked-mode`, `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-build`. | The workflow's restore step is the one that failed with NU1004 in qa-008, and it is the step the refresh targets. I did not observe the runner's actual SDK (see limitation below). |
| Task 5.3 records: coherence | Read both sections in full; compared figures | Both describe one run on 2026-11-02, on Windows 10.0.26300.0, with a fresh package at version 0.3.0. Figures agree: restore passed; Release build 0 warnings and 0 errors (mcp) and "clean" (dashboard); `dotnet test` 406 of 406; `npm test` 245 of 245 in 18 files; packaged e2e 39 of 39 with `WORKLOG_DASHBOARD_NO_NODE=true`; default isolated smoke passed; update fixtures `LAUNCHER PASSED` and `MIXED-VERSION PASSED` started from PowerShell. The dashboard section links to `mcp-validation.md#full-validation-set-task-53`, and that heading exists. | Coherent and in agreement. I did not re-read `Directory.Build.props` for the 0.3.0 figure; it is taken from the record. |
| Git Bash note attribution | `git grep -n -i "git bash" -- docs README.md` at the working tree and at `5a73ab9`; `reports/findings.md` lines 104 and 111 | The only remaining mention is `docs/mcp-validation.md:230`: "The Git Bash failure of the launcher fixtures noted in earlier rounds on 2026-10-29 was not rerun here." The ledger records the observation as F1/B4 on 2026-10-29. The old table row that presented it inside the 2026-11-02 run is gone. The diff of both docs against the baseline is additions only. | Attribution is correct and explicit. analysis-007 residual note 1 is resolved. The failure itself is no longer described anywhere in `docs/`; the reference points to the findings ledger, which I treat as acceptable. |
| Residual note 2 (restore and `npm ci` rows without a counterpart) | Compared the record with qa-008 context | The record now states a full set rerun in one run, including `dotnet restore --locked-mode` and `npm ci --prefix dashboard --no-audit --no-fund`, after the refresh. It names the refresh as the reason the restore passes. qa-008 had shown the earlier "passed" claim was false on the pre-refresh tree. I could not cross-check the rerun figures against QA output (reading limit). | The inaccurate-claim defect is resolved in the text, as far as I can verify. Whether the rerun produced these figures is for QA's report to establish. |
| Hygiene of the two records | Scan of both sections for session or reviewer IDs, report names, user or machine names, home paths, UUIDs, secrets | None found. The contents are a date, "Windows 10.0.26300.0", SDK version, commit SHA, relative paths, command lines and counts. The Windows build number appears in the older records too. | Clean. |
| Contradiction check against other statements | Read `docs/mcp-validation.md` 200-244 and the `docs/dashboard-validation.md` header and sections 214-268; grep for restore and lock statements | One stale statement, see ANALYSIS-121. Older sections correctly describe SDK 10.0.113 for their own dates and are not contradicted. Line 220 ("406 passed, 0 failed (final run of task 5.3, below)") agrees with the record. | One suggestion. |
| Task and proposal text | Read tasks 5.3 and 5.4 and the proposal Impact Build bullet | The proposal bullet says the three lock files are refreshed for the ILLink patch that the current SDK patch resolves, because the CI locked restore fails otherwise, and that the drift predates the change. Task 5.4 matches the diff (three named files, ILLink only). Both tasks are still `[ ]`. | Consistent. Check-boxing is orchestrator-owned. |
| Parity | Review of the change set | The files in this round are lock files and docs only. No integration, instruction or MCP contract file is touched. | Not applicable: no Claude Code or Codex behavior change in this round. |
| Performance, duplication, complexity | Not applicable | Lock files and Markdown only. | Not performed, no code in scope. |

## Steady-state cost and bounds

The target is two documentation sections and three lock files. It contains no timer, poll, cache, accumulating state or spawned tool.

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | none | not applicable | No long-running component in the target. |
| Cache lifetime versus interval and refresh duration | none | not applicable | No cache in the target. The CI `setup-dotnet` cache key is keyed on `**/packages.lock.json`, so the refresh changes that key once; this is expected. |
| Unbounded state | none | not applicable | No state in the target. |
| Repeated full reads | none | not applicable | No requests in the target. |
| External tool discovery | `dotnet` SDK resolution via `global.json` | source observation | Resolution depends on the installed SDK set and `rollForward: latestPatch`. No timeout concerns for this target. See the limitation below. |

## Findings

### ANALYSIS-121: A pre-refresh "locked-restore Release build" claim remains in the day-digest smoke row

- Severity: suggestion
- Related task / requirement: 5.3 (records), QA-121 context
- Locations: `docs/mcp-validation.md:217` at the reviewed identity (the row beginning "`dotnet run --project tools/WorkLog.Smoke ... (default isolated run, no live agents) with a locked-restore Release build and ./scripts/package.ps1 output`")
- Observation: The row is undated and describes an earlier round. qa-008 showed that `dotnet restore --locked-mode` failed on the pre-refresh tree under the selected SDK. The task 5.3 record now says the restore passes because of the refresh. The row's "locked-restore Release build" is a claim about an earlier run that the same document elsewhere treats as one that could not have passed on the current SDK. I could not establish which SDK or tree that earlier run used. I do not claim the sentence is false, only that it is unsupported and sits in tension with the new record.
- Proposed improvement and benefit: Either point the row at the final run ("with the Release build and package of the final run below") or drop the phrase "locked-restore". The smoke result itself does not depend on the lock files. Benefit: the document stops asserting a locked restore that the evidence trail (qa-008) contradicts for that tree.
- Behavior-preservation risks: none (documentation only).
- Evidence type: source observation
- Verification / measurement plan: re-read the row after the edit; no execution needed.
- Relationship: related to QA-121 and analysis-006 ANALYSIS-101 (same family of record-attribution inexactness); not a duplicate.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| analysis-007 residual note 1 (Git Bash wording) | `head=5a73ab9 diff=386e43c851b1` | `docs/mcp-validation.md:230` now dates the note to "earlier rounds on 2026-10-29" and states it was not rerun. The old row in the 2026-11-02 table is gone. Ledger line 104 supports the attribution. | verified (a no-ID note; status is the orchestrator's) |
| analysis-007 residual note 2 (restore and `npm ci` rows) | same | The rows are now part of a stated single run after the lock refresh, and the earlier false "passed" claim is replaced. The rerun figures were not cross-checked against QA output (reading limit). | resolved in text; run evidence is QA's to confirm |

## Assessment and limitations

Result: one suggestion (ANALYSIS-121). The lock-file change is minimal and correct for the SDK selected here (10.0.114). Only `Microsoft.NET.ILLink.Tasks` changed, from 10.0.13 to 10.0.14, in the three files. The two task 5.3 records describe one coherent run and agree with each other. They attribute the Git Bash note to the 2026-10-29 rounds and contain no reviewer or session IDs or machine-private data.

Limitation, not a measurement: the fix is sound only while the SDK that restores the repository resolves ILLink.Tasks 10.0.14. That version comes from the SDK, not from the project files. `global.json` allows any 10.0.1xx patch at or above 10.0.113 (`latestPatch`), while the lock file now pins one value. I did not observe which SDK patch the `windows-latest` runner or any other machine selects, and I did not run CI. `actions/setup-dotnet` with `global-json-file` is commonly understood to install the version named in the file (10.0.113), and the runner image may also carry other 10.0.1xx patches. I did not check which of these `latestPatch` picks on the runner. If the runner resolves a patch whose bundled ILLink.Tasks differs from 10.0.14, `dotnet restore --locked-mode` would fail again with NU1004, in the opposite direction from what qa-008 saw before the refresh. I did not establish which ILLink.Tasks version SDK 10.0.113 bundles.

So the refresh fixes the drift between this machine and the lock files. It does not remove the underlying coupling between SDK patch and lock content. A durable remedy is not required by the acceptance and is an orchestrator decision: raise the `global.json` version to the patch that supplies 10.0.14 and, if exact reproducibility matters, tighten the roll-forward policy. I did not evaluate that option beyond naming it. The first CI run after merge is the real test.

I did not independently verify the 10.0.14 `contentHash` (see the table). The developer's record that the locked restore passes is the supporting evidence.

Not performed: build, tests, packaging, restore and live agents (as assigned); a read of `reports/qa-009.md` (reading limit); cross-check of the rerun figures against QA output. No files were written (the report write was refused). The review identity was identical before and after. The orchestrator owns triage and ledger status, including the unchecked boxes of tasks 5.3 and 5.4.
