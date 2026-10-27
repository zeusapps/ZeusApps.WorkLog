<!-- Saved by the orchestrator (workflow fallback). Reviewer: code-analyzer subagent, assignment A7. Client: Claude Code 2.1.311, delegated subagent (non-interactive). Model: claude-opus-5-5. Case: the reviewer made no write attempt because the client's own subagent instructions told it to return findings as text; no refusal message exists. The instruction the reviewer quoted: "Do NOT Write report/summary/findings/analysis .md files. Return findings directly as your final assistant message — the parent agent reads your text output, not files you create. (Files written as input to another tool are fine; this note is about report files.)" The text below is the reviewer's returned report, verbatim. -->

# Analysis report: add-review-inbox / analysis-002

## Review target

- **Reviewer / role:** code-analyzer subagent (assignment A7) / code-analyzer.
- **Client / version / mode / model:**
  - Claude Code, delegated subagent, non-interactive.
  - Version 2.1.311 as the orchestrator recorded it; I did not observe it myself.
  - Resolved model ID `claude-opus-5-5`.
- **Date:** 2026-10-27, about 19:45–20:10 UTC.
- **Change root / assignment:** `openspec/changes/add-review-inbox`, assignment A7 (recheck after fix rounds A4 and A5).
- **Task and requirement references:**
  - Tasks 1.1 and 1.2, and 2.1 and 2.2 ([tasks.md](../tasks.md)).
  - [design.md](../design.md) Decision 1 (updated: the cache is bounded by the catalog) and Decision 2.
  - The orchestrator's ANALYSIS-006 decision and the user's catalog-bound decision in [findings.md](findings.md).
- **Scope:**
  - `src/WorkLog/Dashboard/DocumentService.Reviews.cs`, `DocumentService.cs`, `ReadBaselineStore.cs`, `ReadBaselineService.cs`.
  - `dashboard/src/app.ts`.
  - `tests/WorkLog.Tests/Dashboard/ReviewOverviewTests.cs` and `ReadBaselineTests.cs`.
- **Paths read outside scope:**
  - `src/WorkLog/Dashboard/SourcePath.cs` (containment), `SourceCatalog.cs` (diff only), `DashboardHost.cs` (diff and error-to-status mapping).
  - `src/WorkLog/Infrastructure/Json.cs`, `AtomicFile.cs`.
  - `dashboard/src/api.ts` (status handling, via grep).
  - `dashboard/e2e/server.mjs` and `dashboard/e2e/dashboard.spec.ts` (fixture start-up; grep only).
  - `artifacts/analysis-001-probe/probe.ps1` (the earlier probe).
  - design.md Decisions 2–3 (next to Decision 1).
- **Baseline commit:** `b53c88c`.
- **Implementation identity before the analysis:**
  - Command: `./scripts/review-identity.ps1 -Baseline b53c88c -Paths README.md dashboard/e2e/dashboard.spec.ts dashboard/package-lock.json dashboard/package.json dashboard/src docs/dashboard-validation.md docs/installation.md src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard` (pwsh 7).
  - HEAD `b53c88c3111d004083c4561492bf4ed1a8bb705a`; 16 modified and 11 untracked files.
  - Tracked diff SHA-256 `98ac5d9d68c17fed0349bbc93f6cbb0672fdceb71be413c94850afcb98b4c218`.
  - Summary: `head=b53c88c baseline=b53c88c files=27 diff=98ac5d9d68c1 untracked=11/438f0453c10f`.
- **Before/after identity:** after the analysis the same command printed the same summary line. It matches, and no target file changed.
- **Prior reports / rechecked findings:** [analysis-001](analysis-001.md): ANALYSIS-001, ANALYSIS-002, ANALYSIS-006.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Binary under test | Compared timestamps, copied the build, grepped for symbols | Release `WorkLog.dll` written 20:53:14 local, after the last source edit (`DocumentService.Reviews.cs`, 20:53:09). No file under `src/WorkLog` or `dashboard/src` is newer. Copied to `artifacts/analysis-002-probe/bin` (SHA-256 `c7e074f6…`). It contains `PruneMarkerCache` and `ResolveChangeDirectory` | I did not rebuild, to avoid clashing with the concurrent QA build. The binary matches the A5 sources by timestamp and symbols. |
| Performance (listing): ANALYSIS-001 | Isolated HTTP probe `artifacts/analysis-002-probe/probe.ps1`: smoke dashboard fixture plus 305 generated archived changes (949–950 catalog documents, 312 changes); dashboard on ports 47971–47973 with `--no-open` and the fixture data directory. Same method as A3 | **Warm `all`, 12 calls per run:** run 1 min 13.3 / median 17.2 / max 36.4 ms; run 2 15.6 / 21.6 / 40.8 ms; run 3 12.3 / 18.4 / 34.2 ms. **Cold `all`:** 483–559 ms. **Warm `active`:** 2.6–4.4 ms. **For comparison:** A3 measured warm `all` at 871–1292 ms and cold at 1360 ms at 935 documents. **Response:** 306 items; a warm `GET /api/catalog` takes 20–29 ms | Measured: the target of under about 50 ms is met. The times include PowerShell JSON handling on the client side. Run 2 reused run 1's fixture, so only its latency rows count. |
| Warm-path work: ANALYSIS-001 | Read `Markers`, `ReviewsAsync` and `ResolveChangeDirectory`; ran the tests | A hit is a dictionary lookup under `markerGate` and returns before `ResolveChangeDirectory`, `SourcePath.Within` or `Read`. On a miss, `directory ??=` resolves the change directory once per change per listing | Verified. `WarmListingResolvesNoPathsAndAChangeDirectoryIsResolvedOnceOnAMiss` passes. |
| Containment: ANALYSIS-001 | Source inspection plus `SourcePath.cs` | On a miss: the live change directory is checked against the source root, then canonicalized; the file is resolved with `Within(change.Directory, directory, path)` before `Read`. A hit reads no file. If the directory check fails, the remaining misses of that change throw the same error | Verified. The pattern of checking a directory and then canonicalizing it again was already in `Resolve` at baseline. It is not a regression, so no finding. |
| Deleted or unreadable documents: ANALYSIS-001 | Tests plus probe steps 4–5 | **After a catalog refresh:** a deleted file disappears (37–57 ms). A file grown over 2 MiB gets a new fingerprint, misses, and is reported unavailable (test). **Within the catalog's 2 s lifetime:** a just-deleted document is still listed with its old markers and no unavailable notice, for 2038 ms (measured) | Verified per the agreed trade-off; the residual is covered under ANALYSIS-041. **Hypothesis:** if only permissions change and the length and write time stay the same, the cache keeps serving the old markers. |
| Bound and thrash: ANALYSIS-002 | Source inspection, tests, probe | **Bound:** `PruneMarkerCache` drops every key not in the whole snapshot, never by age or count. **Tests:** 900-document no-reparse, alternating active/archived, and pruning on a scoped listing all pass. **Probe:** an `archived` listing right after `active` takes 15.7–22.2 ms, so it is warm (cold is about 500 ms) | Verified. |
| Reader cache churn: ANALYSIS-002 | Source inspection plus test | A miss calls `Read(file, cacheResult: false)`. It reuses a cached reader entry when the stamp matches, never inserts one, and skips SHA-256. `ListingMissesDoNotEvictTheReadersDocuments` passes | Verified. |
| Pruning cost: ANALYSIS-002 | Source inspection plus measured upper bound | **Work per listing:** O(catalog documents) to build the keys (one `string.Join` per document) and a `HashSet`, plus an O(cache) scan under the lock. **Upper bound:** each `active` listing also prunes over all ~950 documents, and the whole request takes 2.6–4.4 ms including HTTP | Measured upper bound about 3 ms. The true prune cost of about 0.1–0.3 ms is a hypothesis; I did not time it in process. Acceptable either way. |
| Races between prune and listing: ANALYSIS-002 | Source inspection | **Locking:** every cache access is under `markerGate`; counters use `Interlocked`. **Overlapping listings on different snapshots:** the older one can re-insert keys after the newer one pruned. They stay until the next listing, so they are bounded by one snapshot. It can also overwrite an entry with an older fingerprint, which costs one extra parse but never serves stale data. **Cancellation:** a cancelled listing skips pruning; the next one prunes | No growth beyond one snapshot; transient only. No finding. |
| Freshness trade-off | Source inspection plus probe steps 2–3 | **Agent edit, normal client path:** the client lists only after `/api/catalog` shows a new revision, so the listing uses that same snapshot. Measured: visible on the first listing (48–65 ms). **Own comment write:** `CommentAsync` does not refresh the catalog, so the client's immediate `loadReviews()` (app.ts:1219, 1276) is answered from the old fingerprint. Measured: the new comment is missing from the first listing and appears after 2053 and 2066 ms | Regression, measured: ANALYSIS-041. |
| Error classification: ANALYSIS-006 | Source inspection, tests, `DashboardHost` mapping | **Load:** a missing index means empty. Oversized, invalid JSON, a bad shape, or a null `entries` means reset. **Errors:** `File.ReadAllText` I/O and access errors propagate (I/O maps to 503, access to 403). The client swallows both on `reads/get` and `reads/summary`, so nothing is reported on every refresh. **Blobs:** an unreadable blob throws; a missing or digest-mismatched one returns null and is removed. **Touches:** a failed touch save is swallowed and deletes nothing. **Foreign index:** `Get` returns null, `Entries` is empty, and `Mark` throws an `IOException` before any write | Verified for the decision as specified. `AnIndexThatCannotBeReadIsNotDamageAndNothingIsWrittenOrDeleted` and `ABlobThatCannotBeReadKeepsItsEntryAndOtherBaselines` pass. |
| Foreign-version gaps: ANALYSIS-006 | Isolated HTTP probe `artifacts/analysis-002-probe/probe-store.ps1` (port 47974): mark document A, replace `index.json`, mark document B | **Control** `{"schemaVersion":2,…}`: mark returns 503; index and blob untouched. **Gaps:** `{"SchemaVersion":2,…}`, `{"schemaVersion":"2",…}`, and a version-2 index over 4 MiB each return 200; the index is rewritten as version 1 and A's blob is deleted | Measured: ANALYSIS-042. |
| Client handling of the new persistent failure | Source inspection of `app.ts` | **Mark as read:** shows the server message. **Mark change read:** counts any failure, so a persistent 503 from a foreign or locked index reads as "N documents changed while being marked" | ANALYSIS-043. |
| Functional tests | `dotnet test tests/WorkLog.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~ReviewOverviewTests\|FullyQualifiedName~ReadBaselineTests"` | 31 of 31 passed. The test DLL was built at 21:00 local, after every source and test edit | Passed. The full suites belong to QA (A6). |
| Formatting | — | — | Not run: style is out of scope for a recheck. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | 2 s client poll | Source observation; poll path unchanged since A3 | **While idle:** 1 `GET /api/catalog` per 2 s, and no `/api/reviews` or `/api/reads/*`. **Per catalog revision change:** 1 `/api/reviews` (scope `all`) plus 1 `/api/reads/summary`. The listing now costs 12–41 ms over HTTP at about 950 documents (measured), down from 0.9–1.3 s. Only the changed documents are reread and parsed. |
| Cache lifetime versus interval and refresh duration | Marker cache; catalog cache (2 s); reader document cache (256) | Measured plus source | The marker cache never expires, never thrashes, and serves warm listings with zero stats. Freshness follows the catalog snapshot (at most 2 s), which matches the poll for agent edits. The exception is own writes, which are stale for one catalog lifetime (ANALYSIS-041). |
| Unbounded state | Marker cache; `dashboard-reads/` | Source plus tests | **Marker cache:** bounded by the catalog's document count. Overlapping listings can transiently add at most one older snapshot's keys. **Store:** bounds unchanged (300 keys, 64 MiB, 4 MiB index). |
| Repeated full reads where a change check would do | `POST /api/reviews` | Measured plus source | A warm listing reads and stats no file. What remains per call: O(N) lookups, building the keys twice (lookup and prune), and serializing about 110 KB of JSON. Hypothesis: serialization now dominates. |
| External tool discovery | — | Source | None added. |

## Findings

### ANALYSIS-041: The listing after the user's own comment write returns the pre-write markers for up to one catalog lifetime

- **Severity:** suggestion.
- **Related task / requirement:** 1.2, 3.1; Decision 1 ("refresh when `catalog.revision` changes").
- **Locations:**
  - `src/WorkLog/Dashboard/DocumentService.Reviews.cs:86-93`: a hit is decided by the catalog snapshot's fingerprint.
  - `src/WorkLog/Dashboard/DocumentService.cs:120-…`: `CommentAsync` and `MutateCommentAsync` neither refresh the catalog nor touch the marker cache.
  - `dashboard/src/app.ts:1219` and `:1276`: `void this.loadReviews()` right after a save or delete.
- **Observation:**
  - Before A4, every listing stat'ed the file, so the explicit post-write listing was fresh. Now the write path's catalog snapshot (under 2 s old) still carries the old fingerprint, and the explicit listing returns the cached pre-write markers.
  - Measured on the isolated dashboard: the new comment is missing from the first listing and appears after 2053 and 2066 ms, once the next catalog refresh changes the revision and the poll lists again.
  - A document deleted within the same window is likewise still listed, with no unavailable notice, for 2038 ms.
  - Effect: after saving or deleting a comment, the panel and the card and reader counts lag the reader by about 2 s. The explicit post-write listing becomes wasted work. The state corrects itself.
- **Proposed improvement and benefit:**
  - After a successful own write, remove the written document's marker-cache entry, using `MarkerKey` from the change the write path already resolves. The next listing then misses and reads the current file, so the explicit refresh is fresh again.
  - Optionally, store the stamp of the bytes actually parsed (`value.Length:value.LastWrite.Ticks`) instead of `document.Fingerprint`. A hit would then imply that the parsed content matches the catalog's stamp. That closes a theoretical case (hypothesis): content parsed after the snapshot, followed by a later write that restores the earlier length and write time.
  - Alternative on the client: drop the post-write `loadReviews` (ANALYSIS-008). That accepts the 2 s lag but removes the wasted call.
- **Behavior-preservation risks:**
  - Removal costs one extra parse per own write.
  - Keep the removal under `markerGate`.
  - Agent edits and archive moves are unaffected.
- **Evidence type:** measured result (probe steps 3 and 5).
- **Verification / measurement plan:**
  - .NET test: list, call `CommentAsync`, then list again without forcing the catalog; expect the new marker.
  - Rerun `artifacts/analysis-002-probe/probe.ps1`: expect "visible on first listing=True" for the own write.
- **Relationship:** introduced by the ANALYSIS-001 fix (the agreed freshness trade-off); related to ANALYSIS-008.

### ANALYSIS-042: A foreign-schema index is still reset in three encodings, and its blobs are deleted

- **Severity:** suggestion.
- **Related task / requirement:** 2.1; the orchestrator's ANALYSIS-006 decision (keep an index with a foreign schema version).
- **Locations:** `src/WorkLog/Dashboard/ReadBaselineStore.cs:160-184` (`Load`).
- **Observation:** the foreign-version probe accepts only a camelCase `schemaVersion` with an integer value, and it runs after the size check. Measured through `/api/reads/mark`, each of the following was reset, rewritten as version 1, and the other document's blob was deleted:
  1. Uppercase `"SchemaVersion": 2`. `TryGetProperty` is case-sensitive, but `Json.Options` (`JsonSerializerDefaults.Web`) deserializes case-insensitively, so the value becomes 2 and fails `is not { SchemaVersion: 1 }`.
  2. `"schemaVersion": "2"` or a non-integer number: deserialization throws a `JsonException`.
  3. A version-2 index over 4 MiB: the size check runs before the version probe.
  - Control: a camelCase `"schemaVersion": 2` was preserved, and mark returned 503.
  - Realism (hypothesis): a newer WorkLog would write camelCase through the same `Json.Encode`, so cases 1 and 2 need a hand edit or another writer. Case 3 needs a future index larger than this version could write; 300 entries come to about 75 KB.
- **Proposed improvement and benefit:**
  - Treat any root property matching `schemaVersion` case-insensitively, with a value other than the number 1, as foreign.
  - Treat an oversized index as foreign rather than damaged: this version cannot produce one.
  - Then only invalid JSON, a wrong shape, or a hash mismatch reset the store, which matches the decision's wording.
- **Behavior-preservation risks:**
  - An oversized index would make baselines unavailable (503 on mark) until the user deletes `dashboard-reads/`. The documented reset path covers that.
  - The corrupt-index tests still hold.
- **Evidence type:** measured result (`probe-store.ps1`). How often this happens is a hypothesis.
- **Verification / measurement plan:** extend `IndexOfAnotherSchemaVersionIsPreservedAndReportedUnavailable` with the three inputs. Expect an `IOException` from `Mark` and the index and blobs unchanged.
- **Relationship:** residual of ANALYSIS-006.

### ANALYSIS-043: "Mark change read" reports a persistent store failure as documents that changed

- **Severity:** suggestion.
- **Related task / requirement:** 3.4; the ANALYSIS-006 decision (foreign or unreadable store makes `mark` fail).
- **Locations:** `dashboard/src/app.ts` `markChangeRead`: `catch { failed++; }`, then "N documents changed while being marked and still show changes."
- **Observation:**
  - Since the ANALYSIS-006 fix, `reads/mark` fails persistently while the index is foreign (503) or unreadable (503 or 403). Before, the store reset silently and the mark succeeded.
  - `markRead` shows the server message. `markChangeRead` reports every such failure as a concurrent change, which is wrong and invites retries that cannot succeed.
- **Proposed improvement and benefit:** count only 409 as "changed while being marked". For other statuses, show "Could not mark the documents as read" with the first error message, as `markRead` does.
- **Behavior-preservation risks:** none for the 409 path. Add a unit test with a fake 503.
- **Evidence type:** source observation (not exercised in the browser).
- **Verification / measurement plan:** Angular unit test where `reads/mark` returns 503 for every document; expect the store-failure notice.
- **Relationship:** consequence of the ANALYSIS-006 fix.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | `diff=98ac5d9d68c1 untracked=11/438f0453c10f` | **Code:** a warm hit does no resolve, stat or read; a miss resolves each change once, with containment checked before `Read`. **Tests:** pass. **HTTP probe at 949–950 docs, warm `all`:** median 17.2 / 21.6 / 18.4 ms across 3 runs, max 40.8 ms. A3 measured 871–1292 ms. **Deleted or over-limit documents:** not served after the catalog refresh. **Residual:** a 2 s window after own writes and deletions (ANALYSIS-041); a permissions-only change is still served from cache (hypothesis) | verified (target < ~50 ms met) |
| ANALYSIS-002 | same | **Bound:** catalog-bounded prune, with no age or count eviction. **Tests:** 900-doc, alternating-scope and scoped-prune tests pass; probe scope switches stay warm (15.7–22.2 ms). **Reader cache:** misses do not insert into it. **Prune cost:** at most about 3 ms per listing (measured upper bound). **Races:** bounded and transient, with no growth beyond one snapshot | verified |
| ANALYSIS-006 | same | **Errors:** I/O and access errors propagate with no write or delete; the locked index and locked blob tests pass. **Mapping:** host maps I/O to 503 and access to 403; the client swallows both on `get` and `summary`. **Foreign index:** the canonical form is preserved and mark returns 503 (measured). **Residual:** three foreign encodings are still reset (ANALYSIS-042, measured); Mark change read gives a misleading notice (ANALYSIS-043) | verified for the decision as specified; residual gaps are suggestions |

## Assessment and limitations

- **Overall.** The A4 and A5 fixes do what the three findings asked:
  - Warm review listings over about 950 documents dropped from about 1 s to about 20 ms over HTTP.
  - The cache is bounded by the catalog, without thrash, and keeps other scopes warm.
  - The baseline store no longer deletes data on transient I/O or access errors.
  - The fixes add three suggestion-level issues: a 2 s stale overview after own writes (ANALYSIS-041, measured), foreign-index detection gaps (ANALYSIS-042, measured, unlikely in practice), and a misleading notice on Mark change read (ANALYSIS-043). None is a warning.
- **Measurements.**
  - Isolated Release dashboards on ports 47971–47974 with fixture data directories under `artifacts/analysis-002-probe/`.
  - The host was shared with the concurrent QA run, so the numbers are noisy; the ratio against A3 is the evidence.
  - The in-process prune time and the share of JSON serialization are hypotheses.
- **Not performed.**
  - A rebuild: I used the existing Release build, which is newer than every source.
  - The full suites (QA's scope), browser checks, and the formatter.
- **Cleanup.**
  - All four dashboards were stopped; none was left listening on 47971–47974, and no `worklog` process from the probe directory remained.
  - The fixtures, the copied binaries and the stdout files (which held bootstrap values) were deleted.
  - Left in the ignored `artifacts/analysis-002-probe/`: `probe.ps1`, `probe-store.ps1`, and empty stderr files.
- **Writes.** The only repository writes are that ignored probe folder; there were no unexpected writes.
- **Decisions for the orchestrator.** Triage ANALYSIS-041 through ANALYSIS-043; all three can be deferred. ANALYSIS-041 is the only one users would notice.
