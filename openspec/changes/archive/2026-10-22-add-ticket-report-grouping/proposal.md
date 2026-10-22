# Proposal

## Why

F8 reports that one ticket appears in separate daily groups when historical observations have no change and newer ones do. Tickets are the team's shared reference, while OpenSpec changes describe an individual's implementation workflow. Reporting should group by ticket by default and retain explicit change grouping for reports that need it. See [the saved report](../../../../docs/handoffs/2026-10-21-claude-integration-flaws.md).

## What Changes

- Add optional `period.groupBy: "change" | "ticket"` to `query_work`, defaulting to `ticket`; explicit `change` retains the existing change-first identities. This intentionally changes daily grouping for existing requests that omit the selector.
- In ticket mode, group each observation by its repository and sorted distinct qualified ticket set regardless of change; preserve change metadata on observations. Fall back to existing change identity when no qualified ticket exists.
- Keep one group assignment per observation, including multi-ticket observations; never qualify unresolved IDs using current settings or merge tickets by bare ID alone.
- Bind grouping mode into cursor matching and generated MCP input guidance, with examples and regression coverage for views, pagination, and payload bounds.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `period-reporting`: ticket-first default daily grouping, explicit change grouping and stable continuation semantics.

## Impact

Primary code: `QueryRequest`, `ReportPager`, `ReportBuilder`, `WorkIdentity`, and `DailyGroup` in `src/WorkLog/Reporting`, plus MCP schema/protocol tests, reporting tests, packaged journal smoke, and documentation. Keep response schema version 1 and the existing shape; no stored-entry rewrite, database migration, external issue lookup, or new package. Independently applicable to v0.1.1; no F6, F7, or F9 dependency.
