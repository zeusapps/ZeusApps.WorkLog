# Analysis report: realtime-dashboard-updates / 001

## Review target

- Reviewer / role: independent reviewer, ANALYSIS-REVIEW-001 / code-analyzer.
- Client / version / mode / model (resolved ID): Codex CLI 0.166.0 / Default; assignment configuration `gpt-6.1-sol`, high reasoning. Runtime-resolved model introspection is unavailable.
- Date: 2026-11-06T09:21:08Z.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-REVIEW-001.
- Task and requirement references: [tasks 1.1–1.3](../tasks.md), [measurement design](../design.md), [dashboard delta](../specs/work-dashboard/spec.md), [reader delta](../specs/openspec-browser/spec.md). This review covers baseline tooling, not production C06 behavior or final acceptance.
- Scope and relevant neighboring paths: `scripts/c06-dashboard-acceptance.mjs`, `scripts/c06-instrument-snapshot.ps1`, `tools/WorkLog.Smoke/C06BaselineFixture.cs`, changed dispatch in `tools/WorkLog.Smoke/Program.cs`; relevant dashboard source/instrumented counterparts and build configuration.
- Reading limits and paths read outside scope: no orchestrator workflow, other review conclusions, source edits, screenshots, builds, browser benchmarks or copy execution. Read `.agents/roles/{code-analyzer,project-facts}.md`, `.agents/templates/analysis-report.md`, `docs/architecture.md`, change proposal/design/tasks/specs, `Directory.Build.props`, `src/WorkLog/WorkLog.csproj`, `tools/WorkLog.Smoke/WorkLog.Smoke.csproj`, `dashboard/scripts/stamp.mjs`, `dashboard/src/app.ts` polling excerpts, and `src/WorkLog/Dashboard/{DashboardHost,DashboardJournal,SourceCatalog,SourcePath,LocalCommand}.cs`, `src/WorkLog/Persistence/{DigestStore,SqliteCommands}.cs`. Ignored evidence reads: snapshot manifest and generated source/callsite searches under `artifacts/realtime-dashboard-updates/baseline/instrumented/baseline-validated`, final smoke JSON `baseline/2026-11-06T09-14-25-920Z-822036f3.json`, and preserved package hash. No other reports were read.
- Baseline commit: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Implementation identity: HEAD and baseline both `0d7768581a0305725454dc33d610924cce9d61ef`. Manifest: untracked `scripts/c06-dashboard-acceptance.mjs`, `scripts/c06-instrument-snapshot.ps1`, `tools/WorkLog.Smoke/C06BaselineFixture.cs`; modified `tools/WorkLog.Smoke/Program.cs`. Tracked diff SHA-256 `9035191ea8010f63183b2c974ff730563d71584ce48136dd091128e7bfec1336`. Untracked SHA-256 values respectively `d2062f4fa306378936bb68e197faad41a91826d1457815080bc0a2515eebb41c`, `d0f638003ab4d99140a35d03a9b7f6c4d94aafd2a38d25e94f8ec3adab9d9480`, `11903163b04e7b32a5c098a908d74912d3f1366ab7bd5b422b9ff45b28f7b6c8`.
- Before/after identity: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths scripts/c06-dashboard-acceptance.mjs scripts/c06-instrument-snapshot.ps1 tools/WorkLog.Smoke/C06BaselineFixture.cs tools/WorkLog.Smoke/Program.cs` returned identical manifests/hashes before and after analysis. Both summaries: `head=0d77685 baseline=0d77685 files=4 diff=9035191ea801 untracked=3/e7f7e00974d0`. No unexpected scoped mutation.
- Prior reports / rechecked findings: none.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Syntax / complexity | `node --check scripts/c06-dashboard-acceptance.mjs`; PowerShell `Parser.ParseFile` on instrumenter | Both commands exited 0; PowerShell returned no parse errors | Passed syntax only; generated C# has the namespace defect below |
| Instrumentation / architecture | Inspect guarded replacements and generated snapshot | Exact anchors, source/target/transform/instrumenter hashes, copy-only protected `/api/c06-metrics`, cumulative counters | Correct separation from product source; instrumentation cannot yet compile, ANALYSIS-001 |
| Fixture fidelity | Inspect fixture creation and writer dispatch | Two repositories, two active and two archived changes, 50 Markdown files per active change, repeated nested names, exact 100 KiB initial design, 1,000 base observations plus four additional digest-pair observations and two digests | Shape is explicit; metadata's 1,000 count describes base observations rather than total rows (1,004 before measurement writers). Digest-only samples reuse an unchanged pair and assert stable max rowid |
| Latency / browser measurement | Inspect sample loop and DOM waits | Three warm-ups, at least 30 measured samples, fresh targets, writer commit acknowledgments, visible DOM/text waits, p50/p95/variation and per-sample timestamps | Sound source-level timing boundary; 50 ms document check granularity and post-writer-process observation are measurement overhead. Full traces and counter results pending |
| Provenance | Identity script, package hash and snapshot manifest inspection | Original package SHA-256 `cf11ef5faae749ca76259ee05745ed8f4e8aa42590bd3a0f4f8c951e5d5e7f46`; manifest source inputs `49c0995726cb68161416c45f364b84fdc92caef8abec9ad74a24b8e888370f94`, transforms `b0216468e330d6f595f6034a611c5ac87e1c28e23f1353dbc4147697bc8b1253`, target `310dc197d7ea317b521ee2ab4a13da7320955508e6e53df0e5386b306faaf942` | Package hash matches assignment. Manifest values inspected, not independently recomputed as whole-tree hashes. Instrumented build/runtime validation not performed |
| Isolation / lifecycle | Inspect roots, random fixture creation, owned port/process and final cleanup; read existing final smoke JSON | New disposable fixtures, explicit data directory, ephemeral listener, retained fixtures, no source deletion in browser harness. Snapshot target is normalized and checked beneath the artifact root; staging root is constructed from that absolute root plus a GUID, deletion suffixes are fixed, and snapshots reject existing target names | Existing smoke evidence reports three visibility checks, stable digest journal marker, server exit and port release. Does not prove descendant cleanup or cancellation, ANALYSIS-002 |
| Parity | Inspect writer controls and final MCP reference label | Shared Journal/DigestService path and preserved package protocol fixture | Client-neutral tooling; no integration policy duplication. Actual full protocol acceptance pending; no native Claude Code/Codex session evidence claimed |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Baseline browser poll; five-minute sampler; optional counters | Source observation, not measured full-run totals | Baseline polls every 2 seconds: approximately 150 opportunities per five-minute window, reading catalog and conditionally open document/reviews/reads/search/activity. Catalog discovery reads source metadata and journal markers; exact realized counts depend on cache/races. Sampler does no recurring process probe: CPU/working-set and optional metric snapshots occur at window boundaries. Production detector is out of scope |
| Cache lifetime versus interval and refresh duration | Existing catalog and CLI caches | Source observation | Catalog's 2-second complete cache equals the browser interval, so cannot reliably eliminate discovery on the next poll. CLI cache is 60 seconds, timeout backoff 30 seconds and command parallelism 4. Baseline behavior is preserved for comparison; no measured refresh-duration claim |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Harness request/frame/sample arrays, retained fixtures and JSON | Source observation | Arrays grow through a finite run, with default 45-minute deadline and fixed idle periods; no explicit request/frame count cap. Fixture outputs are retained intentionally per run and need operator artifact retention. No new production cache/queue introduced. Deadline/cancellation limitations below |
| Repeated full reads where a change check would do | Baseline document/journal work, instrumentation | Source observation | Unmodified baseline may repeatedly enumerate metadata and query journal markers; actual Entry decodes and content reads/hashes are counted at named callsites, not inferred from browser request reduction. Document existing cache/knownRevision behavior remains. Copy counters do not represent all OS metadata operations, SQLite page I/O or kernel reads |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Harness `git`, `dotnet`, `powershell`, Edge; existing LocalCommand | Source observation | Explicit package path and smoke DLL; PATH tools for fixture/writers/CPU snapshots, browser channel defaults to msedge. Fixture and writer bounds 120 seconds, setup writer 30 seconds, MCP reference 60 seconds, CPU process snapshot 5 seconds. `git rev-parse` and executable `--version` calls lack explicit spawnSync timeouts. Host CLI calls use 15-second timeout, but Windows OpenSpec lookup covers a PATH-adjacent npm-style node_modules shim. No new discovery policy is added. Host termination does not prove child termination |

## Findings

### ANALYSIS-001: Digest instrumentation inserts an unresolved type name

- Severity: warning (blocks instrumented build and server-work acceptance).
- Related task / requirement: task 1.2; design decision 7, separate instrumented server-work evidence.
- Locations: `scripts/c06-instrument-snapshot.ps1:103`, `:139–145` at the reviewed identity; generated `src/WorkLog/Persistence/DigestStore.cs:130` and `:134` in ignored `baseline-validated` snapshot.
- Observation: `Add-CountedExecutions` inserts `C06Metrics.Sqlite(...)`; digest row replacement inserts `C06Metrics.DigestRowMaterialized()`. `DigestStore` is in `WorkLog.Persistence` with no `using WorkLog.Dashboard` or applicable global import. The generated type exists only in `WorkLog.Dashboard`. Therefore these two references cannot resolve. PowerShell parsing and anchor-count success cannot validate C# type resolution. The neighboring `SqliteCommands` transforms correctly use the fully qualified name.
- Proposed improvement and benefit: qualify inserted metric references as `WorkLog.Dashboard.C06Metrics` or add one guarded import to the copied digest source. Generate a fresh immutable snapshot; retain existing snapshot/manifest as failed history.
- Behavior-preservation risks: instrumentation must remain copy-only; preserve original digest read semantics and counter boundaries, and regenerate transform/target provenance.
- Evidence type: source observation in actual generated snapshot; compilation was not run during the active baseline measurement window.
- Verification / measurement plan: after release of the measurement window, build the fresh isolated copy, then exercise an isolated digest read and assert both SQLite and materialized-row counters move without journal change. Test unauthorized metric access and confirm original source identity remains unchanged. A successful build is required before accepting counter evidence.
- Relationship: new finding; no prior review conclusions consulted.

### ANALYSIS-002: Cleanup proves host exit but does not own descendant cancellation

- Severity: warning.
- Related task / requirement: task 1.1 bounded probes/cleanup; future task 7.2 zero leaked owned processes.
- Locations: `scripts/c06-dashboard-acceptance.mjs:77–79`, `:280–293`; relevant existing `src/WorkLog/Dashboard/LocalCommand.cs:27–51` at the reviewed identity.
- Observation: deadline and finally cleanup call `server.kill()` for the host PID, close the browser and verify host exit plus listener release. The host can spawn Git or OpenSpec/Node discovery processes. Their 15-second cancellation timer belongs to that host and cannot run after forced host termination. There is no owned process-tree/job lifetime or descendant assertion, and no explicit external-cancellation handler that routes through cleanup. A successful host/port cleanup result therefore does not establish the required process cleanup, especially if an external command hangs. No actual leaked descendant was measured in this review.
- Proposed improvement and benefit: give the launched host and its descendants an owned bounded lifetime (for example an appropriate Windows job or a verified owned-tree cleanup mechanism), route supported cancellation through the same cleanup, and record termination of owned descendants as part of cleanup evidence. Restrict termination to processes started by this probe.
- Behavior-preservation risks: never adopt/terminate unrelated dashboard, MCP or agent processes; account for PID reuse and wrapper/payload lifetimes. Keep cancellation cleanup bounded and preserve the last evidence file.
- Evidence type: source observation / unmeasured lifecycle risk, not a claim that the ongoing capture leaked processes.
- Verification / measurement plan: in a disposable fixture, use a controlled hanging CLI child and cancel/expire the harness; assert host, owned child and browser exit, port release and saved cleanup status. Test regular completion as regression. No such probe was run during the baseline window.
- Relationship: new finding.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| None | No corrections assigned | Initial independent review | Not applicable |

## Assessment and limitations

The baseline workload structure, immutable source-copy intent and method-boundary metric labels are reviewable and appropriately separate browser request counts from CPU/allocation and server work. There is no product implementation in this target. ANALYSIS-001 must be corrected before instrumented server-work evidence can be collected; the pure preserved-package capture remains useful and need not be stopped because of it. ANALYSIS-002 needs bounded lifecycle verification before cleanup acceptance is claimed.

No restore/build/npm install, long browser run, source-copy execution or product-source write was performed. Full baseline capture was still pending under the orchestrator's measurement window, so latency totals, full idle counters, normal/packaged acceptance and MCP reference completion are not marked passed. The final short smoke JSON was only inspected as pre-existing evidence, not independently rerun. The ongoing raw run's phase label `true` is an invocation/parser mismatch communicated by the orchestrator; retain that raw evidence unchanged and map its baseline package/workload identity explicitly in a summary. Task 1.1 deterministic fault/cancellation proof and task 1.3 broader behavior/protocol evidence remain acceptance work, not waived requirements. No native client, live installation or actual physical-I/O measurement was performed. Orchestrator owns finding triage and task status.
