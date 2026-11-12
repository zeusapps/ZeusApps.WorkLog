# Analysis report: realtime-dashboard-updates / 007

## Review target

- Reviewer / role: independent code-analyzer, ANALYSIS-RECHECK-007.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime-resolved model introspection unavailable.
- Date: 2026-11-08.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-007.
- Task references: acceptance 7.1, 7.2 and 7.5; original [analysis 004](analysis-004.md) ANALYSIS-085–087 and [QA 004](qa-004.md) QA-080/081. Recheck plus regression only; no new suggestion.
- Scope: `dashboard/e2e`; scripts `c06-dashboard-acceptance.mjs`, `c06-fault-matrix.mjs`, `c06-instrument-snapshot.ps1`, `c06-mcp-compat.mjs`, **`c06-acceptance-invariants.mjs`**; `tools/WorkLog.Smoke`; docs `architecture.md`, `dashboard-validation.md`, `mcp-validation.md`.
- Reading limits: current scoped files, original assigned finding definitions/reports and previously read planning/role facts. Concurrent QA 007 conclusions were not read; no backend/browser implementation was re-reviewed.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths dashboard/e2e scripts/c06-dashboard-acceptance.mjs scripts/c06-fault-matrix.mjs scripts/c06-instrument-snapshot.ps1 scripts/c06-mcp-compat.mjs scripts/c06-acceptance-invariants.mjs tools/WorkLog.Smoke docs/architecture.md docs/dashboard-validation.md docs/mcp-validation.md`.
- Manifest: 14 files; tracked modifications are the three scoped docs, Smoke `Program.cs` and project file; nine untracked files are listed below.
- Tracked diff SHA-256: `260ca06ec5dd4ee4816e7c8c30023f7b724e60136f995fd71c03cd9fdd639ce9`.

| Untracked file | SHA-256 |
| --- | --- |
| `dashboard/e2e/c06-realtime.spec.ts` | `df38f9929f5955cc44ed4b83d76b3b31b41f2c79fd774cd9581e835daefb9477` |
| `scripts/c06-acceptance-invariants.mjs` | `45e7ee2e1ddf90eb3db21ebf0bdd6cd971ef7c0d1111570d5b1557317798f618` |
| `scripts/c06-dashboard-acceptance.mjs` | `fa911ed5ddb99b899a7f8da32975e47e3e620e5a8e1c49b76714846193fc4d33` |
| `scripts/c06-fault-matrix.mjs` | `5590fa81163822376649a09c2443a159adec8993b192ad9df178925412116e45` |
| `scripts/c06-instrument-snapshot.ps1` | `17c8f2cca9a2e60bc7a885b7dbee211d2f88df48f5849f73ed2d10e14f4282f3` |
| `scripts/c06-mcp-compat.mjs` | `5cc2b16cd70fd50d210c349eaa730579830d2eb2ad58bdfcb1ea3de0489c8537` |
| `tools/WorkLog.Smoke/C06BaselineFixture.cs` | `d2880efe74e73cf2fcc3e0ccaf4934d5f777651133a700ece3457df43df9215d` |
| `tools/WorkLog.Smoke/C06LiveAcceptanceHost.cs` | `0fec8a6ea5cada73a2a7f143179a01f0d01398d4a3b9ec93eaf312edbd476ae4` |
| `tools/WorkLog.Smoke/C06ProcessTree.cs` | `d190ee6130fbd21298d5a4cf627cfa2c641c279c77c4232e8f54729aeca9e64d` |

- Before/after identity: both match `head=0d77685 baseline=0d77685 files=14 diff=260ca06ec5dd untracked=9/433416292f3b`; no scoped mutation.
- Finding allocation: existing QA-080/081 and ANALYSIS-085–087; new regression range ANALYSIS-160–179 unused. No new ID allocated.

## Analysis performed

| Area | Independent check / evidence | Result / limitation |
| --- | --- | --- |
| Shared no-host helpers | `node scripts/c06-acceptance-invariants.mjs` | Passed. Matrix imports the actual injection/timing helpers; collector imports the actual eligibility helper, so they are shared production-harness logic, not merely copies |
| Monotonic recovery | Inspect `monotonicElapsedMs`, matrix calls and end-of-cycle storage | QA-080 corrected at source level: UTC overwrite removed; numeric recovery remains monotonic and chronology is separate |
| Digest architecture | Inspect changed digest paragraph against scoped live-path docs | QA-081 corrected at source level: obsolete two-second digest poll is replaced with live invalidation and targeted Daily refresh |
| Relevant wire generation and exact fault count | Inspect `planRevisionInjection` integration and route | Original arbitrary-vector targeting and repeated reorder are corrected. Checkpoint provenance and joint final-state proof remain ANALYSIS-085 |
| Outage writes and per-page recovery | Inspect disconnect/restart/revocation write order and reader/target clocks | Writes now follow a confirmed outage with precommit absence checks; reader and target clocks are separate, and marker-plus-Live helpers are used. Sequential helper still has a final-state gap below |
| Idle eligibility | Inspect collector observations plus execute actual classifier with a missing status transition | Paused/disconnected/user-action rejection is added; a transient missing surface between samples still classifies eligible, ANALYSIS-086 |
| Documentation/provenance | Compare current file hashes and commands with instrumenter/collector guards | ANALYSIS-087 corrected at source level: hashes match, uninstrumented latency and copy-only metrics commands are separated with `--metrics`; actual execution remains pending |
| Proportional static checks | JavaScript/probe syntax, PowerShell AST and newline matrix, scoped whitespace, before/after identity and category-only Markdown hygiene | Passed. No real host, full suite, package, writer, or measurement was executed; no approval prompt issued |

The independent extra probe is `node artifacts/realtime-dashboard-updates/reviews/analysis-007/harness-probes.cjs`. [Source](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-007/harness-probes.cjs) and [results](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-007/harness-results.json) are ignored evidence. It imports the actual shared helper and extracts current `wireRoute`, `cycleWire`, `recordShape`, and `waitForTargetAndLive` function bodies without editing them. Transport/control/DOM waits and the final whole-cycle assertion are fake: the wire case specifically checks the current checkpoint-validation branch, not real delivery or an entire matrix cycle.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | After-run collector monitor | Source observation | The collector samples status/transport every 250ms and records DOM status mutations, local minute boundaries and input. These are measurement-driver/browser observations, not periodic product REST reads. They add observation overhead absent from the historical collection; raw parser/provenance mapping is explicit. Actual browser/server CPU overhead has not been measured |
| Cache lifetime versus interval and duration | Source-generation trackers and retained baseline | Source observation | Target generation is captured before wire faults; unchanged vectors pass through, advanced generations select injection. Existing baseline remains immutable and is not recaptured. A matching generation observed before a later checkpoint can still satisfy the checkpoint proof, so generation equality alone does not establish its source |
| Unbounded state | Run-local traces and injected frames | Source observation | Matrix still has finite categories/deadline and run-local diagnostics; one handled flag prevents repeated injected reorder after success. Repeated held same-generation frames are retained until a newer generation or bounded wait failure. No new long-lived product collection is introduced. Runtime high-water/cleanup evidence is not established here |
| Repeated full reads where a change check would do | Measurement and control observations | Source observation | Collector uses local DOM/control observations and protected metrics at workload boundaries; method-boundary counters remain separate from physical I/O and actual CPU/GC. No after metrics or comparison result exists from this review |
| External tool discovery / timeouts | Instrumented-copy and packaged commands | Source observation | Docs now distinguish the production executable latency command from copy-only snapshot/locked restore/build/DLL metrics execution. Matrix and MCP use existing owned subprocess wrappers and bounded per-operation waits. No tool lookup, package/offline execution or process cleanup was rerun |

## Remaining findings

### ANALYSIS-085: Checkpoint provenance and simultaneous final marker/Live state remain unproven

- Severity: **warning**; existing acceptance-validity finding remains open.
- Related requirements: task 7.2, lost-notification recovery and zero incorrect final states; task 5.4 recovery timing.
- Locations at this target: `scripts/c06-fault-matrix.mjs:253`–`:256`, `:486`–`:489`, and `:765`–`:785`.
- Corrected portions: only target-domain advances consume a wire fault; handled faults pass later frames; reorder waits for a strictly newer generation; writes occur after established outages; clocks use monotonic times and wait through Live rather than stopping at first marker visibility.
- Remaining checkpoint case: any later frame with the dropped domain generation sets `recoveredGeneration` and its timestamp, regardless of whether it is a checkpoint or another completed domain's vector. The cycle independently waits for `checkpointCount` to increase, then accepts that previously stored generation. It does not require recovery to follow the checkpoint or correlate the received frame with it. The field named `recoveredAfterCheckpointAtMonotonicMs` can therefore contain a pre-checkpoint recovery time.
- Executed bounded input/result: baseline journal generation 7; drop an advanced generation-8 vector at monotonic 110; deliver another non-checkpoint generation-8 vector at 120; only afterward expose checkpoint count 1 at monotonic 200. Current extracted route/cycle validation returns success, with dropped generation 8 and reported checkpoint recovery time 120, before checkpoint observation 200. An independent lane publication can supply such a same-generation vector; a checkpoint counter increment is not received-checkpoint proof.
- Remaining final-state case: `waitForTargetAndLive` first waits for the marker, then waits for Live without rechecking the marker. If the marker disappears before Live arrives, it still returns a successful reconciliation timestamp. The extracted current helper probe starts with the marker present and removes it in the fake Live wait; result is success with `markerPresentAtReportedReconciliation: false` and `live: true`. The whole-cycle target records then rely on this helper without another target-state assertion.
- Required outcome / recommendation: establish that the missed generation is recovered through the intended checkpoint, and require current marker plus reconciled status together at the accepted final-state/timing point. Keep exact fault counts, monotonic per-page timing and the corrected outage write order.
- Evidence type: independently executed current-function probes with fake control/transport/DOM waits; no claim that a real matrix passed or failed.
- Verification plan: a same-generation non-checkpoint publication before a counter-only checkpoint must not count as checkpoint recovery; a marker removed before Live must not count as correct final state. Then execute the full matrix on a final frozen product when permitted.
- Relationship: continuation of ANALYSIS-085. QA-080's separate UTC-overwrite issue is corrected; it is not being reopened.

### ANALYSIS-086: An observed temporary absence of Live status still passes idle eligibility

- Severity: **warning**; existing connected/current-window finding remains open for a missing-status observation.
- Related requirement: task 7.1 and the collector/docs promise that missing status makes an after window ineligible.
- Locations: `scripts/c06-acceptance-invariants.mjs:25`–`:36`; collector `recordStatus` at `scripts/c06-dashboard-acceptance.mjs:382`–`:387` and eligibility consumption at `:432`.
- Corrected portions: after collection checks initial Live/transport, samples throughout the window, records status mutations and transport failures, and rejects paused samples/actions. Historical pre-live baseline is explicitly labeled not applicable rather than a connected pass.
- Remaining observation: the mutation monitor records `states: []` when the status surface is temporarily absent. The classifier checks only whether some element of that array differs from Live. An empty array therefore passes. A disappearance/reappearance between 250ms samples can be observed by the monitor yet still be accepted as an eligible uninterrupted window.
- Executed exact input: initial status Live, initial transport count 1; one regular sample `{ status: "live", establishedTransportCount: 1 }`; mutation records `[{ states: [] }, { states: ["live"] }]`; no transport or user events. Calling the actual imported `classifyIdleEligibility` returns `{ state: "eligible", reasons: [] }`.
- Required outcome / recommendation: an empty/missing status observation must invalidate the window, just as a non-Live observed status does. Preserve baseline not-applicable semantics and raw failure records.
- Evidence type: independently executed actual shared classifier; no browser monitor/idle host execution.
- Verification plan: remove/reinsert the status surface between ordinary samples and require ineligible raw evidence; retain healthy, paused, disconnected and user-action cases. A final five-minute run remains separate acceptance.
- Relationship: continuation of ANALYSIS-086; no new ID or suggestion.

## Re-verification

| Finding | Evidence on corrected target | Result | Limitations |
| --- | --- | --- | --- |
| QA-080 | Numeric recovery uses imported monotonic helper; end-of-cycle UTC overwrite is gone; independent helper returns 5,000ms for 100→5,100 | Original source issue corrected | Actual transport timing not executed |
| QA-081 | Architecture digest paragraph describes live invalidation/targeted Daily refresh | Corrected at source level | Documentation check only |
| ANALYSIS-085 | Relevant generations, exact-once handling, confirmed-outage writes and per-page monotonic timing added; two current-function probes still accept invalid checkpoint/final-state evidence | **Still failing** | Fake transport/control/DOM, no full matrix |
| ANALYSIS-086 | Live/transport observation added; actual classifier still accepts a recorded missing status | **Still failing** | No five-minute/browser execution |
| ANALYSIS-087 | Actual frozen script hashes match docs; pure executable and copy-only instrumented metrics commands now correctly differ | Original source/document issue corrected | Copy build/metrics run remains unexecuted |

## Assessment and limitations

The shared no-host invariant command passes and is genuinely imported by the real harness paths. It covers the intended unchanged/advanced generation and basic eligibility examples, but does not cover the two remaining integration proof cases or the empty-status case. QA-080/081 and ANALYSIS-087 are corrected at the source/document level; ANALYSIS-085/086 remain open.

No new regression ID or optional suggestion was introduced. This is the current second-batch recheck; remaining findings are returned for orchestrator disposition/user decision, and no further fix cycle was requested or started. Full host, 100-cycle, normal/package/offline, MCP compatibility and performance acceptance remain unverified. Only this reserved report and ignored probe evidence were written; source, task state, ledger, prior reports and baseline raw evidence were untouched.
