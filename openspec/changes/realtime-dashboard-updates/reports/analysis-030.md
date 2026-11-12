# Analysis report: realtime-dashboard-updates / 030

## Review target

- Reviewer / role: ANALYSIS-RECHECK-030 / code-analyzer; second correction recheck of ANALYSIS-580 and ORCH-009 plus regression only, no suggestions.
- Client / version / mode / model (resolved ID): Codex CLI / `0.167.0` observed earlier in this session / Default delegated review / resolved model ID unavailable. Agent role `default`; no model override supplied.
- Date: 2026-11-11T10:50:07Z.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-030.
- Task and requirement references: [tasks](../tasks.md) 4.1, 4.2, 4.5, 5.4, 7.2, with 6.1/6.2 temporal regression; [design](../design.md) scoped authoritative refresh, retained pending work and transport-derived recovery timing; prior [analysis-028](analysis-028.md).
- Scope: `dashboard/src/app.ts`, `app.reliability.spec.ts`, `live-connection.ts`, `live-updates.ts`, `app-live-test-helpers.ts`; `scripts/c06-fault-matrix.mjs`.
- Reading limits and paths read outside scope: role/project facts/template and relevant tasks/design/architecture retained from earlier assignments; own analysis-028 and ignored actual-method scaffold read. Additional source read: `dashboard/src/api.ts` for actual API/error/date helpers, local installed TypeScript for transpilation. Only the permitted earlier raw `artifacts/realtime-dashboard-updates/fault-matrix/2026-11-11T10-31-17-197Z-e556f130.json` was inspected. No current QA-026, peer current results, ledger, HANDOFF or orchestrator workflow read. No implementation, test source, task or documentation edits allowed or performed.
- Baseline commit and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `pwsh -NoProfile -File scripts/review-identity.ps1 -Baseline 0d77685 -Paths dashboard/src/app.ts dashboard/src/app.reliability.spec.ts dashboard/src/live-connection.ts dashboard/src/live-updates.ts dashboard/src/app-live-test-helpers.ts scripts/c06-fault-matrix.mjs`.
- Implementation identity: modified App and reliability spec; other four files untracked. Tracked diff SHA-256 `b6d72393c44eee7d63e4bca984c04227a0ca832be3aa904a2a323b63b52eadd8`.
- Before/after identity: both `head=0d77685 baseline=0d77685 files=6 diff=b6d72393c44e untracked=4/f9c528ce4550`. Full HEAD, baseline, manifest, diff and hashes match; no unexpected source mutation. Final individual hashes also match assigned target below.
- Prior reports / rechecked findings: [analysis-028](analysis-028.md), ANALYSIS-580; ORCH-009 supplied by assignment and supported by inspected earlier raw. ANALYSIS-640 through ANALYSIS-679 unused.

| Scoped file | SHA-256 |
| --- | --- |
| `dashboard/src/app.ts` | `7a254a23d4f4ea7957a67f8d759e24a21efe406f7e7883f1de92a0c849cdb0d9` |
| `dashboard/src/app.reliability.spec.ts` | `345d5c073ab31503704b31c71d1d69096a7a184d5db8c1e1f2942700810890b5` |
| `dashboard/src/app-live-test-helpers.ts` | `0f57c6112a8ddce266144e1430daf13b0a918cfef061744d53147efface636d1` |
| `dashboard/src/live-connection.ts` | `78db24467ab95c0221d16c986c57ee9e59ff27a35a7140cfd032228b6783e5ac` |
| `dashboard/src/live-updates.ts` | `1430c9e42ae7790af48e9c48af8eefbd6decf65faf94f085b0f7c5319ee16d54` |
| `scripts/c06-fault-matrix.mjs` | `e4a32aa915ccceaf0488cdf732e36f7c345f0c496ed8ceced7909086bbe42331` |

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Original ANALYSIS-580 reproduction | Execute current actual receive/reconcile/load/activation methods with deferred old selected-day response and queued new day | `app.ts:751-766,779-795,1210-1247`; ignored results below | Passed: zero stale publications; one latest publication; concurrency one; loading remains true, status Reconnecting and journal/digest domains pending until latest request succeeds |
| Scope changes without selection epoch movement | Same actual-method probe changes timezone, repository set or local date while first request is held; queues actual manual refresh | Actual Daily scope compares selected day/repos/timezone/local date/selection epoch | All three pass with zero stale data and no Live/applied acknowledgement before latest response; no dependency on route selection epoch alone |
| Same-scope joining / original duplicate-owner regression | Deferred initial Snapshot with activation and manual refresh sharing same scope | One Daily resource/token with union journal+digest dependencies | One request and one publication, success and cleared pending work; same-scope callers do not invalidate each other |
| Stale non-auth error and current failure | Actual API helper supplies old-scope 503, then latest-scope 401; separate current 503 and forced retry | Actual report/invalidate-authorization methods included in probe | Stale 503 does not replace current error/data or stop latest queue drain. Current 401 remains unauthorized/paused/pending; current 503 remains false/dirty/paused and explicit retry succeeds |
| Superseded-scope 401 | Old request returns actual API 401 after scope changes | 401 branch precedes stale-scope success branch; invalidation clears queued protected work | Passed: authorization error is not downgraded to success; no latest protected request starts, no data publication, pending revisions retained |
| Destruction/auth guards | Probe performs existing destruction state/selection invalidation/queue clear while read is pending; inspect actual outer guards | No late publication or Live, no further forced refresh read | Passed within controlled method boundary. Probe does not instantiate Angular destruction lifecycle; actual controller/authorization source remains unchanged |
| Temporal regression | Actual date helpers and timeChanged with live queue across Warsaw 23-hour DST day, explicit earlier selected day retained | New local-day token changes boundary to `2026-03-29T22:00:00.000Z`; selected day stays `2026-03-25` | Passed: one legitimate new read, no subsequent unchanged read; no chosen-day rewrite |
| ORCH-009 source sequence | Inspect full `cycleReauthorize`, actual authorize helper and measurement | Runner `:995-1034`; authorization helper `:412-435`; measure helper `:645-651` | A reader then A target authorize sequentially, B in parallel; all explicit authorization operations finish before any handshake lookup. Pre-authorization accepted-array indices retained |
| Delayed authorization and timestamp fidelity | Execute entire actual cycleReauthorize plus actual measureConnectedAt with controlled authorization promise gates and logical clock | Authorization completes at logical 23000 ms, exceeding previous 15000 ms lookup window | No lookup starts before all authorizations finish. Three lookups start at 23000 with unchanged 15000 ms bound/25 ms poll argument. Return original B/reader/target acknowledgement timestamps 20001/20002/23000, not lookup completion time |
| Stale acknowledgements / auth failure | Same actual cycle with only pre-index reader acknowledgement; separate explicit authorization rejection | Stale-only case rejects before state assertion; rejected authorization aborts before lookup | Passed. Old pre-fault accepted entries cannot satisfy new lookup; authorization success is not inferred from another tab's handshake |
| Required recovery checks retained | Inspect assertCycleState `:782-784`, writer/outage gates and bounded control flow | All three 5000 ms post-transport limits unchanged; real recorded handshakes still feed those calculations | Passed source inspection. No UI timestamp, Snapshot completion, timeout increase, arbitrary delay/retry or weakened five-second target substituted |
| Correction tests | Inspect added deferred day/context readiness assertions and stale-error/current-401 tests in reliability spec | Data null, loading true, Reconnecting and pending work asserted while latest response is held; latest success clears them | Meaningful coverage now includes the original missing intermediate-state assertion. Parent supplies 315 passing dashboard tests; reviewer did not rerun that suite |
| Independent bounded command | `node artifacts/realtime-dashboard-updates/reviews/analysis-030/no-host-recheck.mjs` | Exit 0; nine check groups plus four scoped variants | Passed independently; actual methods extracted from current source, unchanged actual queue/tracker and API/date helpers used |
| Syntax / whitespace / identity | `node --check scripts/c06-fault-matrix.mjs`; scoped `git diff --check`; before/after identity | All exit 0, matching identity. Existing line-ending warning on reliability spec only | Passed. Tracked-diff whitespace does not inspect untracked file contents |

Independent evidence: [probe](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-030/no-host-recheck.mjs) and [results](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-030/no-host-results.json). The prior independent scaffold is reused for controlled dependencies; every evaluated product method is extracted from current source. The proof includes actual error/report/authorization invalidation methods rather than replacing errors with success stubs. Other domain reads are successful stubs to isolate Daily ownership.

The actual reauthorization method is evaluated in full, with controlled filesystem/host/page/auth dependencies. Authorization promise gates model a delayed operation without starting an authentication service or sleeping for real time. The actual handshake selector receives its real pre-authorization offsets and actual recorded acknowledgement entries. The logical delay test establishes sequencing and timestamp retention; it does not establish native browser timing or SDK behavior.

The earlier permitted native raw is `failed`, with completed cycles 1, 26, 51 and 71, and incomplete cycle 86 ending in `Timed out waiting for new SignalR handshake acknowledgement`. This supports retaining its result as a failed diagnostic. The new no-host sequence corrects the premature lookup window, but does not retroactively make that run pass or independently establish the current native five-category diagnostic outcome.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Daily resource and local-time token; reauthorization fixture | Source observation / controlled API count | No new timer or polling owner. Same-scope overlapping Daily callers share one request; scope changes create one latest follow-up. Existing time tick refreshes on local-day movement. Reauthorization merely moves handshake lookup after existing explicit authorization; no additional product HTTP, journal read/hash or subprocess loop introduced |
| Cache lifetime versus interval and refresh duration | Daily queue token, snapshot/recovery waits | Source observation / logical-clock result | Daily token contains journal+digest counters and day/repos/timezone/local date/selection epoch, with no TTL. Old-scope success/non401 failure returns without publication to allow newer queue work to drain. Lookup retains 15-second bound after authorization rather than consuming it during an operation allowed 30 seconds; all successful recovery gates remain 5 seconds from original handshake timestamps |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | RefreshQueue, revision pending work, fixture evidence | Source observation | One Daily key, one in-flight job and one latest desired follow-up; scope context is captured in scalar/local state rather than retained history. Reauthorization adds no collection/worker. Existing finite matrix/global deadline and per-invocation evidence histories remain; no long-lived memory measurement performed |
| Repeated full reads where a change check would do | Daily authoritative reads and handshake arrays | Source observation / controlled count | Same-scope duplicate reads remain eliminated. Old payloads are not published for new scope; required latest read remains. Handshake lookup searches retained accepted suffix after saved index, without restarting transport or rewriting timestamps. No server physical-I/O reduction is inferred |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | No-host proof and existing harness | Source observation | Proof runs Node via PATH and local TypeScript source transpilation, with no product executable/host/browser. Harness discovery and owned lifecycle remain unchanged; 30-second authorization/UI readiness bounds, 15-second lookup and global deadline remain. Native/provider/package variants were not executed here |

## Findings

**No findings** within the assigned second correction recheck and regression scope. ANALYSIS-640 through ANALYSIS-679 remain unused. No new suggestion or further correction cycle initiated.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-580 | Six-file frozen identity above | Original deferred day case and timezone/repos/local-date cases produce zero old-scope publications; loading/Reconnecting/pending retained until current response; same-scope join, non401 drain, real 401/503 and destruction controls | Verified corrected within source/no-host boundary; no false latest applied/Live acknowledgement observed |
| ORCH-009 | Same target; runner hash `e4a32aa9...` | Actual whole-cycle logical delayed-auth probe starts all three lookups only after reader→A-target and parallel B auth complete, preserves original acknowledgement timestamps; stale-only and explicit auth failure cases reject | Verified sequencing correction at method/source level; current native cycle 86 and full matrix acceptance remain developer-owned |
| ANALYSIS-560 / temporal regression | Same target | One Daily owner and one request for Snapshot/activation/manual overlap; actual local-day/DST refresh with explicit past day; unchanged controller/queue/tracker hashes | Prior duplicate-owner correction and temporal behavior retained in controlled probes |

## Assessment and limitations

The two assigned correction mechanisms pass independent source and actual-method checks. Stale Daily responses cannot publish into a newer context, the latest serialized work remains pending until it succeeds, and authorization/service failures retain their required semantics. Reauthorization handshake lookup no longer races ahead of the explicit authorization operation, while recorded transport timestamps and strict recovery limits remain intact.

This is source/no-host verification, not current browser, five-category, 100-cycle, full-suite, package, performance or native-agent acceptance. Supplied 315-test success is carried developer evidence only. No remaining source/no-host blocker or decision is identified in this bounded second recheck; the orchestrator owns final disposition and required runtime acceptance.

Only this report and ignored analysis-030 evidence were written. No product, tests, documentation, ledger, task state, prior report, raw input, client configuration or source selection changed. No host, browser, build or subagent started. Final target identity matched before/after; source released after final capture.
