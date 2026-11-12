# Proposal

## Why

The dashboard currently discovers changes through a two-second browser poll, which adds latency and repeatedly requests catalog and open-document state even when nothing changes. C06 is the next roadmap package: provide timely updates from agents and external editors with visible connection health and reliable recovery while preserving the completed reader behavior.

## What Changes

- Replace periodic browser catalog/document polling with authenticated local change notifications and targeted authoritative HTTP reads. Detect separate-process journal and digest writes, Markdown edits, review/read-state changes, source configuration changes, and archive moves.
- Coalesce bursts, retain revisions across in-flight refreshes, and reconcile after notification gaps, watcher failures, visibility resume, reconnects, and dashboard restarts.
- Show accessible Live, Reconnecting, and Updates paused states, the last successful refresh, and a single-flight Retry action. Retain readable snapshots and drafts through failures; stop automatic connection attempts when authorization is lost.
- Explicitly update seven-day board expiry, fourteen-day quiet indicators, and timezone-dependent History/Daily boundaries as time passes, without relying on file events or polling.
- Establish reproducible evidence: zero periodic catalog/document REST reads in five idle connected minutes, with and without an open reader; reconciliation within five seconds of re-established connections; one clean bounded five-category fault/reconnect smoke run with correct final state.
- Deferred to follow-up change `realtime-dashboard-performance-evidence`: freshness latency percentiles (p95 at most one second per workload), separate server-work capture, and the full 100-cycle fault matrix.

## Capabilities

### New Capabilities

None. Live updates extend the existing dashboard and document browser.

### Modified Capabilities

- `work-dashboard`: event-driven freshness, bounded idle work, authenticated live delivery, connection health and recovery, and explicit time-driven view invalidation.
- `openspec-browser`: notification-driven document/search/review refresh and archive recovery while retaining source selection, navigation, positions, drafts, and conflict protection.

## Impact

- Server: `DashboardHost`, `DashboardAuthorization`, `SourceCatalog`, document/read-baseline services, and a host-owned detector/notification coordinator. Journal and digest access remain read-only from detection; existing storage formats, MCP schemas, and independent writer processes remain compatible.
- Browser: replace `App.poll()` orchestration with a bounded update coordinator and connection status presentation, reusing existing APIs, request epochs, reader state, and view models. Bundle a pinned `@microsoft/signalr` dependency with the existing offline assets; the host already references `Microsoft.AspNetCore.App`.
- Acceptance: isolated server/component/browser tests, deterministic fault and clock fixtures, scoped synthetic performance evidence, and normal Release plus actual self-contained no-Node package checks. Claude Code and Codex use the same unchanged writer contract; protocol fixtures are labeled separately from native-client evaluation.
- Proposed release grouping remains v0.6.0, with the roadmap's rough 5-9 engineer-day estimate. This proposal does not change versions, install or publish a release, or include C07-C16/P09 maintenance. Implementation starts only through a subsequent apply request.
