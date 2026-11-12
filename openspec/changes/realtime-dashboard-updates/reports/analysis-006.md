# Analysis report: realtime-dashboard-updates / 006

## Review target

- Reviewer / role: independent code-analyzer, ANALYSIS-RECHECK-006.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime-resolved model introspection unavailable.
- Date: 2026-11-08.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-006.
- Task references: [tasks 4.1–5.4](../tasks.md), [reader delta](../specs/openspec-browser/spec.md), [dashboard delta](../specs/work-dashboard/spec.md), and [analysis 004](analysis-004.md) findings ANALYSIS-080–084.
- Scope: `dashboard/src`, `dashboard/package.json`, `dashboard/package-lock.json`; recheck assigned findings plus regression only. Harness corrections are concurrent and excluded. No backend recheck or new suggestion is included.
- Reading limits / neighboring reads: original analysis 004, prior in-memory probe, current scoped modules/App methods and added regression cases. Concurrent QA 006 conclusions were not read. No new outside implementation dependency was reviewed.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths dashboard/src dashboard/package.json dashboard/package-lock.json`.
- Manifest: 14 files. Modified tracked files: package manifests; `app.daily.spec.ts`, `app.html`, `app.reliability.spec.ts`, `app.reviews.spec.ts`, `app.spec.ts`, `app.ts`, `styles.css`. Untracked: `app-live-test-helpers.ts`, `live-connection.ts`, `live-connection.spec.ts`, `live-updates.ts`, `live-updates.spec.ts`.
- Tracked diff SHA-256: `a82b70b292d1b1ae148d76dd96e7590ae106a706f978bff5dce2b5647d9219ad`.
- Untracked hashes:

| File under `dashboard/src` | SHA-256 |
| --- | --- |
| `app-live-test-helpers.ts` | `0f57c6112a8ddce266144e1430daf13b0a918cfef061744d53147efface636d1` |
| `live-connection.spec.ts` | `2e585da6b094def70d4c6cbeeef25e4e269ee45f7ff842aba4de7fced0b726e5` |
| `live-connection.ts` | `e92efc945b51d722000682dc70f6cae5e478a9046ed28a1edaaf669c27ed5912` |
| `live-updates.spec.ts` | `3aa4f02d20aaf0919e32076f05375394b5f714940d2cd24fff25fb3ad0f00119` |
| `live-updates.ts` | `1430c9e42ae7790af48e9c48af8eefbd6decf65faf94f085b0f7c5319ee16d54` |

- Before/after identity: both match `head=0d77685 baseline=0d77685 files=14 diff=a82b70b292d1 untracked=5/909fd9f63732`. No scoped mutation detected.
- Finding allocation: existing ANALYSIS-080–084; new regression range ANALYSIS-140–159. Only ANALYSIS-140 is allocated; no suggestion is raised.

## Analysis performed

| Area | Method / evidence | Outcome / limitation |
| --- | --- | --- |
| Original close-during-Snapshot case | Current controller transpiled with fake transport and deferred first Snapshot callback | Corrected mechanism reproduced: two starts/two reconciliations, final Live only after recovery. New repeated-close budget regression is ANALYSIS-140 |
| Authorization during backoff | Current controller with deferred retry delay | Original case corrected: one start, final Paused. Stop-await authorization race remains ANALYSIS-081 |
| Queue clear | Current `RefreshQueue` with deferred job and one queued follow-up | Original callback-after-clear corrected: zero follow-ups. App pagination remains active after destruction, ANALYSIS-082 |
| Navigation supersession | Extract current production reconciliation methods verbatim through the TypeScript AST and run with fake signals/loaders plus real tracker/queue | Reads-domain supersession has a follow-up mechanism; documents-only supersession remains pending across an identical checkpoint, ANALYSIS-083 |
| Focus/scroll preservation | Inspect new focus/scroll checks; execute current baseline refresh/helpers against a fake DOM with newer focus before its restore timer | Focus changed during the request is guarded; focus changed after the response and before the callback is still overwritten, ANALYSIS-084. Scroll guard is source-verified only |
| Proportional static checks | Scoped whitespace check, probe syntax, identity before/after and category-only Markdown hygiene | Passed. No unit suite, browser, loopback host or production package was started; no approval prompt issued |
| Supplied execution | Developer handoff | Dashboard suite reportedly passes 297/297; this reviewer did not rerun it, and it does not cover all remaining cases below |

The bounded independent reproduction is `node artifacts/realtime-dashboard-updates/reviews/analysis-006/recheck-probes.cjs` from the repository root. [Probe source](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-006/recheck-probes.cjs) and [result](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-006/recheck-results.json) are ignored analysis evidence. The controller/queue are the transpiled current modules with fake imports. App probes use selected current production methods and helpers, extracted without changing their bodies, with fake API/DOM/state dependencies; they do not instantiate Angular or establish real SignalR/browser behavior.

Observed result summary:

| Probe | Result |
| --- | --- |
| Original close during Snapshot | Starts 2; Snapshot callbacks 2; statuses Paused, Reconnecting, Live |
| Original 401 during backoff | Starts 1; final Paused |
| Original queue clear | Follow-ups after clear 0 |
| 401 while awaiting failed-start stop | Automatic classification probe after confirmed loss 1 |
| Repeated close before first successful reconciliation | Starts before first Live 11; Snapshot callbacks 11 |
| Documents-only navigation supersession | Pending domain `documents`; catalog load count remains 1 after identical checkpoint |
| Destroy during Ongoing pagination | Two further API calls after destruction: next timeline page and unlinked observations |
| New input focused before baseline restore timer | Final focus is replacement passage, overwriting the newer input |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Client revision/connection handlers | Source observation | No new periodic data poll was introduced by these fixes. Recovery intent is retained across active work, and epoch guards reject old Snapshot completions. Repeated closes before a healthy reconciliation reset the retry loop's counter, permitting an unbounded recovery chain (ANALYSIS-140); no actual idle/network benchmark was run |
| Cache lifetime versus interval and duration | Revision tracker and resource queue | Source observation | Successful tokens remain cached, failures remain pending. Reads supersession can force current-scope reconciliation, but the trigger depends on a baseline-specific counter and misses documents-only work (ANALYSIS-083) |
| Unbounded state | Recovery intent, queue cancellation and foreground waiters | Source observation | One recovery boolean/error and one lifecycle operation; queue clear cancels existing state objects, discards desired jobs and permits fresh queue use. App destruction releases foreground waiters and discards pending revision state. A repeated-close chain is unbounded in attempts, not parallel queue length. Ongoing pagination still continues after destruction |
| Repeated full reads where a change check would do | Current-scope follow-ups and baseline render | Source observation | Benign reads supersession can request one forced current-scope follow-up. No broad background polling was added. Baseline refresh preserves current scroll at response time, with a later scroll-epoch check; its focus eligibility is captured too early. No rendering-cost measurement was performed |
| External tool discovery / timeouts | Controller deadlines and analysis probe | Source observation | Product external tool lookup is unchanged and outside this target. Existing controller timeout still bounds start/Snapshot races; fake probe uses only local TypeScript transpilation and bounded in-memory promises. It starts no executable product, server or network request |

## Findings and remaining recheck cases

### ANALYSIS-081: An authorization classification read can still start after confirmed loss during stop

- Severity: **warning**; existing finding remains open for another awaited step in the same recovery path.
- Related tasks / requirements: 5.2 and 5.3; stop automatic connection/protected refresh work on confirmed 401.
- Locations: `dashboard/src/live-connection.ts:133`–`:139` and `:211`–`:221`.
- Corrected portion: retry-delay and successful start/Snapshot paths now validate lifecycle epoch/authorization. The original backoff and successful-reconciliation cases are covered by new regressions.
- Remaining reproduction: fail start with a generic network error; defer `stopConnectionForRetry`; call `authorizationLost` while that stop is pending; then resolve the stop. The catch path immediately calls `confirmUnauthorized` without a current-epoch/authorization check. Because no classification probe has yet been used, the default path can call the protected info API after the application already confirmed loss. The executed fake-probe count is one.
- Required outcome / recommendation: no further automatic protected probe after confirmed loss; revalidate lifecycle/authorization after the awaited stop and before classification. Keep unknown network errors distinguishable from confirmed 401.
- Evidence type: independently executed current-module in-memory probe; no real protected request was sent.
- Verification plan: defer each awaited failure-cleanup step, confirm loss, release old work and assert no automatic proof/read/start and no Live until explicit reauthorization.
- Relationship: continuation of ANALYSIS-081, not a new suggestion. No further fix cycle was started.

### ANALYSIS-082: Ongoing pagination still issues reads after App destruction

- Severity: **warning**; existing teardown finding remains open in an active loader continuation.
- Related task / requirement: 4.5; no post-destruction reads/timers/connections.
- Locations: `dashboard/src/app.ts:426`–`:443`, `:1298`–`:1335`.
- Corrected portion: queue state cancellation prevents its queued follow-up; destruction marks the App destroyed, invalidates several epochs, releases foreground waiters and guards revision draining/catalog responses.
- Remaining reproduction: start Ongoing loading and defer its first timeline page; destroy the App; return a page with a non-null next offset. `ngOnDestroy` does not invalidate `ongoingEpoch`, and `loadOngoing` checks only that epoch while paginating, then unconditionally starts the unlinked request. The executed method probe observes the next timeline page and unlinked read after destruction. Clearing the resource queue cannot cancel work already executing inside that job.
- Required outcome / recommendation: active multi-request loaders must invalidate/cancel or check teardown before every subsequent request and before applying state/arming timers. A current response may finish, but it must not schedule additional work.
- Evidence type: independently executed current production-method slice with a deferred fake API; no actual HTTP request or Angular component was used.
- Verification plan: destroy during initial/next pagination page and return both more/final pages; assert no additional request, state application or timer arming. Recheck queue reuse and foreground waiters.
- Relationship: continuation of ANALYSIS-082.

### ANALYSIS-083: Documents-only navigation supersession still has no automatic follow-up

- Severity: **warning**; existing navigation/pending-work finding remains open for a domain without a reads update.
- Related tasks / requirements: 4.1, 4.3 and 5.4; reconcile deferred/superseded updates after navigation settles.
- Locations: `dashboard/src/app.ts:679`–`:681`, `:710`–`:739`.
- Corrected portion: overlapped reads work increments `baselineSupersessions`; if selection changes, this can queue a forced current-scope follow-up rather than stranding the old reads target.
- Remaining reproduction: start a documents-only revision refresh and defer catalog; complete navigation from A to B; resolve the old catalog successfully and deliver an identical checkpoint. No reads resource ran, so `baselineSupersessions` did not advance. The selection mismatch returns false without queuing a forced follow-up, and the unchanged checkpoint returns false at line 681. Executed current-method probe leaves `documents` pending and performs only the original catalog read.
- Required outcome / recommendation: benign selection supersession must schedule current-scope work for every affected domain, not only when a baseline request increments its counter; retain real failures as paused and keep stale A responses rejected.
- Evidence type: independently executed current reconciliation methods with real revision tracker/queue and fake current document/loaders; actual router/Angular behavior was not executed.
- Verification plan: documents/catalog-only update overlapping a successful different document/copy selection, followed only by identical checkpoints. Require latest selection retained and automatic convergence without manual Retry or another write.
- Relationship: continuation of ANALYSIS-083.

### ANALYSIS-084: Focus eligibility is not rechecked when the deferred restore actually runs

- Severity: **warning**; existing focus-preservation finding remains open for the deferred callback window.
- Related task / requirement: 4.3 and the newer-input focus preservation scenario.
- Locations: `dashboard/src/app.ts:850`–`:865`.
- Corrected portion: response-time restoration eligibility compares the active element with the original reader node, and scroll restoration uses the latest response-time position plus a scroll epoch. Input focus moved during the network request is therefore respected.
- Remaining reproduction: keep a passage focused until the baseline response resolves; after refresh returns but before its zero-delay restore timer, focus a newer connected input. The saved `restoreFocus` boolean remains true, and the timer unconditionally calls `restoreReaderFocus`, moving focus back into the replacement passage. The executed method/fake-DOM probe ends focused on the replacement passage instead of the newer input.
- Required outcome / recommendation: validate current focus ownership at callback execution, preserving a replacement for a node destroyed by rendering without overriding a newer connected target. Keep the new intentional-scroll guard.
- Evidence type: independently executed current production-method/helper slice with fake DOM focus semantics; real browser event ordering and composition remain unverified.
- Verification plan: explicitly advance the network response, focus/type in a connected composer/input before flushing restore callbacks, then flush and assert focus/caret/text retained. Also retain focus when only the old reader node was replaced.
- Relationship: continuation of ANALYSIS-084.

### ANALYSIS-140: Queued close recovery resets the attempt budget before the outage becomes healthy

- Severity: **warning**; regression of the ANALYSIS-080 recovery correction.
- Related tasks / requirements: 5.1 and 5.2; at most five automatic attempts per outage, bounded retry lifecycle, Live only after healthy reconciliation.
- Locations: `dashboard/src/live-connection.ts:111`–`:145`, `:152`–`:175`, `:276`–`:294`.
- Observation / reproduction: each close records recovery intent; when the current operation settles, `drainRecovery` calls `recoverAfterClose`, which invokes a fresh `connectWithRetries` with attempt counter zero. If each handshake succeeds but the transport closes before Snapshot data reconciliation becomes current, every close starts a new first attempt with no shared outage budget/backoff. The bounded probe closes the first ten Snapshot callbacks and lets the eleventh succeed: eleven starts occur before the first Live state, rather than exhausting at five and pausing. There is one operation at a time, but the automatic chain has no outage-level bound.
- Required outcome / recommendation: retain the attempt/backoff budget across queued recovery until the full connection/data lifecycle is healthy; exhausted attempts must pause until the planned explicit Retry/visibility recovery opportunity. Preserve the corrected ability to retain one close intent during active work.
- Behavior-preservation risks: distinguish a new outage after a healthy Live state from repeated closes during one recovery; keep manual Retry single-flight, authorization stop and epoch guards.
- Evidence type: independently executed current controller with fake transport and ten deterministic pre-reconciliation closes. No live connection or wall-clock benchmark was run.
- Verification plan: close every connection between handshake and reconciliation; require at most five automatic starts and Paused with no queued restart. Then explicit Retry and eventual healthy reconciliation may start a new budget. Repeat close-after-Live as a separate outage.
- Relationship: new regression ID within the reserved range; no optional suggestion and no further correction assignment.

## Re-verification

| Finding | Independent evidence on this target | Result | Remaining limitation |
| --- | --- | --- | --- |
| ANALYSIS-080 | Original close-during-Snapshot source probe now starts/reconciles twice and reaches Live after recovery; epoch rejects old completion | Original dropped-recovery mechanism corrected | Outage attempt-budget regression is ANALYSIS-140; no actual transport acceptance |
| ANALYSIS-081 | Original backoff probe now stops at one start/Paused; stop-await probe still performs one automatic protected classification | **Still failing** | No real HTTP/401 transport execution |
| ANALYSIS-082 | Queue clear now produces zero follow-ups; App destruction releases waiters, but current pagination makes two more reads | **Still failing** | No full component/browser teardown run |
| ANALYSIS-083 | Reads-supersession source mechanism added; documents-only current-method probe remains pending on identical checkpoint | **Still failing** | No actual router/copy-navigation browser run |
| ANALYSIS-084 | Request-time newer focus and scroll guards added; deferred restore still overrides newer focus | **Still failing** | Fake DOM probe, not real browser/composition acceptance |

## Assessment and limitations

The original controller close, backoff-authorization and queue-clear examples now behave correctly in independent in-memory checks. Four existing findings retain concrete uncovered paths, and queued recovery introduces an outage-budget regression. Supplied 297/297 unit success does not establish these remaining cases or final runtime acceptance.

This is the assigned second-batch recheck. Remaining blockers are reported for the orchestrator's disposition/user decision; no third fix cycle was requested or started. Backend and harness status are outside this report. Only this report and ignored probe evidence were written; implementation, task state, ledger and prior evidence were untouched. No real services, data, installation, native agent sessions or approval prompts were used.
