<!-- Saved by the orchestrator (Claude Code, claude-opus-5-5) verbatim from the reviewer's returned text. Reviewer: code-analyzer subagent, Claude Code (version not exposed), delegated subagent mode, model claude-opus-5-5. Case: the client refused the reviewer's write to this path; the reviewer reported the refusal and did not retry. Exact refusal message not supplied by the reviewer. -->

# Analysis report: realtime-dashboard-updates / 032

## Review target

- Reviewer / role: ANALYSIS-MILESTONE-032 / code-analyzer; milestone review, source and no-host only.
- Client / version / mode / model (resolved ID): Claude Code / version not exposed / delegated `code-analyzer` subagent / `claude-opus-5-5`.
- Date: 2026-11-11T19:30Z.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-MILESTONE-032.
- Task and requirement references: [tasks](../tasks.md) 2.4, 3.3, 4.1–4.3, 5.1–5.4, 7.1, 7.2, 8.1. Spec references: [work-dashboard](../specs/work-dashboard/spec.md) "Live connection health and recovery" and "Idle refresh cost"; [openspec-browser](../specs/openspec-browser/spec.md) "Refresh while preserving reading context". Also [design](../design.md) §3–§5.
- Scope: the frozen target paths below. Today's product deltas were read in full: `DashboardLiveConnections.cs`, the `DashboardHost.cs` diff, and in `dashboard/src/app.ts` the live, reader and loader paths. In `app.html` the live-status markup was read. The harness files read were `C06LiveAcceptanceHost.cs` (control I/O), `scripts/c06-fault-matrix.mjs` (drop gate and rename), `c06-dashboard-acceptance.mjs` (idle settle), `c06-acceptance-invariants.mjs`, `e2e/dashboard.spec.ts` (around `:25`, `:145-216`, `:679`, `:1155`), the `.agents/workflows/openspec-delegation.md` and `AGENTS.md` diffs, and `docs/agent-workflow.md` (grep only).
- Reading limits and paths read outside scope: I read tasks, design, specs, the "Current state" section of `findings.md`, and analysis-029/030/031. I did not read the rest of `findings.md`, HANDOFF, QA-027 or any raw runs. I also read the analysis-030 probe script, which I reused below.
- Baseline commit: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Implementation identity (before): `review-identity.ps1 -Baseline 0d77685 -Paths src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard dashboard/src dashboard/e2e dashboard/package.json dashboard/package-lock.json scripts tools/WorkLog.Smoke docs AGENTS.md .agents`. Result: `head=0d77685 baseline=0d77685 files=47 diff=ffa95521f348 untracked=26/2a1ad4cf0bd7`. Tracked diff SHA-256 `ffa95521f348a01bd3f2fdffeef13ef35b0789c2f2b9d571bacbad46cff6338d`.
- Before/after identity: the same command after the analysis gives the same summary line. No unexpected mutation.
- Prior reports carried forward: [analysis-029](analysis-029.md), [analysis-030](analysis-030.md) (ANALYSIS-580, ORCH-009) and [analysis-031](analysis-031.md) (ANALYSIS-680).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Slot `Offer`/`Take` wake-up correctness | Interleaving inspection | `DashboardLiveConnections.cs:35-54,194-196`. `Offer` stores `latest` under the lock and then signals. The signal channel is DropWrite with capacity 1, and every dropped signal still leaves a pending signal that leads to a later `Take`. | No lost wake-up. Spurious wake-ups are skipped by `Take() is null → continue`. |
| Checkpoint max-merge (ANALYSIS-680) | Inspection | `:39-40` keeps the larger pending checkpoint on the replacement vector; `Snapshot()` still returns untagged vectors (`:267`). | Correct for the tag. A separate pre-existing ordering gap in the revision fields is reported as ANALYSIS-703. |
| BeginStop / ApplicationStopping / 503 | Inspection | `:70-91,100-124`, `DashboardHost.cs:141-142`. `Add` re-checks `stopping` after `TryAdd`. The 503 runs after the Host/Origin/fetch-metadata check and before authorization. `StopAsync` awaits a snapshot of the send loops, and each loop ends through `Stop` cancellation. | Ordering is sound. One disposal race is reported as ANALYSIS-700. |
| Revocation vs. Publish vs. Take | Inspection | The pre-send `Valid` check follows `Take` (`:196-201`). The remaining window between that check and `SendAsync` is inherent to a pre-send check and carries no private content. | Acceptable as designed. |
| Steady-state cost in `Publish` | Inspection | `:146-150` computes `PendingHintCount`, a sum over all slots under each slot's lock, once per slot per publish. That is O(n²) lock acquisitions, with n equal to the open tabs. | Negligible at the expected few tabs. Recorded only. |
| `attachTaskAnchors` on `renderDocument` | Inspection | `app.ts:1875` runs an unguarded deferred re-attach; `:1821` and `scrollTo` (`:2100`) attach again. The attach is idempotent and O(tasks). | Correct. The `:1821` call is now redundant (no finding). The same deferral pattern is the clean place for the ANALYSIS-701 fix. |
| Search supersession `return true` | Inspection | `app.ts:2365-2371`. A user-typed search or a cleared query can supersede the live search. A newer request started later reads state at least as new, and a newer failure reports through `report()`. | No user-invisible masking, because the newer foreground failure shows its error. Two residual issues: a superseded 401 is swallowed, and the live domain is acknowledged before the newer search finishes. Both are folded into the ANALYSIS-702 fix. |
| Reader live status and Retry | Inspection | `app.html:159` (header `role=status` only without a document) and `:1031` (reader `role=status`). Both buttons call `retryLiveUpdates` → `LiveConnectionController.retry` → `runSingleFlight` (`live-connection.ts:57-71,293-303`). | One lifecycle and one live region at a time are confirmed. The mousedown `preventDefault` correctly keeps a mouse user's draft focus. Keyboard focus loss is reported as ANALYSIS-705. |
| ANALYSIS-580 Daily owner regression | Copied the analysis-030 probe and ran it against the current `app.ts` | `node artifacts/realtime-dashboard-updates/reviews/analysis-032/analysis-030-probe-rerun.mjs` → exit 0, `passed: true`, all nine check groups and four scope variants pass | Retained on current source (`app.ts` sha `62279995…`). |
| Harness control I/O | Inspection | `C06LiveAcceptanceHost.cs:188-243` reads with `ReadWrite|Delete` share, treats IO/UAE/JSON errors as "no command yet", and moves `ack` with ≤100×10 ms retries before rethrowing. The `when` precedence parses as intended. `c06-fault-matrix.mjs:328-337` `renameWithRetry` retries only on EPERM/EBUSY/EACCES, ≤100×10 ms. | Correct and bounded. A persistent failure still surfaces. |
| Drop/gap gate (ORCH-011a) | Inspection | `c06-fault-matrix.mjs:69-70,789-797`: 6000 ms from the drop (or gap start) to visible. The candidate wait is 10.5 s. The checkpoint must still be newer than the baseline and carry the dropped generation (`:913-917`, `c06-acceptance-invariants.mjs:32-40`). The three 5000 ms post-transport gates are unchanged (`:801-803`). | Matches the user-approved change. No other gate was weakened. |
| Idle settle | Inspection | `c06-dashboard-acceptance.mjs:370-390,418-460`. The settle needs Live plus 2 s with no API, connection or status change, with a 30 s bound, and runs before each window only. Window eligibility (`classifyIdleEligibility`) is unchanged and strict. | Correct. No in-window exemption. |
| Invariant self-test and syntax | `node scripts/c06-acceptance-invariants.mjs`; `node --check` on the fault matrix and acceptance scripts | `C06 no-host acceptance invariants passed.`; both checks exit 0 | Passed |
| Workflow parity | Diff inspection | Both edits are client-neutral and name both clients. No per-client policy appears in `.claude/agents` or `.codex/agents`. | One contradiction is reported as ANALYSIS-706. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Browser `receiveRevision`; server 5 s checkpoint plus immediate full check; 2 s revocation | Source observation; server counts are a hypothesis | **Browser: zero periodic REST.** An unchanged vector gives `accept → changed:false`, and with nothing pending `applyRevisionSnapshotNow` returns true before any reconcile (`app.ts:686-688`, `live-updates.ts:42-52`; `dirty` is ignored). `markReconciling` followed by `markReconciled` happens inside one task, so no status change renders. The time tick refreshes only on local-day movement (analysis-030). **Server per tick:** 1 tagged publish (`DashboardLiveCoordinator.cs:178`), then an immediate full check. It does one `ProcessBatch` publish (`:367`), four lane reconciles each followed by a publish (`:444`), catalog metadata re-enumeration without body hashes, a config read, root-mapping rebuild, read-only journal/digest marker opens, and a reads-index stat (carried from analysis-029). Up to one Git/OpenSpec round per source per 60 s on command-cache expiry. Up to 6 vector sends per connection per tick, each preceded by a 32-byte secret read, plus 2.5 revocation reads per connection every 5 s (ANALYSIS-704). Inputs change rarely while idle. |
| Cache lifetime versus interval and refresh duration | Catalog 2 s cache; command cache 60 s | Source (carried from analysis-029) | Unchanged. The full check invalidates the 2 s catalog cache, so it gives no relief at the 5 s cadence. The command cache does give relief. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Slots, send loops, `latest` holder | Source observation | One `latest` vector and one wake signal per slot. Slots and loops are removed on disconnect, stop and revocation. ANALYSIS-700 can skip `TryComplete` but leaks no entry. The command-cache key growth noted in analysis-029 is unchanged. |
| Repeated full reads where a change check would do | Full check every 5 s; unchanged re-publication | Source observation | The 5 s metadata re-enumeration is a design decision (design §2). Republishing unchanged vectors is avoidable (ANALYSIS-704). |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | `LocalCommand` | Source (carried from analysis-029) | Unchanged: Git/node from PATH, the OpenSpec npm layout, 15 s deadlines. |

## Findings

### ANALYSIS-700: `Remove` can call `Cancel` on an already-disposed slot token

- Severity: warning
- Related task / requirement: 3.3, 7.2; ORCH-015 fix (DEV-038)
- Locations: `src/WorkLog/Dashboard/DashboardLiveConnections.cs:56,132-137,183-188,226-231`; callers `:87-91` (BeginStop), `:177-181` (timer revocation), `DashboardLiveCoordinator.cs:187` (watcher callback), `:254` (OnDisconnected).
- Observation:
  - `Slot.Stop` is linked to `ConnectionAborted`. When an abort or disconnect fires, the send loop can exit and run its `finally`, where `Remove` is a no-op because another thread already won `TryRemove`. The loop then calls `slot.Stop.Dispose()`.
  - This can happen while the winning thread sits between its `TryRemove` and `slot.Stop.Cancel()`. `CancellationTokenSource.Cancel()` throws `ObjectDisposedException` after `Dispose`, and `AbortAndRemove` catches only around `Abort`.
  - Effects depend on the caller:
    - Timer revocation: `ExecuteAsync` faults, and the default `BackgroundServiceExceptionBehavior.StopHost` stops the dashboard (confirmed in .NET docs via ctx7).
    - Watcher authorization hint: the exception is unhandled on the FileSystemWatcher callback thread, because `DashboardChangeSource.cs:164-169` does not catch `ObjectDisposedException`. That likely terminates the process.
    - BeginStop: the `foreach` stops early, so the remaining live connections hold Kestrel's drain, which is the ORCH-015 symptom.
  - Partly pre-existing; BeginStop adds a new caller. The window is narrow but real, and it is widest exactly at revocation and stop, when every slot is aborted at once.
- Proposed improvement and benefit: make `Remove` disposal-tolerant, for example `try { slot.Stop.Cancel(); } catch (ObjectDisposedException) { }`, before `TryComplete`. Alternatively, let only the thread that won `TryRemove` dispose. Either removes host-stop and crash paths on revocation and shutdown.
- Behavior-preservation risks: none to delivery semantics. `TryComplete` must still run.
- Evidence type: source observation plus API documentation; not reproduced.
- Verification / measurement plan: a unit test that ends a slot's loop through `ConnectionAborted` and then calls `Remove`/`RevokeInvalidConnections` must not throw. Rerun the shutdown and revocation tests.
- Relationship: new.

### ANALYSIS-701: A deferred document-render callback is cancelled by a later no-render poll (ORCH-019a mechanism)

- Severity: warning (user-facing, transient)
- Related task / requirement: 4.3, 4.4; openspec-browser "Refresh while preserving reading context"
- Locations: `dashboard/src/app.ts:1756,1780-1786,1818-1841,1852-1854,720-725,810-813,843`
- Observation:
  - An explicit `loadDocument` renders and then schedules `setTimeout(0)`. That callback runs `syncStops`, the anchor scroll, scroll restoration and focus, guarded by `epoch === documentEpoch`.
  - Its `finally` then calls `releaseForegroundWaiters()`. A live `catalog-and-reader` job already waiting in `waitForForegroundIdle` resumes in a **microtask**, which always runs before that timer. The job calls `refreshSelectedDocument` → `loadDocument(poll=true, skipUnchanged)`, which increments `documentEpoch` synchronously.
  - The timer then sees a mismatched epoch and skips everything. The poll returns through `unchanged` or the same-revision fast path (`:1782-1786`) without rendering or re-syncing.
  - Result: `changeCount` keeps its previous value, here 0, so Next/Previous change stay disabled although the marks are rendered. The same race also drops an anchor scroll (a search-result or task link), the remembered position of a newly opened document, and the focus move.
  - Once a Documents or Catalog revision lands during an explicit load, the outcome is deterministic, which matches 1 of 22. The developer's hypothesis is source-confirmed; only the runtime remains unconfirmed.
- Proposed improvement and benefit (minimal): guard the deferred callback on "this load's render is still the visible one" rather than on any newer load. For example, increment a `renderEpoch` only inside `renderDocument` and check `renderEpoch === mine && document()` key matches. Loads that render nothing then no longer cancel the pending post-render work. A narrower alternative covers only the observed symptom: move `syncStops()` into `renderDocument`'s deferred callback, next to `attachTaskAnchors`. That leaves the anchor and focus loss.
- Behavior-preservation risks: a newer render, close or navigation must still cancel stale scroll and focus. A newer same-key render now runs both callbacks; both are idempotent.
- Evidence type: source observation; microtask-before-timer ordering is language semantics.
- Verification / measurement plan: a unit test that holds an explicit load, queues a Documents revision, releases the load, and serves `unchanged` to the poll. Assert `changeCount > 0` and that the anchor was scrolled.
- Relationship: canonical mechanism for ORCH-019a.

### ANALYSIS-702: Superseded live loads still count as failures and leave the page falsely "Updates paused" (ORCH-018 candidate)

- Severity: warning
- Related task / requirement: 4.1, 5.1, 5.4; work-dashboard "Live connection health and recovery"
- Locations: `dashboard/src/app.ts:819` (catalog), `:1018` (reviews), `:1038` (activity), `:1053` (read summary), `:1323` (history), `:1348-1360` (ongoing), `:1780,1791` (document poll); triggers `:1202-1210` (scopeChanged), `:1676` (routeChanged); no retry on an unchanged vector at `:687`.
- Observation:
  - DEV-037 fixed this class for search only. When a foreground call of the same loader bumps its epoch while a live reconcile awaits it, the live call returns `false` and `reconcileDomains` fails.
  - When `selectionEpoch` is unchanged (for example a scope change via "All repositories", or a comment-save `loadReviews`), no supersession follow-up is scheduled. Pending revisions stay, and checkpoints do not re-drive them (`!change.changed && !forcePending → false`).
  - The page stays "Updates paused" until a new revision or Retry, although its data is current.
  - In e2e `:679`, `authorize()` returns before Live (`dashboard.spec.ts:40-43`), so the immediate "All repositories" click can overlap the initial live reconcile. Whether the later REVIEW-write revision heals it depends on ordering. **ORCH-018 hypothesis, low-to-medium confidence:** it matches "reads finished but never Live". Source cannot exclude another cause.
- Proposed improvement and benefit: treat supersession as "joined", not "failed". Keep the latest in-flight promise per loader, and have a superseded call return `await` of the newer call's result. This avoids both the false pause and acknowledging before the newer request finishes. Apply the same change to search instead of its unconditional `return true`. Keep 401 precedence as in `loadDaily` (ANALYSIS-580); the current search catch at `:2370` swallows a superseded 401.
- Behavior-preservation risks: epoch protection against publishing stale data must stay. Only the boolean result changes. The Daily queue semantics are already correct and must not change.
- Evidence type: source observation; the link to ORCH-018 is a hypothesis.
- Verification / measurement plan:
  - A unit test that starts a live reviews reconcile, calls `selectAll()` mid-flight, and asserts Live with no new revision.
  - For ORCH-018, record status transitions and which resource returned false in the e2e trace before attributing the cause.
- Relationship: same class as ORCH-014 (DEV-037 search fix).

### ANALYSIS-703: A concurrent `Publish` can replace a newer pending vector with an older one

- Severity: suggestion
- Related task / requirement: 2.4; spec "out-of-order notifications SHALL NOT replace newer accepted state"
- Locations: `DashboardLiveCoordinator.cs:106-112` (snapshot under `stateGate`), `:178,217,367,444,508` (publish outside it); `DashboardLiveConnections.cs:35-44`.
- Observation:
  - Lanes take `CurrentSnapshot` and then publish without ordering. If lane A snapshots generation N, lane B bumps to N+1 and offers it, and A then offers N, the slot's `latest` regresses to N.
  - The browser state does not regress, because the client merges with max. Delivery of N+1 can wait for the next publish, up to one 5 s checkpoint.
  - A regressed vector that carries a merged checkpoint tag could also fail the drop cycle's "generation ≥ dropped" check and push recovery past 6 s.
  - Pre-existing with the old DropOldest channel.
- Proposed improvement and benefit: in `Offer`, when the instance is equal, merge the revisions component-wise with max and take the union of `dirty`, extending today's checkpoint max-merge. That makes the slot monotonic and removes a source of the up-to-5 s added latency, relevant to the deferred p95 target.
- Behavior-preservation risks: a new instance must replace the vector rather than merge.
- Evidence type: source observation; impact unmeasured.
- Verification / measurement plan: a unit test offers N+1 and then N and asserts N+1 is taken. The deferred freshness run can measure the effect.
- Relationship: neighbor of ANALYSIS-680.

### ANALYSIS-704: Each idle tick re-sends unchanged vectors

- Severity: suggestion
- Related task / requirement: 7.1 (server work deferred to `realtime-dashboard-performance-evidence`)
- Locations: `DashboardLiveCoordinator.cs:367,444`; `DashboardLiveConnections.cs:197`.
- Observation:
  - After the tagged checkpoint, `ProcessBatch` and each of the four lanes publish `CurrentSnapshot` even when no revision moved. That is up to 6 sends per connection per 5 s, each with a secret-file read. Over five idle minutes it reaches about 360 Revision messages per tab, all no-ops in the browser.
  - The slot coalesces some of them, so the real count is lower. This is a hypothesis.
- Proposed improvement and benefit: publish from `ProcessBatch` and the lanes only when the vector, excluding the checkpoint, differs from the last published one. The checkpoint already carries the complete vector each tick, so idle sends drop to about 1 per tick, with fewer secret reads.
- Behavior-preservation risks: dirty-hint publications that carry no revision change must still go out if the client uses them. The current client ignores `dirty` for change detection.
- Evidence type: performance hypothesis.
- Verification / measurement plan: count Revision frames in the existing DEV-041 idle raw, or in the follow-up's server-work run.
- Relationship: extends analysis-031's steady-state note.

### ANALYSIS-705: Keyboard focus falls to `body` during reader Retry

- Severity: suggestion
- Related task / requirement: 5.1, 5.2; ORCH-012 (DEV-036)
- Locations: `app.html:1034-1037`; `app.ts:596-604`; `live-connection.ts:57-71,119-122`.
- Observation:
  - When the connection is down, `retry` sets `reconnecting` synchronously, so the `@if(paused)` button unmounts while it holds keyboard focus.
  - Focus is restored to the reader only after the whole retry finishes. With 5 attempts at up to 8 s each plus delays, that can take tens of seconds. Meanwhile a keyboard or screen-reader user is on `body`.
  - Mouse users are unaffected because of the mousedown `preventDefault`. The header Retry has the same pattern, which is pre-existing.
- Proposed improvement and benefit: when the button is focused at activation, move focus to `.reader` (already focusable) synchronously before awaiting `retryLiveUpdates()`.
- Behavior-preservation risks: draft focus must stay untouched when focus is elsewhere.
- Evidence type: source observation.
- Verification / measurement plan: extend the reader e2e: press Enter on Retry while disconnected and assert `activeElement` stays inside the dialog.
- Relationship: new.

### ANALYSIS-706: The workflow still contains the superseded "both roles" rule for docs-only targets

- Severity: warning (contradictory governing rule; trivial fix)
- Related task / requirement: 8.1; user workflow decision 2026-11-11
- Locations: `.agents/workflows/openspec-delegation.md:31-34` (new tiers: harness or docs → one light QA review) vs `:70` ("A docs-only or config-only target gets a light review from both roles").
- Observation:
  - The two lines prescribe different reviewers for the same target class.
  - Lesser inconsistencies:
    - `:100` still lists "handoffs" among orchestrator artifacts, while `:64` says not to keep a separate handoff file.
    - `docs/agent-workflow.md:57` says "commits ... after both reviews", but checkpoint commits now follow milestone reviews, which is broadly consistent.
  - Both edits are otherwise coherent and client-neutral. AGENTS.md "Required checks are authorized" is client-neutral and matches the role files.
- Proposed improvement and benefit: change `:70` to defer to the tier list, or delete its second sentence. Reword `:100` to refer to the Current state section.
- Behavior-preservation risks: none.
- Evidence type: source observation.
- Verification / measurement plan: re-read the edited lines.
- Relationship: new.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-680 | Frozen identity above; `DashboardLiveConnections.cs` sha `df35eb43…` | `Offer` max-merge `:39-41`. The wake-up analysis shows no lost signal. The tag survives a replacement. | Verified at source level. Runtime results are owned by QA. |
| ANALYSIS-580 / ORCH-009 | `app.ts` sha `62279995…`; runner sha `cfa2417d…` | The analysis-030 probe re-run on current source passes all groups ([scratch](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-032/)) | Retained (no regression) |

Carried-forward mapping:

| Source | Scope | Current status |
| --- | --- | --- |
| analysis-029 (2.1–2.4, 3.1–3.3; no findings) | Backend at `files=10 diff=0a9a5cbc8458 untracked=6/00d0cbabee87` | The coordinator, change source, change-source tests and `ManualTimeProvider` hashes are unchanged, so their conclusions stand. Changed since then: `DashboardLiveConnections.cs` (`89317ca1…` → `df35eb43…`), the `DashboardHost.cs` lifetime/503 wiring, `DashboardLiveCoordinatorTests.cs` (`011d2a73…` → `8a66282c…`) and the new `DashboardShutdownTests.cs`; current backend summary `files=11 diff=1b85e41b3328 untracked=7/a57ea23d65a4`. These were re-reviewed here and give ANALYSIS-700 and ANALYSIS-703. Analysis-029's runtime evidence list (secret replacement, SQLite quiescence, digest lock symmetry, forced SSE, bundling) remains QA- and package-owned and is not re-verified here. |
| analysis-030 (4.1, 4.2, 4.5, 5.4; no findings) | ANALYSIS-580 Daily owner, ORCH-009 | Retained; probe re-run passes on current `app.ts` |
| analysis-031 (ANALYSIS-680) | Checkpoint eviction | Fix present and correct at source; neighbor ANALYSIS-703 raised |

## Assessment and limitations

Assessment by focus area:

- **Today's product changes:** they are structurally sound. The slot holder has no lost wake-ups, the stop path refuses and aborts correctly, and the reader status keeps one lifecycle and one live region at a time. Gated items for triage:
  - ANALYSIS-700: a robustness race that can stop the host on revocation and shutdown.
  - ANALYSIS-701: the confirmed source mechanism for ORCH-019a, a real but transient reader defect with a small fix.
  - ANALYSIS-702: false "Updates paused" after a same-selection supersession, a plausible ORCH-018 cause.
- **Residual flakes:** ORCH-019a looks like a real user-facing defect. ORCH-018 is not attributed from source alone; ANALYSIS-702 is a hypothesis, and instrumenting the e2e status trace is recommended before or alongside its fix.
- **Harness:** the changes are correct and bounded. No gate was weakened beyond ORCH-011a.
- **Idle contract:** confirmed from source. There are no periodic browser REST reads; the server cost per tick is listed above, and its measurement remains deferred.
- **Workflow edits:** client-neutral, with one stale contradictory line (ANALYSIS-706).

Limitations:

- No build, host, browser, smoke or .NET or dashboard suite was run. QA owns runtime.
- The only commands run were the invariant self-test, two `node --check` calls, the analysis-030 probe re-run and the identity script.
- Writes: ignored scratch under `artifacts/realtime-dashboard-updates/reviews/analysis-032/` (copied probe, its results JSON, stdout and stderr). The client refused the report write itself; this text is returned inline for the orchestrator to save.
- No product, test, doc, ledger or prior-report edits were made. The identity matched before and after.
