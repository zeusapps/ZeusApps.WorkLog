# Proposal

## Why

Ongoing work shows active changes in catalog order, with no stages and no way to reorder them, so it is hard to find the change the user cares about. Task checkboxes lag behind real work because agents do not tick them right away. Recorded observations are the more timely signal, but the grid does not use them to show where a change stands. Work that has no OpenSpec change is listed separately, and work with neither a ticket nor a change collapses into one nameless item per repository.

## What Changes

- The Ongoing view becomes a board. The Review inbox stays above it. The board replaces "Pick up where you left off" and "Other ongoing work".
- Three derived columns: **Proposed**, **In progress**, and **Done**. Columns come from recorded facts and cannot be set by dragging.
  - A change card is Done when all of its tasks are ticked or its latest recorded status is done.
  - Otherwise it is In progress when it has any recorded observation or any ticked task.
  - Otherwise it is Proposed.
  - When task progress and recorded status disagree, the card shows both facts without choosing one.
- Change cards stay on the board until the change is archived. A change in Done is a visible reminder to archive it. The quiet indication from `add-ticket-and-repository-links` (no observation in the last 14 days) appears on In-progress change cards.
- Work without an active change is short-lived on the board:
  - **Ticket cards** are ticket-grouped work items with no active change. They are marked "Ticket · no OpenSpec change", or "Follow-up to <change>" when their only associated change is archived.
  - **Ad-hoc cards** stand for single observations with neither a ticket nor a change. Each gets its own card titled with its summary, replacing the nameless per-repository item.
  - Both kinds appear in In progress or Done by their recorded status. They leave the board 7 days after their latest observation.
- A single user-controlled **priority order**:
  - The user reorders cards by drag and drop within a column, or with keyboard-accessible move actions.
  - Each column shows its cards in that order, and a card keeps its rank when the journal moves it to another column.
  - Unranked cards come first in their column, by latest activity.
  - The order is saved in dashboard preferences on the server. It is defined as a dashboard-wide order that other views (such as the planned Daily view) can apply.
- A text filter over change names, ticket IDs, and card titles. Each card shows its latest activity date.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `work-dashboard`: Ongoing work becomes a board with derived columns, short-lived ticket and ad-hoc cards, a persisted priority order, and a filter. The preferences bound covers the priority order.

## Impact

- Client (`dashboard/src`):
  - A pure board module that derives columns, card kinds, expiry, and ordering, with unit tests.
  - The board template replacing the change grid and "Other ongoing work".
  - Native HTML drag and drop plus move actions, with no new dependency.
  - A `board.order` field in `Preferences`.
- Server: a bounded read-only endpoint that returns unlinked observations (no ticket, no change) for the selected repositories within the 7-day window. Change stage uses the existing `/api/activity/changes` summary and catalog task counts. Ticket cards use the existing ticket-grouped timeline over a 7-day window.
- No journal schema, MCP contract, or `worklog init` changes. Claude Code and Codex parity is unaffected.
- Sequencing:
  - Apply after `add-ticket-and-repository-links`, which provides ticket links and the quiet indication.
  - `add-daily-prep` should adopt the shared priority order for Today and its non-done set. Record this as a coordination note in that change when it is applied. This change does not modify it.
- Out of scope:
  - An archive action on the board.
  - A separate `/board` route.
  - Moving cards between columns.
  - Renaming the nameless unlinked item in History.
