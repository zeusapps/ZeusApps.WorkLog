# Analysis report: realtime-dashboard-updates / 009

## Review target

- Reviewer / role: independent code-analyzer, ANALYSIS-RECHECK-009.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime-resolved model introspection unavailable.
- Date: 2026-11-08.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-009.
- Authorization / scope: focused continuation authorized by the user's explicit continuation, as recorded by the orchestrator. Recheck existing ANALYSIS-081–084 and ANALYSIS-140 plus regression only; no new suggestions.
- Task references: [tasks 4.1–5.4](../tasks.md), [reader delta](../specs/openspec-browser/spec.md), [dashboard delta](../specs/work-dashboard/spec.md), and the exact remaining cases in [analysis 006](analysis-006.md).
- Scope: `dashboard/src`, `dashboard/package.json`, `dashboard/package-lock.json`. Backend and acceptance-harness corrections are concurrent and excluded.
- Reading limits / neighboring reads: assigned role, project facts and template; analysis 006 and its original probe; scoped production controller, revision queue/tracker, App methods and relevant regression cases. Analysis 008's report header was read only as a report-format reference, with no backend recheck. Concurrent QA 009 conclusions were not read. Historical reports and evidence were not edited.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths dashboard/src dashboard/package.json dashboard/package-lock.json`.
- Manifest: 14 files. Tracked modifications: both package manifests and `dashboard/src/{app.daily.spec.ts,app.html,app.reliability.spec.ts,app.reviews.spec.ts,app.spec.ts,app.ts,styles.css}`. The five untracked files are listed below.
- Tracked diff SHA-256: `09b6afb81add2a0cfea4b60c62fcb1088ada9c87fc5a8c20ba7d9ae958588308`.

| Untracked file under `dashboard/src` | SHA-256 |
| --- | --- |
| `app-live-test-helpers.ts` | `0f57c6112a8ddce266144e1430daf13b0a918cfef061744d53147efface636d1` |
| `live-connection.spec.ts` | `07ff19e17eea9953b0d78db949d1b9ad9217b7b49950da048b882bcc32282913` |
| `live-connection.ts` | `78db24467ab95c0221d16c986c57ee9e59ff27a35a7140cfd032228b6783e5ac` |
| `live-updates.spec.ts` | `3aa4f02d20aaf0919e32076f05375394b5f714940d2cd24fff25fb3ad0f00119` |
| `live-updates.ts` | `1430c9e42ae7790af48e9c48af8eefbd6decf65faf94f085b0f7c5319ee16d54` |

- Before/after identity: matching initial, intermediate and final outputs: `head=0d77685 baseline=0d77685 files=14 diff=09b6afb81add untracked=5/bdb52114448e`. No scoped mutation detected.
- Finding allocation: existing ANALYSIS-081–084 and ANALYSIS-140; regression range ANALYSIS-200–219 unused.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Authorization lifecycle | Inspect controller epoch guards and independently defer failed-start cleanup, confirm authorization loss, then release cleanup | Actual transpiled production controller produces zero subsequent protected classification probes | Remaining ANALYSIS-081 mechanism corrected; fake transport/probe only |
| Teardown and active continuations | Extract actual `loadOngoing` and `ngOnDestroy` bodies with the TypeScript AST; defer first timeline page, destroy, return another-page offset | One pre-destruction timeline call; zero subsequent calls. Source also checks after each later page and before unlinked state/timer publication | Remaining ANALYSIS-082 mechanism corrected; no actual HTTP or Angular teardown execution |
| Navigation supersession | Extract production reconciliation methods; use actual revision tracker/refresh queue, defer catalog for A, select B, release A, then deliver the identical revision | Two catalog loads and no pending domains; follow-up is scheduled for the current selection without a reads-counter dependency | ANALYSIS-083 case corrected; no real router/copy-navigation execution |
| Deferred focus restoration | Extract actual baseline method/focus helpers; complete response, focus a newer connected input, then flush the restore timer | Focus remains `newer-input`; source checks current ownership in the callback and retains selection/revision/scroll guards | ANALYSIS-084 case corrected; fake DOM does not establish browser caret/composition behavior |
| Recovery bounds | Execute actual controller with close on every pre-reconciliation Snapshot; then explicit Retry and a separate close after healthy Live | Exactly five starts/Snapshots before Paused; Retry reaches Live at start six; new healthy outage reaches Live at start seven | ANALYSIS-140 corrected in the deterministic module probe; backoff wall time was replaced by an immediate injected delay |
| Adjacent regression | Rerun original close-during-Snapshot, authorization-during-backoff and queue-clear cases | Two starts/two Snapshots then Live; one start then Paused; zero queued follow-ups respectively | Earlier corrected mechanisms retained on this target |
| Proportional verification | `node artifacts/realtime-dashboard-updates/reviews/analysis-009/recheck-probes.cjs`; `node --check` on that file; scoped `git diff --check`; identities | Assertions pass, syntax and whitespace checks pass; scoped identity unchanged | No host, build, package, full suite, real data, installation or native agent started by this reviewer |

Evidence: [independent probe source](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-009/recheck-probes.cjs) and [exact results](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-009/results.json). The probe copies the historical 006 harness into new 009 evidence and extracts the current production methods/modules at execution; it does not duplicate the corrected logic. Its obsolete eleven-start success expectation now requires five starts then Paused, with explicit Retry and a new healthy outage checked separately. The fake catalog defers only the original read so the newly correct follow-up can complete. The retry probe waits for the single-flight operation to settle before issuing explicit Retry; an earlier immediate call joined the existing operation, which is expected single-flight behavior. All waits are bounded, and no historical evidence was changed.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Controller and App revision drain | Source observation | Corrections add epoch/counter/focus checks, not a polling interval. Healthy delivery remains notification-driven; the existing UI clock can wake at most once per minute or an earlier temporal boundary. Server checkpoint/transport costs and actual idle requests are outside this review and unmeasured |
| Cache lifetime versus interval and refresh duration | RefreshQueue and revision tracker | Source observation | Successful resource tokens avoid identical work; one current-scope forced follow-up handles a superseded selection. At most one follow-up is scheduled per selection epoch until successful reconciliation resets that guard. Failed work remains pending; no new time-based cache or forced periodic full read is added |
| Unbounded state | Controller counters/epochs and pending recovery/revision | Source observation plus bounded module result | One controller operation and retained close intent; five automatic starts share the outage budget until healthy reconciliation. Explicit Retry or planned visibility recovery can begin a new budget. New fields are scalars; supersession reuses one pending merged revision, not a list. Existing queue keys/lifetimes are unchanged |
| Repeated full reads where a change check would do | Catalog/reader navigation and Ongoing pagination | Source observation plus method results | A superseded documents refresh performs the necessary current-selection follow-up once in the probe. Destroyed Ongoing loading stops before requesting the next page/unlinked data and before state/timer publication. Existing baseline bounded reread behavior remains; full-workload request cost is unmeasured |
| External tool discovery / timeouts | Browser controller | Source observation | Browser code introduces no external executable discovery or processes. Start/Snapshot/proof retain the existing eight-second default deadline and bounded backoff. Transport stop itself remains awaited as before; no changed external lookup/install behavior is in this correction |

## Findings

**No findings** within this focused recheck and adjacent regression scope. No new finding ID was allocated. Source/module verification below is not final runtime acceptance or ledger closure.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-081 | `live-connection.ts` failed-start catch, authorization/lifecycle epochs | Loss during deferred stop produces zero protected probes after release; original loss-during-backoff remains one start/Paused | Corrected at source/module level |
| ANALYSIS-082 | `app.ts:426`, `:1304` onward and unchanged queue cancellation | Destruction invalidates `ongoingEpoch`; first-page continuation produces no next page/unlinked read. Later-page and final publication guards inspected. Queue-clear probe remains zero follow-ups | Corrected at source/method level |
| ANALYSIS-083 | `app.ts:731` onward, pending revision drain | Documents-only update overlapping successful A-to-B navigation now performs two catalog loads and clears pending domains without another write | Corrected at source/method level |
| ANALYSIS-084 | `app.ts:855` onward, actual restore callback | Newer connected input focused after response and before timer retains focus; callback rechecks live ownership | Corrected at source/method level |
| ANALYSIS-140 | `live-connection.ts` outage counter and healthy/reset paths | Five pre-reconciliation closes exhaust and pause; explicit Retry succeeds on sixth start; later healthy outage succeeds on seventh | Corrected at source/module level |

## Assessment and limitations

All five assigned remaining mechanisms are corrected in source and pass independent bounded probes against the current production module/method bodies. Adjacent original controller and queue examples continue to pass. No architectural duplication or changed accumulation mechanism requiring a regression finding was observed within this correction. The dashboard remains client-neutral; MCP/integration parity is outside this browser correction.

The developer reports 303/303 unit checks and a warning-free product build; neither was independently rerun here. These results and the in-memory checks do not prove a real SignalR/HTTP lifecycle, actual Angular routing/destruction, browser focus/caret/composition, final packaged browser acceptance, or performance measurements. Backend and harness conclusions remain in their separately assigned reports. The orchestrator owns ledger disposition and final acceptance.

Only this report and ignored 009 probe evidence were written. Scoped whitespace/syntax and identity checks passed; report hygiene was checked for secret, personal/machine and residue categories. No source, task state, ledger, historical report or historical evidence was modified; no approval question was issued.
