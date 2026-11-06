# Tasks

Implementation order is **C05 -> C03 -> C02 -> C01 -> C04**, followed by integrated acceptance and completion. Follow the delegated repository workflow: the orchestrator owns assignments, task state and findings; the developer owns implementation; both reviewers assess stable targets. All boxes remain unchecked until behavior and the named evidence pass.

## 1. C05 - Repeatable acceptance foundation

- [x] 1.1 Give each Daily scenario that writes observations or digests an isolated disposable fixture/server context with explicit base URL and metadata, preserving the shared parent fixture and real observation/digest assertions; verify teardown releases its owned port/process and that no live journal/WAL is reset or shared fixture metadata overwritten.
- [x] 1.2 Correct the unresolved-store fixture to be a config-only declared member without a planning tree, and add a separate real-root precedence control; verify source-specific failure plus usable History with the real installed CLI and the existing CLI-absent mode, recording the CLI version without changing global store registration.
- [x] 1.3 Replace ambiguous stale-row waits in the affected History/link/copy scenarios with discriminating target assertions; verify the focused scenarios repeat without passing on an earlier filter's rows or weakening their final content assertions.
- [x] 1.4 Run the Daily group ten times in one parent fixture lifetime and the corrected declared-store scenario in normal Release and actual packaged no-Node modes; verify every repetition passes with stable initial expectations and no leaked fixture state, skipped assertions or retained owned servers.

## 2. C03 - Actionable failures

- [x] 2.1 Separate successful read marks, HTTP 409 conflicts, other failures and authorization loss in single/change-wide marking; verify component cases for mixed partial success, storage/network errors and 401 retain successful fingerprints/drafts and produce one accurate aggregate outcome.
- [x] 2.2 Retain and retry configuration-reload failures while using last-valid settings; verify fake-clock/disposable-config tests show one persistent warning across successive refreshes and clear it only after a successful reload, preserving repository scope and journal access.
- [x] 2.3 Recover startup after an expired/used token only when a protected request proves the existing session, remove the handled fragment and add a single-flight startup Retry; verify authorized/unauthorized, network/server-failure and retry cases preserve the access boundary and do not duplicate timers/subscriptions.
- [x] 2.4 Add a bounded safe optional-digest diagnostic sink through the existing MCP stderr logging path without changing tool schemas; verify successful saved observations, coalescing, a failing diagnostic sink and protocol-only stdout through equivalent shared-server protocol fixtures for Claude Code and Codex, explicitly labeling fixture evidence.
- [x] 2.5 Add an optional private Daily digest-issue status and one inline unavailable indication while preserving observations/fallback summaries; verify absent/missing-store, successful, unavailable, repeated-failure and recovered states in server contracts and dashboard components without repairing storage.

## 3. C02 - Correct copy selection

- [x] 3.1 Track offered copies with a source-qualified identity without changing catalog variants, routes, baselines or board keys; verify two sources sharing a variant remain distinct and open the selected source with no duplicate view-tracking keys.
- [x] 3.2 Set/reset the chooser's target path for explicit, route-driven, replaced and closed navigation; verify an earlier nested-document choice cannot leak into a later chooser and that default design/proposal selection, drafts, back/forward history and current-source context remain correct.

## 4. C01 - Vertical document navigator

- [x] 4.1 Add pure grouping/filtering over existing document identities for Overview, Specs and Other documents, with relative-path labels and explicit current-document state; verify 10/30/50-document fixtures, duplicate basenames, nested paths, empty matches and filter reset between changes without new preference persistence.
- [x] 4.2 Replace the horizontal strip with a desktop left navigation rail and collapsible On this page heading/task outline while retaining Read/Source and reader actions; verify keyboard reachability, visible focus, selected-file identity, default selection and readable content in both themes.
- [x] 4.3 Add the narrow-screen document selector and focus-aware dismissal; verify 390px and 768px layouts plus 1440px desktop have no horizontal overflow and that input Escape, selector dismissal and composer drafts follow the existing innermost-interaction rules.
- [x] 4.4 Preserve active/archive/source-reference contexts, remembered positions and the latest explicit selection across background/out-of-order refreshes; verify component and browser cases for nested specs, missing documents, archive moves, deep links, both views and restored drafts.

## 5. C04 - Responsive review comparison

- [x] 5.1 Before changing comparison code, add/use a reproducible isolated comparison harness and capture 50/200/400/800 KiB small-edit and full-rewrite baselines with warm-up, at least 30 measured samples, p50/p95, source/build identity and sanitized environment metadata; save raw results under ignored output and label historical figures separately.
- [x] 5.2 Bound comparison with timeout and edit-distance controls and provide an explicit visible incomplete result with a usable document-level navigation stop in rendered/source views, including an empty current document; verify deterministic timeout/limit, precise small-edit, removed-passage and unchanged-content cases without timing-sensitive unit assertions.
- [x] 5.3 Extract removed review comments through the renderer's shared token rules without full baseline rendering/sanitizing and memoize only the current revision pair; verify legacy/structured markers, fenced examples, existing decoding, revision invalidation and bounded retained state preserve current comment behavior.
- [x] 5.4 Repeat the same comparison workload after the changes and deliver before/after raw evidence; verify p95 synchronous comparison work is at most 100ms on the recorded reference environment and browser behavior stays responsive with honest fallback marks, escalating a failed target rather than weakening it.

## 6. Integrated acceptance and independent review

- [x] 6.1 Update relevant dashboard/MCP validation and user guidance with final behavior, reproducible commands, actual results, evidence labels and known limitations; verify new text matches implementation, preserves historical reviewer reports, and contains no personal data, credentials or fixture residue.
- [x] 6.2 Run locked restore, Release build, full .NET and dashboard unit suites, and full normal Release plus actual self-contained packaged no-Node browser acceptance with no declared-store exclusion; verify existing repository colors, board ordering, preferences, source access controls and theme behavior remain passing.
- [x] 6.3 Deliver reader acceptance for 10/30/50 documents and filter-then-select within two control actions, both themes and 390/768/1440px, including keyboard and draft/position preservation; verify rendered geometry and save allocated synthetic screenshots/evidence without hardcoding an active-change path into default test output.
- [x] 6.4 Obtain current independent QA and code-analyzer reports on the stable implementation, with before/after identities and all applicable spec scenarios and hygiene checks covered; verify every finding is triaged, required evidence is present and no unresolved blocker or unapproved deferral remains.

## 7. Completion

- [x] 7.1 Reconcile this checklist with independent evidence, strict OpenSpec validation and the final implementation identity, then record the scoped implementation commit and actual remaining work; verify no release publication, installation, archive or source-version change is implied by completing the v0.5.0 implementation candidate.
