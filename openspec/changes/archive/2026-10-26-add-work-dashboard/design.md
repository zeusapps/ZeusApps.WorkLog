# Design

## Context

See proposal.md for motivation and scope. At the start of this change, WorkLog was a .NET 10 Windows-first executable with a stable launcher and self-contained versioned payloads. `CliApplication` composed init, serve, and update, with no browser host or frontend. `Journal` owns logging and paginated reporting, `ReportPager` applies snapshot and byte limits, and `WorkIdentity` implements change and qualified ticket-set grouping.

Observations are timestamped facts, not timed sessions. A started observation can record proposal creation, done can be followed by further work, session IDs are optional, and classifications are often unclassified. Existing reporting scenarios preserve those semantics, including timezone boundaries and historical unresolved ticket references.

Private configuration maps common Git directories to repository identities, with legacy checkout paths retained. A common directory is not a guaranteed readable working tree, and several checkouts can share an identity while holding different document contents. OpenSpec documents are currently archived into dated directories; metadata contains schema and creation date, not a permanent change UUID. Source resolution must therefore remain explicit.

The release builder and updater use exact package file allowlists. Assets must travel within the payload executable to avoid a package-contract change. Journal construction currently initializes storage and can run schema setup, so browsing must not blindly construct a writable Journal merely to read history.

## Goals / Non-Goals

**Goals:**

- Separate the Angular client, local transport, timeline projection, and document catalog so each can be tested independently.
- Keep source files and journal observations authoritative; the dashboard's only writes are local UI preferences and adding, editing, or deleting review markers.
- Preserve the existing MCP surface, journal schema 2, saved repository identities, and launcher/package contracts.
- Make empty, missing, stale, clipped, ambiguous, and partially loaded data explicit.
- Prefer stateless, simple mechanisms over caches and server-side handles until measurements require them.
- Support a personal, design-first review workflow: the user reviews documents after the agent finishes editing them, while task execution remains primarily the agent's concern.

**Non-Goals:**

- OpenSpec editing beyond inserting review comments, managing OpenSpec workflows, sending agent messages, tracking review resolution, or updating external ticket systems.
- Inferring labor hours, productivity, idle time, ticket reopen events, or completion from filesystem state.
- Time-travel document revisions, hosted access, and exported HTML snapshots.
- A desktop shell, a user-installed JavaScript runtime, or a permanent background daemon.
- Coordinating simultaneous agent/editor document writes or preserving historical task identity through renumbering.

## Decisions

### 1. .NET JSON API with an embedded Angular client

Add a `DashboardCommand` and a `Dashboard` area with a loopback HTTP host, JSON endpoints, embedded static assets, and application services. Keep `serve` on its existing stdio path.

The client is an Angular application in its own workspace with a committed lockfile. Node.js is a development and build dependency only: the release build runs the Angular production build, and its output is embedded as resources in the payload executable and served by the host. Users receive plain HTML, CSS, and JavaScript. A missing or stale Angular build fails the release build rather than shipping without assets.

The command accepts `--repository`, `--data-dir`, `--port`, and `--no-open`. Use a fixed default loopback port of 47831 so copied links remain usable after restart. Print the actual URL, retain the server if browser opening fails, and stop cleanly on cancellation.

The running dashboard writes `dashboard.lock` in the private data directory with its process ID, port, and a random instance ID. A later `worklog dashboard` that finds a live process at the recorded port answering a health request with the same instance ID prints a fresh bootstrap URL and exits successfully, opening the browser only when `--no-open` is absent. This option suppresses browser opening on both initial and repeat launches; reusing the listener does not change that choice. A stale lock file is replaced. An occupied port without a matching lock fails actionably with guidance to select another port; never trust or adopt an unknown process.

Use ASP.NET Core's Kestrel host through a Microsoft.AspNetCore.App framework reference in the existing executable. It supports ordinary-user loopback binding, cancellation, the private JSON endpoints, and self-contained single-file publishing without a separate web-service installation. The Angular client is pinned to 21.2.25 with CLI/build 21.2.28, compatible with the existing Node 24.13.0 development runtime. The redundant Microsoft.Extensions.Hosting package reference is removed because the shared framework supplies it. Final package and compatibility evidence is recorded in docs/dashboard-validation.md; the existing release file allowlist and shared-data contracts are unchanged.

Alternative: server-rendered HTML avoids a Node toolchain but makes the timeline and selection-based commenting awkward. A desktop shell adds a separate packaging and lifecycle surface.

### 2. Give the client a small, private API

Expose services for source/catalog listing, timeline rows and drill-down, document content, search, review comment insertion, and dashboard preferences. Return opaque source/item identifiers and structured metadata; the client never opens the SQLite database or requests arbitrary filesystem paths. Encode route components independently instead of parsing slash-delimited work identities.

Bind only to the explicit loopback address and validate Host, Origin where present, and browser fetch metadata. Do not enable cross-origin access. Gate API endpoints with an authorization secret stored in the private data directory, readable only by the current user, and reused across restarts. The launch URL carries a one-use bootstrap value in its fragment; the client exchanges it for an HttpOnly same-site cookie and removes the fragment immediately. Because the secret persists, the cookie and copied links keep working after restarts at the same endpoint. Ordinary copied routes contain no secret; a browser without a valid cookie sees how to launch the local dashboard. Deleting the secret file revokes existing sessions.

Raw HTML in Markdown is not rendered and output is sanitized (see Decision 6). Disable automatic remote image and resource loading, reject executable link schemes, and allow external links only through explicit user navigation. Enforce source containment after decoding, canonicalization, and filesystem-link resolution. Do not expose an endpoint for arbitrary paths, shell commands, or OpenSpec CLI mutations.

### 3. Catalog sources without changing shared repository configuration

Build a source catalog from saved `RepositoryContext` records, compatible legacy checkout records, journal repository identities, and the explicit or current launch checkout. Use read-only Git worktree discovery to identify available checkouts from common Git directories. Do not call registry persistence or create a new random repository identity for browsing. An unregistered checkout can supply document-only changes with an explicit local source identity; it is not automatically joined to historical journal entries by remote URL or ticket ID.

Resolve each checkout's planning root using the installed read-only OpenSpec context command, preserving the selected store identity for any additional CLI reads. When the CLI is absent, allow a conservative project-local directory fallback only if there is no store declaration. Report malformed or unresolved store configuration and never substitute a local root for it. Use canonical resolved planning roots as source identities, deduplicating the same store reached through several checkouts while retaining its repository associations.

Index active and archived change directories and available Markdown files, including nested specs and custom Markdown artifacts. Store document identity as source, logical change name, and relative document path. Match a known change name against active names, exact archive names, and a single date-prefixed archive name. Keep exact date-prefixed names valid; if aliases, duplicates, or active/archive copies yield several candidates, expose a source choice instead of guessing. Journal names and previously selected routes can supply the original logical name without rewriting metadata.

Alternative: hardcoding a single checkout path breaks worktrees and stores; fetching documents from GitHub adds network access and cannot show uncommitted agent edits.

### 4. Build timeline projections from a stateless read snapshot

Introduce a dashboard-specific read service over `Persistence`, reusing `WorkIdentity` and reporting period validation. Open an existing supported journal for reading without running migrations, creating missing journals, changing WAL mode, or registering repositories. A missing journal is a valid empty-history state; an unsupported schema receives actionable guidance through the normal supported upgrade path. Do not change the MCP query or cursor contracts.

Each refresh reads the current maximum rowid as its snapshot. The server loads the selected repositories' observations with occurredAt < to and rowid <= snapshot into memory, computes the projection, and returns the snapshot rowid with the first page. The client sends that rowid with subsequent row pages and drill-down requests, so the server keeps no handles or caches between requests; a recomputation under the same rowid yields the same result. Select displayed work identities with observations in the visible [from,to) range, and use all of their earlier observations for lifecycle context, ordered by occurredAt and ID. Carry original repository, ticket sets, and change references into detail DTOs instead of recovering them from identity strings.

For each row calculate first observation, optional initial started observation, last observation, latest status, all completion markers, subsequent-work markers, and distinct visible local activity dates. The drawn observed span is first-to-last observation, clipped to the visible range. A latest done entry supplies a recorded completion endpoint; a later in_progress replaces that endpoint with the later observation and an explicit no-current-completion label. Never stretch an unresolved row to now or label the span as labor time. Lifecycle durations use instants, day counts use the chosen timezone. Archived/current documents remain labeled as current file content even beside historical journal ranges.

Return 50 identities per row page and 100 observations per drill-down page. The client discards responses for superseded filters. If journal size later makes per-request loading too slow, add caching keyed by snapshot behind the same API.

Insights use the same projection: longest observed spans among applicable rows, done observations, subsequent work after done, and distinct work identities recorded on each visible date. Each value links to evidence. Labels avoid claims about hours, attendance, or external ticket status.

Alternative: joining the first and last observation from one MCP page would understate spans and can misstate completion. Public MCP pagination remains useful for agents but is not the dashboard's aggregation boundary.

### 5. Use one navigation model, defaults, and durable document routes

The shell contains Ongoing work and History, a shared multi-select repository filter, and a document/detail panel. Use a launch repository only when there is no valid restored selection; otherwise preserve the saved selection. With no launch context or valid selection, default to all known repositories. Missing sources stay visible as unavailable. When selecting a change without an explicit document route or remembered document selection, open design.md if available, then proposal.md, then the first available Markdown artifact. Tasks remain readable and linkable without becoming the default review surface.

Ongoing work lists active OpenSpec changes, including those without observations, plus work identities whose latest recorded status is not done and that have an observation within the last 14 days. History defaults to the last 30 days, the configured reporting timezone (falling back to the system timezone), and ticket grouping, matching the report default. Changed values are remembered as preferences.

Store a versioned `dashboard-state.json` alongside private data, separate from shared `config.json` and the journal. It contains repository selection, history controls, preferred document sources, last route, reading positions, and the chosen light/dark theme; atomic saves through the authorized preferences endpoint preserve a stable experience across processes. Dark is the default, with a keyboard-accessible top-bar toggle and shared surface/text/border colors across the dashboard and document reader. Embed the favicon with the other local assets. Corrupt or obsolete UI preferences can be reset without affecting journal/configuration data. The file is dashboard-private: other payload versions ignore an unknown version rather than failing.

Background polling captures the document request generation before catalog discovery and skips document refresh if navigation or review starts while discovery is in flight. The latest explicit document selection takes precedence; late document responses and queued scroll callbacks from earlier selections cannot restore the earlier file or move the new document's reading position.

Use Angular router routes containing source/repository identifiers, logical change name, document relative path, and an optional heading or task anchor. A route resolves against the catalog each time and therefore survives an active-to-archive move. Heading anchors are derived on the client and duplicates are resolved deterministically. Task anchors use the OpenSpec task number (for example `1.2`) when the task has one, falling back to normalized task text and occurrence within its section. They resolve against the current document: rewording a numbered task retains the link, but renumbering or reusing its number can change its destination. A missing or ambiguous target resolves to its surviving section or document with a target notice. Do not introduce permanent task IDs or promise historical task identity across renumbering, arbitrary renames, or moves. OpenSpec validation detects duplicate task IDs and tasks under the wrong numbered group, but does not preserve identity between document revisions. Direct links explicitly reveal a known source excluded by the current filter and identify the selection change.

Timeline rows expose accessible text equivalents for dot dates and spans. Filters, tabs, search, heading links, and comment controls use keyboard-operable controls. Back/forward navigation restores meaningful document routes.

### 6. Render on the client and use source lines as the shared coordinate

The API returns raw Markdown with its revision hash; the Angular client renders it with a pinned Markdown library that reports source line ranges per block (for example markdown-it token maps), with raw HTML disabled and output sanitized. Each rendered block carries its source line range. Search results, task targets, and new review insertion use source lines. Existing comment mutations additionally use the comment ID and exact marker text; the server scans only standalone review markers outside code examples, rather than adding a second Markdown renderer or task-progress parser.

Use OpenSpec's read-only change-list output for active-change task totals, respecting its schema-selected tracked task files and the selected planning root/store. Label them as OpenSpec task progress. Do not compute an independent progress total from rendered Markdown or exclude review comments/code examples differently: the installed OpenSpec counter deliberately counts checkbox-shaped lines wherever they occur, and only x/X markers mean complete. If authoritative counts cannot be obtained, including for archived changes not returned by the CLI, show progress as unavailable while keeping readable documents accessible. A failed CLI call or missing field is not zero tasks. This avoids a second task-progress parser and does not require modifying or adding OpenSpec commands.

Extract task numbers, source text, and line positions only for navigation in the current document; these positions do not drive progress totals or reporting completion. Keep the normal design-review flow independent of task numbering. Prefer fixtures captured from the supported CLI to verify count presentation. Do not add a dashboard validation feature in this change; existing OpenSpec validation remains available through the CLI and agent workflow.

Provide a rendered view, source view, heading outline, and task progress. While the dashboard is visible, poll the catalog and selected document every two seconds and provide manual refresh. Polling compares modification time and size first and hashes a file only when either has changed. Pause background polling when hidden. A consistent-read retry handles files rewritten by an agent; on transient failure retain the last complete version with a stale notice. Restore the nearest surviving heading plus scroll offset after replacement, and resolve archive moves before declaring a source missing.

Search is case-insensitive literal text over Markdown under the selected roots, filtered by repositories and active/archive/all scope. Each query reads the files directly without an index and returns up to 50 matches with excerpts, line positions, and a limit-reached flag. Files over the 2 MiB limit are unavailable for rendering and search and reported as skipped. Enumeration and reads support cancellation and report unavailable sources rather than claiming exhaustive results.

Alternative: server-side rendering would give one parser on the server but duplicate interactive concerns on the client. Filesystem watchers alone can miss replaced directories or archive moves. Full Git diff/history indexing and split-pane reading are deferred.

### 7. Write review comments inline after external edits pause

The intended personal workflow is to let the agent finish editing, review primarily the design document, save inline comments, then let the agent read and act on those comments. Comments remain supported in other active change Markdown files. No agent-idle detection, write-lock protocol, or agent integration is required. The user selects a rendered block, writes a comment, and saves. The client sends source identity, change, relative path, the document revision it rendered, the block's line range, and the block's source text. The server:

1. Rejects the request unless the target is a Markdown file inside a discovered active change of an available source, after canonical containment checks. Archived changes are read-only.
2. Rereads the file and rejects with a conflict if its revision differs and the block text no longer appears at the given lines. If an unrelated edit leaves the block unchanged at those lines, build the insertion from the latest reread contents so that edit is retained.
3. Inserts a blank line and a structured REVIEW marker after the block's last line. The marker contains a generated identifier, `Target: preceding passage`, a short `Quote:` excerpt, and a `Comment:` body with line breaks preserved. Escape `--` sequences in metadata and feedback so they cannot close the marker early. The quote records review-time context rather than a permanent document anchor.
4. Writes a temporary file in the same directory and atomically replaces the original, preserving its encoding and line endings.

On a detected conflict the client reloads the document and keeps the typed comment for retry. Within the supported workflow, only the marker and its separating whitespace are added to the latest reread version. Revision checks and atomic replacement are best-effort safeguards: they do not prevent an external edit between the reread and replacement, and simultaneous writes can still lose changes. Document this accepted limitation without claiming concurrent-safe writes or adding a blocking confirmation to every save.

The client renders existing `REVIEW` markers as visible notes next to their block, retaining support for plain legacy markers. Each active note provides Edit and Delete controls in rendered and source views. Edit preloads its body into the review composer; Delete asks for confirmation in the reader. Mutation requests identify the original marker by unique ID when present or original source line for legacy notes and include its exact rendered marker text. The service parses real standalone markers outside fenced/indented code, rereads the latest document, and rejects changed, missing, or ambiguous markers before replacing only that marker. Legacy edits gain a structured reference based on the preceding passage. Preserve unsaved edits on conflict and document navigation. Archived notes remain read-only. The dashboard does not track resolutions or notify agents; the agent can still address feedback and remove its marker directly.

Alternative considered: a separate review file avoids writing the reviewed document. Inline comments are the selected approach because the user reviews after agent edits finish and wants feedback in the files the agent already reads; coordinated simultaneous editing is deferred.

## Risks / Trade-offs

- Local HTTP access could expose private documents --> authorize data endpoints, validate local origin/host, disable remote resources, and test traversal plus filesystem-link escapes.
- External edits can occur after the last revision check and be overwritten --> the supported workflow pauses agent edits during review; retain best-effort revision checks and atomic replacement, keep typed comments on detected conflict, and document the remaining race as an accepted limitation.
- A persisted authorization secret outlives a process --> store it in the current-user private data directory and allow revocation by deletion.
- Several checkouts or archive aliases can match one change --> keep provenance visible and require an explicit source choice when resolution is ambiguous.
- Large journals and document trees can stall the UI --> page results, enforce file limits, support cancellation and partial-source notices, and add caching only if measurements require it.
- Task renumbering or number reuse can change task-link destinations --> document current-number semantics, use OpenSpec validation for current numbering errors, and surface missing or ambiguous targets without promising historical task identity.
- The Angular toolchain and renderer add dependencies --> lock them, build in CI and release, embed the output, preserve package allowlists, and validate a self-contained package offline before declaring the feature complete.
- Dashboard preferences introduce local writes --> isolate them from the journal and shared configuration, keep them optional and resettable, and test simultaneous agent operation.

## Migration Plan

1. Add the dashboard read services, source catalog, host, comment writer, and embedded Angular client without migrating journal schema or rewriting saved identities. Do not refresh agent integrations merely to launch a dashboard.
2. Validate services with isolated journals/checkouts, the client with Angular unit tests, and browser flows with synthetic sources; retain existing journal, MCP, CLI, and updater checks.
3. Build the Windows package and verify launch and second-launch reuse with and without --no-open, offline assets, links after restart/archive, comment insertion after document edits finish, detected stale-block conflicts, shutdown, and concurrent MCP logging through the stable launcher. Simultaneous document editing is not an acceptance guarantee.
4. Document command options, current-file semantics, filter defaults, OpenSpec-reported or unavailable task progress, current-number task links, source limitations, and the design-first review-after-editing convention for users and agents. Publishing or installing a release remains a separate user-authorized action.
5. Roll back by selecting the previous compatible payload. It ignores the dashboard state, lock, and secret files; journal data, shared settings, and repository identity are unchanged. Inserted review comments remain ordinary Markdown comments.
