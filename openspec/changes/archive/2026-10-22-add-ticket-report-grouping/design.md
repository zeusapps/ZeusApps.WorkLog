# Design

## Context

See proposal.md and [the handoff](../../../../docs/handoffs/2026-10-21-claude-integration-flaws.md). Baseline is v0.1.1 / `fa48ada`. `WorkIdentity.For` first returns repository/change, otherwise a sorted distinct set of provider/scope/ID ticket keys. `ReportBuilder.Build` groups each page by date and that identity. `ReportPager` normalizes the request, hashes it without the cursor, applies snapshot pagination, and enforces the 64 KiB serialized budget. MCP rejects unknown fields, so adding a documented request member and protocol coverage is necessary.

## Goals / Non-Goals

**Goals:** Combine the same qualified ticket across change-present and change-absent observations by default, matching the team's ticket-based reporting. Retain entry identity, explicit change-first grouping and pagination guarantees.

**Non-Goals:** Rewriting migration data, guessing missing ticket context, changing the ticket filter, retrieving issue details, aggregating across repositories, removing observations, or presenting whole-period totals from a single page.

## Decisions

### Default to ticket grouping and retain explicit change grouping

Append `string GroupBy = "ticket"` to `QueryRequest` so existing constructor callers retain their argument order. Accept exactly `change` and `ticket`; omitted means `ticket`. Tickets are the shared team reference; OpenSpec changes remain optional implementation metadata. Existing requests intentionally adopt ticket-first daily groups. Explicit `change` restores the previous change-first behavior with its qualified ticket fallback. Reject explicit null, blank, and unknown values with a `period.groupBy` error before reading a page. Explain both modes and the new default in generated input descriptions.

Pass the normalized selector into `ReportBuilder` and `WorkIdentity`. Leave `Report`, `DailyGroup` properties, and schema version 1 unchanged; update guidance for `DailyGroup.WorkIdentity` to mention selectable grouping. A report does not need a new echoed selector field: the caller supplies it. Flat view still validates the selector but returns the same entries and no days.

### Ticket mode uses the complete qualified ticket set

For `ticket`, if qualified tickets exist use exactly the current ticket-only identity algorithm: trim fields, distinct ordinal tuples/keys, sort deterministically, and retain the existing single-ticket and multi-ticket encodings. Ignore change only for selecting the group; preserve the original change and tickets on observations. Thus two same-day observations in one repository with the same single qualified ticket, one with change `repair-links` and one without, form one group by default. Explicit change mode keeps their existing separate identities.

Multiple-ticket observations remain assigned to one identity based on the whole ticket set. Sets `{A}` and `{A,B}` remain separate; reordered `{A,B}` and `{B,A,A}` group together. This avoids duplicating observations or silently inflating counts. Document that this is ticket-set grouping, not one duplicated projection per individual ticket. Repository, provider, and scope stay part of identity; bare ID equality alone never joins unrelated work.

Without qualified tickets, fall back to the existing `WorkIdentity.For` behavior (normally a change, because logging requires a change or qualified ticket). `extractedTicketId` does not become a qualified identity. An unresolved observation and a qualified observation need not coalesce, even when their bare IDs match. This intentionally preserves F2's distinction between search matching and ticket qualification.

### Bind mode to continuation and retain page-local grouping

The normalized request hash already covers request properties. Ensure it covers `GroupBy`; requests omitting it and explicitly specifying `ticket` must normalize identically. Switching selector on a continuation fails through the current cursor validation. Existing cursors from older binaries may fail after this request-shape extension; document restarting the query after upgrading, with no stored-data migration.

Keep SQL filtering, ordering, snapshot isolation, page size, view projection, summary omission, and serialized byte-budget logic unchanged. Groups can span pages; callers combine fragments with the same date/workIdentity after fetching all pages. Do not infer final status from one fragment.

### Verification and fresh-session entry points

Extend `tests/WorkLog.Tests/Reporting/ReportTests.cs` and `PaginationTests.cs`, with focused identity tests as useful. Cover omitted vs explicit ticket, preserved legacy identities with explicit change, the single-ticket F8 example, multiple changes for one ticket, repository/provider/scope separation, sorted/deduplicated sets, unresolved fallback, all four views, timezone day boundaries, filters, snapshots, cursor rejection on mode change, Unicode summaries, and payload limits.

Extend `tests/WorkLog.Tests/Mcp/McpTests.cs`: tools/list exposes `period.groupBy`; both modes return schema-valid structured content equivalent to text fallback; invalid values and misspellings fail without journal writes, and subsequent queries work. Use the existing SDK fixtures, not a new protocol layer. Add a packaged journal smoke case in `tools/WorkLog.Smoke/JournalAcceptanceChecks.cs` exercising the mixed historical/new records through the tool.

```powershell
dotnet restore --locked-mode
dotnet test tests/WorkLog.Tests/WorkLog.Tests.csproj -c Release --filter 'FullyQualifiedName~Reporting|FullyQualifiedName~Mcp'
powershell -NoProfile -File scripts/package.ps1
dotnet run --project tools/WorkLog.Smoke -c Release
```

Run the full suite before completion. The complete smoke also requires native clients. Update README reporting examples, `docs/architecture.md`, and `docs/mcp-validation.md` with the changed default, explicit legacy grouping, ticket-set semantics, unresolved fallback, and complete-pagination guidance.

## Risks / Trade-offs

- One ticket can be linked to several changes -> ticket mode intentionally combines them while observations retain each change.
- Multi-ticket sets remain distinct -> document this before consumers treat groups as per-ticket totals.
- Adding a query property changes the cursor fingerprint -> reject stale cursors clearly and restart the query.
- Existing reports now combine changes for the same ticket -> document the intentional default change; regression assertions compare omitted/explicit `ticket` and preserve legacy identities under explicit `change`.

## Migration Plan

No journal/schema migration. Restart the MCP client after upgrading to refresh tool discovery and restart queries. Requests that omit `period.groupBy` now use ticket grouping. Reports requiring the previous grouping must set `period.groupBy: "change"`. Observation metadata, response shape and schema version 1 remain unchanged. On rollback, omit the new option because older binaries reject it; those binaries resume their original change-first default. No F6/F7/F9 prerequisite exists.
