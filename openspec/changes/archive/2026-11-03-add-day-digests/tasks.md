# Tasks

Apply through the orchestrator workflow (`add-agent-workflow`). Integration and MCP changes: every check that touches `log_work`, `save_digest`, or the managed instructions states the Claude Code and Codex cases (AGENTS.md parity).

## 0. Workflow policy

- [x] 0.1 Relax the live-agent rule (design Decision 6): replace the `--live-agents` prohibition in `.agents/roles/project-facts.md` (Safety) with the planned-evaluation rule, and add the planned-work note to `.agents/workflows/openspec-delegation.md` (Assignment and reservation); verify by review that the wording matches Decision 6, that the default isolated smoke run stays allowed, and that no other safety rule (real repositories, data directory, installation, client settings, non-dry-run `init`) is weakened

## 1. Digest store

- [x] 1.1 Add `DigestStore` over `digests.db` with its own schema version 1, WAL and busy timeout, a named mutex around creation and upgrade, upsert by item and day, and reads by item set (design Decision 1); verify with .NET tests for create on first use, replace on second save, two concurrent writers, a missing store read as empty, a newer store version reported as unavailable, and that `journal.db`'s `user_version` stays 2

## 2. MCP contract

- [x] 2.1 Add `DigestRequest` to `LogResult` and compute it in `JournalTools.LogWork` after a created entry (Decision 2); verify with .NET tests for the second observation of a day producing a request with both entries oldest first, a first observation and a retry replay producing none, 31 observations producing none, a machine-timezone midnight boundary, and a throwing digest store leaving the logging result intact
- [x] 2.2 Add the `save_digest` tool with text normalization, ID validation, identity and date checks, the stale check, and upsert (Decision 3); verify with .NET tests for a saved digest, a stale set after a later observation, mixed identities, mixed dates, an unknown ID, empty and over-long text, line breaks normalized, a repeated identical save leaving one digest, and an unavailable store failing with an explanation
- [x] 2.3 Verify the tool listing exposes `save_digest` with its annotations and input schema, and that `log_work` and `query_work` contracts are otherwise unchanged, with an MCP contract test over the in-process server
- [x] 2.4 Relax the `PayloadInstaller` update health probe to require `log_work` and `query_work` with output schemas and accept additional tools, and update smoke tool-count assertions (Decision 7); verify with a .NET test for a candidate listing a third tool accepted and one missing a journal tool rejected, and with `./scripts/package-update-fixtures.ps1`

## 3. Managed instructions

- [x] 3.1 Add the digest paragraph to `AgentInstructions.Render` (Decision 5, spec "Day digest instructions"); verify with .NET tests that the Claude Code and Codex owned blocks are identical and contain it, that disabled managed instructions omit it, and with `worklog init --dry-run` on a disposable repository showing managed instructions planned for both clients (the dry run does not print instruction text; the unit test proves the paragraph)

## 4. Daily view

- [x] 4.1 Return `digests` from `POST /api/daily` through a read-only `DigestStore` (Decision 4); verify with .NET tests for digests of both the selected day and today, an unrelated item's digest left out, and a missing store giving an empty list
- [x] 4.2 Match digests to Yesterday and Today lines by exact covered-set equality, show the text with an agent-digest mark, and use it in Copy for daily (Decision 4); verify with Vitest and Angular tests for a matching Yesterday line, a stale digest after a later entry falling back to the summary, an assembled change-card line matching, unchanged status and collapsed observations, and the exact copied text in both formats
- [x] 4.3 Extend the e2e fixture with a stored digest and add Playwright scenarios: the digest shows on its line and is copied; logging a further observation for that item makes the line fall back to the new summary after refresh. Verify `npm run test:e2e --prefix dashboard` against a packaged build with isolated data

## 5. Acceptance and docs

- [x] 5.1 Add an isolated stdio MCP check to `tools/WorkLog.Smoke` (default mode, disposable data directory): two `log_work` calls produce a `digestRequest`, `save_digest` succeeds, a third log makes a repeated save stale, and an older-schema-compatible `journal.db` still reports `user_version` 2. Record results in `docs/mcp-validation.md`
- [x] 5.3 Document digests (what triggers a request, the separate store, matching rules, and the timezone caveat) in `README.md` and `docs/architecture.md`, and the one-time manual installation from v0.3.0 and earlier in `docs/releases.md` and `docs/installation.md` (Decision 7); run the full validation set and record results in `docs/dashboard-validation.md` and `docs/mcp-validation.md`
- [x] 5.4 Update the `packages.lock.json` files of `src/WorkLog`, `src/WorkLog.Launcher` and `tools/WorkLog.LauncherProbe` to the `Microsoft.NET.ILLink.Tasks` version that the `global.json` SDK (`latestPatch`) now resolves, so `dotnet restore --locked-mode` (the CI restore step) passes again (user decision 2026-11-02; drift predates this change, see `reports/qa-008.md`); verify with `dotnet restore --locked-mode`, a Release build and `dotnet test`, and confirm that packaging no longer rewrites any lock file
- [x] 5.5 Fix the Ongoing board footer gap on change cards (user report 2026-11-02, pre-existing since the board was added): `.change-card` padding (23px) overrides `.board-card` padding (16px), while `.board-card .card-footer` assumes 16px, so change-card footers stop 7px short of the card edges. Give board change cards the same 16px horizontal padding as compact cards so every board card lines up; verify with a Playwright check that a board card's footer spans the card's full inner width for a change card and a ticket card, plus `npm test` and the packaged e2e run
- [x] 5.6 Prepare release v0.4.0 (user decision 2026-11-02): set `0.4.0` in `Directory.Build.props`; turn the "Upcoming release" section of `docs/releases.md` into `## Release v0.4.0`, covering every user-visible feature merged since v0.3.0 (Daily view, Ongoing board, ticket and repository links, Archive view, concise summaries, development builds, review overview, idle reliability, day digests), the one-time manual installation from v0.3.0 and earlier, and the `worklog init` rerun; verify with the release procedure's local validation (locked restore, Release build and tests, `package.ps1`, `--updates-only` on the packaged 0.4.0, `package-update-fixtures.ps1`) and, where the repository already supports it, that the public v0.3.0 declines the 0.4.0 payload without changing selection or data
