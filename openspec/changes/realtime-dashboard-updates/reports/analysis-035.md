# Analysis report: realtime-dashboard-updates / milestone review of task group 9

> Saved by the orchestrator as a reviewer-refused-write fallback (the client refused the reviewer's write). The content is the reviewer's inline return, condensed only in formatting; findings, locations and evidence are unchanged.

## Review target

- Reviewer / role: independent analyzer subagent / code-analyzer
- Client / version / mode / model: Claude Code 2.1.322, subagent (non-interactive), claude-opus-5-5
- Date: 2026-11-12T18:07Z
- Change root / assignment: `openspec/changes/realtime-dashboard-updates`, milestone review of task group 9 (ORCH-021, ORCH-023, DEV-044, DEV-045)
- References: tasks 9.1-9.3; requirement "Live connection health and recovery" in `specs/work-dashboard/spec.md`; `design.md` section 5
- Scope: `git diff 6f462ce` for `dashboard/src`, `dashboard/e2e`, `docs/dashboard-validation.md`, the change's `specs`, `design.md`, `tasks.md`, plus untracked `dashboard/src/app.live-status.spec.ts`. Neighboring code read: `dashboard/src/live-connection.ts`; revision, activation, visibility and reader-template paths of `app.ts`/`app.html`; live styles in `styles.css`.
- Reading limits: QA report not read; Playwright not run. Outside scope, only `dashboard/package.json` and the `reports/` listing.
- Baseline commit: 6f462ce
- Identity before and after (identical): `head=6f462ce baseline=6f462ce files=13 diff=902ee00d0041 untracked=1/1ba755cd9232` (tracked diff `902ee00d004139835e3171c533e8d67bc0e46ddccdb03bb015b8a6ce71f66fe1`; untracked `app.live-status.spec.ts` `547979b7a287c387058217776f202cd49d1cd12351c6bae22a88247d21a4174b`).
- Prior report: `analysis-034.md`. No rechecks assigned. ANALYSIS-703/704 out of scope and not worsened.

## Analysis performed

| Area | Method | Evidence | Outcome |
| --- | --- | --- | --- |
| State machine | Traced every `setStatus` path in `live-connection.ts` with the new `established` flag | `interruptedStatus()` replaces former `"reconnecting"`; `established` set on first `live`, never reset | Correct for visible tabs; initial failure still ends paused (143-146, 160) |
| Deferred path | Traced `onSnapshot` -> `applyRevisionSnapshotNow` -> `"deferred"` -> `snapshotAndApply`, and `receiveRevision` while hidden | `deferredWhileHidden` reset at each apply; `pendingRevision` merging consistent | Defects ANALYSIS-780, ANALYSIS-781 |
| Grace timer lifecycle | Constructor `effect` (app.ts 439-452) vs Angular 21.2 effect behavior | Timer only for `reconnecting`; `onCleanup` clears on status change and destroy | No leak |
| Announcer | `announceLive`, `liveAnnounced` | Live announced only after announced paused/long Reconnecting | Correct for page region; reader region differs (ANALYSIS-782) |
| Race: first revision vs activation (ORCH-023) | `routeChanged` -> `activateLiveScope` with no target vs concurrent first Snapshot | No-target branch outside `refreshQueue`; `joined()` supersession; never calls `markReconciled` | Live not claimed early; possible duplicate activity load (pre-existing) |
| Race: visibility | `visibilityChanged` and `windowFocused` after deferral | See ANALYSIS-781 | Event-order hypothesis |
| Duplication | Header indicator, page bar, announcer, reader strip | Page uses `liveBarVisible`/`announceLive`; reader binds raw `liveStatus()` | ANALYSIS-782 |
| Test strength | `app.live-status.spec.ts`, `live-connection.spec.ts`, `app.reliability.spec.ts` | ORCH-023 tests guard the branch; DEV-044 tests cover only hidden start from `connecting` | Gaps under 780/781 |
| Checks | `npm test --prefix dashboard` | 23 files, 343 tests | Passed; e2e not run by assignment |
| Architecture | vs design section 5 single lifecycle owner | Status ownership stays in `LiveConnectionController` | No drift |

## Steady-state cost and bounds

- Idle work: one 2 s `setTimeout` only while Reconnecting; no new interval or poll. The no-target branch runs on route changes and visibility return only; bounded by user actions.
- Cache/refresh: no-target branch bypasses `refreshQueue` (no token yet); `joined()` prevents stale publication.
- Unbounded state: none; `outageAttempts` is bounded but grows wrongly across deferred reconnects (ANALYSIS-780).
- Repeated full reads: the first forced snapshot after a hidden launch repeats the initial load, as a visible launch already does.
- External tool discovery: not applicable.

## Findings

### ANALYSIS-780 (warning): Reconnects while hidden use up the outage attempts until updates pause without trying

- Locations: `dashboard/src/live-connection.ts` 127-147 (`connectWithRetries`), 195-211 (`snapshotAndApply`; 204 `if (success === "deferred") return true;`), 101-106 (`markReconciled`), 80 (`resume`).
- Observation: a `"deferred"` snapshot returns before the success-only resets of `outageAttempts` and `authorizationProbeUsed`. While hidden nothing else resets them, so each further close while hidden starts at attempt n+1 with growing backoff; after about five closes the retry loop is skipped and status becomes `paused` without an attempt. `authorizationProbeUsed` also stays set.
- Proposal: in `snapshotAndApply`, reset `authorizationProbeUsed` and `outageAttempts` before returning on `"deferred"`.
- Risk: none for visible tabs.
- Evidence: source observation; hidden-tab disconnect frequency is a hypothesis.
- Verification: controller test with `onSnapshot: async () => "deferred"`; close six times; every recovery calls `start()`, no `"paused"`, delays do not grow. Should fail on current code.

### ANALYSIS-781 (warning): Deferral keeps the previous status, so Live or a Reconnecting bar shows over unreconciled data

- Locations: `dashboard/src/app.ts` 663-673 (`receiveRevision` skips `markReconciling` while hidden), 737-741 (deferral), 742 (`!change.changed && !forcePending`), 2754-2768 (`visibilityChanged`, `windowFocused`); `live-connection.ts` 204.
- Observation: deferral changes no status; it is neutral only when already `connecting` (the only tested case). (1) Live, then a revision while hidden: status stays `live` with pending domains; the catch-up on return reconciles without `markReconciling`, and a failure jumps Live -> paused. (2) Lost while hidden, reconnect succeeds but snapshot deferred: status stays `reconnecting`; the grace timer raises the bar and announces while hidden, contrary to the spec. (3) Hypothesis: if `window:focus` runs `resume` before `visibilitychange` sets `forceNextSnapshot`, line 742 returns `false` while visible and the controller pauses.
- Proposal: give deferral an explicit status (`markDeferred()` setting `connecting` while connected, called from `receiveRevision` when hidden and from `snapshotAndApply` on `"deferred"`, or `markReconciling()` in `visibilityChanged` when `deferredWhileHidden`), and treat the next visible apply after a deferral as forced (e.g. `if (!change.changed && !forcePending && !wasDeferred) return false`).
- Risk: Live -> connecting -> Live stays silent under current announcer rules. Keep Reconnecting for a real unrecovered outage.
- Evidence: source observation for (1)/(2); hypothesis for (3).
- Verification: unit test live + hidden + newer journal revision -> not `live` or marks reconciling on return; unit test `windowFocused()` before `visibilityChanged()` -> no pause; extend `app.live-status.spec.ts` 136-159 to switch visibility and assert Live.

### ANALYSIS-782 (suggestion): The reader status display ignores the grace-period and connecting rules

- Locations: `dashboard/src/app.html` 1042-1046 (`.reader-meta .live-dot`, `.reader-live` role=status bind raw `liveStatus()`); `dashboard/src/styles.css` 1618-1628 (no `connecting` rule, falls back to Live green), 1629-1642.
- Observation: the reader strip renders and announces every raw state, so routine reconciles flash and announce "Reconnecting" inside the reader (ORCH-021 behavior); while `connecting`, the reader dot is Live green with "Connecting…" text.
- Proposal: one computed `presentedLiveStatus` (keeps the previous value during Reconnecting until `reconnectingLong`) bound by reader strip, reader dot and page bar; add a `connecting` color rule for `.reader-meta .live-dot`.
- Risk: reader paused/Retry must stay immediate; reliability tests assert reader `data-state` per raw state (~1530-1545).
- Verification: unit test, reader open, live -> reconnecting -> live within the grace period: reader never shows Reconnecting; dot non-Live while connecting.
- Relationship: related to ORCH-021; partly pre-existing.

## Assessment and limitations

Connecting/reconnecting split, grace-timer lifecycle and the ORCH-023 activation branch are correct for visible tabs and leave nothing running after destroy; unit suite 343/343. The hidden-tab deferral works for a hidden launch but has gaps ANALYSIS-780 and ANALYSIS-781; ANALYSIS-782 is a consistency cleanup. The ORCH-023 tests guard the branch but do not reproduce the reported symptom, which needs the reporter's retest. Not performed: Playwright e2e (by assignment), browser measurement of hidden-tab disconnects and focus/visibility event order.
