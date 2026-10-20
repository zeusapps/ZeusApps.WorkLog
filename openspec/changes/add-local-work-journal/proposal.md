# Proposal

## Why

Daily work reports need reliable records of what was started, worked on, and finished, even when OpenSpec archive dates lag behind actual completion. A local journal shared by Codex and Claude Code removes the need to reconstruct activity from branches and change directories.

## What Changes

- Add a Windows-first .NET application with a local SQLite journal and stdio MCP tools `log_work` and `query_work`.
- Record session activity, timestamps, repository and branch, OpenSpec change, optional ticket references and summaries, and resolved work classification.
- Query explicit reporting periods with timezone-aware daily grouping and filters; leave narrative generation to the consuming assistant.
- Add repeatable repository initialization for private Codex and Claude Code integration, including CLAUDE.local.md where needed, without modifying shared instructions or OpenSpec skills.
- Infer configurable classifications from branch prefixes; default feature to feature and bugs/hotfix to bug. Store references only for Jira and Azure DevOps.
- Provide GitHub CI and tagged, versioned Windows release artifacts, installation documentation, and upgrade-safe storage.

## Capabilities

### New Capabilities

- `work-journal`: Durable local activity capture, metadata inference, and retry-safe logging.
- `period-reporting`: Structured, timezone-aware period queries and daily grouping.
- `local-agent-integration`: Private initialization and common session logging behavior for Codex and Claude Code.
- `application-distribution`: Windows installation, application versions, journal upgrades, CI, and release packaging.

### Modified Capabilities

None; the project has no existing capability specs.

## Impact

Greenfield .NET source, tests, SQLite persistence, official C# MCP SDK, client-specific configuration adapters, documentation, and GitHub Actions workflows. No remote journal, synchronization, issue-service credentials, ticket API calls, or automatic GitHub repository creation are included. macOS/Linux portability is desirable but not required for the initial release.
