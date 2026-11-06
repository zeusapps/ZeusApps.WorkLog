# Proposal

## Why

Changes with many specs and Markdown documents are awkward to navigate in the reader's horizontal document strip. Misleading failure messages, ambiguous source-copy navigation, expensive review comparisons, and non-repeatable browser fixtures also make reviewing agent work harder than necessary.

## What Changes

- Deliver the agreed first release candidate, v0.5.0, in this order: C05 repeatable acceptance, C03 actionable failures, C02 correct copy selection, C01 vertical document navigation, then C04 responsive review comparison.
- Replace the horizontal document strip with a grouped, filterable left navigator that preserves relative paths, the current document, heading/task navigation, drafts, positions, keyboard access, and archived/source-reference reading. Provide a usable narrow-screen selector.
- Make source-copy selection unique across sources and prevent a previously selected path from affecting a later route-driven chooser.
- Distinguish document conflicts from storage/network failures, retain configuration-reload warnings until recovery, handle an expired launch token when the browser remains authorized, and expose optional digest failures without failing logging or hiding recorded work.
- Bound review-comparison work, make incomplete comparisons visibly changed in both views, and avoid rendering the entire baseline merely to extract removed comments.
- Make Daily acceptance repeatable in one disposable fixture and represent unresolved store members correctly with and without the OpenSpec CLI. Capture comparison baselines before performance implementation and retain before/after evidence.
- Preserve the repository color cues already merged into main; they require regression coverage rather than reimplementation.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `openspec-browser`: vertical document navigation, source-qualified copy selection, keyboard/context preservation, accurate read-status failures, and bounded comparison fallback.
- `work-dashboard`: persistent actionable configuration warnings and session-aware startup recovery.
- `day-digests`: bounded, nonblocking failure diagnostics and an honest unavailable state while recorded observations remain accessible.

## Impact

- Dashboard reader, document chooser, startup/error handling, Daily presentation, review comparison and parsing helpers, styles, models, and focused unit/browser coverage.
- Source-catalog configuration reload state, optional digest diagnostics, the additive private Daily response status, and test-only dashboard fixtures. Shared MCP diagnostics must behave identically for Claude Code and Codex while stdout remains protocol-only.
- Reuse the installed Angular and jsdiff dependencies. Preserve journal/digest schemas, work identities, source-containment rules, preference bounds, and MCP tool input/output contracts.
- Estimated effort: 5-10 engineer-days including focused checks and independent QA/code analysis; this is an estimate, not a measured delivery result.
- SignalR/live notifications belong to the later v0.6.0 change. Storage recovery, cache/query optimization, broad refactoring, version bumps, publication, and installation are outside this implementation scope.
