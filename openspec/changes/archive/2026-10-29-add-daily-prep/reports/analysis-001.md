<!-- Saved by the orchestrator. Reviewer: independent code-analyzer subagent (assignment A3). Client: Claude Code 2.1.312, subagent mode, auto permission mode. Model: claude-opus-5-5. Case: the client refused the reviewer's write; exact refusal message: "Subagents should return findings as text, not write report files. Include this content in your final response instead." The text below is the reviewer's returned report, verbatim. -->

# Analysis report: add-daily-prep / round 1 (A3)

## Review target

- Reviewer / role: independent code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 (`claude --version`), subagent spawned by the orchestrator, auto permission mode, model `claude-opus-5-5` (role default)
- Date: 2026-10-29T09:05Z
- Change root / assignment: `openspec/changes/add-daily-prep`, assignment A3
- Task and requirement references: tasks 1.1–3.2 ([tasks.md](../tasks.md)); [design.md](../design.md) Decisions 1–5; [spec.md](../specs/daily-preparation/spec.md) requirements "Last recorded day", "Concise yesterday digest", "Today and blockers", "Copy for daily", "Shared scope and responsiveness"; developer deviations 1–9 in [findings.md](findings.md) "Scope decisions"
- Scope and relevant neighboring paths: `src/WorkLog/Dashboard/{DailyService.cs,DashboardJournal.cs,DashboardHost.cs,TimelineService.cs}`, `tests/WorkLog.Tests/Dashboard/DailyServiceTests.cs`, `tools/WorkLog.Smoke/{DashboardFixture.cs,Program.cs}`, `dashboard/src/{daily.ts,daily.spec.ts,app.daily.spec.ts,app.ts,app.html,main.ts,models.ts,styles.css}`, `dashboard/e2e/dashboard.spec.ts`, `docs/architecture.md`. Neighbors read for context: `dashboard/src/board.ts`, `dashboard/src/api.ts` (`boundary`, `localDate`, `humanize`), `src/WorkLog/Reporting/{ReportingPeriod.cs,WorkIdentity.cs,DailyGroup.cs}`, `src/WorkLog/Journaling/TimestampParser.cs`, `src/WorkLog/Infrastructure/ContentHash.cs`, `src/WorkLog/Persistence/SqliteEntryStore.cs` (the insert that writes `occurred`), `dashboard/package.json`
- Reading limits and paths read outside scope: `reports/qa-001.md` was not read. Outside the listed scope I read only the neighbors named above.
- Baseline commit: `cd1c0e3`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline cd1c0e3 -Paths src/WorkLog tests tools/WorkLog.Smoke dashboard/src dashboard/e2e README.md docs/dashboard-validation.md docs/architecture.md` → HEAD `cd1c0e3395a71ee37c70d76ac117180200803597`. Manifest: 5 untracked (`dashboard/src/app.daily.spec.ts`, `dashboard/src/daily.spec.ts`, `dashboard/src/daily.ts`, `src/WorkLog/Dashboard/DailyService.cs`, `tests/WorkLog.Tests/Dashboard/DailyServiceTests.cs`) and 14 modified. Tracked diff SHA-256 `e68f3c756a4e27b3c69a4af16ca7732eed2eadf770f464d3c7521697e6c108a0`. `Summary: head=cd1c0e3 baseline=cd1c0e3 files=19 diff=e68f3c756a4e untracked=5/5c2041d10040`, which matches the assignment.
- Before/after identity: after the analysis the same command printed `Summary: head=cd1c0e3 baseline=cd1c0e3 files=19 diff=e68f3c756a4e untracked=5/5c2041d10040`, identical to before. No unexpected mutation.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Shared definition of current work (design goal) | Read `daily.ts` against `board.ts` | `dailyToday` (`dashboard/src/daily.ts:36-52`) takes the built board and reuses `isQuiet` and `byPriority`. The view builds that board with `fullBoard()` (`dashboard/src/app.ts:305-310`). No rule for columns, expiry, Done, or order is defined a second time. | The goal is met for which cards are listed and in what order. The matching of lines to cards has a correctness gap (ANALYSIS-001). |
| Card-to-today-line matching | Traced `DailyItem.changes` back to `TimelineService.Project` | Ticket cards match on `row.id`, which is exact (both sides are `IdFor(e, "ticket")`). Change cards match on `item.changes`, which is the identity's change set over its whole history (`TimelineService.cs:55`, copied at `DailyService.cs:80`). The test at `daily.spec.ts:104-111` expects an item that spans two changes to win. | ANALYSIS-001 |
| Reuse of `TimelineService.Project` | Inspection | One read of entries before the start of tomorrow (`DailyService.cs:36`) serves both projections, and the yesterday slice is filtered in memory (`:44`). Unlinked entries are kept out of `Project` and become one item each (`:71-83`). That predicate matches the empty-key bucket of `WorkIdentity.For` (`WorkIdentity.cs:9-18`). `byIdentity[row.Id]` cannot miss, because `Project` and `inPeriod` use the same bounds. | Correct. The extra lookups are redundant (ANALYSIS-002); cost is covered in ANALYSIS-007. |
| Duplication (server) | Inspection | The new `RepositoryClause` (`DashboardJournal.cs:92-102`) is used only by `Occurred`. `Read` (`:138-148`) and `Activity` (`:65-75`) keep their own inline copies. The unlinked predicate is written twice (`DailyService.cs:71`, `TimelineService.cs:104`). | ANALYSIS-003, ANALYSIS-006 |
| Duplication (client) | Inspection | `noSummary` is defined twice (`board.ts:60`, `daily.ts:6`). The ad-hoc title rule appears four times (`board.ts:177`, `DailyService.cs:83`, `app.ts:765-768`, `daily.ts:86`). The Yesterday title rule differs from the one the board and History use. | ANALYSIS-006 |
| Endpoint consistency | Compared `DailyRequest` with `TimelineQuery` and `UnlinkedQuery`, including validation and errors | Errors are `ArgumentException`, which maps to 400 as for the other endpoints (`DashboardHost.cs:123`; test `DailyServiceTests.cs:338-378`). The timezone is validated through a made-up `QueryRequest` (`DailyService.cs:21`). There is no `snapshot` field, and `before` is assumed to be a local midnight without a check. | ANALYSIS-004, ANALYSIS-005 |
| `dataLoaded` race fix | Traced `start()` and `routeChanged()` | `dataLoaded` is set after the opening route is applied and before the first `refreshData()` (`app.ts:433-440`). `routeChanged` loads only once the flag is set and the day or view has changed (`app.ts:1104-1112`). `loadDaily` is guarded by an epoch counter (`app.ts:707-737`). The opening route is loaded exactly once, by `refreshData` (`app.ts:703-705`). | No finding. The fix is sound. |
| Architecture | Compared with `docs/architecture.md:23,25` | `DailyService` sits in `Dashboard` on top of the read-only `DashboardJournal`. The doc names `POST /api/daily` and says the client decides what Today lists. "From one journal read" is true for the entries, but three small extra queries follow (ANALYSIS-002). | No drift |
| Day boundaries | Compared `DailyService.Midnight` (`:56-63`) with the client's `boundary` (`api.ts:47-89`) | Both handle days whose midnight is skipped by a clock change. The server computes every boundary except `before`, which it takes from the client. | ANALYSIS-004 |
| Performance | Source observation only | Requests fired by one refresh on `/daily`, plus a SHA-256 hash per entry in each projection | ANALYSIS-007 (hypothesis, with a measurement plan) |
| Formatting check | `dotnet format ... --verify-no-changes` | not run | Skipped so it could not compete with QA's concurrent build for `obj/` and `artifacts/` outputs |
| Unit tests | `npm test --prefix dashboard`, `dotnet test` | not run | QA (A2) owns functional checks and is running concurrently. The developer's results are in findings deviation 9. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Client poll (`app.ts:443`, every 2 s), `poll()` (`app.ts:1974-2026`) | source observation | While idle, one `GET /api/catalog` every 2 s, as before this change. The Daily view adds nothing while `journalSnapshot` is unchanged and the date has not rolled over. On a journal change or a rollover with `/daily` open: `loadActivity` (one SQL window query, no JSON decoding), `loadHistory` (one `/api/timeline`, decoding the full journal up to `to`), `loadOngoing` (one or more `/api/timeline` pages plus `/api/ongoing/unlinked`, each decoding the full journal), and `loadDaily` (one `/api/daily`: the full journal up to the end of today, two `Project` passes, and 3 small queries on 3 extra connections). No processes are spawned. The inputs change only when an agent logs, so the cost follows how often agents log, not the poll interval. |
| Cache lifetime versus interval and refresh duration | `activityKey` (`app.ts:541`), `dailyData` signal | source observation | The server keeps no daily cache. The client refreshes only when `journalSnapshot` changes, so the poll causes no repeated work. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `dailyData`, `preferences.daily`, the `/api/daily` response | source observation | `preferences.daily` is two scalar fields inside the existing 64 KiB preference limit. `dailyData` is replaced on each load. The response includes every row (`pageSize = int.MaxValue`, `DailyService.cs:73`) and one item per unlinked entry. That is bounded by the entries of one local day (the selected day plus today), but there is no count cap like the 200 on `/api/ongoing/unlinked` (`TimelineService.cs:92`). This is acceptable for one day. |
| Repeated full reads where a change check would do | `/api/daily` | source observation | Each call decodes every entry before the end of today. The client calls it only after a change check (new snapshot or rollover), so no read is wasted on an unchanged journal. Within one call, three SQL lookups ask again for data the decoded entries already contain (ANALYSIS-002). |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Server: SQLite only (`DefaultTimeout = 5`, `DashboardJournal.cs:19`). e2e: `spawnSync("dotnet", ...)` for `--dashboard-log` (`dashboard/e2e/dashboard.spec.ts`, third Daily test) | source observation | The product spawns no external tools. The test-only `dotnet` call is found through PATH and has no timeout of its own; the Playwright test timeout bounds it. |

## Findings

### ANALYSIS-001: A change card's Today line can show another change's work and can drop observations recorded today

- Severity: warning
- Related task / requirement: 2.1; design Decision 3; spec "Today and blockers" (the latest summary from today, with today's other observations collapsed) and "Copy for daily" (each copied line holds only recorded facts from that line's day)
- Locations: `dashboard/src/daily.ts:21-30` (line 26), `src/WorkLog/Dashboard/DailyService.cs:80`, `src/WorkLog/Dashboard/TimelineService.cs:55`, `dashboard/src/daily.spec.ts:104-111`
- Observation: for a change card, `lineFor` picks the `today` item whose `changes` include `card.title`. `DailyItem.Changes` is `TimelineRow.Changes`, which `Project` computes over the identity's whole history (`all`), not over today's entries. A ticket identity is keyed by the ticket alone (`WorkIdentity.cs:9-18`), so its `changes` list every change the ticket was ever logged with.
  1. Wrong attribution. Ticket DEP-7 was logged with active change `alpha` last week and is logged today with active change `beta`. The single DEP-7 item has `changes = [alpha, beta]`. `alpha` is an In-progress card that is not quiet, so Today lists it, and its line is DEP-7's item for today. The screen and the copied text then show `alpha — in progress — <summary of the beta work>`, which is not a recorded fact about `alpha` today. The `beta` card shows the same line. The unit test at `daily.spec.ts:104-111` gives the winning item `changes: ["export-retries", "other"]` and never checks that `primary.change` is the card's change.
  2. Missing observations. A change's entries today can be split between a ticket identity (entries logged with a ticket) and the change-only identity (entries logged without one). Only the item with the latest entry is used, so the other item's entries are missing from "N other observations today". The spec requires those observations to be collapsed and available.
- Proposed improvement and benefit: build a change card's line from today's entries that belong to that change, instead of from the item's history. On the client: gather `[item.primary, ...item.others]` from items whose `repository` is in `card.repositories`, and keep the entries with `entry.change === card.title`. The latest becomes `primary` (its status is the line's status) and the rest become `others`. Each of today's entries belongs to exactly one item, so nothing is counted twice. Ticket cards and ad-hoc cards are unchanged. An alternative is a server-side `today` projection grouped by change, but that adds a third `Project` pass. Benefit: Today lines and copied text contain only facts about the card's own change, and the collapsed observations the spec requires are complete.
- Behavior-preservation risks: in the normal case of one ticket per change, the result is identical: same primary, same status, same others. Tickets on the line currently come from `line.tickets` (`app.html:541`, `daily.ts:95`). With a built line they need to come from the chosen entries or from `card.tickets`.
- Evidence type: source observation
- Verification / measurement plan: add Vitest cases to `daily.spec.ts`. (a) An item with `changes: ["alpha", "beta"]` whose entries today all have `change: "beta"` gives the `alpha` card a null line. (b) Today's entries for one change, split across a ticket item and a change-only item, give one line whose `others` holds both earlier entries. (c) The existing cases still pass. Optionally add an e2e step that logs the fixture's DEMO-2 with a second change.
- Relationship: none

### ANALYSIS-002: The three recorded-day lookups query the database again for data the service has already loaded

- Severity: suggestion
- Related task / requirement: 1.1, 1.2; design Decision 1
- Locations: `src/WorkLog/Dashboard/DailyService.cs:36,39,45-46`; `src/WorkLog/Dashboard/DashboardJournal.cs:14-32,92-126`; `tests/WorkLog.Tests/Dashboard/DailyServiceTests.cs:306-323`
- Observation: the design planned indexed `MAX`/`MIN` queries plus a read of entries before the start of the day after `D`. The implementation instead reads every entry before the start of tomorrow, once (line 36). That set already contains the answers to `LatestBefore(before)`, `LatestBefore(start)`, and `EarliestBetween(end, before)`, with the same repository filter and snapshot. `Read` sorts by `occurred` and returns `data.Snapshot`, and `occurred` is written from `entry.OccurredAt.UtcTicks` (`SqliteEntryStore.cs:134-136`), so the values agree. Each lookup opens another read-only connection and runs `PRAGMA user_version` again (`Open`), so one request opens 4 connections where 1 would do. `Occurred`, `RepositoryClause`, and the two public lookups (about 35 lines) exist only for this.
- Proposed improvement and benefit: take the three instants from `data.Entries`, which is already sorted (for example `LastOrDefault(e => e.OccurredAt < x)` and `FirstOrDefault(e => e.OccurredAt >= end && e.OccurredAt < before)`). Then delete `Occurred`, `LatestBefore`, and `EarliestBetween`. Benefit: about 35 fewer lines, 3 fewer connections opened per request, and the lookups can no longer disagree with the loaded snapshot. The orchestrator needs to decide, because design Decision 1 names the SQL lookups explicitly.
- Behavior-preservation risks: the instants are the same under the same snapshot and filter. The lookup unit test (`DailyServiceTests.cs:306-323`) would become assertions on the service. The existing service tests (Monday after Friday, days off, the timezone boundary, the repository filter, an empty journal) already cover the behavior.
- Evidence type: source observation (the saving is a hypothesis and was not measured)
- Verification / measurement plan: run `DailyServiceTests` unchanged except for the lookup test. Optionally count `SqliteConnection.Open` calls per request before and after.
- Relationship: if the lookups stay, see ANALYSIS-003.

### ANALYSIS-003: `RepositoryClause` was added, but `Read` and `Activity` keep their own copies

- Severity: suggestion
- Related task / requirement: 1.1
- Locations: `src/WorkLog/Dashboard/DashboardJournal.cs:65-75` (`Activity`), `:92-102` (the new helper), `:138-148` (`Read`)
- Observation: the same parameterized `repository IN (...)` builder now appears three times in one class, and the new helper is used only by `Occurred`.
- Proposed improvement and benefit: have `Read` and `Activity` call `RepositoryClause(command, repositories)`. The repository filter for every dashboard read then lives in one place.
- Behavior-preservation risks: none expected. All three versions remove duplicates with `StringComparer.Ordinal` and add no clause when no repositories are given. The parameter names (`$r0`, ...) do not collide with `$snapshot` or `$to`.
- Evidence type: source observation
- Verification / measurement plan: run the existing `DashboardJournal`, timeline, activity, and daily tests.
- Relationship: independent of ANALYSIS-002. If ANALYSIS-002 removes `Occurred`, the helper is still worth keeping for `Read` and `Activity`.

### ANALYSIS-004: `/api/daily` assumes `before` is a local midnight but computes every other boundary itself

- Severity: suggestion
- Related task / requirement: 1.2; spec "Last recorded day" (it SHALL NOT move past the day before the current date)
- Locations: `src/WorkLog/Dashboard/DailyService.cs:19-24,30,37,39`; client `dashboard/src/api.ts:47-89`, `dashboard/src/app.ts:711`
- Observation: the server derives `currentDate` from `before` and computes `tomorrow`, `start`, and `end` with its own `Midnight`. Today's window, though, starts at `before`, and the default day search looks for entries earlier than `before`. If `before` is not the start of its local day (say 10:00 local), the default path picks today as "yesterday", because an entry at 09:00 is earlier than `before`. Line 30 rejects today only when the day is given explicitly. Today's section then also leaves out that morning. The bundled client always sends `boundary(...)`, so this happens only with a different caller, or if client and server disagree at a midnight edge case. For example, when midnight occurs twice because of a clock change, the server takes the earlier one (`:61`), and the client's iteration can land on either. Two definitions of local midnight (the client's `boundary` and the server's `Midnight`) feed one window.
- Proposed improvement and benefit: after validating the timezone, set `before = Midnight(LocalDate(before, zone), zone)` once, or reject a `before` that differs from that value. Every window then uses the server's single definition, and the rule "never past the day before today" holds for any caller.
- Behavior-preservation risks: none for the bundled client, which already sends that value (the test `TodayStartsAtTheLocalMidnightThatBeforeNames` still passes). The e2e fake clock is unaffected, because the date the client sends is kept.
- Evidence type: source observation
- Verification / measurement plan: an xUnit case with `before` at 10:00 local and entries at 09:00 today and on the previous day. Expect `day` to be the previous day and `today` to include the 09:00 entry.
- Relationship: none

### ANALYSIS-005: Endpoint consistency: a made-up period for timezone validation, and no snapshot pinning

- Severity: suggestion
- Related task / requirement: 1.2, 2.2; design Decision 1
- Locations: `src/WorkLog/Dashboard/DailyService.cs:9,21,36`; `src/WorkLog/Reporting/ReportingPeriod.cs:16-28`; `src/WorkLog/Dashboard/TimelineService.cs:8,17`; `dashboard/src/app.ts:305-310,703-705,827,2015-2019`
- Observation:
  1. `DailyService` builds a `QueryRequest` with an invented `to` (`before` plus one day) only to reuse the timezone check in `ReportingPeriod.FromRequest`. Its own comment calls that period "only a vehicle". If the made-up period fails validation, the error names `period.from` or `period.to`, fields the caller never sent.
  2. `TimelineQuery` and `UnlinkedQuery` accept `snapshot`, and `loadOngoing` pins its two reads to one snapshot (`app.ts:827`). `DailyRequest` has no snapshot, so Today combines the board inputs (rows, unlinked entries, activity) with `digest.today`, which can come from up to three different journal states. For a moment a card can be listed with no line, or a line can exist for a card that is not yet listed. Every request reads at or after the catalog's snapshot, so the next poll sees a newer `journalSnapshot` and refreshes. The mismatch therefore clears within about one poll interval.
- Proposed improvement and benefit: (1) Move the timezone check into `ReportingPeriod.ResolveZone(string?)`, called from `FromRequest` and from `DailyService`. Validation stays shared without an invented period. (2) Either accept and document the short mismatch (recommended, since it clears itself), or add an optional `Snapshot` to `DailyRequest` and pass it to `journal.Read`. The second option makes `loadDaily` wait for `loadOngoing`, which adds latency and works against the "fast" goal.
- Behavior-preservation risks: (1) the error text for a bad timezone stays the same if the helper keeps the same messages. (2) No change if the mismatch is accepted.
- Evidence type: source observation
- Verification / measurement plan: (1) the existing invalid-timezone tests for timeline and daily. (2) Not applicable unless the snapshot option is chosen.
- Relationship: none

### ANALYSIS-006: The unlinked and ad-hoc rules and the "no summary" label are repeated, and the Yesterday title rule differs from the rest

- Severity: suggestion
- Related task / requirement: 1.2, 2.1, 2.2; the design goal of one shared definition
- Locations: unlinked predicate in `src/WorkLog/Dashboard/DailyService.cs:71` and `src/WorkLog/Dashboard/TimelineService.cs:104` (and on the client, `dashboard/src/board.ts:172`); label in `dashboard/src/board.ts:60` and `dashboard/src/daily.ts:6`; ad-hoc title in `dashboard/src/board.ts:177`, `src/WorkLog/Dashboard/DailyService.cs:83`, `dashboard/src/app.ts:765-768`, and `dashboard/src/daily.ts:86`; title display in `dashboard/src/app.html:511` (Yesterday, via `dailyTitle`) and `:540` (Today), compared with `:433` (board) and `:765` (History)
- Observation: the server has two copies of the "no change and no tickets" check, and both must keep matching the empty bucket of `WorkIdentity.For`. The client has two identical `"No summary recorded"` constants. `dailyTitle` shows ticket titles without `humanize` (`item.tickets.length ? item.title : humanize(item.title)`), while the Today line, the board, and History all call `humanize(title)`. `humanize` leaves only uppercase IDs like `KEY-123` unchanged (`api.ts:91-96`). A ticket ID outside that pattern is therefore shown differently under Yesterday than under Today for the same item.
- Proposed improvement and benefit: add one `internal static bool IsUnlinked(Entry)` next to `TimelineService.IdFor`, and use it in `Unlinked` and `DailyService`. Export the label from `board.ts` and import it in `daily.ts`. Keep the ad-hoc branch of `dailyTitle`, and otherwise call `humanize(item.title)` as every other view does. Benefit: the identity rules cannot drift apart, and an item has the same title in both sections.
- Behavior-preservation risks: titles change only for ticket IDs outside the `humanize` pattern, which then match the board. Copied text is unaffected, because copying uses raw titles (deviation 2).
- Evidence type: source observation
- Verification / measurement plan: the existing unit tests, plus one assertion in `app.daily.spec.ts` that a Yesterday line and a Today line for the same ticket show the same title.
- Relationship: none

### ANALYSIS-007: Cost of each refresh on `/daily` (performance hypothesis)

- Severity: suggestion
- Related task / requirement: 1.2, 2.2; design Risks ("same order of cost as a History day")
- Locations: `src/WorkLog/Dashboard/DailyService.cs:36-46,69-86`; `src/WorkLog/Dashboard/TimelineService.cs:22,46,84-89`; `dashboard/src/app.ts:703-705,2015-2019`
- Observation: each `/api/daily` call decodes the JSON of every journal entry before the end of today, then runs `Project` twice over the linked history. Each pass groups by `IdFor`, which hashes every entry with SHA-256 (`ContentHash.cs:8-9`), and `byIdentity` hashes the day's entries again. Each pass also computes insights (`TimelineService.cs:86-89`) that Daily throws away. On `/daily`, one journal change triggers this on top of the full decodes already done by `/api/timeline` (History and Ongoing pages) and `/api/ongoing/unlinked`. Hypothesis: Daily adds roughly one History-sized query per journal change, as the design estimates. That matters only for large journals while agents are logging actively. A related note outside this change: `refreshData` also reloads History while History is hidden, which was already the case at the baseline.
- Proposed improvement and benefit: no change unless a measurement shows the cost is significant. If it is, the cheapest step is to compute each entry's identity once per request and share it between the two passes (a `Project` overload that accepts precomputed IDs), or to skip insights when the caller discards them.
- Behavior-preservation risks: not applicable unless implemented.
- Evidence type: performance hypothesis (not measured)
- Verification / measurement plan: create synthetic journals of 10k and 50k entries in a disposable `--data-dir` under `artifacts/`. Start an isolated dashboard with `--port <free> --no-open` and a time limit. Time 20 runs each of `POST /api/daily` and `POST /api/timeline` (7 days, ticket grouping) and compare the medians. Act only if `/api/daily` takes more than about twice the timeline median, or more than 100 ms at 50k entries.
- Relationship: ANALYSIS-002 removes the 3 extra connection opens from the same request.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | not applicable | No recheck was assigned | not performed |

## Assessment and limitations

I analyzed all tasks 1.1–3.2 at the recorded identity. The design's central goal holds for which cards Today lists and in what order: Today comes from `buildBoard`, `isQuiet`, and `byPriority`, and no board rule is written a second time. The `dataLoaded` race fix is sound, and the endpoint's errors follow the existing 400 mapping.

The one correctness risk is ANALYSIS-001. Change cards are matched to Today lines using every change in an item's history, so a card can show another change's work and lose some collapsed observations. I recommend fixing it before archive. ANALYSIS-002 through ANALYSIS-006 are small simplification and consistency fixes. ANALYSIS-002 replaces a mechanism the design names, so it needs an orchestrator decision. ANALYSIS-007 is an unmeasured hypothesis with a measurement plan.

Not performed: unit, e2e, and formatting runs (QA owns functional checks and is building concurrently, and a format check could compete for build outputs), and performance measurements. I wrote no files; the report write was refused. I read developer deviations 1–9; deviations 1, 2, 4, 6, and 8 match the code, and deviation 8's `int.MaxValue` page size is covered under the steady-state bounds. Claude Code/Codex parity does not apply, because this is a dashboard-only change.
