# Analysis report: realtime-dashboard-updates / 016

## Review target

- Reviewer / role: bounded ORCH-003 platform investigation / code-analyzer.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime model introspection unavailable.
- Date: 2026-11-09.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / investigation 016.
- Task and requirement references: [tasks 2.1, 3.2, 4.3 and 7.4](../tasks.md); ORCH-003 archive move with active document watchers and contained source access.
- Scope: original stable `src/WorkLog/Dashboard` and `tests/WorkLog.Tests/Dashboard`; disposable watcher evidence only. This is not a review of a future topology implementation.
- Neighboring reads / limits: existing role/project facts/template, `resume-20261109/archive-watcher-topology.ps1` and JSON, existing disposable junction metadata, original watcher/coordinator source and source-link test helper. Current QA reports/evidence were not read. No Context7 request, HTTP host, port, shared assembly build, real data or installation access.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard`.
- Manifest and hashes: same ten-path manifest and six individual untracked hashes recorded in [analysis 012](analysis-012.md). Tracked diff SHA-256 `0a9a5cbc84589f41c0e5d735310de10495f6f315af47c5a1f3d57e7e707d9aa2`; untracked aggregate `5fde9cd34da9`.
- Before/after identity: both match `head=0d77685 baseline=0d77685 files=10 diff=0a9a5cbc8458 untracked=6/5fde9cd34da9`. No scoped mutation during this investigation.
- Finding allocation: ORCH-003 investigation only; ANALYSIS-340 through ANALYSIS-359 unused, no new suggestions.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Original archive topology | Inspect existing `resume-20261109/archive-watcher-topology.json` and producer | No watchers and a change-root-only watcher permit the fixture ancestor move; a watcher on nested `specs/capability`, alone or alongside the change-root watcher, fails with `IOException` / `0x80070005` | Passed as inspection of earlier executed evidence. Not independently rerun here |
| Initial fresh-junction attempt | Own `reviews/analysis-016/recursive-watch-probe.ps1` | `New-Item -ItemType Junction` returned Access is denied before watcher/C# execution | Not performed on that fixture. Reported immediately; no further approval request or privileged creation attempted |
| Actual bounded recursive-watch probe | `./artifacts/realtime-dashboard-updates/reviews/analysis-016/reuse-junction-probe.ps1` | In-process minimal C# `FileSystemWatcher` rooted at the existing disposable `recursive-link-check/changes`, `IncludeSubdirectories=true`; existing `linked-outside` junction targets sibling `outside`, outside approved changes/source but still disposable artifacts | Passed. Watcher observed two positive-control events, Created and Changed, for a new deeply nested contained Markdown sentinel |
| Outside-junction target writes | Same watcher after draining the contained control events; unique direct-target and junction-alias sentinels | Five mutation operations: direct create, direct append, creation through the junction alias, temporary-file create and atomic replacement. Observed zero events after a bounded 1,500ms window following the final operation; zero watcher errors | Passed as this Windows fixture observation. It does not establish universal platform behavior or root/link-swap safety |
| Probe ownership and cleanup | Watcher disposed by C# `using`; PowerShell `finally` deletes only newly created uniquely named sentinels | Orchestrator explicitly extended write ownership to those unique sentinel files within the existing fixture. Existing directories and junction were neither moved nor deleted; no unrelated file overwritten | Passed. Raw source/results remain ignored under `reviews/analysis-016`; no product or shared build output changed |
| Minimal physical-root coalescing | Inspect current logical roots, coordinator `Classify`, suspect-path and recovery-context routing | Current logical identities/domain masks feed authoritative classification and recovery. A physical recursive ancestor subscription can replace descendant OS handles while keeping these logical entries for routing | Source proposal only. Developer owns implementation and its regression tests; not an approval of a patch |
| Source stability / own-report hygiene | Before/after backend identity; category-only own-report scan | Target unchanged; report contains no personal/machine/credential values or unintended residue | Passed; broad concurrent change-root scanning deferred |

Raw measured results: `artifacts/realtime-dashboard-updates/reviews/analysis-016/reuse-junction-results.json`. The positive control validates that this actual recursive watcher delivered nested events during the observation. Zero outside events is a bounded negative observation, not proof that the operating system can replace application containment checks.

The minimal implementation constraints communicated to the developer are:

- Keep logical approved roots, root IDs, domain masks and recovery-context mappings even when fewer physical watcher handles are used. Project event paths back through current logical roots so document hints still resolve and journal/digest/read/authorization classifications remain precise.
- Retain `SourcePath` validation before initial/rebound traversal, before body reads and when resolving current event targets. Preserve root/link-swap rejection and contained recovery behavior; the initial-junction probe does not cover those races.
- Choose recursive physical ancestors within an approved document subtree. Do not indiscriminately make broad catalog, repository-parent or data-directory discovery roots recursive and thereby subscribe to unrelated neighboring trees.
- Continue bounded watcher/hint limits, overflow/full-reconcile behavior and disposal. Archive, missing-root recovery, deep nested edits and outside links need developer regression evidence on the actual patch.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Disposable native watcher probe | Measured fixture / source proposal | One recursive watcher, five outside mutation operations, two contained control events and a 1.5s final observation window; no polling HTTP host or product benchmark. Future handle reduction is a proposal, not a measured CPU improvement |
| Cache lifetime versus interval and refresh duration | Unchanged coordinator/catalog | Not applicable to probe | No cache, reconciliation interval or source freshness setting changed |
| Unbounded state | Probe queues and proposed physical/logical split | Source observation | Short self-bound probe queues only; existing product bounds must remain in the future patch. This investigation adds no product state |
| Repeated full reads | Probe sentinels / original source | Source observation | Only disposable sentinel file mutations and small evidence reads. No new product traversal or content read introduced |
| External tool discovery / timeouts | In-process C# compilation and FileSystemWatcher | Measured fixture | PowerShell Add-Type compiles a standalone in-process probe without shared binary builds. Existing junction reused; no new link privilege or process-discovery mechanism relied upon |

## Findings

**No new findings** allocated. ORCH-003 remains an implementation issue: the existing nested-handle topology blocks the ancestor archive move in the supplied fixture evidence. This investigation provides bounded native recursive-watch evidence to inform its correction, not a completed product fix.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ORCH-003 | Original backend target unchanged; no correction reviewed | Existing nested-watcher archive failure inspected; actual recursive approved-root probe delivers contained nested events and no outside-junction-write events in the stated window | Investigation complete. Developer topology correction and actual archive/containment regression acceptance remain required |

## Assessment and limitations

On this Windows disposable fixture, a recursive watcher at the approved changes root observed the contained nested positive control but did not emit events for writes through or directly to an outside junction target. This supports investigating physical ancestor-root coalescing while retaining logical routing and application containment checks.

Only the reserved report, ignored probe/evidence files and explicitly authorized unique disposable sentinels were written; sentinels were deleted after watcher disposal. No existing fixture/link, implementation, tests, ledger, tasks or prior report was changed. Backend identity remained the original stable target. Future topology edits need their own stable review; no whole-feature, cross-platform, root-swap or final archive acceptance is claimed here.
