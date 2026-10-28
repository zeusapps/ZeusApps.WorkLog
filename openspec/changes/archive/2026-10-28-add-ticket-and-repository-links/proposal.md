# Proposal

## Why

The dashboard shows ticket IDs and repository identities as plain text, so following work back to its source means copying an ID into a browser. The data needed for direct links is already recorded: tickets carry a provider, a scope, and sometimes an explicit URL, and repository identities derived from Git remotes carry the host and path. Following one ticket across changes and periods is also hard without a filter. And active changes that have gone quiet stay in Ongoing work with nothing marking them.

## What Changes

- Add ticket links wherever tickets appear: timeline rows, Ongoing items, insights, drill-down observations, and later the daily view.
  - The ticket's recorded URL is used when present.
  - Otherwise a URL is built for well-known provider and scope forms: a Jira site host, or an Azure DevOps organization or organization/project.
  - There is no link when the form is not recognized.
  - Each ticket also gets a "copy ticket ID" action.
- Add repository web links for identities on recognized Git hosts (GitHub, GitLab, Bitbucket, Azure DevOps), plus branch links for observations that recorded a branch. Local-only identities (`local:`, `path:`) get no link.
- Add a ticket filter to History. It accepts a bare ticket ID or `provider:scope:id`, with the same matching rules as the `query_work` MCP tool, and shows only the work items that include a matching observation, with their full lifecycle.
- Add "Copy as Markdown" to the drill-down: the item title, tickets with links, the date span, recorded status, and the observations with dates and summaries.
- Mark active changes whose latest recorded activity is older than 14 days as quiet, separately from changes with no recorded activity.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `work-dashboard`: Adds external ticket and repository links, the History ticket filter, copying a work item as Markdown, and quiet active-change indication.

## Impact

- Server:
  - Optional `ticket` on `TimelineQuery`, with matching shared with the `query_work` SQL semantics through a common C# predicate verified by parity tests.
  - `TimelineRow` already carries `Tickets`; observations carry `branch`.
- Client: a link builder module (pure functions with unit tests), link and copy affordances, the ticket filter input, Copy as Markdown, and the quiet indicator, which uses `/api/activity/changes` from `add-archive-browser-and-editor-open`.
- External links open only on explicit user click in a new tab with `noopener,noreferrer` (existing rule). No automatic requests.
- No journal schema or MCP contract changes. `query_work` behavior is unchanged.
- Prerequisites: `add-agent-workflow`, `fix-dashboard-reliability`, `add-archive-browser-and-editor-open` (activity summary).
