# Dashboard validation

Recorded on 2026-10-26 on Windows 10.0.26300.0, using .NET SDK 10.0.113, Node 24.13.0, and headless Microsoft Edge. All journals, review comments, worktrees, and unresolved-store cases used isolated synthetic fixtures under ignored artifacts directories.

## Build and service checks

- Locked .NET restore and clean npm install passed.
- Release build passed with no warnings or errors.
- Full .NET suite: 219 passed, none skipped. Review-service coverage includes marker-only edit/delete, unique-ID matching after earlier edits, UTF-8/UTF-16 BOM and CRLF retention, stale/missing/ambiguous markers, legacy upgrades, code-example isolation, source containment, and bounded Unicode-safe reference excerpts.
- Angular unit tests: 9 passed, covering source-line mapping, Markdown security, structured and legacy review markers, read-only controls, retained edit drafts/conflicts, selection defaults, repository scope, timezone boundaries, and a delayed catalog poll overlapping a document switch. The switching regression failed before the fix and passed afterward.
- Negative build probes confirmed that stale production assets and a missing index fail the build. The original generated files were restored afterward.
- The packaged dashboard help exposes repository, data-directory, port, and no-open options.
- Packaged bootstrap checks passed for self-contained launch/version, MCP startup and EOF, and offline update status.
- Distinct 0.0.1/0.0.2 packaged fixtures passed launcher argument/stream/lifetime checks and concurrent activation, retry, cursor, settings, rollback, recovery, and retained-payload checks.
- Release v0.3.0 also passed those mixed-version checks against the independently downloaded public v0.2.0 baseline (ZIP SHA-256 `63e14a543fa487adaebfa7b3d70d00ef9000297f2d501666c6e01b102ade0562`). Its original MCP PID/stream survived activation and rollback, and new processes adopted v0.3.0. Evidence is under artifacts/update-smoke/1ef6270018ff496f84d4e42ef24e7444/evidence.json.

## Browser acceptance

All eleven packaged browser scenarios passed, including the user acceptance follow-ups for dark-by-default appearance, remembered light mode, the embedded favicon, delayed/out-of-order document switches through nested specs, referenced review notes, edit/cancel in rendered and source views, confirmed deletion, legacy marker upgrades, and conflicts when a note changes or disappears. Both reader themes were checked through computed styles and screenshots. Archived notes lose their mutation controls even when their text revision is unchanged.

The packaged executable was exercised with Node.js and the OpenSpec CLI absent from its runtime PATH. Installed-.NET lookup was pointed at a nonexistent directory; the self-contained package continued to serve its interface and local documents. Missing CLI task totals were shown as unavailable, not zero. Tests covered design-first reading, repository-scoped search, no automatic remote resources, retained review text and stale-block rejection, timeline and observation continuation, archive-safe links, restart authorization, repeat no-open launches, concurrent MCP logging while saving a review, section/task links, missing targets, keyboard navigation, and an unresolved store without a local fallback.

Browser screenshots, traces on failure, and the HTML report are generated in dashboard/test-results and dashboard/playwright-report. Fixture data remains in artifacts/dashboard-e2e for inspection; these directories are ignored by Git.

## Package evidence

| Measure | Existing local v0.2.0 build | Dashboard v0.3.0 build |
| --- | ---: | ---: |
| Payload worklog.exe | 80,916,590 bytes | 109,535,203 bytes |
| Windows ZIP | 68,897,154 bytes | 81,676,017 bytes |

The release-validation package is artifacts/release-v0.3.0-local/worklog-0.3.0-win-x64.zip. Its payload is 28,618,613 bytes larger than the existing local build; the ZIP is 12,778,863 bytes larger. Frontend production assets total approximately 474 kB before transfer compression. Runtime assets, the favicon, and third-party license notices are embedded in the payload, preserving the package file list. Journal schema remains 2 and launcher/shared-data contracts remain 1.

To launch the verified local build from the repository root:

```powershell
.\artifacts\release-v0.3.0-local\worklog-0.3.0-win-x64\worklog.exe dashboard
```

Stop the previous dashboard command before launching the updated package; a repeat launch otherwise reuses its existing process. Dark mode is the default, and the top-bar toggle remembers a light/dark selection.

Review documents after the agent finishes editing. Revision/block checks detect stale selections, but concurrent external edits between the last reread and replacement remain outside the supported workflow. Task links follow current numbering and do not preserve historical identity through renumbering.

## Reliability fixes (fix-dashboard-reliability)

Recorded on 2026-10-27 on Windows 10.0.26300.0, using .NET SDK 10.0.113, Node 24.13.0, OpenSpec CLI 1.13.1, and headless Microsoft Edge. All data used isolated synthetic fixtures under ignored artifacts directories.

### Validation results

- Angular unit tests: 31 passed in 4 files. New coverage: relative and custom ranges evaluated on different dates, legacy ranges, no dates written on History loads, invalid timezone and inverted range rejection (including a zone the browser accepts but the server rejects with 400, which is explained inline, not remembered, and leaves the previous zone and results in place, and a saved zone the server rejects being ignored at start, while a start-up probe that fails for another reason, or a 400 with another message, keeps the saved zone and lets start-up continue), transient day focus, day boundaries in zones that skip midnight (Africa/Cairo 2026-04-24, Asia/Beirut 2026-03-29) and zones that do not, 100/200-entry preference caps, a single preferences notice for repeated save failures, idle polls (no searches, timeline queries, document transfers, or preference saves), refreshes on revision, journal snapshot and day rollover, and the unchanged-document answer keeping the rendered document.
- Full .NET suite (Release): 232 passed, none skipped. New coverage: CLI results reused until their inputs change (per-source task progress, config reload, 60 s bound), catalog revision on document edits, journal snapshot, cached search reads and oldest-first eviction, search notices limited to the selected repositories, headings inside fenced code, 413 for oversize preferences, 404 for an unknown link source, and the `knownRevision` request and `unchanged` response, a timed-out CLI result reused for a 30 s backoff (one spawn inside the window, one after it, and a changed input stamp bypassing it), a junction or symlink at a nested directory below a change being rejected as an escape, a link whose target has a linked parent component being resolved fully and rejected (the Windows junction form ran here; the Unix symlink form of the same scenario has not been run), a link loop failing closed, and a link inside the source being followed once with unchanged document order and stamps.
- Package built with `scripts/package.ps1` (version 0.3.0, fresh output directory).
- Browser acceptance against the packaged build (`WORKLOG_DASHBOARD_EXECUTABLE` set to the packaged `worklog.exe`, `WORKLOG_DASHBOARD_NO_NODE=true`): 16 of 16 scenarios passed, including the new Escape, day-focus reload, rolling range with the browser clock advanced three days, bounded reading positions, and scoped search scenarios.
- Browser acceptance against the Release build with the OpenSpec CLI on the PATH: 15 of 16 scenarios passed. The scenario "keeps history available when a declared store cannot be resolved" fails with OpenSpec CLI 1.13.1 because the declared store resolves, so the local change it expects to be hidden is listed. It is unchanged by this work and tracked separately as QA-007. It passes in the packaged run above, where the CLI is absent from the PATH.

### Idle cost before and after

These are fixture measurements, not user data. Setup: the three-repository dashboard fixture (a repository with a linked worktree, a second repository, and a repository whose declared store cannot be resolved), a Release dashboard on a free port, Edge with one open document (an approximately 870-byte design document) and the search term "timeline" across all repositories. After 15 s of settling, one 60 s window was recorded. OpenSpec invocations were counted by a logging shim placed first on the PATH, git invocations with `GIT_TRACE2_EVENT`, and API requests, document response bytes and preference writes in the browser. "Before" is the build at baseline commit `bf65aeb`; "after" is this change.

| Per minute, idle | Before | After |
| --- | ---: | ---: |
| OpenSpec CLI spawns | 49 | 8 |
| git spawns | 24 | 4 |
| API requests | 42 | 60 |
| `/api/document` response bytes | 10,440 | 2,880 |
| Preference writes (`PUT /api/state`) | 2 | 0 |
| Search requests | 12 | 0 |
| Timeline requests | 4 | 0 |

The request count rises because polling is no longer held back by slow discovery: before, each catalog request waited several seconds for CLI discovery (12 polls per minute), and after it returns in milliseconds (30 polls per minute). The additional requests are catalog and document polls; the unchanged document answer is 96 bytes instead of 870. The 8 OpenSpec and 4 git spawns after the change are the single refresh round at the 60 s bound.

### Cheap-tier cost of a catalog refresh

These are fixture measurements from an in-process harness (Release build, fake CLI command, fake clock, no dashboard and no port; synthetic fixture whose paths have about 16 components, deeper than a typical checkout). Each refresh bypasses the 2 s response cache but stays inside the 60 s CLI lifetime, so it measures only the cheap tier: change discovery, Markdown enumeration and the length and write-time stamps. Median of 40 refreshes after a warm-up. "Before" canonicalized the source root and every path for every file; "after" canonicalizes the root once per discovery and takes attributes, length and write time from the directory entry, fully canonicalizing only reparse points.

| Fixture | Before (median, p90) | After (median, p90) |
| --- | ---: | ---: |
| 60 Markdown files (5 active and 10 archived changes) | 22 ms, 24 ms | 1.0 ms, 1.6 ms |
| 1,280 Markdown files (20 active and 300 archived changes) | 727 ms, 835 ms | 17.5 ms, 19.3 ms |

The catalog revision and a hash over every change's document order, content stamp and task stamp were identical before and after for both fixtures. A timed-out result is now remembered for 30 s (never longer than the 60 s lifetime): a timed-out `openspec context` is still reported as the source error, while a timed-out git or `openspec list` result leaves worktrees or task progress out for the backoff window. So a hanging CLI is spawned at most twice a minute per checkout instead of on every refresh.

## Review inbox (add-review-inbox)

Recorded on 2026-10-27 on Windows 10.0.26300.0, using .NET SDK 10.0.113, Node 24.13.0, OpenSpec CLI 1.13.1 (for the Release-build run), and headless Microsoft Edge. All documents, comments and baselines used isolated synthetic fixtures under ignored artifacts directories.

### Dependency

- `diff` (jsdiff) 9.0.0, pinned exactly in `dashboard/package.json` and `package-lock.json`. It was published on 2026-04-13, more than two weeks before use. The API used is `diffLines(old, new, { timeout })`, checked against the library's current documentation; a timed-out comparison returns `undefined` and is reported as incomplete.

### Validation results

- Locked restore, clean `npm ci --prefix dashboard` and Release build passed with no warnings.
- Full .NET suite (Release): 262 passed, none skipped. New coverage: marker listing by active, archived and all scope, repository filtering, fenced-code exclusion, truncation without splitting surrogate pairs, unreadable documents and sources reported as unavailable (scoped to the requested repositories), no reparse and no path resolution on a warm listing (the marker cache is keyed by catalog identity and the catalog fingerprint, and a miss resolves each change directory once), a document deleted or replaced by an unreadable one between listings not served from the cache, a marker-cache bound of 512 entries with least-recently-used eviction in which a second listing over 520 documents reparses only the 8 over the bound (a parse count, over three rounds), listing misses leaving the reader's document cache alone, zero reparses over 50 repeated listings, catalog document fingerprints with unchanged revision semantics, the baseline store (exact text, deduplication, 300-key and 64 MiB bounds, least-recently-read eviction across many marks over many days, unreferenced-blob deletion, corrupt and oversized indexes reset, an index of another schema version preserved untouched and reported unavailable, an index held open with `FileShare.None` during `Mark`, `Get` and `Entries` writing and deleting nothing, a blob held open during `Get` keeping its entry, tampered entries and damaged blobs), and the HTTP endpoints (401 for every new endpoint before authorization with nothing written, 409 for a stale revision with the baseline kept, 400 for escaping paths and for malformed bodies (`{}`, a null or empty `document`, a missing revision) on `reads/get`, `reads/mark` and `reviews`, 404 for unknown changes, baseline surviving an archive move, BOM and UTF-16 documents).
- Angular unit tests: 81 passed in 8 files. New coverage: grouping and counts per change and document, scope filtering, diff-to-block mapping with known diffs (blocks by `data-start`, removed passages by position, escaped removed text, stops), source-view segments, first open, Mark as read, Mark change read, own comment save after reading (no highlight) and after an unread agent edit (still highlighted), removed-since-last-read by id and by exact legacy marker text, updated-since-read chips and clearing on open, `[` and `]` handling (ignored while typing), and idle polling making no overview requests while the catalog revision is unchanged. Removing the baseline advance after own writes made the two own-write tests fail.
- Browser acceptance against the packaged build (`scripts/package.ps1`, `WORKLOG_DASHBOARD_EXECUTABLE` set to the packaged `worklog.exe`, `WORKLOG_DASHBOARD_NO_NODE=true`): 19 of 19 scenarios passed, including the three new ones (comments across two changes opened at their note and following an agent removing a marker; an agent's edit of a read design highlighted with the addressed comment listed, keyboard navigation, source view, Mark as read, baseline kept across reload and own comment not highlighted; an archived change's leftover marker listed read-only).
- Browser acceptance against the Release build with the OpenSpec CLI on the PATH: the new scenarios pass; the full serial run stops at the existing scenario "keeps history available when a declared store cannot be resolved" (tracked as QA-007, unchanged), so the three later scenarios were run on their own and passed.
- Two defects were found by these browser runs and fixed: the card hint did not clear after Mark as read because 100-ns write times exceed what a JavaScript number holds exactly (fingerprints now travel as text), and a heading whose slug starts with `review-` was mistaken for a comment anchor (comment links now use `note:<line>`, which no heading slug can produce).

### Idle cost with the overview visible

This record replaces an earlier one that reported 0 OpenSpec and 0 git spawns. That window (60 s) missed the 60 s CLI refresh round, and its method was invalid: git was filtered on `child_start` events, which a directly invoked git never emits, and the logging shim was an `openspec.cmd` that the dashboard does not run (`LocalCommand` runs `node` with `node_modules/@fission-ai/openspec/bin/openspec.js` found on the PATH).

Method: the three-repository fixture, the packaged build (`scripts/package.ps1`, `worklog.exe`), a free port with `--no-open`, a disposable data directory, headless Edge on the Ongoing page with the review overview and change cards visible, 15 s of settling, then one 135 s window that spans two refresh rounds. OpenSpec spawns were counted by a logging `openspec.js` placed in a `node_modules/@fission-ai/openspec/bin` directory first on the PATH, which then runs the real CLI. git spawns were counted from `GIT_TRACE2_EVENT` `start` events (one per git process). Requests were counted in the browser.

| 135 s window, idle, overview visible | Count | Per 60 s round |
| --- | ---: | ---: |
| OpenSpec CLI spawns | 16 | 8 |
| git spawns | 8 | 4 |
| API requests | 64 (all `GET /api/catalog`) | |
| `/api/reviews`, `/api/reads/*`, `/api/search`, `/api/timeline`, `/api/document`, preference writes | 0 | |

The per-round cost (8 OpenSpec and 4 git spawns) equals the reliability record, so the overview adds no idle cost. The overview and the read hints are requested at start and again only when the catalog revision changes.

### Warm review listing

The listing `POST /api/reviews` with `scope: "all"` is refreshed on every catalog revision change. It answers an unchanged document from a marker cache keyed by the document's catalog identity (source, variant, change, path) and the catalog's `length:lastWriteTicks` fingerprint, so a warm listing resolves no path and stats no file. On a miss each change directory is resolved once and its documents are resolved below it. The cache is bounded by the catalog, not by a count: after each listing, the entries of documents that are no longer in the current catalog snapshot are removed, and nothing is removed by age or number. A listing over one scope or repository filter therefore keeps the entries of the other documents warm. A listing is at most one catalog refresh (2 s) behind a file, which is the freshness the client already has.

In-process measurements of `DocumentService.ReviewsAsync` (Release, fixture of 900 synthetic documents with one marker each, 10 changes active and 80 archived, 10 documents per change, `scope: "all"`, 12 warm calls after one cold call). They exclude HTTP and JSON, so they are lower than the earlier HTTP probe of the same shape.

| Documents in scope (`all`) | Before the marker cache | Cold call | Warm call |
| --- | ---: | ---: | ---: |
| 900 | 1,648 ms median (1,469 to 2,107 ms), every document reparsed on every call | 666 ms, 900 parses | 0.53 ms median (0.36 to 10.4 ms), zero parses |

The target of under about 50 ms is met at 900 documents, above the former 512 bound.

## Archive view and Open in editor (add-archive-browser-and-editor-open)

### Editor URL handler formats (task 1.4)

Confirmed on 2026-10-28.

| Preset | Format | Status | Source |
| --- | --- | --- | --- |
| VS Code | `vscode://file/{full path to file}:line:column`, for example `vscode://file/c:/myProject/package.json:5:10` | Confirmed by vendor documentation | VS Code docs, "Opening VS Code with URLs" in `docs/configure/command-line.md` (https://github.com/microsoft/vscode-docs), fetched with `ctx7` from `/microsoft/vscode-docs` |
| Cursor | `cursor://file/{full path to file}:line:column` | Not confirmed by vendor documentation. The Cursor documentation retrieved with `ctx7` (`/websites/cursor`) documents only `cursor://anysphere.cursor-deeplink/...` links (MCP and prompt installs), not file links. Community reports (Cursor forum, "Open in Cursor URL Handler" and "Does Cursor have a unique open scheme?") describe `cursor://file/{path}:{line}` working for local files, as expected of a VS Code fork. | Community reports only |

Decision: the Cursor preset uses the VS Code form because Cursor is a VS Code fork and community reports show it working. Until a live observation on Windows confirms it (see the live observation below), treat the Cursor preset as unverified; "System default app" is the fallback and a launch failure is reported with a suggestion to choose another preset. `line` is zero-based in the API and one-based in the link (`line+1`), the column is always 1, forward slashes are used, and every path segment is percent-encoded as UTF-8 except a leading drive designator such as `C:`.

### Validation results

Run on Windows against the working tree of this change, fixture runs and packaged runs labeled separately.

| Check | Result |
| --- | --- |
| `npm ci --prefix dashboard --no-audit --no-fund` | passed |
| `npm test --prefix dashboard` | 94 of 94 tests in 10 files passed (new: `archive.spec.ts`, `app.archive.spec.ts`; includes the source-view line count and the caret-based reading position) |
| `dotnet restore --locked-mode` | passed |
| `dotnet build -c Release --no-restore` | 0 warnings, 0 errors |
| `dotnet test -c Release --no-build --no-restore` | 350 of 351 passed (11 new in `ArchiveAndOpenTests`, including a `.md` link to a `.cmd` file being rejected for all three presets and a `.md` link to a `.md` file still opening). The one failure, `DevInstallTests.PreflightRefusesStaleUnmarkedAndCorruptPackagesAndChangesNothing`, expects the message "Source file" but receives "Source directory dashboard\src changed after ...": it compares the working tree's modification times with a fixture package, so the result depends on directory-versus-file modification-time ordering in the tree under test. It passed in one QA run, failed in another, and fails on a clean `c53e647` worktree, so it is unrelated to this change (including the open endpoint) and was not fixed here. |
| Browser acceptance, fixture run against the Release build (`npm run test:e2e --prefix dashboard`, OpenSpec CLI on the PATH) | the serial run stops at the existing scenario "keeps history available when a declared store cannot be resolved" (QA-007, pre-existing, unchanged): 15 passed, 1 failed, 6 not run. With that scenario excluded (`--grep-invert "declared store"`): 21 of 21 passed, including the three new scenarios. |
| Browser acceptance, packaged run (`scripts/package.ps1` output, `WORKLOG_DASHBOARD_EXECUTABLE`, `WORKLOG_DASHBOARD_NO_NODE=true`) | 22 of 22 passed on the current tree (rerun in the fix round from a fresh `scripts/package.ps1` build), including the Archive view, the Open in editor, the source-view reading-position and the declared-store scenarios |

The new scenarios: the Archive view lists three archives newest first with the undated one last, filters case-insensitively, shows "No activity recorded" and "Recorded done" with a date separate from the archive date, shows the leftover comment count, and opens an entry read-only; and Open in editor posts the expected document and line for a heading, the reader position, a search result and an archived document, with the preset selecting the scheme and being saved in the preferences, and a forged request (path traversal, unknown preset) being rejected without a launch. A third scenario scrolls the source view of a long wrapped paragraph and asserts the recorded line is the first visible source line (3), not the scroll distance divided by the row height. These scenarios use the test-only recording launcher (`WORKLOG_DASHBOARD_TEST_LAUNCH_LOG` naming a file; the dashboard appends targets to it instead of launching). `dashboard/e2e/server.mjs` sets it; the packaged build ignores it unless set.

Server tests cover: archive dates from directory prefixes (dated, undated, impossible dates); latest status with ties on the occurrence time broken by id; repository filtering; missing journal returns empty and creates nothing; an unsupported schema is refused without migration; URI building (drive colon kept, spaces and non-ASCII encoded); open for active and archived documents, unknown presets and negative lines (400), traversal, absolute and encoded paths (400), a directory junction escaping the source (change unavailable), unauthorized (401), cross-origin, cross-site and wrong-host requests (403) with no launch recorded in any rejected case, ignored extra client fields, and an unavailable editor (503 with "Could not open Cursor. Choose another editor.").

### Live observation (task 3.2)

The implementation run did not launch a real editor. The following is a live observation by the maintainer, not an automated result.

On 2026-10-28 the maintainer installed dev build `0.3.9002 dev c53e647 dirty` (built from the reviewed working tree) on their Windows installation through `scripts/install-dev.ps1` Switch, restarted their dashboard, and observed that "Open in editor" from the reader header and the outline heading pencil action both opened VS Code at the expected position.

Not observed in the live check: the source view, the Cursor preset, and System default app. The Cursor link format remains unconfirmed by vendor documentation and was not live-checked; treat the Cursor preset as unverified.
