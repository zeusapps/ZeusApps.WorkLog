# Dashboard validation

Recorded on 2026-10-26 on Windows 10.0.26300.0, using .NET SDK 10.0.113, Node 24.13.0, and headless Microsoft Edge. All journals, review comments, worktrees, and unresolved-store cases used isolated synthetic fixtures under ignored artifacts directories.

## Build and service checks

- Locked .NET restore and clean npm install passed.
- Release build passed with no warnings or errors.
- Full .NET suite: 219 passed, none skipped. Review-service coverage includes marker-only edit/delete, unique-ID matching after earlier edits, UTF-8/UTF-16 BOM and CRLF retention, stale/missing/ambiguous markers, legacy upgrades, code-example isolation, source containment, and bounded Unicode-safe reference excerpts.
- Angular unit tests: 9 passed, covering source-line mapping, Markdown security, structured and legacy review markers, read-only controls, retained edit drafts/conflicts, selection defaults, repository scope, timezone boundaries, and a delayed catalog poll overlapping a document switch. The switching regression failed before the fix and passed afterward.
- Negative build probes confirmed that stale production assets and a missing index fail the build. The original generated files were restored afterward.
- The packaged dashboard help exposes repository, data-directory, port, and no-open options.
- Packaged bootstrap checks passed for self-contained launch/version, MCP startup and EOF, and offline update status.
- Distinct 0.0.1/0.0.2 packaged fixtures passed launcher argument/stream/lifetime checks and concurrent activation, retry, cursor, settings, rollback, recovery, and retained-payload checks.
- Release v0.3.0 also passed those mixed-version checks against the independently downloaded public v0.2.0 baseline (ZIP SHA-256 `63e14a543fa487adaebfa7b3d70d00ef9000297f2d501666c6e01b102ade0562`). Its original MCP PID/stream survived activation and rollback, and new processes adopted v0.3.0. Evidence is under artifacts/update-smoke/1ef6270018ff496f84d4e42ef24e7444/evidence.json.

## Browser acceptance

All eleven packaged browser scenarios passed, including the user acceptance follow-ups for dark-by-default appearance, remembered light mode, the embedded favicon, delayed/out-of-order document switches through nested specs, referenced review notes, edit/cancel in rendered and source views, confirmed deletion, legacy marker upgrades, and conflicts when a note changes or disappears. Both reader themes were checked through computed styles and screenshots. Archived notes lose their mutation controls even when their text revision is unchanged.

The packaged executable was exercised with Node.js and the OpenSpec CLI absent from its runtime PATH. Installed-.NET lookup was pointed at a nonexistent directory; the self-contained package continued to serve its interface and local documents. Missing CLI task totals were shown as unavailable, not zero. Tests covered design-first reading, repository-scoped search, no automatic remote resources, retained review text and stale-block rejection, timeline and observation continuation, archive-safe links, restart authorization, repeat no-open launches, concurrent MCP logging while saving a review, section/task links, missing targets, keyboard navigation, and an unresolved store without a local fallback.

Browser screenshots, traces on failure, and the HTML report are generated in dashboard/test-results and dashboard/playwright-report. Fixture data remains in artifacts/dashboard-e2e for inspection; these directories are ignored by Git.

## Realtime dashboard updates (`realtime-dashboard-updates`)

### Current implementation and safe commands

The live path is documented in [architecture](architecture.md#dashboard-live-updates). The realtime harness creates disposable repositories, data directories and browser profiles below ignored `artifacts/realtime-dashboard-updates/`; it stops the WorkLog process it starts and writes raw JSON manifests there. It never uses the user's data directory or dashboard port. Run from the repository root after obtaining the coordinated build/host slot:

```powershell
npm test --prefix dashboard
dotnet restore --locked-mode
dotnet build -c Release --no-restore
npm run test:e2e --prefix dashboard
node scripts/c06-host-lifecycle.spec.mjs
node scripts/c06-acceptance-invariants.mjs
node scripts/c06-fault-matrix.mjs --startupFailureSmoke --phase startup-cleanup
node scripts/c06-fault-matrix.mjs --smoke --phase five-category-smoke --deadlineMinutes 10
node scripts/c06-fault-matrix.mjs --phase final --deadlineMinutes 90
node scripts/c06-mcp-compat.mjs
node scripts/c06-dashboard-acceptance.mjs --run --phase after-latency --samples 30 --warmups 3 --idleMinutes 5 --exe src/WorkLog/bin/Release/net10.0/worklog.exe
```

The previous command measures the final uninstrumented Release executable's browser latency. Capture method-boundary counters and GC allocation in a separate copy-only instrumented build; this does not instrument the shipped executable:

```powershell
.\scripts\c06-instrument-snapshot.ps1 -Name after-metrics
dotnet restore artifacts/realtime-dashboard-updates/baseline/instrumented/after-metrics/src/WorkLog/WorkLog.csproj --locked-mode
dotnet build artifacts/realtime-dashboard-updates/baseline/instrumented/after-metrics/src/WorkLog/WorkLog.csproj -c Release --no-restore -p:C06BuildDashboard=false
node scripts/c06-dashboard-acceptance.mjs --run --phase after-metrics --samples 30 --warmups 3 --idleMinutes 5 --exe artifacts/realtime-dashboard-updates/baseline/instrumented/after-metrics/src/WorkLog/bin/Release/net10.0/worklog.dll --metrics
```

The instrumented run is the source for method-boundary/GC counters and their measurement-endpoint overhead. Use its latency fields only as instrumented-run observations; the uninstrumented run above is the acceptance latency result. Both runs keep fixture size, writes, three warmups, 30 measured samples and the two five-minute windows unchanged. The preserved raw baseline remains authoritative for before values; do not overwrite or recapture it.

For each after-run idle window, the harness records 250 ms Live/transport observations, observed status changes and transport close/failure events, user input, and local-minute boundaries. A paused/missing status, missing live transport, transport close/failure, or user interaction makes the window ineligible and fails the run after preserving its raw record. Local clock boundaries are recorded separately so a window crossing a minute or date boundary is visible in the evidence. The historical baseline has no live transport; its eligibility is not applicable, not a zero or a pass. A pure no-host invariant probe covers fault targeting, checkpoint-frame delivery correlation, simultaneous marker and Live predicates, monotonic elapsed-time calculation, and idle eligibility (including empty status observations):

```powershell
node scripts/c06-acceptance-invariants.mjs
```

Its bounded output is `C06 no-host acceptance invariants passed.`

The owned-host lifecycle unit check uses fresh fake child processes to cover graceful exit after stop acknowledgement and forced cleanup after an unresponsive stop. It remains a no-host state-machine check. The actual startup-failure smoke is reported separately below.

Create the current 0.4.1 package in a fresh ignored output directory so existing packages and preserved baseline inputs remain unchanged:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -OutputDirectory artifacts/realtime-dashboard-updates/<run>/package
$env:WORKLOG_DASHBOARD_EXECUTABLE = (Resolve-Path 'artifacts/realtime-dashboard-updates/<run>/package/worklog-0.4.1-win-x64/worklog.exe').Path
$env:WORKLOG_DASHBOARD_NO_NODE = 'true'
npm run test:e2e --prefix dashboard
dotnet run --project tools/WorkLog.Smoke -c Release --no-build -- --updates-only $env:WORKLOG_DASHBOARD_EXECUTABLE
Remove-Item Env:WORKLOG_DASHBOARD_EXECUTABLE, Env:WORKLOG_DASHBOARD_NO_NODE
```

The `--startupFailureSmoke` mode deliberately fails the fixture startup readiness after the owned test host is live, then requires proof of wrapper exit, host disposal, an empty connection registry, and fixture-port release. It is a fixture lifecycle probe, not product acceptance. The five-category smoke runs five representative cycles and is diagnostic only; it does not replace all 100 required cycles. The full fault matrix has a bounded 90-minute deadline and runs the exact 25/25/20/15/15 allocation; its 25 Revision-wire cycles comprise 9 lost-generation windows, 8 duplicated targeted Revision frames, and 8 reordered targeted Revision frames. It uses the Release `WorkLog.Smoke` helper. The MCP compatibility command compares the preserved baseline and current Release executable against equivalent Claude Code and Codex stdio protocol fixtures; it does not launch authenticated native clients. Both performance commands retain three warmups, at least 30 samples per freshness workload, and two separate five-minute idle windows. The uninstrumented executable run reports browser request/frame counts and process CPU; the copy-only metrics run reports method-boundary counters, GC and measurement endpoint overhead. Do not interpret method-boundary counters as physical I/O.

The final browser fault matrix has two independent contexts and three tabs/connections: A-reader (retained navigation and draft), A-target (visible authoritative target domain), and B-target (independent visible authoritative target domain). A cycle passes only when both target tabs show the latest changed domain and each target tab observes the expected marker and visible Live status together; the reader tab separately checks its changed document where applicable and verifies draft, focus/caret/selection, source/rendered mode, provenance and navigation context. It records per-page transport re-establishment to visible reconciliation and write acknowledgement to visible state. A companion tab is not evidence that the reader tab's document reconciled.

### Baseline and harness provenance

The preserved instrumented baseline is `artifacts/realtime-dashboard-updates/instrumented-baseline/2026-11-06T17-39-16-472Z-603eea45.json`, captured from source head `0d7768581a0305725454dc33d610924cce9d61ef`. It used harness SHA-256 `5b9d47b2bb93c9924998e9873b97059ecebe955a225bbef58d090e1204f46936`, original instrumenter SHA-256 `f5f0e810b43f786e5f21260e1b9861b808febfc4374a1ce8c3c8afc917ffe10b`, instrumented WorkLog DLL SHA-256 `cc5df0eaff5c7564dde0bdb44bceb35b78a89e2102223d2e7aeee819ba4defb1`, and Smoke DLL SHA-256 `3b3564f65299cd0b4b6f88b336d5c0fda0b01af4de147eb279d84ab70ffd80f8`. Baseline freshness latency exceeded the one-second p95 target:

| Workload | p50 | p95 | Standard deviation | Coefficient of variation |
| --- | ---: | ---: | ---: | ---: |
| Document commit to visible | 1,999 ms | 3,062 ms | 492.27 ms | 0.2471 |
| Journal write to History ticket visible | 3,930 ms | 4,962 ms | 674.02 ms | 0.1830 |
| Digest save to Daily visible | 3,431 ms | 4,449 ms | 864.64 ms | 0.2601 |

Each freshness workload used three warmups and 30 measured samples. In the five-minute no-reader baseline, 147 periodic catalog reads accompanied 148 ordinary browser API requests; server boundaries recorded 8,580 metadata enumerations, 17,160 entries, 4,446 metadata probes, 234 SQLite commands, 12 Git starts and 16 OpenSpec starts. No document content reads or hashes, journal-entry decodes or digest rows were recorded. Process CPU was 4,171.875 ms and GC allocation 39,056,360 bytes. With a reader open, the five-minute window recorded 147 catalog reads and 147 document reads among 296 ordinary API requests; boundaries recorded 9,130 enumerations, 18,260 entries, 13,339 metadata probes, 249 SQLite commands, 15 Git starts and 20 OpenSpec starts. It recorded no content reads/hashes, journal decodes or digest rows; CPU was 1,875 ms and GC allocation 47,665,808 bytes. These values describe the captured process and instrumented method boundaries, not disk operations.

The preserved baseline used acceptance-parser SHA-256 `5b9d47b2bb93c9924998e9873b97059ecebe955a225bbef58d090e1204f46936`. The current acceptance-parser SHA-256 is `7014a436eceaae4e233d04f0aacfaee54d6faa69ed441c0783d16d0706a1ee82`; the fault-matrix SHA-256 is `d05bf9fb62e9841ccf596753f254e2f42ffdc5b89ae59de82b87977a3b618729`; the no-host invariant probe SHA-256 is `1d5c983dc1ad019a9d382ff3568c85f136986a895236593a6397ba335b7cd915`; the current lifecycle helper/spec hashes are `4b27ee19a227c22e4302d51f0b7294c3d7fe2145ac0c33f5d74254a1df341009` and `c9841c840ca76eb8b43bdb352f2000958a7cf66714b9b990048bb3cedfef56e5`; and the MCP fixture/process-outcome hashes are `dd007a7e664720cd8fcb2f32e0e6717f5c3471ed055b284b81e1e56dc7bc2484` and `491dd832f80ab574dd56c7511ef2337f6dd0702c2abba11bd8c83d4221c87dca`. The parser's only server-counter semantic adjustment remains explicit normalization of the known absent `afterOnlyMetadataProbes` field in the preserved pre-C06 baseline. Missing common counters remain errors. It reports common metadata probes, the after-only detector/watcher probes and their after-run sum separately. This does not change fixture, workload, sampling or baseline raw JSON. The current instrumenter SHA-256 is `17c8f2cca9a2e60bc7a885b7dbee211d2f88df48f5849f73ed2d10e14f4282f3`. Its detector/watcher call sites have a separate `afterOnlyMetadataProbes` counter because those call sites did not exist in the preserved baseline; baseline absence means “component absent,” not an unmeasured zero. Full raw failures and manifests remain preserved under the ignored artifact tree.

The 25 revision-wire cycles use a bounded lost-generation window: one advanced target-domain Revision is dropped, subsequent relevant ordinary vectors are suppressed, and the window closes only on a newer tagged checkpoint Revision carrying the dropped generation. The trace retains each observed, dropped and suppressed frame. The optional `checkpoint` sequence is emitted only on actual periodic checkpoint Revision publications; ordinary revisions and Snapshot results omit it. A same-generation ordinary frame or a server checkpoint counter is not proof that the browser received recovery. The two target tabs require their expected marker and visible Live status at the same accepted reconciliation observation. Empty status-surface mutation records invalidate after-run idle eligibility.

### Acceptance status

The paragraphs up to "Final-target results and limitations" record earlier diagnostics and are kept as history; the final-target results are in that subsection.

The preserved baseline remains the comparison reference; its executable and raw before measurements are unchanged. QA independently passed the current full .NET suite at 454/454 with no skips, including the corrected native archive-move coordinator case. The current Release build passed with zero warnings and errors. The dashboard unit suite passed 307/307. The corrected watcher permits the Windows archive move, and the browser reader now refreshes archive metadata on a Documents-only revision while preserving the unchanged-text fast path when archive metadata is stable. The exact normal browser scenario `keeps links working after archiving and honors repeat no-open launches` passed 1/1 after that correction. QA 013's status-surface evidence remains: all six light/dark and 390/768/1440 combinations passed, light refresh text contrast was at least 12.16:1, and keyboard Tab/Enter Retry returned to one Live connection. These focused results do not establish the complete browser suite.

The corrected real `--startupFailureSmoke` probe passed in `artifacts/realtime-dashboard-updates/fault-matrix/2026-11-09T15-44-51-148Z-34310e02.json`: the injected post-readiness startup failure was cleaned up, the wrapper exited, the host reported source disposal and zero active connections, and the owned fixture port was released on the first probe. The corrected five-category diagnostic did not complete cycle 1. In `artifacts/realtime-dashboard-updates/fault-matrix/2026-11-09T15-54-22-394Z-a8b069ba.json`, the first disconnect injection timed out waiting up to five seconds for a target tab to show Reconnecting or Updates paused; zero completed cycle records were produced, and owned host/port cleanup passed. The finalized read-only [analysis 022](../openspec/changes/realtime-dashboard-updates/reports/analysis-022.md) records that all target Reconnecting/Live observations precede `faultStart`; the runner sets the active cycle before reloading target tabs, and no post-offline A-side close, status transition, reopen, or outage-committed write is recorded. The client has no offline listener, so this trace does not establish that either intended A transport disconnected. Do not count a cycle or call this a product recovery failure. Earlier pre-correction raw attempts remain historical at `2026-11-09T08-39-17-798Z-8fc7f8a8.json` and `2026-11-09T08-43-15-836Z-7b351eac.json`.

The corrected MCP runner passed all four current/preserved × Claude Code/Codex protocol fixtures in `artifacts/realtime-dashboard-updates/mcp-compat/2026-11-09T15-51-32-139Z-57010dca.json`. All 12 owned wrappers exited successfully without signals or timeouts, both dashboard MCP and digest checks passed in every case, and neither executable changed during the run. The current apphost SHA-256 is `1d1316d4af203c7a20feba4e3abebcd9ed1ccc6a8d4e08b051d2bd26c6b47fb9`, paired with managed `worklog.dll` SHA-256 `37dc9e451ec42b94a0a58ef89679566fcc41ebd5ff489852a3f1ca2d57445f84` and `WorkLog.Smoke.dll` SHA-256 `f9650189267bdbd8dfb6d9226d1ed91b4d0db432847536d49e99417531c8b749`. The preserved package hash remains `cf11ef5faae749ca76259ee05745ed8f4e8aa42590bd3a0f4f8c951e5d5e7f46`; the corrected MCP runner hash is `dd007a7e664720cd8fcb2f32e0e6717f5c3471ed055b284b81e1e56dc7bc2484`. These are protocol fixtures, not authenticated Claude Code or Codex sessions.

### Final-target results and limitations

Scope was reduced on 2026-11-11: freshness percentiles, instrumented server-work capture and the 100-cycle matrix moved to the follow-up change `realtime-dashboard-performance-evidence`. The preserved baseline executable, harness and instrumentation approach remain available to it. Reduced REST request counts are not a claim of lower server CPU.

Checks on the final target (Release build, locked restore): .NET suite 461 passed, 0 skipped; dashboard unit suite 329 passed in 22 files; normal browser suite 48 passed on three consecutive final-code runs; the bounded five-category fault smoke passed in five separate runs (25 cycles in total, one per category per run), each with zero incorrect final states, lost observations, discarded drafts, accumulated notices or leaked processes. In the first run the owned host stopped gracefully and the connection registry went from 3 to 0. MCP compatibility passed for current and preserved executables with Claude Code and Codex protocol fixtures (fixtures, not native sessions).

Smoke recovery latencies (five runs, one cycle per category each; raw JSON under `artifacts/realtime-dashboard-updates/fault-matrix/`). Disconnect: transport re-established to visible reconciliation 56-99 ms. All categories: wait from the target tab starting to visible 42-3,171 ms (server restart 1.4-3.2 s, the slowest); write commit to visible in both browsers 0.97-5.0 s, including the outage the fixture holds open (watcher gap/overflow reached 5.0 s once; others at most 4.4 s). Commit-to-visible times include the deliberate outage and are not recovery latency; transport re-establishment time is recorded only for the disconnect category.

Packaged evidence (fresh self-contained 0.4.1 package built by `scripts/package.ps1` into a new ignored directory): file list identical to the preserved pre-C06 package (README, INSTALL, `current.json`, `launcher.json`, `release.json`, `worklog.exe`, versioned `worklog.exe` and `worklog.pdb`; no new output files), top-level launcher byte-identical to the baseline, versioned executable 151,552 bytes larger (embedded client). The embedded assets contain the SignalR client and its MIT notice in `3rdpartylicenses.txt`. Packaged no-Node/offline browser acceptance: 48 passed on two runs; a first run failed ORCH-018 (below) and was rerun. Packaged bootstrap smoke (`--updates-only`) passed: launcher/version, MCP protocol and EOF, offline status.

Idle result (task 7.1) is met. Two earlier attempts of the `--run` harness were aborted after the first window because the harness's own reader-close click produced a revision and a sub-200 ms Reconnecting state. The harness now waits, before each window, until the page is Live and quiet (no ordinary API request, connection event or status change for 2 s, bounded at 30 s) and records the settle start, end and duration; the window keeps its strict eligibility rules, and `--idleOnly` skips the later latency samples. On the same Release build, both five-minute windows were eligible: no reader (300.0 s) and open reader (300.0 s), each with a settle of about 2.4 s, zero ordinary API requests (so zero catalog, document, search and timeline reads), zero status changes away from Live, zero connection events and zero user actions. Each window carried one established WebSocket and about 335 frames (roughly 295 classified by heuristic as checkpoint or keep-alive and about 40 opaque), with no REST traffic; no periodic reads or repeated queries occurred, so no checkpoint, keep-alive or time-boundary work needed separating out. Server CPU and boundary counters were not captured (instrumented endpoint not enabled).

Known limitations:

- ORCH-018 (`dashboard.spec.ts` "keeps links working after archiving and honors repeat no-open launches", near line 679): "document and review reads must finish before the page is reconciled Live" timed out in 3 of 4 runs in one earlier session, 0 of 10 in later pre-fix runs, and once in the first packaged run above. Residual flake, cause unconfirmed. ANALYSIS-702 (a superseded live load left the page falsely paused) is the plausible cause and fix; the packaged failure above preceded that fix, and there have been 0 ORCH-018 failures in the browser runs since (`:679` 5 of 5, final-code full e2e 48/48 x3). No packaged browser run has been repeated since the fix.
- ORCH-019a (near line 1155): fixed by ANALYSIS-701; no longer a known limitation.
- ORCH-019b: a rare .NET test flake that was not captured.
- Deferred: freshness p95 against the one-second target, server-work measurement and the 100-cycle matrix (`realtime-dashboard-performance-evidence`). Baseline freshness exceeded the target (p95 3.1-5.0 s), and current after-latency is unmeasured.

Safe reproduction: the idle command above, `node scripts/c06-fault-matrix.mjs --smoke --phase five-category-smoke --deadlineMinutes 10`, and the packaged commands above. All use disposable fixtures, owned ports and ignored output.

## Package evidence

| Measure | Existing local v0.2.0 build | Dashboard v0.3.0 build |
| --- | ---: | ---: |
| Payload worklog.exe | 80,916,590 bytes | 109,535,203 bytes |
| Windows ZIP | 68,897,154 bytes | 81,676,017 bytes |

The release-validation package is artifacts/release-v0.3.0-local/worklog-0.3.0-win-x64.zip. Its payload is 28,618,613 bytes larger than the existing local build; the ZIP is 12,778,863 bytes larger. Frontend production assets total approximately 474 kB before transfer compression. Runtime assets, the favicon, and third-party license notices are embedded in the payload, preserving the package file list. Journal schema remains 2 and launcher/shared-data contracts remain 1.

To launch the verified local build from the repository root:

```powershell
.\artifacts\release-v0.3.0-local\worklog-0.3.0-win-x64\worklog.exe dashboard
```

Stop the previous dashboard command before launching the updated package; a repeat launch otherwise reuses its existing process. Dark mode is the default, and the top-bar toggle remembers a light/dark selection.

Review documents after the agent finishes editing. Revision/block checks detect stale selections, but concurrent external edits between the last reread and replacement remain outside the supported workflow. Task links follow current numbering and do not preserve historical identity through renumbering.

## Reliability fixes (fix-dashboard-reliability)

Recorded on 2026-10-27 on Windows 10.0.26300.0, using .NET SDK 10.0.113, Node 24.13.0, OpenSpec CLI 1.13.1, and headless Microsoft Edge. All data used isolated synthetic fixtures under ignored artifacts directories.

### Validation results

- Angular unit tests: 31 passed in 4 files. New coverage: relative and custom ranges evaluated on different dates, legacy ranges, no dates written on History loads, invalid timezone and inverted range rejection (including a zone the browser accepts but the server rejects with 400, which is explained inline, not remembered, and leaves the previous zone and results in place, and a saved zone the server rejects being ignored at start, while a start-up probe that fails for another reason, or a 400 with another message, keeps the saved zone and lets start-up continue), transient day focus, day boundaries in zones that skip midnight (Africa/Cairo 2026-04-24, Asia/Beirut 2026-03-29) and zones that do not, 100/200-entry preference caps, a single preferences notice for repeated save failures, idle polls (no searches, timeline queries, document transfers, or preference saves), refreshes on revision, journal snapshot and day rollover, and the unchanged-document answer keeping the rendered document.
- Full .NET suite (Release): 232 passed, none skipped. New coverage: CLI results reused until their inputs change (per-source task progress, config reload, 60 s bound), catalog revision on document edits, journal snapshot, cached search reads and oldest-first eviction, search notices limited to the selected repositories, headings inside fenced code, 413 for oversize preferences, 404 for an unknown link source, and the `knownRevision` request and `unchanged` response, a timed-out CLI result reused for a 30 s backoff (one spawn inside the window, one after it, and a changed input stamp bypassing it), a junction or symlink at a nested directory below a change being rejected as an escape, a link whose target has a linked parent component being resolved fully and rejected (the Windows junction form ran here; the Unix symlink form of the same scenario has not been run), a link loop failing closed, and a link inside the source being followed once with unchanged document order and stamps.
- Package built with `scripts/package.ps1` (version 0.3.0, fresh output directory).
- Browser acceptance against the packaged build (`WORKLOG_DASHBOARD_EXECUTABLE` set to the packaged `worklog.exe`, `WORKLOG_DASHBOARD_NO_NODE=true`): 16 of 16 scenarios passed, including the new Escape, day-focus reload, rolling range with the browser clock advanced three days, bounded reading positions, and scoped search scenarios.
- Browser acceptance against the Release build with the OpenSpec CLI on the PATH: 15 of 16 scenarios passed. The scenario "keeps history available when a declared store cannot be resolved" fails with OpenSpec CLI 1.13.1 because the declared store resolves, so the local change it expects to be hidden is listed. It is unchanged by this work and tracked separately as QA-007. It passes in the packaged run above, where the CLI is absent from the PATH.

### Idle cost before and after

These are fixture measurements, not user data. Setup: the three-repository dashboard fixture (a repository with a linked worktree, a second repository, and a repository whose declared store cannot be resolved), a Release dashboard on a free port, Edge with one open document (an approximately 870-byte design document) and the search term "timeline" across all repositories. After 15 s of settling, one 60 s window was recorded. OpenSpec invocations were counted by a logging shim placed first on the PATH, git invocations with `GIT_TRACE2_EVENT`, and API requests, document response bytes and preference writes in the browser. "Before" is the build at baseline commit `bf65aeb`; "after" is this change.

| Per minute, idle | Before | After |
| --- | ---: | ---: |
| OpenSpec CLI spawns | 49 | 8 |
| git spawns | 24 | 4 |
| API requests | 42 | 60 |
| `/api/document` response bytes | 10,440 | 2,880 |
| Preference writes (`PUT /api/state`) | 2 | 0 |
| Search requests | 12 | 0 |
| Timeline requests | 4 | 0 |

The request count rises because polling is no longer held back by slow discovery: before, each catalog request waited several seconds for CLI discovery (12 polls per minute), and after it returns in milliseconds (30 polls per minute). The additional requests are catalog and document polls; the unchanged document answer is 96 bytes instead of 870. The 8 OpenSpec and 4 git spawns after the change are the single refresh round at the 60 s bound.

### Cheap-tier cost of a catalog refresh

These are fixture measurements from an in-process harness (Release build, fake CLI command, fake clock, no dashboard and no port; synthetic fixture whose paths have about 16 components, deeper than a typical checkout). Each refresh bypasses the 2 s response cache but stays inside the 60 s CLI lifetime, so it measures only the cheap tier: change discovery, Markdown enumeration and the length and write-time stamps. Median of 40 refreshes after a warm-up. "Before" canonicalized the source root and every path for every file; "after" canonicalizes the root once per discovery and takes attributes, length and write time from the directory entry, fully canonicalizing only reparse points.

| Fixture | Before (median, p90) | After (median, p90) |
| --- | ---: | ---: |
| 60 Markdown files (5 active and 10 archived changes) | 22 ms, 24 ms | 1.0 ms, 1.6 ms |
| 1,280 Markdown files (20 active and 300 archived changes) | 727 ms, 835 ms | 17.5 ms, 19.3 ms |

The catalog revision and a hash over every change's document order, content stamp and task stamp were identical before and after for both fixtures. A timed-out result is now remembered for 30 s (never longer than the 60 s lifetime): a timed-out `openspec context` is still reported as the source error, while a timed-out git or `openspec list` result leaves worktrees or task progress out for the backoff window. So a hanging CLI is spawned at most twice a minute per checkout instead of on every refresh.

## Review inbox (add-review-inbox)

Recorded on 2026-10-27 on Windows 10.0.26300.0, using .NET SDK 10.0.113, Node 24.13.0, OpenSpec CLI 1.13.1 (for the Release-build run), and headless Microsoft Edge. All documents, comments and baselines used isolated synthetic fixtures under ignored artifacts directories.

### Dependency

- `diff` (jsdiff) 9.0.0, pinned exactly in `dashboard/package.json` and `package-lock.json`. It was published on 2026-04-13, more than two weeks before use. The API used is `diffLines(old, new, { timeout })`, checked against the library's current documentation; a timed-out comparison returns `undefined` and is reported as incomplete.

### Validation results

- Locked restore, clean `npm ci --prefix dashboard` and Release build passed with no warnings.
- Full .NET suite (Release): 262 passed, none skipped. New coverage: marker listing by active, archived and all scope, repository filtering, fenced-code exclusion, truncation without splitting surrogate pairs, unreadable documents and sources reported as unavailable (scoped to the requested repositories), no reparse and no path resolution on a warm listing (the marker cache is keyed by catalog identity and the catalog fingerprint, and a miss resolves each change directory once), a document deleted or replaced by an unreadable one between listings not served from the cache, a marker-cache bound of 512 entries with least-recently-used eviction in which a second listing over 520 documents reparses only the 8 over the bound (a parse count, over three rounds), listing misses leaving the reader's document cache alone, zero reparses over 50 repeated listings, catalog document fingerprints with unchanged revision semantics, the baseline store (exact text, deduplication, 300-key and 64 MiB bounds, least-recently-read eviction across many marks over many days, unreferenced-blob deletion, corrupt and oversized indexes reset, an index of another schema version preserved untouched and reported unavailable, an index held open with `FileShare.None` during `Mark`, `Get` and `Entries` writing and deleting nothing, a blob held open during `Get` keeping its entry, tampered entries and damaged blobs), and the HTTP endpoints (401 for every new endpoint before authorization with nothing written, 409 for a stale revision with the baseline kept, 400 for escaping paths and for malformed bodies (`{}`, a null or empty `document`, a missing revision) on `reads/get`, `reads/mark` and `reviews`, 404 for unknown changes, baseline surviving an archive move, BOM and UTF-16 documents).
- Angular unit tests: 81 passed in 8 files. New coverage: grouping and counts per change and document, scope filtering, diff-to-block mapping with known diffs (blocks by `data-start`, removed passages by position, escaped removed text, stops), source-view segments, first open, Mark as read, Mark change read, own comment save after reading (no highlight) and after an unread agent edit (still highlighted), removed-since-last-read by id and by exact legacy marker text, updated-since-read chips and clearing on open, `[` and `]` handling (ignored while typing), and idle polling making no overview requests while the catalog revision is unchanged. Removing the baseline advance after own writes made the two own-write tests fail.
- Browser acceptance against the packaged build (`scripts/package.ps1`, `WORKLOG_DASHBOARD_EXECUTABLE` set to the packaged `worklog.exe`, `WORKLOG_DASHBOARD_NO_NODE=true`): 19 of 19 scenarios passed, including the three new ones (comments across two changes opened at their note and following an agent removing a marker; an agent's edit of a read design highlighted with the addressed comment listed, keyboard navigation, source view, Mark as read, baseline kept across reload and own comment not highlighted; an archived change's leftover marker listed read-only).
- Browser acceptance against the Release build with the OpenSpec CLI on the PATH: the new scenarios pass; the full serial run stops at the existing scenario "keeps history available when a declared store cannot be resolved" (tracked as QA-007, unchanged), so the three later scenarios were run on their own and passed.
- Two defects were found by these browser runs and fixed: the card hint did not clear after Mark as read because 100-ns write times exceed what a JavaScript number holds exactly (fingerprints now travel as text), and a heading whose slug starts with `review-` was mistaken for a comment anchor (comment links now use `note:<line>`, which no heading slug can produce).

### Idle cost with the overview visible

This record replaces an earlier one that reported 0 OpenSpec and 0 git spawns. That window (60 s) missed the 60 s CLI refresh round, and its method was invalid: git was filtered on `child_start` events, which a directly invoked git never emits, and the logging shim was an `openspec.cmd` that the dashboard does not run (`LocalCommand` runs `node` with `node_modules/@fission-ai/openspec/bin/openspec.js` found on the PATH).

Method: the three-repository fixture, the packaged build (`scripts/package.ps1`, `worklog.exe`), a free port with `--no-open`, a disposable data directory, headless Edge on the Ongoing page with the review overview and change cards visible, 15 s of settling, then one 135 s window that spans two refresh rounds. OpenSpec spawns were counted by a logging `openspec.js` placed in a `node_modules/@fission-ai/openspec/bin` directory first on the PATH, which then runs the real CLI. git spawns were counted from `GIT_TRACE2_EVENT` `start` events (one per git process). Requests were counted in the browser.

| 135 s window, idle, overview visible | Count | Per 60 s round |
| --- | ---: | ---: |
| OpenSpec CLI spawns | 16 | 8 |
| git spawns | 8 | 4 |
| API requests | 64 (all `GET /api/catalog`) | |
| `/api/reviews`, `/api/reads/*`, `/api/search`, `/api/timeline`, `/api/document`, preference writes | 0 | |

The per-round cost (8 OpenSpec and 4 git spawns) equals the reliability record, so the overview adds no idle cost. The overview and the read hints are requested at start and again only when the catalog revision changes.

### Warm review listing

The listing `POST /api/reviews` with `scope: "all"` is refreshed on every catalog revision change. It answers an unchanged document from a marker cache keyed by the document's catalog identity (source, variant, change, path) and the catalog's `length:lastWriteTicks` fingerprint, so a warm listing resolves no path and stats no file. On a miss each change directory is resolved once and its documents are resolved below it. The cache is bounded by the catalog, not by a count: after each listing, the entries of documents that are no longer in the current catalog snapshot are removed, and nothing is removed by age or number. A listing over one scope or repository filter therefore keeps the entries of the other documents warm. A listing is at most one catalog refresh (2 s) behind a file, which is the freshness the client already has.

In-process measurements of `DocumentService.ReviewsAsync` (Release, fixture of 900 synthetic documents with one marker each, 10 changes active and 80 archived, 10 documents per change, `scope: "all"`, 12 warm calls after one cold call). They exclude HTTP and JSON, so they are lower than the earlier HTTP probe of the same shape.

| Documents in scope (`all`) | Before the marker cache | Cold call | Warm call |
| --- | ---: | ---: | ---: |
| 900 | 1,648 ms median (1,469 to 2,107 ms), every document reparsed on every call | 666 ms, 900 parses | 0.53 ms median (0.36 to 10.4 ms), zero parses |

The target of under about 50 ms is met at 900 documents, above the former 512 bound.

## Archive view and Open in editor (add-archive-browser-and-editor-open)

### Editor URL handler formats (task 1.4)

Confirmed on 2026-10-28.

| Preset | Format | Status | Source |
| --- | --- | --- | --- |
| VS Code | `vscode://file/{full path to file}:line:column`, for example `vscode://file/c:/myProject/package.json:5:10` | Confirmed by vendor documentation | VS Code docs, "Opening VS Code with URLs" in `docs/configure/command-line.md` (https://github.com/microsoft/vscode-docs), fetched with `ctx7` from `/microsoft/vscode-docs` |
| Cursor | `cursor://file/{full path to file}:line:column` | Not confirmed by vendor documentation. The Cursor documentation retrieved with `ctx7` (`/websites/cursor`) documents only `cursor://anysphere.cursor-deeplink/...` links (MCP and prompt installs), not file links. Community reports (Cursor forum, "Open in Cursor URL Handler" and "Does Cursor have a unique open scheme?") describe `cursor://file/{path}:{line}` working for local files, as expected of a VS Code fork. | Community reports only |

Decision: the Cursor preset uses the VS Code form because Cursor is a VS Code fork and community reports show it working. Until a live observation on Windows confirms it (see the live observation below), treat the Cursor preset as unverified; "System default app" is the fallback and a launch failure is reported with a suggestion to choose another preset. `line` is zero-based in the API and one-based in the link (`line+1`), the column is always 1, forward slashes are used, and every path segment is percent-encoded as UTF-8 except a leading drive designator such as `C:`.

### Validation results

Run on Windows against the working tree of this change, fixture runs and packaged runs labeled separately.

| Check | Result |
| --- | --- |
| `npm ci --prefix dashboard --no-audit --no-fund` | passed |
| `npm test --prefix dashboard` | 94 of 94 tests in 10 files passed (new: `archive.spec.ts`, `app.archive.spec.ts`; includes the source-view line count and the caret-based reading position) |
| `dotnet restore --locked-mode` | passed |
| `dotnet build -c Release --no-restore` | 0 warnings, 0 errors |
| `dotnet test -c Release --no-build --no-restore` | 350 of 351 passed (11 new in `ArchiveAndOpenTests`, including a `.md` link to a `.cmd` file being rejected for all three presets and a `.md` link to a `.md` file still opening). The one failure, `DevInstallTests.PreflightRefusesStaleUnmarkedAndCorruptPackagesAndChangesNothing`, expects the message "Source file" but receives "Source directory dashboard\src changed after ...": it compares the working tree's modification times with a fixture package, so the result depends on directory-versus-file modification-time ordering in the tree under test. It passed in one QA run, failed in another, and fails on a clean `c53e647` worktree, so it is unrelated to this change (including the open endpoint) and was not fixed here. |
| Browser acceptance, fixture run against the Release build (`npm run test:e2e --prefix dashboard`, OpenSpec CLI on the PATH) | the serial run stops at the existing scenario "keeps history available when a declared store cannot be resolved" (QA-007, pre-existing, unchanged): 15 passed, 1 failed, 6 not run. With that scenario excluded (`--grep-invert "declared store"`): 21 of 21 passed, including the three new scenarios. |
| Browser acceptance, packaged run (`scripts/package.ps1` output, `WORKLOG_DASHBOARD_EXECUTABLE`, `WORKLOG_DASHBOARD_NO_NODE=true`) | 22 of 22 passed on the current tree (rerun in the fix round from a fresh `scripts/package.ps1` build), including the Archive view, the Open in editor, the source-view reading-position and the declared-store scenarios |

The new scenarios: the Archive view lists three archives newest first with the undated one last, filters case-insensitively, shows "No activity recorded" and "Recorded done" with a date separate from the archive date, shows the leftover comment count, and opens an entry read-only; and Open in editor posts the expected document and line for a heading, the reader position, a search result and an archived document, with the preset selecting the scheme and being saved in the preferences, and a forged request (path traversal, unknown preset) being rejected without a launch. A third scenario scrolls the source view of a long wrapped paragraph and asserts the recorded line is the first visible source line (3), not the scroll distance divided by the row height. These scenarios use the test-only recording launcher (`WORKLOG_DASHBOARD_TEST_LAUNCH_LOG` naming a file; the dashboard appends targets to it instead of launching). `dashboard/e2e/server.mjs` sets it; the packaged build ignores it unless set.

Server tests cover: archive dates from directory prefixes (dated, undated, impossible dates); latest status with ties on the occurrence time broken by id; repository filtering; missing journal returns empty and creates nothing; an unsupported schema is refused without migration; URI building (drive colon kept, spaces and non-ASCII encoded); open for active and archived documents, unknown presets and negative lines (400), traversal, absolute and encoded paths (400), a directory junction escaping the source (change unavailable), unauthorized (401), cross-origin, cross-site and wrong-host requests (403) with no launch recorded in any rejected case, ignored extra client fields, and an unavailable editor (503 with "Could not open Cursor. Choose another editor.").

### Live observation (task 3.2)

The implementation run did not launch a real editor. The following is a live observation by the maintainer, not an automated result.

On 2026-10-28 the maintainer installed dev build `0.3.9002 dev c53e647 dirty` (built from the reviewed working tree) on their Windows installation through `scripts/install-dev.ps1` Switch, restarted their dashboard, and observed that "Open in editor" from the reader header and the outline heading pencil action both opened VS Code at the expected position.

Not observed in the live check: the source view, the Cursor preset, and System default app. The Cursor link format remains unconfirmed by vendor documentation and was not live-checked; treat the Cursor preset as unverified.

## Ticket and repository links (add-ticket-and-repository-links)

### Validation results

Run on Windows against the working tree of this change, fixture runs against the Release build. QA also ran the packaged-build e2e (`./scripts/package.ps1`, `WORKLOG_DASHBOARD_EXECUTABLE`, `WORKLOG_DASHBOARD_NO_NODE=true`) with `--grep-invert "declared store cannot"`: 25 of 25 passed (the excluded scenario fails identically on baseline `9243722`, verified by QA). The developer did not rerun the packaged build.

| Check | Result |
| --- | --- |
| `npm test --prefix dashboard` | 122 of 122 tests in 14 files passed (new: `links.spec.ts`, `quiet.spec.ts`, `workitem-markdown.spec.ts`, `app.links.spec.ts`) |
| `dotnet build -c Release --no-restore` | 0 warnings, 0 errors |
| `dotnet test -c Release --no-build --no-restore` | 353 of 353 passed (new: `TicketFilterTests` parity with `SqliteEntryStore.ReadPage`, `TimelineTicketFilterTests`) |
| Browser acceptance, fixture run against the Release build (`npm run test:e2e --prefix dashboard`, OpenSpec CLI on the PATH) | with the existing scenario "keeps history available when a declared store cannot be resolved" excluded (`--grep-invert "declared store cannot"`): 25 of 25 passed, including the four new scenarios. The serial run without the exclusion stops at that scenario, which fails identically on the unchanged baseline `9243722` (the store-member's local change card is shown; QA-007, pre-existing, unchanged by this change), so the scenarios after it do not run. |
| Browser acceptance, packaged run (QA, `--grep-invert "declared store cannot"`) | 25 of 25 passed |

The fixture gained a `links-demo` repository (identity `github.com/example/links-demo`, with changes `quiet-change` and `never-logged`), a local-only identity (`local:links-demo-local`), and Jira (two sites), Azure DevOps, recorded-URL and unrecognized-scope tickets. The existing History scenario now expects 68 rows after "Load more work items" (was 61).

### What the checks cover

- Link rules (`links.spec.ts`): recorded URL precedence; Jira host scope (case, whitespace, non-host scopes); Azure DevOps organization, organization/project and `visualstudio.com` scopes, non-numeric IDs and unrecognized scopes; GitHub, GitLab, Bitbucket and Azure DevOps HTTPS and SSH identities; `local:` and `path:` identities; percent-encoding of branches with `/`, spaces and `#`.
- Rendering (`app.links.spec.ts`): an unlinkable ticket renders without an anchor and with a copy action; a link click goes only through the explicit external-navigation handler (`window.open` with `noopener,noreferrer`); repository and branch links appear on a recognized host and not for a `local:` identity; clipboard fallback.
- Ticket filter: `TicketFilterTests` runs nine fixture entries through `SqliteEntryStore.ReadPage` and `TicketFilter.Matches` for twelve filters (bare, qualified, padded, unresolved, a different site, a colon-containing ID, partial qualified forms) and asserts identical entry IDs; `TimelineTicketFilterTests` asserts a filtered row equals the unfiltered row (span, counts, clipped start) and that the observations drill-down is not filtered. The scenario asserts the filter, its indicator, Clear, a qualified filter excluding another site, the full recorded span, and restoration after a reload.
- Copy as Markdown: format and truncation notice in `workitem-markdown.spec.ts`; the scenario reads the real clipboard for a two-observation ticket and for a 125-observation item whose second page is loaded before copying. The 1,000-observation cap is covered by the unit test only, because the fixture has no work item that large.
- Quiet changes: 13-day, 14-day, 15-day and no-activity cases and the selected timezone in `quiet.spec.ts`; the cards in `app.links.spec.ts` (nothing is claimed before the activity of the current selection loads) and in the scenario.
- No external request is made during the link scenario (all requests stay on the dashboard origin); link targets are read from `href` and the click is captured without navigating.

### Notes

- The ticket predicate mirrors SQLite `trim()`, which removes spaces only; stored ticket fields are trimmed on write, so the difference concerns only hand-edited journal rows.

## Ongoing board (add-ongoing-board)

Recorded on 2026-10-28 on Windows 10.0.26300.0, using .NET SDK 10.0.113, Node 24.13.0, OpenSpec CLI 1.13.1, and headless Microsoft Edge. All data used isolated synthetic fixtures under ignored artifacts directories.

### Validation results

- `dotnet build -c Release --no-restore`: succeeded with 0 warnings.
- `dotnet test -c Release --no-build --no-restore`: 357 passed, none skipped. New: `OngoingUnlinkedTests` (4 tests) covers the unlinked filtering (ticket-only, change-only, both, extracted-ID-only and plain observations), the half-open window, newest-first order, the repository filter, the 200-entry cap with `truncated` (200 entries are not truncated, 201 are), snapshot consistency (a later observation stays out, a snapshot beyond the journal is rejected), and the endpoint rejecting an unauthorized request and a window whose end does not follow its start.
- `npm test --prefix dashboard`: 190 passed in 16 files. New: `board.spec.ts` (card kinds, stage rules, follow-up detection, 6-day and 8-day expiry, filter matching, unranked-first ordering, rank kept across a column change, whole-column ranking, moves past filtered cards, drop placement, pruning and the 200-entry cap) and `app.board.spec.ts` (7-day window and unlinked read, no preference save from rendering, refresh or idle time, preferences without `board` unchanged, the filter not remembered, columns with counts, card labels and dates, quiet chip only on In-progress change cards, truncation note, empty states, cross-column drops rejected, keyboard moves saved, focus retention and `aria-live` announcement).
- `npm run test:e2e --prefix dashboard` against the Release build with the OpenSpec CLI on the PATH: 30 of 31 scenarios passed. The scenario "keeps history available when a declared store cannot be resolved" fails with OpenSpec CLI 1.13.1 exactly as recorded for QA-007 above (the declared store resolves, so the local change it expects to be hidden is listed). Playwright runs the scenarios serially, so the remaining scenarios were run with that one excluded (`--grep-invert "declared store"`): 30 passed.
- Fix rounds 1 and 2 (one card per change across sources, the 200-entry order fitted to 60 KiB by bytes, leaving room for the server's escaped-character count, `isQuiet`): `npm test` 190 passed, `dotnet test` 357 passed, e2e with `--grep-invert "declared store"` 30 passed. QA also ran the packaged `worklog.exe` build with `WORKLOG_DASHBOARD_NO_NODE=true`: 31 of 31 passed.
- The same suite with `WORKLOG_DASHBOARD_NO_NODE=true` against the Release `worklog.dll` (the OpenSpec CLI and Node absent from the dashboard's PATH, as in the packaged CI run): 31 of 31 passed. The first-round packaged run was not made by the developer; see the fix-round line above.
- New scenarios use a fixture repository with an unlogged proposal, an in-progress change with no ticked tasks, a recorded-done change with unticked tasks, a ticket without a change, a follow-up ticket for an archived change, and ad-hoc observations 6 and 8 days old (the 6-day one is in Done with its extracted ticket ID, the 8-day one is not on the board). They also cover the filter (not remembered after a reload), keyboard reordering with focus retention, the announcement and restoration after a reload, a saved rank restored for a card, and both themes at 1440 px and 700 px without horizontal overflow.

### Notes

- Journal entries with neither a ticket nor an OpenSpec change cannot be logged by the current tools (`log_work` requires one of them), so ad-hoc cards come from older journals. The fixture writes such rows directly to its disposable journal, as an older journal would have held them.
- The History fixture count rose from 68 to 74 rows because the board fixture adds a repository.

## Daily view (add-daily-prep)

Recorded on 2026-10-29 on Windows 10.0.26300.0, using .NET SDK 10.0.113, Node 24.13.0, OpenSpec CLI on the PATH, and headless Microsoft Edge. All data used isolated synthetic fixtures under ignored artifacts directories.

### Validation results

| Check | Result |
| --- | --- |
| `dotnet restore --locked-mode` | succeeded |
| `dotnet build -c Release --no-restore` | 0 warnings, 0 errors |
| `dotnet test -c Release --no-build --no-restore` | 374 of 374 passed (new: `DailyServiceTests`, 17 tests) |
| `npm test --prefix dashboard` | 236 of 236 tests in 18 files passed (`daily.spec.ts` and `app.daily.spec.ts` include the change-card line and the loading-state cases) |
| `npm run test:e2e --prefix dashboard`, Release build, OpenSpec CLI on the PATH | The serial run stopped at the existing scenario "keeps history available when a declared store cannot be resolved" (QA-007 from `fix-dashboard-reliability`): 15 passed, 1 failed, 20 did not run. With `--grep-invert "declared store"`: 35 of 35 passed, including the five Daily scenarios. |
| `./scripts/package.ps1 -OutputDirectory artifacts/<folder>`, then `npm run test:e2e --prefix dashboard` with `WORKLOG_DASHBOARD_EXECUTABLE` set to the packaged `worklog.exe` and `WORKLOG_DASHBOARD_NO_NODE=true` | Three full runs against one packaged build: 36 of 36 passed in each run (1.6 to 1.7 minutes). An earlier recorded 36 of 36 was not reproducible before Today and Copy for daily waited for the loaded Ongoing rows (QA-001). |

### What the checks cover

- Server (`DailyServiceTests`): Monday shows Friday and stepping back skips empty days (with the neighbouring recorded days); a day off on a Friday opens on Tuesday; day boundaries in Europe/Warsaw versus UTC at the local-midnight second; repository filtering of the last recorded day and its neighbours; two repositories on one day; an empty journal; only today recorded; status as of the end of the selected day when a later done exists, with the done, in-progress, started order and ties; several observations of one ticket collapsing into one line (latest primary, the rest newest first); two unlinked entries becoming two `adhoc:<entry id>` items titled by their trimmed summaries, with a missing summary kept as null; an explicit day equal to or after today, a malformed day, an unknown timezone and a malformed instant rejected; `today` holding the latest summary per item and being identical when an earlier `day` is selected; the start of today taken from `before`; the snapshot and bound handling of the previous and next lookups; the first valid time on a date whose midnight a clock change skips; and the endpoint requiring authorization and answering 400 for an invalid day.
- Client (`daily.spec.ts`): an In-progress card listed, a quiet In-progress change left out, a Done card listed only with an observation today, a change with all tasks ticked listed only with an observation today, no duplicates, the board priority order across columns, the Proposed count, a ticket card matched to its line by row id, a change card matched by repository and change name with the latest line winning, an ad-hoc card with no line, an In-progress card with nothing today having a null line, and the exact copied text in plain and Markdown formats (links on and off, the URL appended in plain text, unlinked lines, empty sections).
- Angular (`app.daily.spec.ts`): the digest request body, rendering order and collapsed observations, Today lines with and without a line, the board order driving Today, stepping through days via the address, a rejected day falling back to the last recorded day, refresh with the data refresh and after the date rolls over, no preference save from rendering, refreshing or stepping (and no `board` or `daily` key), remembering the format and links choice without the selected day, and the exact copied block in both formats with the clipboard fallback.
- Browser (fake clock on Monday 12 January 2026; the fixture's `daily-demo` repository holds work on Tuesday 6 and Friday 9 January): Monday shows Friday, stepping back skips to Tuesday, back and forward work and the day is not remembered; the plain-text block is copied exactly and the Markdown-with-links choice survives a reload; an observation logged for today by a second process appears under Today with its summary after a refresh while Yesterday keeps Friday's summary for the same ticket; reordering the two In-progress cards on the board reorders Today; and the view fits a 390 px viewport without horizontal overflow.

### Notes

- The fixture gained a `daily-demo` repository (identity `example/daily-demo`, change `daily-flow`) with observations on fixed past dates, and the smoke tool a `--dashboard-log` mode that records one observation into the fixture journal for the browser scenario. Observation times sit around noon UTC, so they keep their calendar day in timezones from UTC-11 to UTC+10.
- The plain-text copy scenario expects the fixture journal as created, so it runs before the scenario that logs an observation for today (the file runs serially); repeating a scenario against the same fixture (`--repeat-each`) sees that observation.
- Live observation on the maintainer's installation was not performed.

## Reader navigation and bounded comparisons (improve-dashboard-reading-and-reliability)

Recorded on 2026-11-05 with Node 24.13.0 on Windows x64. All browser fixtures and benchmark inputs are synthetic; browser screenshots and raw benchmark output stay under ignored `dashboard/test-results/` and `artifacts/` paths. Previous sections remain the historical records for their changes.

### Current implementation checks

| Check | Result |
| --- | --- |
| `dotnet restore --locked-mode` | Passed. |
| `dotnet build -c Release --no-restore` and fresh 0.4.1 package build | Passed with 0 warnings and 0 errors; source version remains 0.4.1 and the review candidate was not published. |
| `dotnet test -c Release --no-build --no-restore` | 427 passed, none skipped. Independent backend QA also passed 72/72 checks; its separate code-analyzer review found no production blocker. |
| `npm test --prefix dashboard` | 270 passed in 20 files. This includes the deferred-response focus regression and the QA-020 deleted-document 404/read-state 503 cases; both failure cases retain successful marks and drafts and show neutral refresh/retry guidance. |
| Focused normal Release browser scenarios before the final focus guard | 3/3 passed in 47.1 seconds. This earlier run is retained as focused evidence; the final full suite below covers the final source. |
| Full normal Release browser suite, final source | 44/44 passed in 4.8 minutes. |
| Fresh self-contained 0.4.1 packaged browser suite with Node absent | 44/44 passed in 2.3 minutes, including the declared-store scenario with no exclusions. |
| Packaged `--updates-only` smoke | `BOOTSTRAP PASSED`: launcher/version, MCP protocol/EOF, and offline status. |
| Integrated independent review | See this change's review reports and completion ledger for the current QA and code-analyzer disposition. |

### Comparison timing

The opt-in harness uses the same Node 24.13 Windows x64 environment, harness and workload definitions before and after the change: 3 warm-ups and 30 measured samples for each of eight workloads (50/200/400/800 KiB, small edit and full rewrite). Helper identity changed from `d4b657ceef14bc87ef90217d8e95d02402ada17ec3c8999b6f7664a766dcc585` to `9102bb3ebf64aa33084784bf50fe2b671a1ef4690a8652319a7225a2964ab92a`; harness SHA-256 is `481a15e4ef04c0d1e6dfd4756b93cecb28378721a352574089aa08a5c56e169b`.

| Full-rewrite size | Before p95 (ms) | After p95 (ms) |
| ---: | ---: | ---: |
| 50 KiB | 15.8704 | 16.786 |
| 200 KiB | 485.1853 | 51.5529 |
| 400 KiB | 1554.8551 | 51.0624 |
| 800 KiB | 3005.1003 | 51.973 |

The largest after p95 is 51.973 ms, below the 100 ms synchronous comparison target. All small-edit samples remained precise, with p95 below 3.3 ms. The bounded limit trades passage-level precision for the whole-document fallback on 200 and 400 KiB full rewrites; the 800 KiB rewrite was already incomplete in every baseline sample. This harness measures `readMarks` normalization, line tokenization, diffing and result processing. It does not measure complete Markdown rendering, removed-comment token extraction, browser interaction latency or arbitrary device performance. The focused browser case separately verifies that an empty current document still has a visible, focusable document-level stop without synthetic text.

Raw JSON: `artifacts/dashboard-reading-reliability/c04/baseline-before.json` and `artifacts/dashboard-reading-reliability/c04/after-bounded.json`.

Reproduce the opt-in timing run with at least 30 samples and one warm-up; the recorded run used 30 and 3:

```powershell
node scripts/benchmark-review-comparison.mjs --run --samples=30 --warmups=3 --out=artifacts/dashboard-reading-reliability/c04/<run>.json
```

Run dashboard checks from the repository root:

```powershell
npm test --prefix dashboard
npm run test:e2e --prefix dashboard
```

The packaged browser run requires a fresh self-contained package and uses its actual executable with Node removed from the fixture runtime path:

```powershell
$env:WORKLOG_DASHBOARD_EXECUTABLE = 'artifacts/releases/<package>/worklog.exe'
$env:WORKLOG_DASHBOARD_NO_NODE = 'true'
npm run test:e2e --prefix dashboard
```

Reader screenshots are captured from the synthetic reader body, including the unfiltered 50-document rail, and remain in ignored test output until reviewers reserve evidence. They exclude machine-specific provenance and do not hardcode an active change into default output.

## Agent digests on the Daily view (add-day-digests)

Recorded on 2026-10-29 on Windows 10.0.26300.0, using Node 24.13.0 and headless Microsoft Edge. All data used isolated synthetic fixtures under ignored artifacts directories.

### Validation results

| Check | Result |
| --- | --- |
| `npm test --prefix dashboard` | 245 of 245 tests in 18 files passed (new: 4 in `daily.spec.ts`, 5 in `app.daily.spec.ts`, including a Daily reload when only the digest revision moved) |
| `./scripts/package.ps1 -OutputDirectory artifacts/releases/<folder>`, then `npm run test:e2e --prefix dashboard` with `WORKLOG_DASHBOARD_EXECUTABLE` set to the packaged `worklog.exe` and `WORKLOG_DASHBOARD_NO_NODE=true` | 39 of 39 passed in 1.7 minutes, including the three new digest scenarios |

### Final validation (task 5.3)

In one run on 2026-11-02, on the current tree with a fresh package: the locked restore passed (the three `packages.lock.json` files were refreshed for the SDK patch), the Release build was clean, `dotnet test` 406 of 406, `npm test --prefix dashboard` 245 of 245, packaged browser acceptance (`WORKLOG_DASHBOARD_NO_NODE=true`) 39 of 39, the default isolated smoke run passed against the fresh executable, and `./scripts/package-update-fixtures.ps1` passed when started from PowerShell. The full table is in [MCP validation](mcp-validation.md#full-validation-set-task-53).

### What the checks cover

- Client (`daily.spec.ts`): a line matches only when its primary plus others are exactly a digest's covered IDs, in any order; a stale digest after a later entry, a subset and a superset do not match; an assembled change-card line (built from a ticket item and a change-only item) matches by the entries it was built from; the exact copied text in plain and Markdown formats uses the digest text on one line without a mark and keeps the summary where no digest matches.
- Angular (`app.daily.spec.ts`): a matching Yesterday line shows the digest with the "agent digest" mark while its status and collapsed observation are unchanged; a matching Today line shows it; a stale digest falls back to the latest summary with no mark; Copy for daily uses the digest text without the mark in both formats.
- Browser (fake clock on Monday 12 January 2026; the fixture's `digest-demo` repository): the digest and mark show on the Yesterday line for DGT-1 and the Today line for DGT-2, and the plain and Markdown copies use the digest text without the mark; after a second process logs a further observation for DGT-2, a refresh shows the new summary without the mark and "2 other observations today", Friday's digest still matches, and the copied text carries the new summary; a digest saved by a second process while Daily is open (journal unchanged) shows the mark and is copied within a poll, without Refresh.

### Notes

- The fixture gained a `digest-demo` repository (identity `example/digest-demo`). Its digests are written through the real save path (`DigestService.Save`) into the isolated `digests.db` of the fixture data directory. Observation times sit around noon UTC like the Daily fixture.
- The fixture's `DigestService` uses UTC, so fixture creation does not depend on the machine's timezone. The smoke tool's `--dashboard-digest` mode saves a digest through the real save path for the scenario that saves one while Daily is open.
- The staleness scenario logs into the fixture journal, so it runs after the display scenario in the serial file; repeating the scenarios against the same fixture (`--repeat-each`) sees that observation.
- Live observation on the maintainer's installation was not performed.
