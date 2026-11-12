# Analysis report: realtime-dashboard-updates / 015

## Review target

- Reviewer / role: independent acceptance-runner recheck / code-analyzer.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime-resolved model introspection unavailable.
- Date: 2026-11-09T08:59:18Z.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / analysis 015.
- Task and requirement references: [tasks 1.1, 7.2 and 7.3](../tasks.md), bounded owned-host cleanup, persistent draft checks and matched shared-protocol fixtures; existing ORCH-004, ORCH-005 and ORCH-006.
- Scope: `scripts/c06-acceptance-invariants.mjs`, `c06-fault-matrix.mjs`, `c06-host-lifecycle.mjs`, `c06-host-lifecycle.spec.mjs` and `c06-mcp-compat.mjs`.
- Reading limits / neighboring reads: scoped scripts, existing role/project facts/template, `C06ProcessTree.cs`, `C06LiveAcceptanceHost.cs`, launcher job-object source, the reader's existing selection/composer handlers, and the supplied successful MCP JSON. Concurrent QA 015 conclusions/evidence were not read; broad concurrent change-root text scanning deferred as assigned. No host, build, performance run, Context7 request, native agent or real installation/data access.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths scripts/c06-acceptance-invariants.mjs scripts/c06-fault-matrix.mjs scripts/c06-host-lifecycle.mjs scripts/c06-host-lifecycle.spec.mjs scripts/c06-mcp-compat.mjs`.
- Manifest: all five files untracked; no tracked diff. Tracked diff SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.

| Untracked file under `scripts/` | SHA-256 |
| --- | --- |
| `c06-acceptance-invariants.mjs` | `1d5c983dc1ad019a9d382ff3568c85f136986a895236593a6397ba335b7cd915` |
| `c06-fault-matrix.mjs` | `1e271a84e988513375477987f3c7de5bee98280f64ba8c97f58718c6dad4455b` |
| `c06-host-lifecycle.mjs` | `6b457597397738b445515d70a44868fb4656f14c14fdfc0501d135e4c770905e` |
| `c06-host-lifecycle.spec.mjs` | `2c3f5e5b374b7fa619d927d20e0790b2ddaae95935f65b3eda7586239059b355` |
| `c06-mcp-compat.mjs` | `e1b2f9c74662229cd7f7efff940613c2c0d26ff7387156236e807d4e2085bba0` |

- Before/after identity: both match `head=0d77685 baseline=0d77685 files=5 diff=e3b0c44298fc untracked=5/b2822b1e8561`; no scoped mutation detected.
- Finding allocation: ANALYSIS-320 and ANALYSIS-321 below; ANALYSIS-322 through ANALYSIS-339 unused. Recheck/regression only, no suggestions.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Startup ownership retention | Trace `startTrackedHost` and matrix startup/finally wiring | `onOwned(host)` runs before either readiness wait, retaining the exact spawned child, wrapper PID and chosen port on failure. `startHost` assigns this retained object to the matrix host immediately; final cleanup sees it even when awaited startup throws | Passed at source level; independently exercised with a controlled no-host readiness rejection |
| Cleanup ownership and bounds | Actual lifecycle module with a fresh stub live child and failing ready-file read | Only that child is killed; no graceful command targets an unknown ready identity; exit wait is 10,000ms and port probe receives only the retained port. Cleanup predicate rejects either missing wrapper-exit proof or failed port release | Passed at function-probe level. No actual process/port cleanup was executed by this reviewer. Existing wrapper's kill-on-close job owns descendants; source contains no global process-name adoption |
| Shipped lifecycle regression | `node scripts/c06-host-lifecycle.spec.mjs` | Exit 1 at line 48, `false !== true`, reproduced twice. The prior graceful case already marked the reused fake child exited | Failed: ANALYSIS-320. This is a repeatability/coverage defect in the new test, not proof of an actual forced-cleanup failure |
| Persistent draft setup | Trace matrix `openDocument`, initial fill, `prepareVisibleState` and `assertCycleState`; compare existing reader UX | Matrix clicks a real rendered `[data-start]` block, which reaches existing `readerClick`/`selectBlock`; it waits for the conditional comment editor, fills one initial draft before the loop, verifies it before every injection, and compares the same draft after each cycle | Passed by source inspection. No hidden App-state mutation or per-cycle replacement draft; actual browser execution not performed here |
| MCP TDZ and failure metadata | Extract current `runOwned` body and inject a stub `spawnSync` timeout/signal result | Environment now reads global `process.env` without a shadowing local declaration; startup TDZ is removed. Cleanup records the injected signal/timeout correctly, but returned per-case outcome uses unrelated global fields | Partial: original crash fixed; failure metadata regression is ANALYSIS-321 |
| Supplied successful compatibility run | Independently parse `mcp-compat/2026-11-09T08-52-11-830Z-839b460f.json` | Four passed cases: preserved/current executable crossed with Claude Code/Codex protocol labels. Twelve wrapper exit codes are zero; final preserved/current hashes match their initial values. Both native-client session fields say not run | Passed as inspection of supplied historical execution, not rerun. Failure-metadata defect does not invalidate these observed successful case results |
| Existing proof mechanisms and syntax | `node scripts/c06-acceptance-invariants.mjs`; `node --check` for fault and MCP runners | No-host acceptance invariants passed; both syntax checks passed. Existing 100-cycle 25/25/20/15/15 allocation and deadlines remain in source | Passed. No full or five-category host matrix was run by this reviewer |
| Whitespace, identity and own-report hygiene | Scoped whitespace check, before/after identity, category-only report scan | No whitespace errors; exact identity unchanged; no personal/machine/credential value or unintended residue in own report | Passed. Broad change-root scan deferred during concurrent review |

Independent probe source and structured results are under ignored `artifacts/realtime-dashboard-updates/reviews/analysis-015/no-host-probes.mjs` and `no-host-probe-results.json`. They execute current source bodies with controlled stubs and never spawn a process, bind a port or contact a service.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Acceptance runners only | Source observation | No shipped idle loop changed. Matrix readiness/control polling is bounded, uses disposable fixtures and exact owned wrappers, and exits through cleanup. No product CPU/I/O claim follows from these checks |
| Cache lifetime versus interval and refresh duration | Runner readiness/control records | Source observation | No product cache changed. Health/root waits remain 30s/15s, stop acknowledgement 3s, wrapper exit 10s and port-release probing 5s. These are runner bounds, not measured successful recovery latency |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Matrix records, owned children, MCP cases | Source observation | Matrix has a fixed 100-cycle plan or explicitly labeled five-category smoke and an overall deadline. Child membership is invocation-owned and removed on exit. MCP has four fixed cases and bounded child calls. Probe data is small and ignored; no product accumulation changed |
| Repeated full reads where a change check would do | Test-only runner reads | Source observation | Ready/control/final records are small fixture files; executable hashes are checked at run boundaries. No new product full read or browser polling owner introduced |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Existing Smoke owned-process wrapper | Source observation | Exact local Smoke DLL is invoked through `dotnet`; helper resolves the requested executable and uses the existing Windows kill-on-close job. Existing child timeouts/deadline remain. Runtime install/PATH variants and failure cleanup not independently executed here |

## Findings

### ANALYSIS-320: Forced-cleanup regression reuses an already-exited fake child

- Severity: warning.
- Related task / requirement: task 1.1; ORCH-004 startup-failure cleanup regression coverage.
- Locations: `scripts/c06-host-lifecycle.spec.mjs:22,37-48` at the reviewed identity.
- Observation: The graceful case sets the shared child's `exitCode` to zero. The forced-cleanup case then reuses that same child without restoring live state. Its `childHasExited` returns true, so `killChild` never executes and `wrapperKilled` remains false. The line 48 assertion fails deterministically. The assertion does not establish an actual cleanup failure; the scenario never reached its intended live-child branch.
- Proposed improvement and benefit: Give the forced-cleanup scenario its own initially live fake child and local observation variables, preserving assertions that only the owned wrapper is stopped, no command targets an unknown host, exit is observed and its port is released.
- Behavior-preservation risks: Do not weaken/remove the kill assertion or merely pre-set its observation flag. Keep incomplete-cleanup rejection checks.
- Evidence type: Independently executed no-host test failure; a separate fresh-child probe of the actual helper passed.
- Verification / measurement plan: Rerun the existing lifecycle test and a controlled fresh-child/no-ready failure case; actual startup/process/port proof remains a separate runtime gate.
- Relationship: New test-regression defect adjacent to ORCH-004; original ownership-retention source mechanism is corrected.

### ANALYSIS-321: MCP per-case timeout and signal metadata still reads the global process

- Severity: warning.
- Related task / requirement: task 7.3; ORCH-006 must preserve truthful bounded failure reporting while removing the TDZ.
- Locations: `scripts/c06-mcp-compat.mjs:75,85,91-92` at the reviewed identity.
- Observation: `spawnSync` now returns into `childResult`, and the cleanup array correctly reads that result. The returned outcome still reads `process.signal` and `process.error`, which refer to the global Node process. An injected child result with `signal: SIGTERM`, `error.code: ETIMEDOUT` and null status produces a correct cleanup entry but returns `signal: null` and `timedOut: false`. Exit status still prevents that injected case from passing; the defect misstates its recorded failure reason.
- Proposed improvement and benefit: Read both returned fields from `childResult` and add a no-process timeout/signal regression, keeping fixture bodies, deadlines, success-text and nonzero-exit checks unchanged.
- Behavior-preservation risks: Do not suppress child errors or accept a null/nonzero exit. Successful four-case execution remains valid historical evidence.
- Evidence type: Source observation and independently executed current-function probe with controlled `spawnSync` result.
- Verification / measurement plan: Inject timeout, signalled and ordinary success outcomes; verify per-case fields agree with the cleanup entry and failure cases remain rejected.
- Relationship: Residual variable-rename regression in the ORCH-006 correction; original startup TDZ is removed.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ORCH-004 | Five-file identity above | Early ownership capture and cleanup wiring; current-module fresh live-child failure probe passes with only owned kill/port operations | Source/function mechanism corrected; shipped regression fails under ANALYSIS-320. Actual startup process/port execution not performed here |
| ORCH-005 | Same identity, matrix initial document/draft setup | Existing rendered-block selection opens the real composer; one initial draft is checked before and after all cycle categories | Corrected by source inspection; browser behavior not executed by this reviewer |
| ORCH-006 | Same identity, MCP runner | No local shadowing of global process; supplied four-case run passes. Independent controlled failure exposes returned metadata disagreement | Original TDZ corrected; ANALYSIS-321 requires the bounded metadata correction |

## Assessment and limitations

The three original runner failure mechanisms have concrete source corrections, and the successful four-case shared-protocol run remains valid historical success. Two narrow regressions remain: the new forced-cleanup test fails before exercising its intended branch, and MCP per-case timeout/signal fields still read the wrong object. Neither finding alleges that an actual owned process leaked or that the successful protocol fixtures failed.

Only this report and ignored analysis-015 evidence were written. No source, ledger, tasks, baseline raw data, prior report or QA evidence was changed. Before/after identity matches. No host, build, actual process/port failure experiment, browser/fault matrix, native client or installation check was run. The orchestrator owns finding disposition and the next correction/independent QA handoff.
