# Tasks

## 1. Journal read model and timeline semantics

- [x] 1.1 Add a read-only dashboard journal access path that validates the supported schema without creating or migrating storage; verify missing journals give empty history and unsupported schemas leave files unchanged in isolated persistence tests.
- [x] 1.2 Implement in-memory timeline projections bound to a client-supplied snapshot rowid, using existing work identity and period rules and lifecycle observations before the visible range; verify clipped starts, as-of upper bounds, repeated done events, subsequent work, and no invented dates or working hours with reporting fixtures.
- [x] 1.3 Add 50-row pages and 100-observation drill-down pages over the same snapshot rowid without server-side handles; verify more than 100 observations, backdated concurrent writes excluded until the next refresh, and identical results when a page is recomputed.
- [x] 1.4 Add traceable longest-span, completion, subsequent-work, and distinct-items-per-day insights; verify ticket-set identity, unresolved references, repository namespaces, and Warsaw daylight-saving boundaries against underlying fixture observations.

## 2. Repository and OpenSpec source catalog

- [x] 2.1 Discover saved repositories, legacy checkout paths, available worktrees, and the launch source without registry writes; verify missing checkouts, shared common directories, unregistered document-only sources, and unchanged saved identities in temporary Git fixtures.
- [x] 2.2 Resolve OpenSpec roots through read-only CLI context with selected-store propagation and the conservative CLI-unavailable local fallback; verify local roots, shared standalone stores, malformed store declarations, unavailable CLI, and source-specific failures using controlled fixtures.
- [x] 2.3 Catalog active and archived changes plus nested Markdown artifacts with stable logical identities; verify dated archive moves, already dated change names, duplicate archive matches, shared-store deduplication, and explicit source choices for conflicting checkouts.
- [x] 2.4 Enforce canonical source containment during catalog enumeration, document reads, and comment writes; verify encoded traversal, absolute paths, outside-root relative links, and Windows junction/symlink escape attempts cannot read or write unrelated files.

## 3. Document reading, targets, and search services

- [x] 3.1 Add bounded raw document reads with source provenance, revision hashes, missing/oversized states, and consistent-read retries; verify complete content is retained across transient replacement and the 2 MiB limit is reported explicitly.
- [x] 3.2 Read authoritative active-change task counts from OpenSpec's read-only change list, respecting schema-tracked files and the selected store; extract task positions separately for navigation and show progress as unavailable when CLI counts are absent, including archived changes. Verify displayed totals match supported CLI fixtures with comment/code examples and unknown markers, missing counts never become zero, and readable documents remain accessible.
- [x] 3.3 Resolve change/document/heading/task routes independently of archive paths, preferring current task numbers, with explicit missing/ambiguous-target fallbacks and no historical identity guarantee after renumbering; verify active-to-archive links, removed headings, reworded numbered tasks, missing/duplicate task numbers, ambiguous sources, and names containing spaces or non-ASCII characters.
- [x] 3.4 Add index-free case-insensitive literal search scoped by repository and active/archive/all state, returning up to 50 matches with excerpts, line positions, and a limit-reached flag; verify scope isolation, the limit flag, skipped and oversized sources, and matches without headings.
- [x] 3.5 Add modification-time-first change detection with hashing only on change, for the catalog and selected document; verify external edits and archive moves are detected while unchanged files are not rehashed.

## 4. Local host, Angular build, and preferences

- [x] 4.1 Add the dashboard command and loopback host adapter with repository/data-directory/port/no-open options, checking current adapter documentation and pinning any new dependencies; verify help, default port 47831, browser-open failure fallback, cancellation, and record payload size impact.
- [x] 4.2 Add the dashboard lock file and second-launch reuse, honoring --no-open on both initial and repeat launches; verify repeat launch prints the URL and exits, opens a browser only without --no-open, creates no extra listener, replaces stale locks, and rejects an unknown process on the port.
- [x] 4.3 Add authorized JSON endpoints with a persisted current-user secret, fragment bootstrap, a same-site session cookie, Host/Origin/fetch checks, and no cross-origin access; verify unauthorized/cross-origin/wrong-host requests fail, sessions survive restart, deleting the secret revokes them, and copied routes contain no secret.
- [x] 4.4 Add atomic, versioned dashboard-state persistence separate from shared settings and journal files, including history defaults; verify selection and history preferences survive restart, invalid UI state is recoverable, and concurrent agent settings writes are preserved.
- [x] 4.5 Create the Angular workspace with a committed lockfile, wire its production build into the release build, and embed the output in the payload executable without expanding the release file allowlist; verify a locked install, build failure when assets are missing, license notices where required, and asset availability from the packaged executable without Node.js.
- [x] 4.6 Add the `application-distribution` delta behavior to help and release checks; verify help lists the dashboard command and the package file list is unchanged.

## 5. Dashboard shell and repository navigation

- [x] 5.1 Build Ongoing work and History routes with the shared all/one/many repository filter, launch defaults, restored selection, and repository labels; verify selection stays consistent across both views and search after a restart.
- [x] 5.2 Show active changes (including unlogged ones), recent not-done work items within 14 days, journal-only repositories, source ambiguity choices, and separate archive/task/recorded-status indicators; verify an unlogged change acquires no timeline dots or inferred completion and a ticket without a change still appears.
- [x] 5.3 Implement Angular router history, selected-document and reading-position restoration, and explicit source reveal for deep links outside the filter; verify browser back/forward, missing sources, and cross-repository links with keyboard navigation.

## 6. History timeline and insights interface

- [x] 6.1 Render elapsed spans and activity dots with the 30-day/configured-timezone/ticket-grouping defaults, date-range and timezone controls, day/week/month scales, clipped endpoints, and text equivalents; verify the displayed dates and durations against reporting fixtures across zoom and grouping changes.
- [x] 6.2 Add item/day drill-down showing all applicable statuses, original summaries, completion/subsequent-work markers, and associated document choices; verify paging and a ticket associated with multiple changes in browser scenarios.
- [x] 6.3 Connect insight panels to the snapshot projection and underlying detail routes, discarding responses for superseded filters; verify values remain scoped to the selected repositories/period and loading states never appear as final totals.

## 7. OpenSpec reader, search, and links interface

- [x] 7.1 Render Markdown in the client with a pinned renderer that exposes source line ranges per block, with rendered/source views, proposal/design/tasks/spec navigation, a heading outline, and OpenSpec-reported or unavailable task progress. Prefer design.md when no explicit or remembered document selection exists; verify that default and proposal fallback, explicit-route precedence, Markdown tables/code/checkbox rendering, absent documents, block line ranges, and current-file labels beside historical activity.
- [x] 7.2 Apply safe rendering with raw HTML, executable markup, and automatic remote resources disabled; verify malicious Markdown and link schemes neither execute code nor trigger remote network requests in the browser.
- [x] 7.3 Integrate two-second visible-tab refresh and manual refresh with reading-position preservation and stale/missing notices; verify an agent-style document replacement and archive move retain the selected section and an unsaved comment being typed.
- [x] 7.4 Add scoped search results with excerpts, archive/source labels, the limit-reached indicator, line-based navigation, and copyable change/document/heading/task links; verify keyboard-only search, archive-safe links, and explicit missing-target feedback.

## 8. Inline review comments

- [x] 8.1 Add the comment insertion service that writes `<!-- REVIEW: ... -->` after a block's last line with escaping, atomic replacement, and preserved encoding and line endings; verify only the marker is added, `-->` in comments stays contained, and CRLF/LF and UTF-8 BOM files round-trip.
- [x] 8.2 Enforce active-change-only writes and best-effort revision/block-text conflict checks against a final reread, inserting into that reread's contents; verify archived documents and outside paths are rejected, selected-block edits before the reread cause a conflict, and unrelated earlier edits are retained. Document review after agent edits finish and the accepted reread-to-replacement race without adding an agent-idle or editor-lock protocol.
- [x] 8.3 Build the select-and-comment UI and render existing REVIEW markers as notes in rendered and source views; verify keyboard operation, retention of the typed comment after a conflict and reload, and no comment action on archived changes.

## 9. Integrated acceptance and documentation

- [x] 9.1 Add reproducible browser acceptance coverage using synthetic multi-repository, worktree, store, archive, and large-history fixtures; verify the ongoing-work-to-history workflow, design-first review after agent document edits finish, retained comments on detected conflicts, links after host restart, keyboard focus, and network-free document reading.
- [x] 9.2 Exercise dashboard browsing and refresh while another process logs/queries through MCP and edits documents, then save review comments after document editing stops while MCP logging continues; verify observations, retry identities, shared settings, protocol-only MCP stdout, and reviewed file content remain correct without dashboard-driven journal writes or a simultaneous-document-write guarantee.
- [x] 9.3 Run locked .NET and Angular restores, Release build/tests, and the relevant packaged Windows smoke checks; verify initial and repeat launch through the stable launcher with and without --no-open, offline embedded assets, shutdown, unchanged package contracts, and rollback compatibility before reporting implementation complete.
- [x] 9.4 Update README, installation, and architecture documentation with command options and --no-open precedence, source discovery, filter/history defaults, Ongoing work definition, elapsed-span semantics, current-file limitations, OpenSpec-reported or unavailable task progress, current-number task-link limitations, deep-link endpoint lifetime, Angular setup, and the design-first review-after-editing convention with its accepted write race and agent handling of REVIEW markers; verify documented commands and flows against the packaged dashboard.

## 10. User acceptance follow-up

- [x] 10.1 Fix background refresh superseding a document switch; verify delayed catalog responses and rapid switches through nested specs, proposal, design, and tasks.
- [x] 10.2 Embed a local favicon and make dark theme the default with a persisted light/dark toggle; verify both themes in the dashboard and document reader.
- [x] 10.3 Rebuild the Windows package and run Angular, .NET, and packaged browser checks for the follow-up changes.

## 11. Review comment references and maintenance

- [x] 11.1 Add explicit review-time passage references and unique IDs to new markers; retain legacy marker rendering and code-example isolation.
- [x] 11.2 Add marker-only edit/delete services with active-source containment, stale/ambiguous-marker conflicts, and preserved encoding, line endings, and unrelated edits.
- [x] 11.3 Add edit, cancel, and confirmed delete controls in rendered and source views, retaining unsaved edit drafts across refresh and navigation.
- [x] 11.4 Verify review creation/edit/delete and conflicts in unit/service and packaged browser checks; update usage documentation and build a fresh package.
