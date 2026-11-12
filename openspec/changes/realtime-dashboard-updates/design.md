# Design

## Context

See [proposal](proposal.md) for motivation and the two [dashboard](specs/work-dashboard/spec.md) and [reader](specs/openspec-browser/spec.md) deltas for behavior. Planning baseline is `0d77685`; this document records source observations and proposed decisions, not implemented or measured results.

- `dashboard/src/app.ts` starts a two-second interval after initialization. `poll()` reads `catalog`, replaces its signal, optionally refreshes reviews/read hints and search, reloads the selected document with `knownRevision`, and reloads activity/History/Ongoing/Daily when journal or digest markers move. It skips the whole pass during document loading or comment saving. Request epochs already protect explicit document selection, queries, and drafts from late responses.
- `SourceCatalog.GetAsync()` serializes discovery behind a gate and caches the complete result for two seconds. Its Git/OpenSpec command tier caches for sixty seconds, with input stamps, bounded parallelism, and timeout backoff. The document revision covers change documents and task progress; it does not fully describe repository/source/notices changes. Calling `force: true` bypasses expensive command caching as well as the complete-snapshot cache, so it is unsuitable as a blanket reaction to every event.
- `DashboardJournal.Snapshot()` reads the append-only maximum rowid. `DigestStore.Revision()` reads the maximum saved timestamp and keeps its old value on a read failure or missing store. These remain useful content markers but are insufficient alone to detect file replacement or transitions into/out of an unavailable state.
- `DashboardHost` creates shared services once for its lifetime and serves protected `/api/` routes. Its custom middleware checks loopback Host, Origin, fetch metadata and the private cookie. `DashboardAuthorization.Valid()` also checks the current secret file. There is no ASP.NET authentication scheme or live endpoint today. CSP currently allows `connect-src 'self'` only.
- Journal/digest writers are separate processes, including MCP processes and compatible older executables. Markdown editors can atomically replace files. Read baselines live under `dashboard-reads`; preferences are a different bounded file. Detector reads must not create or migrate data.
- The app's board calculations use the current time when reactive inputs invalidate. Replacing the catalog during polling currently provides an incidental clock tick. `dayRolledOver()` is also reached through polling. Removing that poll requires explicit temporal invalidation.
- Existing host tests use disposable data and loopback port zero; browser fixtures support owned server restarts, isolated writers, and actual self-contained no-Node execution. These provide the seams for new fault acceptance without using the maintainer's running dashboard or journal.

## Goals / Non-Goals

**Goals:** Separate detection, delivery and authoritative refresh; keep the latency-critical path independent of avoidable discovery; make missed events and partial failures recoverable; retain the existing source, snapshot, review-conflict and privacy boundaries; provide reproducible evidence for C06's targets.

**Non-Goals:** Replicated editing or collaborative document merges; streaming document or observation contents; new MCP tools or managed instructions; journal/digest schema changes; general cross-tab preference synchronization; broad component extraction, search/history optimization, optional digest-store repair, or unrelated P09 cleanup; native authenticated agent evaluation; release/version/install/push operations.

## Decisions

### 1. SignalR invalidation channel, existing HTTP data APIs

Use a host-owned SignalR endpoint under `/api/live`, with the matching JavaScript client bundled and pinned through `dashboard/package-lock.json`. Keep all authoritative data and mutations on the current HTTP endpoints. Prefer WebSockets and permit SignalR Server-Sent Events as a local fallback; disable Long Polling so unavailable persistent transports produce Updates paused with Retry instead of a hidden polling loop. Negotiation remains enabled. Neither transport changes the browser's authorization boundary.

The shared framework already supplies the server implementation. Register only the services needed by the slim host and own their cancellation/disposal with the web application. Production assets must contain the browser client and its license notices, with no CDN or runtime Node requirement. Verify trimmed/self-contained behavior with the real package.

Alternatives: continuing conditional polling retains the idle browser traffic and weak connection story; a raw WebSocket requires custom lifecycle/transport machinery; a custom event-stream implementation duplicates retry and protocol plumbing. SignalR is chosen for lifecycle support, while correctness remains in the revision/reconciliation protocol rather than in transport guarantees.

Current documentation checked through Context7:

- [SignalR JavaScript client](https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/signalr/javascript-client.md): automatic reconnect does not retry initial startup, so initial start and later reconnect need explicit coordinated handling.
- [SignalR authentication](https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/signalr/authn-and-authz.md): authentication is not automatically revalidated during a connection's lifetime.
- [WebSocket security](https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/fundamentals/servers/kestrel/security-considerations.md): CORS does not protect WebSockets; Origin validation and authentication are separate checks.

These constrain the proposed design; they are not evidence that this host already supports SignalR. Use the client major compatible with the repository's .NET 10 target, pin its concrete supported patch during implementation, and verify that patch's APIs then.

### 2. Host-owned detector with bounded reconciliation

Add a small detection/coordinator boundary under `src/WorkLog/Dashboard`, injected with `TimeProvider` and filesystem/change-source seams for deterministic tests. Use watchers as fast invalidation hints, not as an authoritative event log.

| Input | Fast hint | Authoritative reconciliation and effect |
| --- | --- | --- |
| Observation writes | Journal file and SQLite WAL create/change/rename/delete | Read-only journal marker plus file lifecycle generation; refresh scoped activity, Ongoing, History and visible Daily, retaining existing snapshot rules |
| Digest saves or read-availability changes | Digest file/WAL lifecycle | Read-only digest marker plus availability/lifecycle generation; invalidate visible Daily independently of journal rowid |
| Markdown/task/review/archive changes | Approved source Markdown and directory lifecycle | Invalidate affected document/catalog inputs, review/search state and task inputs; re-resolve logical archive identity |
| Source/config/worktree discovery | Existing configuration and resolved-source changes | Refresh repository/source/notices projections, rebind allowed watchers after a successful discovery, retain last-valid settings on errors |
| Shared read state | Read-index replacement/deletion and explicit successful read mutations | Refresh read summary and selected baseline without overwriting preferences/drafts |
| Secret revocation | Authorization-file changes | Revalidate and close connections; never expose secret contents |

Deduplicate canonical allowed roots across repositories/worktrees/stores. Resolve watch roots with existing containment rules; never recursively traverse a linked path outside an approved source. Do not accept client filesystem roots. For a temporarily absent root, observe an appropriate contained existing parent or retry through bounded discovery; do not watch a whole drive/profile to discover it. Dispose obsolete watchers when configuration changes and all watchers when the host stops.

Coalesce normal hints with a 50ms debounce and a 250ms maximum flush delay. Keep one pending dirty flag per domain and at most 128 optional source/document hints per batch; overflow promotes to a domain-wide reconciliation flag instead of dropping the last update or retaining an unbounded path log. Maintain no durable event history.

Run a cancellation-aware server reconciliation pass at most five seconds apart to recover dropped watcher notifications and unavailable watches. It checks supported journal/digest identity/markers and canonical source metadata; it does not repeatedly decode observation payloads or read every document body. Reconcile content when an input is suspect, including a watcher overflow or a same-metadata edit hint. A missing/replaced DB or availability transition cannot be suppressed merely because maximum rowid/save time stayed equal. Suppress read-induced SQLite sidecar noise after checking authoritative state, avoiding a self-generated invalidation loop. Do not write or repair stores from this detector.

Keep Git/OpenSpec discovery on the existing at-most-sixty-second bounded cadence unless its actual inputs changed; keep independent sources parallel and preserve timeout/backoff. Separate complete-snapshot invalidation from expensive command invalidation. A Markdown edit must not automatically launch every Git/OpenSpec command. Background discovery publishes changed repositories, sources and notices even when the document-content revision remains equal. Publish partial/unavailable-source status when needed, keeping unrelated views usable; preserve retry state after failures rather than acknowledging an uncompleted refresh.

Alternative: in-process logging callbacks alone miss external editors, other MCP processes and older writers. Always forcing catalog discovery defeats the sub-second path and the idle-work contract. Watchers without reconciliation cannot recover overflow or root replacement.

### 3. Ephemeral instance identity and revision vector

Each host instance owns an unpredictable instance identifier and monotonically increasing domain revisions for catalog/source status, documents/reviews, journal, digests and reads. Increment a domain generation when validated state changes or when an uncertain hint requires an authoritative check. Existing journal rowids, content hashes and digest timestamps remain snapshot inputs, not a globally comparable revision across host restarts.

The shared implementation contract is `/api/live`, server event `Revision`, and no-argument hub method `Snapshot`. Both return a camelCase vector containing `instance`, numeric `catalog`, `documents`, `journal`, `digests` and `reads`, plus `dirty` domain-name hints. Checkpoint vectors are complete even when `dirty` is empty. Actual checkpoint publications additionally carry an optional monotonic numeric `checkpoint` sequence; ordinary notifications and Snapshot omit it. This is publication metadata for correlating delivered checkpoints, not a data-domain generation, and does not alter desired/applied revision ordering. Clients compare desired/applied revisions rather than depending on the hints. No client-selected source subscriptions or filesystem roots are introduced.

Messages carry the instance ID, revisions, domain flags, and optionally bounded server-derived source/document identities. They contain no document text, observation summaries, secrets or absolute machine paths. A revision-checkpoint message on the live channel at the five-second reconciliation cadence allows a connected browser to notice a deliberately lost invalidation without a REST poll. Count those messages separately from transport keep-alives in evidence. Slow connections retain the latest pending vector, not a queue of every write; disconnect an irrecoverably slow consumer and require reconciliation.

Register browser notification handlers before starting delivery, then obtain the current revision vector through the established channel and perform initial authoritative reconciliation. Merge any notifications received during that reconciliation. Keep per-domain desired and successfully applied revisions, not just a single last-received value. An older completion cannot acknowledge a newer dirty revision; a different instance invalidates prior ordering and triggers a full visible-state reconciliation. Responses must be associated with the instance, domain revision and current UI/request epoch; if a race prevents proving coverage, leave the domain dirty and refresh again.

No replay of every intermediate event is required: the journal and files are authoritative. Reconnect, revision gap, overflow and visibility resume reconcile the current catalog plus visible/derived resources, even if document text did not change but archive state or notices did. An unchanged document uses the existing `knownRevision` response only when its surrounding metadata remains valid; request full metadata after archive/permission changes.

Alternative: considering an incoming notification applied before its HTTP read succeeds loses updates on errors and overlaps. Ordering by timestamps or rowid alone cannot identify a restarted host or replaced files.

### 4. Targeted client coordinator with retained pending work

Introduce a bounded browser update coordinator instead of extending the monolithic `poll()` body. Reuse the existing API wrapper, source-qualified identities, query snapshots and navigation epochs. A domain becomes dirty immediately; at most one background read per resource/selection runs, and at most one coalesced follow-up is pending. Foreground navigation/save remains authoritative and can supersede or cancel background requests. Every deferred domain is retried after saving/navigation settles; skipping a refresh never clears it.

| Dirty domain | Refresh dependency |
| --- | --- |
| Catalog/source status | Current catalog, scoped notices and affected active/archive/source selection; run necessary document metadata reconciliation after catalog |
| Documents/reviews | Affected open document, outline/task navigation, review overview, active search and updated-since-read indicators; preserve filters and draft ownership |
| Journal | Scoped activity and Ongoing backing data; visible History, Daily and open activity details as applicable; mark hidden views stale for their next activation |
| Digests | Visible Daily summary only; retain observations and optional-store failure behavior |
| Reads | Shared read summary and affected selected baseline/comparison |

Coordinate review-count/read-summary refresh with the current catalog generation instead of racing independent fire-and-forget updates. Route each result through the latest repository/query/document epoch; hidden-view laziness must not let an older result appear when that view opens. In-flight History pagination keeps its original snapshot; a new revision schedules a consistent refresh rather than appending rows from another snapshot. Treat notification hints as optimization only: scope the actual requests using current selected repositories, ticket, dates and archive filters.

Successful local comment/read writes use the returned authoritative result immediately and request coordinator invalidation for dependent resources. Duplicate watcher/own-write events are harmless; do not refresh/re-render unchanged text, re-save preferences from an idle refresh, or count the user's own comment as a change when existing read-baseline rules exclude it. Existing deletion/conflict behavior and the 2 MiB document limit remain unchanged. Preserve focus-aware refresh behavior from the completed reader change, including newer input receiving focus while an old request finishes.

Alternative: replacing the poll with a handler that calls every loader on every event moves the same work to the event path, introduces duplicate requests, and still drops updates during saves.

### 5. Connection state includes data health

Use a single lifecycle owner and these user-facing states:

```text
initial snapshot --> connecting/reconciling --> Live
Live --> connection lost --> Reconnecting --> reconciling --> Live
initial start failure / retries exhausted / refresh failure --> Updates paused
Updates paused -- Retry --> connecting or refresh reconciliation --> Live
confirmed authorization loss --> Updates paused + relaunch guidance
```

SignalR's reconnection callbacks feed the coordinator, but never mark data current on their own. Last-successful-refresh means a successful visible-state reconciliation, not the time of a received ping. One accessible status surface replaces repeated toasts: a compact header indicator (the existing journal dot with a tooltip) while Live, and a page-level bar for Updates paused and for Reconnecting only after a ~2 s grace period, which suppresses routine-reconcile flashes (ORCH-021). The initial visible-data load must not depend on the first live revision having arrived; status and Retry work with keyboard, screen readers, light/dark themes and 390/768/1440px layouts. Do not expose transport jargon in product text.

Configure bounded recovery with at most five attempts per outage and delays capped at 15 seconds, with jitter; share one retry owner across initial startup, automatic reconnect, and manual Retry. Each attempt has a cancellation-aware deadline. Retry while already connected retries dirty data rather than opening another connection. After exhaustion, pause until explicit Retry or a visibility-resume recovery opportunity; do not let repeated visibility events create overlapping attempts. A confirmed 401 clears scheduled automatic attempts and suppresses protected background reads until explicit reauthorization. A network failure or generic 403 is not authorization success and must not be mislabeled as one. If the transport library cannot distinguish a handshake failure, use one protected probe to classify it, never an unbounded probe loop.

Keep the last readable snapshot, document, unsaved/editing review text and pending dirty revisions through failures. A fresh transport with failed data reads remains Updates paused. A missing document is a resolved unavailable state with navigation, rather than a permanently failing attempt to open that deleted file. Independent optional digest/source failures retain their existing scoped indications rather than making all journal views unusable.

### 6. Protect every transport and ongoing delivery

Apply the existing custom authorization middleware to `/api/live` negotiation, transport requests and snapshot invocation; do not add an ineffective `[Authorize]` attribute without a configured authentication scheme. Validate the exact listener Host and browser Origin for WebSocket upgrades as well as ordinary HTTP/SSE requests. Missing-origin non-browser requests still require the session and valid Host; they do not acquire browser rights by omitting Origin. Any future subscription identifiers are resolved through the current catalog, never as file paths.

Use the existing HttpOnly same-site cookie, with no query-string authorization tokens or document-route changes. Validate the CSP on actual WebSocket and SSE execution; add only the exact loopback WebSocket origin for the chosen listener if needed, preserving all other restrictions and disallowing wildcard/external connections.

Validate the current secret before every private notification/checkpoint and reconcile active connection validity at most two seconds apart so an otherwise idle revoked connection closes. The registry is host-owned and removes disconnected entries. Revocation prevents further private sends even before the next periodic disconnect check. Map revocation to connection shutdown and failed protected probes; the client then stops retrying and retains its unsaved text. Do not log handshake cookies, raw library errors with sensitive URLs, document identifiers with machine paths, or private content. Detection faults use bounded, sanitized categories and scoped UI warnings.

This preserves the current secret deletion/replacement behavior for long-lived sessions, which framework authentication alone does not guarantee. Actual unauthorized/cross-origin negotiation and upgrades, unauthorized SSE, and active-connection revocation must be tested; REST-only authorization checks are insufficient.

### 7. Explicit temporal invalidation

Give board/quiet/Daily computations a reactive clock input driven by a scheduled next relevant boundary, with a visible-tab safety check no slower than once per minute. Schedule exact seven-day card expiry and the transition strictly beyond fourteen days using the current rules, and timezone-local midnight through the existing date/boundary helpers. Advance the clock and reevaluate on timezone change, visibility resume and reconnect. Cancel or rearm timers as data/scopes change and on destruction.

Time-only expiry/quiet updates use existing data locally; local-midnight transitions trigger only the relevant History/Daily/Ongoing data refresh. Custom ranges and temporary focused History days preserve their established semantics. Avoid a new catalog poll disguised as a clock. Use fake-clock coverage for midnight, DST, custom ranges, long suspension and an otherwise idle connected board.

### 8. Baseline, fault evidence and acceptance

Before changing runtime code, preserve a baseline Release executable/package and establish a reproducible isolated C06 harness. Use the same harness and fixture sizes before/after, with source/build identity, sanitized environment metadata, raw data and summarized statistics. Keep raw transcripts and large results under ignored output; retain only sanitized summaries and allocated synthetic screenshots under this change's evidence directories. This is required acceptance work, not evidence that the planning session ran a benchmark.

The baseline host has no built-in counters for metadata, document/hash, database and command work. Capture latency/network traces against the unmodified executable, and measure server-work boundaries in a separate ignored source snapshot with deterministic, guarded instrumentation transforms reused before and after. Label method-boundary invocation counts separately from physical I/O; capture actual process CPU and GC allocation totals independently. Preserve original package/source identity and identify the instrumented copy, transforms and any measurement overhead. Instrumentation must never ship in the production target or modify the live installation.

Representative common fixture: two repositories with active and archived local changes, 50 Markdown documents per active change, repeated nested basenames, 1,000 synthetic observations, stored Daily digests and an open 50-200 KiB document. Normal-mode source tests additionally cover a worktree and a resolvable standalone store. Packaged no-Node mode covers local roots and the existing truthful unresolved-store fallback. Deliberately unavailable/hung external dependencies are tested as failures and reported separately, not silently mixed into or omitted from the common latency workload.

| Gate | Required measurement / correctness evidence |
| --- | --- |
| Browser idle | Five visible connected minutes, both without and with an open reader; zero periodic catalog/document REST reads and repeated search/timeline queries. Separate user actions, revision checkpoints, transport keep-alives and time boundaries |
| Normal freshness | Deferred to `realtime-dashboard-performance-evidence`. Correctness of change-to-visible refresh remains covered by the unit, browser and smoke gates |
| Server work | Deferred to `realtime-dashboard-performance-evidence` |
| Recovery | Re-established transport to reconciled visible state <=5s per successful representative recovery; faults that keep storage unavailable must remain visibly paused and recover once released |
| Fault convergence | One clean bounded smoke run of five deterministic cycles, one per category: interrupted connection, lost/duplicate/out-of-order notifications, owned server restart, watcher gap/overflow, secret revocation followed by fixture-only reauthorization. Include writes during outages, separate writer processes and simultaneous browsers. The full 100-cycle matrix is deferred to `realtime-dashboard-performance-evidence` |
| Boundaries | Actual Host/Origin/cookie rejection at negotiate/upgrade/SSE; live revocation; unknown identifiers and path containment; no preference-write feedback loop or post-disposal timers/watchers/connections |
| Reader/time | Selected copy/path, archive state, rendered/source mode, search/inbox/read hints, keyboard focus, drafts and positions survive normal/fault refresh; fake clocks exercise seven/fourteen-day and timezone midnight/DST boundaries |
| Packaging | Full normal Release and fresh actual self-contained packaged no-Node/offline browser suites, plus applicable packaged MCP/bootstrap smoke; client assets and notices embedded, no new package output files |

The smoke run has zero incorrect final states, lost saved observations, discarded drafts or unbounded notice accumulation, and runs under a bounded overall timeout with captured host logs. Test request/deadline/concurrency/cleanup assertions deterministically rather than through timing-sensitive unit assertions. An actual target failure is escalated with evidence, not hidden by raising limits or excluding scenarios. Reduced REST requests are not a claim of lower total server CPU; that measurement belongs to the follow-up.

The baseline executable, harness and server-work instrumentation approach above remain preserved for `realtime-dashboard-performance-evidence`, which owns latency percentiles, server-work comparison and the 100-cycle matrix.

Use current and preserved pre-C06 compatible executables as isolated writers. Equivalent Claude Code/Codex protocol-fixture labels cover the shared unchanged MCP contract; no native sessions or live installation checks are authorized by this plan. Independent QA and code analysis must review the final stable implementation target and acceptance evidence through the repository workflow.

## Risks / Trade-offs

- Missed or reordered notifications -> per-domain pending/applied revisions, checkpoints, bounded reconciliation and full recovery on instance change.
- Slow catalog tools block freshness -> separate cheap document invalidation from expensive command refresh, keep discovery parallel/cached, and expose partial source state.
- Watcher volume and linked paths -> deduplicated canonical allowed roots, bounded hints, explicit overflow recovery and disposal; measure server work at representative sizes.
- SQLite WAL/read side effects or same marker after replacement -> compare authoritative markers and lifecycle generations, suppress self-noise, preserve failure transitions and keep detection read-only.
- Revoked cookie continues receiving events -> pre-send secret validation plus bounded idle connection revalidation and transport-level tests.
- Retry/refresh loops damage usability -> one lifecycle owner, one in-flight read per resource, finite attempts, retained pending state, one status surface, and no automatic retries after confirmed authorization loss.
- New bundled dependency or self-contained trimming breaks offline use -> lock concrete client dependency, verify embedded licenses/assets and execute both supported package modes.
- Sub-second target competes with source scanning -> measure before and after; if the agreed workload misses the target, report the bottleneck and seek a scope decision rather than claiming request-count reductions prove success.

## Migration Plan

Implementation order is baseline/harness, detection and revisions, authenticated delivery, targeted refresh/lifecycle, temporal invalidation, then integrated evidence and independent review. Keep the old poll until the replacement passes focused tests, then remove it and prove the idle network gate; do not ship dual background owners. Introducing live delivery requires no schema migration, MCP reconnection requirement, or agent-specific policy. Existing HTTP actions and compatible writers continue working.

The eventual release bundles server and browser together. Reverting to the previous executable restores its polling client against the same private data; no stored event history or new persisted version is introduced by C06. Source version, release notes/publication and installation remain separate authorized work. No unresolved product or architectural question is required before apply; concrete compatible dependency patch selection and evidence-output names can be resolved during implementation without changing these contracts.
