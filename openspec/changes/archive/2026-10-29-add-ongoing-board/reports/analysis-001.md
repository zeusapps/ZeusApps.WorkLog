<!--
Saved by the orchestrator (Claude Code 2.1.312, session 1570e404-00dd-4689-9952-af0e082ed1e3) on 2026-10-28.
Reviewer: A3 code-analyzer, Claude Code 2.1.312, agent subagent (non-interactive), claude-opus-5-5.
Case: the client refused the reviewer's write. Exact refusal message as reported by the reviewer: "Subagents should return findings as text". Text below is the reviewer's report verbatim.
-->

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.312 / claude-opus-5-5
task / finding IDs: tasks 1.1–4.2; new ANALYSIS-001 – ANALYSIS-009 (3 warnings: 001, 002, 003; 6 suggestions: 004–009)
changed or report paths: none written. Intended path D:\Code\ZeusApps.WorkLog\openspec\changes\add-ongoing-board\reports\analysis-001.md; the write was refused, and the full report text is inline below
checks: npx vitest run src/board.spec.ts -> passed (34/34); buildBoard timing probe and duplicate-key probe (Node 24.13.0, board.ts bundled with the repository's esbuild into a scratch directory outside the repository) -> run; preference payload size estimate -> run; full npm test / dotnet test / e2e / dotnet format -> not run (QA owns functional validation)
target identity: before head=607df5f baseline=607df5f files=19 diff=8af72419df0b untracked=6/6493d633bf13; after head=607df5f baseline=607df5f files=19 diff=8af72419df0b untracked=6/6493d633bf13 (match)
limitations / decisions needed: the report write was refused by the client, so the report text is inline and the orchestrator saves it. Decisions needed: whether copies of one change across sources form one card (001); an order bound or key length that fits 64 KiB, which may need a spec text change (002); the deviation 2 pruning rule (006); adding a quiet helper to the board.ts contract that add-daily-prep consumes (003). Not measured: server read cost (005), drag re-render cost (008), a live NG0955 warning (001)
next: orchestrator saves the report, triages ANALYSIS-001 – 009 into findings.md, and routes the accepted ones to the developer
```

---

# Analysis report: add-ongoing-board / analysis-001

## Review target

- Reviewer / role: code-analyzer subagent (assignment A3) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 / agent subagent, non-interactive / claude-opus-5-5
- Date: 2026-10-28T21:38Z
- Change root / assignment: `openspec/changes/add-ongoing-board` / A3
- Task and requirement references: tasks 1.1–4.2 ([tasks.md](../tasks.md)); [design.md](../design.md) Decisions 1–6; [spec delta](../specs/work-dashboard/spec.md) "Ongoing board", "Board priority order", "Bounded dashboard preferences"; [findings.md](findings.md) "Scope decisions" deviations 1–11
- Scope and relevant neighboring paths: `dashboard/src/board.ts`, `board.spec.ts`, `app.board.spec.ts`, `app.ts`, `app.html`, `models.ts`, `styles.css`; `src/WorkLog/Dashboard/TimelineService.cs`, `DashboardHost.cs`; `tests/WorkLog.Tests/Dashboard/OngoingUnlinkedTests.cs`; `tools/WorkLog.Smoke/DashboardFixture.cs`; `dashboard/e2e/dashboard.spec.ts`. Neighbors read for reuse and drift: `dashboard/src/archive.ts`, `quiet.ts`, `links.ts`, `reviews.ts`, `workitem-markdown.ts`; `src/WorkLog/Dashboard/DashboardJournal.cs`, `SourceCatalog.cs`, `DashboardState.cs`; `src/WorkLog/Reporting/WorkIdentity.cs`; `src/WorkLog/Persistence/SqliteEntryStore.cs`
- Reading limits and paths read outside scope: the assignment named the key code without a hard limit. Outside the list, I read `openspec/changes/add-daily-prep/design.md` (Decision 3, as the assignment asked), `docs/architecture.md`, `README.md` (board section), and the installed `@angular/core` 21.2.25 runtime (`node_modules/@angular/core/fesm2022/_debug_node-chunk.mjs`, the duplicate-track check)
- Baseline commit: `607df5f`
- Implementation identity (before): `head=607df5f baseline=607df5f files=19 diff=8af72419df0b untracked=6/6493d633bf13`. Tracked diff SHA-256 `8af72419df0b3c7da87a5c4dea181ee02d2cae2894d133e44cc22305cef59279`. Untracked: `board.ts` `53092c5c…`, `board.spec.ts` `54f2ce46…`, `app.board.spec.ts` `152b70d5…`, `OngoingUnlinkedTests.cs` `a47f11d1…`, two screenshots `fd0b5fb6…` and `e2d533b7…`
- Before/after identity: after the analysis, `head=607df5f baseline=607df5f files=19 diff=8af72419df0b untracked=6/6493d633bf13`. It matches; there was no unexpected mutation.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Purity / determinism | Inspected `board.ts` | No DOM, signals, `Date.now()` or I/O. `now` and `order` are parameters. `byPriority` breaks ties by title and then key, so sorting is total. | Pure and deterministic, as the design Goals require. In `App.fullBoard`, `this.now()` is not a signal. Expiry is re-evaluated only when another input changes. Each 2 s poll currently does `catalog.set(newObject)`, so in practice the board recomputes on every visible poll (see the steady-state table). |
| Duplication with `quiet.ts` / `links.ts` / `app.ts` / `archive.ts` | Inspection, `grep` | `changeCards` reuses `archive.ts#activityFor`. The old `ongoingTickets`/`changeRow` predicates moved into `ticketCards` and were not copied. `activityReady()` was extracted and now deduplicates the scope check that `changeActivity` used to repeat. The quiet rule is not in `board.ts` but is re-derived per card in the template (ANALYSIS-003). The ticket dedupe key is a third copy (ANALYSIS-007). | No logic from `quiet.ts` or `links.ts` is duplicated. Two gaps are noted. |
| Order / move / prune algorithm | Inspection plus a reasoning trace of `moveWithin`, `dropWithin`, `rankColumn`, `pruneOrder`. Ran `npx vitest run src/board.spec.ts`: 34/34 passed. | `rankColumn` inserts at the first moved key's index, which equals its index in `rest` because no earlier key was removed. `moveWithin` "down" splices at the neighbor's original index, which lands after it once the key is removed. `pruneOrder` dedupes, filters live keys, and then slices to keep the highest ranks. `applyColumn` (`app.ts:800`) adds every active catalog change key to `live` (deviation 2). | Correct and simple for unique keys. It breaks when two cards share a key (ANALYSIS-001). Deviation 2 applies asymmetrically (ANALYSIS-006). The 500 cap does not fit the 64 KiB budget (ANALYSIS-002). |
| Endpoint reuse | Inspected `DashboardHost.cs:117–146`, `TimelineService.Unlinked`, `DashboardJournal.Read` | The `/api/` authorization middleware covers the new route, and `ArgumentException` maps to 400 through the existing handler. `Unlinked` calls the same `journal.Read(to, repositories, snapshot, ct)` as `Query`, so snapshot validation is shared. The predicate `Change is null && Tickets.Length == 0` matches `WorkIdentity.For`'s bucket case. | Reused, not copied. The read is unbounded below (ANALYSIS-005). |
| Extra request per refresh | Inspected `loadOngoing` (`app.ts:697–735`) and `poll` | One `POST /api/ongoing/unlinked` per Ongoing refresh. A refresh happens only on a `journalSnapshot` change, a day rollover, or a scope change. It is sent serially after all timeline pages. | No new timer or polling. The client cost is acceptable. The server cost is one extra full-journal decode per journal change (ANALYSIS-005, hypothesis). |
| Recomputed signals / per-card work | Inspected `app.ts:270–284` and `app.html:359–460`. Timed `buildBoard`+`filterBoard` on synthetic inputs, using `board.ts` bundled with the repository's esbuild into a disposable scratch directory outside the repository, on Node 24.13.0. | `fullBoard`, `board`, `boardTotal` and `boardShown` are `computed` and are not recomputed per card. The template calls `changeActivity(change)` per change card on every change-detection pass, and it rescans all activity. `formatDate` builds a new `Intl.DateTimeFormat` per call, which is pre-existing; the board adds one call per card. `dragOver` writes a new object to a signal on each event (ANALYSIS-008). | Measured: 0.205 ms per recompute for 40 changes, 100 rows, 20 unlinked, 80 activity and 50 order keys. 2.741 ms for 200/500/200/800/500. This is negligible at one recompute per 2 s poll. |
| Dead code from removed UI | `grep` for `changeRow`, `ongoingTickets`, `latestActivity`, `.ongoing-panel`, `.change-grid`, `.work-item`, `.work-list-icon`, and baseline comparison | `changeRow`, `ongoingTickets`, `.change-grid` and `.ongoing-panel` are gone. Left behind: `App.latestActivity` (`app.ts:625`), plus orphaned `.work-item*`, `.work-list-icon` and `.work-list-item strong/small` rules in `styles.css` | ANALYSIS-004 |
| API for `add-daily-prep` | Compared `board.ts` exports with `add-daily-prep/design.md` lines 13 and 49–56 | `buildBoard(input, now)` has no filter parameter, so "an empty filter" means calling it without `filterBoard`, which is usable as is. `byPriority(order)` is a generic comparator over `BoardCard`, which is usable. Daily's design states that `buildBoard` carries the quiet indication and filters In-progress cards by it. `BoardCard` has no quiet field. | One gap (ANALYSIS-003). The `BoardInput.changes` contract must include archived changes for follow-ups, which is easy to get wrong in a second caller. Recommendation in ANALYSIS-003. |
| Test-fixture duplication | Inspection | The legacy-row `INSERT INTO entries…` helper exists in both `OngoingUnlinkedTests.cs:16–31` and `DashboardFixture.cs:98–113`, both tied to the schema 2 columns. | No finding. The two projects share no test-support assembly, and a shared helper in production code would be worse. Recorded for awareness if the schema changes. |
| Architecture drift | Compared with `docs/architecture.md` | The doc names `POST /api/activity/changes` but not the new `POST /api/ongoing/unlinked`. The client-module boundary (a pure TS module beside `quiet.ts` and `links.ts`) matches the existing pattern. | ANALYSIS-009 |
| Parity | `git diff --stat 607df5f` | Under `src/WorkLog` only `Dashboard/DashboardHost.cs` and `Dashboard/TimelineService.cs` changed. No `Integration`, `Mcp`, `Cli`, or launcher paths changed. | Parity does not apply. Confirmed that no MCP, init or integration path changed. |
| Formatting | Not run | The new C# lines follow the surrounding style on inspection. | Not performed: proportionality. QA owns the functional checks. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Client `poll()` every 2 s; `fullBoard` computed | Source observation; `buildBoard` cost measured | The change adds no timer. Each visible idle poll is still `GET /api/catalog` (pre-existing). Because `catalog.set` stores a new object, `filteredChanges`, `fullBoard` and `board` recompute on every poll: 0.2 ms typical, 2.7 ms large (measured). The new request and its journal read run only when `journalSnapshot` moves, on day rollover, or on a scope change. No process is spawned. |
| Cache lifetime versus interval and refresh duration | `/api/ongoing/unlinked` | Source observation | There is no cache, and none is needed: the request is gated by `journalSnapshot`, the same as the timeline. `loadActivity`'s key cache is unchanged. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `Preferences.board.order`; `expandedCards`; unlinked list | Measured payload estimate | `board.order` is capped at 500 and pruned to live keys on save. The 500 cap combined with the existing position and document bounds can exceed the 64 KiB state limit (about 80.6 KB, see ANALYSIS-002). The order is not capped on load, but nothing writes it without a save. `expandedCards` grows within a visit only, by user clicks. The unlinked list is capped at 200 on the server. |
| Repeated full reads where a change check would do | `TimelineService.Unlinked` → `DashboardJournal.Read` | Source observation; cost is a hypothesis | Each call decodes every entry with `occurred < to` in the selected repositories (the whole history) and then filters to the 7-day window in memory (ANALYSIS-005). |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | None added | Source observation | Not applicable. The change spawns no process and adds no tool lookup. |

## Findings

### ANALYSIS-001: A change present in several sources yields cards with identical keys

- Severity: warning
- Related task / requirement: 2.1, 2.2, 3.2, 3.3; "Board priority order" (rank identity by repositories and name)
- Locations: `dashboard/src/board.ts:55–57` (`changeKey`), `:82–108` (`changeCards` maps every active catalog entry), `:234–265` (`moveWithin`/`dropWithin` use `indexOf`/`filter` by key); `dashboard/src/app.html:359` (`track card.key+(card.change?.variant??'')`); `app.ts` `focusCard` (first element with the key)
- Observation: the catalog emits one `Change` per source. `SourceCatalog.cs:273` sets `Variant` to the hash of the change's path relative to the source root. A checkout and its worktree, which both map to one repository, therefore produce two active entries with equal `name`, `repositories` and `variant`. The e2e fixture already contains this case (`activity-timeline` in the main repository and its worktree, `DashboardFixture.cs:34,39–42`). A probe running `buildBoard` over two such copies plus one other change returned in-progress track keys `[…activity-timelinesame, …activity-timelinesame, …repository-filterssame]`:
  - The `@for` track expression is duplicated. Angular 21 dev mode warns with NG0955, and DOM reuse for those rows is undefined.
  - `moveWithin(column, visible, <second copy>, "down")` returned a 2-key column from a 3-key column. Both copies jumped together, and the second copy's own position was never used. `dropWithin` behaves the same.
  - `focusCard` and the `.dragging` class address the first or both copies.
  - The baseline grid tracked by `change.sourceId+change.variant`, so this is a regression in tracking.
- Proposed improvement and benefit: decide whether copies form one logical card. The spec's rank identity (repositories plus name) and `add-daily-prep`'s "each card once by its board key" both imply that they do.
  - If they do, have `changeCards` emit one card per `changeKey` and keep the copies for the existing copy chooser (`openChange` already handles several copies).
  - If not, at minimum track by `card.change.sourceId + card.key` and make the move helpers operate on unique keys.
  - Either way, add a `board.spec.ts` case with two copies.
- Behavior-preservation risks: collapsing copies changes the card count in the fixture's worklog repository, which affects e2e counts and History row expectations. The document links on a merged card must open the copy chooser.
- Evidence type: measured result (a probe over the bundled `board.ts`), plus a source observation for the template and runtime warning
- Verification / measurement plan: a unit case with two `Change` objects that differ only in `sourceId`. Assert unique track keys and that a move keeps the column length. In the e2e fixture, check the browser console for NG0955 on `/ongoing` with a development build.
- Relationship: none

### ANALYSIS-002: A full 500-entry order can push preferences past the 64 KiB cap

- Severity: warning
- Related task / requirement: 2.2, 3.1; "Bounded dashboard preferences" ("SHALL remain within their storage limit regardless of … how many cards the user ranks")
- Locations: `dashboard/src/board.ts:7` (`orderLimit = 500`), `:280–282`; `dashboard/src/app.ts:94–95` (`positionLimit = 100`, `documentLimit = 200`); `src/WorkLog/Dashboard/DashboardState.cs:29` (65,536-byte limit)
- Observation: each budget is bounded separately, but there is no shared byte budget. Using the real key formats:
  - Positions: 100 entries whose keys combine a 64-hex `sourceId`, a change name, a document path and a 64-hex `variant`. About 19.9 KB.
  - Documents: 200 entries of `sourceId:change` → path. About 25.7 KB.
  - Board order: 500 keys mixing `change:<repo>/<name>` and `ticket:<64-hex row id>`. About 34.8 KB.
  - Totals, by `Buffer.byteLength(JSON.stringify(…))`: about 45.9 KB without the board and about 80.6 KB with it. The cap is 65,536 bytes, and it is reached at roughly 280 ranked keys when positions and documents are full.
  - Past the cap, every later `PUT /api/state` fails with 413. The existing single notice appears, and from then on no preference saves, including the theme, range and positions.
  - Separately, keys cut by the cap become unranked, and `byPriority` shows unranked cards first. Truncating "the lowest-ranked entries" therefore moves those cards to the top of their column.
- Proposed improvement and benefit: pick a bound that fits the shared budget. For example, lower `orderLimit`, shorten the ticket keys (a prefix of the row hash), or prune the order by bytes before `persist()`. A spec update may be needed because the spec states 500. Record the budget arithmetic next to the three limits.
- Behavior-preservation risks: a lower cap or a key-format change invalidates saved ranks, and existing keys need a tolerant read. The spec text "at most 500 entries" would change.
- Evidence type: measured result (computed payload sizes from synthetic keys in the real formats). Reaching about 280 live cards in practice is a hypothesis, because pruning keeps only keys that are on the board or are active changes.
- Verification / measurement plan: a Vitest case that fills positions, documents and the order to their limits with realistic key lengths and asserts `JSON.stringify(preferences)` ≤ 65,536 bytes.
- Relationship: none

### ANALYSIS-003: Quiet indication rule lives in the template, not in the board model

- Severity: warning
- Related task / requirement: 2.1, 3.2; "Ongoing board" (an In-progress change card shows the quiet indication); `add-daily-prep` Decision 3
- Locations: `dashboard/src/app.html:399–402`; `dashboard/src/app.ts:621–624` (`changeActivity`); `dashboard/src/board.ts:13–35` (`BoardCard`); `openspec/changes/add-daily-prep/design.md:13,51`
- Observation:
  - The spec rule "an In-progress change card shows quiet when its latest observation is older than 14 days" is implemented as the template expression `card.stage==='in_progress'&&quiet?.kind==='quiet'`, with `quiet = changeActivity(change)`.
  - On every change-detection pass, `changeActivity` re-runs `activityFor` over the whole activity list for each change card, although `card.lastAt` already holds that result. Its `activityReady()` guard is redundant on the board, because change cards exist only once activity is ready (deviation 3).
  - `add-daily-prep` states that `buildBoard` exports the quiet indication and selects "every card in the In progress column that does not show the quiet indication". `BoardCard` has no such fact, so Daily would have to copy the template rule a second time. Doing so contradicts that design's own "would define current work … a second time" rationale.
- Proposed improvement and benefit:
  - Export a pure `quietSince(card, now, zone): string | null` (or `isQuiet`) from `board.ts`, based on `activityOf(card.lastAt, now, zone)` and limited to `kind === "change" && stage === "in_progress"`. Use it in the template and from Daily.
  - This removes the per-card activity rescan, makes the rule unit-testable in `board.spec.ts`, and gives `add-daily-prep` a contract it can rely on.
  - Also have Daily read `App.fullBoard()` instead of calling `buildBoard` again, so the `BoardInput` assembly (scoped catalog including archived changes, the activity readiness gate) stays in one place.
- Behavior-preservation risks: the zone must stay the reporting timezone, as now. The "No recorded activity" chip on non-In-progress cards (deviation 5) must keep its current condition.
- Evidence type: source observation; the per-render cost is a hypothesis (small)
- Verification / measurement plan: Vitest cases for `quietSince` across the in_progress, done and proposed stages and the 14/15-day boundary. The existing `app.board.spec.ts` "shows the quiet indication only on In-progress change cards" keeps passing.
- Relationship: coordination with `add-daily-prep` (the scope decision says this change must keep its exports; this would add one)

### ANALYSIS-004: Orphaned helper and styles from the removed Ongoing UI

- Severity: suggestion
- Related task / requirement: 3.2
- Locations: `dashboard/src/app.ts:625–627` (`latestActivity`, which no longer has a caller in the template or the specs); `dashboard/src/styles.css:887–897` (`.work-list-item strong/small`), `:899–906` (`.work-list-icon`), `:2750–2763` (`.work-item …` rules)
- Observation: the baseline template used these only in "Pick up where you left off" (`latestActivity`) and "Other ongoing work" (`.work-item`, `.work-list-icon`, and the `<strong>`/`<small>` inside `.work-list-item`). The only remaining `.work-list-item` use is the copy chooser (`app.html:814`), which has neither `strong` nor `small`. `.work-list-item` itself, `:first-of-type` and `> span:nth-child(2)` still apply there and must stay.
- Proposed improvement and benefit: delete the listed method and rules. This removes about 30 lines with no behavior change.
- Behavior-preservation risks: none, if the copy-chooser rules are kept.
- Evidence type: source observation (`grep` over `dashboard/src`, compared with the baseline `app.html`)
- Verification / measurement plan: `npm test --prefix dashboard`, then the e2e theme screenshot scenario.
- Relationship: none

### ANALYSIS-005: The unlinked endpoint decodes the whole journal history to return 7 days

- Severity: suggestion
- Related task / requirement: 1.1; design Goals ("Idle refresh cost stays as it is")
- Locations: `src/WorkLog/Dashboard/TimelineService.cs:98–107`; `src/WorkLog/Dashboard/DashboardJournal.cs:92–124`; `dashboard/src/app.ts:720–725`
- Observation:
  - `Unlinked` calls `journal.Read(to, …)`, which runs `SELECT payload … WHERE rowid <= $snapshot AND occurred < $to` with no lower bound, `Json.Decode`s every row, and then keeps only `[from, to)` entries without a change or tickets.
  - The timeline needs full pre-window history for spans, but this query does not.
  - Each journal change therefore adds one more full-history decode. This is on top of the History page and each Ongoing timeline page, which already re-read in full; that part is pre-existing.
  - The call also waits until every timeline continuation page has finished, although it needs only the snapshot from the first page.
- Proposed improvement and benefit: add an optional `from` bound to `DashboardJournal.Read`, or a small sibling method, that adds `AND occurred >= $from AND change_name IS NULL` (`change_name` is a column). The tickets check stays in memory. This makes the cost proportional to the 7-day window. The snapshot semantics are unchanged. On the client, the request could start in parallel with the continuation pages once `query.snapshot` is known.
- Behavior-preservation risks: snapshot validation must stay identical. `change_name IS NULL` must match `Entry.Change is null`, as `SqliteEntryStore.Insert` writes it.
- Evidence type: performance hypothesis (not measured)
- Verification / measurement plan: in a disposable data directory, insert about 50k synthetic entries spread over a year. Time `TimelineService.Unlinked` before and after with a Stopwatch in a throwaway test. The existing `OngoingUnlinkedTests` must pass unchanged.
- Relationship: none

### ANALYSIS-006: Deviation 2 keeps off-selection change ranks but drops off-selection ticket and ad-hoc ranks

- Severity: suggestion (decision needed)
- Related task / requirement: 2.2, 3.3; "Bounded dashboard preferences"; findings.md deviation 2
- Locations: `dashboard/src/app.ts:800–805` (`applyColumn` live set); `dashboard/src/board.ts:128,152` (ticket and ad-hoc key formats)
- Observation: `live` is the set of keys on the current (selection-scoped) board plus every active catalog change. When the user moves a card while the repository selection is narrowed, ranks of ticket and ad-hoc cards in unselected repositories are dropped, while change ranks survive. The cause is structural: change keys carry their repositories, but `ticket:<row id>` and `adhoc:<entry id>` do not, so the client cannot tell "off this selection" from "gone".
- Proposed improvement and benefit: one uniform rule would be simpler to state and test: prune only keys that belong to the current selection and are not on the board. That rule needs the repository in every key (for example `ticket:<repo>/<row id>`, `adhoc:<repo>/<entry id>`). The spec already identifies cards by work identity and observation, which this preserves. Alternatively, accept the asymmetry and state it in the spec, since ticket and ad-hoc cards expire within 7 days anyway. Pair the decision with the user's pending decision on deviation 2.
- Behavior-preservation risks: a key-format change orphans existing saved ticket and ad-hoc ranks. They are short-lived, so the loss is small, and pruning cleans them up.
- Evidence type: source observation
- Verification / measurement plan: an `app.board.spec.ts` case that ranks a ticket card under "all repositories", narrows the selection, moves another card, and asserts the expected survival rule.
- Relationship: deviation 2 (orchestrator-owned decision)

### ANALYSIS-007: A third ticket identity key with a weaker separator

- Severity: suggestion
- Related task / requirement: 2.1
- Locations: `dashboard/src/board.ts:63–65,87–90`; `dashboard/src/workitem-markdown.ts:38–39`; compare with `dashboard/src/links.ts:116–118`
- Observation: `board.ts` dedupes a change card's tickets by `provider|scope|id`. `workitem-markdown.ts` dedupes the same `Ticket` triple with `\u0000` separators. A scope or ID containing `|` could merge distinct tickets on the board but not in Markdown.
- Proposed improvement and benefit: export one `uniqueTickets(tickets)` (or `ticketIdentity`) from `links.ts` and use it in both places. The rule then exists once.
- Behavior-preservation risks: none for realistic data.
- Evidence type: source observation
- Verification / measurement plan: the existing board and Markdown specs pass. Add one case with a `|` in the scope.
- Relationship: none

### ANALYSIS-008: `dragOver` writes a new drop-mark object on every event

- Severity: suggestion
- Related task / requirement: 3.3
- Locations: `dashboard/src/app.ts:775–785`
- Observation: browsers fire `dragover` continuously, every few tens of milliseconds, while the pointer is over a card. Each event `set`s a fresh `{ key, after }` object, and signals compare by identity, so every event marks the template dirty and re-runs per-card template calls such as `changeActivity`, `formatDate` (a new `Intl.DateTimeFormat`) and `updatedDocs`.
- Proposed improvement and benefit: set the signal only when `key` or `after` actually changes, or give the signal an `equal` function. This removes redundant change detection during a drag.
- Behavior-preservation risks: none.
- Evidence type: performance hypothesis (not measured; drag was not exercised in a real browser, as deviation 11 notes)
- Verification / measurement plan: a DevTools performance trace of a 3-second drag over a column on the e2e fixture, before and after.
- Relationship: none

### ANALYSIS-009: Architecture reference does not mention the new endpoint

- Severity: suggestion
- Related task / requirement: 1.1, 4.2
- Locations: `docs/architecture.md:23` (lists `POST /api/activity/changes`)
- Observation: the architecture reference enumerates the read-only journal endpoints next to `DashboardJournal`, but it does not mention `POST /api/ongoing/unlinked` or its 200-entry cap.
- Proposed improvement and benefit: add one sentence next to the `/api/activity/changes` mention. This keeps the reference that drift reviews are judged against accurate.
- Behavior-preservation risks: none (documentation only).
- Evidence type: source observation
- Verification / measurement plan: review the doc text.
- Relationship: none

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | n/a | n/a | not performed: this is the first analysis round |

## Assessment and limitations

Answers to the assignment questions:

- **`board.ts` purity and duplication.** `board.ts` is pure and deterministic. It reuses `activityFor` and does not duplicate `quiet.ts` or `links.ts` logic. The quiet rule, though, sits in the template (ANALYSIS-003), and the ticket dedupe key is a third copy (ANALYSIS-007).
- **Order, move and prune algorithm.** It is correct and compact for unique keys, and it implements deviation 2 as described. It fails for duplicate keys, which the fixture already produces (ANALYSIS-001). The 500 cap is not consistent with the 64 KiB preference budget (ANALYSIS-002). Deviation 2 is asymmetric between card kinds (ANALYSIS-006).
- **Endpoint reuse.** The endpoint reuses the existing authorization middleware, error mapping and `DashboardJournal.Read` snapshot path. It does not copy them. Its read is unbounded below (ANALYSIS-005).
- **Cost of the extra request.** It adds no polling. It costs one request and one full-journal decode per journal change, which is acceptable on the client. The server side is a hypothesis.
- **Recomputation.** The computeds are not recomputed per card. `fullBoard` recomputes on every visible 2 s poll at a measured 0.2–2.7 ms. Per-card template calls re-scan activity (ANALYSIS-003), and drag events re-render redundantly (ANALYSIS-008).
- **Dead code.** It is mostly gone. ANALYSIS-004 lists what remains.
- **API for `add-daily-prep`.** `buildBoard` and `byPriority` are usable as `add-daily-prep` intends, except that the quiet indication it relies on is not part of the model (ANALYSIS-003).
- **Parity.** It does not apply. No MCP, init, integration or launcher path changed.

Measurements and checks:

- I timed `buildBoard`, probed duplicate keys, and estimated the preference size. All three ran as Node scripts in a disposable scratch directory outside the repository, against `board.ts` bundled with the repository's esbuild.
- `npx vitest run src/board.spec.ts` passed 34/34.
- Not performed:
  - the full `npm test`, `dotnet test`, e2e and formatting runs (QA owns functional validation)
  - the server read-cost measurement for ANALYSIS-005
  - a drag trace for ANALYSIS-008
  - a live NG0955 observation for ANALYSIS-001

No repository file was written. The one attempt to write this report was refused by the client, and the target identity is unchanged.

Decisions needed (orchestrator or user):

- whether copies of one change across sources form one card (ANALYSIS-001)
- the order bound or key length that fits 64 KiB, which may need a spec text change (ANALYSIS-002)
- the deviation 2 pruning rule (ANALYSIS-006)
- whether to add a quiet helper to the `board.ts` contract that `add-daily-prep` consumes (ANALYSIS-003)
