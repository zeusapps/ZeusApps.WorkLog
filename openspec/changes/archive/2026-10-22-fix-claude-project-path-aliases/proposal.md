# Proposal

## Why

F7 reports that Claude's Windows project key can use a different path casing from Git's root. WorkLog currently updates and checks ownership at only the exact Git spelling, leaving an existing alias stale or an unrelated registration undetected. See [the saved report](../../../../docs/handoffs/2026-10-21-claude-integration-flaws.md) for original evidence and its limits.

## What Changes

- On Windows, find existing project keys equivalent to the checkout root after separator normalization and ordinal case-insensitive comparison.
- Validate every matching alias before writes; update only owned `worklog` registrations under existing aliases and the canonical Git-root key, preserving each project's other content.
- Make repeat init and dry-run describe and reconcile aliases safely, including persistent instruction opt-out.
- Add isolated regression and native Claude checks for the reported mixed-case launch scenario. Retain exact-case matching outside Windows.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `local-agent-integration`: define Windows project-alias reconciliation and ownership protection during repeatable initialization.

## Impact

Primary code: `src/WorkLog/Integration/Clients/ClaudeIntegration.cs`, with focused integration fixtures and `tools/WorkLog.Smoke`. Update installation/validation documentation. No database migration, new dependency, global registration, Codex behavior change, or arbitrary enumeration of path spellings. Applies independently to v0.1.1; F6 remains in `verify-worktree-server-startup`.
