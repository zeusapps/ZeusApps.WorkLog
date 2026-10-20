# WorkLog

A Windows-first .NET MCP server for a private local work journal. Codex and Claude Code record what you actually worked on; either assistant can retrieve structured facts for daily or periodic reports.

```powershell
worklog --version
worklog init --repository D:\Code\MyProject --dry-run
worklog init --repository D:\Code\MyProject
```

Install a self-contained Windows x64 release from [GitHub Releases](https://github.com/zeusapps/ZeusApps.WorkLog/releases). No separately installed .NET runtime is needed. See [installation and upgrades](docs/installation.md).

`init` defaults to both clients and requires Git plus Codex CLI 0.162.1+ and Claude Code 2.1.289+. Select just one with `--clients codex` or `--clients claude`. Initialization preserves shared instructions and uses private configuration. Restart clients and follow their normal repository trust flow after initialization.

## Journal behavior

- `started`: successful OpenSpec change creation, including `openspec-propose`.
- `in_progress`: actual meaningful session work, including resuming an unfinished feature.
- `done`: the session establishes reporting completion; archive dates are independent.

An unfinished feature generates no entries on days you do not work on it. Switching features does not finish the previous one. Multiple observations on a day remain available in reports. This is an activity journal, not a timer.

Automatic logging is instructed through local agent configuration. It depends on the agent calling the tools: abruptly terminated sessions or ignored instructions can miss activity. Logging failures must be reported, and tools can also be called explicitly.

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

Report `schemaVersion: 1` defaults to `view: "compact"`: observations appear once in `entries`, and daily groups contain `entryIds`. Pages default to 25 observations (`pageSize`: 1–100) and at most 64 KiB of structured JSON, so pages may be smaller. While `hasMore` is true, pass `nextCursor` as `period.cursor` with every other option unchanged. Combine all pages before writing a complete report; a daily group can span pages. Continuation excludes observations written after the first page, even if backdated.

Other views are `flat` (entries only), `grouped` (full observations in daily groups only), and `legacy` (both full views; count observations once). Every view is paginated. Use `includeSummaries: false` for smaller reports or when a single summary exceeds the byte budget. Cursor tokens are local continuation state, not credentials; start a new query after switching journals or changing filters.

## Classifications and ticket references

Configuration lives in `%LOCALAPPDATA%\ZeusApps.WorkLog\config.json` by default, alongside `journal.db`. Override with `--data-dir PATH` or `WORKLOG_DATA_DIR`. Each machine is independent. There are no issue-service connections or credentials; use separate official issue MCPs for lookup.

After init, edit your private config to customize mappings or repository ticket context:

```json
{
  "branchClassifications": {
    "feature": "feature",
    "bugs": "bug",
    "hotfix": "bug",
    "chore": "maintenance"
  },
  "repositories": {
    "D:\\Code\\MyProject": {
      "identity": "github.com/company/project",
      "provider": "jira",
      "scope": "company.atlassian.net",
      "ticketPattern": "(?<id>[A-Z][A-Z0-9]+-\\d+)"
    }
  }
}
```

Restart the MCP/client after configuration edits. Match prefixes before the first slash, case-insensitively. Explicit classification overrides mappings; unknown prefixes become unclassified. Existing entries retain their classification. Explicit tickets override branch extraction. Inference uses the effective repository's configuration, including when `entry.repository` overrides the initialized repository. An extracted ID without configured provider/scope is retained as unresolved metadata, not assigned a guessed provider. For Azure DevOps use provider `azure-devops`, scope `organization/project`, and a numeric ID; custom branch patterns must have a named `id` capture. Ticket fields are trimmed before storage and filtering. Ticket-only work groups by its sorted set of qualified references, independent of input order.

## Development and releases

WorkLog checks the public release feed in the background, at most once per 24 hours for servers sharing a data directory. A newer stable release appears as optional `updateHint` metadata in successful MCP tool results. Managed assistant instructions ask Codex and Claude Code to mention it once per version per session; presentation depends on the assistant. A failed release check never blocks logging or reporting, and checks send no journal/project data.

```powershell
worklog update --check
# Stop MCP clients using this installation before installing:
worklog update
worklog update --status
```

The update command verifies and stages the package, then starts a helper to replace the idle installation after the command exits. `staged` means installation is still pending; check `--status` for `installed` or `failed`. Restart clients afterward and rerun init where managed instructions need refreshing. Keep `--data-dir` consistent across these commands when using a custom journal directory.

Set `"updates": { "enabled": false }` in the private config and restart the server to disable automatic checks and notices. Explicit `update --check` and `update` remain available. Source builds must use the manual ZIP installation path before self-update can be used.

See [code organization](docs/architecture.md) for component responsibilities and the logging, reporting, and initialization paths.

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
./scripts/package.ps1
```

With both agent CLIs installed, run `dotnet run --project tools/WorkLog.Smoke -c Release --no-build` for an isolated packaged-executable smoke test. It uses temporary client configuration, verifies private registration/instruction loading, checks Claude's MCP connection, and exercises journal tools. Add `-- --live-agents` only when you intend to use your authenticated accounts and send test prompts to their services; that optional test asks each agent to log and query synthetic entries. Logs stay under ignored artifacts/smoke directories.

GitHub Actions validates pull requests and pushes to main, including packaged update acceptance. Update Version in Directory.Build.props, commit, wait for primary-branch validation, and push a matching `vMAJOR.MINOR.PATCH` tag to publish a Windows ZIP and checksum after validation. Observe the release job and verify the downloaded assets before announcing publication. See [release operations](docs/releases.md). Creating the GitHub repository and pushing/tagging are separate actions; no remote is created by the application. macOS/Linux are not yet release-validated.
