# Design

## Context

See [proposal](proposal.md) for motivation and release scope. The target is main at `ac6d7f0`; the agreed execution order is **C05 -> C03 -> C02 -> C01 -> C04**. The estimate is 5-10 engineer-days, not measured delivery time.

Current source observations:

| Area | Current implementation / evidence | Consequence for this design |
| --- | --- | --- |
| Reader | `app.html` renders `selectedChange().documents` in a horizontal strip and headings/tasks in a left outline; `SourceCatalog.AddChange` already retains recursive relative paths | Reuse document identity and discovery; change navigation presentation rather than invent new scanning |
| Source copies | The chooser tracks variant alone; `openCard` sets `copyPath`, while route-driven chooser paths do not reset it | Use source-qualified keys and navigation-scoped path state |
| Read marks | `markChangeRead` counts every exception as a changed document | Preserve successful marks and classify conflicts separately from failures |
| Config reload | `SourceCatalog.DiscoverAsync` advances the stamp after failure and adds a warning only to that snapshot | Retain a last-failure state and retry without an accumulating notice list |
| Startup | `start` aborts immediately when bootstrap fails, even if an existing cookie would authorize protected requests | Verify an existing session independently after an expired-token response |
| Optional digests | `DigestService.Request` and `DailyService.Digests` catch failures and return no request/digests | Add bounded diagnostics and an additive private Daily issue field; do not fail saved logging |
| Comparison | `diff.ts` runs synchronously with a 3000ms limit; incomplete marks are empty; `parseReviews` renders/sanitizes the baseline | Bound the comparison and extract comment tokens without a second full render |
| Daily acceptance | A scenario appends a real DEMO-2 observation to the shared fixture; reset restores preferences rather than journal contents | Isolate the mutating scenario's data and process context while preserving real-write acceptance |
| Store acceptance | The fixture declares an unknown store and also creates `openspec/changes/` | It is a real root under the installed CLI's precedence rules, not an unresolved member |

Read-only probe: running `openspec context --json` in the existing synthetic store-member fixture returned its local root and explicitly warned that its declaration is ignored because it has a planning shape. The installed resolver defines `specs/` or `changes/` as that shape, while a config-only directory can be a declared member. No user store was registered or modified. This resolves C05's fixture ambiguity without changing product root precedence.

The installed `diff` package is 9.0.0. Its local implementation and current [upstream documentation](https://github.com/kpdecker/jsdiff/blob/master/README.md) support `timeout` and `maxEditLength`, returning an undefined result on abortion. Callback mode exists but adds asynchronous reader-state coordination.

## Goals / Non-Goals

**Goals**

- Keep the release slice self-contained and ready for delegated implementation with explicit evidence.
- Preserve source-qualified navigation, bounded preferences, drafts, document freshness, revision conflicts, read-only archives, local access controls and existing repository colors.
- Make large comparisons fail gracefully within an explicit measured work budget.
- Apply shared optional-digest behavior consistently to Claude Code and Codex; preserve stdout as MCP protocol traffic.

**Non-Goals**

- No SignalR, change watchers, new polling architecture, storage recovery/migration, identity re-keying, general cache/query optimization or component rewrite.
- No change to managed agent guidance, MCP tool schemas or journal/digest schemas.
- No live-agent evaluation, user installation/configuration change, version bump, publication or release tagging. v0.5.0 remains the intended release grouping.

## Decisions

### 1. Stabilize acceptance before changing behavior (C05)

The shared serial dashboard fixture remains alive for normal browser cases. Give each Daily scenario that writes observations or digests its own disposable fixture/server context, explicit base URL and metadata. Scope helper changes to that context; do not overwrite the shared `fixture.json` while another server uses it. Stop only harness-owned processes through managed teardown.

Implementation clarification: the initial repetition run showed that the stale-digest and digest-save scenarios also mutate shared Daily data. Isolate those writers through the same scoped context so the required ten repetitions retain fixed expectations; this does not expand product scope.

This permits ten repetitions of the Daily group during one parent fixture lifetime without making previous cases observe the later write. Preserve the real writer and actual Daily update assertions. Do not delete observations, swap a live SQLite/WAL file, weaken fixed expectations or derive expected values from the result being tested. A purely intercepted response is not a substitute for the real-write case.

Make the unresolved-store fixture a genuine config-only member with no `specs/` or `changes/` tree. Strengthen assertions around the source-specific unresolved status and retained History. Keep a separate unit/control case for a real root plus store declaration: the CLI-resolved real root is authoritative. Do not change `SourceCatalog.ResolveAsync` just to make an incorrectly modeled fixture pass.

Capture the CLI version and relative fixture shape in evidence; never change the installed CLI, global store registry or personal settings. Cover CLI-present normal Release and CLI-absent packaged modes without excluding the declared-store scenario.

**Alternative considered:** reuse the shared append-only journal and restore it afterward. Rejected because a live database/WAL reset is unnecessary and fragile. Reject broad CLI shims that only fake the failing expectation; mocked source-command tests remain useful separate unit evidence.

### 2. Represent failures honestly with bounded state (C03)

- **Read marks:** separate successful marks, confirmed HTTP 409 conflicts, and other failures. Keep successful fingerprints. Summarize partial outcomes once. On authorization loss, stop the batch and explain how to authorize rather than generating one error per remaining document.
- **Configuration:** retain the last reload failure and last successful settings independently. Retry a failed stamp at the existing bounded refresh cadence; publish one warning while stale settings are used. Clear it only after a successful reload. Preserve scoped source notices.
- **Startup:** after a bootstrap 401, a protected request such as `/api/info` can prove that the existing cookie is valid. Reuse that result during initialization. Do not infer authorization from a public health check. Without proof, show the existing relaunch instructions. Handle unexpected/network failures separately, remove the handled token fragment, and offer a single-flight Retry action without duplicating polling timers/subscriptions.
- **Digest logging:** supply a small optional diagnostic sink from `McpServerHost` using its existing stderr logger. Keep category-based suppression bounded (one diagnostic per unchanged category per minute); no observation text, authorization material or raw private configuration. Sink failures must not fail `log_work`. Existing constructors and tool schemas remain compatible.
- **Daily digest reads:** add an optional private `digestIssue` value with a stable category and safe message; absent/null means no failure. A missing store or absent digest is normal. A failed read shows one inline unavailable indication and keeps observations/fallback summaries. A later successful read clears it. Do not attempt store repair here.

Use an explicit empty/available/unavailable result at the digest-read boundary so normal absence is not mistaken for failure. The API addition is private and additive; older clients ignore it.

**Alternative considered:** turn optional digest failures into tool errors. Rejected because an observation may already have been saved and optional processing must not invalidate that success. Unbounded logging is also rejected.

### 3. Keep chooser identity and targets local to one navigation (C02)

Reuse the existing source/variant identity helper or an equivalent unambiguous composite for view tracking. Do not re-key persisted board order, baselines or routes. Set or clear the requested document path whenever a chooser is opened, replaced or closed; route-driven choices derive their target from that route. Preserve `design.md`, then `proposal.md`, then first-artifact defaults when no explicit/remembered target exists.

Use component fixtures with two sources sharing a variant, explicit nested paths, closing/reopening from another route, drafts and browser history.

**Alternative considered:** make variants globally unique in the catalog. Rejected because that changes existing identities unnecessarily.

### 4. Reuse the reader's left column as a document navigator (C01)

Introduce a small pure grouping/filtering helper, for example `dashboard/src/document-navigation.ts`, over the existing document list. Use Overview (Proposal, Design, Tasks), Specs (capability-relative paths), and Other documents (relative paths). Group order does not change the reader's default-document rule.

Use native navigation, lists/buttons, input and disclosure semantics rather than promising a complex tree keyboard model. Preserve full paths in visible/accessibility text; do not make tooltips the only way to distinguish equal basenames. Add an explicit current-document indication even when a filter excludes its list entry. Filter state is local to the reader and resets with the selected change; no new preference field.

Place the existing heading/task outline in a collapsible On this page section. Keep Read/Source and document actions with the reader toolbar. The navigation rail scrolls independently from content. At narrow widths, expose the same list in a labeled drawer/collapsible selector. Escape first leaves focused inputs as today; dismissing the selector restores its opener without closing the reader. Preserve focus after explicit selection and on background updates.

For `@source` documents with no selected change, show truthful current-source/current-document context rather than fabricating sibling documents. Preserve archived read-only behavior and missing-document notices.

**Alternative considered:** merely rotate the horizontal buttons or add another permanent column. Rejected because long paths, filtering, narrow screens and the existing outline need one coherent navigation surface.

### 5. Bound synchronous comparison and make abortion visible (C04)

Before editing comparison code, capture a synthetic baseline for 50/200/400/800 KiB documents, both small edits and complete rewrites. Use repeated warm runs, report p50/p95 and compare the same workload/build/environment before and after. Historical three-second results motivate the work but are not the current baseline.

Retain a synchronous bounded path first: use the installed diff implementation's timeout plus an edit-distance limit, starting with a 50ms algorithm timeout and a 2000-line edit-distance ceiling. Tune downward within the comparison budget from evidence. Measure the complete comparison helper, including normalization/tokenization, not only the inner algorithm.

The acceptance target is **p95 <=100ms of synchronous comparison work** on the recorded reference fixture/environment. It is not a guarantee that arbitrary Markdown rendering completes in that time on every device. Keep timing evidence separate from deterministic unit assertions.

An aborted comparison returns an explicit incomplete result with a visible document-level fallback mark and at least one change-navigation stop in rendered and source views. Handle an empty current document with a visible fallback banner/stop instead of inventing text. Preserve exact normal-edit added/rewritten/removed ranges.

Extract review comments from the same Markdown token rule the renderer uses, without rendering and sanitizing the whole baseline. Memoize removed-comment results for the current revision pair only. Preserve current marker decoding, fenced-code exclusions and legacy behavior; lossless marker-format redesign is a separate parked item.

**Alternatives considered:** unlimited sync work, immediate worker introduction, and callback-based comparison. Unlimited work violates responsiveness; workers/callbacks add state and build complexity. If the bounded path cannot meet the measured target, the developer returns the evidence for a scoped design decision rather than silently accepting a slower result.

### 6. Keep evidence and completion proportional

Preserve implementation order C05, C03, C02, C01, C04. Each handoff and independent review follows the delegated workflow. Resolve blockers before dependent work; do not treat checked tasks as proof.

Run relevant .NET/dashboard unit checks during each slice. Final acceptance includes locked restore, Release build, full .NET/dashboard suites, and full normal Release plus actual self-contained packaged no-Node browser runs. The declared-store case must not be skipped. The Daily group repeats ten times in the same parent fixture lifetime. Reader acceptance covers 10/30/50 documents, repeated basenames, both themes and 390/768/1440px. Retain previous board/color/theme checks.

For shared MCP diagnostics, run equivalent protocol scenarios through the shared server path used by both integrations, verify stdout parsing and successful saved observations, and label them as fixture/protocol evidence. No native-agent account probe is part of this change.

Default generated browser/performance output stays in ignored `dashboard/test-results/` or `artifacts/`. Reviewers copy only reserved evidence into this change's `reports/` and `screenshots/`. Do not hardcode an active-change path into long-lived test output.

## Risks / Trade-offs

- **An aggregate release slice spans frontend and shared diagnostics.** -> Keep ordered task groups, scoped developer ownership and stable review targets; no unrelated cleanup.
- **Current-source controls might become hard to reach on narrow screens.** -> Test the full navigator, toolbar, outline and comment composer at each viewport with keyboard input.
- **A filtered list or route refresh could lose selection/drafts.** -> Preserve the existing explicit-selection epochs and per-document state; test out-of-order responses and back/forward navigation.
- **Lower diff limits provide less detail for very large rewrites.** -> Keep an honest visible fallback and precise ordinary-edit coverage; record quality and latency together.
- **Diagnostic repetition or raw exception text could overwhelm users or disclose content.** -> Fixed categories, safe messages, bounded suppression and protocol-only stdout.
- **Configuration retry can repeatedly parse an invalid file.** -> Existing bounded refresh cadence and one persistent warning; no tight retry loop.
- **The mutating Daily fixture increases harness complexity.** -> One scoped fixture context with explicit ownership and guaranteed teardown; no general test-framework rewrite.
- **Optional API status can drift between frontend and backend.** -> Add contract/component coverage for missing/null/unavailable/recovered states.
- **A microbenchmark can be mistaken for user-visible speed.** -> Label comparison-only timing and retain browser behavior evidence separately.

## Migration Plan

No data migration is required. Changes use existing document/source identities and preference bounds; new filter state is ephemeral. The private Daily issue field is additive. Existing repository color changes already on main are carried forward.

Implement on a change branch through the developer role after an apply request. Package acceptance uses disposable data and self-contained artifacts; do not install into the user's environment. Application rollback uses the prior compatible payload and retains journal, digests, documents and existing preferences. Release versioning, push, tags, publication and installation require their own authorized actions.

## Evidence references

- Current source: `dashboard/src/app.ts`, `app.html`, `styles.css`, `diff.ts`, `markdown.ts`, `reviews.ts`; `SourceCatalog.cs`, `DailyService.cs`, `DigestService.cs`, `McpServerHost.cs`; browser server/fixture and focused test files.
- [Review-inbox initial analysis](../archive/2026-10-28-add-review-inbox/reports/analysis-001.md) and [follow-up analysis](../archive/2026-10-28-add-review-inbox/reports/analysis-002.md): comparison, read-status errors.
- [Board follow-up analysis](../archive/2026-10-29-add-ongoing-board/reports/analysis-002.md): source chooser identity/path.
- [Reliability analysis](../archive/2026-10-27-fix-dashboard-reliability/reports/analysis-001.md): persistent configuration warning.
- [Digest analysis](../archive/2026-11-03-add-day-digests/reports/analysis-002.md) and [Daily fixture QA](../archive/2026-11-03-add-day-digests/reports/qa-011.md): optional failures and fixture isolation.
- [Installed-version supported diff options checked against current upstream documentation](https://github.com/kpdecker/jsdiff/blob/master/README.md).
