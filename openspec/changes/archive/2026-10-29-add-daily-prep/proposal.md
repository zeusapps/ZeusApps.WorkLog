# Proposal

## Why

WorkLog's original purpose is to track activity over time. Its most frequent practical use is answering "what did I do on my last working day?" before a daily stand-up. The user's agent skill for dailies queries the journal, follows ticket links, and looks for open questions in the tickets. That is thorough but slow. Before an informal stand-up the user needs a fast, factual answer they can scan in seconds and paste if needed, without waiting for an agent.

## What Changes

- Add a Daily view (`/daily`) in the classic yesterday / today / blockers structure.
- **Yesterday** is the most recent earlier local date with recorded observations in the selected repositories. This handles weekends, holidays, and absences without a working-day calendar. The view names the date ("Thursday 22 Oct — last recorded day") and can step to the previous or next recorded day.
  - One line per work item (ticket grouping): title, ticket links, recorded status as of the end of that day, and that day's latest summary.
  - Other observations from that day are collapsed and can be expanded.
  - An observation with neither a ticket nor a change gets its own line, titled with its summary, instead of one nameless line per repository.
  - Order: done, then in progress, then started.
- **Today** is derived from the Ongoing board (`add-ongoing-board`) and follows the user's priority order:
  - In-progress cards, except quiet ones (no observation in the last 14 days).
  - Plus cards with an observation recorded today, from any column.
  - Proposed changes are not listed; the view can show how many there are.
  - A card with observations today shows today's recorded status and latest summary, in the same line shape as Yesterday ("yesterday I did X with result Y, today I'm working on Z"). A card with nothing recorded today shows its title only. The user's stand-up is with US clients near the end of their own workday, so Today is usually half the report. An item worked on both days appears in both sections; for a personal recap of ongoing work that overlap is wanted.
- **Blockers** is always empty, with a prompt for the user to fill in. It is never inferred.
- Add "Copy for daily", which copies the three sections as concise Markdown or plain text, with ticket links optional. The choice is remembered.
- Uses the shared repository filter and the History timezone. No separate client or scope configuration.
- No language-model summarization. The view presents recorded facts and summaries as written. Agent-written day digests are a separate follow-up (`add-day-digests`) that improves these lines without changing this view's structure.

## Capabilities

### New Capabilities

- `daily-preparation`: Fast, factual last-working-day and today digest of recorded work for stand-up preparation, with copyable text.

### Modified Capabilities

None.

## Impact

- Server: `POST /api/daily`, which reuses `TimelineService.Project` for the selected day and for the current day so far, splits unlinked observations into separate lines, and adds a read-only lookup of the previous and next recorded days in `DashboardJournal`. It builds the Yesterday digest and the per-item lines recorded today; it does not decide which items are listed under Today.
- Client: `/daily` route and navigation entry, the digest rendering, Today derived from the board model (`buildBoard` and `byPriority` from `dashboard/src/board.ts`) over the inputs Ongoing already loads, with today's lines attached to matching cards, copy formatting, and preferences (`daily.format`, `daily.links`).
- Ticket links come from `dashboard/src/links.ts` (`add-ticket-and-repository-links`).
- The quality of each line depends on the summaries agents log. `improve-observation-summaries` is a complementary optional change.
- No journal schema or MCP contract changes.
- Prerequisites: `add-agent-workflow`, `fix-dashboard-reliability`, `add-ticket-and-repository-links`, `add-ongoing-board`.
