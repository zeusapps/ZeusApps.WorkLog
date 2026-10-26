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
