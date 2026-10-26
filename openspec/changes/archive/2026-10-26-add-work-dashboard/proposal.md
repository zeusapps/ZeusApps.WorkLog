# Proposal

## Why

WorkLog records useful activity facts, but users cannot visually follow work over time or comfortably read and review the OpenSpec documents that guide implementation. A local browser dashboard would bring ongoing changes, their documents, and recorded work history into one place without requiring a hosted service, and would let review comments reach the agent through the documents it already reads.

## What Changes

- Add `worklog dashboard` to launch an offline-capable Angular interface, served as prebuilt static assets by the local executable over a private JSON API. Node.js is needed only for development and builds, never by users.

<!-- REVIEW: test comment -->
- Provide Ongoing work and History views with a shared repository filter supporting all, one, or several repositories; use the launch repository as the initial default when available and remember subsequent selections.

<!-- REVIEW: foo bar -->
- Default to a dark theme with a remembered light/dark toggle covering dashboard views and document review.
- Visualize ticket/change work as elapsed spans with dots only on dates containing recorded observations, with date-range, timezone, and grouping controls, drill-down, and factual insights.
- Discover active OpenSpec changes independently of journal activity and render proposal, design, tasks, and spec documents for active and archived changes, including configured standalone stores.
- Show OpenSpec-reported task progress separately from recorded work status and archive state; refresh documents while preserving reading position, and prefer the design document when opening a change without an explicit or remembered document selection.
- Search documents within the repository and active/archive filters, returning matching passages and direct navigation.
- Add inline review comments to active change documents, primarily for reviewing designs after the agent finishes editing. The dashboard inserts a marked Markdown comment with an explicit passage reference and supports editing and confirmed deletion of saved notes, with best-effort conflict detection; simultaneous agent edits are outside the supported review workflow.
- Provide links to changes, documents, headings, and tasks that resolve across archiving and dashboard restarts, with clear handling of missing targets. Task links follow current task numbering and do not guarantee historical identity after renumbering.

## Capabilities

### New Capabilities

- `work-dashboard`: Local dashboard lifecycle, shared repository filtering, ongoing/history views, factual timelines, and activity insights.
- `openspec-browser`: Local source discovery, active/archive document reading, task progress, document refresh, search, and durable navigation targets.
- `document-feedback`: Inline review comments written into active change documents after external edits pause, with best-effort conflict detection and display of existing comments.

### Modified Capabilities

- `application-distribution`: The Windows release additionally supports the `dashboard` command and carries the embedded dashboard assets inside the payload.

Existing journal and MCP reporting contracts remain in force; the dashboard adds a consumer and its own presentation contracts.

## Impact

- Add CLI composition and dashboard hosting, document discovery, and a JSON API alongside existing `Cli`, `Reporting`, `Repositories`, and `Configuration` areas.
- Add an Angular workspace with a locked dependency tree; the release build compiles it and embeds the output in the payload executable. No Node.js or separately installed web runtime is required for users.
- Reuse journal querying and work identity rules while adding complete-history timeline aggregation and local source resolution; do not change historical entries or journal schema 2.
- Preserve the stable launcher, package allowlist, and offline operation.
- Add isolated service, Angular, browser, and packaged acceptance checks and document dashboard launch, source selection, filtering, review comment conventions, and limitations.
- Document writes are limited to adding, editing, and deleting review comments in active change Markdown files. Keep out of scope: other OpenSpec editing or workflow management, review resolution tracking, agent messaging, time tracking, Git revision browsing, hosted sharing, and HTML export.
