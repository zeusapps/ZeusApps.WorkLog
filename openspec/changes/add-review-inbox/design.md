# Design

## Context

See proposal.md for motivation. Relevant current code:
- REVIEW markers are parsed on the server by `ReviewMarker.Parse` (`src/WorkLog/Dashboard/ReviewMarker.cs`). It skips fenced code and returns line, offsets, raw text, optional 32-hex `id`, quote, and comment.
- The client renders markers with the `worklog_review` markdown-it rule (`dashboard/src/markdown.ts`), which produces `Review` objects.
- Rendered blocks carry `data-start` and `data-end` source-line attributes. These are the shared coordinate for search, tasks, and comments.
- `DocumentService` reads documents with a length+mtime cache and SHA-256 revisions.
- After `fix-dashboard-reliability`:
  - The catalog stats every Markdown file. Its revision includes content fingerprints.
  - Preferences are bounded.
  - Polling refreshes dependent views only when `catalog.revision` changes.
- Dashboard-private files live in the data directory next to `dashboard-state.json`. `DashboardState` caps that file at 64 KiB, so it cannot hold document copies.

## Goals / Non-Goals

**Goals:** fast overview of open comments, and a precise "what changed" view. No new external dependencies beyond one pinned diff library. Bounded local storage.

**Non-Goals:**
- Git-based history or diffs between arbitrary revisions.
- Resolution states, assignees, or notifying agents.
- Timestamps inside REVIEW markers. The marker format stays unchanged so agents and existing parsers are unaffected.
- Per-viewer sync across machines.

## Decisions

### 1. Marker listing endpoint
`POST /api/reviews { repositories: string[], scope: "active"|"archived"|"all" }` returns `{ items: ReviewItem[], unavailable: string[] }`.
- `ReviewItem` contains `sourceId`, `change`, `variant`, `path`, `archived`, `repositories`, `line`, `id`, `quote` (truncated to 280 chars), and `comment` (truncated to 500 chars, with a `truncated` flag).
- The server iterates catalog documents in scope and parses each with `ReviewMarker.Parse`. It caches parsed markers per canonical path keyed by length and last-write time, with a bounded cache (512 entries, oldest-first eviction).
- Unavailable rules follow the scoped-notice rules from `fix-dashboard-reliability`.
- The client requests it on load and when `catalog.revision` changes.
- The counts per change and document are derived on the client from the item list. This avoids a second endpoint.

Alternative: parse markers on the client from every document. Rejected, because it requires downloading every document.

### 2. Read baseline store
Store baselines in `<data>/dashboard-reads/`:
- `index.json` holds schema version 1 and `{ key → { revision, length, lastWriteTicks, readAt, blob } }`. The key is `sourceId:change-logical-name:path`. The variant is excluded so archive moves keep the baseline, but the source is included.
- Blobs live in `blobs/<sha256>.md` as the exact text, content-addressed and deduplicated.

Bounds: at most 300 keys and 64 MiB of blobs, enforced on write by evicting the least recently read keys and deleting unreferenced blobs. Writes are atomic (temporary file plus replace) under a process-wide lock, using the existing `AtomicFile` helper. A corrupt index is treated as empty and rewritten.

Endpoints:
- `POST /api/reads/get { document }` returns `{ revision, text } | null`.
- `POST /api/reads/mark { document, revision }`. The server rereads the document, verifies that the revision matches the current content, and stores it. It rejects with 409 when stale, so a baseline never records content the user did not see.

Alternative: browser `localStorage`. Rejected, because it is limited to about 5 MiB, tied to one browser profile, and not resettable from the CLI side.

### 3. When the baseline moves
- First open with no baseline: the client calls `mark` with the displayed revision.
- Explicit "Mark as read" button in the reader header, plus "Mark change read", which marks each document in the selected change.
- Own writes: before a dashboard comment insert, edit, or delete, the client notes whether `baseline.revision == displayed.revision`. If so, after the successful write it calls `mark` with the revision returned by the write. Otherwise the baseline stays, so earlier agent changes remain highlighted.
- Polling never moves the baseline.

### 4. Diff and block mapping
The client computes a line diff between baseline text and current text, normalizing line endings, with the `diff` (jsdiff) package pinned to an exact version and recorded in `docs/dashboard-validation.md`. Fetch its current API docs via `ctx7` before use.
- Added or changed current lines map to rendered blocks whose `[data-start, data-end)` intersects them. Those blocks get a `changed-since-read` class and a gutter marker with accessible text ("Changed since last read").
- Removed hunks become a collapsed marker inserted before the block that starts at the hunk's position in the current text. Expanding it shows the removed lines as preformatted, escaped text.
- Source view highlights lines directly.
- Change navigation: "Previous/Next change" buttons and the `[` and `]` keys, ignored while typing in inputs.
- Addressed comments: parse markers from the baseline text with the client renderer's marker rule and compare by `id`. Legacy markers without an id are compared by exact marker text. Missing markers are listed in a reader panel "Removed since last read".

Performance: documents are at most 2 MiB. Diffing runs only when the revisions differ, and is computed once per (baseline, revision) pair.

### 5. Updated-since-read indicator on cards
The catalog document entries include `fingerprint` (`length:lastWriteTicks`). `/api/reads/summary` returns, for the keys in the current catalog, the stored `length` and `lastWriteTicks`. The client marks a document updated when the fingerprint differs. mtime-only changes, such as touch or checkout, can show a false positive. Opening the document confirms by revision and clears the indicator when the content is unchanged, by re-marking with the same revision.

### 6. UI placement
- Ongoing work gets an "Open review comments" panel with a total, grouped by change. It has a scope switch (active/archived/all), using the shared repository filter.
- Change cards show "N comments" and "Updated since read" chips.
- The reader header gets a "N open comments in this change" menu, a "Changed since last read" banner with navigation and Mark as read, and a "Removed since last read" disclosure.

## Risks / Trade-offs

- Baseline blobs duplicate private document content on disk. → They are stored in the same private data directory as the journal and are bounded. Deleting `dashboard-reads/` resets them. This is documented in `docs/installation.md`.
- Line-level diff highlights a whole paragraph for a one-word change. → Acceptable for review; word-level detail is deferred.
- Archive moves change the directory but keep the logical name. → The key uses the logical name, so the baseline survives the move. Two copies in different sources keep separate keys.
- Clock or mtime resolution can hide a change of equal length within the same tick. → The reader always compares revisions on open, so cards are only a hint.

## Migration Plan

Additive. Older payloads ignore `dashboard-reads/`. Rolling back leaves the directory unused, and it can be deleted.
