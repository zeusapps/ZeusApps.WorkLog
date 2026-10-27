# Design

## Context

See proposal.md for motivation. Relevant current code:
- `Ticket(Provider, Scope, Id, Url?)` (`src/WorkLog/Journaling/Ticket.cs`). The provider is `jira` or `azure-devops` (`EntryFactory` validates this). Scope is free text, documented as a Jira site or an Azure DevOps organization/project. Azure DevOps IDs are numeric.
- `Entry.ExtractedTicketId` holds unresolved IDs from branch names.
- Repository identity comes from `RepositoryIdentity.FromRemote` (`src/WorkLog/Repositories/RepositoryIdentity.cs`): `host/path` with a lowercased host and `.git` stripped, for HTTPS and scp-style remotes. Otherwise it is `path:<full path>`. The registry falls back to `local:<guid>` when there is no remote. Example: this repository's journal identity is `local:...` even though it has a GitHub remote, because it was registered before the remote existed. It gets no links, which is correct behavior.
- The `query_work` ticket filter is SQL in `SqliteEntryStore.ReadPage` (`src/WorkLog/Persistence/SqliteEntryStore.cs:60-69`).
- The dashboard timeline filters in memory after `DashboardJournal.Read` (`TimelineService.Project`).
- `/api/activity/changes` (from `add-archive-browser-and-editor-open`) returns `lastAt` per repository and change.

## Goals / Non-Goals

**Goals:** deterministic, testable link building; filter parity with `query_work`; no network access.

**Non-Goals:**
- Fetching ticket titles or states from issue services.
- Commit links or commit discovery.
- Configurable URL templates for self-hosted Jira, GitLab, or Azure DevOps Server. Deferred; recorded URLs already cover these cases.

## Decisions

### 1. Client-side link builder
`dashboard/src/links.ts` exports pure functions.

`ticketUrl(ticket)`:
- If `url` is an absolute `http(s)` URL, return it.
- Jira: if scope matches a host name (`^[a-z0-9.-]+\.[a-z]{2,}$`, case-insensitive) → `https://<scope>/browse/<id>`.
- Azure DevOps:
  - scope `org` → `https://dev.azure.com/<org>/_workitems/edit/<id>`
  - scope `org/project` → `https://dev.azure.com/<org>/<project>/_workitems/edit/<id>`
  - scope `<org>.visualstudio.com[/project]` → the same path on that host.
- Each path segment is percent-encoded. Return `null` otherwise.

`repositoryUrl(identity)`:
- `github.com`, `gitlab.com`, `bitbucket.org`: `https://<identity>`.
- `dev.azure.com/<org>/<project>/_git/<repo>`: as-is over HTTPS.
- `ssh.dev.azure.com/v3/<org>/<project>/<repo>`: becomes `https://dev.azure.com/<org>/<project>/_git/<repo>`.
- `<org>.visualstudio.com/...`: as-is.
- Anything else, including `local:` and `path:`: `null`.

`branchUrl(identity, branch)`:
- GitHub: `/tree/<branch>`
- GitLab: `/-/tree/<branch>`
- Bitbucket: `/src/<branch>`
- Azure DevOps: `?version=GB<branch>`

Branches are encoded per path segment, keeping `/`. Anything else returns `null`.

Building links on the client keeps the server API unchanged. The functions are trivially unit-testable.

Alternative: build the links on the server. Rejected; there's no benefit, and it would spread the presentation contract across the API.

### 2. Shared ticket predicate with parity tests
Add `TicketFilter.Matches(Entry entry, string filter)` in `src/WorkLog/Reporting`, mirroring the SQL:
- Trim the filter.
- A bare ID (no `:`) matches `ExtractedTicketId` or any trimmed `ticket.Id`.
- A qualified filter matches `provider:scope:id` composed from trimmed parts.

`TimelineQuery` gains `Ticket?`. `TimelineService.Project` selects identities where any entry, among all lifecycle entries up to `to`, matches, and then projects the full rows as today. Observations drill-down applies no ticket filter, so it keeps showing the full lifecycle.

A parity test runs the same fixture entries through `SqliteEntryStore.ReadPage` with the `Ticket` filter and through `TicketFilter.Matches`, and asserts the same matching entry IDs, including whitespace, unresolved IDs, and qualified mismatches.

Alternative: push the filter into the dashboard SQL. Rejected, because lifecycle context needs every observation of the selected identities, not just the matching ones.

### 3. Copy as Markdown
Client only. It loads remaining observation pages up to 1,000 entries, then formats:

```markdown
### <title>
Tickets: [DEP-12](url), DEP-13 · Repository: <label>
Recorded: <first date> → <latest date> · Latest status: <status>
Changes: <names>

- <date time> — <status> — <summary or "No summary recorded.">
```

If the cap is hit, it appends a "(truncated at 1,000 observations)" line. Dates use the selected timezone. Clipboard failure falls back to the existing copy dialog (`copyFallback`).

### 4. Quiet indicator
After loading the catalog, Ongoing work calls `/api/activity/changes` for the selected repositories, refreshed when `journalSnapshot` changes. A card whose `lastAt` is older than 14 days, in the selected timezone, gets the quiet chip. Without an entry, the card shows the existing "No recorded activity".

## Risks / Trade-offs

- A link built from a free-text scope could point to the wrong site if the user recorded an odd scope. → Only clearly recognized forms are linked. The recorded URL always wins, and the link text shows the ID. The user can check the link before clicking.
- The parity test depends on SQLite JSON functions behaving like the C# predicate for unusual whitespace. → The fixtures cover leading and trailing whitespace on each part.

## Migration Plan

Additive. Older payloads ignore the `ticket` query field; the client only sends it when it is set.
