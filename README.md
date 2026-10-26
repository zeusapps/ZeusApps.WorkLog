# WorkLog

A Windows-first .NET MCP server for a private local work journal. Codex and Claude Code record what you actually worked on; either assistant can retrieve structured facts for daily or periodic reports.

```powershell
worklog --version
worklog init --repository D:\Code\MyProject --dry-run
worklog init --repository D:\Code\MyProject
```

Install a self-contained Windows x64 release from [GitHub Releases](https://github.com/zeusapps/ZeusApps.WorkLog/releases). No separately installed .NET runtime is needed. See [installation and upgrades](docs/installation.md).

`init` defaults to both clients and requires Git plus Codex CLI 0.162.1+ and Claude Code 2.1.289+. Select just one with `--clients codex` or `--clients claude`. Initialization preserves shared instructions and uses private configuration. Restart clients and follow their normal repository trust flow after initialization.

For an existing application that already supplies logging instructions:

```powershell
worklog init --clients claude --ticket-provider jira --ticket-scope company.atlassian.net --no-instructions --dry-run
# Review the plan, then rerun without --dry-run.
```

`--no-instructions` removes WorkLog-owned instruction blocks and remembers the opt-out for the selected repository and clients, while keeping MCP registration. Later init runs retain that choice. Use `--instructions` to re-enable managed instructions explicitly. Unrelated application/user instructions remain intact. Without managed instructions, your existing rules or explicit tool calls drive logging.

## Journal behavior

- `started`: successful OpenSpec change creation, including `openspec-propose`.
- `in_progress`: actual meaningful session work, including resuming an unfinished feature.
- `done`: the session establishes reporting completion; archive dates are independent.

An unfinished feature generates no entries on days you do not work on it. Switching features does not finish the previous one. Multiple observations on a day remain available in reports. This is an activity journal, not a timer.

Automatic logging is instructed through local agent configuration. It depends on the agent calling the tools: abruptly terminated sessions or ignored instructions can miss activity. Logging failures must be reported, and tools can also be called explicitly.

## Local dashboard

```powershell
worklog dashboard
worklog dashboard --repository D:\Code\MyProject
worklog dashboard --no-open --port 47831
```

The dashboard opens in your browser and runs on this machine. Dark theme is the default; the top-bar theme button switches to light and remembers your choice. Ongoing work shows active OpenSpec changes and recent work without a recorded completion. History connects recorded observations with elapsed spans and activity-date dots. Filter all views and document search to one repository, several, or all known repositories. The first visit selects the launch repository when known; later visits restore your selection. History initially uses the last 30 days, your system timezone, and ticket grouping, and remembers changes to these controls.

Select a change to read its design, proposal, tasks, or specs. Designs open first unless you follow a document link or have a remembered selection. Local OpenSpec and configured standalone-store documents are supported, including archives and separate worktree copies. Documents refresh while open and retain your reading position. They show current local files, including beside historical activity. Task totals come from OpenSpec when available; unavailable totals are not reported as zero.

After the agent finishes editing, select a passage in an active change document, type a review comment, and save it. WorkLog inserts a REVIEW marker immediately after that passage, containing a comment ID, an explicit preceding-passage reference, a short quote, and the feedback. Notes have Edit and Delete buttons in both rendered and source views; Delete asks for confirmation, and Cancel edit leaves the saved comment unchanged. Older plain REVIEW markers remain supported and gain a reference when edited. The agent can address the feedback and remove its marker when it next reads the file. Archived documents are read-only. Stale passage/comment checks retain your typed feedback on detected conflicts, but an external edit between the final read and replacement can still be lost: finish agent edits before saving reviews. Comments can be added to any active change Markdown document, with designs as the usual review surface.

Copied change/document/section links contain no authorization secret and resolve after archiving at the same dashboard endpoint. Task links follow current numbering; renumbering can change their destination. Launching the command again reuses this data directory's running dashboard. `--no-open` prints a URL without opening a browser on both first and repeat launches. Cancel the original command to stop its listener. A compatible application update leaves an existing dashboard on its original payload until it is restarted.

No Node.js, hosted service, or internet connection is required to use the packaged dashboard. The OpenSpec CLI supplies store resolution and active task counts; without it, ordinary local OpenSpec files remain readable when no store is declared. Journal history remains available if a source checkout is missing. See [dashboard operations](docs/installation.md#dashboard-operations) and [code organization](docs/architecture.md).

## MCP tools

Both tools advertise output schemas and return MCP `structuredContent` with a JSON text fallback. See [MCP validation](docs/mcp-validation.md) for protocol checks and documentation references.

`log_work` accepts an `entry` object:

```json
{
  "entry": {
    "status": "in_progress",
    "change": "add-link-verification",
    "branch": "feature/DEP-1232-adding-verify-links",
    "tickets": [{"provider": "jira", "scope": "company.atlassian.net", "id": "DEP-1232"}],
    "summary": "Implemented link validation; migration remains.",
    "occurredAt": "2026-10-20T14:30:00+02:00",
    "idempotencyKey": "session-123-observation-1",
    "agent": "codex"
  }
}
```

The initialized repository supplies repository identity; direct servers can instead receive `entry.repository`. Branches are snapshots supplied by the agent. At least a change or qualified ticket is required. Timestamp defaults to now; summaries, agent, and sessionId are optional. Reuse an idempotency key only to retry identical input. A change gets one initial started record across both agents; subsequent work uses in_progress.

`query_work` accepts a `period` object:

```json
{
  "period": {
    "from": "2026-10-19T00:00:00+02:00",
    "to": "2026-10-26T00:00:00+01:00",
    "timezone": "Europe/Warsaw"
  }
}
```

`from` is inclusive and `to` exclusive. The timezone controls calendar-day grouping; bounds must include offsets. Optional filters: repository, change, ticket (ID or `provider:scope:id`), status, classification. The assistant writes report prose from recorded facts.

A bare `ticket: "DEP-7483"` also finds observations whose ticket is still only in `extractedTicketId`. A qualified filter matches stored qualified references only, even after you configure the repository's provider/scope. Reports never retroactively qualify those historical entries; rejected observations that were never saved cannot be recovered by querying.

Choose grouping per report with optional `period.groupBy`: `"ticket"` (the default) combines same-day observations with the same complete qualified ticket set in one repository, even when their change names differ or are absent. Each observation retains its original change and ticket metadata. Set `"groupBy": "change"` for the previous change-first grouping with a qualified ticket-set fallback. Existing requests that omit the selector now group by ticket. For example:

```json
{
  "period": {
    "from": "2026-10-20T00:00:00Z",
    "to": "2026-10-21T00:00:00Z",
    "timezone": "UTC",
    "groupBy": "ticket"
  }
}
```

Ticket sets are sorted and deduplicated: `{A,B}` and `{B,A,A}` share a group, while `{A}` and `{A,B}` remain separate, including when filtering by A. Observations belong to one daily group each. Repository, provider and scope distinguish tickets; equal bare IDs alone never merge groups. Without qualified tickets, ticket mode retains the existing change identity, even when an unresolved extracted ID matches a qualified ticket elsewhere. Grouping applies to compact, grouped and legacy views; flat entries are unchanged. Only `"change"` and `"ticket"` are accepted; explicit null, blank and other values return a `period.groupBy` error.

Report `schemaVersion: 1` defaults to `view: "compact"`: observations appear once in `entries`, and daily groups contain `entryIds`. Pages default to 25 observations (`pageSize`: 1–100) and at most 64 KiB of structured JSON, so pages may be smaller. While `hasMore` is true, pass `nextCursor` as `period.cursor` with every other option unchanged. Combine all pages before writing a complete report; a daily group can span pages. Continuation excludes observations written after the first page, even if backdated.

Merge daily group fragments by `(date, workIdentity)` after retrieving every page; a fragment does not establish a final status or whole-period total. Keep `groupBy` unchanged during continuation (omitted and explicit `"ticket"` are equivalent). After upgrading, restart the MCP client to refresh tool discovery and start a new query instead of reusing an older cursor. No journal migration is required for grouping. Older binaries reject the new option; omit it when rolling back.

Other views are `flat` (entries only), `grouped` (full observations in daily groups only), and `legacy` (both full views; count observations once). Every view is paginated. Use `includeSummaries: false` for smaller reports or when a single summary exceeds the byte budget. Cursor tokens are local continuation state, not credentials; start a new query after switching journals or changing filters.

## Classifications and ticket references

Configuration lives in `%LOCALAPPDATA%\ZeusApps.WorkLog\config.json` by default, alongside `journal.db`. Override with `--data-dir PATH` or `WORKLOG_DATA_DIR`. Each machine is independent. There are no issue-service connections or credentials; use separate official issue MCPs for lookup.

Set repository ticket context with paired `init --ticket-provider jira --ticket-scope company.atlassian.net` options (or `azure-devops` with an organization/project scope). Omitting both flags preserves existing settings; invalid or incomplete pairs fail before writes. No provider or scope is inferred from a branch or Git remote.

After init, edit the active `repositoryContexts` entry in your private config to customize identity or extraction pattern, and `branchClassifications` to customize mappings. Merge changes into the existing file rather than replacing other settings:

```json
{
  "branchClassifications": {
    "feature": "feature",
    "bugs": "bug",
    "bugfix": "bug",
    "hotfix": "bug",
    "chore": "maintenance"
  },
  "repositoryContexts": {
    "D:\\Code\\MyProject\\.git": {
      "repository": {
        "identity": "github.com/company/project",
        "provider": "jira",
        "scope": "company.atlassian.net",
        "ticketPattern": "(?<id>[A-Z][A-Z0-9]+-\\d+)"
      },
      "instructions": { "claude": false },
      "legacyPaths": []
    }
  }
}
```

Git worktrees share context and instruction preferences through their common Git directory, including repositories without origin. Separate clones do not inherit settings just because their remote URLs match. Legacy `repositories` records remain readable and are retained for recovery after promotion; `legacyPaths` identifies promoted copies, so leave that list unchanged when editing settings. Edit the authoritative shared entry after promotion. Conflicting legacy identities or nonempty ticket settings require explicit reconciliation; WorkLog does not rewrite journal history or silently choose a scope. See [worktree migration and recovery](docs/installation.md#worktree-context-and-legacy-configuration).

Restart the MCP/client after configuration edits. Match prefixes before the first slash, case-insensitively. The missing `bugfix` alias is added for existing configurations too; explicit mappings such as `BUGFIX: maintenance` take precedence. Use `bugfix: unclassified` to opt out of that alias. Explicit classification overrides mappings; unknown prefixes become unclassified. Existing entries retain their classification. Explicit tickets override branch extraction. Inference uses the effective repository's configuration, including when `entry.repository` overrides the initialized repository. An extracted ID without configured provider/scope is retained as unresolved metadata, not assigned a guessed provider. For Azure DevOps use provider `azure-devops`, scope `organization/project`, and a numeric ID; the ticket flags preserve the existing pattern, so set a suitable named `id` capture for numeric branch IDs or supply explicit tickets. Ticket fields are trimmed before storage and filtering. Ticket-only work groups by its sorted set of qualified references, independent of input order.

## Development and releases

WorkLog checks the public release feed in the background, at most once per 24 hours for servers sharing a data directory. A newer stable release appears as optional `updateHint` metadata in successful MCP tool results. Managed assistant instructions ask Codex and Claude Code to mention it once per version per session; presentation depends on the assistant. A failed release check never blocks logging or reporting, and checks send no journal/project data.

```powershell
worklog update --check
# Compatible updates preserve existing conversations:
worklog update
worklog update --status
# Explicitly select the previous compatible payload if needed:
worklog update --rollback
```

The stable launcher selects an immutable version folder for each new process. A compatible update verifies the package and activates a new selection while existing conversations keep their original server and connection. `staged` means activation has not committed; `activated` means new processes use the selection. `--status` reports running and selected versions offline. Existing conversations adopt the new code at their next normal reconnect/restart. Rollback changes application selection only and preserves journal/configuration data. Keep `--data-dir` consistent for cache and operation receipts.

Pre-launcher installations need a [one-time manual maintenance migration](docs/installation.md#one-time-migration-from-the-legacy-layout). Launcher changes and incompatible data changes also require maintenance. Journal schema remains 2; ordinary updates require matching launcher/shared-data contracts. Published payloads are retained on disk; automatic cleanup is deferred. Rerun init when managed instructions need refreshing; owned registrations always use the stable launcher.

Set `"updates": { "enabled": false }` in the private config and restart the server to disable automatic checks and notices. Explicit `update --check` and `update` remain available. Source builds must use the manual ZIP installation path before self-update can be used.

See [code organization](docs/architecture.md) for component responsibilities and the logging, reporting, and initialization paths.

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
./scripts/package.ps1
# Builds two distinct disposable versions and exercises live updates and launcher lifecycle:
./scripts/package-update-fixtures.ps1
```

Source builds also require Node.js (the tested version is in `dashboard/.nvmrc`). Run `npm ci --prefix dashboard` for a locked install. The .NET build compiles the Angular production client when inputs change and embeds its assets; missing or stale assets fail validation. Run `npm test --prefix dashboard` for Angular tests and `npm run test:e2e --prefix dashboard` after a Release build for isolated Edge browser acceptance. Browser acceptance supports `WORKLOG_DASHBOARD_EXECUTABLE` to exercise a packaged executable and `WORKLOG_BROWSER_CHANNEL` to choose an installed browser channel. Its sample repositories and journal remain under ignored `artifacts/dashboard-e2e` directories.

With both agent CLIs installed, run `dotnet run --project tools/WorkLog.Smoke -c Release --no-build` for an isolated packaged-executable smoke test. It uses temporary client configuration, verifies private registration/instruction loading, checks Claude's MCP connection, and exercises journal tools. Add `-- --live-agents` only when you intend to use your authenticated accounts and send test prompts to their services; that optional test asks each agent to log and query synthetic entries. Logs stay under ignored artifacts/smoke directories.

GitHub Actions validates pull requests and pushes to main, including packaged update acceptance. Update Version in Directory.Build.props, commit, wait for primary-branch validation, and push a matching `vMAJOR.MINOR.PATCH` tag to publish a Windows ZIP and checksum after validation. Observe the release job and verify the downloaded assets before announcing publication. See [release operations](docs/releases.md). Creating the GitHub repository and pushing/tagging are separate actions; no remote is created by the application. macOS/Linux are not yet release-validated.
