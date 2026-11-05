# Analysis report: add-repository-card-colors / 004

## Review target

- Reviewer / role: independent assigned reviewer / code-analyzer
- Client / version / mode / model (resolved ID): Codex CLI 0.165.0 / native agent mode / gpt-6.1-sol, high reasoning. Client/version supplied by orchestrator; model is resolved role default.
- Date: 2026-11-04 20:14:08 UTC
- Change root / assignment: `openspec/changes/add-repository-card-colors` / ANALYSIS-REVIEW-004, user-authorized additional correction round.
- Task and requirement references: [tasks 2.2, 3.1–3.3](../tasks.md), [spec delta](../specs/work-dashboard/spec.md). Recheck QA-001 and correction regression, including test isolation and faithful stage movement; carry unaffected original and ANALYSIS-001 analysis.
- Scope and relevant neighboring paths: `dashboard/src`, `dashboard/e2e`, `dashboard/playwright.config.ts`, `tools/WorkLog.Smoke/DashboardFixture.cs`; architecture context retained from `docs/architecture.md`. Inspected responsive CSS diff, narrow assertions, movement/restoration test, existing packaged-mode expectations, board stage function, and synthetic BoardDemo setup.
- Reading limits and paths read outside scope: earlier role/template/planning/report context retained; assigned `scripts/review-identity.ps1` executed. No additional out-of-scope paths read. No browser/harness run, personal installation/settings, or default dashboard port accessed.
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
Tracked diff SHA-256: f238b66a2d97bd15721fc1483b1d392def7e19a86605236f88fd1343927d406e
Untracked file SHA-256:
  7189c8dbf8a47d0842fe6d7cf0aa3ab0411a073b326db79ec3a5c365ad6b5a74  dashboard/src/repository-colors.spec.ts
  dddebb1b52235f220e15d706e4121129b88952e643521698ec8579700d7934b9  dashboard/src/repository-colors.ts
Summary: head=7f45c37 baseline=7f45c37 files=9 diff=f238b66a2d97 untracked=2/c03c9bfee23b
```

- Before/after identity: identical command repeated after source checks. Complete outputs match, including both helper hashes and full tracked hash. After summary: `head=7f45c37 baseline=7f45c37 files=9 diff=f238b66a2d97 untracked=2/c03c9bfee23b`. No unexpected mutation.
- Prior reports / rechecked findings: [analysis-001.md](analysis-001.md), [analysis-002.md](analysis-002.md), [analysis-003.md](analysis-003.md). Original seven-file `77e6ccd1c7e4` target mapped to nine-file neutral/harness correction `534043eb0e8d`, then synthetic cleanup `ef93906b78fd`, now responsive CSS plus reversible task fixture `f238b66a2d97`. Helper hashes unchanged throughout. Rechecked ANALYSIS-001 and QA-001 context; reserved new IDs ANALYSIS-061 through ANALYSIS-080.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Stable target | Assigned identity command before/after | Exact matching identity above | Current target matches assignment; only this report written. |
| QA-001 responsive correction | Inspect `dashboard/src/styles.css:2199` and corresponding template controls | At widths up to 600 pixels, board heading wraps; filter/input use available width; review controls wrap with bounded editor select and shrinking heading text. No overflow-hiding rule, footer geometry, global color token, or status selector added | Bounded correction addresses the actual controls identified by supplied measurements: review controls right edge 423.4 and fixed board filter 410.4 in a 390-pixel viewport, while footers already fitted at 81–374. Measurements are supplied by root, not executed here. |
| Narrow acceptance coverage | Inspect `dashboard/e2e/dashboard.spec.ts:1401` and execute Node source assertions | All six checks retained: document overflow, board heading, filter, filter input, review controls, and footer clipping. Synthetic palette evidence still removed before measuring real board | Source assertion check passed. No masking overflow or relaxed footer bounds found. |
| Reversible fixture isolation | Inspect `dashboard.spec.ts:1370`, `:1434`; Node script checks actual test source | Original UTF-8 tasks text saved before modification; first unchecked task changed only after asserting content differs; exact saved text restored in guarded `finally`, with nested `finally` preserving preference reset. New color test no longer invokes `--dashboard-log` | Normal successful/error paths restore disposable fixture content before reset; no permanent journal append remains. Read failure leaves saved value null, avoiding a fabricated overwrite. Subsequent normal-mode tests passing in root's coordinated run support isolation. |
| Faithful stage movement | Inspect `board.ts:75`, BoardDemo fixture setup, and actual test; execute extracted generic stage-function body | Fixture starts proposal with zero ticked tasks and no logged work. Stage function returns proposed for 0/3, in_progress for 1/3, and proposed for unavailable totals with no activity. Test reloads actual source and locates moved card under in_progress with unchanged palette | Normal-mode task change is a real supported stage transition, not a direct DOM manipulation. Packaged no-Node mode is a concrete remaining test regression: ANALYSIS-061. |
| Prior unaffected analysis | Carry round 001 original mapping/architecture and round 002 neutral correction/harness controls through explicit target mapping above | Shared exact-ID helper, eight palette keys, all three card-kind bindings, deduplication, neutral body, decorative dots, status colors, ranking, preferences, API, and polling unchanged. Neutral/tinted footer values unchanged | Prior source conclusions remain applicable. Neutral ratios previously calculated at 9.471005 dark / 5.351738 light, tinted minimum 4.734769; calculations not repeated this round. No integration/MCP parity change. |
| Supplied current runtime acceptance | Root reports Release build with zero warnings/errors, 250 passing units, and coordinated eight-test browser run against this target | Fresh isolated run with `qa-004` prefix: all eight tests passed, exit 0, 42.8 seconds. Includes identity, actual task-derived movement, every palette/shared/missing contrast, neutral backgrounds, 390-pixel bounds, later filtering/reordering/order restoration/themes | Supplied runtime evidence, not this reviewer's execution. It verifies normal Release acceptance and resolves the previously observed narrow failure for that run. It does not cover the existing packaged no-Node mode. |
| Duplication / architecture / performance regression | Inspect correction scope | Responsive rules target two known control groups; fixture change remains test-only; no product fetch, persistence, timer, or process logic added | No new architecture/performance finding or suggestion. ANALYSIS-061 is a concrete supported-harness-mode regression, not a styling preference. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Existing product polling; revised browser fixture | Source observation | Runtime unchanged: eligible visible dashboard poll every two seconds, about 30 catalog requests/minute even when unchanged; revision/snapshot gates control additional refreshes. CSS adds no HTTP/process/file/journal work. Test performs one fixture task read, one modified write, and one restoration write instead of appending journal activity; this is explicit test work, not an idle loop. |
| Cache lifetime versus interval and refresh duration | Palette and existing revision gates | Source observation carried from earlier rounds | No new cache, lifetime, interval, overlap behavior, or HTTP cache setting. No runtime-duration measurement performed. Prior pure hashing/no cache and catalog no-store analysis unchanged. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Palette, board state, test fixture | Source observation | Eight fixed palette keys and per-card temporary lists unchanged; existing 200-rank and preference-byte bounds retained. Synthetic palette DOM removed before narrow checks. Revised movement stores one bounded fixture text string and restores the file, avoiding permanent journal growth from that test. Screenshot artifacts remain explicitly prefix-selected test output. |
| Repeated full reads where a change check would do | Existing client refresh; fixture task mutation | Source observation | No new product full reads. One small synthetic task file is read once for exact restoration; no loop or repeated full-tree read added. Server-side costs remain outside scope and unmeasured. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Existing isolated server; revised movement trigger | Source observation | Color test no longer spawns the smoke logger. Existing harness still resolves dotnet via PATH and fixed repository build output, with unchanged startup/shutdown limits. Existing no-Node mode strips Node/OpenSpec tool directories, which explains the mode-specific task-total dependency in ANALYSIS-061. No browser, harness, or external tool process launched by this reviewer. |

## Findings

### ANALYSIS-061: Reversible movement test cannot advance the card in supported no-Node acceptance

- Severity: warning
- Related task / requirement: task 3.2 relevant existing browser checks; Stable repository color cues / reload and column movement regression coverage.
- Locations: at current reviewed identity, `dashboard/e2e/dashboard.spec.ts:1282`, `:1370`, `:1376`; `dashboard/e2e/server.mjs:51`; `dashboard/src/board.ts:75`; `tools/WorkLog.Smoke/DashboardFixture.cs:89`.
- Observation: movement now unconditionally edits task ticks, then expects Board proposal in the in-progress column. The existing packaged acceptance mode sets `WORKLOG_DASHBOARD_NO_NODE=true`, removes Node/OpenSpec paths, and explicitly expects task progress unavailable; stages then rely on recorded facts. Board proposal starts with no journal activity. In this mode the task edit cannot supply completed-task totals: `changeStage(null, null, null)` remains proposed, so the moved-card expectation cannot succeed. Root's eight passing normal Release checks have task tooling available and therefore do not exercise this supported mode. The orchestrator additionally confirms `.github/workflows/validate.yml:43` enables this mode for packaged E2E; that workflow was not independently read by this reviewer. This is a source-demonstrated expected failure, not a claimed packaged-browser execution.
- Proposed improvement and benefit: preserve a mode-compatible, isolated real stage-transition check for packaged no-Node acceptance, while keeping exact fixture restoration and the existing baseline counts. A mode-aware test path or dedicated isolated movement fixture can cover the intended contract without leaving journal mutations for later tests.
- Behavior-preservation risks: retain the actual column transition and stable palette assertion, avoid making task tooling mandatory for packaged product acceptance, and preserve later four-card settled expectations. Do not weaken the product fallback when task totals are unavailable.
- Evidence type: source observation plus executed evaluation of the actual pure stage-function body. Results: 0/3/no activity → proposed; 1/3/no activity → in_progress; unavailable totals/no activity → proposed. Packaged browser mode not executed by this reviewer.
- Verification / measurement plan: run the focused color/movement check with a packaged executable and `WORKLOG_DASHBOARD_NO_NODE=true` under the existing isolated harness, then run the subsequent filtering/order tests in the same fresh serial fixture. Confirm a genuine transition, unchanged identity color, fixture restoration, and preserved settled counts.
- Relationship: new correction-induced harness regression; not a duplicate of QA-001 or ANALYSIS-001. Other reserved IDs unused.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | `files=9 diff=f238b66a2d97 untracked=2/c03c9bfee23b` | Unchanged board-scoped neutral correction carried from round 002; current source retains all contrast checks; root supplies passing rendered contrast | Prior source verification remains applicable; supplied runtime now passes. Orchestrator owns closure. |
| QA-001 | Same current target | Independent responsive-CSS and retained-bounds inspection; root supplies all eight current-target normal browser checks passing, including 390-pixel bounds and later tests | Correction verified in source; supplied normal-mode rendered evidence passes. No independent browser run here; orchestrator owns closure. |
| ANALYSIS-061 | Same current target | No correction assigned; exact source stage evaluation and existing no-Node harness behavior above | New warning requires triage. Normal-mode fixture isolation is verified; no-Node movement remains uncovered/failing by source analysis. |

## Assessment and limitations

Responsive layout and normal-mode fixture restoration are bounded and supported by current-target passing browser results. Earlier original and neutral-footer analysis carries forward without a product/API/persistence regression. ANALYSIS-061 prevents an unqualified claim that all existing supported acceptance modes are preserved; the normal Release run passes and the packaged no-Node mode was not run.

Only this report was written. No source/tasks/ledger changes, browser/harness runs, or live installation access. Identity matched before and after. Current runtime results are explicitly attributed to the orchestrator; this review independently executed source invariants and pure stage logic only. The orchestrator owns finding triage, any further authorization, and feature-completion status.
