# Proposal

## Why

A review of the v0.3.0 dashboard (commit `582f8bc`) found defects that affect everyday use.

- History stops rolling forward after the first visit, because absolute dates are saved on every load.
- Saved reading positions grow without bound until preference saves fail with an error on every scroll.
- Escape inside the comment box closes the document.
- Search reports itself incomplete because of notices from unrelated repositories.
- Test REVIEW comments were committed into the archived dashboard proposal.
- The 2-second poll re-runs OpenSpec discovery almost continuously. Each `openspec` CLI call takes about 1 s on Windows and the calls run one after another, which delays document refresh and comment saves.

These need fixing before the dashboard gains more features.

## What Changes

- History date ranges become either relative (last 7, 30, or 90 days, rolling with the current date) or custom. The default is last 30 days. Presets are added. A legacy saved range is treated as the default. Focusing a single day is temporary and offers a way back. The reporting timezone is saved only when the user changes it, and invalid timezones and reversed ranges are rejected inline.
- Saved per-document reading positions and document choices are bounded (most recent entries kept). A failed preference save shows one non-repeating notice instead of an error on every scroll.
- Escape inside the comment composer, delete confirmation, or any text input cancels that interaction first and does not close the reader.
- Search `unavailable` notices are limited to the sources and repositories in the current search scope.
- Polling becomes proportional to change:
  - OpenSpec root resolution and task progress are cached and refreshed only when relevant inputs change or a longer interval passes.
  - CLI calls run concurrently.
  - The catalog revision includes document content fingerprints.
  - Search and history refresh only when the catalog revision or the journal snapshot changes.
  - History rolls forward after midnight.
- An unchanged open document is not resent on each poll: the client sends the revision it has and the server answers "unchanged" without the text. Idle polls no longer rewrite saved preferences.
- History range boundaries are correct in timezones that skip midnight.
- Smaller fixes: an unknown link source returns 404 instead of 503. Search section labels ignore `#` lines inside fenced code.
- Remove the two leftover `<!-- REVIEW ... -->` markers from `openspec/changes/archive/2026-10-26-add-work-dashboard/proposal.md`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `work-dashboard`: History range semantics (relative vs custom, presets, temporary day focus, timezone persistence and validation). New requirements for bounded preferences and for idle refresh cost.
- `openspec-browser`: Search incompleteness scoped to the searched sources. Escape behavior inside text inputs.

## Impact

- Server: `src/WorkLog/Dashboard/SourceCatalog.cs`, `DocumentService.cs`, `DashboardHost.cs`, `DashboardJournal.cs`. A new cheap journal-revision read is added to the catalog response.
- Client: `dashboard/src/app.ts`, `app.html`, `models.ts`, `api.ts`. Preference and poll logic may be extracted into small services so they can be unit tested.
- Preferences file `dashboard-state.json` stays schema version 1, with a new optional `history.range` field. Older payloads ignore it.
- Tests: .NET dashboard tests, Angular unit tests, Playwright scenarios for rolling range, bounded positions, Escape, and scoped search.
- No journal schema, MCP, or package contract changes.
- Prerequisite: `add-agent-workflow` (apply through the orchestrator). Later changes (`add-review-inbox`, `add-archive-browser-and-editor-open`, `add-ticket-and-repository-links`, `add-daily-prep`) build on the poll and preference behavior fixed here.
