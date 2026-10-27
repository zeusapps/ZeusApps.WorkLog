# Proposal

## Why

The dashboard's Daily view, timeline drill-downs, and copied Markdown, as well as the user's agent-driven daily and weekly reports, are only as good as the summaries agents record with each observation. The managed instructions currently say only "Include a small factual summary when useful", and the `log_work` tool gives no guidance for the `summary` field. In practice summaries end up missing, narrating process ("worked on tests"), or too long to scan. Short, result-focused summaries make every downstream report faster to read and need no summarization step.

## What Changes

- Update the managed logging instructions for both Claude Code and Codex. Agents include a summary with every observation: one sentence stating the concrete outcome, decision, or remaining step, short enough to scan (about 140 characters), without process narration, secrets, or personal data.
- For `in_progress`, the summary states what was achieved and what remains when known. For `done`, it states what was completed.
- Add a description to the `log_work` tool's `summary` parameter with the same guidance, so it also reaches clients whose managed instructions are disabled.
- Summaries stay optional at the protocol level. No validation of content or length is added, and existing entries are unchanged.
- Users refresh owned instruction blocks by rerunning `worklog init` after upgrading (existing behavior).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `local-agent-integration`: The managed logging instructions require concise, result-focused summaries.

## Impact

- `src/WorkLog/Integration/AgentInstructions.cs` (rendered instruction text for both clients).
- `src/WorkLog/Journaling/LogRequest.cs` (`Summary` parameter description, visible in the MCP tool schema).
- Tests for rendered instructions and the tool schema description. Documentation in `README.md` and `docs/installation.md`.
- No journal schema change, and no change to accepted `log_work` inputs. The MCP schema gains a description string only.
- Independent of the dashboard changes. Recommended before or alongside `add-daily-prep`. Prerequisite: `add-agent-workflow`. Codex and Claude parity applies (shared instruction renderer).
