# Analysis report: realtime-dashboard-updates / 021

## Review target

- Reviewer / role: ANALYSIS-RECHECK-021 / code-analyzer.
- Client / version / mode / model: Codex / version unavailable / Default delegated review / assigned role model `gpt-6.1-sol`, high reasoning; runtime introspection unavailable.
- Date: 2026-11-09T15:50:33Z.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-021.
- Task and requirement references: [tasks](../tasks.md) 4.1, 4.3, 5.4 and 7.4; [reader delta](../specs/openspec-browser/spec.md), unchanged-text archive metadata and reader-context preservation.
- Canonical finding: QA-400; supplied ORCH-007 is its duplicate. The assignment supplied the observed browser failure: rename succeeded but the badge stayed active and retained `canComment` stayed true; server mutation guard still rejected writes. No current peer report was read.
- Substantive correction scope: `dashboard/src/app.ts` and `dashboard/src/app.reliability.spec.ts`, specifically selected-document metadata decisions and their regressions. Relevant neighboring reads: revision queue/tracker, `DocumentService.ReadAsync`/resolution, surrounding document/selection/focus code and prior own [analysis 009](analysis-009.md).
- Reading limits: no current `qa-020` report, current QA evidence, findings ledger, handoff or orchestrator workflow read. Role/project facts/template were already read.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `pwsh -NoProfile -File scripts/review-identity.ps1 -Baseline 0d77685 -Paths dashboard/src/app.ts dashboard/src/app.reliability.spec.ts dashboard/src/live-updates.ts dashboard/src/live-connection.ts src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard dashboard/e2e/dashboard.spec.ts`.
- Tracked manifest: modified `dashboard/e2e/dashboard.spec.ts`, `dashboard/src/app.reliability.spec.ts`, `dashboard/src/app.ts`, `src/WorkLog/Dashboard/DashboardHost.cs`, `DocumentService.cs`, `SourceCatalog.cs`, and `tests/WorkLog.Tests/Dashboard/SourceCatalogTests.cs`.
- Tracked diff SHA-256: `1ff82d0623d82a4ad6cb4e4aa95a027d8b6883fd15f86c361fd4b91134fb5c17`.

| Untracked scoped file | SHA-256 |
| --- | --- |
| `dashboard/src/live-connection.ts` | `78db24467ab95c0221d16c986c57ee9e59ff27a35a7140cfd032228b6783e5ac` |
| `dashboard/src/live-updates.ts` | `1430c9e42ae7790af48e9c48af8eefbd6decf65faf94f085b0f7c5319ee16d54` |
| `src/WorkLog/Dashboard/DashboardChangeSource.cs` | `b4e0857797f114c56a4e199c235894b707a8436bf83a66ce436784a49e30e7a3` |
| `src/WorkLog/Dashboard/DashboardLiveConnections.cs` | `89317ca104c88bbf54df33d3467982b7d38f303b44a02be2c0ff0ce2a564185e` |
| `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs` | `db42cf7bfd22b7da0fdb3c97a811cd832f3b71605ad3b9ccf86e0b40bab8bfd6` |
| `tests/WorkLog.Tests/Dashboard/DashboardChangeSourceTests.cs` | `3cb5aac067f550ae3a8dc413f196ade260c656f63ce60430bc0c2e54583cf9ec` |
| `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs` | `011d2a738dd6cdc35c63d98f84899d4f57fc4481f1d85c56108313b11a423d9a` |
| `tests/WorkLog.Tests/Dashboard/ManualTimeProvider.cs` | `80f730379a27edfd7cc8d19d6d15123b6a0f52dac9746e89859440bf3b6b5c45` |

- Before/after identity: both `head=0d77685 baseline=0d77685 files=15 diff=1ff82d0623d8 untracked=8/2590af010779`; exact match.
- Reserved ANALYSIS-440 through ANALYSIS-459 remain unused. This recheck adds no suggestions.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Archive metadata decision | Inspect `refreshSelectedDocument` at `app.ts:808-824`, execute actual method extracted with TypeScript AST and transpiled locally | Documents-only active-to-archive and archive-to-active transitions both request full response; Catalog advancement also retains its full-response behavior | Passed independently at source/method level; no Angular or actual HTTP execution |
| Ordinary unchanged validation | Actual-method controlled stable-metadata case; inspect companion regression around `app.reliability.spec.ts:992` | Stable Documents-only invalidation keeps `skipUnchanged=true`; existing `loadDocument` supplies `knownRevision` for the same logical document | Passed independently at decision level and source inspection; avoids blanket full retransmission on ordinary document invalidations |
| Source-qualified copies and routes | Actual method with same-name changes in another source/copy and with a directory-name route | Matching remains source-qualified and variant-qualified when supplied. Unrelated archived candidates do not force metadata reads for the selected copy. Directory-name match detects its archive transition; request retains selected source/change/path/variant | Passed independently with controlled candidates |
| Missing and ambiguous candidates | Actual method with zero/two matching candidates; inspect authoritative server read order | Both still issue the selected document request rather than declaring it current solely from catalog matching. Server `ReadAsync` resolves source/change/containment and reads before considering the hash-only unchanged shortcut | Passed at source/method level; actual missing/unavailable recovery behavior is not independently run |
| Navigation / stale completion | Actual method with selection epoch advanced during candidate publication; inspect surrounding reconciliation/load guards | Selection change prevents the old read. Existing surrounding selection checks, follow-up scheduling and `documentEpoch` response guard remain. No route/draft identity or epoch logic changed | Passed for assigned source regression; controlled selection mutation is not native router evidence |
| Render, permission and retained context | Inspect full-response `loadDocument` branches | Full metadata replaces `DocumentContent`; changed `canComment` causes render update rather than the equal-permission fast path. Same-document background load preserves draft and baseline ownership, using existing focus/position handling | Passed by source inspection; browser focus/caret/selection and actual read-only UI are not established by this reviewer |
| Regression fixture validity | Inspect archive and stable-metadata fixtures around lines974-1005 | Archive fixture advances Documents only, leaves text/revision equal, asserts absence of `knownRevision` and archived/read-only state. Companion case asserts `knownRevision` retained with active/commentable metadata | Passed by source inspection; full Angular suite not executed here because developer holds runtime slot |
| Domain semantics / architecture / bounds | Compare correction to revision tracker, queue and server document behavior | No server revision semantics or endpoint contract changed. New logic is a candidate-local metadata comparison after the existing catalog refresh and before the existing authoritative document read | Passed; no additional polling, subscription, durable history or separate refresh owner |
| Proportional checks | `node artifacts/realtime-dashboard-updates/reviews/analysis-021/recheck.cjs`; scoped `git diff --check`; before/after identity | Ten controlled actual-method scenarios pass. Whitespace exit0; Git emits only its line-ending conversion advisory. Identity exact match | Passed independently; transpilation executes the production method body, with controlled dependencies |
| Own-report hygiene | Category-only scan after writing | No credential, personal/machine-path or unintended fixture-residue category match | Passed; whole-change hygiene remains coordinated QA work |
| Native / full-runtime acceptance | No host, build, full unit suite, browser, package, MCP or performance execution in this assignment | Developer owns shared binary/host slot; original QA400 behavior was supplied assignment evidence | Not performed; current browser success and feature completion are not inferred from source checks |

Independent no-host evidence: ignored `artifacts/realtime-dashboard-updates/reviews/analysis-021/recheck.cjs` and `results.json`. The probe extracts the actual `refreshSelectedDocument` body from the reviewed source with the installed TypeScript parser and transpiles it; it does not duplicate the metadata decision. Controlled `loadDocument` records decision arguments and identity, rather than performing Angular/HTTP/rendering.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Selected-document reconciliation | Source observation; passed | No new interval or idle request. Existing catalog/document event reconciliation gains a boolean archive-state comparison; no journal reads or CLI starts are added |
| Cache lifetime versus interval and refresh duration | Known-revision document validation | Source observation and controlled method result; passed | Ordinary stable Documents-only invalidations retain conditional reads. Full response is requested when Catalog changed or matched archive state differs. Once authoritative metadata is applied, subsequent stable updates regain the fast path; no new time-based cache |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Local candidates/matching value | Source observation; passed | Existing catalog filter result is reused; new locals last one reconciliation. No persistent collection, queue or history is added; existing one-in-flight/coalesced-follow-up ownership remains |
| Repeated full reads where a change check would do | Document metadata transitions | Source observation and controlled method result; passed | Full response is limited to existing Catalog cases and archive-state transitions, which hash-only responses cannot represent. Stable metadata remains conditional. Actual bytes, hashing and render cost unmeasured |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Browser correction | Source observation; not applicable | No executable discovery, process launch or timeout changed. Local TypeScript is used only by this ignored review probe |

## Findings

**No findings** within QA-400 and adjacent regression recheck. No new IDs allocated and no suggestions raised.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| QA-400 (ORCH-007 duplicate) | Fifteen-file frozen identity above; `app.ts:808-824`, reliability regressions around974-1005 | Actual production method requests full metadata on Documents-only archive transitions and retains conditional reads when stable; source confirms full response updates permissions and preserves existing selection/request guards | Verified at source/method level. Real browser badge, comment controls, retained reader context and exact archive scenario remain for independent runtime verification |

## Assessment and limitations

The correction addresses the observed hash-only metadata gap without changing domain semantics or forcing full text on every Documents invalidation. Matching the selected document's archive state against its refreshed catalog candidate establishes when a full authoritative response is needed; ordinary stable metadata stays conditional. Independent source/method checks cover both transition directions, Catalog advancement, source/copy scope, directory routes, missing/ambiguous matching, closed reader and selection supersession.

Only this report and ignored review evidence were written; target identity remained unchanged. No implementation, tests, documentation, ledger, tasks, prior report, current QA evidence, baseline package or shared binary changed. Builds, native tests, actual Angular rendering, browser focus/caret/navigation and the exact archive browser scenario were not run by this reviewer. Prior startup-cleanup/backend evidence is unaffected by these browser files, but is not newly approved or rerun here. The dashboard remains client-neutral and MCP contracts are unchanged. The orchestrator owns triage, final runtime evidence and completion.
