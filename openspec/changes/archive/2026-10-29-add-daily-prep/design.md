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
  - `dashboard/src/board.ts` exports `buildBoard` (columns, card kinds, stages, 7-day expiry), `isQuiet`, and `byPriority`.
  - `Preferences.board.order` holds the dashboard-wide priority order.
  - `App.loadOngoing` loads the 7-day ticket rows and the unlinked entries, and `App.loadActivity` loads change activity. Both run on every view except History.
- `WorkIdentity.For` puts every entry with neither a ticket nor a change into one `<repository>/tickets/` identity with an empty title.

## Goals / Non-Goals

**Goals:** a single request for a fast digest; deterministic ordering and copy format; reuse of the existing projection; one definition of current work and its order, shared with the Ongoing board.

**Non-Goals:**
- Summarization or rewriting of summaries (agent-written day digests are the follow-up `add-day-digests`).
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

- The same projection runs for the current local day `T` (the date of `before`): entries `< start(T+1)`, period `[before, start(T+1))`, with the same unlinked split. It returns `today: DailyItem[]` with status as of now. It is not affected by `day`.

Response: `{ day, previousDay, nextDay, items: DailyItem[], today: DailyItem[] }`. `DailyItem` contains `id`, `title`, `repository`, `tickets`, `changes`, `status`, `primary: Entry`, `others: Entry[]`. `today` only supplies lines; which items are listed under Today comes from the board (Decision 3).

The `MAX` and `MIN` lookups use the `(occurred, id)` index. Reading entries `< start(D+1)` matches the existing History cost.

Alternative: compose the digest on the client from existing timeline and observation calls. Rejected, because it takes several round trips plus extra per-item drill-down calls, which works against the "fast" goal.

### 2. Ordering and status as of day end
Rows are sorted by a status rank (`done`=0, `in_progress`=1, `started`=2), then by `primary.occurredAt` descending, then by title. Status comes from `Project` with `to = start(D+1)`, which already excludes later observations.

Yesterday statuses stay recorded facts as of the end of that day. Task checkbox progress has no history, so the board's "all tasks ticked" rule applies only to Today.

### 3. Today from the board
Add `dashboard/src/daily.ts` with a pure `dailyToday(board, todayStart, today, { zone, order })` (`zone` for `isQuiet`, `order` = the saved `board.order` for `byPriority`) that returns `{ items: { card: BoardCard, line: DailyItem | null }[], proposed: number }`:
- every card in the In progress column for which `isQuiet` is false,
- plus every card in any column whose `lastAt >= todayStart`,
- each card once by its board key, sorted with `byPriority` over the saved `board.order`,
- `line` is the matching `today` item, or null when nothing was recorded for the card today:
  - ticket card: the item whose `id` equals `card.row.id` (both are ticket-grouped identities),
  - change card: a line built from today's entries (`primary` and `others` of items whose `repository` is in `card.repositories`) whose `change` equals `card.title`, or whose `change` is empty and which carry one of `card.tickets` (same provider, scope, and ID); entries of another change never qualify. The newest is `primary` and gives the line status, the rest are `others`, and tickets come from those entries or else `card.tickets`. An item's `changes` spans its whole history, so it is not used for matching (ANALYSIS-001),
  - ad-hoc card: none; its title is already that entry's summary.
- `proposed` is the number of cards in the Proposed column.

A Today line shows the card title, tickets, today's recorded status, and the `primary` summary (or "No summary recorded"). Today's other entries are collapsed as on Yesterday lines. A card with `line = null` shows its title and tickets only. Today and Copy for daily wait until the Ongoing inputs (rows, unlinked entries, activity) for the current selection have loaded, and show a loading state meanwhile, so neither works on a partial board (QA-001).

The Daily view calls `buildBoard` over the inputs `loadOngoing` and `loadActivity` already provide, with an empty filter. Today therefore uses the board's Done rule (all tasks ticked or recorded done), keys, expiry, and order, and the user's reordering on the board changes Today on the next render. The Daily view never saves `board.order`.

**Alternative: keep a Today rule on the server.** Rejected. It would define current work and its order a second time, and the two would drift.

### 4. Copy format
Plain text:

```text
Yesterday (Thu 22 Oct):
- DEP-1232 — done — Retry policy for export jobs merged
- add-work-dashboard — in progress — Review comments edit/delete
Today:
- DEP-1240 — in progress — Export retries wired to the job scheduler; load test remains
- add-work-dashboard
Blockers:
-
```

Markdown uses `**Yesterday (Thu 22 Oct)**` headings and bullet lists. With links enabled, ticket IDs that have a `ticketUrl` become `[DEP-1232](url)`. A missing summary is shown as the status only. A Today card without a line today is copied as its title or ticket IDs alone. Copied titles for change-only items use the raw change name, as in the example; the on-screen view humanizes it as elsewhere. Preferences: `daily: { format: "text"|"markdown", links: boolean }`, defaulting to text without links, stored in bounded preferences.

### 5. Navigation and refresh
The `/daily` route is added to the main navigation. The selected `day` is in the URL query (`?day=`) for back and forward navigation, and is not persisted. Opening `/daily` loads the Ongoing inputs as the Ongoing view does. The view refreshes when `journalSnapshot` changes, and on day rollover (reusing the rollover check from `fix-dashboard-reliability`).

## Risks / Trade-offs

- An observation backdated into an earlier day can change which day counts as last recorded. → That is expected, since the view reflects recorded facts. Refresh picks it up.
- Ticket grouping merges an item's work across changes into one line. → Matches the report default. Change names are shown on the line.
- Today changes when the user reorders the board. → Intended: one priority order for the whole dashboard.
- The stand-up happens near the end of the user's day (US clients), so an item worked on both days appears in both sections with different lines. → Intended: the view is a personal recap, and the overlap reflects ongoing work.
- Today's projection reads entries up to the end of the current day on every refresh. → Bounded by one day's entries plus history already read for Yesterday; same order of cost as a History day.
- A day of unlinked work produces many Yesterday lines. → Each is a distinct recorded fact; the line format stays compact. Revisit grouping by summary together with the board if it proves noisy.
- Poor or missing summaries make lines vague. → Shown honestly as "No summary recorded"; `improve-observation-summaries` addresses this at the source.
- Unlinked observations come only from older journal entries, because current logging requires a change or ticket (`add-ongoing-board` ledger QA-003). → The one-line-per-observation rule is legacy support.

## Migration Plan

Additive. Older payloads ignore the `daily` preferences and the endpoint.
