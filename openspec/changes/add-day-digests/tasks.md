# Tasks

Apply through the orchestrator workflow (`add-agent-workflow`). Integration and MCP changes: every check that touches `log_work`, `save_digest`, or the managed instructions states the Claude Code and Codex cases (AGENTS.md parity).

## 1. Digest store

- [ ] 1.1 Add `DigestStore` over `digests.db` with its own schema version 1, WAL and busy timeout, a named mutex around creation and upgrade, upsert by item and day, and reads by item set (design Decision 1); verify with .NET tests for create on first use, replace on second save, two concurrent writers, a missing store read as empty, a newer store version reported as unavailable, and that `journal.db`'s `user_version` stays 2

## 2. MCP contract

- [ ] 2.1 Add `DigestRequest` to `LogResult` and compute it in `JournalTools.LogWork` after a created entry (Decision 2); verify with .NET tests for the second observation of a day producing a request with both entries oldest first, a first observation and a retry replay producing none, 31 observations producing none, a machine-timezone midnight boundary, and a throwing digest store leaving the logging result intact
- [ ] 2.2 Add the `save_digest` tool with text normalization, ID validation, identity and date checks, the stale check, and upsert (Decision 3); verify with .NET tests for a saved digest, a stale set after a later observation, mixed identities, mixed dates, an unknown ID, empty and over-long text, line breaks normalized, a repeated identical save leaving one digest, and an unavailable store failing with an explanation
- [ ] 2.3 Verify the tool listing exposes `save_digest` with its annotations and input schema, and that `log_work` and `query_work` contracts are otherwise unchanged, with an MCP contract test over the in-process server

## 3. Managed instructions

- [ ] 3.1 Add the digest paragraph to `AgentInstructions.Render` (Decision 5, spec "Day digest instructions"); verify with .NET tests that the Claude Code and Codex owned blocks are identical and contain it, that disabled managed instructions omit it, and with `worklog init --dry-run` on a disposable repository showing it for both clients

## 4. Daily view

- [ ] 4.1 Return `digests` from `POST /api/daily` through a read-only `DigestStore` (Decision 4); verify with .NET tests for digests of both the selected day and today, an unrelated item's digest left out, and a missing store giving an empty list
- [ ] 4.2 Match digests to Yesterday and Today lines by exact covered-set equality, show the text with an agent-digest mark, and use it in Copy for daily (Decision 4); verify with Vitest and Angular tests for a matching Yesterday line, a stale digest after a later entry falling back to the summary, an assembled change-card line matching, unchanged status and collapsed observations, and the exact copied text in both formats
- [ ] 4.3 Extend the e2e fixture with a stored digest and add Playwright scenarios: the digest shows on its line and is copied; logging a further observation for that item makes the line fall back to the new summary after refresh. Verify `npm run test:e2e --prefix dashboard` against a packaged build with isolated data

## 5. Acceptance and docs

- [ ] 5.1 Add an isolated stdio MCP check to `tools/WorkLog.Smoke` (default mode, disposable data directory): two `log_work` calls produce a `digestRequest`, `save_digest` succeeds, a third log makes a repeated save stale, and an older-schema-compatible `journal.db` still reports `user_version` 2. Record results in `docs/mcp-validation.md`; record live Claude Code and Codex sessions as not performed (to be checked by the user after release)
- [ ] 5.2 Document digests (what triggers a request, the separate store, matching rules, and the timezone caveat) in `README.md` and `docs/architecture.md`; run the full validation set and record results in `docs/dashboard-validation.md` and `docs/mcp-validation.md`
