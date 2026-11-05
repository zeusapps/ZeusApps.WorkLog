# Analysis report: add-repository-card-colors / 005

## Review target

- Reviewer / role: independent assigned reviewer / code-analyzer
- Client / version / mode / model (resolved ID): Codex CLI 0.165.0 / native agent mode / gpt-6.1-sol, high reasoning. Client/version supplied by orchestrator; model is resolved role default.
- Date: 2026-11-04 20:22:27 UTC
- Change root / assignment: `openspec/changes/add-repository-card-colors` / ANALYSIS-REVIEW-005, authorized test-only ANALYSIS-061 correction.
- Task and requirement references: [tasks 3.1–3.3](../tasks.md); [Stable repository color cues](../specs/work-dashboard/spec.md), including reload/column movement; regression coverage for contrast, neutral footers, narrow bounds, and later board tests.
- Scope and relevant neighboring paths: same expanded scope: `dashboard/src`, `dashboard/e2e`, `dashboard/playwright.config.ts`, `tools/WorkLog.Smoke/DashboardFixture.cs`. Inspected revised activity fixture, cleanup and retained assertions, application activity loading, `ChangeActivity` fields, and pure stage derivation. Prior architecture/CSS context retained from earlier assigned reviews.
- Reading limits and paths read outside scope: earlier role/template/planning/report context retained; assigned identity script executed. No additional out-of-scope filesystem paths read. Current Playwright API excerpts/primary-source references supplied by orchestrator from its version-specific ctx7 lookup, not separately fetched here. No browser/harness run or personal installation/settings access.
- Baseline commit: `7f45c373a6e4637edf8fd4a5853d11b5170bc1c5`
- Implementation identity: before analysis:

```text
./scripts/review-identity.ps1 -Baseline 7f45c373a6e4637edf8fd4a5853d11b5170bc1c5 -Paths dashboard/src dashboard/e2e dashboard/playwright.config.ts tools/WorkLog.Smoke/DashboardFixture.cs
HEAD: 7f45c373a6e4637edf8fd4a5853d11b5170bc1c5
Baseline: 7f45c373a6e4637edf8fd4a5853d11b5170bc1c5
Paths: dashboard/e2e dashboard/playwright.config.ts dashboard/src tools/WorkLog.Smoke/DashboardFixture.cs
Manifest:
  ? dashboard/src/repository-colors.spec.ts
  ? dashboard/src/repository-colors.ts
  M dashboard/e2e/dashboard.spec.ts
  M dashboard/e2e/server.mjs
  M dashboard/playwright.config.ts
  M dashboard/src/app.board.spec.ts
  M dashboard/src/app.html
  M dashboard/src/app.ts
  M dashboard/src/styles.css
Tracked diff SHA-256: 56bf02e4ead9305a5a77ecc00a9bedc7a0b517d8d04f331648ea2e56ff124804
Untracked file SHA-256:
  7189c8dbf8a47d0842fe6d7cf0aa3ab0411a073b326db79ec3a5c365ad6b5a74  dashboard/src/repository-colors.spec.ts
  dddebb1b52235f220e15d706e4121129b88952e643521698ec8579700d7934b9  dashboard/src/repository-colors.ts
Summary: head=7f45c37 baseline=7f45c37 files=9 diff=56bf02e4ead9 untracked=2/c03c9bfee23b
```

- Before/after identity: repeated identical command after independent source checks. Full manifest, HEAD/baseline, tracked hash, both untracked hashes, and summary match exactly. After summary: `head=7f45c37 baseline=7f45c37 files=9 diff=56bf02e4ead9 untracked=2/c03c9bfee23b`. No unexpected mutation.
- Prior reports / rechecked findings: [analysis-001.md](analysis-001.md), [analysis-002.md](analysis-002.md), [analysis-003.md](analysis-003.md), [analysis-004.md](analysis-004.md). Original `77e6ccd1c7e4` target mapped through neutral/harness `534043eb0e8d`, synthetic cleanup `ef93906b78fd`, responsive/reversible-task `f238b66a2d97`, then current test-only activity-response correction `56bf02e4ead9`. Product CSS/helpers remain unchanged from round 004; helper hashes unchanged throughout. Recheck ANALYSIS-061 plus isolation/regression, not a fresh full implementation review.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Stable target | Assigned identity command before/after | Exact matching identity above | Current frozen correction reviewed; only assigned report written. |
| Response scope and preservation | Inspect `dashboard/e2e/dashboard.spec.ts:1343`; execute actual transformation body extracted from source in Node | Handler matches activity endpoint, fetches real fixture response, filters only exact `example/board-demo` plus `board-proposal`, and appends one synthetic activity. Four executed cases: empty array, unrelated only, existing target, duplicate target. Unrelated object references/order and original input remain intact | All cases passed. Same change name under another repository and another change under the target repository are preserved. This is explicitly a temporary synthetic API response, not a persisted observation or backend mutation. |
| Record shape and stage fidelity | Compare handler fields with `dashboard/src/models.ts:28`, `app.ts:553`, and `board.ts:75`; execute actual pure stage-function body | Synthetic record has repository/change, equal valid ISO first/last timestamps, and `lastStatus: in_progress`. With unavailable task totals, actual stage source returns proposed without activity and in_progress with this record | Passed. App reload consumes the activity API through normal loading and derives the moved card; test does not alter board DOM, task files, journal, or application stage logic. Color is checked on the real in-progress card and matching sidebar dot. |
| Route lifecycle and persistent isolation | Inspect `dashboard.spec.ts:1392`, `:1455`; execute source invariants | Named handler registered only after initial real-data checks; installed flag gates awaited removal of that exact handler/pattern in `finally`, before preference reset in nested `finally`. No task-file write, logger call, or no-Node skip branch remains in this test | Source check passed. Other activity handlers are not removed indiscriminately. Existing test preferences/screenshots remain intentional test operations; no source/journal/task persistence is introduced by the movement fixture. |
| API-contract provenance | Orchestrator-supplied ctx7 resolution `/microsoft/playwright/v1.64.1`, matching installed version | Official [Route API](https://github.com/microsoft/playwright/blob/v1.64.1/docs/src/api/class-route.md), [Page API](https://github.com/microsoft/playwright/blob/v1.64.1/docs/src/api/class-page.md), and [mock examples](https://github.com/microsoft/playwright/blob/v1.64.1/docs/src/mock.md) support fetching a response, overriding its JSON body during fulfillment, and removing a specified handler | Documentation verification supplied by root, not a new reviewer lookup. Source uses the documented fetch/parse/fulfill pattern and exact-handler cleanup. Browser results below separately exercise it. |
| Retained regression assertions | Execute source check on current color-test section | Six palette/shared/missing contrast assertions and six narrow overflow/control/footer checks remain. Initial selection/filter/reload checks, actual stage movement/color, synthetic-section cleanup, and screenshot prefixes retained | Passed. No skipped assertions or dependency on task totals/Node introduced. |
| Normal current-target runtime | Orchestrator-supplied fresh isolated Release run, `qa-006` evidence prefix | Eight tests passed, exit 0, 34.5 seconds; includes movement/contrast/narrow checks plus later filtering/order/themes | Supplied evidence, not reviewer execution. Serial later tests passing support absence of leaked fixture activity. |
| Packaged no-Node current-target runtime | Orchestrator-supplied fresh isolated run, `qa-007` prefix, executable `artifacts/repository-card-colors-validation/worklog-0.4.1-win-x64/worklog.exe` | Eight tests passed, exit 0, 29.1 seconds, with `WORKLOG_DASHBOARD_NO_NODE=true`; existing harness also points runtime discovery at an absent fixture .NET location. Movement/contrast/narrow and later board/theme tests passed | Supplied actual self-contained packaged validation directly covers ANALYSIS-061's failing mode. No packaged/browser execution by this reviewer. |
| Prior original/neutral/responsive analysis carried forward | Explicit target mapping above; correction limited to browser fixture | Shared exact-ID mapping, fixed palette, all card kinds/deduplication, decorative dots, neutral bodies/footers, status meanings, ranks/preferences, API/polling, neutral contrast correction, and narrow control CSS unchanged from prior reviewed targets | Prior source conclusions carry forward; no claim of repeating the original full review. Prior source-derived contrast remains 9.471005 dark / 5.351738 light for neutral text, 4.734769 minimum tinted text. Current supplied browser runs verify contrast and bounds in both deployment modes. |
| Duplication / complexity / architecture / performance regression | Inspect bounded fixture replacement | One local handler with linear array filtering, one appended record, and explicit cleanup; no product source changes | No new concrete regression or suggestion. Dashboard remains client-neutral; no integration/MCP parity change. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Unchanged dashboard polling; temporary response fixture | Source observation | Product still polls eligible visible catalog every two seconds, about 30 requests/minute even while unchanged; revision/snapshot gates limit further work. Correction adds no product idle request/process/file/journal work. While test handler is installed, matching activity requests fetch the existing real response and transform its array; no new polling loop or persisted observation is created. |
| Cache lifetime versus interval and refresh duration | Pure palette, existing activity key, route fixture | Source observation | No product cache/expiry/interval changed. Existing activity key remains journal snapshot plus selected repositories; reload causes fresh activity loading for the fixture transition. No runtime-duration measurement beyond supplied browser suite durations. Prior catalog no-store/overlap analysis unchanged. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Product palette/order; local route state | Source observation | Fixed eight palette keys, temporary per-card lists, existing 200-rank cap and preference-byte fit unchanged. Handler data is proportional to fetched array length, with matching target records replaced by one; no historical accumulation. Handler removed in finally; synthetic palette DOM removed before narrow checks. Existing prefix-selected screenshots/preferences remain test artifacts/state. |
| Repeated full reads where a change check would do | Runtime client; activity interception | Source observation | No new product full file/journal/document reads and no fixture task-file read/write. Handler parses the existing matched response once and filters it once per intercepted call; work is linear in response size. Backend read costs remain outside this scope and unmeasured. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Unchanged isolated harness; corrected movement test | Source observation and supplied runtime | Movement fixture now requires no external executable or task CLI. Existing harness dotnet/build-output discovery and startup/shutdown limits are unchanged. Supplied actual self-contained no-Node run passes with tooling paths excluded. No external service/browser/harness process launched by this reviewer. |

## Findings

**No new findings.** Reserved ANALYSIS-081 through ANALYSIS-100 unused. ANALYSIS-061 correction is verified below; earlier reports remain immutable history.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-061 | `head=7f45c37 baseline=7f45c37 files=9 diff=56bf02e4ead9 untracked=2/c03c9bfee23b` | Executed real transformation/stage source and lifecycle/assertion checks; no task/Node dependency. Root supplies passing complete normal and packaged no-Node eight-test serial runs | Correction verified in source and supplied rendered validation, including later-test isolation. Orchestrator owns finding closure. |
| ANALYSIS-001 and QA-001, regression context | Same current target | Unchanged prior neutral/responsive source corrections; current normal and packaged runs supply passing palette/neutral contrast and real-board narrow bounds | Earlier source conclusions remain applicable; supplied runtime passes in both modes. No new independent browser measurement by this reviewer. |

## Assessment and limitations

The correction removes the task-tool dependency and permanent fixture mutations while preserving genuine application stage derivation from an explicitly synthetic activity response. Exact handler cleanup and preservation of unrelated records are proportionate. Source checks and supplied normal/packaged runs support ANALYSIS-061 remediation without a new regression.

Only `reports/analysis-005.md` was written; no implementation, tasks, ledger, earlier report, personal settings, or installation changes. Identity matched before and after. Independent execution covered generic response transformation, actual stage logic, and source invariants; API documentation/build/browser outcomes are explicitly supplied by the orchestrator. The orchestrator owns final finding status and feature-completion decisions.
