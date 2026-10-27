<!-- Saved by the orchestrator (workflow fallback). Reviewer: code-analyzer subagent, assignment A3. Client: Claude Code 2.1.311, delegated subagent (non-interactive). Model: claude-opus-5-5. Case: the client refused the reviewer's write; exact refusal message: "Subagents should return findings as text, not write report files. Include this content in your final response instead." The text below is the reviewer's returned report, verbatim. -->

# Analysis report: add-review-inbox / analysis-001

## Review target

- Reviewer / role: code-analyzer subagent (assignment A3) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.311, delegated subagent (non-interactive), model `claude-opus-5-5`
- Date: 2026-10-27, 19:04–19:16 UTC
- Change root / assignment: `openspec/changes/add-review-inbox`, assignment A3 (review analysis)
- Task and requirement references:
  - Tasks 1.1–4.3 ([tasks.md](../tasks.md)).
  - [design.md](../design.md) Decisions 1–6.
  - [document-feedback spec](../specs/document-feedback/spec.md): "Open review comments overview".
  - [openspec-browser spec](../specs/openspec-browser/spec.md): "Changes since last read".
  - Developer deviations 1–9 in [findings.md](findings.md#scope-decisions).
- Scope and relevant neighboring paths:
  - `src/WorkLog/Dashboard/{ReadBaselineStore,ReadBaselineService,DocumentService.Reviews,DocumentService,DashboardHost,SourceCatalog,ReviewMarker,SourcePath,DashboardState}.cs`
  - `dashboard/src/{app.ts,app.html,diff.ts,reviews.ts,markdown.ts,models.ts}`
  - `tests/WorkLog.Tests/Dashboard/ReviewOverviewTests.cs` (marker-cache tests only)
  - `docs/dashboard-validation.md` (added section)
  - `docs/architecture.md`
- Reading limits and paths read outside scope:
  - No limits beyond the assignment's scope.
  - Read outside the scoped paths:
    - `src/WorkLog/Infrastructure/AtomicFile.cs`
    - `src/WorkLog/Infrastructure/ContentHash.cs`
    - `dashboard/e2e/server.mjs` (fixture start-up only)
    - excerpts of `tools/WorkLog.Smoke/Program.cs` and `tools/WorkLog.Smoke/DashboardFixture.cs` (fixture entry point)
    - `.gitignore`
  - I read all of [findings.md](findings.md), not only "Scope decisions".
- Baseline commit: `b53c88c`
- Implementation identity before the analysis:
  - Command: `./scripts/review-identity.ps1 -Baseline b53c88c -Paths README.md dashboard/e2e/dashboard.spec.ts dashboard/package-lock.json dashboard/package.json dashboard/src docs/dashboard-validation.md docs/installation.md src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard` (pwsh 7.6.6).
  - HEAD `b53c88c3111d004083c4561492bf4ed1a8bb705a`; 16 modified files and 11 untracked files.
  - Tracked diff SHA-256 `19f5e4ad5594d9b1f23f068e3af1b3b70253aa301e956decc7e02b0c83b37865`.
  - Summary: `head=b53c88c baseline=b53c88c files=27 diff=19f5e4ad5594 untracked=11/a7b097ece98f`.
- Before/after identity:
  - After the analysis, the same command printed `head=b53c88c baseline=b53c88c files=27 diff=19f5e4ad5594 untracked=11/a7b097ece98f`, which matches.
  - Several scoped source files (`ReadBaselineStore.cs`, `DocumentService.Reviews.cs`) and the Release `WorkLog.dll` had last-write times about one minute before my first identity run, presumably from concurrent QA activity. Their contents match the assigned identity, so the target was not mutated.
- Prior reports / rechecked findings: none (first analysis round)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Performance (server listing) | HTTP probe: Release `worklog.exe` copied to `artifacts/analysis-001-probe/bin`; smoke dashboard fixture plus generated archived changes; dashboard on free ports 47963/47964 with `--no-open` and the fixture data directory; 6 timed `POST /api/reviews` per scope. Script: `artifacts/analysis-001-probe/probe.ps1` (ignored, disposable) | 152 docs: `all` 106–158 ms, `active` 19–26 ms. 512 docs: `all` cold 864 ms, warm 365–493 ms. 935 docs: `all` cold 1360 ms, warm 871–1292 ms; `active` 19–25 ms. 301 items = 109,841 bytes of JSON | Measured. ANALYSIS-001 and ANALYSIS-002. The host was shared with the concurrent QA run, so timings are noisy. |
| Performance (server cache) | Source inspection of `Markers`, `Read`, and the marker-cache tests | `Order` is set only on insert in both caches. `MarkerCacheStaysBoundedAndEvictsOldestFirstAcrossManyListings` checks size but not parse count | Source observation, supported by the 935-doc timings. ANALYSIS-002. |
| Performance (client render/diff) | Node 24 + jsdom probe: `dashboard/src/markdown.ts` and `diff.ts` bundled with the repo's esbuild (`artifacts/analysis-001-probe/render.mjs`, `parse.mjs`) | 800 KiB: `renderMarkdown` 0.6–1.2 s; `parseReviews(baseline)` 0.6–1.2 s; markdown-it parse alone 50–76 ms; render alone 2–7 ms. `readMarks`: 50 KiB full rewrite 64 ms; 200 KiB 797 ms; 400/800 KiB heavy rewrites 3.0 s, then `incomplete` | Measured in jsdom, not a browser. Browser DOMPurify cost is a hypothesis. ANALYSIS-004, ANALYSIS-005. |
| Performance (mark walk) | Same probe with 1000–2667 changed ranges and removed hunks | `renderMarkdown(current, marks)` 1.5–2.5 s vs 0.6–1.6 s without marks; noise is too high to isolate the walk | `applyMarks` is O(tokens × ranges) through `intersects`. `insertRemoved` is O(hunks × tokens) through `findIndex`. The extra cost stays below one render at 2.7k hunks. No finding. |
| Polling / idle | Source inspection of `poll`, `init`, `scopeChanged`, `saveComment`, `confirmDelete`; developer's 60 s measurement in `docs/dashboard-validation.md` | `/api/reviews` and `/api/reads/summary` run only at start, on a catalog-revision change, on a repository-filter change, and after own comment writes | Matches the design. No idle regression (source observation; I did not repeat the idle measurement). The double fetch and missing single-flight are ANALYSIS-008. |
| Store bounds / atomicity | Source inspection of `ReadBaselineStore` and `AtomicFile` | Bounds: 300 keys, 64 MiB, 4 MiB index. The blob is written before the index. Writes use a temp file plus replace with `flushToDisk`. `AtomicFile` writes UTF-8 without BOM, which matches the blob hash | Correct by inspection. Error classification is ANALYSIS-006. |
| Lock scope | Source inspection | A static `Gate` serializes every store instance in the process. That covers blob hashing, per-mark stats of up to 300 blobs, directory enumeration, and fsync | Acceptable. The dashboard lease guarantees one host per data directory, and the operations are user-paced. Only parallel in-process tests over different directories serialize needlessly. No finding. |
| Blob validation cost | Source inspection | `Get` does one SHA-256 of up to 2 MiB per document open (about 1–5 ms). `Mark` re-reads and re-hashes an existing blob just to test that it exists | Acceptable. The rehash also repairs a damaged blob. No finding. |
| Duplication (server) | Source comparison of `SearchAsync` and `ReviewsAsync` | The shared `Unavailable` helper was extracted. Scope validation, 3 copies of `InScope`, the change filter, and the per-document Resolve with identical catch remain duplicated | ANALYSIS-003 |
| Duplication (client marker rule) | Source comparison | `removedReviews` reuses the reader's `worklog_review` rule through `parseReviews`, as the design requires, but runs a full render and sanitize to do it | Rule reuse is correct; the cost is ANALYSIS-004. Separate server and client marker parsers predate this change, and no third parser was added. |
| Duplication (cross-tier identity) | Source comparison of `reviews.ts:79`, `DocumentService.Reviews.cs:86`, `app.ts:898,935` | The baseline key format and logical-name resolution are re-derived on the client | ANALYSIS-007 |
| Architecture | Comparison with `docs/architecture.md` | Dashboard-area responsibilities hold. Infrastructure `AtomicFile`/`Json` are reused. Endpoints sit behind the existing `/api/` authorization middleware and exception-to-status mapping. The architecture text does not mention the read store or the review listing | ANALYSIS-009 (documentation only) |
| Endpoint wiring / authorization | Source inspection of `DashboardHost.Create` | All 4 endpoints are under `/api/` with the Host/Origin/Sec-Fetch-Site and cookie checks. `GET /api/reads/summary` is read-only. Conflicts map to 409; an invalid scope (`ArgumentException`) maps to 400 | No finding |
| Formatting | `dotnet format --verify-no-changes` | — | Not run. Style is outside the role's reporting unless it causes a defect. |
| Functional suites | `dotnet test`, `npm test`, e2e | — | Not run. These are QA (A2) checks, and running them would contend for the build output. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | 2 s client poll (`app.ts` `poll`) | Source observation; developer measurement in `docs/dashboard-validation.md` (not repeated) | **Idle (catalog revision unchanged):** 1 `GET /api/catalog` per 2 s, plus the existing `POST /api/document` with `knownRevision` while a document is open. No `/api/reviews`, no `/api/reads/*`, no process spawns, no index reads. **Per catalog-revision change** (any Markdown edit in any change, active or archived): 1 `/api/reviews` (scope `all`) and 1 `/api/reads/summary`. While an agent edits, this can happen every poll. Measured `/api/reviews` server time: about 0.1 s at 152, 0.4 s at 512, and 0.9–1.3 s at 935 in-scope documents (ANALYSIS-001). |
| Cache lifetime versus interval and refresh duration | Marker cache (512, length+mtime); document cache (256); catalog cache (2 s); client `marksMemo` (1 pair) | Source observation + measured | The marker cache never expires, but it only helps up to 512 documents. Above that, FIFO eviction under a cyclic scan makes every lookup miss (ANALYSIS-002). Even with hits, the per-document canonical path walk keeps warm cost at about 0.8 ms per document (measured), so the cache saves only about half of the cold cost. `marksMemo` holds the diff once per (baseline, revision) pair, as designed. `removedReviews` is not memoized and repeats a full baseline render on every render with marks (ANALYSIS-004). |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `dashboard-reads/` index and blobs; marker cache; document cache; `ReviewList` | Source observation + measured response size | **Store:** bounded at 300 keys, 64 MiB of blobs, and a 4 MiB index cap, with LRU eviction and unreferenced-blob deletion on each mark. **Marker cache:** 512 entries. **Document cache:** 256 entries (existing bound; the listing now fills it with archived texts on misses). **`ReviewList`:** no count cap (deviation 7). Each item is bounded (quote 280 and comment 500 characters; measured about 365 B per item), but the list grows with archived leftover markers, which accumulate over time. |
| Repeated full reads where a change check would do | `POST /api/reviews` | Source observation + measured | Every call stats and canonical-resolves every in-scope document, about 4 path walks per document. The catalog already holds a fresh per-document fingerprint (`ChangeDocument.Fingerprint`) that would be enough to answer from cache (ANALYSIS-001). `reads/summary` and `reads/get` each deserialize the index (at most 300 entries) per call, which is acceptable. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | None added | Source observation | The change adds no process spawns or executable lookups. `diff` 9.0.0 is bundled into the dashboard assets. The client diff has a 3 s time budget on the main thread (ANALYSIS-005). |

## Findings

### ANALYSIS-001: The review listing re-resolves every document on each catalog change, and scope "all" makes its cost grow with archive history

- Severity: warning
- Related task / requirement: 1.1, 1.2, 3.1; Decision 1; deviation 2; "The overview SHALL refresh when document contents change"
- Locations (at `b53c88c` plus the worktree identity above):
  - `src/WorkLog/Dashboard/DocumentService.Reviews.cs:37-56`: per-document `Resolve`, then `Markers`.
  - `src/WorkLog/Dashboard/DocumentService.cs:32-50`: `Resolve` calls `SourcePath.Within` twice, and each call canonicalizes root and candidate component by component.
  - `dashboard/src/app.ts:400-410`: `scope: "all"`.
  - `dashboard/src/app.ts:1494-1497`: refresh on every revision change.
- Observation:
  - Measured on the isolated fixture (warm calls, scope `all`): 152 docs 106–141 ms; 512 docs 365–493 ms; 935 docs 871–1292 ms. Scope `active`: 19–30 ms in every case.
  - At 512 documents every lookup is a marker-cache hit, yet the warm cost is still about 0.8 ms per document. So canonical path resolution plus the stat dominates, not parsing.
  - `Resolve` repeats `Canonical(source.Root)` and `Canonical(change.Directory)` for every document of the same change.
  - The client always requests `all` (deviation 2) and refreshes on every catalog revision. So an agent editing any active document triggers a full walk of every archived document, as often as every 2 s.
  - For comparison, `docs/dashboard-validation.md` records a warm catalog refresh of 17–19 ms for 1,280 files. One review refresh costs about 50 times that.
- Proposed improvement and benefit, in order of value:
  1. Key the parsed-marker cache by catalog identity (source, change directory, path) plus the catalog's `ChangeDocument.Fingerprint`. On a hit, return the cached markers with no `Resolve` and no stat; resolve and read only on a miss. The catalog refreshed that fingerprint at most 2 s earlier, and the overview already refreshes only when the catalog revision (built from the same stamps) moves, so freshness semantics stay the same.
  2. On a miss, resolve each change directory once and use the existing `SourcePath.Within(root, canonicalRoot, relative)` overload for its documents.
  3. Optionally memoize the whole `ReviewList` per (catalog revision, repositories, scope). That makes repeated requests free, including the double fetch in ANALYSIS-008.

  With option 1, the warm cost should drop to dictionary lookups (hypothesis), which keeps deviation 2 affordable.
- Behavior-preservation risks:
  - Containment is still enforced before any read. A cache hit would rely on the catalog's discovery-time containment instead of a per-request recheck. That is acceptable, because a hit returns already-parsed quotes and reads no file.
  - Unreadable documents must still be reported as unavailable. The catalog already omits or flags them, but check that a cached entry for a document that has disappeared is not returned.
  - Truncation and `unavailable` scoping are unaffected.
- Evidence type: measured latency, plus source observation for attributing it to the canonical walk. The improvement is a hypothesis.
- Verification / measurement plan:
  - Rerun `artifacts/analysis-001-probe/probe.ps1` (935 docs, warm `all`) before and after; target under about 50 ms.
  - Add a .NET test that a second listing does zero file-system resolutions (a counter on the miss path).
  - Add a .NET test that a document deleted between listings is reported unavailable or absent, not served from cache.
- Relationship: The opinion on deviation 2 depends on this finding. Related to ANALYSIS-002 and ANALYSIS-008.

### ANALYSIS-002: The marker cache is FIFO, so listings over more than 512 documents miss every time and churn the reader's document cache

- Severity: warning
- Related task / requirement: 1.2 ("a second listing over unchanged files does not reparse"); Decision 1 (512 entries, oldest-first eviction)
- Locations:
  - `src/WorkLog/Dashboard/DocumentService.Reviews.cs:60-72`: `Order` is assigned only on insert; a hit does not refresh it.
  - `src/WorkLog/Dashboard/DocumentService.cs:59-60,71-73`: the document cache uses the same policy, with 256 entries.
  - `tests/WorkLog.Tests/Dashboard/ReviewOverviewTests.cs:133-147`: asserts the cache size over 3 rounds of 520 documents, not the parse count.
- Observation:
  - The listing visits documents in the same order every time. With more than 512 documents, FIFO eviction under that cyclic scan removes each entry just before it is needed, so every lookup misses (the classic cyclic-scan pathology).
  - The existing 520-document test would show `MarkerParses` reaching 520 × 3.
  - Each miss calls `Read`: it reads the bytes, computes SHA-256, decodes, and inserts into the 256-entry document cache, evicting the documents the reader uses.
  - Timings agree: warm per-document cost rises from about 0.8 ms (512 docs, all hits) to about 0.9–1.4 ms (935 docs).
- Proposed improvement and benefit:
  - Tie the bound to the catalog instead of a fixed constant. The catalog already bounds the document count, so after each listing, remove entries whose path is not in the current snapshot instead of evicting by age. The cache is then bounded by catalog size and cannot thrash.
  - If a fixed cap has to stay, refresh `Order` on hits (true LRU). Also skip inserts when the in-scope set exceeds the cap, so that at least the first 512 entries stay warm.
  - Parse misses from a read that does not populate the document cache, or reuse a cached text when one exists. Archived listings then stop evicting reader entries.
- Behavior-preservation risks:
  - Memory: the marker cache stores raw marker text only, so tying it to catalog size costs about the total size of all markers, which is small.
  - The test `MarkerCacheStaysBoundedAndEvictsOldestFirstAcrossManyListings` encodes the current policy and would change.
  - The 512 figure in design Decision 1 would need an orchestrator decision.
- Evidence type: source observation, supported by measured latency
- Verification / measurement plan:
  - Extend the 520-document test to assert `MarkerParses` after round 2: 1040 today, 520 after the fix.
  - Assert that `HashReads` is unchanged by a warm listing.
  - Rerun the 935-doc probe.
- Relationship: Complements ANALYSIS-001. If ANALYSIS-001 option 1 is adopted, pruning to the catalog is the natural bound for that cache.

### ANALYSIS-003: Search and the review listing duplicate the scoped-document iteration

- Severity: suggestion
- Related task / requirement: 1.1
- Locations:
  - `src/WorkLog/Dashboard/DocumentService.cs:205-239` (`SearchAsync`)
  - `src/WorkLog/Dashboard/DocumentService.Reviews.cs:29-58` (`ReviewsAsync`)
  - `src/WorkLog/Dashboard/DocumentService.cs:192-203` (`Unavailable`, extracted by this change)
- Observation: The developer extracted the unavailable-notice rules, which is good. Both methods still duplicate:
  - scope validation and its message;
  - the change predicate `(scope == "all" || c.Archived == (scope == "archived")) && InScope(...)`;
  - a local `InScope` (now 3 copies);
  - a per-document `Resolve` with an identical catch that turns `IOException`, `UnauthorizedAccessException`, and `ArgumentException` into `change/path: message`.
- Proposed improvement and benefit: Add one private iterator, for example `ScopedDocuments(snapshot, scope, repositories, unavailable)`, that yields `(change, document, file)`, and use it in both methods. The scope and unavailable rules then live in one place, and ANALYSIS-001's per-change resolution has a single home, so search benefits too.
- Behavior-preservation risks: Search returns early at 50 matches, so the iterator must be lazy. Keep the exact unavailable message text; tests and the client display it.
- Evidence type: source observation
- Verification / measurement plan: The existing search and review-listing .NET tests pass unchanged.
- Relationship: Enables ANALYSIS-001 option 2.

### ANALYSIS-004: "Removed since last read" renders and sanitizes the whole baseline on every render with marks

- Severity: suggestion
- Related task / requirement: 3.5; Decision 4 ("parse markers from the baseline text with the client renderer's marker rule")
- Locations:
  - `dashboard/src/markdown.ts:23-25`: `parseReviews` is `renderMarkdown(text).reviews`.
  - `dashboard/src/reviews.ts:97-101`: `removedReviews`.
  - `dashboard/src/app.ts:876-885`: `renderDocument` calls it on every render that has marks, with no memo (unlike `marksFor` at 887-893).
- Observation:
  - To get `Review` objects, which the renderer rule creates as a side effect, `parseReviews` runs the whole pipeline: markdown-it parse, render, the attribute walk, and DOMPurify sanitize.
  - Measured `parseReviews` time (Node + jsdom, bundled module): 40–51 ms at 50 KiB; 283–307 ms at 200 KiB; 0.6–1.2 s at 800 KiB.
  - On the same 800 KiB text, markdown-it parse alone takes 50–76 ms and render 2–7 ms. So sanitize and the DOM work make up most of the cost, at least in jsdom.
  - Each time the open document's revision changes while it differs from the baseline, the reader pays two full renders: one of the current text and one of the unchanged baseline.
- Proposed improvement and benefit:
  - Build each `Review` from a `worklog_review` token in one shared helper, used by both the renderer rule and `parseReviews`.
  - Make `parseReviews` call only `md.parse` (with the same `worklog_review` block rule) and collect the tokens.
  - Memoize the baseline's reviews per `baseline.revision`, since they do not depend on the current text.
  - The design still has a single client marker rule, and one full render plus sanitize per revision change goes away. Expected saving: most of the measured `parseReviews` time. That is a hypothesis for browsers, where DOMPurify uses the native DOM and is likely faster than in jsdom.
- Behavior-preservation risks: `Review.line`, `id`, `quote`, and `marker` must be identical; the existing `removedReviews` unit tests cover id and legacy exact-text matching. The renderer rule must keep pushing to `reviews` for the reader.
- Evidence type: measured result (jsdom); the browser magnitude is a performance hypothesis
- Verification / measurement plan:
  - Unit test that `parseReviews(text)` equals `renderMarkdown(text).reviews` for a fixture with structured, legacy, fenced, and list-nested markers.
  - Rerun `artifacts/analysis-001-probe/render.mjs`, or time a 500 KiB document in the browser's Performance panel.
- Relationship: none

### ANALYSIS-005: The 3 s diff budget runs synchronously on the UI thread, and a timeout shows no changes at all

- Severity: suggestion
- Related task / requirement: 3.3; Decision 4 (performance note); deviation 6
- Locations:
  - `dashboard/src/diff.ts:4,44`: `diffLines(..., { timeout: 3000 })`.
  - `dashboard/src/app.ts:887-893`: called synchronously inside `renderDocument`.
- Observation:
  - Measured in Node on the bundled `readMarks`: a full rewrite of a 50 KiB document took 64 ms, and of a 200 KiB document 797 ms. Heavy rewrites of 400 KiB and 800 KiB documents ran the full 3,005 ms and returned `incomplete` with no positions. The reader thread is blocked the whole time.
  - The result is memoized per pair, so the cost is once per agent revision. But an agent rewriting a large document produces a new pair at every save.
  - jsdiff 9's documented options (fetched with ctx7, `/kpdecker/jsdiff`) include `maxEditLength`, which gives up early once the edit distance exceeds a limit, and an async `callback` mode, documented as computing "in async mode to avoid blocking the event loop".
- Proposed improvement and benefit:
  - Add `maxEditLength`, sized to a few thousand line edits, so pathological diffs give up in milliseconds instead of seconds. Lower the time budget.
  - Optionally use `callback` mode, guarded by the document epoch.
  - When the diff is incomplete, mark the whole document as changed (one stop) instead of nothing. That is more truthful than "too many differences" with no highlight.
  - Opinion on deviation 6: degrading instead of failing is right, but a 3 s main-thread budget is too high.
- Behavior-preservation risks: Async mode makes `renderDocument` two-phase (render, then apply marks), which affects `syncStops` timing and the "nothing flashes" intent at `app.ts:817`. Synchronous `maxEditLength` with a lower timeout avoids that. `diff.timeout.spec.ts` needs updating.
- Evidence type: measured in Node on a shared machine. The UI freeze in the browser follows directly from the synchronous call.
- Verification / measurement plan:
  - Rerun `render.mjs` with `CASES='[[1000,1]]'` before and after; target well under 500 ms.
  - Unit test that an over-budget diff returns quickly with a whole-document mark.
- Relationship: none

### ANALYSIS-006: The baseline store treats transient I/O failures as corruption, and the next mark deletes every other baseline

- Severity: warning
- Related task / requirement: 2.1 (corrupt-index recovery, unreferenced-blob deletion); spec requirement that remembered content is bounded and removable
- Locations:
  - `src/WorkLog/Dashboard/ReadBaselineStore.cs:148-165`: `Load` returns an empty index on `IOException` and `UnauthorizedAccessException`, as well as on JSON, schema, or size problems.
  - `:131-143`: `ReadBlob` returns null on `IOException`.
  - `:45-51`: `Get` then removes the entry and saves.
  - `:66-80`: `Mark` saves the loaded index, then `DeleteUnreferenced` deletes every blob the index no longer names.
- Observation:
  - A momentary sharing violation on `index.json` (antivirus, a backup or sync client, the indexer) cannot be told apart from a damaged index.
  - The next `Mark` (any first open, Mark as read, or own comment write) then writes a one-entry index and deletes every other blob. All baselines are lost silently.
  - Similarly, a transient failure reading one blob in `Get` drops that key permanently.
  - An index with a foreign schema version, for example one written by a newer WorkLog after a rollback, is also overwritten, and its blobs are deleted.
  - The existing `DashboardState` follows the same empty-on-error pattern, but there the next write only replaces preferences and deletes nothing.
- Proposed improvement and benefit:
  - Reset only on positive evidence of damage: `JsonException`, oversized or invalid content, or a hash mismatch.
  - Let `IOException` and `UnauthorizedAccessException` propagate. The host middleware already maps them to 503, and the client's `loadBaseline` already treats a failure as "no highlights", so nothing is written or deleted.
  - For a foreign `schemaVersion`, leave the directory untouched and report baselines as unavailable instead of overwriting them.
- Behavior-preservation risks:
  - The corrupt, oversized, and foreign-version tests in `ReadBaselineTests.cs` encode today's reset behavior; the foreign-version behavior would change.
  - A persistent permission failure would show up as repeated 503 responses on `reads/*` instead of the store quietly working without memory. That is arguably more honest, but the client must not show an error notice on every open (today `loadBaseline` swallows the error).
- Evidence type: source observation. How often this happens in practice is a hypothesis; I did not reproduce it.
- Verification / measurement plan:
  - .NET test: hold `index.json` open with `FileShare.None`, call `Mark` for another key, and expect an `IOException` with the existing blobs and index unchanged after release.
  - Same test with a blob held open during `Get`: expect the entry to be kept.
- Relationship: none

### ANALYSIS-007: The client re-derives the server's baseline identity

- Severity: suggestion
- Related task / requirement: 3.4, 3.6; Decision 2 (key), Decision 5
- Locations:
  - `dashboard/src/reviews.ts:79-81` (`baselineKey`) duplicates `src/WorkLog/Dashboard/DocumentService.Reviews.cs:86-89` (the server key, which also normalizes `\`).
  - `dashboard/src/app.ts:898-904`: `logicalName` re-implements `SourceCatalog.Find` name resolution.
  - `dashboard/src/app.ts:935-943`: `looksUpdated`.
  - The change-lookup predicate `c.sourceId === … && (c.name === … || c.directoryName === …)` now appears 5 times in `app.ts` (lines 658, 723, 902, 939, 1510), 2 of them new, even though `selectedChange()` already holds the open document's change.
- Observation: The client has to reproduce the server key byte for byte, both to read the summary and to update it optimistically in `noteRead`. If either side diverges (a normalization change, or a `@source` rule), the "Updated since read" chips would silently stick or never show. No test pairs the two implementations.
- Proposed improvement and benefit:
  - Return the server-computed `key` in `ReadMarkResult`, so `noteRead` uses it directly and `logicalName` can be removed.
  - Resolve the open document's change through `selectedChange()` or one shared helper.
  - This removes about 15 lines and the cross-tier format coupling for writes. The summary lookup still depends on the format, and one paired test can pin it.
- Behavior-preservation risks: `/api/reads/mark` gains an additive field and stays compatible.
- Evidence type: source observation
- Verification / measurement plan: The existing chip-clearing unit tests (3.6) and the e2e Mark-as-read scenario pass.
- Relationship: Deviation 1 (fingerprint sent as a string) is unaffected.

### ANALYSIS-008: Overview refreshes are neither coalesced nor single-flight

- Severity: suggestion
- Related task / requirement: 3.1; Decision 1 ("requests it on load and when `catalog.revision` changes")
- Locations:
  - `dashboard/src/app.ts:1494-1497`: `void this.refreshOverview()` on a revision change.
  - `:1218` and `:1275`: `void this.loadReviews()` after own comment writes.
  - `:400-410`: the epoch only discards stale results.
- Observation:
  - An own comment write fetches `/api/reviews` explicitly, then again when the next poll sees the new catalog revision. That is two full listings per write.
  - Refreshes are started fire-and-forget from `poll`, outside its `polling` guard. When a listing takes longer than the 2 s interval, listings overlap and the server does work whose results the epoch then discards. A listing measured about 1.3 s here at 935 docs, and takes longer on slower disks or with more history.
- Proposed improvement and benefit:
  - Make `refreshOverview` single-flight, with one trailing rerun if the revision moved in the meantime.
  - Optionally drop the explicit post-write `loadReviews` and rely on the revision-driven refresh, which comes at most one poll later. Or keep it and skip the next revision-triggered listing.
  - If the server adopts ANALYSIS-001 option 3, the duplicate fetch becomes free and only the single-flight part still matters.
- Behavior-preservation risks: If the explicit call is dropped, counts after an own write can lag by up to one poll (2 s). The idle test in `app.reviews.spec.ts` still holds.
- Evidence type: source observation. The overlap is a hypothesis based on the measured listing latency.
- Verification / measurement plan: Unit test with a slow fake `/api/reviews` (3 s) and 2 revision changes: expect at most 2 calls, not 3 or more.
- Relationship: Client-side mitigation for ANALYSIS-001.

### ANALYSIS-009: The architecture reference does not describe the read-baseline store or the review listing

- Severity: suggestion
- Related task / requirement: 4.2
- Locations: `docs/architecture.md`: the Dashboard row ("…inline review comments, and private UI preferences") and the paragraph that names `dashboard-state.json` as the only dashboard-private file.
- Observation:
  - The implementation conforms to the architecture: it reuses Infrastructure's `AtomicFile`/`Json`, stays in the Dashboard area, and uses the same authorization.
  - The reference text, however, still describes private dashboard data as UI preferences only. It does not mention `dashboard-reads/` (bounded, resettable, content-addressed private copies of documents) or the server-side marker listing and its cache.
  - Task 4.2 updated only `docs/installation.md` and `README.md`.
- Proposed improvement and benefit: Add one sentence to the Dashboard paragraph covering read baselines under `dashboard-reads/` (bounded, independent of journal and config, removable) and the review-marker listing with its per-file parse cache. Future reviewers judging drift then have a reference.
- Behavior-preservation risks: none (documentation)
- Evidence type: source observation
- Verification / measurement plan: Read-through. Links in `docs/installation.md` already resolve per task 4.2.
- Relationship: none

## Design-level opinions on developer deviations

These opinions are judgments, not measurements, except where a number is cited.

- **Deviation 2 (client always fetches scope `all`).** Sound as a contract: one list feeds the panel, the cards, and the reader counts. It is affordable only once the server listing costs in proportion to changed documents (ANALYSIS-001 and ANALYSIS-002). Without that, at 935 documents it costs 40–60 times the `active` request (measured), on every agent edit. If the server fix is deferred, request `active` by default, and fetch archived items only when the archived or all scope is selected or an archived document is open.
- **Deviation 4 (store details).** I agree with all five parts:
  - Filtering the summary to current-catalog keys bounds the payload and avoids exposing stale keys.
  - Returning `null` when there is no baseline is the simplest contract.
  - The 60 s touch interval avoids an index rewrite and fsync on every open.
  - Strictly increasing `readAt` costs O(n) per write at n ≤ 300 and keeps the LRU order exact.
  - Checking blob names (regex) and SHA on read is cheap, one hash of up to 2 MiB per open, and is the right integrity guard. Keep it, together with ANALYSIS-006's error classification.
- **Deviation 5 (removed passages before the top-level block, shown both inline and in the panel, neighbouring changes grouped into one stop).** I agree. Placing them at the top-level block avoids splicing tokens inside lists and tables. The `findIndex` and `intersects` walks measured below the cost of one render at 2.7k hunks. Showing a removed marker both inline and in the panel is a presentation choice with no cost or consistency problem.
- **Deviation 7 (no item cap).** I agree, for now. Each item is bounded (about 365 B measured, under 1 KB worst case), and a cap would make the card and reader counts wrong. What actually grows is archived leftover markers under scope `all`, so control the cost through ANALYSIS-001 rather than a count cap. If a cap is ever needed, mirror search's `limitReached` and report counts separately.

## Re-verification

None (first round).

## Assessment and limitations

**Overall.** The implementation is cohesive and follows the architecture: it reuses Infrastructure, sits in the Dashboard area, and uses the existing authorization and error mapping. It meets the idle-cost goal: no new requests while the catalog revision is unchanged. The diff is computed once per (baseline, revision) pair, as designed.

**Main issues.**
- Cost (ANALYSIS-001, ANALYSIS-002): the cost problem is outside idle time. Every content change anywhere triggers a full review listing over active and archived documents. That measured about 1 s at 935 documents, and FIFO cache thrash makes it worse above 512 documents.
- Robustness (ANALYSIS-006): transient I/O errors in the baseline store lead to deletion of all baselines.
- The remaining findings are bounded simplifications and client-side savings for large documents.

**How the measurements were taken.**
- Server latencies come from an isolated Release dashboard: copied binaries, the smoke fixture plus generated archived changes, ports 47963 and 47964, and fixture data directories.
- Client timings come from Node 24 with jsdom running the bundled reader modules, not a browser.
- The host was shared with the concurrent QA run, so absolute numbers are noisy; the ratios are the evidence.

**Cleanup.** Both dashboards I started were stopped, and I verified that no process or listener remained. The probe binaries, fixtures, and stdout files (which held one-use bootstrap values) were deleted. Only the probe scripts remain, in the ignored `artifacts/analysis-001-probe/`.

**Not performed.** I did not repeat the idle 60 s measurement, did not run the functional suites (QA's scope), and did not run the formatter check.

**Writes.** There were no unexpected writes. The only repository writes are the ignored probe folder; this report could not be saved.

**Decisions for the orchestrator.**
- Whether to change Decision 1's 512-entry FIFO (ANALYSIS-002).
- Whether to accept deviation 2 on the condition that ANALYSIS-001 is fixed.
- Whether indexes with a foreign schema version should be preserved (ANALYSIS-006).
