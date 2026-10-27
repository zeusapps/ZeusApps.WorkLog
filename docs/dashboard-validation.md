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
