<!-- Saved by the orchestrator under the reviewer-text fallback. Reviewer: code-analyzer (AN-R4). Client: Claude Code 2.1.317 subagent, light review. Model: claude-sonnet-5-5 (recorded override: test-only light review). Case: the client refused the reviewer's write; exact refusal message: "Subagents should return findings as text, not write report files. Include this content in your final response instead." Text below is verbatim. -->

```
status: done
role / client / model: code-analyzer / Claude Code (version not exposed to the agent), subagent, light review / claude-sonnet-5-5 (sonnet override, as assigned)
task / finding IDs: AN-R4 / ANALYSIS-041 (warning), ANALYSIS-042 (suggestion), ANALYSIS-043 (suggestion); ANALYSIS-044 to 050 unused
changed or report paths: none written. The write of openspec/changes/archive/2026-11-03-standup-oriented-summaries/reports/analysis-004.md was refused by the client. The report text is inline below.
checks: ./scripts/review-identity.ps1 -Baseline 7b12245 -Paths dashboard/e2e/dashboard.spec.ts -> passed (before and after); npx playwright test (focused, repeat-each=6) -> not run (port 47942 already held by a process I did not start; Playwright refused to start); builds, live agents, dotnet format -> not run (out of scope or no C# changes)
target identity: before: head=7b12245 baseline=7b12245 files=1 diff=936b318c50f3 untracked=0/e3b0c44298fc; after: head=7b12245 baseline=7b12245 files=1 diff=936b318c50f3 untracked=0/e3b0c44298fc (identical, no mutation)
limitations / decisions needed: report write refused, text inline; line 1115 hazard not reproduced because the isolated fixture port was occupied by another process, so rerun with --repeat-each after the port is free; the "2 activity dates" check at line 1093 is safe only if the stale "other" row label differs (hypothesis, not verified). I started no processes. Decision: whether to guard line 1115 in this fix (recommended) and whether to drop the redundant guard at line 1035.
next: orchestrator saves the report text below; developer adds `await expect(page.locator(".timeline-row")).toHaveCount(1);` between lines 1114 and 1115 of dashboard/e2e/dashboard.spec.ts; rerun the two ticket scenarios with --repeat-each=10 once port 47942 is free
```

# Analysis report: standup-oriented-summaries / AN-R4 (history ticket-filter race, test-only fix)

## Review target

- Reviewer / role: code-analyzer / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (version not exposed to the agent) / subagent, light review (test-only target) / claude-sonnet-5-5 (override to sonnet as assigned)
- Date: 2026-11-03 (UTC date; time not recorded)
- Change root / assignment: openspec/changes/archive/2026-11-03-standup-oriented-summaries / AN-R4
- Task and requirement references: none (branch `fix/history-ticket-filter-race`, test-only fix)
- Scope and relevant neighboring paths: `dashboard/e2e/dashboard.spec.ts` (diff at lines 1029-1030 and 1034-1035 of the working tree, scenario "links tickets, repositories and branches from recorded facts without navigating"); neighbors `dashboard/e2e/dashboard.spec.ts` ticket/history scenarios, `dashboard/src/app.ts` (`loadHistory`, `ticketChanged`, `controlsChanged`), `dashboard/src/app.html` (History timeline template)
- Reading limits and paths read outside scope: no stated limit beyond the assignment. Also read `dashboard/playwright.config.ts`, the head of `dashboard/e2e/server.mjs`, and the role, project-facts and template files. No user data read.
- Baseline commit: 7b12245
- Implementation identity (before): `head=7b12245 baseline=7b12245 files=1 diff=936b318c50f3 untracked=0/e3b0c44298fc` (manifest: `M dashboard/e2e/dashboard.spec.ts`; tracked diff SHA-256 936b318c50f3121c497bef6b86802202cb15ac70d2dc06d1704f9170d92f6acf)
- Before/after identity: after the analysis the same command printed the identical summary line. No unexpected mutation. I wrote no file (the report write was refused).
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Fix correctness | Read the diff and the surrounding scenario; traced the DOM before and after each filter change | `.timeline-row` rows are rendered from `timeline()` (`app.html` 761); `loading()` only adds a status line (741) and `timeline` is replaced only when the response arrives (`app.ts` 812-813). So after `Enter` the previous filter's rows stay until the new page arrives. | The first guard (line 1030) waits on the right state: the stale DEP-12 view has 2 ticket links, the target has 1, so the count wait only passes on the new rows. Locator-level strict mode makes `toHaveAttribute` fail at once on 2 matches rather than retry; the guard prevents this. |
| Second guard | Same trace | Stale view for REC-5 is the 4211 row, which also has exactly 1 `.ticket-link` | The guard at line 1035 passes on stale rows, so it does not wait for new state. The test is still correct, because with one element `toHaveAttribute` retries until the href changes. Redundant, harmless (ANALYSIS-042). |
| Same pattern elsewhere | Read every ticket-filter / History scenario in `dashboard/e2e/dashboard.spec.ts`, and grep for `.row-label`, `.timeline-row ... click`, preset, and focus actions | Hazard at line 1115 (ANALYSIS-041). Other sites use a count that differs from the stale count or a discriminating single-element assertion (see table below). | One real site, several vacuous but safe guards. |
| Product race | Read `loadHistory`, `ticketChanged`, `controlsChanged`, `selectPreset`, `datesChanged`, `focusDay`, template | `historyEpoch` is incremented per call and checked before `timeline.set`, in the `catch`, and in `finally` (`app.ts` 797-818). Load-more reuses `historyQuery` + `snapshot` and its result is dropped if a newer load started. The Load-more button is `[disabled]="loading()"` (`app.html` 809). | No out-of-order-response defect found. A brief stale-row window is by design (ANALYSIS-043). Source observation only. |
| Fixture run | Planned `npx playwright test -g "copies a work item as Markdown\|links tickets, repositories" --repeat-each=6` on the isolated fixture | Playwright refused to start: `http://127.0.0.1:47942/health is already used`. Port 47942 was held by a process I did not start. | Not performed. I did not stop or reuse that process (project-facts safety). The line 1115 hazard is therefore a source-level finding, not a reproduced failure. |

Per-site review of the ticket-filter flows (state before Enter -> target; what the first following assertion does):

| Line | Transition | First assertion | Verdict |
| --- | --- | --- | --- |
| 1023 | 50 rows -> 2 | `.timeline-row` count 2 | safe (differs from stale) |
| 1030 | 2 rows -> 1 (4211) | new guard, count 1 | safe (fixed) |
| 1036 | 1 -> 1 (REC-5) | guard count 1 (vacuous), then single-element `toHaveAttribute` | safe, retries |
| 1039-1041 | 1 -> 1 (UNK-7) | count 1 (vacuous), `.ticket-id` has 0 matches on stale row, retries | safe |
| 1049, 1055, 1061 | 2 -> 1 | count 1 first, then clicks | safe |
| 1069-1071 | 1 -> 1 (DEP-30) | count 1 (vacuous), `toHaveAttribute` on one element with a different href, retries, then `.row-label` click | safe (href discriminates before the click) |
| 1087, 1091, 1093 | 50 -> 1, 1 -> 1 | count 1, then `toContainText("2 activity dates")` | safe if the stale "other" row's label differs (hypothesis, not verified) |
| 1096, 1101, 1104 | 1 -> 2, reload, 2 -> 50 | counts that differ from stale | safe |
| 1115 | 50 rows -> 1 | `.timeline-row .row-label` `click()` with no wait | unsafe (ANALYSIS-041) |
| 1134 | clear filter, 2 -> 50 | `.row-label` filtered by text WL-190 | safe (the filter matches one row; click auto-waits) |
| 353, 580, 1244-1258 | Load-more / `.daily-item` `.first()` / client-side board filter | different mechanisms | not affected |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | not applicable: the target is a test-only change with no timer, poll, or loop | n/a | n/a |
| Cache lifetime versus interval and refresh duration | not applicable: no cache in the target | n/a | n/a |
| Unbounded state | not applicable: no state added | n/a | n/a |
| Repeated full reads | not applicable: no per-request work added | n/a | n/a |
| External tool discovery | not applicable: no tools spawned by the target | n/a | n/a |

## Findings

### ANALYSIS-041: Same strict-mode race remains at the Markdown-copy scenario

- Severity: warning
- Related task / requirement: test reliability for the History ticket filter
- Locations: `dashboard/e2e/dashboard.spec.ts` lines 1113-1115 at 7b12245 plus the working-tree diff (`copies a work item as Markdown, loading every observation page`)
- Observation: `openHistory` leaves the full 50-row History rendered. The scenario then fills the filter, presses Enter, and immediately calls `page.locator(".timeline-row .row-label").click()`. Playwright strict mode throws a strict-mode violation as soon as the locator resolves to more than one element, and the stale 50 rows are rendered until the filtered page arrives (the same mechanism the fix describes). The click happens right after the key press with no await on the new state, so it passes only if the response lands first. Source observation; the failure was not reproduced (fixture port was occupied by another process, see Analysis performed).
- Proposed improvement and benefit: add `await expect(page.locator(".timeline-row")).toHaveCount(1);` between lines 1114 and 1115. 50 differs from 1, so the wait passes only on the new rows. Removes an intermittent strict-mode failure.
- Behavior-preservation risks: none. Test-only; the next assertion (`.observation` count 2) is unchanged.
- Evidence type: source observation (reproduction not performed)
- Verification / measurement plan: after the change, run the scenario with `--repeat-each=10` on the isolated fixture (port 47942) when the port is free. A failure rate before the change would confirm the hypothesis; I did not measure one.
- Relationship: same root cause as the fixed site; new location

### ANALYSIS-042: Two guards in the fixed scenario pass on stale rows

- Severity: suggestion
- Related task / requirement: none
- Locations: `dashboard/e2e/dashboard.spec.ts` lines 1034-1035 (new guard); also pre-existing count checks at 1039, 1069, 1091
- Observation: when the stale state and the target both have exactly one row/link, `toHaveCount(1)` is satisfied by the stale row. The following single-element `toHaveAttribute` / `toHaveText` retries and is what actually discriminates, so the tests are correct today. The guard at 1035 therefore documents intent but does not wait for the new state, and the comment above it claims a failure mode that cannot happen at that site (the stale view has one link).
- Proposed improvement and benefit: optional. Either drop the second guard and comment, or gate on the discriminating value (for example, assert the `href` directly, which retries on one element). Keeps the stated reasoning accurate.
- Behavior-preservation risks: none. If the guard is dropped, the retrying `toHaveAttribute` covers it. A future fixture change giving the stale row two links would reintroduce the first-site problem, so keeping it is also defensible.
- Evidence type: source observation
- Verification / measurement plan: none needed.
- Relationship: related to ANALYSIS-041 (same pattern, safe sites)

### ANALYSIS-043: Product side has no out-of-order defect; stale rows during a filter load are by design

- Severity: suggestion
- Related task / requirement: dashboard History loading
- Locations: `dashboard/src/app.ts` 796-818 (`loadHistory`), 945-958 (`ticketChanged`, `controlsChanged`); `dashboard/src/app.html` 741-761, 805-815
- Observation: `loadHistory` takes an epoch before the request and discards the result, the error report, and the `loading` reset if a newer call began. Fast typing or rapid filter changes cannot render an older response over a newer one. Load-more reads `historyQuery` and `snapshot` from the page that is displayed and is disabled while loading, and a filter change during load-more discards it. Users can see stale rows (old filter's rows with an "Updating the journal snapshot..." status and `—` in the stats) for the duration of one request; the rows remain clickable and the detail opens with the old query, which is consistent with the rows shown. Residual hypotheses, not reproduced: if the new request fails, `this.ticket` is already updated (`ticketChanged` sets it before awaiting) while the timeline keeps the old filter's rows, so the filter chip and the rows disagree under an error banner. Also the rows are not marked busy (only the stats grid has `aria-busy`).
- Proposed improvement and benefit: optional product polish, not needed for the test fix: mark the timeline region busy, or clear/dim rows while loading, and consider reverting `this.ticket` on a failed load. This would also make the test race impossible by construction, but it changes behavior and needs its own proposal. Not recommended as part of this fix.
- Behavior-preservation risks: clearing rows would cause flicker and change the e2e tests that rely on counts.
- Evidence type: source observation; user visibility of the error-path mismatch is a hypothesis
- Verification / measurement plan: if pursued, a unit test in `dashboard/src/app.reliability.spec.ts` that rejects the second request and asserts the displayed filter state.
- Relationship: none

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| none | n/a | initial analysis, no rechecks | n/a |

## Assessment and limitations

The fix at line 1030 is correct and waits on the right observable (a count the stale view cannot match). The second guard is redundant but harmless. One additional site (line 1115) has the same stale-state pattern with a strict-mode `click()` and should be guarded the same way (warning, recommended before merge since the root cause is the same flake). No product race was found; epoch handling covers out-of-order responses.

Not performed: the focused Playwright run (isolated fixture port 47942 was already bound by a process I did not start, so I did not start another or touch it), any build, any live agent. I started no processes. No `dotnet format` run (no C# changes). Identity is unchanged before and after. The orchestrator owns triage and ledger status.
