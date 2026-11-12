# Proposal

## Why

Two small defects remain from the realtime dashboard work (archived `2026-11-12-realtime-dashboard-updates`). A keyboard or screen-reader user who presses Retry updates loses focus to the page body, possibly for tens of seconds while recovery runs (ANALYSIS-705). The server can also replace a newer pending update with an older one that arrives late, which delays a refresh until the next five-second checkpoint (ANALYSIS-703). The user also decided to drop the planned performance-evidence follow-up, and the validation docs still describe it as deferred.

## What Changes

- Pressing Retry updates (page bar or reader) keeps keyboard focus in a sensible place inside the same surface while recovery runs, instead of dropping it to the page body. Focus elsewhere, such as an unsaved comment draft, is left untouched.
- Each browser connection's pending live update only moves forward. For the same server instance, a late older update merges into the pending one and never replaces newer domain revisions or change hints. A new server instance still replaces the pending update.
- Housekeeping, with no behavior change:
  - Record QA-652 (the 2-second gap without a status bar after Retry) as resolved by the presented-status work (ANALYSIS-782).
  - Update `docs/dashboard-validation.md`: the user dropped the freshness p95 measurement, server-work capture, the 100-cycle fault matrix and the remaining minor suggestions. They are not deferred, and the planned `realtime-dashboard-performance-evidence` change will not be created.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `work-dashboard`: adds requirements for keyboard focus during Retry updates and for monotonic pending live delivery.

## Impact

- Browser: the Retry handlers and focus handling in `dashboard/src/app.ts` and `dashboard/src/app.html`; unit and e2e tests.
- Server: `Slot.Offer` in `src/WorkLog/Dashboard/DashboardLiveConnections.cs`; .NET unit tests.
- Docs: `docs/dashboard-validation.md`.
- No API, MCP contract, storage, packaging or client-integration changes, so Claude Code and Codex parity is unaffected.
