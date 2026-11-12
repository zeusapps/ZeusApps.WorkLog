# Proposal

## Why

The Daily view's agent digests are not usable at the user's stand-up. The user's stored digests show the cause. A digest request already lists every observation of the day, but the digest usually restates the latest summary. Earlier achievements are dropped, and development-process detail leaks in (task numbers, test counts, acceptance steps). Each digest is a single sentence, so the stand-up's three questions blur together: done, next, blocked. The Daily page is also hard to scan. It uses wide text in a stack of tiles, Today sits below the fold, and Blockers is always an empty prompt. The user wants this fixed in v0.5.0.

## What Changes

- **Structured digests.** A digest becomes three bullet lists, `done`, `next` and `blockers`, instead of one free-text sentence. `done` combines the achievements of all of the day's observations, not only the latest. `next` gives the concrete next steps in plain words. `blockers` lists only what needs someone else, and is empty otherwise. **BREAKING (MCP tool input):** `save_digest` takes `done`, `next` and `blockers` lists. The legacy `text` input is still accepted and stored as a single Done bullet, so clients that have not reconnected keep working.
- **A digest for every item with observations, including the first of the day**, so every Daily row has the same Done / Next / Blockers shape. The existing exclusions stay: retries, items with more than 30 observations, and observations with neither ticket nor change.
- **Team-relevant guidance only.** The shared summary and digest guidance excludes the development process explicitly. That covers OpenSpec mechanics (proposing, creating, archiving or syncing changes, specs and tasks) and any agent or role (developer, QA, analyzer, reviewer, bug-hunter or any other) and their findings or rounds, plus the existing list: test and task counts, commits, hashes, branches, merges, builds and installation. Summaries and digests are written in the user's own voice, without referring to "the user" or the agent. The guidance gains wording for `started`. Digest guidance forbids restating only the latest entry.
- **Daily page redesign.**
  - Today and Yesterday sit side by side on wide screens, as narrow columns, with Today first. Narrow screens stack them with Today on top.
  - Each work item is a light row rather than a tile. It shows Done, Next and Blockers bullets when a matching digest exists, and the latest summary as a single Done bullet otherwise.
  - A Blockers strip lists the current blockers recorded in today's digests, replacing the empty prompt.
  - Copy for daily keeps the Yesterday / Today / Blockers sections, now with bullets and the derived blockers.
- **Compatibility.** The journal schema, shared-data contract and launcher are unchanged. The digest store keeps its version and gains structured parts in an additive table that older processes ignore. Older digests remain readable. After updating, the user reruns `worklog init` to refresh the managed instructions and reconnects agent clients to pick up the new tool schema.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `day-digests`: request on every observation, structured `save_digest` input, additive storage, and the revised writing guidance.
- `daily-preparation`: structured digest display, derived blockers, bulleted copy, and the side-by-side layout.
- `local-agent-integration`: revised summary guidance and digest instructions for both integrations.

## Impact

- MCP server: `src/WorkLog/Journaling/LoggingGuidance.cs`, `DigestService.cs`, `DigestRequest.cs`, `LogResult.cs`; `src/WorkLog/Mcp/JournalTools.cs` and `UnknownFieldValidation.cs`; `src/WorkLog/Persistence/DigestStore.cs`.
- Integration: `src/WorkLog/Integration/AgentInstructions.cs`. Instruction text is shared, so Claude Code and Codex stay identical, with parity tests.
- Dashboard: `src/WorkLog/Dashboard/DailyService.cs`; `dashboard/src/daily.ts`, `models.ts`, `app.html`, `app.ts` and `styles.css`; unit and e2e tests.
- Docs: `docs/releases.md` (v0.5.0 notes, including the rerun-`worklog init` and reconnect steps), plus architecture and validation notes where digests are described.
