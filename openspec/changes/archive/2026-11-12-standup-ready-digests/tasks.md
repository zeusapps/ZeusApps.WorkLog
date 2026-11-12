# Tasks

## 1. Guidance text (shared by both clients)

- [x] 1.1 Rewrite the `LoggingGuidance` constants per design D4: the team-relevant content rule with the explicit OpenSpec, agent-role and process exclusions; the user's own voice; `started` wording; the structured Done / Next / Blockers digest rule (Done covers all entries, bullet counts, 120-character limit); and the saving rule that names the listed summaries as the record. Update the digest paragraph in `AgentInstructions.cs` and the `LogResult.DigestRequest` and `save_digest` descriptions. Verify with updated guidance and parity tests: both clients get identical blocks containing the new exclusions and the "never only the latest" rule, and the request instruction is complete without managed instructions.

## 2. Digest requests and saving (MCP)

- [x] 2.1 Request a digest from an item's first observation of the day (design D3), keeping the retry, more-than-30 and unlinked exclusions. Verify with tests written to fail first: first observation gives a one-entry request; retry and unlinked entries give none.
- [x] 2.2 Accept `done`, `next` and `blockers` lists in `save_digest` (1–4, 0–3 and 0–2 bullets, each at most 120 characters after trimming and replacing line breaks), plus a legacy `text` stored as a single Done bullet, with exactly one form required. Allow 1–30 `entryIds` and update `UnknownFieldValidation`. Verify with tests written to fail first for valid structured saves, a single-observation save, legacy text, too many or too long bullets, a missing Done, both forms or neither, and the existing stale and invalid cases.

## 3. Digest storage compatibility

- [x] 3.1 Add the additive `digest_parts` table with store version 1 unchanged (design D1). A structured save writes both rows in one transaction, with a derived text. Readers return the parts only when their covers match. Verify with tests: a v0.4.1-style store opens without being disabled; a structured digest gives a readable text to a text-only reader; an older-process upsert supersedes the stale parts; and repeated identical saves leave one digest.

## 4. Daily view

- [x] 4.1 Return structured parts from `DailyService` and `DayDigest`, and derive per-line Done / Next / Blockers in `daily.ts`: a legacy text becomes one Done bullet; no digest means the latest summary becomes one Done bullet. Build the Blockers section from Today's matched digests only, in Today order, with a no-blockers statement otherwise. Copy renders labelled bullets in text and Markdown, including blockers. Verify with unit tests written to fail first for each spec scenario in "Digests on daily lines", "Today and blockers" (blocker scenarios) and "Copy for daily".
- [x] 4.2 Redesign the Daily layout per "Readable stand-up layout" and design D5: two columns with Today first on wide screens, stacked on narrow screens, compact rows, and full-width Blockers. Keep keyboard access and the existing listing and order. Verify with e2e at 390, 768 and 1440 px in both themes: the first Today item is visible without scrolling at 1440, there is no horizontal scroll at 390, and keyboard reaches each row's collapsed observations and the day navigation.

## 5. Documentation and verification

- [x] 5.1 Update `docs/releases.md` (a v0.5.0 section covering all of v0.5.0's user-visible features, including this change: rerun `worklog init` and reconnect clients; unchanged contracts) and the architecture and validation notes describing digests. Run the full .NET suite, the dashboard unit suite and the full e2e suite. Run the committable-content hygiene scan and `openspec validate standup-ready-digests --strict`.
- [x] 5.2 Obtain milestone reviews from QA and the code analyzer on the stable target, including Claude Code and Codex parity for the instructions and the MCP contract. Reconcile the findings ledger and commit after user approval. A dev build is installed only if the user asks.
