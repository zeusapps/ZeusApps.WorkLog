# Tasks

Apply through the orchestrator workflow from `add-agent-workflow` (developer implements; QA and code-analyzer review). Design references are to `design.md` decisions.

## 1. History range and timezone (A)

- [ ] 1.1 Add the `history.range` model, effective-range computation, legacy handling, and presets 7/30/90/custom (Decision 1); verify with Angular unit tests that a relative range evaluated on two different dates yields different bounds and that legacy `from`/`to` without `range` yields relative 30
- [ ] 1.2 Stop persisting `from`/`to` on load and persist timezone only on validated user change; verify with a unit test that loading History twice does not write dates and that an invalid timezone is rejected inline without a query
- [ ] 1.3 Make day focus transient with a "back to range" control; verify with a unit test and a Playwright scenario that a reload after focusing a day restores the remembered range
- [ ] 1.4 Validate that From is not after Through before querying; verify with a unit test

## 2. Bounded preferences (B)

- [ ] 2.1 Cap `positions` (100) and `documents` (200) as most-recently-used maps and debounce scroll saves to 1 s (Decision 2); verify with a unit test that opening 150 documents keeps 100 positions and keeps the latest
- [ ] 2.2 Show a single preferences notice on save failure instead of the global error, and return 413 from the server for oversize state; verify with a .NET test for 413 and a unit test that repeated failures produce one notice

## 3. Escape and keyboard (C)

- [ ] 3.1 Implement focus-aware Escape (Decision 3); verify with a Playwright scenario: type a comment, press Escape, and confirm the reader stays open with text retained; press Escape again and confirm the reader closes

## 4. Search scope and small fixes (E, F)

- [ ] 4.1 Scope search `unavailable` notices to the requested repositories (Decision 4); verify with a .NET test where an unselected repository without a source does not appear in `unavailable`
- [ ] 4.2 Ignore `#` lines inside fenced code for section labels; verify with a .NET test
- [ ] 4.3 Return 404 for unknown link sources; verify with a .NET host test

## 5. Poll cost

- [ ] 5.1 Split catalog discovery into cheap and expensive tiers with the invalidation rules and concurrency of Decision 5; verify with `SourceCatalogTests` using a counting fake `LocalCommand` that repeated `GetAsync` calls within 60 s with no input changes do not re-run CLI commands, and that touching a `tasks.md` re-runs only that source's `openspec list`
- [ ] 5.2 Include Markdown file fingerprints in the catalog revision and add `journalSnapshot` to the catalog response; verify with .NET tests that editing a document changes the revision and that logging an entry changes `journalSnapshot`
- [ ] 5.3 Change the client poll to re-run search only on revision change, refresh history only on snapshot change or day rollover, and drop the every-5th-poll refresh (Decision 6); verify with Angular unit tests using a fake API that counts calls over simulated idle polls
- [ ] 5.4 Use cached reads for search and replace cache clearing with oldest-first eviction at 256 entries (Decision 7); verify with a .NET test that two consecutive searches over unchanged files do not increase `HashReads`
- [ ] 5.5 Measure idle cost before and after (CLI spawns and request count per minute with one open document and a search term, three-repository fixture) and record both in `docs/dashboard-validation.md`

## 6. Cleanup and acceptance

- [ ] 6.1 Remove the two REVIEW markers from `openspec/changes/archive/2026-10-26-add-work-dashboard/proposal.md`; verify `git grep -n "<!-- REVIEW" openspec/` returns no matches outside intentional fixtures
- [ ] 6.2 Add Playwright scenarios for the rolling range (fake clock advanced three days), bounded positions, and scoped search to `dashboard/e2e/dashboard.spec.ts`; verify `npm run test:e2e --prefix dashboard` passes against a packaged build
- [ ] 6.3 Run the full validation set (`npm test`, `dotnet test`, package, packaged browser acceptance) and update `docs/dashboard-validation.md` with results
