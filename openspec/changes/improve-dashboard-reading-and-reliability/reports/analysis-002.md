# Analysis report: improve-dashboard-reading-and-reliability / 002

## Review target

- Reviewer / role: delegated independent reviewer / code-analyzer.
- Client / version / mode / model (resolved ID): Codex / version unavailable to this subagent / delegated Default mode / `gpt-6.1-sol`, high reasoning, assigned role default.
- Date: 2026-11-05, final identity captured at 18:44 UTC.
- Change root / assignment: `openspec/changes/improve-dashboard-reading-and-reliability` / ANALYSIS-REVIEW-002.
- Task and requirement references: [proposal](../proposal.md), [design](../design.md), [tasks](../tasks.md), especially C05 -> C03 -> C02 -> C01 -> C04 and tasks 1.1-5.4, 6.1-6.4. Acceptance references: [openspec-browser](../specs/openspec-browser/spec.md), [work-dashboard](../specs/work-dashboard/spec.md), [day-digests](../specs/day-digests/spec.md).
- Scope and relevant neighboring paths: remaining frontend, comparison/parser helper, browser harness, synthetic dashboard fixture and final four documentation deltas. Backend coverage from [analysis-001](analysis-001.md) is carried only after confirming its exact identity is unchanged.
- Reading limits: report-only; no implementation, planning, ledger, configuration or prior-report edits; no subagents. Outside implementation scope, read the assigned role/facts/template, change planning/specs, prior analysis-001, the ignored release roadmap, raw benchmark JSON and final acceptance logs. Small neighboring reads included the existing reader state bounds, `ReadBaselineStore`, Playwright configuration and dashboard process-lifetime references. No user data, installation, personal client configuration, authenticated agents or user dashboard port was accessed.
- Baseline and HEAD: `ac6d7f075a4a52e5eeef5896d034ae3c35a504fc` throughout.
- Prior reports / rechecked findings: unchanged backend analysis-001 is carried; its deferred ANALYSIS-001 suggestion was explicitly excluded from rechecking or closure. ANALYSIS-020..039 were reserved for new findings; only ANALYSIS-020 is used here.

### Implementation identity

Stage A command, executed before and after source analysis:

```powershell
./scripts/review-identity.ps1 -Baseline ac6d7f0 -Paths dashboard scripts src tests tools
```

Both outputs matched:

```text
HEAD: ac6d7f075a4a52e5eeef5896d034ae3c35a504fc
Baseline: ac6d7f075a4a52e5eeef5896d034ae3c35a504fc
Tracked diff SHA-256: 00fcf593bdd88cfb431fda000aceabf3b36eb80ead822f5ddbddcd7b71e858ae
Summary: head=ac6d7f0 baseline=ac6d7f0 files=28 diff=00fcf593bdd8 untracked=3/16c9bd9c4903
```

Stage B command, executed before and after the final documentation review:

```powershell
./scripts/review-identity.ps1 -Baseline ac6d7f0 -Paths README.md dashboard docs scripts src tests tools
```

Both outputs matched the orchestrator's frozen target:

```text
HEAD: ac6d7f075a4a52e5eeef5896d034ae3c35a504fc
Baseline: ac6d7f075a4a52e5eeef5896d034ae3c35a504fc
Tracked diff SHA-256: 11c3f4732c553341eea72cdb885e9b4d17c2357c3a68e89e15d910c40a112b46
Summary: head=ac6d7f0 baseline=ac6d7f0 files=32 diff=11c3f4732c55 untracked=3/16c9bd9c4903
```

Final manifest; Stage A contained the same list except the four documentation files:

```text
? dashboard/src/document-navigation.spec.ts
? dashboard/src/document-navigation.ts
? scripts/benchmark-review-comparison.mjs
M README.md
M dashboard/e2e/dashboard.spec.ts
M dashboard/e2e/server.mjs
M dashboard/src/app.board.spec.ts
M dashboard/src/app.daily.spec.ts
M dashboard/src/app.html
M dashboard/src/app.reliability.spec.ts
M dashboard/src/app.reviews.spec.ts
M dashboard/src/app.spec.ts
M dashboard/src/app.ts
M dashboard/src/diff.timeout.spec.ts
M dashboard/src/diff.ts
M dashboard/src/markdown.spec.ts
M dashboard/src/markdown.ts
M dashboard/src/models.ts
M dashboard/src/styles.css
M docs/architecture.md
M docs/dashboard-validation.md
M docs/mcp-validation.md
M src/WorkLog/Dashboard/DailyService.cs
M src/WorkLog/Dashboard/SourceCatalog.cs
M src/WorkLog/Journaling/DigestService.cs
M src/WorkLog/Mcp/McpServerHost.cs
M src/WorkLog/Persistence/DigestStore.cs
M tests/WorkLog.Tests/Dashboard/DailyDigestResponseTests.cs
M tests/WorkLog.Tests/Dashboard/SourceCatalogTests.cs
M tests/WorkLog.Tests/Mcp/DigestContractTests.cs
M tests/WorkLog.Tests/Mcp/DigestTests.cs
M tools/WorkLog.Smoke/DashboardFixture.cs
```

All before/after identities contained these same untracked hashes:

| File | SHA-256 |
| --- | --- |
| `dashboard/src/document-navigation.spec.ts` | `9ab6a6cee89b7368fcc851640353c375a09ee2b14214ac76d81111497366121b` |
| `dashboard/src/document-navigation.ts` | `cdf933b478bf3e7d54621b0a65b0acede883ce4e39ea5db4d4a71b43de1c771f` |
| `scripts/benchmark-review-comparison.mjs` | `481a15e4ef04c0d1e6dfd4756b93cecb28378721a352574089aa08a5c56e169b` |

Backend carry command:

```powershell
./scripts/review-identity.ps1 -Baseline ac6d7f0 -Paths src/WorkLog/Dashboard src/WorkLog/Journaling src/WorkLog/Mcp src/WorkLog/Persistence tests/WorkLog.Tests/Dashboard tests/WorkLog.Tests/Mcp
```

Before and after remained the nine-file manifest in analysis-001, tracked SHA-256 `f736d5e40c9feb140b6be6e356a3d259f1732fe61cc755bc4f4837e19f9afcc7`, summary `head=ac6d7f0 baseline=ac6d7f0 files=9 diff=f736d5e40c9f untracked=0/e3b0c44298fc`. Therefore Stage A source and prior backend coverage can be carried into this whole-target report. No unexpected mutation was observed. Review artifacts and checklist/ledger updates are outside these identities.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Duplication and responsibility boundaries | Inspected all remaining source/test/harness deltas, grouping helper and parser sharing against `docs/architecture.md` | `document-navigation.ts` groups existing identities; renderer and token-only extraction share one block rule and decoder; optional status remains an additive private model | Passed. Small desktop/mobile template repetition has a shared computed group source and native controls; no separate discovery, client-specific policy or new persistence layer was introduced. No abstraction-only recommendation. |
| Reader state and complexity | Inspected `app.ts`, template/CSS, component and browser deltas | Filter owner is source/variant-qualified; current path remains visible when filtered out; drafts, positions and epochs reuse existing behavior; focus restoration respects a connected control the user focused during a pending request | Passed source analysis. Browser tests exercise 10/30/50 documents, duplicate basenames, two themes, three widths, independent rail scroll and keyboard/draft/position preservation. Functional execution is independent QA/orchestrator evidence, not an analyzer rerun. |
| Chooser lifecycle | Inspected copy tracking, `openCard`, `openChange`, route and close paths plus board tests | Copy tracking uses existing source-qualified identity; explicit target is set for route choices and cleared when closing/replacing the chooser | Passed. Existing route/default-document and persisted key contracts remain intact. |
| Startup and error handling | Inspected single-flight startup, token handling, timer creation and failure tests | Protected `/api/info` confirms an existing session after bootstrap 401; network-failed token is retained only in memory for Retry; handled fragment is removed; only one poll timer is started | Passed source analysis for session/startup behavior. Read marking separates 409 and authorization loss and retains successful fingerprints. The generic failure branch still supplies a connection-restored hint for every other cause; QA's canonical QA-020 warning covers that issue and remains for orchestrator correction/verification. |
| Optional Daily status | Inspected model/template/component delta and carried backend analysis | Absent/null issue has no UI notice; one unavailable notice preserves observation summaries and clears after recovery | Passed. No storage repair, data migration or MCP schema change. |
| Comparison fallback | Inspected `diff.ts`, reader template/CSS, timeout/component/browser assertions | 50 ms algorithm timeout plus edit-distance 2000; abort result has no invented passage coordinates; entire rendered/source element becomes one changed stop with a minimum visible height | Passed. Empty-current-document fallback is focusable without inserting synthetic text. Precise normal ranges remain supported. |
| Baseline extraction and retained state | Inspected `markdown.ts`, `reviews.ts` use through App and focused tests | Baseline extraction calls the shared token rule without rendering/sanitizing; fence exclusions, marker identity and decoding are retained; removed-review memo stores one current revision pair | Passed. Current-document rendering still uses sanitization. No growing revision-pair history was added. Token extraction/rendering latency was not independently benchmarked. |
| Fixture isolation | Inspected new Daily server helper, writer scenarios and fixture changes | Each mutating Daily case obtains separate synthetic data/server metadata and closes in `finally`; cleanup waits for process exit and checks port release before deleting fixture data; unresolved member is config-only, real-root precedence has a separate control | Passed isolation structure, with ANALYSIS-020 below for a bounded startup-health improvement. No shared journal/WAL reset or store exclusion was introduced. |
| Performance measurement | Independently parsed both raw JSON files, recomputed nearest-rank p95, compared metadata and current file hashes | All eight workloads have 30 raw samples and three warmups; same harness, dependencies, environment and input sizes; each recomputed after p95 matches recorded value | Passed recorded comparison-only target; see table below. This reviewer did not rerun timing during packaged acceptance. |
| Syntax and whitespace | `node --check scripts/benchmark-review-comparison.mjs`; `node --check dashboard/e2e/server.mjs`; `git diff --check ac6d7f0 -- README.md dashboard docs scripts src tests tools` | All returned success; Git emitted only existing line-ending advisories | Passed. No build, restore, package, unit or browser suite was launched by this analyzer. |
| Final documentation and evidence labels | Inspected all four final documentation deltas and summary lines in ignored `final-normal.log`, `final-packaged-browser.log`, `final-packaged-smoke.log` | Logs show 44 passed/3.8m, 44 passed/2.6m and `BOOTSTRAP PASSED`; final docs label only packaged `--updates-only` smoke, shared-server fixtures, comparison-only timing and source version 0.4.1 | Passed factual/safety review. Docs no longer assert a separate default smoke run. Reported 268 dashboard/427 .NET results are supplied independent-check evidence, not new analyzer executions. The later QA-020 disposition must be reconciled by the orchestrator; the docs refer to current reports/ledger neutrally. |
| Hygiene | Scanned added lines of final four docs for current account/machine/home values, personal locations, email and secret patterns; inspected synthetic fixture content | Zero added-document matches; no private values quoted here | Passed within this scope. Own-report hygiene is checked after the single reserved write. Existing historical reports were preserved. |

### Independently checked timing evidence

Raw inputs: ignored `artifacts/dashboard-reading-reliability/c04/baseline-before.json` and `after-bounded.json`. Both are complete, use Node 24.13.0 on Windows x64, `diff` 9.0.0 and TypeScript 5.9.3, fixed 128-byte synthetic lines, the same harness hash above and three warmups plus 30 measured samples per row. After helper hash `9102bb3ebf64aa33084784bf50fe2b671a1ef4690a8652319a7225a2964ab92a` matches the current file; baseline helper hash is `d4b657ceef14bc87ef90217d8e95d02402ada17ec3c8999b6f7664a766dcc585`.

| Size / workload | Before p95 ms | After p50 ms | After p95 ms | Incomplete before / after |
| --- | ---: | ---: | ---: | --- |
| 50 KiB small edit | 0.335 | 0.145 | 0.406 | 0/30 / 0/30 |
| 50 KiB rewrite | 15.870 | 15.167 | 16.786 | 0/30 / 0/30 |
| 200 KiB small edit | 1.395 | 0.756 | 1.221 | 0/30 / 0/30 |
| 200 KiB rewrite | 485.185 | 50.968 | 51.553 | 0/30 / 30/30 |
| 400 KiB small edit | 1.831 | 1.469 | 2.407 | 0/30 / 0/30 |
| 400 KiB rewrite | 1554.855 | 50.981 | 51.062 | 0/30 / 30/30 |
| 800 KiB small edit | 3.282 | 1.791 | 3.153 | 0/30 / 0/30 |
| 800 KiB rewrite | 3005.100 | 51.018 | 51.973 | 30/30 / 30/30 |

The maximum after p95 is 51.973 ms, within the <=100 ms target for `readMarks`: normalization, line tokenization/comparison and result processing. The 200/400 KiB rewrites now trade exact detail for the visible document-level fallback; 800 KiB was already incomplete. Small-edit rows remain precise. These measurements exclude Markdown rendering, sanitization, removed-comment token extraction, browser latency and arbitrary devices. No broader speedup is inferred from them.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | App poll, SourceCatalog, digest diagnostics | Source observation / calculated rates; no new idle trace | Existing visible/nonbusy poll interval remains two seconds: nominally up to 30 catalog requests/minute plus 30 document checks/minute with an open reader. Polls skip hidden pages, in-flight polls, document loads and saves. Revision changes trigger the existing overview/search/activity/data refreshes. No-op known-revision document responses avoid parsing. Backend catalog discovery, command/journal reads and existing costs are carried from analysis-001. Diagnostic throttling adds no timer or idle work; new navigator state adds none. Inputs do not necessarily change each interval. This is explicitly deferred to C06, not an achieved idle-cost reduction. |
| Cache lifetime versus interval and refresh duration | Catalog cache, command cache, document/marks memos | Source observation | Backend catalog snapshot lives two seconds after completion, so a nominal two-second poll does not guarantee a hit; existing successful command cache is 60 seconds and command timeout 30 seconds. Existing document known-revision path avoids rerendering unchanged text. Marks and new removed-comment memo each retain one revision pair, invalidated by changed revision/baseline. No new polling cache or latency claim. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | App local state, preferences, baseline storage, diagnostic suppression | Source observation | New filter is one string, mobile-open state one boolean, pending startup one promise/token and removed-comment memo one pair; no accumulating failure notices or revision history. Carried diagnostic map has three fixed categories. Existing preferences retain 100 positions and 200 document selections with a 60 KiB client budget; baseline retention is 300 documents/64 MiB. Existing in-memory draft map can grow with distinct unsaved document drafts during a page lifetime and clears entries on applicable save/cancel; this change preserves it. Existing journal/derived-store growth is unchanged. No new unbounded cache was identified. |
| Repeated full reads where a change check would do | Document rendering, baseline extraction, catalog/journal/digest reads | Source observation; comparison measurements only | Unchanged document responses avoid parsing; changed current content still receives a full parse/render/sanitize. Removed-comment extraction parses baseline tokens once for the current revision pair and avoids the old extra full HTML/sanitization pass. Catalog/journal/digest full-read/query costs carried from analysis-001 remain candidates for C06/C14/R4 measurement. No measured rendering or SQL improvement is claimed. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | SourceCatalog, browser fixture, benchmark harness | Source observation | Product discovery/resolution is unchanged from analysis-001: existing Git/OpenSpec paths, cache and command bounds remain. Fixture uses fixed Release smoke DLL, normal dotnet or explicit packaged executable; packaged runtime filters Node/OpenSpec from PATH and uses isolated bundle extraction. Daily child shutdown has a five-second wait and release check; its readiness request needs the bounded-deadline improvement in ANALYSIS-020. Existing synchronous smoke helper invocations rely on test ownership rather than introducing product discovery. Benchmark is opt-in, transpiles the actual repository helper with installed pinned dependencies and writes only an explicitly validated ignored output; it adds no product runtime work. |

## Findings

### ANALYSIS-020: Bound each isolated-server health request during startup

- Severity: suggestion.
- Related task / requirement: C05, tasks 1.1-1.2; isolated owned server lifecycle and reliable cleanup.
- Locations: `dashboard/e2e/dashboard.spec.ts:104-111`, especially the awaited health fetch at line 109, at reviewed source identity `00fcf593bdd8` carried into final `11c3f4732c55`.
- Observation: the new helper limits readiness to 150 attempts and sleeps 100 ms between them, but each attempt awaits a health fetch with no explicit request/deadline bound. A connection that accepts and never completes can hold that await, so the attempt counter does not itself establish the intended approximately 15-second startup limit. The stop/retain-data cleanup is reached only once control leaves this await. Successful acceptance runs did not exhibit a hang.
- Proposed improvement and benefit: give health probing a short per-request or shared overall startup deadline and ensure deadline expiry reaches the existing owned-child cleanup path. This keeps failed fixture startup bounded and makes the cleanup guarantee easier to verify without restructuring product code.
- Behavior-preservation risks: allow sufficient normal startup time for packaged extraction; cancel only the probe/helper's owned process and preserve fixture data when shutdown cannot be confirmed.
- Evidence type: source observation; no reproduced hang or measured flaky run.
- Verification / measurement plan: in a disposable helper-level probe, inject a health response that never completes and verify the deadline leads to confirmed owned-child exit/port release or the existing explicit retained-fixture failure. Retain a normal packaged-start success case. Do not run against the user's dashboard or installation.
- Relationship: new test-harness suggestion; distinct from deferred ANALYSIS-001's stderr delivery assertion concern.

No additional new production finding is raised. The orchestrator notified this reviewer of canonical QA-020 during report completion: `markChangeRead` gives every non-409/non-401 failure a connection-restored hint, although a missing document or baseline storage failure may require another action. Source inspection is consistent with that report; use QA-020 as the canonical ID and obtain the assigned independent correction recheck. This report does not close or supersede it.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | No correction assigned | Backend SHA-256 still exactly `f736d5e40c9feb140b6be6e356a3d259f1732fe61cc755bc4f4837e19f9afcc7`; prior report carried | Not performed by explicit assignment; remains deferred. No closure claim. |
| QA-020 | No correction in this frozen target | Existing generic failure hint inspected in `app.ts`; parent identified the canonical warning | Not a correction recheck; pending orchestrator fix/independent verification. |
| ORCH-010 documentation concern | Final four-doc delta within `11c3f4732c55` | Final dashboard/MCP validation says only packaged `--updates-only`; raw smoke summary is `BOOTSTRAP PASSED` | Factual text observed consistent with available evidence. Orchestrator owns ledger disposition. |

## Assessment and limitations

The remaining frontend/harness implementation and final documentation were analyzed at matching before/after identities, with unchanged backend coverage explicitly carried. Shared MCP behavior remains on the existing common host; fixtures labeled Claude Code/Codex are accurately described as protocol evidence, not native-client or live-agent evaluations. No per-client policy duplication or architecture drift was found.

ANALYSIS-020 is a bounded test-harness improvement based on source inspection. QA-020 remains a separate canonical warning requiring the orchestrator's planned correction/recheck. ANALYSIS-001 stays deferred and was not rechecked. No ledger finding is closed by this report.

This reviewer ran identity, syntax, whitespace, hygiene and raw-stat consistency checks only. Full unit, build, package, browser and smoke results are independent QA/orchestrator evidence; no redundant CPU-heavy run was started during packaged acceptance. No install, source-version change, archive, release, commit, branch, push, tag, settings change or native-agent action was performed. The only intended write is this reserved report. No unexpected source mutation was observed. The orchestrator owns severity/deferral decisions, final correction verification and checklist reconciliation.
