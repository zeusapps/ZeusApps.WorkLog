# Analysis report: realtime-dashboard-updates / 012

## Review target

- Reviewer / role: independent backend recheck / code-analyzer.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime-resolved model introspection unavailable.
- Date: 2026-11-09T08:31:18Z.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-012.
- Task and requirement references: [tasks 2.1, 2.2, 2.4 and 7.4](../tasks.md), [revision and watcher design](../design.md); existing QA-180 plus regression of ANALYSIS-043 and checkpoint metadata.
- Scope: `src/WorkLog/Dashboard` and `tests/WorkLog.Tests/Dashboard`. Concurrent browser and acceptance changes are excluded.
- Reading limits / neighboring reads: read scoped implementation/tests, role/project facts, report template, [QA 008](qa-008.md), [analysis 008](analysis-008.md), the architecture's live-update paragraph, and the supplied final TRX. Change-root text was scanned for hygiene. The concurrent `qa-012.md` was excluded from that scan and was not read. No implementation, tracking, prior-report or runtime-evidence writes were permitted or made.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard`.
- Tracked manifest: modified `src/WorkLog/Dashboard/DashboardHost.cs`, `DocumentService.cs`, `SourceCatalog.cs` and `tests/WorkLog.Tests/Dashboard/SourceCatalogTests.cs`.
- Tracked diff SHA-256: `0a9a5cbc84589f41c0e5d735310de10495f6f315af47c5a1f3d57e7e707d9aa2`.

| Untracked file | SHA-256 |
| --- | --- |
| `src/WorkLog/Dashboard/DashboardChangeSource.cs` | `8241ae398e0136be338d053849d278cab4b844a18d55cf503c67e0d1589ae8a3` |
| `src/WorkLog/Dashboard/DashboardLiveConnections.cs` | `89317ca104c88bbf54df33d3467982b7d38f303b44a02be2c0ff0ce2a564185e` |
| `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs` | `aae58db0247a02dfe8504e4af1be5322d944c5108f9ad7ce8dac867f670f2970` |
| `tests/WorkLog.Tests/Dashboard/DashboardChangeSourceTests.cs` | `aae4810c49773abe0dc5fcc5521841b8da0dd0cf48b265a2151e37c84435f1ab` |
| `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs` | `24bb8380caed98bd1f873bb444962d743bac30d1c04313a242690de0f2c07871` |
| `tests/WorkLog.Tests/Dashboard/ManualTimeProvider.cs` | `80f730379a27edfd7cc8d19d6d15123b6a0f52dac9746e89859440bf3b6b5c45` |

- Before/after identity: both match `head=0d77685 baseline=0d77685 files=10 diff=0a9a5cbc8458 untracked=6/5fde9cd34da9`; no scoped mutation detected.
- Prior reports / finding allocation: [QA 008](qa-008.md) defines QA-180; [analysis 008](analysis-008.md) covers the preceding ANALYSIS-043 correction. Existing QA-180 rechecked; reserved ANALYSIS-260 through ANALYSIS-279 unused. No suggestions allocated.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Settled-state publication order | Trace `ReconcileCatalogAsync`, `RebindWatchersAsync` and `CurrentSnapshot` under their existing locks | `DashboardLiveCoordinator.cs:462-470` refreshes settings and awaits rebinding before committing stamps, advancing domain revisions and clearing the catalog/document dirty bits. `RebindWatchersAsync` awaits the change-source rebind at line 775 before replacing the diagnostic root maps at lines 776-781 | Passed at source level. This removes the original clean-revision-before-root-rebind window |
| Error and cancellation behavior | Trace exits before the clean-state commit | Recoverable exceptions reach the existing catch at lines 474-476 and preserve requested catalog/document dirty bits; host cancellation propagates before the commit | Passed at source level. No injected throwing-rebind experiment was run |
| Regression fixture fidelity | Inspect the original restored-root assertion and the adjusted missed-hint socket receive ordering | `DashboardLiveCoordinatorTests.cs:319-346` still waits for an advanced document revision plus empty dirty state, then immediately checks the restored child root. Its assertion and timeout were not weakened. Lines 488-506 start the existing checkpoint receive before advancing either manual tick | Passed by inspection. Continuous draining avoids a fake-clock send deadline during the full-suite schedule without changing the required replacement/vector assertions |
| Supplied execution evidence | Parse the existing final developer-run TRX independently | `artifacts/realtime-dashboard-updates/resume-backend-checks/DEV-016-full-final.trx`: 452 total/executed/passed, zero failed or not executed. The restored-root, no-hint replacement and derived-identity-cache cases each passed. Category-safe extracted summary: `artifacts/realtime-dashboard-updates/reviews/analysis-012/supplied-trx-summary.json` | Passed as inspection of supplied execution. This reviewer did not rerun tests or build. The ledger's `resume-full-checks/DEV-016-full-final.trx` reference is stale; the artifact is in `resume-backend-checks` |
| ANALYSIS-043 regression | Inspect validated typed journal-marker comparison, derived catalog invalidation and bounded follow-up | `ReconcileJournalAsync` and `JournalRepositoryIdentityChanged` retain the no-lifecycle-hint same-rowid replacement path reviewed in 008. The exact missed-hint fixture and derived-cache fixture passed in the supplied final TRX | Passed for the original source mechanism and supplied unit/fixture case; no claim about replacements indistinguishable by all observed metadata |
| Checkpoint metadata regression | Inspect `DashboardRevisionVector`, `Publish`, `CurrentSnapshot` and the fixture assertions | Optional checkpoint sequence remains added only by `Publish(checkpoint: true)` to a copied vector; ordinary/Snapshot payloads omit null metadata. The missed-hint fixture receives an advanced real checkpoint after the replacement | Passed at source and supplied fixture level; full browser recovery timing remains separate |
| Duplication / complexity / architecture | Compare the bounded ordering change with the live-update architecture and existing single catalog lane | The correction reorders existing awaited work and one atomic commit; it adds no new abstraction, polling owner, queue, client-specific policy or contract | Passed within this focused recheck; no unrelated suggestions |
| Whitespace and target stability | `git diff --check -- src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard`; identity command before/after | No whitespace errors; exact target identity unchanged | Passed |
| Committable hygiene / residue | Category-only scan of scoped text and committable change-root Markdown, excluding concurrent QA 012; inspect flagged categories | No personal/machine values or unintended residue identified. Broad credential-pattern hits were ordinary compound-word text in historical reports and source expressions constructing the existing launch fragment; no stored credential value. Own report checked separately | Passed. No secret or personal value is quoted in this report; raw evidence stays ignored |
| Build, independent test execution, host and packaged acceptance | Deliberately not run under the assigned no-host/no-build scope | The other developer owns the shared build/host window | Not performed; supplied TRX inspection is not independent execution |

The supplied test durations are fixture execution durations, not real recovery or freshness benchmarks. No server process, browser, native agent, real installation or personal data directory was used by this review.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Existing five-second server reconciliation and two-second connection authorization loop | Source observation | QA-180 only moves existing settings/watch work before the revision commit. It adds no timer, HTTP request, filesystem traversal or journal query. Existing checkpoint work still schedules the independent catalog/journal/digest/read lanes. No new CPU or I/O measurement was made |
| Cache lifetime versus interval and refresh duration | Catalog snapshot, expensive discovery tier and derived repository-name cache | Source observation | Cache policies and source discovery cadence are unchanged by the ordering fix. A catalog revision stays pending until its already-required watcher rebind finishes; the independent lanes remain separate. No claim that final steady-state work is lower than baseline |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Pending source hints, lane batches, watch maps and connection slots | Source observation | Existing hint promotion at 128, one active/one pending batch per lane, a 512-watcher cap and one queued vector per connection remain. The correction adds no retained state or event history. It replaces the watcher maps after rebinding rather than accumulating them |
| Repeated full reads where a change check would do | Catalog reconciliation and journal replacement detection | Source observation | No extra full read is introduced. Validated journal identity change still invalidates only derived repository discovery and queues bounded catalog work; unchanged journal markers do not add that follow-up |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Existing source discovery dependencies | Source observation | Executable lookup, supported install methods and command timeouts are unchanged by this fix. They were not executed or revalidated here; final normal/offline package acceptance remains required |

## Findings

**No findings** within the assigned QA-180 recheck and regression scope. No new regression ID or optional suggestion is allocated.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| QA-180 | Backend identity `files=10 diff=0a9a5cbc8458 untracked=6/5fde9cd34da9` | Source order now awaits settings/watch rebinding before stamps/revisions/clean state; recovery catch retains requested dirty bits. The unchanged restored-root assertion passes in the supplied final 452-pass TRX | Correction verified at source level and supported by supplied executed fixture; no independent rerun |
| ANALYSIS-043 regression | Same backend identity | No-hint same-rowid replacement still reaches derived-cache invalidation; final TRX records the exact detector/real-WebSocket fixture and isolated cache case passing | Prior correction retained within its original source/fixture boundary |
| Checkpoint metadata regression | Same backend identity | Checkpoint-only optional sequence and ordinary/Snapshot omission remain intact; fixture receives advanced catalog/journal vector | Passed by source inspection and supplied execution evidence; no full fault-matrix claim |

## Assessment and limitations

The original QA-180 publication-order defect is corrected. Root binding now settles before the corresponding catalog/document revisions are exposed as clean, and the unchanged regression assertion passed in the supplied full-suite result. The prior missed-hint replacement and checkpoint metadata mechanisms remain coherent on this target.

Only the reserved report and ignored review evidence were written. Before/after implementation identities match. This reviewer did not build, rerun tests, start a host or browser, measure runtime cost, or perform packaged/offline/MCP/native-client acceptance. The backend is client-neutral; no Claude Code or Codex MCP/instruction contract changes are involved in this correction. Full integrated acceptance and final whole-change disposition remain with the orchestrator.
