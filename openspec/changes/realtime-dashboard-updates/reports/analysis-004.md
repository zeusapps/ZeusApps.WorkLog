# Analysis report: realtime-dashboard-updates / 004

## Review target

- Reviewer / role: independent code-analyzer, ANALYSIS-REVIEW-004.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime-resolved model introspection unavailable.
- Date: 2026-11-08.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-REVIEW-004.
- Task references: browser portion of 3.1, 4.1–6.2, acceptance 3.4 and 7.1–7.5; [tasks](../tasks.md), [design](../design.md), [dashboard delta](../specs/work-dashboard/spec.md), [reader delta](../specs/openspec-browser/spec.md).
- Scope: `dashboard`; `scripts/c06-dashboard-acceptance.mjs`, `scripts/c06-fault-matrix.mjs`, `scripts/c06-instrument-snapshot.ps1`, `scripts/c06-mcp-compat.mjs`; `tools/WorkLog.Smoke`; `docs/architecture.md`, `docs/dashboard-validation.md`, `docs/mcp-validation.md`.
- Reading limits: concurrent QA 003 conclusions were not read. Backend corrections are excluded from this review; [analysis 003](analysis-003.md) remains the separate backend report. No backend finding is duplicated here. Neighboring unchanged client sources read for context: API/model/date, quiet, board and reader template helpers within `dashboard`.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths dashboard scripts/c06-dashboard-acceptance.mjs scripts/c06-fault-matrix.mjs scripts/c06-instrument-snapshot.ps1 scripts/c06-mcp-compat.mjs tools/WorkLog.Smoke docs/architecture.md docs/dashboard-validation.md docs/mcp-validation.md`.
- Manifest: 27 scoped files. Modified tracked files are dashboard package manifests; `app.daily.spec.ts`, `app.html`, `app.reliability.spec.ts`, `app.reviews.spec.ts`, `app.spec.ts`, `app.ts`, `styles.css`; all three scoped docs; Smoke `Program.cs` and project file. The 13 untracked files are C06 browser acceptance, five client live/helper files, four C06 scripts and three Smoke C06 helpers.
- Tracked diff SHA-256: `77d2c8f20156eca842e30e9e151bf2d12ac0e7e140646e4cd35543101c72f919`.
- Untracked hashes:

| File | SHA-256 |
| --- | --- |
| `dashboard/e2e/c06-realtime.spec.ts` | `df38f9929f5955cc44ed4b83d76b3b31b41f2c79fd774cd9581e835daefb9477` |
| `dashboard/src/app-live-test-helpers.ts` | `0f57c6112a8ddce266144e1430daf13b0a918cfef061744d53147efface636d1` |
| `dashboard/src/live-connection.spec.ts` | `0641db8f22b18224eab7e6a4d8ef204aa0e869c82bd1371358c05a6f24dbedfe` |
| `dashboard/src/live-connection.ts` | `5c2a01e89de9eabade6ef5530b577c64a9e1dd75ccfbffc6ba6e901c38222681` |
| `dashboard/src/live-updates.spec.ts` | `f23589b1a0dd81d1f5c816ad2c6daaf3e083a6e2e3e798542bfc3eb7704dcfc2` |
| `dashboard/src/live-updates.ts` | `23d85a8575a54556f96d33b148fe86cf091714eb329fdd5d59e865d44c5225b2` |
| `scripts/c06-dashboard-acceptance.mjs` | `842deb819a75abbbbdfa882817b0d138b73ad217b32e9573a367f4dbc5a35dcc` |
| `scripts/c06-fault-matrix.mjs` | `a05851469cf3ec12f073fc616fcf24fc0798bbd8a9afb3c55c4d5f712aea9c46` |
| `scripts/c06-instrument-snapshot.ps1` | `17c8f2cca9a2e60bc7a885b7dbee211d2f88df48f5849f73ed2d10e14f4282f3` |
| `scripts/c06-mcp-compat.mjs` | `5cc2b16cd70fd50d210c349eaa730579830d2eb2ad58bdfcb1ea3de0489c8537` |
| `tools/WorkLog.Smoke/C06BaselineFixture.cs` | `d2880efe74e73cf2fcc3e0ccaf4934d5f777651133a700ece3457df43df9215d` |
| `tools/WorkLog.Smoke/C06LiveAcceptanceHost.cs` | `0fec8a6ea5cada73a2a7f143179a01f0d01398d4a3b9ec93eaf312edbd476ae4` |
| `tools/WorkLog.Smoke/C06ProcessTree.cs` | `d190ee6130fbd21298d5a4cf627cfa2c641c279c77c4232e8f54729aeca9e64d` |

- Before/after identity: both match `head=0d77685 baseline=0d77685 files=27 diff=77d2c8f20156 untracked=13/c2352b9f3737`. No scoped edits were made by this reviewer.
- Prior review: backend analysis 003 only; no browser/harness finding is being rechecked. New finding allocation: ANALYSIS-080 through ANALYSIS-119.

## Analysis performed

| Area | Method / evidence | Outcome / limitation |
| --- | --- | --- |
| Revisions and resource ownership | Inspect `RevisionTracker`, `RefreshQueue`, App domain mapping and epochs | Per-domain desired/applied targets and one pending job exist. Navigation retry and teardown gaps are ANALYSIS-082/083 |
| Startup overlap correction | Inspect `refreshOpenBaseline` and the new App overlap cases | Same-logical-document revision advance is retried once; a failed retry retains the draft and dirty revisions. Developer's 289/289 suite is supplied evidence, not independently rerun here |
| Connection lifecycle | Run in-memory probes against transpiled current source with fake transport/API dependencies | Reproduced ANALYSIS-080/081 without sockets, server, build or approval prompt; [probe source](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-004/source-probes.cjs), [results](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-004/source-probe-results.json) |
| Queue disposal | Same bounded source probe | Reproduced ANALYSIS-082: queued follow-up runs once after `clear` |
| Reading context / focus | Inspect foreground epochs, baseline refresh and focus restoration | Existing foreground navigation guard is retained, but new baseline refresh restores stale focus: ANALYSIS-084. No browser focus/layout run performed |
| Time invalidation | Inspect clock dependency, next-boundary timer, quiet/board rules, midnight and visibility paths | Seven-day instant expiry and strictly-more-than-fourteen calendar-day quiet scheduling follow existing calculations. Timer uses a one-minute maximum safety interval without catalog polling. Exact DST/resume/focused-day integrated acceptance remains unverified |
| Fault matrix and evidence | Inspect category allocation, wire transforms, two-context/three-tab assertions, timing and cleanup | Topology is disclosed and both target pages are checked; actual fault targeting/recovery timing have ANALYSIS-085 gaps |
| Baseline comparison | Inspect collection script and guarded instrumentation, compare preserved/current parser concepts | Common and after-only metadata counters are separated; CPU/GC remain independent. ANALYSIS-086 concerns connected/current eligibility, not historical baseline validity. No after measurement was run |
| MCP parity / packaging | Inspect fixture scripts, helper dispatch and docs | Baseline/current protocol fixtures have equivalent Claude Code/Codex labels and disclaim native clients. Pinned SignalR dependency is bundled by existing build. Actual current protocol and self-contained/offline execution remain pending |
| Architecture and documentation | Compare source responsibilities with `docs/architecture.md` and controlling design | Core separation is coherent; docs truthfully state final gates pending. ANALYSIS-087 concerns incorrect reproducibility details, not prose style |
| Proportional static checks | Node syntax checks for four scripts, PowerShell instrumenter AST/newline check, target identity, category-only Markdown hygiene scan | Passed. No unit/browser/server/package suite was independently run and no approval request was issued |

The probe command is `node artifacts/realtime-dashboard-updates/reviews/analysis-004/source-probes.cjs` from the repository root. It reads and transpiles the two current TypeScript modules, supplies fake imports/connections, and executes only in-memory promises/callbacks. Its result at this target is:

```json
{
  "followupRunsAfterClear": 1,
  "closedDuringSnapshot": { "starts": 1, "statuses": ["live"], "lastStatus": "live" },
  "unauthorizedDuringRetryDelay": { "startCount": 2, "unauthorizedCallbacks": 1, "statuses": ["paused", "live"], "lastStatus": "live" }
}
```

These are controlled module-level observations, not proof of actual SignalR or packaged execution.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Client revision handler and temporal timer | Source observation | Identical successfully applied vectors cause no authoritative REST reads, though status is first set Reconnecting and then Live on every vector. The visible clock evaluates at the next boundary or at most once per minute; midnight can load only relevant History/Daily data. Five-minute zero-read and connected/current behavior is not measured; ANALYSIS-086 prevents using the existing collection script alone to prove it |
| Cache lifetime versus interval and duration | Revision and refresh resource caches | Source observation | Cache keys combine instance/domain revisions and current scope; successful identical tokens skip loaders. Failed tokens persist until explicit retry or a different token, which is appropriate for real errors but strands benign navigation supersession (ANALYSIS-083). Journal scopes include view/repositories/date/query state. Server cache timing is outside this review |
| Unbounded state | Client queues, drafts, diagnostics | Source observation | Refresh states are keyed by fixed resource names; one in-flight and one desired job per resource. App has one merged pending revision. Delivery/retry attempts are intended to be single-flight, but dropped recovery and disposal defects remain. Draft map is preexisting and keyed by documents visited; saved positions/documents retain existing limits. Fault traces grow per bounded run, not persisted product history |
| Repeated full reads where a change check would do | Document/read baseline and view loaders | Source observation | Open documents use known-revision reads except metadata reconciliation; digest-only changes target Daily. `refreshOpenBaseline` rerenders on each accepted shared-read refresh, even when the baseline is unchanged; no measured rendering cost is claimed. Fresh journal loading may paginate the representative 1,000-row fixture; request volume must be distinguished from periodic polling. Own writes still call some loaders directly as well as watcher-driven work; final no-duplicate behavior is unverified |
| External tool discovery / timeouts | Acceptance Node scripts, Smoke helper, build outputs | Source observation | Scripts use fixed repository Release output paths and the installed Playwright/browser channel. Owned server wrappers reuse kill-on-close process-tree handling. Matrix has cancellation and bounded waits; source snapshot instrumentation is excluded from shipped source. MCP compatibility uses synchronous bounded subprocesses, so its outer timer is observed only when the event loop resumes; no exact wall-clock deadline measurement was made. No package/install layout was executed by this reviewer |

## Findings

### ANALYSIS-080: A transport close during Snapshot reconciliation loses its recovery action

- Severity: **warning**.
- Related tasks / requirements: 5.1, 5.2 and 5.4; Live requires a connected transport and successful reconciliation, and reconnect recovery must have one owner without dropping an outage.
- Locations: `dashboard/src/live-connection.ts:140`–`:145`, `:159`–`:170`, `:251`–`:256`.
- Observation / reproduction: start the controller and defer `onSnapshot`; invoke its registered close callback; then resolve reconciliation successfully. `onclose` calls `runSingleFlight`, which returns the already-running start promise and discards the new recovery function. The old `snapshotAndApply` then sets Live without checking whether that connection closed during the await. Executed probe result: only one start and final status Live after the close; no recovery start.
- Proposed improvement / benefit: retain one pending recovery intent or connection epoch when a close arrives during active work. After awaited Snapshot/data work, verify the same connection is still established and authorized before setting Live; drain the pending recovery under the same lifecycle owner.
- Behavior-preservation risks: intentional stop versus outage, repeated close callbacks, new server instance, manual Retry, authorization classification and no duplicate starts.
- Evidence type: independently executed in-memory source probe, with fake transport and deferred callback.
- Verification plan: deterministic close during start, Snapshot invocation and data reconciliation; assert bounded recovery or truthful pause, no false Live and no concurrent starts. Repeat actual restart acceptance later.
- Relationship: new; separate from backend findings 040–043 and the same-document baseline fix.

### ANALYSIS-081: Confirmed authorization loss does not cancel an already-running retry loop

- Severity: **warning**.
- Related tasks / requirements: 5.2 and 5.3; confirmed 401 must stop automatic connection and protected refresh attempts until explicit reauthorization.
- Locations: `dashboard/src/live-connection.ts:99`–`:125`, `:159`–`:170`, and `setUnauthorized`.
- Observation / reproduction: make the first start fail with a network error and defer the retry delay; call `authorizationLost` to represent App receiving a confirmed protected 401; resolve the delay. The retry loop checks only `disposed`, not `unauthorized`, and starts again. In the probe, one unauthorized callback is followed by a second start and statuses Paused then Live. The success path likewise does not reject a successful callback that completes after authorization loss.
- Proposed improvement / benefit: invalidate/cancel the current lifecycle attempt on authorization loss, check authorization/epoch before and after every retry delay and awaited transport/data operation, and permit a new attempt only through explicit successful authorization proof.
- Behavior-preservation risks: unknown network failures must not be mislabeled as 401; keep one bounded classification probe, draft/readable state and explicit reauthorization behavior.
- Evidence type: independently executed in-memory source probe; no unauthorized real request was sent.
- Verification plan: 401 during backoff, start, Snapshot and reconciliation; assert no further protected operation and no Live until explicit reauthorization succeeds.
- Relationship: new; acceptance blocker distinct from lost-close recovery.

### ANALYSIS-082: Clearing the refresh queue does not stop its queued post-destruction follow-up

- Severity: **warning**.
- Related tasks / requirements: 4.5; teardown must not leave post-destruction reads or callbacks.
- Locations: `dashboard/src/live-updates.ts:154`–`:187`; `dashboard/src/app.ts:422`–`:428` and its revision work/foreground waiters.
- Observation / reproduction: enqueue a deferred first job and a second token for the same resource; call `clear`; resolve the first job. `clear` removes the map but the running `drain` retains the old state object and executes its desired follow-up. The in-memory probe observes one follow-up after clear. App uses `clear` during destruction and has no destroyed epoch guarding the pending revision drain, so these callbacks can still issue loaders after teardown; foreground waiters can also remain retained.
- Proposed improvement / benefit: cancel/invalidate active queue states and discard desired jobs during disposal, guard App revision work by destruction/epoch, and release or cancel retained waiters. Where HTTP cannot be aborted, ignore its result and prohibit subsequent work.
- Behavior-preservation risks: reusable queue reset versus permanent disposal, settling caller promises, pending failure state, new instance resets, teardown during saving/navigation.
- Evidence type: independently executed in-memory source probe for the queue; App consequence follows its disposal call path.
- Verification plan: dispose during a deferred read with one pending newer revision and during a foreground wait; resolve old work and assert zero subsequent reads, renders, timers or retained waiters.
- Relationship: new.

### ANALYSIS-083: Successful navigation can strand dirty revisions until manual Retry or another write

- Severity: **warning**.
- Related tasks / requirements: 4.1, 4.3 and 5.4; navigation-superseded/deferred work must reconcile after navigation settles.
- Locations: `dashboard/src/app.ts:648`–`:699`, `:722`–`:739`, `:776`–`:812`, and `:1620`–`:1621`; `dashboard/src/app.reliability.spec.ts:666`–`:699`.
- Observation / reproduction: defer a read-baseline refresh for document A, navigate successfully to B, then finish A's old response. Ignoring A is correct, but the refresh returns false and leaves the global reads domain pending. Route activation only enqueues journal/Daily work; it does not schedule the pending document/read reconciliation for B. Every unchanged checkpoint then returns false at line 658 because `forcePending` is false. The existing new-selection case explicitly expects pending reads and Paused, but does not exercise automatic follow-up after the newer navigation completes.
- Proposed improvement / benefit: distinguish benign supersession/deferment from actual request failure and schedule one coalesced current-scope follow-up when foreground navigation settles. Keep the stale response rejected without requiring an unrelated write or user Retry.
- Behavior-preservation risks: real network/storage failures must remain truthfully paused; avoid repeated failed reads or applying A's baseline/draft to B; preserve single-flight bounds.
- Evidence type: source observation plus the existing fixture's asserted pending/Paused outcome; no independent App fixture run here.
- Verification plan: complete newer navigation after an overlapped successful background read, deliver identical checkpoints with no new writes, and require current-scope convergence/Live while preserving B and its draft.
- Relationship: new; the same-document one-retry fix does not cover a different selection.

### ANALYSIS-084: Background read-baseline refresh can restore focus over newer user input

- Severity: **warning**.
- Related tasks / requirements: 4.3 and the reader's newer-input/focus preservation scenarios.
- Locations: `dashboard/src/app.ts:780`–`:805`, especially the unconditional `restoreReaderFocus` call after awaiting `reads/get`.
- Observation / reproduction: focus a reader passage, start a delayed shared-read refresh, then move to the comment textarea or another connected field and type before the read resolves. `refreshOpenBaseline` captures the old passage before the request and later focuses its replacement regardless of the current active input. The newer logical document guards do not detect a focus change within the same selection. The existing foreground document-navigation focus guard does not protect this new background path.
- Proposed improvement / benefit: restore a replaced reader node only if focus has not since moved to a newer connected target; likewise avoid restoring old scroll when the user has intentionally scrolled during the request. Keep focus restoration for a node destroyed by the background render itself.
- Behavior-preservation risks: keyboard passage navigation, review-note controls, source mode, reader remounting and background refresh during text composition.
- Evidence type: source observation; browser focus reproduction remains to execute.
- Verification plan: delay shared-baseline response, focus/type in the composer and another input, resolve response, and assert focus, caret, text and intentional scroll remain where the user left them.
- Relationship: new.

### ANALYSIS-085: Fault cycles do not prove the intended lost-generation recovery or full recovery latency

- Severity: **warning**.
- Related tasks / requirements: 7.2 and 5.4; exact allocated faults, writes during outages, correct final state and at-most-five-second reconciliation after transport recovery.
- Locations: `scripts/c06-fault-matrix.mjs:220`–`:232`, `:577`–`:659`, `:682`–`:744` and `:771`–`:795`.
- Observation: wire injection changes the first arbitrary `Revision` frame, without requiring an advance in the target domain. The server protocol includes unchanged-generation dirty/checkpoint vectors, so a drop/duplicate can affect an irrelevant vector while the actual new generation arrives normally. Reorder mode also omits a handled guard and can reorder further pairs after the first. No assertion proves checkpoint recovery supplied the missing generation. Separately, `visibleA/B` timestamps are recorded when the marker is found, while `waitForLive` occurs later; the reported five-second recovery may pass even when the full visible reconciliation becomes Live after that limit. Restart/revocation writes occur before the outage and are not asserted absent before disconnection, so those cases can pass with a marker already visible before recovery.
- Bounded reproduction: feed the wire transformer an unchanged dirty vector followed by the changed vector. The former consumes the configured drop while the latter is delivered normally. For timing, let the marker appear at one second after handshake but retain Paused until six seconds; marker-based recovery is recorded as one second and later status waiting can still succeed. For restart/revocation, show the written marker before the fault, then observe that final checks still pass.
- Proposed improvement / benefit: target explicit relevant domain generations and record the transformed/delivered shapes; prove missing-generation convergence via a checkpoint, apply each exact fault once, and await marker plus fully reconciled Live state for each relevant page before stopping its recovery clock. Commit a marker while the intended outage is established, or assert it was not applied before that outage.
- Behavior-preservation risks: preserve the 25/25/20/15/15 allocation, nine/eight/eight wire split, two contexts/three tabs, independent reader/draft assertions, committed-write timing and raw failure history. Keep timing clocks monotonic and per page.
- Evidence type: source observation; no 100-cycle run executed.
- Verification plan: deterministic transformer cases with unchanged versus advanced vectors; fake delayed-Live case that must fail the five-second gate; marker-previsibility guard; then actual full matrix on the frozen product.
- Relationship: new; not a duplicate of backend containment/isolation findings. After this report's findings and matching after-identity were complete, the orchestrator supplied the separately reported QA-080 wall-clock-overwrite finding. Its timing concern is related, but the missing-generation targeting and marker-versus-Live timestamps above are distinct defects; this report does not duplicate its wall-clock allegation.

### ANALYSIS-086: Idle collection can complete without proving five connected, current minutes

- Severity: **warning**.
- Related tasks / requirements: 7.1; five idle connected minutes with current data, separately reported from transport keep-alives and server reconciliation.
- Locations: `scripts/c06-dashboard-acceptance.mjs:307`–`:318` and `:343`–`:367`.
- Observation / reproduction: collection waits for content/search/reader DOM but never waits for or records the Live state before the idle windows, and does not monitor connection/status during them. A paused client can retain the fixture DOM and perform no catalog/document reads, satisfying the current raw idle record while automatic updates are absent. A disconnect and later reconnect within the window is likewise unclassified. Consequently zero recorded REST reads alone cannot establish the connected/current acceptance premise.
- Proposed improvement / benefit: retain the historical baseline semantics and original raw files, while adding explicit after-run eligibility/observation evidence for established transport and reconciled Live throughout each measured window. Record outages and time-boundary/user-action exceptions rather than presenting them as idle connected success. Settle intentional navigation work before the after measurement starts and keep its traffic separately labeled.
- Behavior-preservation risks: baseline has no live transport, so its eligibility must be labeled not applicable rather than reinterpreted. Keep fixture size, workload, sample counts and write-to-DOM timing comparable; do not silently rewrite the preserved collection inputs.
- Evidence type: source observation; no idle capture executed here.
- Verification plan: force startup Paused or close the socket during an otherwise quiet fixture; the after idle gate must reject/classify it. Then capture both full five-minute connected windows on one final target.
- Relationship: new measurement-validity finding, not a claim that historical baseline numbers are invalid.

### ANALYSIS-087: Published reproduction details do not identify or collect the claimed measurements

- Severity: **warning**.
- Related tasks / requirements: 7.5 and the baseline/after provenance requirement in 7.1.
- Locations: `docs/dashboard-validation.md`, the C06 safe command block and current acceptance-parser provenance paragraph.
- Observation: the documented parser SHA-256 differs from `Get-FileHash` on the frozen script: actual `842deb819a75abbbbdfa882817b0d138b73ad217b32e9573a367f4dbc5a35dcc`, documented `842deb819a75abbbdafa882817b0d138b73ad217b32e9573a367f4dbc5a35dcc`. The documented after command also points to the production executable and omits `--metrics`; `serverMetrics` then returns null, so that command does not collect the server method-boundary counters/GC allocations the adjacent paragraph says it includes. The instrumented snapshot/build invocation needed for that evidence is not supplied there.
- Proposed improvement / benefit: correct the hash from the final frozen file and distinguish the pure executable traffic/latency command from the copy-only instrumented counter command, including the required metrics flag and reproducible build/provenance mapping. Keep unfinished evidence explicitly pending.
- Behavior-preservation risks: do not instrument shipped source, overwrite baseline artifacts, or imply a DLL run is self-contained package proof.
- Evidence type: independently executed file-hash comparison plus source inspection of the metrics guard.
- Verification plan: recompute documented hashes after the final freeze and verify each documented command's output schema contains the fields it promises, with raw measurement overhead and source identities preserved.
- Relationship: new factual reproducibility issue, not a wording/style request.

## Re-verification

No existing finding was assigned for re-verification in this review. Backend findings 040–043 and foundation findings retain their own reports and identities.

## Assessment and limitations

This target adds explicit revision/resource coordination, a pinned local SignalR client, bounded connection attempts, source-preserving refresh, an independent temporal clock and a substantial disposable acceptance harness. Three lifecycle/teardown defects were independently reproduced in memory, and the remaining findings are concrete source/evidence issues requiring correction and verification. All findings above concern agreed acceptance or safe lifecycle behavior; no style refactor was requested.

The developer reports 289 passing dashboard unit cases; that report does not establish the missing race cases or final runtime acceptance. Full ordinary and fresh self-contained/offline browser runs, current/baseline Claude Code/Codex protocol fixtures, all 100 fault cycles, final after measurements, exact DST/resume behavior and final server-work accounting remain pending. No native clients, installation, real journal, or user dashboard was used. No approval prompts were issued. Only this reserved report and its ignored source-probe evidence were written.
