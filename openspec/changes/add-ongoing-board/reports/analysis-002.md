<!--
Saved by the orchestrator (Claude Code 2.1.312, session 1570e404-00dd-4689-9952-af0e082ed1e3) on 2026-10-29.
Reviewer: A6 code-analyzer, Claude Code 2.1.312, agent subagent (non-interactive), claude-opus-5-5.
Case: the client refused the reviewer's write. Exact refusal message as reported by the reviewer: "Subagents should return findings as text, not write report files. Include this content in your final response instead." Text below is the reviewer's report verbatim.
-->

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.312 (as recorded in findings.md) / claude-opus-5-5
task / finding IDs: recheck ANALYSIS-001, 002, 003 (all verified; 002 has a leftover key-length risk that needs a decision); new ANALYSIS-041 (warning), ANALYSIS-042 (suggestion); ANALYSIS-043 – 060 unused
changed or report paths: none written. Intended path D:\Code\ZeusApps.WorkLog\openspec\changes\add-ongoing-board\reports\analysis-002.md. The write was refused, and the full report text is below
checks: npm test --prefix dashboard -> passed (16 files, 184/184); buildBoard copy-order probe (board.ts bundled with the repository's esbuild into the scratch directory, Node) -> run; preference size recomputation (scratch Node script) -> run; e2e / packaged / dotnet -> not run (QA A5 owns functional validation)
target identity: before head=607df5f baseline=607df5f files=20 diff=6a4edc95043c untracked=7/274b8c8b917f; after head=607df5f baseline=607df5f files=20 diff=6a4edc95043c untracked=7/274b8c8b917f (match, as expected)
limitations / decisions needed: the client refused the report write with the exact message "Subagents should return findings as text, not write report files. Include this content in your final response instead." The report text is inline for the orchestrator to save. Decisions: ANALYSIS-041 (which copy's facts a merged card shows); the ANALYSIS-002 leftover risk (accept it, or limit the order by bytes). Not observed live: the NG0955 warning
next: orchestrator saves the report, triages ANALYSIS-041 and ANALYSIS-042, and decides on the ANALYSIS-002 leftover risk
```

---

# Analysis report: add-ongoing-board / analysis-002

## Review target

- Reviewer / role: code-analyzer subagent (assignment A6, recheck) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 (version as recorded in [findings.md](findings.md), not independently observed) / agent subagent, non-interactive / claude-opus-5-5
- Date: 2026-10-29T06:40Z
- Change root / assignment: `openspec/changes/add-ongoing-board` / A6
- Task and requirement references: tasks 2.1, 2.2, 3.1–3.3 ([tasks.md](../tasks.md)); [spec delta](../specs/work-dashboard/spec.md) "Ongoing board" (one card per change), "Bounded dashboard preferences" (200 cap, budget fit), "Board priority order"
- Scope and relevant neighboring paths: `dashboard/src/board.ts`, `board.spec.ts`, `app.ts`, `app.html`, `app.board.spec.ts`, `dashboard/e2e/dashboard.spec.ts`. Neighbors read: `src/WorkLog/Dashboard/SourceCatalog.cs` (variant and source order), `src/WorkLog/Repositories/RepositoryIdentity.cs` (key format)
- Reading limits and paths read outside scope: the assignment is a recheck of ANALYSIS-001..003 plus regression, with no hard limit. Outside the list: `openspec/changes/add-daily-prep/design.md` (lines 13 and 51, the quiet contract)
- Baseline commit: `607df5f`
- Implementation identity (before): `head=607df5f baseline=607df5f files=20 diff=6a4edc95043c untracked=7/274b8c8b917f`. Tracked diff SHA-256 `6a4edc95043cb92540234c1796456386163f44eabeb002cd8ff246370615d1c8`. Untracked: `app.board.spec.ts` `be0f04cc…`, `board.spec.ts` `ad3202e5…`, `board.ts` `47bf9b6b…`, `OngoingUnlinkedTests.cs` `a47f11d1…`, screenshots `fd0b5fb6…`, `e2d533b7…`, `a891a466…`. Matches the expected value.
- Before/after identity: after the analysis, `head=607df5f baseline=607df5f files=20 diff=6a4edc95043c untracked=7/274b8c8b917f`. It matches, with no unexpected mutation; `git status --short` is unchanged at 23 lines.
- Prior reports / rechecked findings: [analysis-001](analysis-001.md): ANALYSIS-001, ANALYSIS-002, ANALYSIS-003

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-001 correction | Inspected `board.ts:87–120`, `app.ts:624–637`, `app.html:359–416, 808–822`. Probed `buildBoard` with `board.ts` bundled by the repository's esbuild into the scratch directory, run on Node | There is one card per `changeKey`, and `copies` is kept. `openCard` opens the copy chooser when `copies.length > 1`. Comment counts and updated-since-read marks add up across all copies. Probe: two copies produce one card, whose stage, task count and documents come from `copies[0]` | Keys are unique. The merge causes two regressions (ANALYSIS-041, ANALYSIS-042) |
| ANALYSIS-002 correction | Inspected `board.ts:8–10, 140, 293` and `board.spec.ts:303–377`. Recomputed the size test's payload, plus two variants, in a scratch Node script | The test payload is 62,637 bytes, leaving 2,899 bytes of headroom. With 200 single-repository change keys of the same lengths it is 63,482 bytes. With 200 two-repository change keys (the test's change-key shape) it is 74,682 bytes, which exceeds 65,536 | The approved resolution is in place. The leftover risk is under Re-verification |
| ANALYSIS-003 correction | Inspected `board.ts:297–303`, `app.ts:620–623`, `app.html:399–402` and `board.spec.ts:333–353` | `isQuiet` is pure and applies only to In-progress change cards. The template uses it through `quiet(card)`. The per-card `activityFor` rescan (`changeActivity`) is gone | Verified |
| Regression: unit tests | `npm test --prefix dashboard` | 16 files, 184/184 passed | Passed |
| Regression: e2e | Inspected the e2e diff | The change-card count going from 4 to 3, and the chooser step in `openTimeline`, both match the merge | e2e not run. QA A5 owns functional validation, including the reported NO_NODE keyboard-reorder failure |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Client `poll()`, `fullBoard` | Source observation | Unchanged from analysis-001. The merge adds a `Map` grouping to each recompute, which is linear in the number of changes. No new timer, request or process |
| Cache lifetime versus interval and refresh duration | None added | Source observation | Not applicable: the corrections add no cache |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `board.order` | Measured payload computation | Capped at 200 entries. The byte size still depends on key length, because change keys include the repositories and the change name (see the ANALYSIS-002 leftover risk) |
| Repeated full reads where a change check would do | `/api/ongoing/unlinked` | Source observation | Unchanged (ANALYSIS-005, deferred) |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | None | Source observation | Not applicable: the corrections add no tool lookup |

## Findings

### ANALYSIS-041: A merged change card takes its column, task count and documents from whichever copy sorts first

- Severity: warning
- Related task / requirement: 2.1, 3.2; "Ongoing board" (one card per change; columns come from recorded facts; change cards keep task progress and document links)
- Locations: `dashboard/src/board.ts:94–115` (`change = copies[0]`; `changeStage(change.completedTasks, …)`); `dashboard/src/app.html:389–421` (`change.documents`, task footer); `src/WorkLog/Dashboard/SourceCatalog.cs:140` (checkouts sorted by path, ordinal)
- Observation:
  - When one change exists in a checkout and in its worktree, the two copies usually differ: the proposal lands in one, and tasks get ticked on the implementation branch in the other.
  - The merged card uses only `copies[0]`, the checkout whose path sorts first. That choice has nothing to do with which copy is current.
  - Probe with one copy at 3/9 tasks and one at 9/9: with the 3/9 copy first, the card is In progress at 3/9 with 1 document. With the order reversed, it is Done at 9/9 with 2 documents.
  - The document buttons list only the first copy's documents. The chosen path is passed to every copy in the chooser (`copyPath`), so picking a copy that lacks that file opens a missing document. Documents that exist only in the other copies never appear on the card.
- Proposed improvement and benefit: derive the merged card's facts from all copies, independent of their order. For example, take stage and progress from the copy with the most ticked tasks or the latest task stamp. Show the documents of all copies, de-duplicated by path. In the chooser, offer only the copies that contain the chosen path. The column then follows the recorded facts, as the spec requires.
- Behavior-preservation risks: e2e counts and column expectations for `activity-timeline`. The rule that shows both facts when task progress and recorded status disagree must still hold.
- Evidence type: measured result (probe over the bundled `board.ts`). That copies typically differ is a source and workflow observation
- Verification / measurement plan: a `board.spec.ts` case with two copies that differ in `completedTasks` and documents. Assert that the stage is the same in either input order and that the card shows the expected documents.
- Relationship: arises from the ANALYSIS-001 correction

### ANALYSIS-042: The copy chooser tracks copies by variant alone and keeps a stale `copyPath`

- Severity: suggestion
- Related task / requirement: 3.2
- Locations: `dashboard/src/app.html:813` (`track copy.variant`); `dashboard/src/app.ts:145, 625–631` (where `copyPath` is set), `:996–1004` and `:1025–1031` (where it is not reset)
- Observation:
  - `Variant` is the hash of the change's path relative to the source root (`SourceCatalog.cs:273`). A checkout copy and its worktree copy therefore have the same variant, and the probe confirmed this (`copyVariants=same,same`).
  - Opening the chooser from a merged card therefore gives duplicate track keys, which Angular reports in dev mode as warning NG0955. Before the merge, the chooser opened only for copies within one source, and those always have different variants.
  - Only `openCard` sets `copyPath`. `closeDocument` never clears it, and neither does the chooser that a URL opens. A chooser opened later from a URL (route-driven) then opens the previous card's document path in a different change.
- Proposed improvement and benefit: track copies by `copy.sourceId + copy.variant`, as `trackChange` already does. Clear `copyPath` in `closeDocument` and wherever `routeChanged` sets `copies`.
- Behavior-preservation risks: none.
- Evidence type: source observation, plus the probe's measured variant equality
- Verification / measurement plan: an `app.board.spec.ts` case that opens a document from a two-copy card, closes the chooser, then opens a chooser from a URL, and asserts that the default document opens.
- Relationship: arises from the ANALYSIS-001 correction

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | `diff=6a4edc95043c untracked=7/274b8c8b917f` | `board.spec.ts:312–331` passes: keys are unique, and a move or drop keeps the column length. The probe gives one card per key. `openCard` sends cards with several copies through the chooser, and the e2e `openTimeline` helper exercises that path | Verified: keys are unique and the chooser path works. The merge introduces ANALYSIS-041 and ANALYSIS-042 |
| ANALYSIS-002 | same | `orderLimit = 200` and `ticketKeyLength = 16` (`board.ts:8–10, 140`). The size test (`board.spec.ts:358–371`) passes, and my recomputation gives 62,637 bytes. Leftover risk: the test uses 100 change keys and 100 ticket keys, but ticket ranks expire within 7 days while ranks of active changes persist. 200 change keys of the test's own two-repository shape give 74,682 bytes, above the limit; single-repository keys give 63,482. The spec sentence "preferences with every bound at its limit SHALL fit the storage limit" therefore holds only for the key lengths the test assumes. Whether 200 change keys can actually be live is a hypothesis: it needs 200 active changes. The test title at `board.spec.ts:303` still says "500 entries" | Verified for the approved resolution. The key-length risk needs an orchestrator decision: accept it, or limit the order by bytes |
| ANALYSIS-003 | same | `isQuiet` (`board.ts:301–303`) is used by the template through `App.quiet`. The boundary tests pass: 14 days is not quiet, 15 is, and cards that are not In-progress change cards are never quiet. So does `app.board.spec.ts:184`. `add-daily-prep/design.md:13` still says `buildBoard` carries the quiet indication and does not name `isQuiet` | Verified. The orchestrator may want to update that line in add-daily-prep |

## Assessment and limitations

All three corrections are implemented as approved, and the unit suite passes 184/184. Merging copies into one card leaves the card's column, task count and documents dependent on which source sorts first (ANALYSIS-041, warning), and adds two small chooser defects (ANALYSIS-042). The ANALYSIS-002 size guarantee depends on key length, and the test's mix of keys does not cover the worst case.

Not performed: e2e and packaged runs (QA A5 owns them), a live NG0955 observation, and `dotnet` checks (the corrections changed no C#).

Probes and size computations ran in the session scratch directory outside the repository. No repository file was written: the client refused the one attempt to write this report, and the target identity is unchanged. The orchestrator owns triage and ledger status.
