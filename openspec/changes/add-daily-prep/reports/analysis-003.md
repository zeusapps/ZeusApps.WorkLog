<!-- Saved by the orchestrator. Reviewer: independent code-analyzer subagent (assignment A8). Client: Claude Code 2.1.312, delegated subagent, auto mode. Model: claude-opus-5-5. Case: the client refused the reviewer's write. The reviewer paraphrased the refusal ("it said subagents should return findings as text") and did not quote the exact message; the same client refused the A3 and A6 writes with "Subagents should return findings as text, not write report files. Include this content in your final response instead." The text below is the reviewer's returned report, verbatim. -->

# Analysis report: add-daily-prep / round 3 (A8 recheck)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312, delegated subagent (Agent SDK), auto mode; model `claude-opus-5-5`
- Date: 2026-10-29T09:46:30Z
- Change root / assignment: `openspec/changes/add-daily-prep`; assignment A8 (code-analyzer recheck of the A7 correction)
- Task and requirement references: task 2.1; [design Decision 3](../design.md) (updated wording); [spec "Today and blockers"](../specs/daily-preparation/spec.md) and "Copy for daily"
- Scope and relevant neighboring paths: the A7 diff, which touches `dashboard/src/daily.ts` (`qualifies` and `lineFor`, with a local `ticketKey`), `dashboard/src/daily.spec.ts` (two new cases), and `docs/dashboard-validation.md` (Vitest count). Neighboring paths read for context: `dashboard/src/board.ts` (`ticketKey`, `changeCards`, `ticketCards`), `dashboard/src/models.ts` (`Ticket`), `src/WorkLog/Dashboard/TimelineService.cs` (row grouping) and `src/WorkLog/Reporting/WorkIdentity.cs` (ticket identity).
- Reading limits and paths read outside scope: I did not read `reports/qa-003.md`. I read no paths outside the scope and neighbors listed above.
- Baseline commit: `cd1c0e3`
- Implementation identity (before): HEAD `cd1c0e3395a71ee37c70d76ac117180200803597`. The manifest lists 14 modified files (README.md, dashboard/e2e/dashboard.spec.ts, dashboard/src/app.html, app.ts, main.ts, models.ts, styles.css, docs/architecture.md, docs/dashboard-validation.md, src/WorkLog/Dashboard/DashboardHost.cs, DashboardJournal.cs, TimelineService.cs, tools/WorkLog.Smoke/DashboardFixture.cs, Program.cs) and 5 untracked files (dashboard/src/app.daily.spec.ts, daily.spec.ts, daily.ts, src/WorkLog/Dashboard/DailyService.cs, tests/WorkLog.Tests/Dashboard/DailyServiceTests.cs). Tracked diff SHA-256 `a56cce33925bdb384c1921bfeccc8d73a4b8d629610a6d61b604326f03104f3d`. The untracked hash for `daily.ts` is `da26f4b9…`, and for `daily.spec.ts` it is `637303a1…`. `Summary: head=cd1c0e3 baseline=cd1c0e3 files=19 diff=a56cce33925b untracked=5/951d6715a50a`, which matches the expected identity.
- Before/after identity: after the analysis the summary was `Summary: head=cd1c0e3 baseline=cd1c0e3 files=19 diff=a56cce33925b untracked=5/951d6715a50a`. It is identical to the before identity, so nothing changed unexpectedly.
- Prior reports / rechecked findings: [analysis-002.md](analysis-002.md) (ANALYSIS-041)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-041 rule | Read `daily.ts:20-65` | `qualifies(entry, card)` returns true when `entry.change === card.title`. It returns false for any other non-empty `change`. Otherwise it returns true only when one of the entry's tickets matches a ticket in `card.tickets` by `provider + "\|" + scope + "\|" + id`. `lineFor` applies it after the repository filter, then sorts newest first. This matches the updated design Decision 3 bullet: "whose `change` is empty and which carry one of `card.tickets` (same provider, scope, and ID); entries of another change never qualify". | Matches the design and the spec wording ("that card's own ticket or change") |
| Verification case | Read `daily.spec.ts:142-158`; ran `npx vitest run src/daily.spec.ts` from `dashboard/` | The case sets up `alpha` with its last observation two days ago, a DEP-7 row with `changes: ["alpha"]`, and a DEP-7 item whose primary has `change: null` and the summary "Fixed DEP-7 regression". It asserts that Today lists only `alpha`, that the line's primary is `f1`, that its status is `in_progress`, and that `dailyText` contains the summary. This is exactly the plan in ANALYSIS-041. Vitest: 22 of 22 passed (20 in round 2, plus the 2 new cases). | Passes |
| Exclusions | Read `daily.spec.ts:160-176` | The case uses three items: DEP-7 with provider `github`, DEP-7 with scope `elsewhere`, and DEP-7 with `change: "beta"`. It asserts that `alpha`'s line is null. If any one of the three qualified, the line would not be null, so the single assertion covers each exclusion separately. | Passes |
| ANALYSIS-001 cases (a) and (b) | Read `daily.spec.ts:117-140`; same Vitest run | Case (a), "item spans the change only through history", still yields a null line: its entries carry `change: "beta"`, and `qualifies` returns false for any other non-empty change. Case (b), "split across a ticket item and a change-only item", still yields `t1` / `done` / `["c1","t0"]` / `[dep12]`. | Both still pass |
| `ticketKey` duplication | Compared `daily.ts:20` with `board.ts:70-72`, and both with `WorkIdentity.cs:14` | The expression is character-for-character the same as the board's private `ticketKey`. The two cannot drift today, but they are separate copies, so a future change to the board's ticket identity (for example trimming, which the server's `WorkIdentity` does) would not reach `daily.ts`. Then a change card could show a ticket that its Today line no longer matches. The bounded fix is to export `ticketKey` from `board.ts` and import it in `daily.ts`: one line removed, no behavior change. | Suggestion only; it does not affect acceptance. Not raised as a finding, because a recheck raises no new suggestions (ANALYSIS-061 is free if the orchestrator wants it in the ledger). |
| Regression: one entry on two Today lines | Traced `ticketCards` (`board.ts:135-167`) and `WorkIdentity.For` (an entry with several tickets gets its own `tickets/…` identity per repository). Ran a throwaway Vitest probe in the session scratch directory, outside the repository, that imports `buildBoard` and `dailyToday` at the reviewed identity. | Setup: one repository, `alpha` In progress, a DEP-7 row with `changes: ["alpha"]`, and a "DEP-7 + DEP-8" row with no change, which becomes a ticket card. A change-less entry carrying [DEP-7, DEP-8] is logged today. Result: the entry is the primary of both the "DEP-7 + DEP-8" ticket card's line and `alpha`'s line, so it is listed and copied twice. `alpha`'s line tickets also include DEP-8 (from `distinctTickets`). The same happens for a change spanning two repositories when the ticket's row in the other repository has no change history. | Not raised. The behavior follows the design Decision 3 wording, which ANALYSIS-041 option 1 anticipated ("its change-less entry appears on both"). The spec requires "each card once", not each entry once, and every copied fact is still a recorded fact. It does not block acceptance or safety. Recorded for the orchestrator. |
| Complexity / performance | Read `qualifies` | `qualifies` builds `new Set(card.tickets.map(ticketKey))` once per entry instead of once per card. The cost is the number of cards times today's entries times the card's tickets, and all three are bounded by one day and the board. | Hypothesis, not measured: negligible. Not raised. |
| Docs | Read the `docs/dashboard-validation.md` diff | It records `npm test --prefix dashboard` at 236 of 236 tests in 18 files. | Not run: the full Angular unit run belongs to QA, which runs concurrently. My Vitest run agrees that `daily.spec.ts` gained exactly 2 cases. |
| Architecture | Compared with `docs/architecture.md` and design Decision 3 | The line rule stays in the client's `daily.ts`. A7 adds no endpoint and changes no server code. | No drift |
| Formatting and .NET checks | not run | A7 changed no .NET file | Not applicable |

## Steady-state cost and bounds

A7 adds no timer, poll, cache, request, or saved state. The answers below are unchanged from [analysis-002.md](analysis-002.md#steady-state-cost-and-bounds).

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Client poll (`app.ts`, every 2 s); `dailyTodayView` | source observation | Unchanged. Each idle interval makes one `GET /api/catalog` request. `lineFor` runs only when its input signals change. A7 adds one per-entry ticket lookup inside that pure computation. |
| Cache lifetime versus interval and refresh duration | none added | source observation | Not applicable: A7 adds no cache. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | The temporary `Set` in `qualifies` | source observation | Bounded. It is rebuilt and discarded on each call. |
| Repeated full reads where a change check would do | none added | source observation | Not applicable: A7 adds no reads. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | none | source observation | Not applicable: A7 starts no external tools. |

## Findings

**No findings.** A7 introduces no regression that blocks acceptance or safety. The `ticketKey` duplication and the one-entry-on-two-lines behavior are recorded in the Analysis table and not raised, because this is a recheck.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-041 | `head=cd1c0e3 baseline=cd1c0e3 files=19 diff=a56cce33925b untracked=5/951d6715a50a` | `daily.ts:29-38` widens the rule as option 1 proposed, and design Decision 3 now states the same rule. The verification case `daily.spec.ts:142-158` asserts the primary `f1`, the status `in_progress`, and the summary in the copied text. The exclusion case `:160-176` covers a different provider, a different scope, and another change. ANALYSIS-001 cases (a) `:117-125` and (b) `:127-140` are unchanged and pass. `npx vitest run src/daily.spec.ts`: 22 of 22 passed. | Verified |

## Assessment and limitations

ANALYSIS-041 is corrected as specified. A change card's Today line now also takes today's change-less entries that carry one of the card's tickets, matched by provider, scope, and ID. Entries of another change never qualify. The verification case and the exclusion case exist and pass, and ANALYSIS-001 cases (a) and (b) still pass.

The local `ticketKey` matches the board's rule exactly, so it does not drift today. Exporting the board's helper would remove the risk of future drift (suggestion; not raised). The widened rule lets one change-less entry with several tickets, or an entry from another repository of a multi-repository change, appear on both a ticket card's line and a change card's line. This follows the design wording, so the orchestrator may accept it or assign it later.

Not performed:
- the full `npm test --prefix dashboard` run (the 236 count), .NET tests, e2e, and formatting. QA owns these and runs concurrently.
- performance measurement; the cost statements are hypotheses.

Writes: Vitest's normal cache under `dashboard/node_modules`, and a throwaway probe spec in the session scratch directory outside the repository. The client refused the write of this report; the text is returned inline for the orchestrator to save. Claude Code and Codex parity does not apply, because A7 changed only the dashboard, which is client-neutral.
