# Analysis report: realtime-dashboard-updates / 008

## Review target

- Reviewer / role: independent code-analyzer, ANALYSIS-RECHECK-008.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime-resolved model introspection unavailable.
- Date: 2026-11-08.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-008.
- Authorization / scope: the orchestrator records the user's new explicit continuation as authorization for this focused correction/recheck after the prior cycle limit. Recheck existing ANALYSIS-043 plus regression and additive checkpoint-publication metadata for task 2.4. No unrelated suggestion or feature review.
- Task references: [tasks 2.2–2.4](../tasks.md), [design revision/publication contract](../design.md), [analysis 005](analysis-005.md) and [QA 005](qa-005.md) definitions of the remaining ANALYSIS-043 case.
- Scope: `src/WorkLog/Dashboard` and `tests/WorkLog.Tests/Dashboard`. Browser/harness work is concurrent and excluded.
- Reading limits / neighboring reads: current scoped source and relevant fixtures, the two prior finding definitions, updated design contract and supplied `DEV-015.trx`. Concurrent QA 008 conclusions were not read. Historical acceptance report 007 remains unchanged; a later observation of intentionally advanced acceptance sources is not a review of those sources and is excluded here.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard`.
- Tracked manifest: modified `DashboardHost.cs`, `DocumentService.cs`, `SourceCatalog.cs` and `SourceCatalogTests.cs` within the scoped paths.
- Tracked diff SHA-256: `0a9a5cbc84589f41c0e5d735310de10495f6f315af47c5a1f3d57e7e707d9aa2`.

| Untracked file | SHA-256 |
| --- | --- |
| `src/WorkLog/Dashboard/DashboardChangeSource.cs` | `8241ae398e0136be338d053849d278cab4b844a18d55cf503c67e0d1589ae8a3` |
| `src/WorkLog/Dashboard/DashboardLiveConnections.cs` | `89317ca104c88bbf54df33d3467982b7d38f303b44a02be2c0ff0ce2a564185e` |
| `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs` | `a41518c3de1e46d237fca78e011c37f00d53319cc9866cd99c5ec333cf664e03` |
| `tests/WorkLog.Tests/Dashboard/DashboardChangeSourceTests.cs` | `aae4810c49773abe0dc5fcc5521841b8da0dd0cf48b265a2151e37c84435f1ab` |
| `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs` | `6d025fef68b1e9085a32f466bc823722040591003f96fe117a2b344aca71fb31` |
| `tests/WorkLog.Tests/Dashboard/ManualTimeProvider.cs` | `80f730379a27edfd7cc8d19d6d15123b6a0f52dac9746e89859440bf3b6b5c45` |

- Before/after identity: both match `head=0d77685 baseline=0d77685 files=10 diff=0a9a5cbc8458 untracked=6/217131e766d2`. No scoped mutation detected.
- Finding allocation: existing ANALYSIS-043; regression range ANALYSIS-180–199 unused. No new ID allocated.

## Analysis performed

| Area | Independent check / evidence | Result / limitation |
| --- | --- | --- |
| Periodically detected replacement | Trace typed journal marker, comparison, derived-cache invalidation and bounded catalog follow-up | Corrected source path now reaches invalidation without any lifecycle hint; ANALYSIS-043 source correction verified |
| Cache efficiency | Inspect `JournalRepositoryIdentityChanged` and existing SourceCatalog rowid/version cache condition | Unchanged probes do not invalidate; normal append-only rowid movement remains handled by the existing rowid key. Availability/lifecycle changes and same-rowid changed file identity/signature trigger only derived repository invalidation, not blanket expensive-command invalidation |
| Lock/error retention | Inspect `ReadJournalMarker`/reconcile catch and previous-marker commit order | Busy/Locked escapes before committing a marker, retains dirty state and preserves the previous validated identity. Persistent availability states have explicit markers and recovery can trigger derived refresh |
| Lane/race behavior | Inspect single journal lane, catalog invalidation generation and pending catalog merge | One journal probe at a time; invalidation increments a version so an overlapping catalog discover cannot publish a snapshot based on the old cache version. Follow-up enters the existing one-pending catalog lane; no second poll or unbounded work queue is introduced |
| Publication metadata | Inspect `DashboardRevisionVector`, `Publish`, `CurrentSnapshot`, hub Snapshot and actual send path | Only checkpoint publications copy in an atomic numeric sequence; ordinary coordinator vectors/Snapshot omit it. The immutable vector copy enters the same bounded channel and is sent unchanged. Coalescing may drop a vector, but does not label an ordinary vector as a checkpoint |
| Protocol/privacy regression | Compare updated design contract and scoped call graph | Existing instance/domain/dirty fields and ordering are unchanged; no HTTP/MCP request contract, client-selected path or private content is added. Checkpoint is optional publication metadata, not a domain revision |
| Supplied executed regression | Independently read `artifacts/realtime-dashboard-updates/resume-backend-checks/DEV-015.trx` and inspect matching fixture source | TRX records 28 total/executed/passed, zero failed/aborted/not-executed. Includes missed-hint A-to-B replacement, same-marker replacement, slow-catalog journal delivery and locked-journal isolation. Execution was developer-run, not independently rerun here |
| Proportional checks | Scoped whitespace, identity before/after and category-only report/change Markdown hygiene | Passed. No test runner, server, build, real data, installation or native client was started by this reviewer; no approval prompt issued |

The supplied no-hint regression, `MissedJournalReplacementIsDetectedByPeriodicCheckpointAndRefreshesRepositoryCatalog`, uses an injected clock and a manual hint source. It builds a one-row repository-A journal/catalog, replaces the file with a one-row repository-B journal with changed file metadata, emits **no** lifecycle hint, advances the ordinary five-second timer, and asserts journal/catalog revisions plus B present/A absent. It then advances a subsequent checkpoint and receives a real WebSocket vector carrying the advanced journal/catalog and numeric checkpoint field. The same fixture asserts Snapshot omits `checkpoint`; independent journal delivery while catalog is blocked asserts an ordinary notification omits it.

The TRX reports this fixture passed in about 0.308 seconds. That is an accelerated-clock fixture duration, not a measured real five-second recovery or a healthy-workload latency benchmark. The supplied slow-catalog and locked-journal delivery fixtures also passed. Developer reports a warning-free Release build; this review did not independently rebuild or execute those fixtures.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Existing five-second journal lane | Source observation | Each journal marker now also records file/WAL lifecycle signatures alongside existing length/write/creation metadata and rowid. Unchanged identities do not schedule another derived catalog refresh. Actual CPU/allocation/metadata overhead remains for final measurement; no lower-cost claim is made |
| Cache lifetime versus interval and duration | Journal-derived repository cache and expensive command tier | Source observation | Availability or lifecycle movement, or changed file identity/signature with the same rowid, resets the derived names cache. Ordinary rowid changes remain sufficient to refresh it through the existing catalog condition. Separate command caches keep their lifetimes/input stamps; this correction does not force Git/OpenSpec |
| Unbounded state | Marker, catalog follow-up and checkpoint sequence | Source observation | One previous typed marker replaces the prior string; journal/catalog lanes keep their existing bounded single-active/single-pending work. Checkpoint uses one numeric counter and immutable per-publication copy, no retained event history. Notification channels remain one element per connection |
| Repeated full reads where a change check would do | Repository identity query | Source observation | Distinct repository names are not recomputed on every unchanged marker read. A validated replacement/recovery invalidates the derived cache and a subsequent catalog query recomputes names. Same-rowid metadata noise can conservatively cause an extra derived refresh; that bounded tradeoff must be represented in final server-work measurements |
| External tool discovery / timeouts | Existing server dependencies | Source observation | External executable discovery/timeouts are unchanged. Marker probes retain bounded SQLite busy waits and independent worker lanes. Supplied fixtures prove selected isolation paths, not every install method or final package environment |

## Findings

**No new findings.** The assigned remaining case is corrected at source level and supported by the supplied executed detector-level fixture. No regression ID or optional suggestion is allocated.

## Re-verification

| Finding / behavior | Corrected target evidence | Independent assessment | Boundary |
| --- | --- | --- | --- |
| ANALYSIS-043: missed lifecycle hint with same-rowid A-to-B replacement | `ReconcileJournalAsync` compares validated typed identity and invalidates the derived repository cache before queuing one catalog follow-up; `JournalRepositoryIdentityChanged` covers availability/lifecycle and same-rowid changed file metadata | **Correction verified at source level**; supplied DEV-015 fixture records the exact detector-level no-hint case passing | Test was developer-executed with fake clock/manual hints and real local WebSocket, not independently rerun; no claim about replacements for which every observed identity/metadata marker is unchanged |
| Checkpoint correlation metadata, task 2.4 | `Publish(checkpoint: true)` assigns a copied vector's optional monotonic sequence; ordinary/Snapshot producers create null metadata and serialization omits it; queued send uses the copy unchanged | Source contract coherent; supplied fixture confirms delivered numeric checkpoint and omitted Snapshot/ordinary field | Does not establish every checkpoint survives slow-consumer coalescing or prove full browser fault acceptance |

## Assessment and limitations

The remaining ANALYSIS-043 path identified in analysis/QA 005 is addressed: validated periodic replacement detection now propagates to the derived catalog cache without relying on a delivered watcher hint. The new checkpoint field marks actual checkpoint publications without changing domain ordering or disclosing private data. Supplied 28/28 fixture evidence supports these corrections; source and TRX inspection are the independent work performed here.

This focused recheck does not establish the whole realtime feature complete. Full current browser/fault, fresh self-contained/offline, MCP compatibility, idle, freshness, server-work and broader final acceptance remain separate gates. No unmeasured performance claim or additional fix request is made. Only this reserved report was written; implementation, tasks, ledger, prior reports and runtime evidence were not modified.
