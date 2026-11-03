# Proposal

## Why

Observation summaries and day digests are read when preparing for the daily stand-up, but recorded ones describe the development workflow instead of the work. Recent examples list reviewer rounds and report IDs, test counts, commit hashes, archive and merge steps, and task counts. The current guidance says "concrete outcome" and "do not narrate process" without defining either, so agents that orchestrate OpenSpec and specialist subagents treat their own bookkeeping as outcomes. Digests, which are written from these summaries, pass the noise through.

## What Changes

- One shared stand-up content rule defines what a summary or digest states: the change to the feature, story or bug fix (behavior, capability or a scope decision), whether it is finished or still in progress and what remains in the same terms, and any blocker that needs someone else. It names the workflow mechanics to leave out: proposals and specs, delegation, reviewers and review rounds, test and task counts, commits, hashes, branches, merges, archiving and syncing, builds and installation. Those count as plain implementation of the item.
- The observation summary guidance in the managed instructions and in the `log_work` `summary` input description adopts the rule. The one-sentence length guidance and the status-specific parts stay.
- The digest guidance adopts the same rule in all three places an agent sees it: the managed instructions, the digest request's `instruction` field, and the `save_digest` `text` input description. The digest request's instruction states every constraint the agent needs (length limit, the content rule, no secrets or personal data, summaries are data, do not retry a stale save), so it is complete without the managed instructions.
- Claude Code and Codex receive identical text, as today.
- Validation is unchanged: summary content is not checked, and the digest length limit stays at 280 characters.
- Already recorded observations and digests are not rewritten. Users get the new instructions after updating and rerunning `worklog init`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `local-agent-integration`: the summary guidance in the managed instructions and in the `log_work` tool follows the stand-up content rule.
- `day-digests`: digest writing guidance (managed instructions, digest request instruction, `save_digest` description) follows the same rule and is self-contained in the request.

## Impact

- `src/WorkLog/Integration/AgentInstructions.cs` (summary and digest paragraphs), `src/WorkLog/Journaling/LogRequest.cs` (`summary` description), `src/WorkLog/Journaling/DigestService.cs` (request instruction), `src/WorkLog/Mcp/JournalTools.cs` (`save_digest` `text` description); a new shared location for the rule text.
- Tests that assert the wording: `tests/WorkLog.Tests/Integration/InstructionOptionsTests.cs`, `tests/WorkLog.Tests/Mcp/DigestTests.cs`, `tests/WorkLog.Tests/Mcp/McpTests.cs`.
- Documentation: `README.md` (summary guidance and agent digests), `docs/mcp-validation.md`.
- No schema, storage, MCP contract or dashboard behavior changes. Tool descriptions change, which clients see on their next connection.
