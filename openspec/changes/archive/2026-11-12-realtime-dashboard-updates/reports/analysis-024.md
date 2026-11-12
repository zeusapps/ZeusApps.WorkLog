# Analysis report: realtime-dashboard-updates / 024

## Review target

- Reviewer / role: ANALYSIS-RECHECK-024 / code-analyzer; code-only final scope.
- Client / version / mode / model: Codex / version unavailable / Default delegated review / assigned role model `gpt-6.1-sol`, high reasoning; runtime introspection unavailable.
- Date: 2026-11-09T16:33:27Z.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-024.
- Tasks:1.1,5.4,7.2; canonical QA-440, duplicate ANALYSIS-460. User authorized this correction; this reviewer implements nothing.
- Scope: `scripts/c06-fault-matrix.mjs`, `c06-disconnect-control.mjs`, `c06-disconnect-control.spec.mjs`, `c06-acceptance-invariants.mjs`, `c06-host-lifecycle.mjs`.
- Reading limits: no current QA report/evidence, findings ledger, handoff, orchestrator workflow or moving documentation read. Prior own [analysis022](analysis-022.md) is the original attribution. Relevant additional reads: installed Playwright1.64.1 `coreBundle.js` route-client/dispatcher/injected-WebSocket implementation, local SignalR handshake source, and parent-supplied actual diagnostic raw named below. Official Playwright routed-close documentation was supplied in the assignment; no new fetch was performed.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `pwsh -NoProfile -File scripts/review-identity.ps1 -Baseline 0d77685 -Paths scripts/c06-fault-matrix.mjs scripts/c06-disconnect-control.mjs scripts/c06-disconnect-control.spec.mjs scripts/c06-acceptance-invariants.mjs scripts/c06-host-lifecycle.mjs`.
- Manifest: five untracked files, no tracked diff; tracked diff SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.

| Scoped file under `scripts/` | SHA-256 |
| --- | --- |
| `c06-acceptance-invariants.mjs` | `1d5c983dc1ad019a9d382ff3568c85f136986a895236593a6397ba335b7cd915` |
| `c06-disconnect-control.mjs` | `8e01b64e801e082342cb508429e1a425e9695411862a4cef7e023a11469d47ca` |
| `c06-disconnect-control.spec.mjs` | `c1a8cfde9e96f2c4ca2ec97ea9258fe08ea31535ec65922bc1ebe2c0dfd3debe` |
| `c06-fault-matrix.mjs` | `6f75bde88c6fa8abfe0a5c1932d581a214446a6b392da84034cc0bba11ae2e9f` |
| `c06-host-lifecycle.mjs` | `4b27ee19a227c22e4302d51f0b7294c3d7fe2145ac0c33f5d74254a1df341009` |

- Before/after identity: both `head=0d77685 baseline=0d77685 files=5 diff=e3b0c44298fc untracked=5/00213b8ffa18`; exact match.
- Finding allocation: ANALYSIS-500/501/502 below; ANALYSIS-503 through ANALYSIS-519 unused. No suggestions.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Original disconnect attribution | Inspect route registration before initial navigation, generation tracking, actual page-close observers and held replacement control | Both A pages now have retained routed pairs and page socket identities; B keeps its separate real upstream/wire route | Original offline-toggle-only mechanism is corrected structurally; robustness gaps below remain |
| Existing no-host regression | `node scripts/c06-disconnect-control.spec.mjs`, lifecycle spec and acceptance invariants | All exit0 | Passed independently. Route fake accepts all close codes and does not model mutable captured-generation proof or handshake temporal correlation |
| Close direction / installed implementation | Inspect symmetric callbacks and installed Playwright route/injected-WebSocket source | Each installed close handler forwards to the opposite side; no message handler replaces default message forwarding. Native upstream closure uses the browser WebSocket `close(code,reason)` path; default helper code1001 conflicts with accepted caller codes | ANALYSIS-500; source-contract issue with controlled actual-helper reproduction, not a newly executed browser failure |
| Captured identity / held replacement | Execute actual helper with complete original closure followed by a held replacement | Original pair remains fully closed; current pointer changes to the held generation, so `requirePageClosed(label)` fails against the replacement rather than proving the captured pair | ANALYSIS-501 reproduced independently without a host |
| Post-release handshake evidence | Extract actual `measureConnectedAt` body and supply a stale accepted acknowledgement after its index | Helper accepts it without a release-time or generation predicate. Current tracker records handshakes without their socket generation | ANALYSIS-502; current actual success is consistent with correct timing but the required correlation is not enforced |
| Outage writes and independent B | Inspect actual runner and parent-supplied raw | Source waits for both pair/page close proofs and both non-Live states before external write; it requires a newer B Revision on its original unclosed socket while A is held | Passed on the supplied one-cycle path; held-route identity robustness remains501 |
| Persistent reader and recovery target | Inspect unchanged assertions and actual record | Original draft, focus/caret, source/rendered mode, logical document and position retained; target recovery54ms, reader311ms from their recorded handshakes, each within unchanged5000ms bound | Passed in preserved supplied actual run; no runtime replay by reviewer |
| Diagnostic mode / full allocation | Inspect cycle selection and labels | `--disconnectOnly` is explicitly single-disconnect diagnostic; default100 and25/25/20/15/15 allocation preserved | Passed; single cycle is not five-category or full matrix acceptance |
| Actual lifecycle outcome | Read `fault-matrix/2026-11-09T16-28-02-576Z-16cb0b27.json` | State passed, one record, post-cycle active connections3; final source disposed, connections0, port released, wrappers0 | Passed in parent-executed actual record, not a universal close-control guarantee |
| Stability / whitespace / own-report hygiene | Before/after identity, scoped `git diff --check`, category scan | Identity matches and whitespace exit0; own report has no sensitive/residue category hit | Passed |
| Broad runtime / docs | No new host/build/browser/package/performance/full matrix or documentation review | Parent owns current execution; docs extension withdrawn until fixes | Not performed; current native/provider/package/whole-feature acceptance not inferred |

Independent evidence: ignored `artifacts/realtime-dashboard-updates/reviews/analysis-024/no-host-recheck.mjs` and `no-host-results.json`. The first two probes import the actual current helper; route dependencies are controlled no-host models. The close-code model enforces the caller constraint shown by the installed browser route implementation. The timestamp probe extracts the actual runner function body. These are independent bounded method tests, not native browser replay.

The parent-executed actual record proves meaningful success on this run: captured A ids `a2`/`a4` show actual page socket and both route close events before outage confirmation and writer commit; later ids `a5`/`a6` open and reconcile. Both original client-route close callbacks carried unspecified codes, and upstream events carry1000. Thus this run did not exercise forwarding of the helper's explicit default1001 code. `heldReplacementRelease` is empty, so it also does not exercise releasing a route created while held. These facts preserve the actual successful diagnostic while bounding what it covers.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Fixture disconnect routes | Source observation | No product idle/polling work changed. Only owned A test sockets are routed; B retains independent real traffic. No product timeout/heartbeat reduction |
| Cache lifetime versus interval and refresh duration | Held routes / closure and recovery waits | Source observation | Existing5second close/status gates,15second handshake lookup and5second successful reconciliation gate remain. Mutable closure selection and uncorrelated timestamps can invalidate those gates;501/502 |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Generation history, current map, event arrays | Source observation | Current map is one entry per two A labels; generations/events retain per-run history without eviction. Finite100cycle plan, bounded client retries and overall deadline bound the invocation, rather than a fixed per-history cap. Superseded held entries are cancelled before release; mutable-current proof remains501 |
| Repeated full reads where a change check would do | Fixture evidence and existing refresh path | Source observation | No new complete journal/document read loop. Existing authoritative writes/DOM checks remain; helper adds close/generation observations |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Existing owned Smoke process and browser fixture | Source observation | No new executable discovery. Existing owned host and cleanup remain; route-close failures are incompletely surfaced by swallowed forwarding errors500 |

## Findings

### ANALYSIS-500: Default routed upstream close code1001 can be rejected before the upstream closes

- Severity: warning; blocks reliable explicit-disconnect control across required paths.
- Related tasks:1.1,5.4,7.2 / exact real owned transport closure and error handling.
- Locations: `scripts/c06-disconnect-control.mjs:18,36-42,105-109,127-129`; installed Playwright1.64.1 `coreBundle.js:54713-54720,61362-61364` for native upstream closure, and injected WebSocket caller-code validation near54660.
- Observation: Helper default1001 is forwarded to the real upstream route. That route invokes browser-native `WebSocket.close`, whose caller code must be1000 or3000–4999 when supplied. The helper suppresses the forwarding rejection. In the actual-helper controlled code-validation model, page side closes but upstream stays open and pair proof cannot succeed. Existing FakeRoute accepts any code and marks itself closed, masking this path.
- Proposed correction / benefit: Use a supported caller close code for fixture-initiated shutdown, normalize forwarded transport-only codes where necessary, and retain a safe recorded category for failed forwarding so it cannot be mistaken for completed closure. Strengthen the actual-helper test with a rejected-close model; do not merely set closure flags after an awaited API call.
- Behavior-preservation risks: Preserve both-direction close propagation, real callback/page evidence and exact pair ownership. An actual peer close code is not automatically valid when used as a new native close call argument. Never weaken closure requirements.
- Evidence: Installed source contract and bounded actual-helper model. Parent actual run passed through unspecified client code/upstream1000; no observed upstream leak is alleged.
- Verification: Actual helper with valid1000/custom caller code must close both sides; invalid/failed forward must produce a bounded explicit failure. Then actual disposable socket run must retain both route and native page close events before writing.
- Relationship: Adjacent correction within QA-440 / ANALYSIS-460, not a product transport defect.

### ANALYSIS-501: Closure proof loses the captured generations when a valid held replacement becomes current

- Severity: warning; blocks the required held-replacement path.
- Related tasks:1.1,5.4,7.2 / stable socket ownership while reconnects are held.
- Locations: `scripts/c06-disconnect-control.mjs:97-103`; `scripts/c06-fault-matrix.mjs:801-806`.
- Observation: Runner captures the two ids to close, but `requirePageClosed` looks up mutable `current` by label. An arriving correctly held replacement changes current. The independent actual-helper probe first proves all original socket/route close flags, creates a held replacement with no upstream, then reproduces failed proof for the original label despite complete closure of the captured pair. Source passes no expected ids into the proof. Current actual run had no held replacement to release.
- Proposed correction / benefit: Bind close operation, required closure proof and resulting evidence to the exact captured generation ids. Keep replacement ownership/gating separate; reject a missing, mismatched or incomplete captured pair. Cover the interleaving where replacements arrive before the original closure wait settles.
- Behavior-preservation risks: Do not accept any other closed historical generation or remove the held replacement gate. Ensure superseded pending routes stay cancelled and release only the current eligible replacement.
- Evidence: Bounded actual-helper reproduction using valid1000 to isolate this issue; no browser replay.
- Verification: Both captured pairs close while replacements are held; proof returns those captured ids and release connects only current eligible replacements. Missing/stale ids, one-sided close and rejected close fail closed.
- Relationship: Adjacent QA-440 correction robustness, distinct from500's close-code failure.

### ANALYSIS-502: Recovery handshake lookup does not enforce post-release time and socket-generation correlation

- Severity: warning; blocks the agreed evidence-fidelity requirement for recovery timing.
- Related task:7.2 / fresh transport re-establishment timestamps for each intended A tab.
- Locations: `scripts/c06-fault-matrix.mjs:194-230,645-648,828-841`.
- Observation: Tracker handshake records have time/kind but no socket generation. `measureConnectedAt` selects a handshake solely from a pre-fault accepted-array offset. The runner separately proves that some newer socket generation exists, without linking that generation's handshake or checking handshake time against an explicit release timestamp. Executing the actual extracted method accepts a controlled acknowledgement at15ms when the intended release boundary is100ms. Current actual acknowledgements follow the observed new socket opens, but that successful timing is not an enforced predicate.
- Proposed correction / benefit: Record a monotonic release boundary and associate accepted handshakes with the actual page socket generation. For each A tab, require the handshake of its eligible new post-release generation with timestamp at/after release; use that exact record as recovery start. Reject preparation/old-generation acknowledgements even if they appear after the pre-fault array offset.
- Behavior-preservation risks: Preserve true transport acknowledgement timing, per-tab records, authoritative DOM/Live conjunction and unchanged5000ms reconciliation bound. Do not substitute a UI transition, Snapshot completion, route creation or write timestamp for transport acceptance.
- Evidence: Actual runner method extraction with controlled tracker plus source wiring analysis; not a claim of wrong timing in the supplied actual run.
- Verification: Negative probes insert old/preparation acknowledgements after the offset and before/after release; none may satisfy recovery for a different generation. A valid post-release acknowledgement for each new A generation must be accepted and drive the unchanged latency calculation.
- Relationship: New precise evidence gap within QA-440 correction, independent of actual close proof501.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| QA-440 / ANALYSIS-460 | Frozen five-file target above | Retained A route/page close evidence, held control, independent B frame and outage write now exist; parent actual single cycle passes with draft/context and bounded recovery | Original offline-toggle-only setup improved and succeeds on one actual path;500/501/502 prevent complete robust acceptance |
| ANALYSIS-500/501/502 | Same unchanged target | Independent actual-helper/method probes and installed source contracts | Reported; no implementation or ledger closure by reviewer |

## Assessment and limitations

The new helper establishes real closure evidence and preserves the original scenario checks on the actual successful single-disconnect run. That success must remain recorded as diagnostic evidence. It does not cover the invalid explicit close-code branch, mutable captured-identity interleaving with held replacements or enforced post-release/generation handshake attribution. The three bounded findings should be handled together without changing product behavior or acceptance thresholds.

Only this report and ignored analysis024 evidence were written. No implementation, tests, documentation, task state, ledger, prior report, current QA evidence, raw run, baseline inputs or shared outputs changed. Exact target identity remained stable. No build, host, browser, package, performance capture or runtime replay was started by this reviewer. The earlier aborted developer escalation is not a test outcome or proof of a permission restriction. The parent supplied and executed the successful actual run. The orchestrator owns triage, the authorized correction bundle and final acceptance; code-only scope excludes current documentation.
