# Tasks

Apply through the orchestrator workflow (`add-agent-workflow`). Requires `fix-dashboard-reliability` and `add-review-inbox`; verify `/api/reviews` accepts `scope: "archived"` before starting group 2.

## 1. Server

- [x] 1.1 Add `archivedOn` to `ChangeInfo` from the archive directory prefix (design Decision 1); verify with a `SourceCatalogTests` case for dated and undated archive directories
- [x] 1.2 Add `POST /api/activity/changes` with the read-only grouped query (Decision 2); verify with .NET tests for latest-status selection on equal timestamps (ordered by id), repository filtering, and a missing journal
- [x] 1.3 Add `POST /api/open` with enum validation, containment through `DocumentService.Resolve`, URI building, and the `IEditorLauncher` seam (Decision 3); verify with .NET tests for an unknown preset rejected, traversal and junction escape rejected without launch, unauthorized and cross-origin rejected, archived document allowed, and percent-encoding of paths with spaces and non-ASCII characters
- [x] 1.4 Confirm VS Code and Cursor URL handler formats from current vendor documentation and record them in `docs/dashboard-validation.md`

## 2. Client

- [x] 2.1 Add the `/archive` route, navigation entry, sorting, name filter, activity and leftover-comment display, and read-only opening; verify with Angular unit tests for ordering and filtering
- [x] 2.2 Add the editor preference selector and Open in editor actions in the reader header (current position), headings, search results, and review entries (Decisions 4 and 5); verify with unit tests that the computed line matches the first visible block

## 3. Acceptance and docs

- [x] 3.1 Add Playwright scenarios: Archive view ordering and filter, opening an archived change read-only, and Open in editor posting the expected document and line (server running with a recording launcher via a test-only environment switch that the packaged build ignores unless set); verify `npm run test:e2e --prefix dashboard` passes
- [x] 3.2 Manually verify on Windows that VS Code opens a fixture document at the expected line through the packaged build, and record the result as a live observation in `docs/dashboard-validation.md`
- [x] 3.3 Document the Archive view and the editor presets, including the security boundary, in `README.md` and `docs/installation.md`; run the full validation set and record results
