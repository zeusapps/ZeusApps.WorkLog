# Analysis report: realtime-dashboard-updates / 027

## Review target

- Reviewer / role: ANALYSIS-INVESTIGATION-027 / code-analyzer; bounded source, raw-record and no-host attribution.
- Client / version / mode / model (resolved ID): Codex CLI / `0.167.0` observed in preceding review / Default delegated review / resolved model ID unavailable. Agent role is `default`; no model override supplied.
- Date: 2026-11-11T10:12:08Z.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-INVESTIGATION-027.
- Task and requirement references: [tasks](../tasks.md) 4.1, 4.2, 5.4, 7.2; [design](../design.md) resource ownership, domain reconciliation and fault matrix. Investigate the supplied failed five-category smoke, without attributing a product cause from status text alone.
- Scope: frozen `scripts/c06-fault-matrix.mjs`, `c06-disconnect-control.mjs`, `c06-disconnect-control.spec.mjs`, `c06-acceptance-invariants.mjs`, `c06-host-lifecycle.mjs`; permitted neighboring reads `dashboard/src/app.ts`, `live-connection.ts`, `live-updates.ts`, `tools/WorkLog.Smoke/C06LiveAcceptanceHost.cs`.
- Reading limits and paths read outside scope: current QA-024, ledger, HANDOFF, orchestrator workflow and other reports were not read. Previously read role, project facts, report template, architecture, change tasks/design and own analysis-024/026 context retained. Supplied raw input: `artifacts/realtime-dashboard-updates/fault-matrix/2026-11-11T10-06-32-478Z-3f8c885d.json`. The identity script was executed. The proof loads installed dashboard TypeScript solely to transpile extracted actual methods and `live-updates.ts`; no framework or native service is started.
- Baseline commit and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Implementation identity: original five-script manifest remains five untracked files, empty tracked diff SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`; hashes below.
- Expanded identity: `dashboard/src/app.ts` modified, other three neighboring paths untracked. Tracked diff SHA-256 `15ad8a7590de6fbbaf3146f09d425ffa32ec1454a85440787a67499272f2376e`; app file SHA-256 `a0fa4b75a5ab59d62c4cbae7492e034393e5a5e66ea78cf6ef48446e1325c965`.
- Before/after identity: original summary both `head=0d77685 baseline=0d77685 files=5 diff=e3b0c44298fc untracked=5/539857b9a06c`; expanded summary both `head=0d77685 baseline=0d77685 files=4 diff=15ad8a7590de untracked=3/7208d30d1f9e`. HEAD, baseline, manifests and hashes match; no mutation observed.
- Identity commands: `pwsh -NoProfile -File scripts/review-identity.ps1 -Baseline 0d77685 -Paths` followed by the five original script paths; same command with `dashboard/src/app.ts dashboard/src/live-connection.ts dashboard/src/live-updates.ts tools/WorkLog.Smoke/C06LiveAcceptanceHost.cs` for neighboring paths.
- Prior reports / rechecked findings: [analysis-026](analysis-026.md) supplies prior frozen-script context; this is a new attribution, not reopening its verified source corrections. ANALYSIS-560 allocated below; ANALYSIS-561 through ANALYSIS-579 unused.

| Scoped path | SHA-256 |
| --- | --- |
| `scripts/c06-acceptance-invariants.mjs` | `1d5c983dc1ad019a9d382ff3568c85f136986a895236593a6397ba335b7cd915` |
| `scripts/c06-disconnect-control.mjs` | `a84529e90dd0a87a16502a09a97b0e3d15736fe042c68d0fda0d4e4c47270b34` |
| `scripts/c06-disconnect-control.spec.mjs` | `afa09a6b99997b4f2998320e744982e2d80968110de45980b41c26c02054449a` |
| `scripts/c06-fault-matrix.mjs` | `069a829f4ce520a5fc9566146b10d7574c2cf365af391c67491415892bf12037` |
| `scripts/c06-host-lifecycle.mjs` | `4b27ee19a227c22e4302d51f0b7294c3d7fe2145ac0c33f5d74254a1df341009` |
| `dashboard/src/live-connection.ts` | `78db24467ab95c0221d16c986c57ee9e59ff27a35a7140cfd032228b6783e5ac` |
| `dashboard/src/live-updates.ts` | `1430c9e42ae7790af48e9c48af8eefbd6decf65faf94f085b0f7c5319ee16d54` |
| `tools/WorkLog.Smoke/C06LiveAcceptanceHost.cs` | `0fec8a6ea5cada73a2a7f143179a01f0d01398d4a3b9ec93eaf312edbd476ae4` |

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Exact failing cycle and phase | Parse supplied raw records, transitions, diagnostics and source control flow | Completed records are 1/disconnect/Documents and 26/revisionWire/Journal; both have completion timestamps | Cycle 26 completed. Cycle 51 preparation navigates both target tabs to Daily and times out waiting 20 seconds for Live. Cycle 51 restart injection was never entered |
| Missing incomplete record | Inspect runner loop `:1092-1125` | `prepareVisibleState` and initial status run before record creation and inner fault try/catch | Preparation failure reaches outer catch without `incompleteCycle`. Its absence does not implicate the last completed record. This explains attribution limits without proposing a protocol change |
| Routing/state-leak hypothesis | Inspect post-26 path, new-connection lifecycle and browser responses | `cycleWire` clears `activeWireFault` after successful assertions; raw records 26 complete. Cycle 51 A-target `a8` connects upstream and opens in the page; B opens independently; negotiate responses succeed | No affirmative evidence of lingering wire injection or held disconnect replacement causing this failure. Does not prove every possible transport issue absent |
| Data/refresh attribution | Inspect actual domain dependencies, epoch handling and controller Boolean gates | `app.ts:671-730,752-784,1200-1227`; `live-connection.ts:143-150,210-231` | Concrete duplicate Daily owners produce false reconciliation despite successful responses; ANALYSIS-560 |
| Independent actual-method reproduction | `node artifacts/realtime-dashboard-updates/reviews/analysis-027/daily-owner-proof.mjs` | [proof](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-027/daily-owner-proof.mjs), [results](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-027/daily-owner-results.json), exit 0 | Actual app methods plus actual RefreshQueue/RevisionTracker reproduce two identical requests, both successful, current data applied, overall reconciliation false and journal queue dirty. Unchanged checkpoint does not retry; each individual domain succeeds with one request |
| Runtime correlation | Extract paired Daily request/response times and subsequent pause for both target labels | Table below | Both runtime traces match the independently reproduced failure mechanism. Runtime bodies/epochs are not captured, so this is source-supported attribution, not native instrumented proof excluding additional causes |
| Host ownership/status hypothesis | Inspect C06 host control loop and raw cleanup | No status-command timeout in raw failure. Final wrapper exit, source disposal and fixture-port release succeed, wrappers remaining zero | Earlier status-ACK failure did not recur. No ACK/control protocol redesign justified by this record |
| Stability | Before/after original and expanded identity commands | Exact matching summaries above | No implementation writes; source released for developer correction after report |
| Runtime and broad suites | No host/build/browser run | Developer owns native execution | Not performed. The bounded method probe was resumed only after the parent relayed the user's clarification to run required tests |

The raw trace is a failed five-category smoke with two completed cycles; it is not an incomplete cycle-26 loss-window result. Cycle 26 drops one advanced Journal revision and later receives the qualifying checkpoint, then completes its state assertions. The next iteration sets active cycle 51 before navigating targets to `/daily`. Both targets show initial Reconnecting, establish new sockets, receive successful API responses, then show Updates paused with no successful live refresh. The 20-second wait is in `navigateTarget` during preparation. The owned server restart function is not reached.

| Target | Concurrent Daily request times, monotonic ms | Successful Daily response times, monotonic ms | Paused time, monotonic ms |
| --- | --- | --- | --- |
| A target | 20395.0462, 20395.0614 | 20420.0976, 20565.6574 | 20575.1867 |
| B target | 20339.1536, 20339.1671 | 20361.4753, 20507.6631 | 20518.2267 |

All four responses in this table have HTTP status 200. No HTTP error, request failure, page error or console failure is recorded for this phase. These observations support a reconciliation-result failure rather than a failed HTTP fetch. HTTP 200 alone cannot prove payload validity; the independent proof therefore uses successful controlled responses to establish the source defect directly.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Daily reconciliation, same-vector checkpoints, harness preparation | Source observation plus bounded method measurement | One all-domain Daily reconciliation starts two identical Daily HTTP reads instead of one; both raw tabs exhibit this pair. The no-host reproduction measures two calls, current data applied, failure result. After failure, an unchanged non-forced checkpoint performs zero retries and returns false while domains remain pending. This is persistent paused state, not a measured ongoing request flood. No Git/OpenSpec process or product file/hash work is added by these app methods. Host fixture polls its control file every 20 ms; unrelated to the observed failure |
| Cache lifetime versus interval and refresh duration | Separate RefreshQueue entries for journal and digest, shared Daily epoch | Source observation / controlled result | Queue deduplication applies per key, so `journal-view` and `daily-digest` do not share in-flight work. Both touch the same `dailyEpoch`; the older request returns false. The journal queue retains failure and the digest queue can cache success. Identical checkpoints do not retry it automatically. Initial Live preparation deadline remains 20 seconds; increasing it would not correct the demonstrated dependency conflict |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | App queues and revision state; finite harness evidence | Source observation | App RefreshQueue uses fixed resource names; each retains one in-flight and one desired job, and RevisionTracker has five domain counters. No new accumulating collection is required for the correction. Harness history remains per invocation with finite cycle plan/global deadline; not measured for growth in this investigation |
| Repeated full reads where a change check would do | Duplicate Daily authoritative reads | Source observation / raw HTTP counts | Both dependency paths request the same Daily query during initial Snapshot. Exact server DB-read cost is not measured; two HTTP requests are observed and the dependency duplication is reproduced. One resource owner removes duplicate scheduling without a new polling or full-read strategy |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Proof and existing fixture harness | Source observation | Proof uses Node via PATH and repository-local installed TypeScript; no child service, host, browser or product executable launched. Existing harness resolves Playwright from dashboard dependencies and Smoke from fixed Release output, with global/operation deadlines. No discovery change proposed or required |

## Findings

### ANALYSIS-560: Journal and digest queue owners race the same Daily epoch and pause successful reconciliation

- Severity: warning; blocks Daily initial Live state and the smoke before restart fault injection.
- Related task / requirement: 4.1 one in-flight read per resource; 4.2 Daily journal/digest routing; 5.4 truthful successful reconciliation; 7.2 matrix progression.
- Locations at the reviewed identities: `dashboard/src/app.ts:671-696` (success and pending gates), `:727-730` (two owners), `:752-763` (journal work includes Daily), `:766-784` (scope activation repeats both owners), `:1200-1227` (shared Daily epoch and stale false); `dashboard/src/live-updates.ts:22-31` (initial instance makes every domain pending); `dashboard/src/live-connection.ts:143-150,210-231` (false Snapshot result pauses). Runner `scripts/c06-fault-matrix.mjs:515-525,1092-1125` establishes the failing preparation boundary.
- Observation: Initial Snapshot marks journal and digests pending. `reconcileDomains` starts `journal-view`, whose `loadVisibleJournal` starts `loadDaily` when the view is Daily. It also starts `daily-digest`, whose job calls `loadDaily` again. These are separate queue keys, so neither deduplicates the other's request. Both increment the same `dailyEpoch`; the earlier read returns false after the later starts, even when both HTTP responses succeed and the later correctly updates Daily data. The false result propagates through `Promise.all` to failed journal work and failed Snapshot reconciliation, which pauses the established live connection. The same vector on later checkpoints returns false without retrying pending work. `activateLiveScope` also schedules this duplicate pair.
- Proposed correction and benefit: Give the Daily resource one queue owner with journal and digest revision dependencies and the existing Daily scope. Keep Daily out of the journal job's nested reads; use the same Daily owner for scope activation. Include both revision dependencies in its token so journal-only and digest-only changes both refresh current Daily data, and simultaneous changes cause one read. Preserve Ongoing/activity/detail work in the journal path. This removes the concrete same-resource race and makes successful authoritative responses eligible to complete reconciliation.
- Behavior-preservation risks: Do not change stale epoch handling to unconditional success, add a blind Retry click, loosen the Live prerequisite, extend the timeout as a substitute, or weaken the five-second recovery threshold. Preserve actual failure propagation, latest-scope ownership, retained pending work after real failures, journal-only and digest-only freshness, visibility/navigation behavior and midnight/date selection. Required verification should cover initial both-domain Snapshot and both-domain update, each isolated domain, genuine Daily failure, and superseded scope behavior, followed by developer-owned Daily/browser smoke.
- Evidence type: source observation; independent deterministic execution of actual app methods and actual queue/tracker; supplied runtime paired-request/paused trace. No native instrumented replay by this reviewer.
- Verification / measurement plan: The existing ignored proof confirms the current defect with two identical requests and false overall result despite successful responses, plus one-request successful isolated-domain controls. A corrected implementation should demonstrate one Daily read for a combined target and successful overall reconciliation, retain genuine failure and supersession behavior, then pass native Daily initial load and the formerly failing cycle-51 preparation through the strict Live gate. Developer owns production tests and runtime execution.
- Relationship: New concrete defect discovered while investigating the supplied smoke. Distinct from prior corrected disconnect ownership/handshake findings and from the non-recurring host-status-ACK issue. No optional suggestion or protocol redesign.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-560 | Current expanded target above; no correction yet | Actual-method proof returns false with two successful duplicate Daily reads and leaves journal work dirty; raw both-target trace matches | Still failing; new finding for orchestrator triage |
| Prior attribution: incomplete cycle 26 | Supplied raw | Both completed records have completion timestamps; cycle-51 preparation fails before record creation | Corrected attribution: cycle 26 completed; cycle 51 fault was never injected |
| Earlier status-ACK failure | Supplied raw only | Failure is Live locator timeout; no command-ACK timeout, cleanup succeeds | Not reproduced in this record; no claim of universal correction |

## Assessment and limitations

The observed blocker has a concrete source defect and an independent bounded reproduction. It is unnecessary to assume a routing leak, failed initial HTTP request or server restart defect to explain the Daily pause. This report does not assert that every other runtime cause is excluded; it identifies the duplicate Daily owner as a proven required correction consistent with both target traces. The raw artifact does not expose the internal Boolean or epoch values, so direct native causation is not overstated.

Only this report and ignored `reviews/analysis-027` proof/results were written. No source, documentation, tests, raw record, ledger, prior report or client settings were modified; no host/build/browser or subagent was started. Original and expanded source identities remained stable. Source is released for developer correction; orchestrator owns triage and subsequent QA acceptance. The supplied five-category smoke remains failed with two completed cycles and must not be counted as acceptance.
