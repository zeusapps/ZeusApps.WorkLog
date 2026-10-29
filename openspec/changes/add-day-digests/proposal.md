# Proposal

## Why

The Daily view (`add-daily-prep`) shows each work item's latest recorded summary for a day. When an item has several observations that day, the latest summary describes only the last step, not what the day achieved. The user's stand-up line is "yesterday I did X with result Y, today I'm working on Z", which needs a short rollup of the day per item. WorkLog has no language model of its own, and it cannot borrow the agent's one:
- MCP sampling is not supported by Claude Code (anthropics/claude-code#1785).
- OpenAI declined it for Codex (openai/codex#4929).
- The MCP specification deprecated it in revision `2026-07-28` (SEP-2577).

The agent that has just logged the work has the best context for that rollup, so WorkLog can ask it through the tool result, the same way `updateHint` already works.

## What Changes

- **Rolling digest request.** When `log_work` records an observation and the item now has two or more observations on the local day of that observation, the result includes an optional `digestRequest`:
  - the item identity, using ticket grouping, which matches the Daily view and is normally 1:1 with the change,
  - the observations it covers, with their summaries and statuses,
  - a short instruction on what to write.

  An item with a single observation that day gets no request, because its summary already is the day's line.
- **New MCP tool `save_digest`.** The agent sends the item, the exact set of observations covered, and one or two sentences. WorkLog stores it only if that set is still the item's complete set for that day. Otherwise it reports the digest as stale and stores nothing.
- **Digests are stored per item per day.** A newer accepted digest for the same item and day replaces the older one. Digests never change observations.
- **Shared managed instructions for Claude Code and Codex.** After a successful `log_work` that carries a `digestRequest`, the agent writes the digest and calls `save_digest` once, after the user's request is handled, and never blocks it. The digest states outcomes, not process, and holds no secrets or personal data. An ignored or failed request is allowed; the agent reports a failed save as it reports logging failures.
- **The Daily view prefers a matching digest.** For each Yesterday line and each Today line with observations recorded today, Daily shows and copies the stored digest when the digest covers exactly the observations Daily selected for that item and day. Otherwise it shows the latest summary as before. Matching on the covered set, not on dates, means the server and the dashboard never have to agree on day boundaries or timezones. A digest is not used if its observations differ from Daily's selection, for example after a later or backdated entry, a different History timezone, or a skipped request.
- Because the request rolls with every log, the last log before the user's late-day stand-up (US clients) leaves Today's digests current. No end-of-day trigger is needed.
- Not in scope: server-side model calls, provider API keys, MCP sampling, digest editing in the dashboard, and digests for the Blockers section.

## Capabilities

### New Capabilities
- `day-digests`: Agent-written per-item day digests. Covers the rolling `digestRequest` on `log_work` results, the `save_digest` tool, the rule that a digest must cover the item's complete observation set for that day to be accepted, persistence, and replacement.

### Modified Capabilities
- `local-agent-integration`: The managed instructions for both clients add the digest behavior: write and save once per request, after the user's request, never blocking it, and report failures.
- `daily-preparation`: Yesterday and Today lines prefer a digest whose covered observations exactly match the line's observations, and fall back to the latest summary. This capability is introduced by `add-daily-prep` and must be archived before this change.

## Impact

- Server:
  - `LogResult` gains an optional `digestRequest`. It is additive, and existing clients ignore it.
  - New `save_digest` tool in `JournalTools`.
  - New digest storage in the journal database, with a schema migration.
  - `POST /api/daily` returns the matching digest for each item.
- Integration: `AgentInstructions.cs` gets shared instruction text, with no per-client adapter changes expected. `worklog init` refreshes managed instructions as it already does after an upgrade.
- Client: Daily line rendering and copy formatting use the digest when present.
- Cost: one short extra agent turn per log for items with two or more observations that day, using the session the user is already running.
- Prerequisites: `add-daily-prep`. Complements `improve-observation-summaries`.
