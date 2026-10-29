# Design

## Context

See proposal.md for motivation and specs for requirements. Relevant current code:
- `JournalTools` (`src/WorkLog/Mcp/JournalTools.cs`) exposes `log_work` and `query_work`, and enriches results with `updateHint`. `McpServerHost` registers the tool type with `McpJson.Options`.
- `Journal.Log` appends through `SqliteEntryStore` to `journal.db` (`JournalDatabase`, schema 2 via `JournalSchema`). WAL mode, a named mutex around schema upgrade, `busy_timeout` 15 s.
- The installer and launcher accept a payload only when its `JournalSchema` and `SharedDataContract` match (`InstallationLayout`), and the `work-journal` spec forbids a compatible payload from changing the journal schema. Several MCP server processes from different installed versions can share one journal.
- Work identity under ticket grouping: `WorkIdentity.For(entry, "ticket")`, hashed by `TimelineService.IdFor`. The Daily view (`DailyService`, `dashboard/src/daily.ts`) builds Yesterday items per identity and Today lines per board card, including change-card lines assembled from several identities' entries (ANALYSIS-001, ANALYSIS-041 of `add-daily-prep`).
- Managed instruction text is shared in `AgentInstructions.Render`, used by both client adapters.

## Goals / Non-Goals

**Goals:** an additive MCP contract that works identically in Claude Code and Codex; no change to journal schema, shared-data contract, or update path; a digest is only ever shown where it covers exactly what the line shows.

**Non-Goals:**
- Any server-side model call, API key, or MCP sampling.
- Digests for periods other than one local day, or for Blockers.
- Editing or deleting digests from the dashboard; retention policies (digests are tiny and derived).

## Decisions

### 1. Separate digest store
Digests live in `digests.db` next to `journal.db`, with its own `PRAGMA user_version` (1) and the same connection settings (WAL, busy timeout, a named mutex around creation and upgrade). One table:

```sql
CREATE TABLE digests (
  item TEXT NOT NULL,          -- TimelineService.IdFor(entry, "ticket")
  day TEXT NOT NULL,           -- yyyy-MM-dd in the machine timezone
  covers TEXT NOT NULL,        -- JSON array of entry IDs, sorted ordinally
  text TEXT NOT NULL,
  saved INTEGER NOT NULL,      -- UTC ticks
  PRIMARY KEY (item, day)
);
```

A `DigestStore` class in `Persistence` opens it lazily. Any failure to open, upgrade, or read the store is caught at the call site and turns digests off for that call (no request, `save_digest` fails with the reason); logging and querying never depend on it. A store version newer than supported is treated the same way.

**Alternative: a journal migration to schema 3.** Rejected. It changes `JournalSchema` in the install contract, so every user needs a maintenance update, and older running servers refuse the journal ("schema 3 is newer than supported"), which breaks the mixed-version operation the `work-journal` spec requires.

**Alternative: a table added to `journal.db` without a version bump.** Rejected: it changes the journal schema behind the contract's back.

### 2. When `log_work` asks for a digest
After `Journal.Log` returns `Created = true`, `JournalTools` computes the request:
- the new entry's identity `IdFor(entry, "ticket")` and its local date in `TimeZoneInfo.Local`,
- all journal entries with that identity whose `occurred` falls in that local day (one bounded read using the `(occurred, id)` index for the day's UTC range, then an identity filter),
- a request when there are 2 to 30 of them.

`LogResult` gains `DigestRequest? DigestRequest` (omitted when null, like `UpdateHint`): `{ item, day, timezone, entries: [{ id, occurredAt, status, change, tickets, summary }], instruction }`, entries oldest first. `instruction` is a fixed one-line reminder ("Write one or two sentences on what these observations achieved today and call save_digest with exactly these entry IDs"); the policy lives in the managed instructions. The request computation is wrapped so an exception drops the request and keeps the logging result.

Machine timezone is used because the server has no other user timezone. The dashboard does not depend on it (Decision 4).

### 3. `save_digest`
`[McpServerTool(Name = "save_digest", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]` with input `{ entryIds: string[], text: string }` and result `{ item, day, covers, text }`.

Validation, in order, each failing with `McpException` and no write:
1. Text normalized (trim, line breaks → spaces, collapse repeated spaces), 1–280 characters.
2. 2–30 distinct IDs, all stored entries.
3. One identity and one machine-local date across them.
4. The set equals that identity's current entries on that date ("stale: a later observation exists; do not retry").

Then upsert by `(item, day)`. The check-then-write race with a concurrent `log_work` can store a digest that is already stale; Daily's exact-match rule (Decision 4) never shows it, and the next request replaces it. No lock across the two databases is needed.

**Alternative: let the agent pass the item and day.** Rejected; deriving them from the IDs removes a class of mismatched inputs.

### 4. Daily view matching
`POST /api/daily` adds `digests: { covers: string[], text: string }[]`: the stored digests whose `item` is the identity of any entry in the response (both the selected day and today). `DailyService` reads them through `DigestStore` (read-only open; missing or unreadable store → empty list).

The client matches by set equality: for each line, the sorted IDs of `primary` plus `others` must equal a digest's `covers`. This covers Yesterday items, ticket-card Today lines, and assembled change-card Today lines alike; in the usual one-ticket-per-change case a change-card line's entries are one identity's entries and match. The line shows the digest text with a small "agent digest" mark; status and collapsed observations are unchanged. `dailyText` uses the digest text when the line shows one (no mark).

### 5. Instructions and mixed versions
`AgentInstructions.Render` gains the digest paragraph from the spec, once, for both clients. Older servers simply never send `digestRequest`. A newer instruction block connected to an older server is harmless: no request arrives; the paragraph also says to skip when `save_digest` is unavailable. No adapter changes in `Integration/Clients`.

### 6. Planned live-agent evaluation
Isolated MCP checks prove the server contract and the instruction text, but not whether real Claude Code and Codex models follow the digest instructions. That is prompt quality, and only live sessions show it. The project currently forbids reviewers from running `--live-agents`. This change relaxes that rule to allow live agents as planned work:
- `.agents/roles/project-facts.md` (Safety): live agents use the user's authenticated accounts and send prompts to their services. Run them only when the assignment includes a planned live-agent evaluation (for example, judging how real clients follow managed instructions or tool-result prompts). The assignment names the clients, the number of sessions, and the per-session budget. Run them only in disposable repositories and data directories with the smoke tool's isolation flags. Never use the user's real repositories, data directory, installation, or client settings, and never put secrets or personal data in prompts. Record client versions, models, and redacted prompts and outputs as evidence. Without such an assignment, live agents are not run. The default isolated smoke run is always allowed.
- `.agents/workflows/openspec-delegation.md`: a live-agent evaluation is planned work. It must appear in the change's `tasks.md`, and the assignment repeats its clients, session count, and budget. Parity applies: evaluate Claude Code and Codex alike unless a restriction is evidenced.

`LiveAgentChecks` already isolates both clients (a disposable home and repository; Codex `--ignore-user-config --ephemeral -s read-only`; Claude `--no-session-persistence --strict-mcp-config`, WorkLog tools only, `--max-budget-usd 1`). The digest scenario adds `mcp__worklog__save_digest` to Claude's allowed tools and keeps every other flag.

Outcomes are recorded as observations, not pass/fail gates, because model behavior varies between runs. A consistent miss feeds a follow-up to tune the instruction text.

**Alternative: leave live checks to the user after release.** Rejected by the user. Prompt quality should be evaluated before release, as part of the change.

## Risks / Trade-offs

- [Agents ignore or skip requests] → Daily falls back to summaries; nothing depends on digests.
- [Extra tokens per log] → Only for items with 2+ observations that day; capped at 30 entries; one short turn.
- [Machine timezone differs from the History timezone] → Digests near midnight may not match and are not shown; documented.
- [Two databases can disagree momentarily] → Exact coverage match at display time; stale digests are invisible.
- [Prompt-injection surface: summaries flow back to the agent as data] → They are the agent's own local records; the request labels them as data and the instructions forbid acting on them.
- [Live evaluation spends the user's quota and sends prompts to Anthropic and OpenAI] → Planned and bounded: two sessions, USD 1 each, synthetic prompts only, isolated configuration.
- [Structured result size] → 30 entries × short summaries stays far below the existing structured-content bound; `query_work` paging is unaffected.

## Migration Plan

Additive. A new payload creates `digests.db` on first use; older payloads never touch it. Rolling back leaves an unused file. Users rerun `worklog init` after upgrading to receive the digest instructions, as with any instruction change.
