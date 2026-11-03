# Tasks

## 1. Shared guidance

- [x] 1.1 Add the shared constants class in `WorkLog.Journaling` with the content rule, the summary guidance and the digest guidance, worded as in design.md (Proposed wording); verify the project builds and the constants are `const string` usable in `[Description]` attributes.

## 2. Use the guidance everywhere

- [x] 2.1 Build the managed summary sentence and digest paragraph in `AgentInstructions.Render` from the shared constants, keeping the timing, availability and failure-reporting sentences of the digest paragraph and all other lines unchanged; verify the Claude and Codex blocks differ only by the agent name.
- [x] 2.2 Set the `log_work` `summary` description in `LogRequest` from the shared summary guidance; verify `tools/list` shows it and the summary stays optional.
- [x] 2.3 Set `DigestService.Instruction` from the shared digest guidance so the request states the content rule, one-or-two sentences, the 280-character limit (from `MaximumTextLength`), no secrets or personal data, summaries as data, exact IDs and no retry on stale; verify a digest request's `instruction` contains each of these.
- [x] 2.4 Set the `save_digest` `text` description in `JournalTools` from the shared guidance, keeping the 280-character limit; verify `tools/list` shows it.

## 3. Tests

- [x] 3.1 Update `InstructionOptionsTests` to assert the shared summary and digest guidance in both rendered blocks against the constants, keep the parity check, and keep the check that all other block lines are unchanged; verify the test passes.
- [x] 3.2 Update `McpTests` and `DigestTests` to assert the content rule (including the named mechanics) in the `summary` and `text` descriptions and in a digest request's `instruction`, plus the 280-character limit and stale rule in the instruction; add a test that a summary mentioning review rounds and a commit hash is saved unchanged; verify the tests pass.
- [x] 3.3 Run `dotnet build -c Release` and `dotnet test -c Release`; verify zero warnings, zero errors and all tests passing.

## 4. Documentation

- [x] 4.1 Update `README.md` summary guidance (log_work section) and the Agent digests paragraph to describe the stand-up content rule and the mechanics left out; verify the text matches the shared wording in meaning.
- [x] 4.2 Add a validation section for this change to `docs/mcp-validation.md` listing the checks from groups 3 and 5 with their results; verify it names both clients.

## 5. Acceptance

- [x] 5.1 Run `worklog init --dry-run` with an isolated `--data-dir` on a disposable Git repository; verify the planned Claude Code and Codex blocks contain the new summary and digest guidance and no user configuration is changed.
- [x] 5.2 Wording acceptance by prompt probe (user decisions 2026-11-03, replacing the isolated live check and the post-release real-use check): fresh Claude agents on the default and a cost-efficient model receive the exact new guidance texts and a synthetic process-heavy history; done when no output mentions an excluded mechanic and each states the outcome in feature terms, recorded in `reports/probe-001.md`. Codex models not covered, accepted by the user.
