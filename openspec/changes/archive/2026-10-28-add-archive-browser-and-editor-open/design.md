# Design

## Context

See proposal.md for motivation. Relevant current code:
- `SourceCatalog.DiscoverAsync` lists archived directories under `openspec/changes/archive/` and strips the `yyyy-MM-dd-` prefix into the logical name (`ArchivePrefix()`).
- `ChangeInfo` has `Archived`, `DirectoryName`, `Name`, `Variant`, and `Documents`.
- `DocumentService.Resolve` enforces containment and returns the canonical file path.
- The journal table `entries(id, occurred, repository, change_name, status, payload)` has an index on `(occurred, id)` and is opened read-only by `DashboardJournal` with a schema check.
- The design of the original dashboard explicitly excluded endpoints for shell commands. This change adds one narrow exception, recorded in the modified boundary requirement.
- After `add-review-inbox`, `/api/reviews` supports `scope: "archived"`.

## Goals / Non-Goals

**Goals:** a fast archive list; open-in-editor with no client-supplied paths or commands; a reusable activity summary.

**Non-Goals:**
- Configurable editor command lines.
- Opening folders or arbitrary repository files.
- Remote Git hosting links (see `add-ticket-and-repository-links`).
- Un-archiving or any OpenSpec mutation.

## Decisions

### 1. Archive date and view
`ChangeInfo` gains `archivedOn`, the `yyyy-MM-dd` prefix of the archive directory name, or `null` when absent. The `/archive` route renders entries from the catalog filtered by the shared repository selection, sorted by `archivedOn` descending, then logical name. The name filter is client-side. Leftover comment counts come from one `/api/reviews` call with `scope: "archived"`. Activity comes from Decision 2, matched by repository (any of the change's repositories) and logical change name.

### 2. Change activity endpoint
`POST /api/activity/changes { repositories: string[] }` returns `[{ repository, change, firstAt, lastAt, lastStatus }]`. It is implemented in `DashboardJournal` as a single read-only query:

```sql
SELECT repository, change_name, MIN(occurred), MAX(occurred) FROM entries
WHERE change_name IS NOT NULL [AND repository IN (...)] GROUP BY repository, change_name
```

A second query takes the status of the latest row per group (`ORDER BY occurred DESC, id DESC`, using a window function or a correlated subquery). Ticks are converted to instants. The query uses the same schema check, and returns empty for a missing journal. No index changes are needed for personal-size journals. The analyzer measures it on a synthetic journal of 50k rows during review.

### 3. Editor presets and launcher
`POST /api/open { document: DocumentRequest, line: int, editor: "vscode"|"cursor"|"system" }`:
1. Validate `editor` against the enum and `line >= 0`.
2. Resolve the file with `DocumentService.Resolve(snapshot, request)` (read rules, so archived documents are allowed).
3. Build the target:
   - `vscode`: `vscode://file/<path>:<line+1>:1`
   - `cursor`: `cursor://file/<path>:<line+1>:1`
   - `system`: the canonical file path itself.

   The path is converted to forward slashes, and each segment is percent-encoded except the drive colon.
4. Launch through an `IEditorLauncher` seam. The default uses `Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })`. Tests substitute a recording fake.
5. Return 204, or 503 with "Could not open <preset>. Choose another editor." on `Win32Exception`.

URI schemes avoid resolving `code.cmd` and quoting through `cmd.exe`. They also need no knowledge of install locations, and use the editors' documented URL handlers. Fetch current VS Code and Cursor URL handler documentation via `ctx7` or the vendors' docs during implementation, and record the confirmed formats in `docs/dashboard-validation.md`.

Alternative: run `code --goto`. Rejected, because of `.cmd` resolution and argument quoting on Windows, and because adding a new editor would mean a command template, which turns into a general command endpoint.

### 4. Position mapping
The reader's current line is the `data-start` of the first rendered block whose top is at or below the reader's top edge. Headings use their `line`, search results use `line`, and review entries use `line`. Source view uses the first visible line.

### 5. Preference
`preferences.editor` defaults to `vscode`. A small selector next to the action switches it, and the choice is remembered through the bounded preferences from `fix-dashboard-reliability`.

## Risks / Trade-offs

- Shell-executing a URI could be abused if an attacker controlled the target. → The target is built only from a server-resolved contained path and a fixed scheme, and requests require the authorized same-origin session.
- An editor's URL handler may not be registered (for example, a portable install). → Report the failure clearly, and keep `system` as a fallback.
- `system` opens Markdown in whatever app is associated, possibly a browser. → Label the preset "System default app".

## Migration Plan

Additive. Older payloads ignore `editor` and the new endpoints.
