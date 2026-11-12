# Analysis report: realtime-dashboard-updates / fix-tier recheck of ANALYSIS-800, ANALYSIS-801 and the retryFailed residual

> Saved by the orchestrator as a reviewer-refused-write fallback (the client refused the reviewer's write). Condensed only in formatting; verdicts, findings, locations and evidence are unchanged.

## Review target

- Reviewer / role: independent analyzer subagent / code-analyzer
- Client / version / mode / model: Claude Code 2.1.322, subagent (non-interactive), claude-opus-5-5
- Date: 2026-11-12T18:34Z
- Assignment: fix-tier recheck of ANALYSIS-800, ANALYSIS-801 (user chose option A) and the `wasDeferred`/`retryFailed` residual from analysis-036, with a specific judgement on patching `WritableSignal.set`
- Scope: `git diff 6f462ce` for `dashboard/src`, `dashboard/e2e`, `docs/dashboard-validation.md`, plus untracked `dashboard/src/app.live-status.spec.ts`. Read-only outside scope: Angular 21.2.25 signal factory (`dashboard/node_modules/@angular/core/fesm2022/_effect-chunk2.mjs` 2723-2745, `_effect-chunk.mjs` 340-379).
- Baseline commit: 6f462ce
- Identity before and after (identical): `head=6f462ce baseline=6f462ce files=10 diff=5cc34179e3a8 untracked=1/0f489d533de0` (tracked diff `5cc34179e3a86735b74b39417ff872ad1c61b4e83bbebe7f389c3fe555ee6fe6`; untracked `app.live-status.spec.ts` `f831c74a5271d1c963878c10f8e8c6aeb5b365a13475f4846433c20e202599d9`)

## Analysis performed

| Area | Evidence | Outcome |
| --- | --- | --- |
| 800 purity | `presentedLiveStatus` (`app.ts` 653-657) is a pure computed over `liveStatus`, `reconnectingLong`, `steadyStatus` | Fixed |
| 800 synchronous steady state | Patched `liveStatus` (`app.ts` 152-162) sets `steadyStatus` on every non-Reconnecting `set`; production writes at 618 and 689 | Correct for current callers; mechanism judged in ANALYSIS-810 |
| Patch soundness | Angular `signal()` attaches `set`, `update`, `asReadonly` as separate closures; `update` calls `signalSetFn` on the node directly, bypassing a reassigned `set` | Works today, fragile (ANALYSIS-810) |
| 801 header | `app.html` 86-89 and `liveIndicatorLabel()` -> `presentedLabel()` (`app.ts` 668-670) | Fixed; `liveStatusLabel()` (661-663) now unused |
| Residual retryFailed | `app.ts` 772 passes `forcePending \|\| wasDeferred` | Fixed |
| Tests | 800: paused then reconnecting without render keeps reader `paused`; 801: header stays Live within grace, Reconnecting after; residual: failed load retried on first visible apply after deferral | Guard each fix (judged from source); nothing guards the `update` bypass |
| Checks | `npm test --prefix dashboard`: 23 files, 351 tests | Passed |

## Steady-state cost and bounds

Unchanged: one 2 s timer only while Reconnecting; `steadyStatus` is a single value; one forced reconcile per return to the tab; failed tokens retried once, no loop; no external tools.

## Findings

### ANALYSIS-810 (suggestion): `steadyStatus` depends on a reassigned `WritableSignal.set`, which `update` bypasses

- Location: `dashboard/src/app.ts` 152-162
- Observation: a future `liveStatus.update(...)` (or `linkedSignal`/`model()`/store refactor) would bypass the patch and reintroduce ANALYSIS-800; the coupling is hidden and reassigning framework methods is not a documented extension point. Nothing is broken today: all production and 81 spec writes use `.set`.
- Rejected alternatives: `linkedSignal` and `effect` are lazy and miss intermediate states, reintroducing ANALYSIS-800. Only a synchronous write path is correct.
- Required change: (1) replace 152-162 with private `steadyStatus` and `liveStatusState` signals, public `readonly liveStatus = this.liveStatusState.asReadonly()`, and `setLiveStatus(value)` that sets `steadyStatus` when `value !== "reconnecting"` and then `liveStatusState`; (2) production writes at 618 and 689 call `setLiveStatus`; (3) spec files replace `app.liveStatus.set(` with `app.setLiveStatus(` (including `fakeLive`), so the type checker rejects any bypass; (4) remove unused `liveStatusLabel()`.
- Fallback (not recommended): also route `update` through the patch and add a test.
- Risk: none at runtime.
- Verification: unit suite stays 351/351; type check rejects any remaining `liveStatus.set`/`update`.

## Re-verification

| Finding | Outcome |
| --- | --- |
| ANALYSIS-800 | verified (mechanism follow-up ANALYSIS-810) |
| ANALYSIS-801 | verified |
| analysis-036 retryFailed residual | verified |

## Assessment and limitations

All three rechecked items are fixed and guarded; unit suite 351/351. ANALYSIS-810 is a maintainability follow-up with an exact minimal change. Not performed: e2e (not needed) and failing-before re-runs (need reverts outside this role).
