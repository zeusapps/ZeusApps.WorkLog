# Findings: add-review-inbox

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

Advisory marker; resume and hand-off rules are in the [workflow](../../../../../.agents/workflows/openspec-delegation.md#mixed-client-operation).

- Client / version: Claude Code 2.1.311
- Session identity: af3b664a-0d93-4dfd-8c0d-ee33a3b15c02 (conversation after `/clear`; same client process as eae5a20b-6b19-45bd-b040-24cfe844c709, the `fix-dashboard-reliability` orchestrator)
- Since (UTC): 2026-10-27
- Previous owner: none

## Scope decisions

- 2026-10-27, prerequisite check: `fix-dashboard-reliability` is archived and `catalog.revision` includes per-file Markdown fingerprints. Catalog documents do not yet expose a `fingerprint` field, although the proposal attributed it to that change. Design Decision 5 already specifies the field, and the per-file stamp is already computed in `SourceCatalog.AddChange`, so the developer adds it under task 3.6. No acceptance change.
- 2026-10-27, A1 developer deviations, accepted provisionally by the orchestrator for reviewers to check:
  1. `/api/reads/summary` and the `/api/reads/mark` result carry `fingerprint: "length:lastWriteTicks"` as a string instead of separate numeric fields, because write-time ticks exceed 2^53 in JavaScript.
  2. The client fetches `/api/reviews` with `scope: "all"` and the panel's scope switch filters locally, so card and reader counts cover archived changes. The server-side `scope` is implemented and tested.
  3. Comment links use the anchor `note:<line>`, because `review-<line>` collided with heading slugs.
  4. Store details: the summary covers only current-catalog documents; `get` returns `null` when no baseline exists; `get` refreshes `readAt` at most once per 60 s; `readAt` is strictly increasing; blob names and SHA are validated on read.
  5. Removed passages are placed before the top-level block. A removed REVIEW marker appears both inline and in the panel, and navigation groups neighbouring changes into one stop.
  6. A diff that times out after 3 s reports "too many differences" instead of failing.
  7. The review listing has no cap on item count.
  8. Mark change read fetches each document and marks it with the fetched revision.
  9. Developer evidence: the Release-build e2e with the OpenSpec CLI on PATH failed the existing QA-007 store-fallback scenario (deferred follow-up from `fix-dashboard-reliability`). Whether it fails at baseline is unverified. The full e2e passed 19/19 against the packaged build. The 60 s idle window missed the CLI refresh round.
- Baseline commit: `b53c88c` on branch `change/add-review-inbox` (created from `change/fix-dashboard-reliability`).

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A1 implementation | developer | Claude Code / role default | none: role default | tasks 1.1–4.3; baseline `b53c88c` | implementation paths only | none | done 2026-10-27, model claude-sonnet-5-5; identity `head=b53c88c baseline=b53c88c files=27 diff=19f5e4ad5594 untracked=11/a7b097ece98f` (paths: README.md dashboard/e2e/dashboard.spec.ts dashboard/package-lock.json dashboard/package.json dashboard/src docs/dashboard-validation.md docs/installation.md src/WorkLog/Dashboard tests/WorkLog.Tests/Dashboard), confirmed by the orchestrator |
| A2 review QA | qa | Claude Code / role default | none: role default | all tasks 1.1–4.3; identity of A1 | `qa-001.md`; screenshots `../screenshots/qa-001-*.png` | QA-001 – QA-040 | done 2026-10-27, model claude-sonnet-5-5; QA wrote its own report; QA-001..004 new (005–040 unused, retired); identity unchanged |
| A4 fix round 1 | developer | Claude Code / role default | none: role default | QA-001, QA-002, ANALYSIS-001, ANALYSIS-002, ANALYSIS-006; identity `diff=19f5e4ad5594 untracked=11/a7b097ece98f` | implementation paths only | none | done 2026-10-27, model claude-sonnet-5-5; identity `head=b53c88c baseline=b53c88c files=27 diff=967a595168aa untracked=11/ff54990f1e3e`, confirmed by the orchestrator |
| A5 fix round 2 | developer | Claude Code / role default | none: role default | ANALYSIS-001, ANALYSIS-002 under the user's catalog-bound decision; identity `diff=967a595168aa untracked=11/ff54990f1e3e` | implementation paths only | none | done 2026-10-27, model claude-sonnet-5-5; identity `head=b53c88c baseline=b53c88c files=27 diff=98ac5d9d68c1 untracked=11/438f0453c10f`, confirmed by the orchestrator |
| A6 recheck QA | qa | Claude Code / role default | none: role default | QA-001, QA-002, ANALYSIS-006 (functional) plus regression; identity of A5 | `qa-002.md`; screenshots `../screenshots/qa-002-*.png` | QA-041 – QA-060 | done 2026-10-27, model claude-sonnet-5-5; QA wrote its own report; QA-001, QA-002, ANALYSIS-006 (functional) verified; no new findings (QA-041..060 unused, retired); identity unchanged |
| A7 recheck analysis | code-analyzer | Claude Code / role default | none: role default | ANALYSIS-001, ANALYSIS-002, ANALYSIS-006 plus regression; identity of A5 | `analysis-002.md` | ANALYSIS-041 – ANALYSIS-060 | done 2026-10-27, model claude-opus-5-5; no write attempted (client subagent instruction to return text), saved by orchestrator under a header quoting the instruction; ANALYSIS-001/002/006 verified; ANALYSIS-041..043 new (044–060 unused, retired); identity unchanged |
| A3 review analysis | code-analyzer | Claude Code / role default | none: role default | all tasks 1.1–4.3; identity of A1 | `analysis-001.md` | ANALYSIS-001 – ANALYSIS-040 | done 2026-10-27, model claude-opus-5-5; write refused, saved by orchestrator under a header quoting the refusal; ANALYSIS-001..009 new (010–040 unused, retired); identity unchanged |

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| QA-001 | [qa-001](qa-001.md) | 4.3 | warning | yes: the task's required validation record is inaccurate (missed the CLI refresh round; spawn counters invalid) | verified | A4 developer | [qa-002](qa-002.md): independent packaged re-measure, 150 s window, 2 CLI rounds, matches the record; identity diff=98ac5d9d68c1 |
| QA-002 | [qa-001](qa-001.md) | 2.2 | warning | yes: malformed `/api/reads/get` input returns 500 instead of the host's 400 mapping for invalid requests; clear in-scope fix | verified | A4 developer | [qa-002](qa-002.md): 16 malformed bodies per endpoint return 400; host tests present |
| QA-003 | [qa-001](qa-001.md) | 1.2 | suggestion | — | duplicate | — | duplicate of ANALYSIS-002 |
| QA-004 | [qa-001](qa-001.md) | 2.2 | suggestion | no | deferred | — | follow-up: return 404 from `/api/reads/get` for a nonexistent document, like other endpoints |
| ANALYSIS-001 | [analysis-001](analysis-001.md) | 1.1, 1.2, 3.1; Decision 1; deviation 2 | warning | yes: deviation 2 was accepted on condition that it stays affordable; measured ~1 s per refresh at 935 docs on every catalog change, repeating up to every 2 s during agent edits | verified | A4, A5 developer | [analysis-002](analysis-002.md): HTTP probe at ~950 docs, warm `all` median 17–22 ms; identity diff=98ac5d9d68c1; residual ANALYSIS-041 |
| ANALYSIS-002 | [analysis-001](analysis-001.md) | 1.2 | warning | yes: task 1.2's "second listing does not reparse" fails above 512 documents | verified | A4, A5 developer | [analysis-002](analysis-002.md): catalog-bounded prune, no reparse on warm or alternating-scope listings |
| ANALYSIS-003 | [analysis-001](analysis-001.md) | 1.1 | suggestion | no | deferred | — | follow-up: one scoped-document iterator shared by search and review listing (may fall out of the ANALYSIS-001 fix) |
| ANALYSIS-004 | [analysis-001](analysis-001.md) | 3.5 | suggestion | no | deferred | — | follow-up: token-only `parseReviews` and memoize baseline reviews per revision |
| ANALYSIS-005 | [analysis-001](analysis-001.md) | 3.3; deviation 6 | suggestion | no | deferred | — | follow-up: `maxEditLength`, lower budget, whole-document mark when incomplete |
| ANALYSIS-006 | [analysis-001](analysis-001.md) | 2.1; spec retention/removability | warning | yes: a transient I/O error silently deletes every stored baseline (data-loss safety) | verified | A4 developer | [analysis-002](analysis-002.md) (classification) and [qa-002](qa-002.md) (locked index and blob, foreign schema, corruption reset, browser under failure); residual ANALYSIS-042, ANALYSIS-043 |
| ANALYSIS-007 | [analysis-001](analysis-001.md) | 3.4, 3.6 | suggestion | no | deferred | — | follow-up: return server `key` from `/api/reads/mark`; one change-lookup helper |
| ANALYSIS-008 | [analysis-001](analysis-001.md) | 3.1 | suggestion | no | deferred | — | follow-up: single-flight overview refresh; drop the duplicate post-write fetch |
| ANALYSIS-009 | [analysis-001](analysis-001.md) | 4.2 | suggestion | no | deferred | — | follow-up: mention `dashboard-reads/` and the review listing in `docs/architecture.md` |
| ANALYSIS-041 | [analysis-002](analysis-002.md) | 1.2, 3.1; ANALYSIS-001 fix | suggestion | no: measured ~2 s lag of the overview after an own comment write; self-correcting; both fix cycles used | deferred | — | follow-up: drop the written document's marker-cache entry after an own write (or key hits on the parsed stamp) |
| ANALYSIS-042 | [analysis-002](analysis-002.md) | 2.1; ANALYSIS-006 decision | suggestion | no: needs a hand-edited or oversized future index | deferred | — | follow-up: detect `schemaVersion` case-insensitively and by any non-1 value; treat an oversized index as foreign |
| ANALYSIS-043 | [analysis-002](analysis-002.md) | 3.4; ANALYSIS-006 fix | suggestion | no | deferred | — | follow-up: in Mark change read, count only 409 as "changed while being marked"; report other failures as store errors |

## Finding details and history

- 2026-10-27, QA-001..004 reported in qa-001; ANALYSIS-001..009 reported in analysis-001. Triage by the orchestrator per the severity gate. QA-003 is a duplicate of ANALYSIS-002 (same FIFO thrash, source-only in QA). QA also established that the store-fallback e2e failure (deviation 9) also occurs at baseline `b53c88c`, so this change did not introduce it; it stays the deferred QA-007 follow-up of `fix-dashboard-reliability`.
- 2026-10-27, ANALYSIS-006 fix decision by the orchestrator: reset the index only on positive evidence of damage; I/O and access errors propagate (503) without writing or deleting; an index with a foreign schema version is preserved and baselines are reported unavailable. Design Decision 2's "corrupt index is treated as empty and rewritten" still holds for real corruption. Reported to the user for override.
- 2026-10-27, ANALYSIS-002 fix stays within Decision 1's 512-entry bound: least-recently-used eviction with no cyclic-scan thrash, and listing misses no longer evict the reader's document cache.
- 2026-10-27, A4 assigned: QA-001, QA-002, ANALYSIS-001, ANALYSIS-002, ANALYSIS-006.
- 2026-10-27, A4 done (claude-sonnet-5-5), identity `head=b53c88c baseline=b53c88c files=27 diff=967a595168aa untracked=11/ff54990f1e3e`, confirmed by the orchestrator. All five findings moved to awaiting-verification. The developer reported the open limit: with the 512 cap kept, a warm listing at 900 documents still reparses its 388-document overflow (350 ms median, down from 1,648 ms). The freshness trade-off: the listing now follows the catalog fingerprint, which can lag up to the catalog's 2 s cache.
- 2026-10-27, user decision: bound the marker cache by the current catalog instead of a fixed 512 entries. The orchestrator updated design Decision 1 and task 1.2 to match. A5 (fix round 2, the second and last fix cycle) is assigned for ANALYSIS-001/002 under the new bound; rechecks of all five findings follow on its target.
- 2026-10-27, A5 done: the cache is pruned to the whole current catalog after each listing; 900 documents warm `all` median 0.53 ms with 0 reparses (in-process, developer measurement). ANALYSIS-001/002 moved to awaiting-verification. A6 and A7 assigned.
- 2026-10-27, A7 (analysis-002): ANALYSIS-001 and ANALYSIS-002 verified by independent measurement and source review; ANALYSIS-006 verified for the decision as specified, awaiting QA's functional recheck. New suggestions ANALYSIS-041..043 deferred per the severity gate; both fix cycles are used.
- 2026-10-27, A6 (qa-002): QA-001, QA-002 and the functional side of ANALYSIS-006 verified; full suites and packaged e2e 19/19 pass on the current target. Observations recorded without findings: POST without a JSON body falls through to the SPA fallback with 200 on all `/api/` endpoints (pre-existing, also `/api/document`); `UnauthorizedAccessException` maps to 403 rather than 503 (not exercised).

## Completion assessment

2026-10-27, by the orchestrator:

- Current target `head=b53c88c baseline=b53c88c files=27 diff=98ac5d9d68c1 untracked=11/438f0453c10f`, covered by both current reports: [qa-002](qa-002.md) and [analysis-002](analysis-002.md), both with unchanged before/after identity. Unaffected coverage from [qa-001](qa-001.md) (all spec scenarios, endpoint authorization, store bounds, own-write rule, archived read-only) carries forward: A4/A5 touched only the listing cache, the store error classification, input validation and the read-summary failure handling, all re-exercised in qa-002 together with the packaged e2e (19/19) and a light pass of the spec scenarios.
- Acceptance evidence: .NET 263/263, dashboard unit 81/81, packaged-build Playwright 19/19 (qa-002); idle cost with the overview visible independently re-measured (qa-002); warm listing at ~950 documents 17–22 ms over HTTP (analysis-002); jsdiff pinned at 9.0.0 and recorded; docs links resolve (qa-001).
- Every finding is triaged. No blocking finding is open. Deferred follow-ups: QA-004, ANALYSIS-003, -004, -005, -007, -008, -009, -041, -042, -043. QA-003 is a duplicate. The store-fallback e2e failure with the OpenSpec CLI on PATH predates this change (qa-001, baseline worktree) and remains the `fix-dashboard-reliability` QA-007 follow-up.
- No scope deferral. One planning change by user decision: the marker cache is bounded by the catalog (design Decision 1, task 1.2).
- Parity: not applicable (dashboard-only; confirmed by qa-001).
- Hygiene: passing in qa-002 over the committed change files; the orchestrator re-ran the search over findings.md, analysis-002.md, design.md and tasks.md written afterwards: only pattern-list quotes and loopback literals.
- 2026-10-28, user acceptance: the user installed dev build `0.3.9001` (commit `50df7b8`) on their own installation, followed the walkthrough on real data, and accepted the comment presentation and the changed and removed views. Further adjustments will come from practical use as new changes.
- Commit: `50df7b8` on `change/add-review-inbox` (implementation and change artifacts). Not pushed; archive and publication need separate user requests.
