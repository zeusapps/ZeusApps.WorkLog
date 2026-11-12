# Analysis report: realtime-dashboard-updates / 022

## Review target

- Reviewer / role: ANALYSIS-INVESTIGATION-022 / code-analyzer; bounded read-only attribution.
- Client / version / mode / model: Codex / version unavailable / Default delegated review / assigned role model `gpt-6.1-sol`, high reasoning; runtime introspection unavailable.
- Date: 2026-11-09T15:58:31Z.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-INVESTIGATION-022.
- Task and requirement references: [tasks](../tasks.md) 5.4 and 7.2; actual interrupted-connection cycles, writes committed during a proven outage, preserved persistent reader draft and measured reconciliation after actual re-establishment.
- Scope: preserved five-category diagnostic failure and current fault-runner / browser connection source. No third implementation cycle is authorized by this assignment.
- Permitted raw source: `artifacts/realtime-dashboard-updates/fault-matrix/2026-11-09T15-54-22-394Z-a8b069ba.json`, SHA-256 `df01abbe9c27a000a17d50260490fa77de0f879b633f497f56d6a7221cd44ccf`. It is failed actual diagnostic evidence, not a current QA report or completed acceptance run.
- Reading limits: no current QA report/evidence, findings ledger, handoff or orchestrator workflow read. Relevant reads: fault-runner injection/observation/preparation/cleanup, App connection setup and listeners, connection controller, existing lifecycle helper. Additional relevant local reads: installed Playwright type declarations for offline/routed WebSocket operations, installed SignalR HubConnection timeout source, and the pinned package manifest. No new documentation fetch or runtime probe.
- Supplied evidence, not independently rerun here: current dashboard suite307/307 and exact archive browser scenario1/1 passed. This investigation does not re-approve those checks.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `pwsh -NoProfile -File scripts/review-identity.ps1 -Baseline 0d77685 -Paths dashboard/src/app.ts dashboard/src/app.reliability.spec.ts dashboard/src/live-updates.ts dashboard/src/live-connection.ts src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard dashboard/e2e/dashboard.spec.ts scripts/c06-fault-matrix.mjs scripts/c06-host-lifecycle.mjs scripts/c06-acceptance-invariants.mjs`.
- Manifest: modified `dashboard/e2e/dashboard.spec.ts`, `dashboard/src/app.reliability.spec.ts`, `dashboard/src/app.ts`, `src/WorkLog/Dashboard/DashboardHost.cs`, `DocumentService.cs`, `SourceCatalog.cs`, `tests/WorkLog.Tests/Dashboard/SourceCatalogTests.cs`; eleven untracked scoped files below.
- Tracked diff SHA-256: `1ff82d0623d82a4ad6cb4e4aa95a027d8b6883fd15f86c361fd4b91134fb5c17`.

| Untracked scoped file | SHA-256 |
| --- | --- |
| `dashboard/src/live-connection.ts` | `78db24467ab95c0221d16c986c57ee9e59ff27a35a7140cfd032228b6783e5ac` |
| `dashboard/src/live-updates.ts` | `1430c9e42ae7790af48e9c48af8eefbd6decf65faf94f085b0f7c5319ee16d54` |
| `scripts/c06-acceptance-invariants.mjs` | `1d5c983dc1ad019a9d382ff3568c85f136986a895236593a6397ba335b7cd915` |
| `scripts/c06-fault-matrix.mjs` | `d05bf9fb62e9841ccf596753f254e2f42ffdc5b89ae59de82b87977a3b618729` |
| `scripts/c06-host-lifecycle.mjs` | `4b27ee19a227c22e4302d51f0b7294c3d7fe2145ac0c33f5d74254a1df341009` |
| `src/WorkLog/Dashboard/DashboardChangeSource.cs` | `b4e0857797f114c56a4e199c235894b707a8436bf83a66ce436784a49e30e7a3` |
| `src/WorkLog/Dashboard/DashboardLiveConnections.cs` | `89317ca104c88bbf54df33d3467982b7d38f303b44a02be2c0ff0ce2a564185e` |
| `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs` | `db42cf7bfd22b7da0fdb3c97a811cd832f3b71605ad3b9ccf86e0b40bab8bfd6` |
| `tests/WorkLog.Tests/Dashboard/DashboardChangeSourceTests.cs` | `3cb5aac067f550ae3a8dc413f196ade260c656f63ce60430bc0c2e54583cf9ec` |
| `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs` | `011d2a738dd6cdc35c63d98f84899d4f57fc4481f1d85c56108313b11a423d9a` |
| `tests/WorkLog.Tests/Dashboard/ManualTimeProvider.cs` | `80f730379a27edfd7cc8d19d6d15123b6a0f52dac9746e89859440bf3b6b5c45` |

- Before/after identity: both `head=0d77685 baseline=0d77685 files=18 diff=1ff82d0623d8 untracked=11/cb60b965cc31`; exact match.
- New allocation: ANALYSIS-460 below; ANALYSIS-461 through ANALYSIS-479 unused.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Actual failure boundary | Read preserved `failure`, `records` and `incompleteCycle` | Zero completed records; cycle1 Documents/disconnect stopped at the five-second non-Live status wait. No `outageConfirmedAtUtc`, writer result or restored-transport timestamp exists | Failed diagnostic setup; no recovery attempt after an outage write was reached |
| Correct temporal attribution | Compare each raw monotonic timestamp with `faultStartMonotonicMs=10792.3387` | A-target cycle1 Reconnecting10326.5799 / Live10454.9671 and B-target Reconnecting10341.7776 / Live10670.6919 all precede the fault. `activeCycle` is assigned before `prepareVisibleState`, which navigates target pages | Passed attribution. Those transitions are preparation activity, not evidence of post-fault reconnection |
| Post-fault transport observations | Read raw lifecycle, status and browser-frame records after the exact fault timestamp | No recorded A-side close/open/status transition or live frame. Five B-target Revision-frame observations remain. No recorded close/open for either A tab establishes actual detachment or a new transport | Evidence gap; no inference that both A sockets closed. Lack of a recorded close also does not prove a native socket stayed open |
| Injection contract | Inspect `cycleDisconnect` at `scripts/c06-fault-matrix.mjs:782-807` | Calls `contextA.setOffline(true)`, then waits for two status surfaces, with no retained A socket handle or per-page close proof. Restored handshakes are only measured after later online restoration | Failed disconnect-evidence fidelity for this run; ANALYSIS-460 |
| Product detection path | Inspect App builder/listeners and `LiveConnectionController` | App has no offline-event listener. Status changes on actual transport close / connection work / failed data work. Builder does not override the installed SignalR server timeout; local SignalR10.0.11 source defaults to30seconds and schedules its timeout on lack of messages | Passed source attribution. A five-second offline-emulation-to-UI-transition guarantee is not established. The source timeout is an explanatory mechanism, not a measured browser closure time |
| Available bounded control seam | Read existing B-target WebSocket route and installed Playwright1.64.1 declarations | Runner already proxies real `/api/live` WebSockets for B wire faults. Local types expose retained routed-side closure, optional close observation and server-side pairing. Offline emulation is described as affecting regular network/WebSocket traffic but does not promise immediate established-socket close callbacks | Passed feasibility at local source-contract level; proposed A-side route behavior remains unprobed |
| Owned cleanup | Read this raw cleanup only | Graceful ACK and exit, no force termination, final source disposed, zero final connections, released fixture port and zero owned wrappers | Passed in preserved actual record; not a new execution and not a completed fault cycle |
| Target stability / own-report hygiene | Before/after identity and category-only scan of this report | Exact identity unchanged; no sensitive/residue category match in report | Passed |
| Replay / implementation | None | No build, host, test, browser, runtime probe or implementation edit performed | Not performed by explicit assignment boundary; two correction cycles already used |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Proposed fixture-only disconnect control | Source observation; not measured | Investigation adds no runtime work. A future correction should affect only explicit owned fault cycles; do not add product polling or shorten product heartbeat/timeout settings to satisfy this diagnostic |
| Cache lifetime versus interval and refresh duration | Disconnect detection and recovery timestamps | Source observation; failed setup assumption | Current five-second status wait begins after offline emulation, rather than after proven closure. This is separate from the agreed five-second re-established-transport-to-reconciled-state target, which must remain unchanged |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Prospective per-page socket registry | Source observation; unimplemented recommendation | Retain only current sockets for the two owned A pages, remove closed/navigation-replaced entries and prove bounded cardinality. Preserve fixed100cycle allocation and overall run deadline; no accumulating stale route history as live ownership |
| Repeated full reads where a change check would do | Fault setup and evidence | Source observation; passed within investigation | No new data or document read path recommended. Record lightweight close/generation/handshake facts, preserving authoritative DOM reconciliation checks |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Existing fixture runner | Source observation; unchanged | No new executable discovery or external process proposed. Existing owned Smoke host/control/cleanup and safe ports remain. New route-control support needs bounded fixture verification before full matrix execution |

## Findings

### ANALYSIS-460: Disconnect cycle does not establish closure of both intended transports before its five-second status gate

- Severity: warning; blocks required disconnect/matrix acceptance.
- Related tasks:5.4 and7.2, actual interrupted connections and reliable recovery evidence.
- Locations: `scripts/c06-fault-matrix.mjs:782-807` (`cycleDisconnect`), `:1009-1021` (A observers without retained close controls), `:1029-1049` (cycle labels include target-navigation preparation). Relevant product setup: `dashboard/src/app.ts:555-564`, `dashboard/src/live-connection.ts:167-187`.
- Observation: The failed actual run sets the A context offline and assumes both established A connections will generate non-Live UI state within five seconds. It neither actively closes the retained intended sockets nor records their actual detachment. All raw cycle1 reconnect/Live transitions occurred before fault injection during target preparation. After injection the run has no A transport close/open/status observations and reaches neither outage-confirmed writes nor recovery measurement. Installed connection source supports delayed timeout detection; it supplies no immediate status guarantee for this specific browser/offline emulation.
- Proposed improvement and benefit: In the next explicitly authorized correction, add a fixture-only bounded disconnect control for the exact current A-reader and A-target live sockets. Register that ownership before their initial navigation, keep current real upstream/page pair identities, use the available routed-WebSocket closure seam to terminate both intended pairs, and prevent replacement A connections from completing while the outage is held. Context offline can continue to block ordinary requests; do not rely on it as the only socket-closure proof. Keep B's independent control and existing Revision-wire behavior separate. Record per-page fault-start, actual closure and generation identities and reject missing/incorrect targets before any writer proceeds.
- Behavior-preservation risks: Do not reload/close A pages or replace the persistent reader draft. Do not call an App/SignalR method that bypasses the real transport failure path. Routed close interception must preserve explicit/default close forwarding for both sides; local Playwright declarations warn that installing a close handler disables default forwarding. Do not infer transport loss solely from UI status or assume offline emulation governs a newly proxied upstream. Any reconnect gate needs explicit release and bounded cleanup. Preserve existing outage-write, both-target state, reader-context, new-handshake and recovery latency requirements.
- Evidence type: Independent source and preserved actual raw-record analysis. No runtime replay, new platform guarantee or proven product-recovery defect.
- Verification plan: First verify the narrow control with retained A socket identities and real upstream connections on disposable fixtures: both intended sockets close after the exact fault start, B remains independent, no replacement A handshake succeeds while held, both A statuses become non-Live, and the initial draft remains intact. Only then perform the existing independent committed write with precommit absence checks, prove it occurred after both closures while outage is held, release the gate, require a new accepted SignalR handshake for each A tab after release, and measure current visible reconciliation within the unchanged five-second recovery target. Run the bounded five-category diagnostic before all100cycles; preserve every failure and cleanup outcome. No recommendation to remove a requirement, raise a timeout, lower a target or alter product timeout settings.
- Relationship: New concrete acceptance-harness blocker. It does not reopen the corrected archive-metadata finding or establish a new product connection-recovery defect.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-460 | Current unchanged eighteen-file target | Preserved actual cycle1 failure plus source attribution above | Reported; no implementation correction authorized or attempted |
| Earlier archive / cleanup corrections | No new implementation examined beyond neighboring semantics | Supplied archive307/307 and1/1 evidence remains distinct; this raw record independently contains successful final cleanup | Not re-reviewed for closure here; no completed fault/recovery acceptance inferred |

## Assessment and limitations

The evidence supports a fault-injection/observability blocker: the runner has not established the intended two-socket disconnection and stops before outage writing and recovery. It does not demonstrate that the product failed to recover from a proven disconnection. The absence of A frames during the short offline interval is compatible with blocked traffic, but cannot establish closure or how long detection would take. An ordinary HTTP preference request is recorded after the toggle; that is not proof it completed successfully. B continues receiving frames as the independent context. The current raw status labels must be interpreted with their timestamps because cycle tagging starts before target navigation.

The minimal likely next correction belongs in the fixture runner's owned transport control/evidence, followed by bounded real verification. Exact behavior of both intended sockets under that new route control remains uncertain until executed; if explicit transport closure is observed and the UI still does not leave Live, investigate the product close-handler path with that evidence. No API or browser guarantee beyond the installed source/type contracts is asserted.

Only this report was written. No new runtime command, test, host, browser, build, package, documentation fetch or platform probe ran. No source, implementation documentation, task state, ledger, prior report, current QA evidence, preserved raw record, baseline input or shared binary changed. Before/after identity matches. The orchestrator must obtain the next-cycle decision under the repository limit before assigning implementation; this investigation does not supply that authorization.
