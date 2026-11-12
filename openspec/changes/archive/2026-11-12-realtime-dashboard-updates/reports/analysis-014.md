# Analysis report: realtime-dashboard-updates / 014

## Review target

- Reviewer / role: independent archive-topology correction recheck / code-analyzer.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime model introspection unavailable.
- Date: 2026-11-09.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / analysis 014, current frozen ORCH-003 correction.
- Task and requirement references: [tasks 2.1, 3.2, 4.3 and 7.4](../tasks.md), contained watcher roots, archive recovery and preserved reader behavior. [Analysis 016](analysis-016.md) is the preceding platform investigation, not current-target execution evidence.
- Scope: `src/WorkLog/Dashboard`, `tests/WorkLog.Tests/Dashboard`, `dashboard/e2e/dashboard.spec.ts`.
- Reading limits / neighboring reads: scoped watcher, coordinator, SourcePath and tests; relevant e2e archive/readiness/diagnostic helpers; prior own platform investigation. Current `findings.md`, QA 014 and current QA evidence were not read. No Context7 calls, builds, HTTP hosts, test approval requests, native agents or real data/installation access.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard dashboard/e2e/dashboard.spec.ts`.
- Tracked manifest: modified `dashboard/e2e/dashboard.spec.ts`, `src/WorkLog/Dashboard/DashboardHost.cs`, `DocumentService.cs`, `SourceCatalog.cs`, and `tests/WorkLog.Tests/Dashboard/SourceCatalogTests.cs`.
- Tracked diff SHA-256: `0028b95318a86a2b96b51ffa83ad956b0fc23673cd5b7b4c198cd26abf71e57c`.

| Untracked file | SHA-256 |
| --- | --- |
| `src/WorkLog/Dashboard/DashboardChangeSource.cs` | `b4e0857797f114c56a4e199c235894b707a8436bf83a66ce436784a49e30e7a3` |
| `src/WorkLog/Dashboard/DashboardLiveConnections.cs` | `89317ca104c88bbf54df33d3467982b7d38f303b44a02be2c0ff0ce2a564185e` |
| `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs` | `db42cf7bfd22b7da0fdb3c97a811cd832f3b71605ad3b9ccf86e0b40bab8bfd6` |
| `tests/WorkLog.Tests/Dashboard/DashboardChangeSourceTests.cs` | `3cb5aac067f550ae3a8dc413f196ade260c656f63ce60430bc0c2e54583cf9ec` |
| `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs` | `92da29e2da7330eba1d247100570d690fa3a0f6e64102e6bd7ffc6a7923f86ae` |
| `tests/WorkLog.Tests/Dashboard/ManualTimeProvider.cs` | `80f730379a27edfd7cc8d19d6d15123b6a0f52dac9746e89859440bf3b6b5c45` |

- Before/after identity: both match `head=0d77685 baseline=0d77685 files=11 diff=0028b95318a8 untracked=6/fa18895730a1`; no scoped mutation detected.
- Prior reports / findings: existing ORCH-003; new ANALYSIS-300 below, ANALYSIS-301 through ANALYSIS-319 unused. No unrelated suggestions.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Physical ancestor coalescing | Trace canonical logical-root deduplication and ordered physical-root planning | `DashboardChangeSource.cs:49-79` merges only under an explicitly recursive existing ancestor, combines physical domains and retains each logical root in its mapping. Native creation uses `physical.Recursive` at line 106 | Passed by source inspection. Removes descendant OS watcher handles from the approved changes subtree, addressing the handle topology demonstrated in 016. Actual current-target archive execution remains unperformed |
| Recursive authorization boundary | Trace coordinator root discovery and explicit recursion flag | `DashboardLiveCoordinator.cs:732-768` validates `openspec/changes` with SourcePath before enabling recursion, and validates listed children. General repository/data/recovery-parent additions remain nonrecursive. Missing changes root uses the nearest existing contained parent until the actual changes root exists | Passed at source level. No new arbitrary client-selected watcher root or broad recursive repository-parent subscription |
| Event containment and logical routing | Trace `ReportPhysical`, current watcher lookup, canonicalization and deepest logical-root selection | `DashboardChangeSource.cs:134-151` reads the current mapping, canonicalizes event paths, drops those outside every current canonical logical root, and emits the deepest matching root's ID and relative path. Existing coordinator classification restores catalog/document impact for directory changes and config files | Passed by source inspection. SourcePath checks remain on suspect body resolution. Initial/rebound and root/link-swap runtime safety is not independently executed here; prior initial-junction native behavior is supporting context only |
| New directories, archive rename and errors | Trace rename old/new paths, ancestor logical fallback and overflow handling | A new or archived directory can fall back to the retained approved ancestor before child logical roots are rebound. Both rename sides retain lifecycle/directory hints. Native failures promote bounded reconciliation; stale physical roots are disposed and removed | Passed at source level. No rename retries, transport pauses or suppressed product failures |
| Mapping replacement and lifecycle | Inspect same-physical-root update, replacement, disposal and settled-state commit | Same-domain/same-recursion entries replace their logical mapping without retaining the old mapping; topology changes dispose the old watcher. Coordinator still awaits rebinding before committing clean revisions. Global disposal closes all retained native subscriptions | Passed by source inspection; no current native disposal/rename run by this reviewer |
| New regression fixtures and latest supplied execution | Inspect `RecursivePhysicalWatcherCoalescesLogicalRootsAndFiltersOutsideJunctionEvents` and `RecursiveFilesystemWatchAllowsExternalArchiveMoveAndRefreshesOpenDocument`; incorporate the orchestrator's later current-target execution checkpoint | All three DashboardChangeSource tests passed, including new coalescing/outside-junction coverage. The actual coordinator archive move succeeded, then the test failed its ten-second wait for both Catalog and Documents to advance with empty dirty state at lines 351-353; authoritative archived-document checks were not reached | Partial / failed coordinator gate, supplied execution not independently rerun. Earlier testhost socket startup failure is historical; later runtime outcome supersedes the statement that no tests ran. ANALYSIS-300 identifies the extra catalog-counter condition as structurally invalid for a pure archive move |
| E2E readiness and original acceptance | Inspect `trackTimelineReads`, archive case and configurable port | Real request-finished events for document/review reads plus Live establish the setup precondition. Original external rename, archived/read-only permissions, route reload and repeat no-open launch assertions remain. Configured test port replaces hard-coded launch checks and still rejects the user's normal dashboard port | Passed by source inspection. No arbitrary delay, retry, skipped scenario or raised timeout masks archive behavior. Actual browser case remains unrun on this target |
| Optional archive diagnostics | Inspect failure-only capture helper | It runs only when explicitly configured, restricts the diagnostic script to its ignored workspace location, targets the synthetic fixture/health PID, bounds the child call at 10 seconds and rethrows the original rename error | Passed within failure-diagnostic scope; not executed here |
| Proportional static checks | `git diff --check` over scoped paths; local TypeScript parser over e2e file | No whitespace errors; zero parse diagnostics. Evidence under ignored `reviews/analysis-014/static-checks.cjs` and `static-results.json` | Passed. Parsing is not typechecking, browser execution or a source build |
| Build / runtime claims | Supplied developer/orchestrator checkpoints only | Release build reported zero warnings/errors. Later focused execution passed three source-watcher tests and failed the coordinator revision wait after a successful native archive move | Not independently performed. Native rename lock is corrected in that execution; required authoritative archived-document convergence was not reached and remains unverified. No current whole-suite or integrated acceptance pass inferred |
| Target stability / own-report hygiene | Before/after identity; configured category-only report scan | Identity unchanged; own report contains no personal/machine/credential values or unintended residue | Passed. Broad final change-root hygiene deferred during concurrent reviews |

The actual platform experiment in analysis 016 had two contained nested events and zero events for five outside-target mutation operations over its bounded observation window. It used a standalone native watcher on the original fixture. It does not execute this new mapping/filtering implementation, establish all platform/link-swap behavior, or replace the new native/coordinator/browser regression gates.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Physical watcher plan and existing coordinator | Source observation | No new timer, HTTP request, journal query or CLI discovery owner. Physical handles are reduced under approved recursive roots. Each delivered event now resolves its canonical path and searches mapped logical roots; actual metadata/CPU/allocation cost remains for final measurement |
| Cache lifetime versus interval and refresh duration | Logical mappings and source discovery | Source observation | Existing catalog/discovery lifetimes and five-second reconciliation remain unchanged. Rebind refreshes current mappings and reuses a physical subscription only when domains/recursion agree. No measured latency or idle improvement claimed |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Physical dictionary, logical root arrays and hint channels | Source observation | Existing 512 physical-watcher limit and 128-hint overflow promotion remain. Logical mappings still reflect discovered directories rather than being independently capped; coalescing retains that existing logical discovery state. Per-lane/connection bounds remain unchanged; no durable event history added |
| Repeated full reads where a change check would do | Event canonicalization / periodic discovery | Source observation | Canonical event validation is required containment work, not a full document-body read. Existing discovery/traversal still occurs on its established path. New mapping computation and lookup work has not been benchmarked |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Existing source resolver and optional diagnostic | Source observation | Product executable discovery unchanged. Optional failure-only diagnostic uses an allowlisted ignored script and a 10-second child timeout. Supported install/PATH/offline package variants were not rerun here |

## Findings

### ANALYSIS-300: Archive regression requires an unrelated catalog generation to advance

- Severity: warning.
- Related task / requirement: tasks 2.1, 4.3 and 7.4 / ORCH-003; valid native archive recovery acceptance without changing domain semantics.
- Locations: `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs:351-353`; domain stamp definitions in `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs:466-470,628-636` and `src/WorkLog/Dashboard/SourceCatalog.cs:252`.
- Observation: The new native archive test waits for both Catalog and Documents counters to advance. CatalogProjectionStamp intentionally covers repositories, source identity/location/error, launch repository and notices, excluding the Changes collection. A pure active-to-archive move can therefore leave that catalog projection unchanged. SourceCatalog.Revision includes change directory names/archive metadata and advances the Documents domain instead. The browser's existing catalog-and-reader refresh depends on either catalog or documents, so both counters need not advance to recover archive state.
- Actual evidence: The orchestrator's current-target runtime checkpoint reports all three watcher-source tests passing, Directory.Move succeeding, then this ten-second conjunctive wait failing. No per-counter vector was supplied, so this review does not assert which condition actually remained false in that run. The source proves the extra catalog-advance assertion is not a valid required consequence of an otherwise pure archive move. The test never reached its authoritative archived-document assertions.
- Proposed improvement and benefit: Capture the actual before/after vector for diagnosis, wait for the relevant Documents generation and settled dirty state, then retain or strengthen the authoritative catalog/archive identity and returned archived-document/text/permissions assertions. Do not make the product increment an unrelated domain merely to satisfy this fixture, and do not increase the timeout or drop real archive assertions.
- Behavior-preservation risks: Successful native rename alone does not establish reader convergence. The corrected test must still prove external detection and the current archived representation with unchanged text, followed by the original browser acceptance.
- Evidence type: Independent source/domain-contract analysis plus an explicitly supplied developer execution outcome; no independent host/test run by this reviewer.
- Verification / measurement plan: Run the corrected native coordinator case on the same topology, preserve counter diagnostics, execute the unchanged archived-document checks and then the exact browser archive scenario. A continued failure after the invalid counter condition is removed remains a product or fixture finding requiring evidence.
- Relationship: New acceptance-fixture blocker adjacent to ORCH-003; no observed current reader-convergence defect is claimed yet.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ORCH-003 | Eleven-file identity above | Source removes descendant physical handles under validated recursive changes roots; latest supplied run passes three native source tests and successfully moves the active directory. Coordinator recovery test then fails its conjunctive counter wait before archived-document checks | Original native lock is corrected in the supplied execution. Full archive convergence remains unverified; ANALYSIS-300 must be resolved without weakening authoritative archive/reader assertions |

## Assessment and limitations

The patch addresses the identified archive-blocking watcher topology with a bounded physical/logical split and preserves the existing source authorization and reader acceptance paths. The latest supplied run confirms the directory move now succeeds and the three native source-watcher tests pass. One acceptance-fixture blocker remains: the new coordinator test demands a catalog-counter advance that is not required by the domain contract, and it fails before proving archived-document convergence. This is not evidence that the reader itself failed to converge.

Only this report and ignored static evidence were written. No implementation, tests, ledger, task state, prior reports, QA evidence, shared binaries or baseline measurements changed. Before/after identity matches. No build, host, native watcher/test run, browser suite, package or performance capture was started. Current-source root/link-swap, archive and broader final acceptance remain explicit execution requirements; the orchestrator owns completion and finding disposition.
