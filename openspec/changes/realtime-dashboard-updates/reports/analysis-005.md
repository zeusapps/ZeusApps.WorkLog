# Analysis report: realtime-dashboard-updates / 005

## Review target

- Reviewer / role: independent code-analyzer, ANALYSIS-RECHECK-005.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime-resolved model introspection unavailable.
- Date: 2026-11-08.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-005.
- Task and requirement references: [tasks 2.1–2.4 and server 3.1–3.3](../tasks.md), [design decisions 2, 3 and 6](../design.md), [dashboard delta](../specs/work-dashboard/spec.md); recheck only ANALYSIS-040 through ANALYSIS-043 plus regression.
- Scope: `src/WorkLog/Dashboard` and `tests/WorkLog.Tests/Dashboard`. Browser/harness edits are concurrent and excluded.
- Reading limits / neighboring paths: read original [analysis 003](analysis-003.md), current scoped source and regression cases, and previously read role/project facts/template. No concurrent QA conclusions or orchestrator workflow were read. No new neighboring implementation paths were needed.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard`.
- Tracked manifest: modified `DashboardHost.cs`, `DocumentService.cs`, `SourceCatalog.cs`, `SourceCatalogTests.cs` within the scoped directories.
- Tracked diff SHA-256: `0a9a5cbc84589f41c0e5d735310de10495f6f315af47c5a1f3d57e7e707d9aa2`.
- Untracked manifest and SHA-256:

| File | SHA-256 |
| --- | --- |
| `src/WorkLog/Dashboard/DashboardChangeSource.cs` | `8241ae398e0136be338d053849d278cab4b844a18d55cf503c67e0d1589ae8a3` |
| `src/WorkLog/Dashboard/DashboardLiveConnections.cs` | `356ea2d43ad8edb87975b891f403dce17914de19a24d1e8da5c81a9849bc9c2f` |
| `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs` | `23c60660e00bedf2abbf31fc8f69e13b11ab501a9739ac4652f10f1b7a77921f` |
| `tests/WorkLog.Tests/Dashboard/DashboardChangeSourceTests.cs` | `aae4810c49773abe0dc5fcc5521841b8da0dd0cf48b265a2151e37c84435f1ab` |
| `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs` | `1b15185a2e30b457244f02b658314062b0db313cfbe6ff2cd0546580a0c40cab` |
| `tests/WorkLog.Tests/Dashboard/ManualTimeProvider.cs` | `80f730379a27edfd7cc8d19d6d15123b6a0f52dac9746e89859440bf3b6b5c45` |

- Before/after identity: both runs match `head=0d77685 baseline=0d77685 files=10 diff=0a9a5cbc8458 untracked=6/17c377e0516d`. No unexpected scoped mutation.
- Prior report / findings: [analysis 003](analysis-003.md), ANALYSIS-040–043. Regression allocation ANALYSIS-120–139 was reserved; no new ID or suggestion is raised.

## Analysis performed

| Area | Method / evidence | Outcome / limitation |
| --- | --- | --- |
| Watch-root containment | Trace initial/fallback changes path resolution and recursive binding | Original ANALYSIS-040 entry path is corrected at source level. `SourcePath.Within` rejects escaped OpenSpec/changes paths before enumeration/binding; fallback remains the approved root. New initial/rebound link fixture is present but unexecuted |
| Cache revalidation containment | Trace cache request ownership, eviction and `Resolve` before fresh reads | Original ANALYSIS-041 unchecked-old-key path is corrected at source level. Entries without an approved request are evicted; supported entries are re-resolved through live containment before reading. Parent-link replacement fixture is present but unexecuted |
| Independent domain progress | Inspect four reconciliation lanes, bounded merging, marker scheduling and delivery | Original ANALYSIS-042 single awaited batch is removed at source level. Journal/digest probes use worker tasks; completed lanes publish independently. Later-hint-after-blocker and actual WebSocket-vector assertions are present but unexecuted |
| Same-marker replacement | Trace all calls to `InvalidateJournalRepositories` and periodic full checks | ANALYSIS-043 remains failing for a missed lifecycle hint. The new cache generation is wired only from the hint flag, not from validated replacement detection during periodic reconciliation |
| Regressions / lifetime | Inspect lane pending queues, cancellation, lock failures and existing transport disposal | One pending merged batch and one active worker per lane; hint sets remain capped and overflow promotes domain-wide work. Transient lock errors retain dirty state. Existing transport/security implementation is unchanged. Cancellation of already-running synchronous SQLite work still relies on its bounded busy timeout; no timing execution was performed |
| Proportional checks | Identity before/after, scoped whitespace check and category-only Markdown hygiene scan | Passed. No server, test runner, build, installation or native client was started; no approval prompt issued |
| Supplied execution evidence | Developer handoff | Release build reportedly passed with zero warnings/errors; new link/cache/lane/lock/replacement regressions compile but have not been executed. Earlier passing test counts do not cover this corrected target |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Checkpoint loop and four lanes | Source observation | Five-second checkpoints still queue all-domain work and publish current vectors. Work is now split into catalog/documents, journal, digests and reads, with each completed lane publishing independently. Connection revocation retains its separate two-second cadence. This is improved scheduling structure, not measured lower CPU, lower I/O or latency |
| Cache lifetime versus interval and duration | Catalog and journal-derived repository cache | Source observation | Complete catalog cache remains two seconds with explicit invalidation; command cache remains independent. New repository-cache generation bypasses the same-rowid shortcut when explicitly invalidated, but the periodic missed-hint path does not advance that generation (ANALYSIS-043) |
| Unbounded state | Lane queues and global pending batch | Source observation | Four fixed lanes each have one active reconciliation, one merged pending batch and one semaphore wake. Merged hints cap at 128 per lane batch, then promote to overflow. Existing document cache, notification channel and actual watcher caps remain. Total live queue/worker high-water measurements have not been executed |
| Repeated full reads where a change check would do | Suspect body refresh and marker probes | Source observation | Suspect cached bodies are evicted/re-resolved and then reread only after containment succeeds; entries without request ownership are evicted rather than granted stale path access. Idle marker probes still query rowid/max-save metadata without payload decoding. No new benchmark was run |
| External tool discovery / timeouts | Existing command discovery and worker probes | Source observation | CLI resolution, 15-second tool deadline and expensive-cache backoff are unchanged. Catalog work is isolated from later independent-domain lanes. SQLite worker probes are awaited to completion and bounded by existing busy settings; exact cancellation and recovery deadlines remain unverified |

## Findings

### ANALYSIS-043: Same-rowid replacement still leaves stale repositories when the lifecycle hint is missed

- Severity: **warning**; existing finding remains unresolved for the required lost-hint reconciliation path.
- Related tasks / requirements: 2.2, 2.3 and 2.4; same-marker replacement, independent repository/catalog updates, and recovery after missed watcher hints.
- Locations at this corrected identity: `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs:338`–`:345`, `:480`–`:493`, `:536`–`:545`; `src/WorkLog/Dashboard/SourceCatalog.cs:50`–`:54` and `:118`–`:130`; `tests/WorkLog.Tests/Dashboard/SourceCatalogTests.cs:108`–`:133`.
- Corrected portion: `InvalidateJournalRepositories` increments a distinct repository-cache generation and invalidates the cheap projection. `ProcessBatch` calls it when `batch.JournalLifecycleChanged` is true and also queues catalog work. With a delivered lifecycle hint, same-rowid A-to-B replacement can therefore refresh the repository identities without blanket command-cache invalidation.
- Remaining observation: that call site is the only production caller. A full five-second check has no lifecycle hint flag. `ReadJournalMarker` can detect the replacement through changed file/WAL signatures and advance the journal revision, but `ReconcileJournalAsync` does not invalidate the derived repository cache. Full catalog discovery still sees the same maximum rowid and unchanged repository-cache generation, so it continues using the former repository list indefinitely. The new test manually calls `catalog.InvalidateJournalRepositories`; it does not demonstrate that periodic detection reaches that call when the watcher hint is absent.
- Bounded reproduction: with a disposable manual-change-source host, load a one-row journal containing only synthetic repository A and allow all domains to settle. Atomically replace it with another one-row journal containing only synthetic repository B; allow file metadata to differ but deliver no filesystem lifecycle hint. Wait for ordinary five-second reconciliation. Expected: journal and catalog reflect B and remove journal-only A without an appended observation. Remaining source behavior: journal revision advances, while the derived repository list stays A because the sole invalidation call is never reached. The same issue can affect a replacement recognized through availability recovery without a delivered lifecycle hint.
- Evidence type: source observation on the exact corrected target. No runtime replacement or test execution was performed by this reviewer.
- Recommendation: connect derived-cache invalidation to validated journal lifecycle/availability identity changes, including periodic recovery, while preserving ordinary unchanged-read caching and independent expensive command caches. This states the remaining acceptance outcome; no additional correction cycle was started by this reviewer.
- Behavior-preservation risks: distinguish append-only row changes from replacement/availability identity; retain configured repository identities; do not force Git/OpenSpec discovery or query all repository names on every unchanged probe.
- Verification plan: extend the detector-level replacement fixture to drop the lifecycle hint and rely on the ordinary checkpoint; assert delivered journal/catalog vectors plus catalog repository IDs and notices. Run the already added delivered-hint, missing/recovered and lock isolation cases separately when execution is available.
- Relationship: continuation of original ANALYSIS-043, not a new finding or suggestion. The orchestrator was informed that the second correction batch boundary requires a decision before any further fix cycle.

## Re-verification

| Finding | Corrected target / source evidence | Independent result | Execution limitation |
| --- | --- | --- | --- |
| ANALYSIS-040 | `RebindWatchersAsync` resolves initial/fallback OpenSpec paths through `SourcePath.Within` before adding or traversing; escaped paths retain only a contained recovery root | Original bypass corrected at source level | Initial/rebound real link fixtures added but not run; no claim of atomic protection against arbitrary concurrent path swaps |
| ANALYSIS-041 | Cache entries retain `DocumentRequest`; `RevalidateSuspectAsync` evicts old keys, calls live `Resolve`, then reads the resolved path; ownerless entries are discarded | Original bypass corrected at source level | Parent-link fixture added but not run; no independent external-body access probe |
| ANALYSIS-042 | `ProcessBatch` dispatches four fixed lanes; each lane owns bounded pending work and publishes completion; journal/digest synchronous marker work is scheduled through awaited worker tasks | Original serial scheduling corrected at source level | Later-arriving journal and journal-only-lock fixtures include actual WebSocket delivery checks but remain unexecuted; no measured sub-second/five-second guarantee |
| ANALYSIS-043 | New repository cache generation and delivered-lifecycle-hint wiring | **Still failing** for missed-hint/full-check replacement | Direct cache-reset fixture is not detector-level proof of periodic recovery; exact remaining case above was not run |

## Assessment and limitations

The corrections close the original watcher/cache access paths and replace serial batch execution with bounded independent domain lanes at the source level. They do not establish executed transport, link, lock, cancellation or latency acceptance. ANALYSIS-043 remains open because the fallback detector does not propagate validated replacement identity into the repository cache when its hint is missing.

No new suggestion or unrelated finding was introduced. This is the assigned second-batch recheck; the orchestrator owns the unresolved disposition and user decision. Only this reserved report was written. Prior reports, implementation, task state and the ledger were not modified, and concurrent browser/harness edits were excluded from the identity.
