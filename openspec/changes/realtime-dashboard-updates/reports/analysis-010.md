# Analysis report: realtime-dashboard-updates / 010

## Review target

- Reviewer / role: independent code-analyzer, ANALYSIS-RECHECK-010.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime-resolved model introspection unavailable.
- Date: 2026-11-08.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-010.
- Task references: acceptance [tasks 7.1, 7.2 and 7.5](../tasks.md); exact remaining ANALYSIS-085/086 cases and prior QA-080/081, ANALYSIS-087 corrections in [analysis 007](analysis-007.md). Recheck plus regression only; no new suggestions.
- Scope: `dashboard/e2e`; scripts `c06-dashboard-acceptance.mjs`, `c06-fault-matrix.mjs`, `c06-instrument-snapshot.ps1`, `c06-mcp-compat.mjs`, `c06-acceptance-invariants.mjs`; `tools/WorkLog.Smoke`; docs `architecture.md`, `dashboard-validation.md`, `mcp-validation.md`.
- Reading limits: assigned role/project facts/template, historical 007 finding definitions and evidence, current scoped helper/route/tracker/cycle/wait/collector methods, documentation and process-cleanup references. No backend or browser implementation recheck and no concurrent QA conclusions. The optional checkpoint field's publication-only meaning is the supplied controlling contract; this report checks its harness consumption.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths dashboard/e2e scripts/c06-dashboard-acceptance.mjs scripts/c06-fault-matrix.mjs scripts/c06-instrument-snapshot.ps1 scripts/c06-mcp-compat.mjs scripts/c06-acceptance-invariants.mjs tools/WorkLog.Smoke docs/architecture.md docs/dashboard-validation.md docs/mcp-validation.md`.
- Manifest: 14 files; tracked modifications are the three scoped docs, Smoke `Program.cs` and project file. Nine untracked files are listed below.
- Tracked diff SHA-256: `d46961eb79d119f4a19c673f136dd3ce0aa600ac6271190fbbd78a0e72905422`.

| Untracked file | SHA-256 |
| --- | --- |
| `dashboard/e2e/c06-realtime.spec.ts` | `df38f9929f5955cc44ed4b83d76b3b31b41f2c79fd774cd9581e835daefb9477` |
| `scripts/c06-acceptance-invariants.mjs` | `1d5c983dc1ad019a9d382ff3568c85f136986a895236593a6397ba335b7cd915` |
| `scripts/c06-dashboard-acceptance.mjs` | `7014a436eceaae4e233d04f0aacfaee54d6faa69ed441c0783d16d0706a1ee82` |
| `scripts/c06-fault-matrix.mjs` | `654b7554c365df71cbbf8641fbb35401bf22073923b4e1a0d19fa09e0d9004d9` |
| `scripts/c06-instrument-snapshot.ps1` | `17c8f2cca9a2e60bc7a885b7dbee211d2f88df48f5849f73ed2d10e14f4282f3` |
| `scripts/c06-mcp-compat.mjs` | `5cc2b16cd70fd50d210c349eaa730579830d2eb2ad58bdfcb1ea3de0489c8537` |
| `tools/WorkLog.Smoke/C06BaselineFixture.cs` | `d2880efe74e73cf2fcc3e0ccaf4934d5f777651133a700ece3457df43df9215d` |
| `tools/WorkLog.Smoke/C06LiveAcceptanceHost.cs` | `0fec8a6ea5cada73a2a7f143179a01f0d01398d4a3b9ec93eaf312edbd476ae4` |
| `tools/WorkLog.Smoke/C06ProcessTree.cs` | `d190ee6130fbd21298d5a4cf627cfa2c641c279c77c4232e8f54729aeca9e64d` |

- Before/after identity: matching `head=0d77685 baseline=0d77685 files=14 diff=d46961eb79d1 untracked=9/2673bcb8f289`; no scoped mutation.
- Finding allocation: existing ANALYSIS-085/086, QA-080/081 and ANALYSIS-087 regression; ANALYSIS-220–239 unused.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Checkpoint provenance and exact loss window | Extract actual `wireRoute`, `cycleWire`, `createTransportTracker`, `recordShape`; import actual shared helpers | Baseline journal 7/checkpoint 4; drop generation 8 at 110, suppress ordinary generation 8 at 120, accept received tagged checkpoint 5/generation 8 at 200 | ANALYSIS-085 checkpoint path corrected in bounded function integration probe |
| Invalid recovery proof | Feed untagged, absent tagged publication, stale sequence, missing generation and proxy-only/no-tracker-delivery cases through those production functions | All five reject; ordinary frame creates no recovery candidate, and proxy candidate alone cannot satisfy browser-received proof | Former earlier-ordinary-plus-later-counter proof cannot satisfy this path. No real browser frame was received in this fake transport probe |
| Joint final-state proof | Extract actual `waitForTargetAndLive`, execute its DOM callback against changing fake nodes | Marker present while reconnecting then absent while Live rejects; third simultaneous visible marker/Live sample accepts. Hidden marker, hidden status and missing status reject | ANALYSIS-085 final-state mechanism corrected; no real rendering/layout execution |
| Idle eligibility | Invoke actual classifier with the exact historical empty-status input and adjacent cases; inspect collector import and use | `states: []` followed by Live returns ineligible with disappearance reason; healthy remains eligible; paused, disconnected and user action reject | ANALYSIS-086 corrected at helper/integration-source level; no five-minute browser run |
| Fault counts and boundedness | Execute actual `chooseWireKind` over cycles 26–50; inspect per-stage waits and loss-window lifetime | 9 drop, 8 duplicate, 8 reorder retained. Loss window closes on one qualifying checkpoint; candidate and browser-delivery waits are 10,500 ms and 1,500 ms | Exact planned distribution preserved; actual full matrix and runtime cleanup unverified |
| Timing regression | Inspect actual recovery storage and end-of-cycle update; execute monotonic helper | 100 to 5,100 yields 5,000 ms; recovery remains based on per-page monotonic transport/reconciliation observations; UTC is chronology | Prior QA-080 correction retained; no measured recovery latency |
| Architecture/document regression | Inspect digest paragraph and compute current script hashes against documented provenance | Digest uses live invalidation/targeted Daily refresh; all five documented current parser/matrix/helper/instrumenter/MCP hashes match | QA-081 and ANALYSIS-087 source/document corrections retained |
| Measurement semantics | Inspect collector counters, metrics commands, observation eligibility and baseline text | Missing common counters remain errors; only absent pre-C06 after-only component normalizes to zero. Docs separate uninstrumented executable latency from copy-only instrumented DLL metrics with `--metrics` | No invented after counters or latency result; preserved baseline remains a comparison reference |
| Independent static checks | Shared no-host invariant command, probe assertions, JavaScript syntax, PowerShell AST, scoped whitespace and before/after identity | Passed | No host, build, package, writer, full suite or measurement started by this reviewer |

Independent evidence: [probe source](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-010/harness-probes.cjs) and [exact results](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-010/harness-results.json). The script imports the current real helpers and extracts current production-harness functions with the TypeScript AST; it does not copy their corrected logic into assertions. The whole-cycle UI assertion is stubbed, then the actual joint DOM waiter is checked separately. Controlled routing, DOM nodes, monotonic clock and waits are fake and bounded. Historical 007 probes and report are unchanged.

The positive wire result is exactly one dropped frame, one suppressed ordinary frame, one forwarded checkpoint, recovered generation 8/sequence 5 at monotonic 200, with browser-tracker receipt required. The untagged case suppresses two ordinary frames and rejects before a checkpoint candidate exists. The no-tag/counter-only case likewise rejects; the production cycle no longer uses a server checkpoint counter as its drop-recovery proof. A stale sequence 4 rejects; a checkpoint 5 carrying only generation 7 cannot recover generation 8; a qualifying proxy frame deliberately withheld from the browser tracker rejects at the browser-receipt gate. These are executed function outcomes, not real transport or checkpoint-latency measurements.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Collector observation and matrix waits | Source observation | Existing collector samples Live/transport every 250 ms and records status mutations/input. This correction adds empty-surface rejection, not product requests or another poll. Matrix joint-state polling observes DOM at 25 ms while awaiting a target. Instrumentation/observation overhead remains unmeasured |
| Cache lifetime versus interval and refresh duration | Target generation/checkpoint trackers | Source observation | A single active loss window holds baseline generation/sequence and its dropped generation. Ordinary vectors cannot shortcut it; a newer tagged publication and corresponding received frame end it. No new product cache or expiration policy is added; final throughput/latency remains for runtime acceptance |
| Unbounded state | Run-local traces and loss frames | Source observation | One active fault/candidate and finite cycle plan; each wait and the whole run have deadlines. The loss trace records received dropped/suppressed frames until recovery/failure, so high-water size depends on traffic within the bounded window. Existing owned-process cleanup paths remain; actual process/port cleanup was not executed |
| Repeated full reads where a change check would do | Measurement collector | Source observation | Local DOM/status observations and protected metrics at workload boundaries remain distinct from product file/database I/O. New checkpoint proof uses frame metadata, not additional authoritative content rereads. No after-workload CPU, GC or physical-I/O reduction is inferred from the code |
| External tool discovery / timeouts | Measurement/build/MCP wrappers | Source observation | Corrections do not change executable discovery or owned subprocess wrappers. Copy-only build commands, per-operation waits and final cleanup remain documented/source-level mechanisms. No lookup/install variant, package/offline run or cleanup guarantee was independently exercised here |

## Findings

**No findings** within the assigned recheck and regression scope. ANALYSIS-085/086 mechanisms now reject their former false evidence and accept a valid controlled example. No new regression ID or suggestion was allocated.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-085 | Matrix route/cycle at lines 243–282, 787–820; joint waiter at 505–526 | Ordinary-before-checkpoint cannot create recovery; tagged sequence/generation and tracker receipt required. Sequential marker/Live false state rejects; simultaneous visible state accepts | Corrected at source/function-probe level |
| ANALYSIS-086 | Helper empty-status condition; collector `recordStatus` and classifier consumption | Exact historical `states: []` input now ineligible; collector records and passes it to actual helper | Corrected at source/helper level |
| QA-080 | Per-page monotonic recovery and end-of-cycle storage | Monotonic helper 5,000 ms result; no UTC overwrite of recovery fields | Prior correction retained at source/helper level |
| QA-081 | Architecture digest paragraph | Live invalidation and targeted Daily refresh retained; no obsolete two-second poll claim | Prior correction retained at document level |
| ANALYSIS-087 | Current five source hashes and metrics commands | All documented current hashes match actual source; executable latency and copy-only DLL metrics commands remain separate | Prior correction retained at source/document level |

## Assessment and limitations

Both remaining acceptance-validity mechanisms are corrected at source/function-probe level, and the assigned prior corrections have no observed regression. Shared no-host invariants and independent integration probes pass. Report/script syntax, scoped whitespace, identity and category-only report hygiene checks passed. Only this reserved report and ignored 010 evidence were written; source, tasks, ledger, historical reports/evidence and baseline raw measurements were untouched. No approval prompt was issued.

The supplied full .NET 452-pass and browser 303-pass results do not establish live acceptance and were not rerun here. No actual fault smoke, 100-cycle matrix, normal/package/offline browser run, Claude/Codex protocol fixture, five-minute connected idle window, latency sample collection, final CPU/GC measurement or process/port-cleanup execution was performed by this reviewer. The orchestrator owns those next executions, ledger disposition and final acceptance. Native-agent parity is not claimed by protocol fixtures, and no native agent was launched.
