# Design

## Context

See proposal.md for motivation and the delta spec for the requirements. Relevant current code:

- **Ongoing template** (`dashboard/src/app.html`, "Pick up where you left off" and "Other ongoing work"):
  - It renders `activeChanges()` in catalog order.
  - It renders `ongoingTickets()`: ticket-grouped rows that are not done and are not matched to an active change (`dashboard/src/app.ts`).
  - `changeRow` matches a change to a row by repository and `row.changes`.
- **Ongoing data source:** `App.loadOngoing` loads a 14-day ticket-grouped timeline with continuation paging and a consistent snapshot.
- **Work identity:** `WorkIdentity.For` (`src/WorkLog/Reporting/WorkIdentity.cs`) groups an entry by change when it has no tickets, and by its ticket set otherwise.
  - An entry with neither a ticket nor a change gets the identity `<repository>/tickets/` with an empty title. Every unlinked observation in a repository merges into that one item.
- **Change activity:** `/api/activity/changes` returns, per repository and change name, the first and latest observation time and the latest status over all history (`DashboardJournal.Activity`). The client already loads it into `activity` and refreshes it when `journalSnapshot` changes.
- **Catalog task counts:** a change carries `completedTasks` and `totalTasks`. Both are null when progress is unavailable.
- **Preferences:**
  - Server-side JSON through `/api/state`, limited to 64 KiB (`DashboardState`).
  - The client `Preferences` type uses `schemaVersion: 1` with optional fields.
  - Positions and documents are already bounded (`positionLimit`, `documentLimit`).
- **Dependencies:** the dashboard has no drag-and-drop library. Angular CDK is not a dependency.
- **Prerequisite:** `add-ticket-and-repository-links` adds ticket links, the quiet chip, and `tickets` on client rows. This change builds on them and does not redefine them.

## Goals / Non-Goals

**Goals:**
- Stage derivation, card building, expiry, filtering, and ordering are pure, deterministic functions, unit-testable without the DOM.
- Idle refresh cost stays as it is: no new polling, and no preference writes from rendering.
- The ordering is reusable by other views without moving it later.

**Non-Goals:**
- Changing how History groups or titles the unlinked item.
- Persisting the filter text, column collapse, or card expansion.
- A per-column order, or ranks that move cards between columns.

## Decisions

### 1. A pure board module on the client
Add `dashboard/src/board.ts`.

`buildBoard(input, now)` takes:
- active changes from the catalog
- change activity
- the 7-day ticket rows
- the unlinked entries
- the saved order

It returns three columns of `BoardCard`:
- `kind`: `change | ticket | adhoc`
- `key`
- `stage`
- `title`
- `repository` (or repositories)
- `lastAt | null`
- `status | null`
- task counts
- `followUpTo | null`
- `tickets`
- a reference to the source object, for the template

`App` exposes `board = computed(...)` over its existing signals and the filter signal.

Stage rules for change cards follow the spec:
```
done        = (total > 0 && completed == total) || activity.lastStatus == "done"
in_progress = !done && (activity exists || completed > 0)
proposed    = otherwise
```

The change's activity is the `ChangeActivity` entries matching the change name in any of its repositories, taking the newest `lastAt`. A change whose task progress is unavailable falls back to recorded status alone.

**Alternative: derive stages on the server.** Rejected. All inputs already reach the client, and keeping this next to the template keeps the API unchanged and the rules testable in Vitest.

### 2. Ticket cards from a 7-day timeline
`loadOngoing` changes its window from 14 to 7 days and keeps the ticket grouping, paging, and snapshot.

A row becomes a ticket card when either:
- it has tickets and none of its `changes` is an active change in the row's repository, or
- it has no tickets and its `changes` name only archived changes. These are change-grouped entries of an archived change, shown as follow-ups.

Rows associated with an active change are not separate cards. Their tickets are shown on that change's card.

`followUpTo` is the archived change name when every associated change is archived in the catalog, and null otherwise.

The bucket row with no tickets and no changes is dropped. Its observations come from Decision 3 instead.

Change cards don't use these rows for their stage. Activity covers all history, so a change idle for 20 days still shows its true latest status and the quiet chip.

### 3. A bounded endpoint for unlinked observations
Add `POST /api/ongoing/unlinked` with body `{ repositories, from, to, snapshot? }`. It returns `{ snapshot, entries: Entry[], truncated }`:
- entries in the selected repositories within `[from, to)` that have no tickets and no change
- newest first
- capped at 200

It reads through `DashboardJournal.Read` under the same snapshot rules and authorization as `/api/timeline`.

The client calls it from `loadOngoing` with the same 7-day bounds. When `truncated` is true, the board shows a visible "more unlinked work in History" note rather than silently dropping cards.

An entry with an `ExtractedTicketId` but no qualified tickets still qualifies as unlinked, since `WorkIdentity` puts it in the bucket. Its card shows the extracted ID next to the summary, and the filter matches that ID.

Alternatives considered:
- Paging `/api/observations` for the bucket identity: one call per repository, with day-based filtering that doesn't map to "last 7 days".
- Changing `WorkIdentity` to give each unlinked entry its own identity: that would change History grouping and `query_work` semantics, which is out of scope.

### 4. Priority order in preferences
Add an optional field to `Preferences`:
```
board?: { order?: string[] }
```
`schemaVersion` stays 1. A missing field means nothing is ranked.

Keys:
- `change:<sorted repositories joined by ,>/<name>`
- `ticket:<row id>`. The row id is the content hash of the work identity and is stable across refreshes.
- `adhoc:<entry id>`

Ordering within a column:
1. Unranked cards first, by `lastAt` descending.
2. Then cards without activity, by name.
3. Then ranked cards by their index in `order`.

When the user moves a card:
1. Compute the column's new displayed order.
2. Remove those keys from `order`.
3. Insert the full sequence at the index of the first removed key, or at index 0 when none was ranked.

This ranks the whole column, so previously unranked cards keep their visible position. Other columns' relative order doesn't change. The save goes through the existing `remember()` path.

When saving, keys that aren't on the current board (other than active changes outside the selection) are dropped before the 200-entry cap is applied (ledger QA-001, ANALYSIS-002). Rendering never prunes or saves, so the idle-refresh requirement holds.

This is a dashboard-wide order. `add-daily-prep` can sort Today by `order` with the same comparator, which `board.ts` exports as `byPriority`.

**Alternative: store the order in `localStorage`.** Rejected. Other preferences live on the server per data directory, and the spec asks for the order to be restored across browsers.

### 5. Reordering interaction without a new dependency
- **Drag and drop:** native HTML5 drag-and-drop events on cards with a visible drag handle. Drops are accepted only within the source column. A drop indicator shows where the card will land.
- **Keyboard:** each card has a compact menu with Move up, Move down, and Move to top. Each action keeps focus on the moved card and announces the new position through an `aria-live` region.
- **Touch:** native drag-and-drop is unreliable on touch screens, so touch users rely on the menu. The dashboard is a desktop loopback tool, so this is acceptable.

**Alternative: add `@angular/cdk/drag-drop`.** Rejected. It's a new dependency for one interaction, and the keyboard actions are still needed.

### 6. Layout
- **Order on the page:** the Review inbox stays first, and the board follows.
- **Columns:** three equal columns on wide screens, stacked on narrow ones, each with a count.
- **Change cards** keep the current content: title, repositories, chips, document links, task progress and recorded status. They add the latest activity date and the quiet chip.
- **Ticket and ad-hoc cards** are compact. A ticket card opens the existing drill-down, as "Other ongoing work" rows do today. An ad-hoc card expands in place to show the full summary, time, agent, and branch. It has no drill-down, because its work identity is the shared bucket.
- **Empty states:** the board's empty state replaces the current "A little space for your next idea" panel when there are no cards. An empty column shows a short muted line.

## Risks / Trade-offs

- **[Risk] Activity is matched by change name and repository**, so two changes with the same name in different sources of one repository share activity. → This is the existing behavior of `/api/activity/changes`. Accept it and note it.
- **[Risk] A busy week of unlinked work produces many ad-hoc cards.** → The 200-entry cap with a visible note, the 7-day expiry, and the filter. Revisit grouping by summary if this proves noisy.
- **[Risk] A rank key for a change uses its repositories.** If a source's repository identity changes, the change becomes unranked. → It degrades to the default order without losing data, and the stale key is pruned on the next save.
- **[Trade-off] Done tickets are now visible for 7 days**, where today they are hidden. This is intended, but it's a visible behavior change for existing users.
- **[Trade-off] The ongoing window shrinks from 14 to 7 days for non-change work.** A ticket idle for 8 to 14 days that is still in progress disappears from Ongoing and stays in History.

## Migration Plan

No data migration. Preferences without `board` load unchanged. Rolling back to an earlier build ignores the unknown `board` field, because preferences are read as partial objects and the server stores the JSON as-is.
