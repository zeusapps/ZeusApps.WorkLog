# Analysis report: improve-dashboard-reading-and-reliability / backend round 1

## Review target

- Reviewer / role: `reading_backend_analysis` / code-analyzer.
- Client / version / mode / model: Codex / version unavailable to this subagent / delegated Default mode / `gpt-6.1-sol`, high effort, role default.
- Date: 2026-11-05 UTC.
- Change root / assignment: `openspec/changes/improve-dashboard-reading-and-reliability` / ANALYSIS-REVIEW-001.
- Task references: 2.2, 2.4, and the server side of 2.5 in [tasks](../tasks.md); persistent configuration failures in [work-dashboard](../specs/work-dashboard/spec.md); nonblocking digest failures in [day-digests](../specs/day-digests/spec.md).
- Scope: `src/WorkLog/Dashboard`, `src/WorkLog/Journaling`, `src/WorkLog/Mcp`, `src/WorkLog/Persistence`, `tests/WorkLog.Tests/Dashboard`, and `tests/WorkLog.Tests/Mcp`.
- Reading limits: frontend implementation, user data/configuration, live services, installation, and native-agent execution were excluded. No prohibited paths or services were accessed.
- Additional paths read: role/project-facts/report-template instructions; this change's proposal, design, tasks and the two spec deltas above; `docs/architecture.md`; `src/WorkLog/Configuration/LocalSettings.cs`; `src/WorkLog/Cli/ServeCommand.cs`; and registration portions of `src/WorkLog/Integration/Clients/ClaudeIntegration.cs` and `CodexIntegration.cs`.
- Baseline and HEAD: `ac6d7f075a4a52e5eeef5896d034ae3c35a504fc`.
- Prior reports / rechecked findings: none. ORCH-002 and ORCH-003 remain owned by the orchestrator and independent QA.

The same scoped review-identity command ran before and after the analysis:

```text
HEAD: ac6d7f075a4a52e5eeef5896d034ae3c35a504fc
Baseline: ac6d7f075a4a52e5eeef5896d034ae3c35a504fc
Paths: src/WorkLog/Dashboard src/WorkLog/Journaling src/WorkLog/Mcp src/WorkLog/Persistence tests/WorkLog.Tests/Dashboard tests/WorkLog.Tests/Mcp
Manifest:
  M src/WorkLog/Dashboard/DailyService.cs
  M src/WorkLog/Dashboard/SourceCatalog.cs
  M src/WorkLog/Journaling/DigestService.cs
  M src/WorkLog/Mcp/McpServerHost.cs
  M src/WorkLog/Persistence/DigestStore.cs
  M tests/WorkLog.Tests/Dashboard/DailyDigestResponseTests.cs
  M tests/WorkLog.Tests/Dashboard/SourceCatalogTests.cs
  M tests/WorkLog.Tests/Mcp/DigestContractTests.cs
  M tests/WorkLog.Tests/Mcp/DigestTests.cs
Tracked diff SHA-256: f736d5e40c9feb140b6be6e356a3d259f1732fe61cc755bc4f4837e19f9afcc7
Untracked files: none
Summary: head=ac6d7f0 baseline=ac6d7f0 files=9 diff=f736d5e40c9f untracked=0/e3b0c44298fc
```

Before and after identities match. No unexpected implementation mutation was observed.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Configuration state and complexity | Inspected `SourceCatalog.DiscoverAsync` and the configuration fixture | The last successful settings object remains assigned when construction of a replacement throws; one issue string survives refreshes; a failed reload is retried even if the file stamp returns to its last successful value. The success path updates the stamp and clears the issue together. | Passed by source inspection. No extra state machine or accumulating notice collection was introduced. |
| Optional diagnostic isolation and bounds | Inspected `DigestService.Request`, its diagnostic helper, and unit cases | Three fixed categories determine dictionary keys. The lock covers rate bookkeeping, while the callback runs outside that lock and callback exceptions are caught. Messages contain a fixed category, without exception text or observation data. | Passed by source inspection. The clock and sink are supplied once per service; no timer or background retry loop was added. |
| Persistence boundary and recovery | Inspected `DigestStore.Read`, `DailyService.Digests`, and response tests | Empty item sets bypass storage. Missing path components remain normal absence; an existing directory or other read failure propagates to the Daily boundary, which supplies a fixed private issue while preserving items. Each request computes a fresh issue, allowing a later successful read to clear it. | Passed by source inspection. Read-only connections, store version, migrations, and journal data are unchanged. |
| API and responsibility boundaries | Compared changes with `docs/architecture.md` and unchanged tool records | Digest diagnostics remain in the shared server composition; private Daily status is additive and nullable. No MCP input/output record or tool registration schema was changed. Optional constructor arguments follow the prior positional arguments. | Passed for this backend subset. Final documentation updates and full-feature completeness were explicitly outside this review. |
| Claude Code and Codex parity | Inspected both registration adapters, `ServeCommand`, and `McpServerHost` | Both adapters use the supplied shared server command/arguments. `serve` enters the same host, which routes console logging to stderr and supplies the same digest sink. The two new protocol labels exercise that shared path. | Passed by source inspection of shared composition. These are protocol fixtures, not native client observations. |
| Test synchronization | Inspected the new stderr capture and assertions | The stderr queue is sampled immediately after stdout protocol responses, without an explicit stderr-delivery or EOF barrier. | Suggestion ANALYSIS-001 below. This review did not observe or measure a flaky execution. |
| Whitespace | `git diff --check ac6d7f0 -- src/WorkLog/Dashboard src/WorkLog/Journaling src/WorkLog/Mcp src/WorkLog/Persistence tests/WorkLog.Tests/Dashboard tests/WorkLog.Tests/Mcp` | Exit 0; only a line-ending conversion advisory was emitted. | Passed. No formatting mutation was run. |
| Functional execution and performance measurement | No additional test or benchmark run by this analyzer | Independent QA owns functional execution. The orchestrator's reported suite results were not relabeled as analyzer-executed evidence. | Not performed to avoid duplicate execution and contention with the concurrent comparison benchmark. Conclusions above are source observations. |
| Report hygiene | Inspected this report and the changed diagnostic/status strings | References are repository-relative; no raw protocol transcript, launch material, private settings or machine identifiers are included. | Passed for this report. Whole-change hygiene remains part of final QA. |

## Steady-state cost and bounds

The following are source observations and nominal bounds, not measured workload rates.

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Catalog discovery, optional diagnostics | Source observation | `GetAsync` retains its two-second cache and serial gate. A discovery reads the journal snapshot, config metadata, source/document metadata, and the digest revision; repository-name decoding is reused while the journal snapshot is unchanged. A failed config adds one JSON reload attempt per discovery and one warning. Diagnostics have no idle timer and do no work without a failed eligible request. A two-second caller produces a nominal 30 requests/minute, but discovery frequency can be lower because freshness is measured after completion; no live counts were collected. |
| Cache lifetime versus interval and refresh duration | `SourceCatalog` command cache | Source observation | Successful expensive Git/OpenSpec calls retain 60-second entries; timeouts retain 30-second entries. Thus ordinary refreshes can reuse CLI results across many polling intervals. Changed stamps and explicit force requests invalidate or bypass that relief. Existing command stages remain sequential, with at most four calls in parallel within a stage. |
| Unbounded state | Diagnostic suppression, config issue, existing catalog/store state | Source observation | The new suppression dictionary has at most three fixed keys; the config state holds one issue string and one last-successful settings object. Daily status is one value per response. Existing command-cache keys and persistent digest rows can grow with sources/history; this patch adds no new growth path or retention policy. Their planned cache/query work is separate scope. |
| Repeated full reads | Daily and digest request projections | Source observation | Existing Daily queries still decode the selected journal history up to the current-day boundary, and digest requests still read the item's day from the journal. Digest retrieval reads matching items' stored days. The new read-boundary check replaces a file-existence check with attributes and does not add another journal pass. Existing polling/query optimization candidates were not reopened as change-local blockers. |
| External tool discovery | `LocalCommand` called by catalog discovery | Source observation; environment probing not performed | Discovery remains unchanged: Git and Node use PATH, while Windows OpenSpec lookup searches PATH entries for the package's Node entry point. Calls have a 15-second timeout and terminate the process they own on cancellation/timeout. Alternate installation layouts were not probed in this analysis; no new executable lookup was added. |

## Findings

No production correctness, architecture, or performance blocker was identified in the assigned backend subset. One test-maintenance suggestion follows.

### ANALYSIS-001: Synchronize stderr completion before asserting diagnostic counts

- Severity: suggestion.
- Related task: 2.4, shared-server diagnostic evidence.
- Locations: `tests/WorkLog.Tests/Mcp/DigestContractTests.cs:126` and `:144` at the reviewed identity.
- Observation: the callback enqueues stderr lines independently of the stdout protocol reader. Awaiting the third `CallToolAsync` response does not establish that the stderr callback has processed the corresponding diagnostic. The immediate queue snapshot can therefore see an early incomplete view of the output. The same issue can also make a late duplicate invisible to the count assertion.
- Proposed improvement and benefit: use a bounded delivery/completion barrier before checking the complete stderr result. The existing managed-process/EOF-drain pattern in `McpTests.ServerStopsAfterClientClosesStandardInput` is a local reference. This would make the diagnostic evidence independent of scheduler and pipe-reader timing while retaining the assertion that logging and protocol output succeed.
- Behavior-preservation risks: keep the child-server lifetime bounded and retain both labeled shared-server cases, saved-row assertions, sanitization checks, and protocol parsing; do not replace these with a fixed sleep or a native-agent probe.
- Evidence type: source-observed synchronization gap. No flaky test failure or failure rate was observed or measured by this analyzer.
- Verification plan: deliberately delay stderr delivery in a disposable fixture, await the bounded completion/drain, and confirm the final output contains exactly one safe diagnostic after repeated failures. Re-run both protocol labels and the existing server shutdown test.
- Relationship: new suggestion; no canonical duplicate identified in this review.

## Re-verification

Not applicable: this is the initial backend analysis, not a finding recheck. ORCH-002 and ORCH-003 await the assigned QA assessment and orchestrator triage.

## Assessment and limitations

The backend changes preserve the shared MCP composition and storage boundaries while keeping their new state bounded. ANALYSIS-001 concerns test synchronization rather than an observed production failure; the orchestrator decides its disposition.

Frontend C04, final user/validation documentation, packaged browser acceptance, full-feature completeness, and native-client execution are outside this report. Final analysis should carry this coverage only after confirming that the backend identity above remains unchanged. No implementation, settings, data, process, or ledger was modified; the only authored file is this reserved report.
