# Proposal

## Why

Archived changes are the record of finished work, but the dashboard can only reach them through search, deep links, or a History drill-down. There is no list of what was archived and when. Also, when reading a document the user often wants to jump into the actual file to edit it or look at nearby code. Today they must locate it by hand, because the browser cannot open local files and the dashboard deliberately exposes no paths or commands.

## What Changes

- Add an Archive view: archived changes in the selected repositories, newest archive date first, with name filter, archive date, documents, source, latest recorded work status, and any leftover review comments. Selecting one opens the read-only reader.
- Add a read-only change-activity summary: for each repository and change name, the latest recorded observation and status. The Archive view uses it, and later changes reuse it.
- Add "Open in editor" for a document at the current reading position, plus a per-heading action and actions on search results and review-comment entries.
  - Editor choices are a fixed set: VS Code, Cursor, or the system default application for Markdown. The choice is remembered.
  - The server resolves the file itself from the document identity, enforcing the same containment rules as reads, and launches only the chosen preset. No arbitrary paths or commands are accepted.
- Extend the local data boundary to allow this single launch operation.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `openspec-browser`: Adds the Archive view and the open-in-editor action.
- `work-dashboard`: The local data access boundary now permits launching a preset editor for a contained document. Adds the change-activity summary.

## Impact

- Server:
  - `POST /api/activity/changes`: read-only SQL over the `entries` table's `repository`, `change_name`, `occurred`, and `status` columns.
  - `POST /api/open`: preset launcher, using `Process.Start` with shell execution of a fixed URI scheme or the file itself.
  - Archive date parsing in `SourceCatalog`.
- Client: `/archive` route and navigation entry, editor preference, and editor actions in the reader, search results, and the review overview.
- Preferences gain `editor` (`vscode` | `cursor` | `system`).
- Tests: .NET tests for containment, editor enum validation, and the activity query; Playwright for the Archive view (the launch is verified through a fake launcher seam).
- No journal schema, MCP, or package contract changes.
- Prerequisites: `add-agent-workflow`, `fix-dashboard-reliability`, `add-review-inbox` (for leftover comment counts in archived scope).
