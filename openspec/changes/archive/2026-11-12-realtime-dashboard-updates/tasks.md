# Tasks

Implementation order is baseline/harness -> detection/revisions -> authenticated delivery -> targeted client refresh -> connection recovery -> explicit time invalidation -> integrated acceptance. All boxes remain unchecked until behavior and the named evidence pass. Follow the repository's delegated workflow: the orchestrator owns assignments/findings/task state, the developer owns implementation, and independent QA plus code analysis review stable targets. The controlling behavior is in the two delta specs; the design defines bounded coordination and acceptance workloads.

## 1. Reproducible baseline before runtime changes

- [x] 1.1 Add an isolated C06 acceptance/measurement harness using disposable data, owned non-default ports and deterministic faults; verify fixture metadata, separate-writer controls, bounded requests and cleanup leave the parent fixture and real journal/dashboard untouched.
- [x] 1.2 Preserve a baseline Release executable/package and record five-minute idle traces with and without an open reader, plus document/journal/digest write-to-visible samples and server-work counters; verify source/build identity, fixed representative fixture sizes, three warm-ups and at least thirty measured samples per freshness workload, p50/p95/variability and sanitized raw before-results exist before product edits.
- [x] 1.3 Capture baseline normal/packaged behavior and a compatible independent writer reference for the future fault matrix; verify the baseline package runs with isolated data, the current MCP contract and no changes to source version or the user's installation.

## 2. Host detection and authoritative revisions

- [x] 2.1 Introduce a host-lifetime detector with injectable clock/change-source seams and deduplicated canonical watch roots; verify document edit/atomic replacement, task changes, review edits, archive rename, missing-root recovery, root rebinding, link containment and complete watcher disposal in isolated tests.
- [x] 2.2 Detect independent journal and digest file/WAL changes using read-only state and file-lifecycle/availability generations; verify observation writes, digest-only saves, same-marker replacement, absent/locked/recovered stores and read-induced SQLite noise without creating, migrating or repairing storage.
- [x] 2.3 Separate catalog snapshot invalidation from expensive Git/OpenSpec command invalidation and publish repository/source/notices changes independently of document revision; verify normal Markdown edits avoid blanket forced discovery, last-valid configuration warnings persist/recover, sources run in bounded parallelism, and one unavailable source does not block unrelated readable state.
- [x] 2.4 Add per-instance monotonic domain revisions, bounded dirty batches, revision checkpoints and five-second server reconciliation; verify 50ms debounce/250ms maximum flush, the 128-hint overflow promotion, lost watcher hints, duplicate/out-of-order events, slow-consumer bounds, failed-reconcile pending state and cancellation with deterministic tests.

## 3. Authenticated bundled delivery

- [x] 3.1 Register the SignalR endpoint under the existing protected API boundary and pin/bundle a compatible browser client with licenses; verify same-origin notification delivery through WebSockets and SSE fallback, with Long Polling disabled and HTTP data/mutation contracts unchanged.
- [x] 3.2 Preserve Host, Origin, fetch-metadata, cookie and source-containment checks for negotiation, upgrades, SSE and any channel methods; verify actual unauthorized/cross-origin transport attempts and invalid targets are rejected without secrets, content, absolute paths or arbitrary watch roots, and CSP permits only the required local transport connections.
- [x] 3.3 Add pre-send authorization validation and at-most-two-second idle revocation checks with host-owned connection disposal; verify removing/replacing the secret on an established connection prevents subsequent private notifications, terminates delivery, refuses protected reconnects and leaves no retained connection registry entries.
- [x] 3.4 Produce an early self-contained validation package after endpoint/client integration; verify the embedded client, license notices and transport negotiation operate offline without Node or an installed .NET runtime, with no new files outside the existing package allowlist.

## 4. Targeted browser reconciliation

- [x] 4.1 Add a bounded update coordinator with desired/applied domain revisions and current instance/selection epochs; verify initial handler-registration races, new-instance reset, stale completions, one in-flight background read per resource, one pending follow-up and retained dirty work after failures or deferred saving/navigation.
- [x] 4.2 Route document/catalog, journal, digest and shared-read changes to their affected visible/derived resources; verify inactive-view refresh on activation, scoped search/notices, review-count/read-summary consistency, digest-only Daily updates, activity detail refresh and History pagination that never combines snapshots.
- [x] 4.3 Preserve source-qualified copies, nested paths, archive metadata, rendered/source mode, focus, drafts and remembered positions during refresh and recovery; verify component/browser cases for newer navigation/input during old reads, unchanged-text archive moves, removed/unreadable/oversized documents and missing-target recovery.
- [x] 4.4 Integrate own comment and read writes with coordinator invalidation while retaining authoritative mutation responses; verify overlapping watcher/own-write events, comment-save conflicts, second-browser read changes, own-comment baseline rules and no preference-write feedback loop or repeated unchanged-text render.
- [x] 4.5 Remove the two-second catalog/document polling owner after the new coordinator passes focused cases; verify startup retry and component teardown do not leave dual refresh owners, duplicate subscriptions, timers, connections or post-destruction reads.

## 5. Connection health, bounded retries and recovery

- [x] 5.1 Present Live, Reconnecting and Updates paused with last successful reconciliation and keyboard-accessible Retry; verify both themes and 390/768/1440px layouts, readable initial snapshots when connection startup fails, and a connected socket with a failed refresh never falsely reports Live.
- [x] 5.2 Coordinate initial start, reconnect callbacks, manual Retry and visibility recovery under one bounded lifecycle; verify at most five automatic attempts per outage, delays capped at fifteen seconds with jitter, cancellation-aware attempt deadlines, exhausted-retry pause and repeated Retry without duplicate work.
- [x] 5.3 Classify confirmed authorization loss separately from network/server failures, retaining draft/readable state; verify automatic protected reads/retries stop on 401, any classification probe is bounded, and explicit fixture reauthorization resumes reconciliation without assuming a failed handshake granted access.
- [x] 5.4 Reconcile current visible state after restart, reconnect, revision gaps, lost invalidations and visibility resume; verify disconnected file/journal/digest/archive changes converge, unchanged documents avoid full retransmission, failed refreshes retain pending revisions, and successful representative recoveries finish within five seconds of connection re-establishment.

## 6. Time-driven freshness without data polling

- [x] 6.1 Add reactive time invalidation with next-boundary scheduling, at-most-one-minute visible safety checks and lifecycle cleanup; verify seven-day board expiry, strictly-more-than-fourteen-day quiet state and dependent Daily Today updates occur without file/journal events or catalog reads.
- [x] 6.2 Reevaluate timezone-local History/Daily boundaries on midnight, timezone changes and resume; verify fake-clock midnight/DST cases, explicit custom dates, focused-day semantics and long suspension preserve user choices without fabricating observations or rewriting preferences.

## 7. Integrated acceptance and before/after evidence

- [x] 7.1 Run the connected idle check on the final target, with and without an open reader, and deliver raw results plus a sanitized summary; verify five connected idle minutes have zero periodic catalog/document REST reads or repeated search/timeline queries. Freshness p95 and server-work capture are deferred to `realtime-dashboard-performance-evidence`.
- [x] 7.2 Execute one clean bounded five-category fault/reconnect smoke run (one cycle per design category) with external writes and simultaneous browsers, under a bounded overall timeout with captured host logs; verify zero incorrect final states, lost saved observations, discarded drafts, accumulated notices, leaked owned processes or unbounded queues, and record per-cycle recovery latency truthfully. The 100-cycle matrix is deferred to `realtime-dashboard-performance-evidence`.
- [x] 7.3 Verify shared cross-process compatibility with current and preserved baseline writer executables and equivalent Claude Code/Codex protocol fixtures; verify both integrations' unchanged logging/query/digest contracts and observations remain intact, distinguish fixtures from native-client evidence, and do not invoke authenticated live-agent sessions.
- [x] 7.4 Run locked restore, Release build, full .NET and dashboard unit suites, full normal and fresh actual self-contained packaged no-Node/offline browser acceptance, and applicable packaged MCP/bootstrap smoke; verify source access, task authority, review conflicts, board colors/order, themes, Daily/history snapshots, missing-store fallback and cleanup remain passing without exclusions.
- [x] 7.5 Update architecture and validation guidance with final transport/reconciliation behavior, safe reproducible commands, actual idle/recovery results, known limitations and the deferred follow-up; verify allocated synthetic screenshots/evidence and all committable artifacts contain no secrets, personal data or fixture residue, and preserve prior reports.

## 8. Independent review and completion

- [x] 8.1 Obtain current independent QA and code-analyzer reports on the frozen implementation/evidence target with matching before/after identities; verify every delta scenario and applicable privacy/parity/idle-work check has evidence, all findings are triaged, and no required acceptance or unapproved deferral remains unresolved.
- [x] 8.2 Reconcile task state and findings with actual evidence, run strict OpenSpec validation, and record the scoped implementation commit and remaining optional follow-ups; verify a missed recovery target is escalated rather than weakened, and completion does not imply version changes, installation, publication, push, merge or archive.

## 9. User-testing fixes

- [x] 9.1 ORCH-023: load initial visible data even when the first live revision has not arrived, so the Ongoing board does not stay empty on first launch; verify with a test that fails before the fix and passes after, plus the affected e2e tests.
- [x] 9.2 Present Live through the header indicator (tooltip, accessible name, both themes, 390/768/1440px) and show the page-level bar only for Updates paused or for Reconnecting beyond the ~2 s grace period; verify with failing-first tests for no bar while Live, no flash under the grace period, and the bar after it or when paused. Also present the initial connection and hidden-tab deferral (DEV-044, DEV-045) only as a neutral header connecting state, never as Updates paused or a bar, with failing-first tests.
- [x] 9.3 Run the dashboard unit suite and full e2e suite; update `docs/dashboard-validation.md` to mark ORCH-021 resolved and record ORCH-023.
- [x] 9.4 Obtain the milestone QA and code-analyzer reviews, reconcile the findings ledger, commit after user approval, and install a new dev build for user testing.
