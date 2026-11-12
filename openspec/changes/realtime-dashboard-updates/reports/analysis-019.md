# Analysis report: realtime-dashboard-updates / 019

## Review target

- Reviewer / role: ANALYSIS-RECHECK-019 / code-analyzer.
- Client / version / mode / model: Codex / version unavailable / Default delegated review / assigned role model `gpt-6.1-sol`, high reasoning; runtime model introspection unavailable.
- Date: 2026-11-09T15:33:17Z.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-019.
- Task and requirement references: [tasks](../tasks.md) 1.1, 2.1, 4.3, 7.2 and 7.4; archive reading-context preservation and bounded owned-host cleanup.
- Scope: frozen backend/archive-E2E and six-script identity below. Substantive recheck covers `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs`, `scripts/c06-host-lifecycle.mjs` and `scripts/c06-host-lifecycle.spec.mjs`.
- Relevant neighboring reads: catalog stamp/reconciliation in `DashboardLiveCoordinator.cs`, host stop/disposal in `tools/WorkLog.Smoke/C06LiveAcceptanceHost.cs`, stop/start/cleanup wiring in `scripts/c06-fault-matrix.mjs`, architecture live-update description, role/project facts and analysis template. Prior own [analysis 014](analysis-014.md) and [analysis 017](analysis-017.md) establish original findings. No current peer report, current QA evidence, `findings.md`, `HANDOFF.md` or orchestrator workflow was read by this reviewer. The assignment supplied the finding descriptions and developer build/no-host results.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `pwsh -NoProfile -File scripts/review-identity.ps1 -Baseline 0d77685 -Paths src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard dashboard/e2e/dashboard.spec.ts scripts/c06-acceptance-invariants.mjs scripts/c06-fault-matrix.mjs scripts/c06-host-lifecycle.mjs scripts/c06-host-lifecycle.spec.mjs scripts/c06-mcp-compat.mjs scripts/c06-process-outcome.mjs`.
- Manifest: modified `dashboard/e2e/dashboard.spec.ts`, `src/WorkLog/Dashboard/DashboardHost.cs`, `DocumentService.cs`, `SourceCatalog.cs`, and `tests/WorkLog.Tests/Dashboard/SourceCatalogTests.cs`; untracked files listed below. Tracked diff SHA-256: `0028b95318a86a2b96b51ffa83ad956b0fc23673cd5b7b4c198cd26abf71e57c`.

| Untracked scoped file | SHA-256 |
| --- | --- |
| `scripts/c06-acceptance-invariants.mjs` | `1d5c983dc1ad019a9d382ff3568c85f136986a895236593a6397ba335b7cd915` |
| `scripts/c06-fault-matrix.mjs` | `d05bf9fb62e9841ccf596753f254e2f42ffdc5b89ae59de82b87977a3b618729` |
| `scripts/c06-host-lifecycle.mjs` | `4b27ee19a227c22e4302d51f0b7294c3d7fe2145ac0c33f5d74254a1df341009` |
| `scripts/c06-host-lifecycle.spec.mjs` | `c9841c840ca76eb8b43bdb352f2000958a7cf66714b9b990048bb3cedfef56e5` |
| `scripts/c06-mcp-compat.mjs` | `dd007a7e664720cd8fcb2f32e0e6717f5c3471ed055b284b81e1e56dc7bc2484` |
| `scripts/c06-process-outcome.mjs` | `491dd832f80ab574dd56c7511ef2337f6dd0702c2abba11bd8c83d4221c87dca` |
| `src/WorkLog/Dashboard/DashboardChangeSource.cs` | `b4e0857797f114c56a4e199c235894b707a8436bf83a66ce436784a49e30e7a3` |
| `src/WorkLog/Dashboard/DashboardLiveConnections.cs` | `89317ca104c88bbf54df33d3467982b7d38f303b44a02be2c0ff0ce2a564185e` |
| `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs` | `db42cf7bfd22b7da0fdb3c97a811cd832f3b71605ad3b9ccf86e0b40bab8bfd6` |
| `tests/WorkLog.Tests/Dashboard/DashboardChangeSourceTests.cs` | `3cb5aac067f550ae3a8dc413f196ade260c656f63ce60430bc0c2e54583cf9ec` |
| `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs` | `011d2a738dd6cdc35c63d98f84899d4f57fc4481f1d85c56108313b11a423d9a` |
| `tests/WorkLog.Tests/Dashboard/ManualTimeProvider.cs` | `80f730379a27edfd7cc8d19d6d15123b6a0f52dac9746e89859440bf3b6b5c45` |

- Before/after identity: both `head=0d77685 baseline=0d77685 files=17 diff=0028b95318a8 untracked=12/cd3ab06967b3`; exact match.
- Existing findings: ANALYSIS-300 and ANALYSIS-360 only, with relevant cleanup/archive regression. Reserved ANALYSIS-400 through ANALYSIS-419 remain unused.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Archive domain contract | Inspect native archive fixture and `ReconcileCatalogAsync` / `CatalogProjectionStamp` | At `DashboardLiveCoordinatorTests.cs:349-352`, the fixture now requires a later Documents generation and empty dirty state. Product catalog projection still excludes change metadata; document stamp still incorporates source snapshot revision | Passed by source inspection. Removes the invalid unrelated Catalog increment without changing product semantics |
| Authoritative archive behavior retained | Inspect assertions after the corrected gate | The real external `Directory.Move` remains; the response must be HTTP 200, archived, same source/change/path, unable to comment, exact original text, with old directory absent and archived file present | Passed by source inspection. No rename retry, transport suspension, skipped case or raised timeout; native convergence not independently executed in this assignment |
| Archive timeout and containment regression | Inspect existing `WaitUntil` and neighboring watcher topology | Ten-second cancellation deadline and ten-millisecond check delay remain. No watcher, containment or document resolver source changed in this correction | Passed by source inspection; current native/browser behavior remains for coordinated execution |
| Graceful shutdown sequencing | Inspect actual helper against host ACK-before-StopAsync/final ordering | Helper now offers `waitChildExit(child, 10_000)` after successful stop acknowledgement; only a still-live retained child is killed. A separate bounded exit wait remains available after force termination | Passed by source inspection; exact wrapper ownership retained |
| Actual-helper delayed final publication | Independent no-host probe imports current `stopTrackedHost` and schedules asynchronous final publication and clean wrapper exit after ACK | Recorded order: stop request, ACK, bounded exit wait, final publication, wrapper exit, final read. No kill; final disposal and zero connections retained | Passed independently with controlled no-host dependencies; no native process or port was created |
| Actual-helper unresponsive/failed/unknown-ready shutdown | Same probe, distinct state per case | Acknowledged unresponsive host exhausts grace then kills retained child. Rejected stop and missing ready record force-stop without grace; missing ready record never targets a stop command. Wrapper/port completion predicate passes while absent final disposal remains visibly absent | Passed independently at helper level. Final disposal is not fabricated or inferred from ACK |
| Existing lifecycle regressions | `node scripts/c06-host-lifecycle.spec.mjs` | Exit 0; delayed healthy, acknowledged unresponsive, fresh unknown-ready force case and original cleanup predicates pass | Passed independently; test fakes do not establish actual process disposal |
| Acceptance invariant regression | `node scripts/c06-acceptance-invariants.mjs` | Exit 0, acceptance invariants passed | Passed independently; does not execute matrix cycles |
| Startup-failure final proof | Inspect unchanged `startupFailureSmoke` checks | Still requires identified host, final source disposal, zero active connections, wrapper exit and released fixture port. ACK and `hostCleanupComplete` alone cannot pass its stronger gate | Passed by source inspection; actual startup-failure diagnostic not run by this reviewer |
| Duplication / complexity / architecture | Compare narrow correction with architecture and existing source boundaries | Archive fix stays in its fixture. Shared lifecycle helper owns grace/fallback and is exercised by tests; production coordinator and MCP contracts are unchanged | Passed for assigned recheck; no unrelated restructuring or suggestions |
| Whitespace / target stability | Scoped `git diff --check`; before/after identity | Whitespace command exit 0 and exact identity match | Passed |
| Own report hygiene | Category-only scan of this report after writing | No credential, personal/machine-path or unintended fixture-residue match | Passed; whole-change hygiene belongs to coordinated QA |
| Native / packaged / performance / MCP execution | No build, host or client execution under this assignment | Developer held the shared runtime slot. Supplied Release clean/no-host results are not independent execution by this reviewer | Not performed; no current whole-suite, archive browser, MCP, startup-smoke, matrix or performance pass inferred |

Independent controlled evidence: ignored `artifacts/realtime-dashboard-updates/reviews/analysis-019/no-host-recheck.mjs` and `no-host-results.json`. The script imports the actual helper, uses no service/process, and records each scenario's operation order and final/forced/graceful outcome.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Archive fixture and shutdown-only lifecycle | Source observation; passed | No shipped idle owner or recurring product work changes. Shutdown adds an exit-event opportunity, not periodic REST/catalog/document/journal/CLI work. Actual idle CPU/I/O was not measured |
| Cache lifetime versus interval and refresh duration | Cleanup records and bounded shutdown | Source observation; passed | Product caches are unchanged. Cleanup ACK remains bounded at three seconds in the runner; grace is ten seconds, force-exit wait ten seconds, and port release retains its existing five-second bound. Small fixture record I/O remains existing work |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Per-stop helper result and fixture state | Source observation; passed | Adds two scalar outcome fields and at most the bounded waits for the one retained child; no durable queue or accumulating product map. Overall matrix deadline remains unchanged |
| Repeated full reads where a change check would do | Archive authoritative document check / cleanup ready-final records | Source observation; passed | Archive fixture performs its required post-move authoritative read. Cleanup reads the small ready/final records as before. No new complete journal/tree/body scan is added |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Existing owned Smoke wrapper | Source observation; passed within scope | Discovery and spawn paths remain unchanged; termination targets the actual retained child object. Supported package/PATH/native process behavior is not performed in this no-host assignment |

## Findings

**No findings** within this recheck. No new IDs allocated and no suggestions raised.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-300 | Seventeen-file frozen identity above; coordinator test hash `011d2a73...` | Source verifies Documents advancement plus settled state, unchanged domain semantics, preserved real move, exact archived identity/text and read-only permission assertions, unchanged deadline | Verified for invalid fixture-gate correction at source level; actual native/browser convergence remains unperformed by this reviewer |
| ANALYSIS-360 | Same identity; lifecycle `4b27ee19...`, spec `c9841c84...` | Independent actual-helper delayed asynchronous final-publication probe passes without kill; unresponsive/rejected/unknown-ready cases retain bounded exact-child force fallback. Existing lifecycle spec and invariants pass | Verified at source/no-host helper level; actual startup-failure disposal/port evidence remains for coordinated runtime acceptance |

## Assessment and limitations

The two named correction mechanisms are resolved within the frozen target. The archive fixture now waits for the domain that carries archive metadata while retaining its authoritative reader checks. The host lifecycle allows healthy disposal to complete after ACK and still force-cleans an unresponsive owned wrapper within bounds. Both existing no-host suites and the independent delayed-final-publication/fallback probe pass.

Only this report and uniquely named ignored analysis evidence were written. Implementation identity remained unchanged. No source, test source, documentation, tasks, ledger, prior report, current QA evidence, baseline measurement or shared binary changed. No build, native watcher, browser, real host, package, actual port, MCP fixture or authenticated agent was invoked by this reviewer. Dashboard corrections are client-neutral and leave MCP/integration contracts unchanged; historical protocol fixture parity is not current native-client evidence. Required current runtime gates remain unverified here. The orchestrator owns finding disposition and completion.
