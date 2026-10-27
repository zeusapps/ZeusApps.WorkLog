# Design

## Context

See proposal.md for the defects. File and line references are at commit `582f8bc`.

**Rolling range (A):**
- `App.loadHistory()` calls `remember()` after every load (`dashboard/src/app.ts:352`).
- `remember()` writes `history.from`, `history.to`, and `history.timezone` unconditionally (`app.ts:959-978`).
- `start()` restores them as-is (`app.ts:197-201`).
- `focusDay()` overwrites `from` and `to` and persists them (`app.ts:394-398`).
- The timezone field is free text (`app.html`, History controls), so an invalid zone produces a 400 from `ReportingPeriod.FromRequest` on every query.

**Unbounded preferences (B):**
- `onScroll` stores `preferences.positions[documentKey]` for every document ever opened (`app.ts:843-851`), and `loadDocument` adds to `preferences.documents` (`app.ts:576-578`).
- `DashboardState.Write` rejects JSON over 64 KiB (`src/WorkLog/Dashboard/DashboardState.cs:29`). `Read` silently resets when the file is over 64 KiB.
- Failed saves call `report()`, which sets the global error banner (`app.ts:976`).

**Escape (C):** the document-level keydown handler closes the reader on Escape regardless of focus (`app.ts:1018-1030`).

**Residue (D):** `openspec/changes/archive/2026-10-26-add-work-dashboard/proposal.md` lines 11 and 14 hold `<!-- REVIEW: test comment -->` and `<!-- REVIEW: foo bar -->`.

**Search scope (E):** `DocumentService.SearchAsync` starts `unavailable` from all `snapshot.Notices` and errored sources (`DocumentService.cs:173`). The notices include a "no available OpenSpec source" line for every journal repository without a checkout.

**Poll cost:**
- The client polls every 2 s (`app.ts:211`, `poll()` at `app.ts:979-1017`). Each poll fetches `/api/catalog`, reloads the open document, re-runs search when text is present, and every 5th poll calls `refreshData()`, which issues two or more timeline queries.
- `SourceCatalog.GetAsync` caches for 2 s (`SourceCatalog.cs:29`), so almost every poll triggers `DiscoverAsync`.
- `DiscoverAsync` runs, sequentially: `git worktree list` per repository context (`:54`), `git rev-parse` for the launch directory, `openspec context --json` per checkout (`:84`), and `openspec list --json` per source (`:127`).
- Measured on the development machine: about 1.0 s for `openspec context --json` and about 0.9 s for `openspec list --json`.
- Search re-reads every file with `fresh: true` (`DocumentService.cs:183`), which also clears the 64-entry document cache.
- Each timeline request reads and decodes every journal row with `occurred < to` (`DashboardJournal.Read`).

**Smaller:**
- `LinkAsync` uses `Sources.Single` (`DocumentService.cs:206`), so an unknown source throws `InvalidOperationException`, which maps to 503.
- Search section detection uses `StartsWith('#')` on every line, including inside fences (`DocumentService.cs:187`).

## Goals / Non-Goals

**Goals:** fix A–E and the poll cost without changing API shapes more than needed. Keep preferences backward compatible, and make the fixed behaviors unit-testable.

**Non-Goals:**
- Multi-tab preference merging (last write wins remains).
- Journal read caching keyed by snapshot. Reconsider only if measurements after this change show timeline reads dominate.
- Splitting `app.ts` beyond the services needed here.

## Decisions

### 1. Range model in preferences
`history.range` is `{ "kind": "relative", "days": 7|30|90 }` or `{ "kind": "custom", "from": "yyyy-MM-dd", "to": "yyyy-MM-dd" }`.
- The client computes the effective `from`/`to` from the range at each load, using `localDate(new Date(), timezone)`.
- The `history.from` and `history.to` fields are no longer written. If they appear without `range`, the client treats them as legacy and uses relative 30.
- `history.timezone` is written only from the timezone field's change handler, after validation. Validation: `new Intl.DateTimeFormat("en", { timeZone })` must not throw. When absent, the client uses `/api/info`.
- Day focus is a transient `focus` signal that overrides the effective range for queries. It is not persisted, and the UI shows "Showing <day> — back to <range label>".
- Editing the date inputs switches the range to custom. Selecting a preset switches it to relative.
- A day-rollover check runs in the poll: when `localDate(now)` differs from the last evaluated date, History reloads.
- `boundary(date, zone)` returns the first instant of the requested local date. In zones whose clocks skip 00:00 (for example `Africa/Cairo` on 2026-04-24), the current search lands on 23:00 of the previous day; when the result maps to the previous local date it advances to the first valid instant of the requested date (QA-006).

Alternative: keep absolute dates but stop saving them on load. Rejected, because a user who explicitly picks "last 30 days" still expects it to roll forward.

### 2. Bounded preference maps
`positions` and `documents` become most-recently-used maps capped at 100 and 200 entries. They are stored as objects in insertion order and moved to the end on each use. Scroll writes are debounced to 1 s. `remember()` failures set a dedicated `preferencesNotice` once per visit instead of `error`. The server keeps its 64 KiB limit and returns 413 with a clear message for oversize payloads. The client cap keeps typical payloads well under 20 KiB.

### 3. Escape handling
The keydown handler checks `event.target`. Inside `textarea`, `input`, `select`, or the delete confirmation, Escape cancels the delete confirmation if one is open, otherwise blurs to the reader container, and returns without closing. A composer with a selection but no text clears the selection. Only Escape with focus outside inputs closes the reader or detail panel.

### 4. Scoped search notices
`SearchAsync` builds `unavailable` from:
- (a) errored sources whose repositories intersect the requested repositories (all, when the request has none),
- (b) per-document read failures within scope, and
- (c) for each requested repository, a "no available OpenSpec source" notice when it has none.

Catalog-wide notices that do not match these rules are dropped.

### 5. Catalog refresh tiers
Split discovery into two tiers.
- **Cheap tier, every request (still deduplicated by the 2 s cache):** journal repositories, configured contexts, directory enumeration of changes, and a stat of each Markdown file (length and last-write time).
- **Expensive tier:** `git worktree list`, launch `git rev-parse`, `openspec context`, and `openspec list`. Results are cached per checkout and source. They are recomputed when:
  - (a) the cached entry is older than 60 s,
  - (b) `config.json` last-write time changed (repository contexts),
  - (c) a source's set of change directories changed (create, archive), or
  - (d) any `tasks.md` (or schema-tracked task file) under the source changed length or mtime. This invalidates only that source's `openspec list` result.
- Expensive-tier calls for independent checkouts run concurrently, with a small degree of parallelism (4), each still under the existing 15 s timeout.
- `CatalogSnapshot.Revision` hashes change identity, task counts, and the Markdown file fingerprints, so content edits change the revision.
- The catalog response gains `journalSnapshot`, the journal's `MAX(rowid)`, from a read-only connection. A missing journal reads as 0.

Alternative: filesystem watchers. Rejected, as in the original design: they miss replaced directories and add lifecycle complexity.

### 6. Client poll uses revisions
`poll()` keeps the 2 s interval while visible, but:
- It sends the open document's `knownRevision` in the `/api/document` body. When the current revision equals it, the server returns `{ revision, unchanged: true }` without text and skips splitting and task parsing; the client keeps the rendered document. Requests without `knownRevision` get the full response, so the API stays backward compatible (ANALYSIS-002). A GET with ETag and 304 was rejected: the endpoint is a POST with the location in the body, and converting it changes every caller.
- It re-runs search only when `catalog.revision` changed since the last search.
- It calls `refreshData()` only when `journalSnapshot` changed, or after the day-rollover check fires.

The fixed every-5th-poll refresh is removed. Idle polls do not save preferences, so `dashboard-state.json` is not rewritten while nothing changes (ANALYSIS-003).

### 7. Search reads use the cache
Search uses `Read(path)` without `fresh`, relying on the length and mtime check. The cache cap increases to 256 entries, evicting the oldest instead of clearing everything.

### 8. Small fixes
- `LinkAsync` uses `SingleOrDefault` and throws `FileNotFoundException("Document source is unavailable.")`.
- Search tracks fenced-code state, using the same fence rule as `ReviewMarker.Parse`, and ignores `#` lines inside fences.
- Remove the two archive markers and the blank lines added with them, so the remaining list reads as the original bullets.

## Risks / Trade-offs

- Task progress can lag up to 60 s if a task file is tracked under an unexpected name. → Invalidation also covers any Markdown file change in a change directory whose name contains "task". The 60 s bound caps staleness.
- Stat of every Markdown file per catalog request costs more than enumeration alone. → It is still far cheaper than one CLI spawn. The analyzer measures it during review.
- Markdown stamps come from directory entries (ANALYSIS-001 fix), and NTFS refreshes an entry only for the name a file was written through. A Markdown file changed through a second hard link is therefore noticed on its next read through the listed name or at the 60 s round. → Accepted: git, editors and `openspec archive` do not create hard-linked documents (ANALYSIS-042).
- Legacy users lose an intentionally chosen custom range once. → Acceptable: the saved dates were written unconditionally, so they cannot be told apart from defaults.

## Migration Plan

No data migration. Old payloads (v0.3.0) ignore `history.range`. Rolling back keeps the new field unused. Remove the archive markers in the same commit as the code fixes.
