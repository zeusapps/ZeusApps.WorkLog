# WorkLog

A Windows-first .NET MCP server for a private local work journal. Codex and Claude Code record what you actually worked on; either assistant can retrieve structured facts for daily or periodic reports.

```powershell
worklog --version
worklog init --repository D:\Code\MyProject --dry-run
worklog init --repository D:\Code\MyProject
```

Install a self-contained Windows x64 release from GitHub Releases. No separately installed .NET runtime is needed. See [installation and upgrades](docs/installation.md).

`init` defaults to both clients and requires Git plus Codex CLI 0.162.1+ and Claude Code 2.1.289+. Select just one with `--clients codex` or `--clients claude`. Initialization preserves shared instructions and uses private configuration. Restart clients and follow their normal repository trust flow after initialization.

## Journal behavior

- `started`: successful OpenSpec change creation, including `openspec-propose`.
- `in_progress`: actual meaningful session work, including resuming an unfinished feature.
- `done`: the session establishes reporting completion; archive dates are independent.

An unfinished feature generates no entries on days you do not work on it. Switching features does not finish the previous one. Multiple observations on a day remain available in reports. This is an activity journal, not a timer.

Automatic logging is instructed through local agent configuration. It depends on the agent calling the tools: abruptly terminated sessions or ignored instructions can miss activity. Logging failures must be reported, and tools can also be called explicitly.

## MCP tools

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

`from` is inclusive and `to` exclusive. The timezone controls calendar-day grouping; bounds must include offsets. Optional filters: repository, change, ticket (ID or `provider:scope:id`), status, classification. The versioned response includes entries and daily groups with all observations, without invented idle-day activity. The assistant writes the report prose.

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

Restart the MCP/client after configuration edits. Match prefixes before the first slash, case-insensitively. Explicit classification overrides mappings; unknown prefixes become unclassified. Existing entries retain their classification. Explicit tickets override branch extraction. An extracted ID without configured provider/scope is retained as unresolved metadata, not assigned a guessed provider. For Azure DevOps use provider `azure-devops`, scope `organization/project`, and a numeric ID; custom branch patterns must have a named `id` capture.

## Development and releases

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
./scripts/package.ps1
```

With both agent CLIs installed, run `dotnet run --project tools/WorkLog.Smoke -c Release --no-build` for an isolated packaged-executable smoke test. It uses temporary client configuration, verifies private registration/instruction loading, checks Claude's MCP connection, and exercises journal tools. Add `-- --live-agents` only when you intend to use your authenticated accounts and send test prompts to their services; that optional test asks each agent to log and query synthetic entries. Logs stay under ignored artifacts/smoke directories.

GitHub Actions validates pull requests and pushes to main. Update Version in Directory.Build.props, commit, and push a matching `vMAJOR.MINOR.PATCH` tag to publish a Windows ZIP and checksum after validation. Creating the GitHub repository and pushing/tagging are separate actions; no remote is created by the application. macOS/Linux are not yet release-validated.
