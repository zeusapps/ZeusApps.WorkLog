# Tasks

Apply through the orchestrator workflow (`add-agent-workflow`). This is a small change; design.md is intentionally omitted.

Implementation notes:
- The rendered text lives in `AgentInstructions.Render` (`src/WorkLog/Integration/AgentInstructions.cs`). It is shared by both clients, which preserves parity by construction.
- Replace the sentence "Include a small factual summary when useful." with guidance along these lines:

  > Include a summary with every observation: one short sentence (about 140 characters) stating the concrete outcome, decision, or remaining step — for in_progress what was achieved and what remains, for done what was completed. Do not narrate process, repeat the status, or include secrets or personal data.

  Keep the rest of the block unchanged.
- `LogRequest.Summary` (`src/WorkLog/Journaling/LogRequest.cs`) currently has no `[Description]`. Add one with the same guidance in a single sentence.

## 1. Instructions and schema

- [x] 1.1 Update the summary guidance in `AgentInstructions.Render`; verify with a .NET test that the rendered blocks for `claude` and `codex` contain the identical summary sentence and that the rest of the block text is unchanged apart from that sentence
- [x] 1.2 Add the `[Description]` to `LogRequest.Summary`; verify with a .NET test (or the MCP smoke check in `tools/WorkLog.Smoke`) that the `log_work` tool schema exposes the description and still accepts an observation without a summary
- [x] 1.3 Verify `worklog init --dry-run` on a disposable repository with isolated `--data-dir` reports the owned-block refresh for both clients and no other changes

## 2. Docs and validation

- [x] 2.1 Describe the summary convention in `README.md` (logging section) and note in `docs/installation.md` that rerunning `worklog init` refreshes owned instructions; verify links resolve
- [x] 2.2 Run `dotnet test -c Release` and the packaged MCP smoke check; record the results in `docs/mcp-validation.md`
