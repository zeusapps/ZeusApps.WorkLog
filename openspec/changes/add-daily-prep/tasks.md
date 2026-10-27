# Tasks

Apply through the orchestrator workflow (`add-agent-workflow`). Requires `fix-dashboard-reliability` and `add-ticket-and-repository-links`; verify `journalSnapshot` is in the catalog and `dashboard/src/links.ts` exports `ticketUrl` before starting.

## 1. Server

- [ ] 1.1 Add the previous/next recorded-day lookups and last-recorded-day selection in `DashboardJournal` (design Decision 1); verify with .NET tests for Monday after Friday, skipped days off, a timezone midnight boundary, repository filtering, and an empty journal
- [ ] 1.2 Add `POST /api/daily` reusing `TimelineService.Project` for the day digest with primary and collapsed entries, status as of day end, ordering (Decision 2), Today rows, and the active-without-activity count; verify with .NET tests for status as of day end when a later done exists, ordering, deduplicated Today, and a rejected explicit `day` not earlier than today

## 2. Client

- [ ] 2.1 Add the `/daily` route, navigation entry, Yesterday/Today/Blockers rendering with collapsed others, ticket links, previous/next day navigation via `?day=`, and refresh on `journalSnapshot` change and day rollover (Decision 4); verify with Angular unit tests for rendering order and navigation
- [ ] 2.2 Implement Copy for daily with text and Markdown formats, the links toggle, remembered preferences, and clipboard fallback (Decision 3); verify with unit tests asserting the exact copied text for a fixture digest in both formats

## 3. Acceptance and docs

- [ ] 3.1 Add Playwright scenarios with a fake clock: Monday shows Friday; stepping back skips empty days; the copied plain text matches the expected block; a new observation for today appears under Today after refresh. Verify `npm run test:e2e --prefix dashboard` passes against a packaged build with isolated data
- [ ] 3.2 Document the Daily view, the last-recorded-day rule, and the copy formats in `README.md`; run the full validation set and record results in `docs/dashboard-validation.md`
