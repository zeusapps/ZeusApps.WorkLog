# Tasks

Apply through the orchestrator workflow (`add-agent-workflow`). Requires `add-ticket-and-repository-links`. Before group 3, verify that the quiet chip, ticket links, and `tickets` on client rows exist.

## 1. Server

- [ ] 1.1 Add `POST /api/ongoing/unlinked` (design Decision 3): entries with no tickets and no change in the selected repositories within `[from, to)`, newest first, capped at 200 with `truncated`, under the existing snapshot and authorization rules; verify with .NET tests for the filtering (ticket-only, change-only, extracted-ID-only, unlinked), the cap and `truncated`, the snapshot consistency, and rejection without authorization

## 2. Board model

- [ ] 2.1 Add `dashboard/src/board.ts` with `buildBoard` and card kinds, stage derivation, follow-up detection, 7-day expiry, and filter matching (Decisions 1 and 2); verify with Vitest cases for every scenario of the Ongoing board requirement, plus a change with unavailable task progress, a change matched in several repositories, and an extracted-ID ad-hoc card
- [ ] 2.2 Add priority ordering, the move operation, and pruning with the 500-entry cap, and export `byPriority` (Decision 4); verify with Vitest cases for unranked-first ordering, rank kept across a column change, a move that ranks the whole column without changing other columns, the stale-key pruning on save, and the cap

## 3. Ongoing view

- [ ] 3.1 Extend `Preferences` with `board.order`, switch `loadOngoing` to the 7-day window plus the unlinked endpoint, and expose `board` and the filter signal in `App`; verify with unit tests that rendering and idle refresh perform no preference save, and that preferences without `board` load unchanged
- [ ] 3.2 Replace "Pick up where you left off" and "Other ongoing work" with the board template: columns with counts, change, ticket, and ad-hoc cards, the latest activity date, follow-up and no-change labels, the quiet chip, the truncation note, the empty states, and the filter with its active indicator (Decision 6); verify with component tests and a screenshot in both themes
- [ ] 3.3 Add native drag and drop within a column and the Move up, Move down, and Move to top actions with focus retention and `aria-live` announcements (Decision 5); verify with unit tests that cross-column drops are rejected and keyboard moves save the order

## 4. Acceptance and docs

- [ ] 4.1 Update `dashboard/e2e/dashboard.spec.ts` for the new Ongoing structure and add Playwright scenarios with fixture data:
  - an unlogged proposal in Proposed
  - an in_progress change with no ticked tasks in In progress
  - a recorded-done change with unticked tasks in Done
  - ad-hoc cards at 6 and 8 days
  - a follow-up ticket
  - the filter
  - keyboard reordering restored after reload

  Verify `npm run test:e2e --prefix dashboard` passes.
- [ ] 4.2 Document the board, the column rules, card expiry, and reordering in `README.md`; run the full validation set (`dotnet test -c Release`, `npm test --prefix dashboard`, e2e) and record the results in `docs/dashboard-validation.md`
