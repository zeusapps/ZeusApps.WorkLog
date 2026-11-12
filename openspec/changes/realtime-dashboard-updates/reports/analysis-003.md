# Analysis report: realtime-dashboard-updates / 003

## Review target

- Reviewer / role: independent code-analyzer, ANALYSIS-REVIEW-003.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime-resolved model introspection unavailable.
- Date: 2026-11-08T17:24:14Z.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-REVIEW-003.
- Task and requirement references: [tasks 2.1–2.4 and server portions of 3.1–3.3](../tasks.md), [design decisions 2, 3 and 6](../design.md), [dashboard delta](../specs/work-dashboard/spec.md), and [reader delta](../specs/openspec-browser/spec.md).
- Scope: `src/WorkLog/Dashboard` and `tests/WorkLog.Tests/Dashboard`. Browser, acceptance harness and documentation edits remain concurrent and are excluded from this implementation identity. This report is a backend review, not final integrated acceptance.
- Reading limits / neighboring reads: role, project facts, report template, planning artifacts, the assignments and finding definitions in the ledger, and prior analysis 002; no QA conclusions or orchestrator workflow read. Neighboring implementation read: `src/WorkLog/Persistence/DigestStore.cs`. Read `docs/architecture.md` as the architectural reference; its pending C06 documentation update is outside this review target. No browser implementation was reviewed.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Review identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard`.
- Tracked manifest: modified `DashboardHost.cs`, `DocumentService.cs`, `SourceCatalog.cs`, and `SourceCatalogTests.cs` in the scoped directories.
- Tracked diff SHA-256: `3c05ca98d991119c544de6ce431a7a46b4ffbbcf30a47791c7ae9f72ed87ef52`.
- Untracked manifest and SHA-256:

| Scoped file | SHA-256 |
| --- | --- |
| `src/WorkLog/Dashboard/DashboardChangeSource.cs` | `8241ae398e0136be338d053849d278cab4b844a18d55cf503c67e0d1589ae8a3` |
| `src/WorkLog/Dashboard/DashboardLiveConnections.cs` | `356ea2d43ad8edb87975b891f403dce17914de19a24d1e8da5c81a9849bc9c2f` |
| `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs` | `76221f395d4b2b612e6388aac2cdb2622608e779f33b87222b7c481e2fa9861f` |
| `tests/WorkLog.Tests/Dashboard/DashboardChangeSourceTests.cs` | `aae4810c49773abe0dc5fcc5521841b8da0dd0cf48b265a2151e37c84435f1ab` |
| `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs` | `6b54594399c7f508d2492912b4d0605fe75595c5f9474732e05265a93ff53c47` |
| `tests/WorkLog.Tests/Dashboard/ManualTimeProvider.cs` | `80f730379a27edfd7cc8d19d6d15123b6a0f52dac9746e89859440bf3b6b5c45` |

- Before/after identity: both runs match `head=0d77685 baseline=0d77685 files=10 diff=3c05ca98d991 untracked=6/fe28508b72b3`; no scoped mutation detected.
- Prior reports: [analysis 002](analysis-002.md) covers foundation tooling only. No prior runtime finding is being rechecked here. IDs allocated for this review are ANALYSIS-040 through ANALYSIS-079.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Target stability | Independent identity command before and after inspection | Complete scoped manifest and hashes above | Passed; frontend/harness changes are outside this identity |
| Watch-root containment and lifetime | Trace `RebindWatchersAsync`, `SourcePath` canonical/containment helpers and `FileSystemDashboardChangeSource` | ANALYSIS-040; nonrecursive watchers are capped at 512 and disposed/rebound, but initial traversal roots bypass containment | Failed containment review; no executable link probe was run |
| Cached document revalidation | Compare public read resolution with the new cache revalidation path | ANALYSIS-041; `Resolve` rechecks containment, while `RevalidateSuspect(null)` calls `Read` on old cache keys | Failed containment review; no outside fixture file was opened by this reviewer |
| Coordination and unavailable domains | Trace task construction, synchronous marker calls, awaited catalog batches and checkpoint loop | ANALYSIS-042 | Failed isolation review; the timing consequence is a source-derived hypothesis pending a bounded fixture measurement |
| Replacement identity and derived catalog | Trace journal lifecycle marker into `SourceCatalog.repositorySnapshot` / `repositoryNames` | ANALYSIS-043 | Failed replacement review; existing same-marker test replaces payloads under the same repository name and misses this case |
| Revision protocol | Inspect instance/vector construction, domain counters, dirty state and full checkpoints | Complete camelCase host serialization contract uses instance and five numeric domain generations; notification payload contains no document body or source paths | Passed source inspection; end-to-end ordering and client application are outside this target |
| Batching and bounds | Inspect semaphore, pending batch, hint cap, overflow promotion and manual-clock cases | 50ms trailing debounce, 250ms batch-age limit, 128-hint cap, one pending channel element per connection | Passed bounded data-structure inspection; new exact-clock cases were not executed here |
| Storage access | Inspect `DashboardJournal`, new digest marker query, and lock handling | Read-only SQLite connections, no detector migration/creation path, Busy/Locked retained dirty; persistent missing/schema states have markers | Passed source inspection for explicit writes and pending lock behavior; real WAL read-noise suppression remains unverified |
| Local transport boundary | Inspect middleware, endpoint options, hub `Snapshot`, send loop and revocation loop | Expected Host/Origin/fetch metadata and cookie checks precede `/api/live`; only WebSockets/SSE enabled; `Snapshot` has no client-selected root; pre-send authorization and two-second idle revalidation | Passed source inspection; actual transport execution, revocation latency and offline packaging were not independently rerun |
| Duplication / complexity / architecture | Compare responsibilities with architecture and controlling design | Detector, delivery registry and existing authoritative HTTP services are separated; snapshot invalidation preserves the expensive command cache | No style or speculative refactor finding; concrete boundary/isolation defects are listed below |
| Committable report hygiene | Read own report and scan change-root Markdown for configured secret/personal-data/residue patterns, without quoting values | Repository-relative references and synthetic fixture descriptions only | Passed scoped report/change-artifact check; this does not replace QA's final full-target hygiene gate |
| Execution | No builds, test runs, servers or native clients started by this reviewer | Developer reported a warning-free Release build; earlier 23/23 precedes the last lock/timing changes | Not performed independently. New lock and exact-clock cases remain unexecuted; no approval or escalation request was issued |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | `PublishCheckpointsAsync`, catalog reconciliation, watcher rebinding, connection revocation | Source observation | Every five seconds the host publishes a complete vector and queues an all-domain reconciliation. A completed idle pass invalidates and rebuilds the complete catalog, enumerates source/document metadata, queries journal markers and digest save markers, stats the read index, reloads settings and rebuilds the directory/watch-root projection. Catalog discovery additionally calls its existing journal snapshot/repository logic and `DigestStore.Revision`. The two-second revocation loop reads the authorization secret once per connection; sends also revalidate it. No observation payload decode or intentional idle document-body hashing occurs. Approximately 60 reconciliation opportunities and 150 revocation opportunities occur in five minutes, subject to blocking work; actual counts and total CPU were not measured here |
| Cache lifetime versus interval and duration | Complete catalog and expensive command tiers | Source observation | Complete catalog cache lifetime is two seconds, shorter than the five-second reconcile cadence, and each pass invalidates it explicitly. It therefore avoids adjacent HTTP duplication but does not eliminate the server's periodic full metadata projection. Git/OpenSpec caches retain 60-second lifetimes and task/config input stamps; timed-out commands retain a 30-second backoff. Markdown-only edits do not blanket-force those command caches. An in-flight discovery still holds the catalog gate for all callers |
| Unbounded state | Hint set, channels, watcher/root maps, document and command caches | Source observation | Hint set is capped at 128 and overflow drops optional paths in favor of a domain check; wake semaphore is capped at one; delivery channel is capped at one per connection, with a two-second send deadline. Actual watchers cap at 512; cached document entries cap at 256. Logical roots and recursive directory traversal are constructed before the watcher cap and scale with discovered directories. The preexisting command-cache dictionary is not pruned when sources/contexts disappear; repeated configuration churn can grow it. No measured long-lived growth claim is made |
| Repeated full reads where a change check would do | Metadata projection, suspect documents, storage markers | Source observation | Idle reconciliation traverses catalog and watcher metadata but uses rowid/max-save markers rather than decoding the journal or digest rows. Suspect cached documents are reread and hashed on hints, with full cached-body revalidation on overflow/directory hints. ANALYSIS-041 concerns the containment of those rereads, not their cache bound. DB and WAL file signatures are part of markers; SHM is explicitly ignored, but this reviewer did not establish whether read-generated WAL lifecycle noise is suppressed in an actual watcher run |
| External tool discovery and timeouts | Existing `LocalCommand` and source cache | Source observation | Git and node use process executable resolution. On Windows, OpenSpec is located by scanning PATH entries for its npm `node_modules/@fission-ai/openspec/bin/openspec.js`, then launched through node. Each command has a 15-second cancellation deadline and process-tree cleanup; independent command work has parallelism four. Alternative package-manager layouts were not executed. ANALYSIS-042 explains why bounded individual commands can still delay unrelated batch work |

## Findings

### ANALYSIS-040: Initial watcher traversal can escape the approved source through an OpenSpec directory link

- Severity: **critical**.
- Related tasks / requirements: 2.1 and 3.2; local data access boundary and design decision 2 prohibit watchers and recursive traversal outside approved sources through filesystem links.
- Locations at the reviewed identity: `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs:586` through `:607`; `src/WorkLog/Dashboard/SourcePath.cs:64` through `:71` explains the `Contained` helper's assumption about its parent.
- Observation: the coordinator forms `changesPath` with `Path.Combine`, canonicalizes it and immediately adds/pushes that canonical directory without proving it is inside `canonicalRoot`. A linked `openspec` or `changes` directory can therefore resolve outside the source. The later per-entry `SourcePath.Contained(canonicalRoot, item)` does not repair this: ordinary non-link children are returned unchanged because that helper assumes its starting parent was already canonical and contained. The external tree can consequently be enumerated and watched recursively. The catalog's separate `SourcePath.Within` rejection adds a notice but does not remove the source object from `snapshot.Sources`, so the watcher path is still reached.
- Bounded reproduction: in a disposable fixture, make a configured source's `openspec/changes` a directory link to a sibling fixture tree containing nested directories. Start the coordinator with a recording change source, or invoke its binding path after discovery. Expected: no root or enumeration below that outside tree. Actual source path: `SourcePath.Canonical(changesPath)` enters that tree, `Add` accepts it, and non-link descendants are traversed. Do not use real repositories or files.
- Proposed improvement and benefit: resolve the initial OpenSpec/changes path and every fallback root through the same live containment policy used by catalog/document resolution before enumeration or binding. Reject escaped roots and retain a contained recovery watch or bounded discovery fallback. Keep approved standalone-store roots and contained links supported. This closes a new background-access path that bypasses the public API boundary.
- Behavior-preservation risks: missing roots, linked but contained directories, archive moves, legitimate standalone stores and root-recovery watches must retain their expected behavior. The contained identity must be rechecked when a directory is rebound, not assumed from an earlier snapshot.
- Evidence type: source observation; no executable filesystem-link probe was run.
- Verification plan: disposable link cases for both `openspec` and `changes`, plus replacement after an initial successful bind. Assert no external enumeration/subscription and no external descendant hints. Recheck contained links, missing-root recovery and disposal independently.
- Relationship: new finding; no foundation finding covers runtime watch containment.

### ANALYSIS-041: Full cached-document revalidation bypasses live source containment

- Severity: **critical**.
- Related tasks / requirements: 2.1 and 3.2; local data access boundary; reader reconciliation must not read outside its source through a replaced link.
- Locations: `src/WorkLog/Dashboard/DocumentService.cs:123` through `:139`, especially `Read(path, fresh: true)` at `:135`; `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs:492` through `:495` selects all cached entries after overflow/directory hints.
- Observation: normal document reads call `Resolve`, which rechecks the live source/change path, before `Read`. The new `RevalidateSuspect` path instead rereads the old physical cache key directly. If a previously cached file or one of its parent directories is replaced by a link to an external fixture target, a domain-wide hint causes `File.ReadAllBytes` on that old key without containment resolution. The comment about catching a no-longer-contained file does not supply the missing check: `Read` validates size and metadata, not containment. Scoped file hints use `SourcePath.Within` earlier, but the null/all-cache overflow path bypasses that protection.
- Bounded reproduction: cache a contained fixture Markdown file; replace it or its parent with a link to a sibling fixture target; emit an overflow or directory-change document hint. Expected: evict/reject that cached target without reading the external file. Actual source path: all cache keys are selected and the old path is passed directly to `File.ReadAllBytes`. A subsequent public API containment rejection does not undo the background read.
- Proposed improvement and benefit: retain enough approved source/document identity with cache entries to re-resolve and recheck live containment before suspect-body reads, or evict suspect entries and let the existing authoritative resolution path reread them. Preserve same-metadata edit detection without granting old cache keys lasting access rights.
- Behavior-preservation risks: same-metadata edits, source-qualified copies, linked contained files, archive relocation, removed documents, encoding/size checks and concurrent reads. Rejecting a replaced path must not repopulate it from an unchecked read.
- Evidence type: source observation; no external target was opened by this reviewer.
- Verification plan: cache then link-swap the file and a parent directory under a disposable source; trigger overflow and directory lifecycle hints. Assert no external body read/cache population, safe eviction/unavailable behavior, and unaffected same-metadata contained refresh.
- Relationship: separate entry point from ANALYSIS-040; both should use one consistent containment policy but neither fix alone closes the other.

### ANALYSIS-042: One awaited batch still blocks reconciliation of later independent-domain changes

- Severity: **warning**.
- Related tasks / requirements: 2.2–2.4; unavailable sources must not block unrelated readable state, bounded five-second reconciliation, and independent journal/digest freshness.
- Locations: `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs:142` through `:150`, `:338` through `:345`, `:378` through `:411`; neighboring `SourceCatalog.cs:108` through `:120` and `DashboardJournal.cs:16`.
- Observation: the sole hint worker awaits the entire current batch before taking the next one. A catalog command that is already awaiting its 15-second timeout therefore holds all subsequently queued journal/digest/read batches behind it. Separately, `ReconcileJournalAsync` and `ReconcileDigestsAsync` perform synchronous SQLite queries before their only completed-task await, so adding them to a task list does not start those probes concurrently: a locked journal can consume its five-second busy wait before the digest or read-state probe even starts. Initial catalog discovery also synchronously reads that journal. The checkpoint timer queues work and publishes the existing vector; it does not independently recompute the stalled domain. Publishing an immediate dirty hint cannot replace the required authoritative revision reconciliation.
- Bounded reproduction: after initial quiescence, deliver a catalog hint and wait until a fake context command is blocked. Only then commit and hint a journal/digest change. Observe that its domain generation cannot advance while the worker is awaiting the earlier batch. Also hold only the journal under an exclusive fixture lock and change the digest/read index; its marker probe starts after the synchronous journal wait. The existing slow-source test emits both hints before the blocked batch starts, so it does not cover the later-arrival case; it checks the in-memory revision, not independent actual delivery.
- Proposed improvement and benefit: keep one in-flight reconciliation plus one coalesced follow-up per independent domain/resource, and publish completed domain generations without awaiting unrelated catalog work. Run unavoidable synchronous storage probes behind an actually asynchronous, bounded execution boundary with cancellation/timeouts. Preserve single-flight catalog ownership and dirty state on failures. This implements the planned isolation rather than adding a second polling owner.
- Behavior-preservation risks: cross-domain catalog metadata consistency, cancellation/shutdown, revision ordering, no duplicate work, bounded concurrency and SQLite connection ownership.
- Evidence type: source observation. Actual additional delay is a performance/correctness hypothesis derived from control flow; no latency benchmark was run here.
- Verification plan: controlled slow catalog with journal/digest writes arriving after the command is blocked, plus a journal-only lock while digest/read state changes. Check generations and actual delivered vectors before releasing the unrelated blocker, retained dirty work and bounded cleanup. Measure the same cases separately from healthy-workload p95 evidence.
- Relationship: new runtime isolation finding; foundation cleanup findings do not cover it.

### ANALYSIS-043: Same-marker journal replacement leaves repository discovery cached indefinitely

- Severity: **warning**.
- Related tasks / requirements: 2.2 and 2.3; same-marker replacement and independent repository/catalog updates.
- Locations: `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs:432` through `:441` and `:378` through `:388`; `src/WorkLog/Dashboard/SourceCatalog.cs:108` through `:118`.
- Observation: the detector's file-lifecycle generation correctly makes a replaced journal a new journal revision even if its maximum rowid stays equal. That generation is not carried into `SourceCatalog`, whose cached `repositoryNames` is refreshed only when `journal.Snapshot()` differs from `repositorySnapshot`. Replacing a journal with the same maximum rowid but different repository identities therefore leaves the previous journal-only repository list in catalog responses, including after periodic full catalog discovery. The equal numeric rowid makes that cache permanent until another append changes it.
- Bounded reproduction: seed a disposable journal with one observation for synthetic repository A; build the catalog; atomically replace it with another one-observation journal for synthetic repository B and emit the lifecycle hint. Expected: catalog repository identities and source notices reflect B and remove journal-only A. Actual source path: journal revision advances, but catalog continues using the cached A list because both maximum rowids are one. The current same-marker replacement test uses the same repository name in both databases, so it misses this derived cache.
- Proposed improvement and benefit: invalidate/rekey the journal-derived repository cache using the same validated lifecycle/availability identity that drives the live journal revision, or explicitly reset it when the detector confirms replacement. Keep this separate from expensive Git/OpenSpec cache invalidation.
- Behavior-preservation risks: configured repositories must remain listed even without journal rows; a transient unavailable journal must retain the chosen truthful fallback; avoid requerying distinct repository identities on every unchanged marker read.
- Evidence type: source observation; no runtime replacement probe executed here.
- Verification plan: same-rowid replacement with different synthetic repository identities, then unavailable/recovered replacement. Check catalog generation, repository filter choices and scoped notices without requiring an appended observation.
- Relationship: new runtime derived-cache finding, separate from the detector's already implemented same-marker generation.

## Re-verification

No earlier finding was assigned for re-verification. Foundation findings remain historical; this report does not reopen or close them.

## Assessment and limitations

The backend has clear ownership for detection, delivery and authoritative HTTP reads, bounded hint and notification queues, explicit domain revisions, authenticated transports and retained transient-lock work. The review found two concrete background containment bypasses and two independent-domain/replacement issues. The orchestrator owns their blocking dispositions and fix assignments.

This review establishes source-level findings on the exact frozen backend identity. It does not establish final correctness, healthy-workload latency, lower CPU, zero idle browser reads, packaged/offline behavior, native-client parity, or the unexecuted lock/clock cases. No servers, test runners, builds, installations or authenticated agents were started, and no approval prompts were issued. Only this reserved report was written; implementation, task state, ledger and prior reports were untouched.
