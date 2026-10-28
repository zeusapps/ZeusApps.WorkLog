# Design

## Context

See proposal.md for motivation. Relevant current code:
- `TimelineService.Project(query, period, snapshot, entries)` (`src/WorkLog/Dashboard/TimelineService.cs`) already groups by work identity, selects identities with observations in `[from, to)`, and uses all earlier observations for lifecycle. It returns `TimelineRow` with `Title`, `Status` (latest as of `to`), `Tickets`, `Changes`, `Repository`, and `LastAt`.
- `TimelineService.Observations` returns a work identity's entries, optionally for one local day.
- `DashboardJournal.Read(to, repositories, snapshot)` loads entries with `occurred < to`.
- `ReportingPeriod.FromRequest` validates bounds and timezone.
- The client computes local day boundaries with `boundary(date, zone)` (`dashboard/src/api.ts`).
- After `fix-dashboard-reliability`, the catalog exposes `journalSnapshot`, and polling refreshes on its change. After `add-ticket-and-repository-links`, `links.ts` provides `ticketUrl`.
- After `add-ongoing-board`:
  - `dashboard/src/board.ts` exports `buildBoard` (columns, card kinds, stages, quiet indication, 7-day expiry) and `byPriority`.
  - `Preferences.board.order` holds the dashboard-wide priority order.
  - `App.loadOngoing` loads the 7-day ticket rows and the unlinked entries, and `App.loadActivity` loads change activity. Both run on every view except History.
- `WorkIdentity.For` puts every entry with neither a ticket nor a change into one `<repository>/tickets/` identity with an empty title.

## Goals / Non-Goals

**Goals:** a single request for a fast digest; deterministic ordering and copy format; reuse of the existing projection; one definition of current work and its order, shared with the Ongoing board.

**Non-Goals:**
- Summarization or rewriting of summaries.
- Working-day calendars, holidays, or configured stand-up times.
- Ticket service lookups.
- Saving generated dailies.

## Decisions

### 1. One endpoint for the digest
`POST /api/daily { before: instant, timezone, repositories, day?: "yyyy-MM-dd" }`:
- `before` is the start of the current local day, computed by the client with `boundary(today, tz)`.
- If `day` is omitted, the server finds `MAX(occurred) WHERE occurred < before` (with the repository filter), converts it to a local date `D`, and uses that. If `day` is given, it must be earlier than the current local date.
- It computes `[start(D), start(D+1))` through `ReportingPeriod`. It reads entries `< start(D+1)` and calls `Project` with ticket grouping, taking all rows (not just one page). For each row, it returns the entries of `D`: the latest is "primary", and the rest are collapsed.
- The row with no tickets and no changes (the unlinked bucket) is split: each of its entries on `D` becomes its own item with `id` = `adhoc:<entry id>`, the entry's summary as the title (empty when missing), the entry's status, and `others: []`. This matches the board's ad-hoc cards.
- `previousDay` is the local date of `MAX(occurred) WHERE occurred < start(D)`. `nextDay` is the local date of `MIN(occurred) WHERE occurred >= start(D+1) AND occurred < before`. Either can be null.

Response: `{ day, previousDay, nextDay, items: DailyItem[] }`. `DailyItem` contains `id`, `title`, `repository`, `tickets`, `changes`, `status`, `primary: Entry`, `others: Entry[]`. Today is not part of the response (Decision 3).

The `MAX` and `MIN` lookups use the `(occurred, id)` index. Reading entries `< start(D+1)` matches the existing History cost.

Alternative: compose the digest on the client from existing timeline and observation calls. Rejected, because it takes several round trips plus extra per-item drill-down calls, which works against the "fast" goal.

### 2. Ordering and status as of day end
Rows are sorted by a status rank (`done`=0, `in_progress`=1, `started`=2), then by `primary.occurredAt` descending, then by title. Status comes from `Project` with `to = start(D+1)`, which already excludes later observations.

Yesterday statuses stay recorded facts as of the end of that day. Task checkbox progress has no history, so the board's "all tasks ticked" rule applies only to Today.

### 3. Today from the board
Add `dashboard/src/daily.ts` with a pure `dailyToday(board, todayStart)` that returns `{ items: BoardCard[], proposed: number }`:
- every card in the In progress column that does not show the quiet indication,
- plus every card in any column whose `lastAt >= todayStart`,
- each card once by its board key, sorted with `byPriority` over the saved `board.order`,
- `proposed` is the number of cards in the Proposed column.

The Daily view calls `buildBoard` over the inputs `loadOngoing` and `loadActivity` already provide, with an empty filter. Today therefore uses the board's Done rule (all tasks ticked or recorded done), keys, expiry, and order, and the user's reordering on the board changes Today on the next render. The Daily view never saves `board.order`.

**Alternative: keep a Today rule on the server.** Rejected. It would define current work and its order a second time, and the two would drift.

### 4. Copy format
Plain text:

```text
Yesterday (Thu 22 Oct):
- DEP-1232 — done — Retry policy for export jobs merged
- add-work-dashboard — in progress — Review comments edit/delete
Today:
- DEP-1240
- add-work-dashboard
Blockers:
-
```

Markdown uses `**Yesterday (Thu 22 Oct)**` headings and bullet lists. With links enabled, ticket IDs that have a `ticketUrl` become `[DEP-1232](url)`. A missing summary is shown as the status only. Titles for change-only items use the humanized change name, as elsewhere. Preferences: `daily: { format: "text"|"markdown", links: boolean }`, defaulting to text without links, stored in bounded preferences.

### 5. Navigation and refresh
The `/daily` route is added to the main navigation. The selected `day` is in the URL query (`?day=`) for back and forward navigation, and is not persisted. Opening `/daily` loads the Ongoing inputs as the Ongoing view does. The view refreshes when `journalSnapshot` changes, and on day rollover (reusing the rollover check from `fix-dashboard-reliability`).

## Risks / Trade-offs

- An observation backdated into an earlier day can change which day counts as last recorded. → That is expected, since the view reflects recorded facts. Refresh picks it up.
- Ticket grouping merges an item's work across changes into one line. → Matches the report default. Change names are shown on the line.
- Today changes when the user reorders the board. → Intended: one priority order for the whole dashboard.
- A day of unlinked work produces many Yesterday lines. → Each is a distinct recorded fact; the line format stays compact. Revisit grouping by summary together with the board if it proves noisy.
- Poor or missing summaries make lines vague. → Shown honestly as "No summary recorded"; `improve-observation-summaries` addresses this at the source.

## Migration Plan

Additive. Older payloads ignore the `daily` preferences and the endpoint.
