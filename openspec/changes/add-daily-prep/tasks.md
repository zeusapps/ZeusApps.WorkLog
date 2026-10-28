# Tasks

Apply through the orchestrator workflow (`add-agent-workflow`). Requires `fix-dashboard-reliability`, `add-ticket-and-repository-links`, and `add-ongoing-board`; verify `journalSnapshot` is in the catalog, `dashboard/src/links.ts` exports `ticketUrl`, and `dashboard/src/board.ts` exports `buildBoard` and `byPriority` before starting.

## 1. Server

- [ ] 1.1 Add the previous/next recorded-day lookups and last-recorded-day selection in `DashboardJournal` (design Decision 1); verify with .NET tests for Monday after Friday, skipped days off, a timezone midnight boundary, repository filtering, and an empty journal
- [ ] 1.2 Add `POST /api/daily` reusing `TimelineService.Project` for the day digest with primary and collapsed entries, status as of day end, the unlinked bucket split into one item per entry, and ordering (Decisions 1 and 2); verify with .NET tests for status as of day end when a later done exists, ordering, two unlinked entries on one day becoming two items titled by their summaries, a missing summary, and a rejected explicit `day` not earlier than today

## 2. Client

- [ ] 2.1 Add `dashboard/src/daily.ts` with `dailyToday` (Decision 3); verify with Vitest cases for an In-progress card listed, a quiet In-progress card left out, a Done card with an observation today listed, a change with all tasks ticked left out, no duplicates, the board priority order, and the Proposed count
- [ ] 2.2 Add the `/daily` route, navigation entry, Yesterday/Today/Blockers rendering with collapsed others, ticket links, previous/next day navigation via `?day=`, loading of the Ongoing inputs, and refresh on `journalSnapshot` change and day rollover (Decision 5); verify with Angular unit tests for rendering order, navigation, and that the view performs no preference save
- [ ] 2.3 Implement Copy for daily with text and Markdown formats, the links toggle, remembered preferences, and clipboard fallback (Decision 4); verify with unit tests asserting the exact copied text for a fixture digest in both formats

## 3. Acceptance and docs

- [ ] 3.1 Add Playwright scenarios with a fake clock: Monday shows Friday; stepping back skips empty days; the copied plain text matches the expected block; a new observation for today appears under Today after refresh; reordering two In-progress cards on the board changes their order under Today. Verify `npm run test:e2e --prefix dashboard` passes against a packaged build with isolated data
- [ ] 3.2 Document the Daily view, the last-recorded-day rule, how Today follows the board, and the copy formats in `README.md`; run the full validation set and record results in `docs/dashboard-validation.md`
