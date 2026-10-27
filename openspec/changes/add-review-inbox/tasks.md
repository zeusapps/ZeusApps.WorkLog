# Tasks

Apply through the orchestrator workflow (`add-agent-workflow`). Requires `fix-dashboard-reliability` to be applied first; verify `catalog.revision` includes content fingerprints and catalog documents expose `fingerprint` before starting.

## 1. Open comments overview (server)

- [x] 1.1 Add `POST /api/reviews` with scope, repository filtering, truncation, and scoped unavailable notices (design Decision 1); verify with .NET tests covering active/archived/all scope, fenced-code exclusion, and an unreadable document reported as unavailable
- [x] 1.2 Add the per-document parsed-marker cache keyed by the catalog fingerprint (length and mtime) and bounded by the current catalog (Decision 1); verify with a .NET test that a second listing over unchanged files does not reparse, including above 512 documents

## 2. Read baselines (server)

- [x] 2.1 Implement the `dashboard-reads/` store (index, content-addressed blobs, 300-key and 64 MiB bounds, LRU eviction, unreferenced-blob deletion, atomic writes, corrupt-index recovery) (Decision 2); verify with .NET tests for eviction, deduplication, and corrupt-index recovery
- [x] 2.2 Add `/api/reads/get`, `/api/reads/mark` (with a 409 when the revision is stale), and `/api/reads/summary`; verify with host tests including the unauthorized-request rejection

## 3. Client

- [x] 3.1 Add the Open review comments panel with scope switch and repository filtering, and per-change and per-document counts on cards and in the reader (Decision 6); verify with Angular unit tests for grouping and counts
- [x] 3.2 Pin `diff` (jsdiff) to an exact version in `dashboard/package.json` and update the lockfile; verify `npm ci` and `npm test` pass and that the version is recorded in `docs/dashboard-validation.md`
- [x] 3.3 Implement the diff-to-block mapping, removed-passage markers, source-view highlighting, and Previous/Next change navigation with `[`/`]` keys (Decision 4); verify with Angular unit tests mapping known diffs to expected `data-start` blocks
- [x] 3.4 Implement baseline movement rules: first open, Mark as read, Mark change read, and own-write advancement (Decision 3); verify with unit tests that an own comment save after reading produces no highlight, and that a save after an unread agent edit keeps the agent edit highlighted
- [x] 3.5 Implement the Removed since last read panel from baseline markers compared by id or exact text; verify with a unit test
- [x] 3.6 Show Updated since read chips from `/api/reads/summary` versus catalog fingerprints, clearing on open when the revision is unchanged (Decision 5); verify with a unit test

## 4. Acceptance and docs

- [x] 4.1 Add Playwright scenarios: comments listed across two changes and opened; agent removes a marker and the overview updates; agent edits a read design and highlights plus the addressed comment appear; Mark as read clears; own comment is not highlighted; archived scope lists a leftover marker read-only. Verify `npm run test:e2e --prefix dashboard` passes against a packaged build with isolated data
- [x] 4.2 Document the overview, read baselines, their storage location, bounds, and reset in `docs/installation.md` and `README.md`; verify the links resolve
- [x] 4.3 Run the full validation set and record results, including the idle-cost measurement from `fix-dashboard-reliability` repeated with the overview visible, in `docs/dashboard-validation.md`
