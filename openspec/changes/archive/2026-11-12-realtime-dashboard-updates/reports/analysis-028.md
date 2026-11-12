# Analysis report: realtime-dashboard-updates / 028

## Review target

- Reviewer / role: ANALYSIS-RECHECK-028 / code-analyzer; ANALYSIS-560 correction and regression only, no suggestions.
- Client / version / mode / model (resolved ID): Codex CLI / `0.167.0` observed earlier in this session / Default delegated review / resolved model ID unavailable. Agent role `default`; no model override supplied.
- Date: 2026-11-11T10:32:47Z.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-028.
- Task and requirement references: [tasks](../tasks.md) 4.1, 4.2, 4.5, 5.4, 7.2; 6.1/6.2 temporal regression; [design](../design.md) resource ownership and stale response protection; original [analysis-027](analysis-027.md) / ANALYSIS-560.
- Scope: `dashboard/src/app.ts`, `dashboard/src/app.reliability.spec.ts`, `dashboard/src/live-connection.ts`, `dashboard/src/live-updates.ts`, `dashboard/src/app-live-test-helpers.ts`.
- Reading limits and paths read outside scope: role/project facts/template, tasks/design/architecture retained from preceding assignments. Previous own analysis-027 and ignored actual-method proof read. Additional reads: `dashboard/src/api.ts` for actual API error/date-boundary helpers, `dashboard/src/range.ts` for local helper context, `dashboard/package.json` and installed TypeScript used solely for transpilation. No current QA-025, current peer results, ledger, HANDOFF or orchestrator workflow read. No implementation/test/doc writes, build, browser or host allowed or performed.
- Baseline commit and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `pwsh -NoProfile -File scripts/review-identity.ps1 -Baseline 0d77685 -Paths dashboard/src/app.ts dashboard/src/app.reliability.spec.ts dashboard/src/live-connection.ts dashboard/src/live-updates.ts dashboard/src/app-live-test-helpers.ts`.
- Implementation identity: modified `app.ts`, `app.reliability.spec.ts`; three untracked files below. Tracked diff SHA-256 `7652a7629e38ad0050d7ff9bc16a9c5227dfacf7e5c1bb8dea59518adfec9e25`.
- Before/after identity: both `head=0d77685 baseline=0d77685 files=5 diff=7652a7629e38 untracked=3/3da791212782`. Full HEAD, baseline, manifest, tracked diff and untracked hashes match; no source mutation observed.
- Prior reports / rechecked findings: [analysis-027](analysis-027.md), ANALYSIS-560. New regression ANALYSIS-580 below; ANALYSIS-581 through ANALYSIS-599 unused.

| Untracked scoped path | SHA-256 |
| --- | --- |
| `dashboard/src/app-live-test-helpers.ts` | `0f57c6112a8ddce266144e1430daf13b0a918cfef061744d53147efface636d1` |
| `dashboard/src/live-connection.ts` | `78db24467ab95c0221d16c986c57ee9e59ff27a35a7140cfd032228b6783e5ac` |
| `dashboard/src/live-updates.ts` | `1430c9e42ae7790af48e9c48af8eefbd6decf65faf94f085b0f7c5319ee16d54` |

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Original duplicate-owner mechanism | Inspect actual methods and rerun original method path with fixed expectations | `app.ts:699-795,1206-1237`; ignored proof/results below | Initial both-domain Snapshot now starts one Daily request, returns true with current data, clears pending domains and both queue states. Original ANALYSIS-560 mechanism corrected |
| Same-scope overlap | Actual deferred Snapshot plus simultaneous actual activation/manual refresh | One request throughout, maximum one in flight, repeated checkpoint adds no read | Passed independently. Same-scope callers join without invalidating one another |
| Journal/digest dependency union | Actual `receiveRevision` with sequential journal-only, digest-only and combined changes | Same `daily-digest` resource and token includes both counters | Passed: one Daily read per target, pending empty and status Live after successful responses |
| Real failures and explicit retry | Actual API helper receives controlled HTTP 503, then unchanged vector, then forced Snapshot retry | First failure returns false through actual queue/work path; both journal/digests pending, Daily queue dirty, status paused, no fabricated data | Passed: unchanged vector does not blind-retry; explicit forced retry recovers. Failure semantics were not weakened to hide the original defect |
| Inactive view / activation | Actual revision processing while History active, then switch to Daily and activate | No Daily reads while inactive; one current-scope read on activation | Passed independently |
| Pre-live direct load | Actual `refreshDaily` with no live controller | One direct read succeeds | Preserved |
| Local day / DST / selected past day | Actual `dailyScope`, `timeChanged`, `refreshDaily`, actual `api.ts` localDate/boundary; controlled clock across Warsaw's 23-hour day | Before boundary changes from `2026-03-28T23:00:00.000Z` to `2026-03-29T22:00:00.000Z`; explicit day remains selected | Passed independently for sequential live-queue rollover; one new read, subsequent same-day call cached. Existing midnight unit uses pre-live direct loading, so this probe additionally exercises the live queue |
| New explicit scope while request pending | Actual `receiveRevision`, then new chosen day/selection epoch and actual `activateLiveScope`, then settle older response | Source `loadDaily` checks only request epoch; new queued scope has not started/incremented it | ANALYSIS-580 reproduced: old successful response publishes data for previous day under newer selected day before follow-up starts. Eventual follow-up succeeds but does not erase the stale publication |
| Outer guards and thresholds | Inspect revision drain, reconciliation selection/auth/destruction checks, route activation, unchanged controller | Existing outer guards remain; no stale original generation is marked applied before follow-up. Live connection and recovery-limit contracts unchanged | Guards prevent false settled acknowledgement but do not guard Daily data publication in 580. No five-second timing threshold changed |
| Developer unit coverage | Inspect added tests around `app.reliability.spec.ts:566-695` and temporal cases | Deferred initial/activation, isolated/mixed revisions, 503/pending/paused, inactive activation assertions | Coverage is meaningful for original mechanism. No deferred chosen-day/repository-scope supersession case found in the correction tests |
| Independent bounded check | `node artifacts/realtime-dashboard-updates/reviews/analysis-028/daily-owner-recheck.mjs` | Exit 0; [proof](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-028/daily-owner-recheck.mjs), [results](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-028/daily-owner-results.json) | Six positive/control groups pass; negative scope probe explicitly records reproduced regression. Exit 0 means the evidence assertions completed, not that the product regression passed |
| Supplied full checks | Parent supplies 310/310 dashboard unit tests and Release build with zero warnings/errors | Supplied execution only | Not independently rerun; no inference of current native five-category or full matrix acceptance |
| Stability / whitespace | Before/after identity; scoped `git diff --check` | Matching identity; whitespace exit 0 with existing line-ending warning for reliability spec | Passed. Tracked-diff whitespace command does not inspect untracked files |

The independent proof transpiles extracted actual App methods and unchanged actual RefreshQueue/RevisionTracker, and uses the actual API/error/date helpers with controlled fetch responses. Other domain reads are successful stubs to isolate Daily ownership. It starts no service, browser, native agent or build and does not copy a replacement queue implementation.

The original analysis-027 reproduction changes from two concurrent reads plus false reconciliation to one read plus successful reconciliation. Its basic defect is fixed. The negative case then tests the behavior that serialization must preserve: a request for selected day `2026-01-01` is deferred; selection changes to `2025-12-31`, increments the actual selection epoch and invokes activation. Activation correctly queues the new scope without a second in-flight request. Settling the older response calls `dailyData.set` with returned day `2026-01-01` while selected day is already `2025-12-31`. The next request targets `2025-12-31`, and eventual final data is correct. The evidence captures the mismatched publication while that follow-up is outstanding, with maximum concurrency one.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Daily queue and existing local-time timer | Source observation / bounded call measurement | No new interval or background owner. Combined journal/digest reconciliation now issues one Daily read; same-scope Snapshot/activation/manual overlap also one, measured by controlled API calls. Unchanged target/local-day calls add no request. Existing time safety tick remains at most one minute and refreshes Daily when local day changes; no periodic catalog/document REST polling or subprocess work introduced |
| Cache lifetime versus interval and refresh duration | Daily resource token and request epoch | Source observation / controlled result | Daily token depends on journal+digests, selected day/repos/timezone and current local date; it has no TTL. Identical target/scope joins/cache-reuses; changed scope becomes one follow-up. New local-day token makes legitimate date refresh possible without changing selected past day. Serialization delays the next request's epoch increment, exposing 580 while prior request completes |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Existing fixed resource queue and revision pending state | Source observation | Reuses one Daily key, one in-flight read, one latest desired follow-up and existing latest revision work; no per-day key map or event history added. Independent probe observes concurrency one. No long-duration memory or whole-app growth measurement performed |
| Repeated full reads where a change check would do | Daily authoritative API reads | Source observation / controlled count | Removes duplicate identical reads from journal/digest paths. Selected new scope still needs one authoritative follow-up; suppressing stale publication must retain it. No journal/document body read or server physical-I/O claim is made from browser-side request counts |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | No-host proof and unchanged app | Source observation | Proof uses Node via PATH and local installed TypeScript for source evaluation; no child service or framework build. App transport controller is unchanged. No new executable discovery, timeout or package behavior introduced; native recovery timing remains developer-owned |

## Findings

### ANALYSIS-580: A queued Daily scope change permits the old in-flight response to publish into the new selected day

- Severity: warning; regression in stale response ownership introduced by serializing Daily refreshes.
- Related task / requirement: 4.1 current selection epochs/stale completions, 4.2 correct scoped Daily refresh, 5.4 retained authoritative state; regression of ANALYSIS-560 correction.
- Locations at reviewed identity: `dashboard/src/app.ts:751-767` (scope token and serialized Daily queue), `:779-795` (activation), `:1210-1224` (Daily publication guarded only by `dailyEpoch`), `:1643-1655,1711` (route changes selection/day and activates instead of directly starting the live read). Existing outer selection guard is `:732-741`.
- Observation: Changing selected day while a Daily request runs now enqueues the next scope instead of starting it immediately. The next request does not increment `dailyEpoch` until the old read has returned. The old response therefore passes `epoch === dailyEpoch` and updates `dailyData` using its previous-day payload even though the selected day has changed. The outer selection guard rejects reconciliation after that publication, but cannot undo it. A subsequent correct read is eventual recovery, not protection against stale publication.
- Independent reproduction: actual method probe starts `receiveRevision` for day `2026-01-01`; changes selected day to `2025-12-31`, increments selection epoch and calls actual activation; resolves old response. Evidence records selected day `2025-12-31` with published digest day `2026-01-01` while follow-up for the new day is pending. Both read concurrency and final eventual data are otherwise correct.
- Proposed correction and benefit: Preserve the serialized single owner while associating response publication/error handling with the requested Daily scope, including the explicit selection and relevant context. A scope that is no longer current must not publish its returned data or acknowledge current dirty work; the latest queued scope must still execute and settle. Same-scope duplicate callers must continue joining without invalidating the shared request. Add a deferred scope-change regression demonstrating absence of the stale publication, not only eventual final correctness.
- Behavior-preservation risks: Returning false for stale work alone can stop the queue before its newer desired job; ensure the current follow-up is drained/retried under the existing bounded ownership contract. Do not turn real 503/authorization failures into success, drop pending revisions, start parallel Daily reads, change selected day to match an old payload, weaken the Live prerequisite or alter timing thresholds. Retain local-day rollover, inactive activation, repository/timezone scope and current auth/destruction guards.
- Evidence type: independent deterministic execution of actual current methods/API/date helpers and actual queue/tracker; source observation. No native/browser claim.
- Verification plan: Hold a first request, change the selected day and trigger activation, resolve first, assert no old-scope publication at any point; then resolve latest and assert correct scope and truthful completion. Include same-scope joins, changed repository/day context, real failure, and temporal rollover controls. Follow with developer-owned browser/five-category acceptance.
- Relationship: New regression adjacent to ANALYSIS-560; original duplicate-read/false-result mechanism is corrected. No unrelated suggestion raised.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-560 | Frozen five-file identity above | Original actual-method path now makes one request and returns true; activation/manual overlap shares it; isolated and mixed revisions pass; real 503 stays false/paused/pending | Original mechanism verified corrected within source/no-host boundary; complete acceptance withheld for 580 |
| ANALYSIS-580 | Same target | Bounded chosen-day supersession probe captures stale publication under new selection before eventual correct follow-up | Still failing; new required regression correction for orchestrator triage |
| 6.1/6.2 regression | Same target | Actual queued Daily local-day token/date helpers across Warsaw DST boundary; past day retained and unchanged day does not reread | Passed in sequential controlled rollover; overlapping temporal-scope invalidation shares 580's ownership concern |

## Assessment and limitations

The correction removes the original two-owner failure and preserves real failure/pending behavior, single-flight work and legitimate time refreshes. It introduces a concrete stale publication window when the requested Daily scope changes before an existing read completes. That regression requires correction before accepting the bundle. No claim is made that the current five-category diagnostic failed or passed; its execution belongs to the developer and was not inspected here.

Only this report and ignored analysis-028 evidence were written. No product, tests, documentation, prior report, raw input, ledger, task state, client settings or source selection changed. No subagent, build, host or browser was started. Before/after source identity matched exactly; source released after final capture. Orchestrator owns triage and finding closure; developer owns implementation and native acceptance.
