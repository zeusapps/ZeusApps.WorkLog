# Analysis report: realtime-dashboard-updates / fix-tier recheck of ANALYSIS-780, -781, -782 and QA-650

> Saved by the orchestrator as a reviewer-refused-write fallback (the client refused the reviewer's write). The content is the reviewer's inline return, condensed only in formatting; verdicts, findings, locations and evidence are unchanged.

## Review target

- Reviewer / role: independent analyzer subagent / code-analyzer
- Client / version / mode / model: Claude Code 2.1.322, subagent (non-interactive), claude-opus-5-5
- Date: 2026-11-12T18:20Z
- Assignment: fix-tier recheck of ANALYSIS-780, -781, -782 (`analysis-035.md`) and QA-650; task 9.2; requirement "Live connection health and recovery"
- Scope: `git diff 6f462ce` for `dashboard/src`, `dashboard/e2e`, `docs/dashboard-validation.md`, plus untracked `dashboard/src/app.live-status.spec.ts`; neighbor read `RefreshQueue.enqueue` in `dashboard/src/live-updates.ts`
- Reading limits: findings, new diff and test evidence only; QA report not read
- Baseline commit: 6f462ce
- Identity before and after (identical): `head=6f462ce baseline=6f462ce files=10 diff=baf7c510f24e untracked=1/e0017e7fdcf1` (tracked diff `baf7c510f24eb5a2f07da6e28faf1b9d8da2e0832ccd4bdfdf67563e6a85ed88`; untracked `app.live-status.spec.ts` `da17b347fb1b38f6d8a7dca9bd44039ea43dcc3f6a9fcb3a019362410d4260a1`)

## Analysis performed

| Area | Evidence | Outcome |
| --- | --- | --- |
| 780 counter reset | `live-connection.ts` 209-215 resets `authorizationProbeUsed` and `outageAttempts`, then `markDeferred()`, `return true` | Fixed |
| 781 neutral deferral | `markDeferred` (113-115) sets `connecting` only when connected, authorized and not disposed; called from the controller deferral branch and `receiveRevision` (`app.ts` 687) | Fixed; no real outage masked |
| 781 focus before visibility | `wasDeferred` (`app.ts` 743, 760) lets the first visible apply after deferral proceed | Fixed (residual note below) |
| 782 / QA-650 presented status | `presentedLiveStatus` (`app.ts` 643-651) bound to page bar, reader strip, reader dot, reader Retry; `.reader-meta .live-dot[data-state="connecting"]` added | Fixed (purity follow-up ANALYSIS-800) |
| Double announcements | One live region at a time; routine reconcile changes no live-region text | No regression |
| Header vs presented | `app.html` 86 binds the header to raw `liveStatus()` | ANALYSIS-801 |
| Paused to Retry | Presented status stays `paused` during the grace period; Retry disabled "Retrying…" | Acceptable |
| Tests | 780: six deferred closes, no `paused`, no delays; 781: Live + hidden revision -> `connecting` -> visible -> `live`, focus-first apply returns true; 782: reader holds Live under grace, Reconnecting after, paused immediate, connecting dot | Guards each fix (judged from source; not re-run with reverts). Gap: no controller test that `markDeferred` is a no-op while disconnected |
| Checks | `npm test --prefix dashboard`: 23 files, 348 tests | Passed; e2e not run |

## Steady-state cost and bounds

Unchanged from analysis-035: one 2 s timer only while Reconnecting, no new interval; deferral does no I/O and enqueues nothing; state is scalar; one reconcile of pending domains per return to the tab; no external tools.

## Findings

### ANALYSIS-800 (suggestion): `presentedLiveStatus` stores its steady state as a side effect of a lazy computed

- Location: `dashboard/src/app.ts` 643-651 (`presentedLiveStatus`, `steadyStatus`)
- Observation: the computed writes `this.steadyStatus` only when the template evaluates it, so it records only rendered steady statuses. A transition through `paused` to `reconnecting` with no change detection between them (for example a throttled hidden tab followed by `resume()`) makes the bar and reader briefly show Live after a pause. Computeds should be pure.
- Proposal: track the steady status as a signal written synchronously in the `onStatus` callback whenever the value is not `reconnecting`; make `presentedLiveStatus` a pure function of `liveStatus`, `steadyStatus` and `reconnectingLong`.
- Risk: none for tested transitions.
- Evidence: source observation; real-world frequency a hypothesis.
- Verification: reader open; `live` + detectChanges; `paused` then `reconnecting` without detectChanges; detectChanges; reader shows `paused`. Fails on current code.

### ANALYSIS-801 (suggestion, needs a product/spec decision): the header indicator still shows Reconnecting during routine reconciles

- Locations: `dashboard/src/app.html` 86 (`[attr.data-state]="liveStatus()"`), `liveIndicatorLabel()`, and the "does not show a Reconnecting bar …" test in `app.live-status.spec.ts`
- Observation: on every visible revision the header dot turns amber and its tooltip/accessible name read "Reconnecting · …" for the reconcile, while bar and reader hold Live. Not a live region, so no a11y regression, but the surfaces disagree and the general spec sentence forbids presenting Reconnecting for routine reconciles; the scenario wording is ambiguous.
- Options: bind the header to `presentedLiveStatus()` (one rule everywhere; very short real reconnects also hidden, consistent with the bar), or narrow the spec sentence to page and reader surfaces.
- Relationship: related to ORCH-021 and ANALYSIS-782; not introduced by this round.

## Re-verification

| Finding | Outcome |
| --- | --- |
| ANALYSIS-780 | verified |
| ANALYSIS-781 | verified (real browser event order not measured) |
| ANALYSIS-782 | verified (purity follow-up ANALYSIS-800) |
| QA-650 | verified |

## Assessment and limitations

The four rechecked defects are fixed and guarded; unit suite 348/348; no announcement regression; a real hidden outage is not masked as connecting. Residual risk (not a finding): a `wasDeferred` apply calls `reconcileDomains` with `retryFailed = false`, so a page already paused on the same revision before hiding returns to Updates paused rather than retrying when focus precedes visibility; passing `forcePending || wasDeferred` as `retryFailed` would make both orders behave the same. Not performed: e2e (optional) and failing-before re-runs (need reverts outside this role). ANALYSIS-801 needs a decision.
