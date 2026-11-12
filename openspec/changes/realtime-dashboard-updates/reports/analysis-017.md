# Analysis report: realtime-dashboard-updates / 017

## Review target

- Reviewer / role: second bounded acceptance-runner correction recheck / code-analyzer.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime-resolved model introspection unavailable.
- Date: 2026-11-09.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / analysis 017.
- Task and requirement references: [tasks 1.1, 7.2 and 7.3](../tasks.md); ANALYSIS-320/321 and regression ORCH-004/005/006 only. No automatic further correction cycle authorized by this review.
- Scope: `scripts/c06-acceptance-invariants.mjs`, `c06-fault-matrix.mjs`, `c06-host-lifecycle.mjs`, `c06-host-lifecycle.spec.mjs`, `c06-mcp-compat.mjs`, and `c06-process-outcome.mjs`.
- Reading limits / neighboring reads: scoped scripts and the prior role facts/template; prior own [analysis 015](analysis-015.md) and its existing source observations establish the host's acknowledgement-before-disposal ordering. Current `findings.md`, QA 017 and current QA evidence were not read. No host, build, Context7 request, approval prompt, native agent or installation/data access.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths scripts/c06-acceptance-invariants.mjs scripts/c06-fault-matrix.mjs scripts/c06-host-lifecycle.mjs scripts/c06-host-lifecycle.spec.mjs scripts/c06-mcp-compat.mjs scripts/c06-process-outcome.mjs`.
- Manifest: six untracked scoped files; no tracked diff. Tracked diff SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.

| Untracked file under `scripts/` | SHA-256 |
| --- | --- |
| `c06-acceptance-invariants.mjs` | `1d5c983dc1ad019a9d382ff3568c85f136986a895236593a6397ba335b7cd915` |
| `c06-fault-matrix.mjs` | `d05bf9fb62e9841ccf596753f254e2f42ffdc5b89ae59de82b87977a3b618729` |
| `c06-host-lifecycle.mjs` | `6b457597397738b445515d70a44868fb4656f14c14fdfc0501d135e4c770905e` |
| `c06-host-lifecycle.spec.mjs` | `0d6282463598e04a76b6ec7acaa0979fe31f2a7d9dfd7e59df64649c4ceb5f32` |
| `c06-mcp-compat.mjs` | `dd007a7e664720cd8fcb2f32e0e6717f5c3471ed055b284b81e1e56dc7bc2484` |
| `c06-process-outcome.mjs` | `491dd832f80ab574dd56c7511ef2337f6dd0702c2abba11bd8c83d4221c87dca` |

- Before/after identity: both match `head=0d77685 baseline=0d77685 files=6 diff=e3b0c44298fc untracked=6/9418f6589b14`; no scoped mutation detected.
- Prior reports / finding allocation: [analysis 015](analysis-015.md), ANALYSIS-320/321 rechecked. ANALYSIS-360 below; ANALYSIS-361 through ANALYSIS-379 unused. No suggestions.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Forced-cleanup test repeatability | `node scripts/c06-host-lifecycle.spec.mjs`; inspect the forced case | Exit 0. A fresh live fake child with a distinct PID and port replaces the previously exited shared child; the actual kill branch executes and incomplete-cleanup predicates remain tested | Passed; ANALYSIS-320 corrected at no-host test level |
| Actual MCP outcome wiring | Inspect `runOwned`, imported `processOutcome`, cleanup entry and returned spread | Both records use the same helper-produced object; no returned global process signal/error references remain | Passed by source inspection |
| MCP failure and success probes | Execute actual extracted `runOwned` and `requireSuccess` with imported actual `processOutcome`, stubbed `spawnSync` | Timeout, signal-only and nonzero cases preserve exact child metadata and are rejected. Ordinary zero-exit expected-text success is accepted. Cleanup entry and returned fields agree for every case | Passed independently without any child process; ANALYSIS-321 corrected |
| Startup-failure diagnostic wiring | Trace `startHost(... injectReadinessFailure:true)` and `startupFailureSmoke` branch | Real health/root readiness precedes intentional throw before successful startup returns; `onOwned` retains the wrapper early. The diagnostic requires wrapper exit, port release, host identity, source disposal and zero connections, and is explicitly labeled non-acceptance | Passed at source level for tracking and assertion fidelity. Actual diagnostic has not run; graceful-final-record race is ANALYSIS-360 |
| Delayed graceful-stop sequencing | Actual `stopTrackedHost` with a controlled acknowledgement-before-disposal model | Operation order is stop acknowledgement, kill wrapper, wait exit, read final. Wrapper/port proof succeeds but final record is absent; new startup diagnostic would reject it. The model could finish gracefully if offered the bounded exit wait before force kill | Failed for graceful-disposal evidence reliability; ANALYSIS-360. This does not allege an observed process leak |
| Persistent draft regression | Inspect unchanged existing block-selection/editor setup and pre/post-cycle assertions | One initial draft is created through the real composer; checked before injection and after every cycle; no per-cycle replacement draft introduced | Passed at source level; actual browser matrix not run |
| Existing acceptance predicates / syntax | `node scripts/c06-acceptance-invariants.mjs`; `node --check` for fault, lifecycle, lifecycle spec, MCP and process-outcome modules | Invariants and all syntax checks exit 0. Scoped whitespace check reports no errors | Passed |
| Historical MCP parity boundary | Retain prior inspected four-case evidence as historical success | Existing preserved/current crossed with Claude Code/Codex protocol fixtures was successful on its earlier executable/helper target; no authenticated native clients | Not rerun; no claim of current managed-assembly or final runtime acceptance |
| Target identity / own-report hygiene | Before/after identity and category-only own-report scan | Exact identity unchanged; no personal/machine/credential values or unintended residue in own report | Passed. Whole-change content hygiene deferred to final coordinated review |
| Actual startup/five-category/100-cycle execution | No host invocation under this assignment | Latest attempted execution was reported aborted before running; none of these runtime gates has been established by this review | Not performed; no passing runtime inference from no-host checks |

Independent evidence: `artifacts/realtime-dashboard-updates/reviews/analysis-017/no-host-recheck.mjs` and `no-host-results.json`. The probes execute actual current function bodies/modules with bounded controlled stubs; they do not reimplement production behavior or start a service.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Acceptance runners only | Source observation | No shipped idle loop changed. Startup diagnostic adds one explicitly selected run with existing bounded health/root/control/exit/port waits; no product CPU or I/O improvement claim |
| Cache lifetime versus interval and refresh duration | Startup/control/final records | Source observation | No product cache change. Readiness remains 30s/15s; stop acknowledgement is bounded at 3s, wrapper exit at 10s and port-release probe at 5s. The new branch requires a final disposal record, but the helper currently force kills before giving graceful shutdown its exit wait |
| Unbounded state | Matrix records, invocation-owned children, process metadata | Source observation | Existing fixed cycle plan, overall deadlines and invocation-local child ownership remain. The shared outcome helper returns three scalar fields; no retained history or product state added |
| Repeated full reads | Runner fixture records | Source observation | No new product read. Startup diagnostic reads small readiness/final fixture records and does not run the matrix or browser after its intentional failure |
| External tool discovery / timeouts | Existing owned Smoke wrapper | Source observation | Executable resolution, kill-on-close descendant ownership and child/deadline settings remain unchanged. Actual process cleanup, package/PATH and managed-assembly runtime behavior were not executed here |

## Findings

### ANALYSIS-360: Immediate post-acknowledgement kill can prevent the diagnostic's required disposal record

- Severity: warning.
- Related task / requirement: task 1.1 / ORCH-004; reliable owned startup-failure cleanup proof without abandoning graceful disposal evidence.
- Locations: `scripts/c06-host-lifecycle.mjs:24-31`; `scripts/c06-fault-matrix.mjs:984-999`. Existing host ordering is `tools/WorkLog.Smoke/C06LiveAcceptanceHost.cs:102-106,116-120` as inspected in the preceding review.
- Observation: The host writes its stop acknowledgement before awaiting `app.StopAsync` and writing `final.json`. The lifecycle helper treats that acknowledgement as the end of its graceful opportunity: if the wrapper is still alive it kills it immediately, and only then offers the 10-second exit wait. The new startup-failure diagnostic additionally requires `cleanup.final.source.isDisposed` and zero active connections. A valid shutdown taking longer than acknowledgement polling can therefore be force-terminated before the final record exists, making the diagnostic fail despite wrapper exit and port release.
- Proposed improvement and benefit: After a successful stop request, offer a bounded graceful-exit opportunity before killing the exact retained wrapper; retain force-kill fallback and bounded exit/port proof. Preserve the diagnostic's final-disposal requirement rather than weakening it.
- Behavior-preservation risks: Never wait indefinitely, adopt another PID/process name, or treat a stop acknowledgement alone as disposal proof. A failed/unknown-ready host still needs the owned force-stop path.
- Evidence type: Source ordering plus independently executed actual-helper probe with a controlled delayed-but-completable graceful stop. Actual host timing was not measured.
- Verification / measurement plan: A no-host delayed-acknowledgement/disposal case must reach final proof without an unnecessary kill; an unresponsive case must still be killed within bounds. Then run the actual startup-failure diagnostic and record wrapper exit, identified host/final disposal and port release.
- Relationship: New regression in the newly introduced disposal-proof diagnostic. ANALYSIS-320/321 are corrected; this finding is not an actual observed leak or a replacement ID for those fixes.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-320 | Six-file identity above | Existing lifecycle spec now passes with a distinct live forced child and port | Corrected at no-host regression level |
| ANALYSIS-321 | Same identity | Actual MCP runOwned/helper/requireSuccess probes cover timeout, signal, nonzero and success with identical cleanup/outcome metadata | Corrected independently at function level |
| ORCH-004 | Same identity | Early ownership capture retained; new after-ready failure diagnostic has strong assertions | Original lost-host-reference source mechanism remains corrected; actual cleanup gate unrun and ANALYSIS-360 blocks reliable graceful final proof |
| ORCH-005 | Same identity | Initial real composer draft and all pre/post-cycle checks retained | Prior source correction retained; runtime not performed |
| ORCH-006 | Same identity | No TDZ or metadata disagreement remains; negative cases rejected | Prior crash correction and metadata fix retained; historical four-case success not rerun |

## Assessment and limitations

Both findings from analysis 015 are corrected and independently rechecked. The no-host lifecycle suite and acceptance invariants pass; actual MCP function wiring now reports and rejects failure outcomes consistently. One residual issue remains in the new startup-failure diagnostic: the helper can force-kill a healthy shutdown immediately after acknowledgement, preventing the final disposal evidence that the diagnostic now requires.

Only the reserved report and ignored analysis-017 probe/evidence were written. Scoped identity remained stable. No implementation, tasks, ledger, prior reports, current QA material, baseline raw evidence or shared binaries were changed. No actual host, startup-failure smoke, five-category/full matrix, build or native client was run. This is the second bounded recheck; further correction or acceptance decisions belong to the orchestrator rather than an automatic additional cycle.
