<!-- Saved by the orchestrator (Claude Code, claude-opus-5-5) verbatim from the reviewer's returned text. Reviewer: code-analyzer subagent, Claude Code 2.1.322, subagent auto mode, model claude-opus-5-5. Case: the client refused the reviewer's write to this path; the reviewer reported the refusal and did not retry. Exact refusal message not supplied by the reviewer. -->

# Analysis report: realtime-dashboard-updates / analysis-034 (recheck)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.322, subagent, auto mode / claude-opus-5-5
- Date: 2026-11-11T20:48Z
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-RECHECK-034 (fix tier)
- Task and requirement references: ANALYSIS-740, ANALYSIS-741 ([analysis-033](analysis-033.md)), ORCH-022 ([findings](findings.md)); DEV-043
- Scope and relevant neighboring paths: `dashboard/src/app.ts` (`joined`, the eight joined loaders, `reconcileDomains`, `applyRevisionSnapshotNow`, `report`), `dashboard/src/live-updates.ts` (`RefreshQueue`), `dashboard/src/live-connection.ts` (`retry`, `markPaused`, `authorizationLost`), `dashboard/src/app.reliability.spec.ts:540-655`, `dashboard/src/app.html:12-40,837`, `dashboard/e2e/dashboard.spec.ts`
- Reading limits and paths read outside scope: source and no-host only, plus `npm test --prefix dashboard`. No paths outside scope were read.
- Baseline commit: 0d77685
- Implementation identity (before): `head=0d77685 baseline=0d77685 files=25 diff=a65cbf6c32b0 untracked=13/d972d1bec3c4` (tracked diff SHA-256 `a65cbf6c32b02020422d02f65745e6f1284aad6d70ce74fa09ce26059d29b0e1`)
- Before/after identity: after is identical, and `git status` shows the same 49 entries. No unexpected mutation.
- Prior reports / rechecked findings: [analysis-033](analysis-033.md): ANALYSIS-740, ANALYSIS-741. [findings](findings.md): ORCH-022.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| 740 fix | Inspection of `app.ts:1347-1376`, `:813-827`, `:752-806`, `live-updates.ts:125-193` | Live `loadHistory()` and `loadHistory(true)` share `historyEpoch` but join under separate names. A live call superseded by Load more finds itself as the latest "history" call, so it returns `whenAlone = false`. The journal stays pending and the status is paused. A live call superseded by a newer live or foreground reload still joins that fresh read. | Correct |
| 740 re-drive | Same | The failed `journal-view` token is held as `failed` in `RefreshQueue`. A later revision that does not change the journal re-enqueues the same token with `retryFailed = false` (`receiveRevision` passes force false), so it returns false without a read (`live-updates.ts:133`). The journal is re-driven automatically only by (a) the next journal revision, which carries a new token (any agent logging work), (b) a view or scope change through `activateLiveScope`, or (c) Retry updates, which forces the snapshot. | No automatic retry for the same revision; see the assessment |
| 740 caller | `grep loadHistory(true)` | The only caller is `app.html:837` `(click)="loadHistory(true)"`, which discards the result. A superseded `more` call publishes nothing because of the epoch, and its `finally` leaves `loading` to the newer call. | The false return is harmless |
| 741 fix | Inspection of `joined` and all catches | `newest(error)` reports a 401 and returns `Promise.resolve(false)` before the supersession lookup. All seven joined catches pass `error`. Summary reports its 401 itself and calls `newest()` with no error, so a summary call reports at most once. Document calls report from either the current-epoch branch or `newest(error)`, never both. | Correct |
| 741 double report / loop | Inspection of `report`, `invalidateAuthorizationWork`, `LiveConnection.authorizationLost` | A superseded call and its newer call can each report the same 401. Parallel loaders (catalog, reviews) already did this. `invalidateAuthorizationWork` increments `authorizationEpoch` only on the first report. `authorizationLost` returns early once unauthorized. `refreshQueue.clear()` and the waiter release are idempotent, and the error text is the same. `report` starts no load, and the 401 branch never awaits, so there is no recursion or loop. | Twice is possible and benign; no loop |
| ORCH-022 | `grep -i "work history" e2e/*.ts app.html`; `git diff 0d77685 -- e2e/dashboard.spec.ts` | All five former `getByRole("button",{name:"Work history",exact:false})` sites (`:653,887,927,994,1393`) are now scoped to `navigation "Main navigation"`. There is one such landmark (`app.html:14`), and it holds only one button whose name contains "Work history". "Explore work history" (`app.html:377`) and the header label (`:70`) are outside it. No unscoped "Work history" locator remains in `e2e/`, including `c06-realtime.spec.ts`. | Correct; not run (no e2e by assignment) |
| Regression | `npm test --prefix dashboard` | 22 files, 329/329 passed | Passed |
| Mutation proof | A scratch copy of `dashboard/` (src, scripts, configs, with `node_modules` as a junction) under `artifacts/realtime-dashboard-updates/reviews/analysis-034/dashboard`. `npx ng test --watch=false --include src/app.reliability.spec.ts`. The unmodified copy passed 62/62. | M1: `joined(more ? "history-more" : "history", …)` changed to `joined("history", …)`. Only the ANALYSIS-740 test fails: `expected [] to include 'journal'`. M2: the 401 block in `newest` removed. Only the ANALYSIS-741 test fails: `expected true to be false`. Each mutation was restored, and the restored file was confirmed byte-identical to `dashboard/src/app.ts` with `cmp`. The junction was removed non-recursively, and the real `node_modules` is intact. | Both new tests detect their revert |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Browser loaders | Source observation | Unchanged. The fixes add no requests. A superseded `more` or live call issues nothing new. |
| Cache lifetime versus interval and refresh duration | none new | Source observation | Not applicable to these fixes |
| Unbounded state | `newestLoads` | Source observation | Bounded at 9 fixed keys, with "history-more" added. Each holds one promise. |
| Repeated full reads where a change check would do | none new | Source observation | Not applicable |
| External tool discovery | none new | Source observation | Not applicable |

## Findings

**No findings.**

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-740 | `diff=a65cbf6c32b0 untracked=13/d972d1bec3c4` | Inspection, plus the M1 mutation fails the new test. The paused end state is honest: History shows old-snapshot rows, and the status does not claim Live. | Verified |
| ANALYSIS-741 | same | Inspection, plus the M2 mutation fails the new test. A 401 can be reported twice, which is idempotent. No loop. | Verified |
| ORCH-022 | same | Inspection: all five sites are scoped to a unique landmark, and no ambiguous locator remains. Not executed. | Verified from source; e2e not run |

## Assessment and limitations

**740 end state.** "Paused" is the right end state. The rows on screen come from the pre-revision snapshot, and the status says so instead of claiming Live. Nothing re-drives that same revision automatically. It is re-driven by the next journal revision (frequent while agents log work), by a view or scope change, or by Retry updates. Retry reloads page one and drops the extra pages loaded with Load more. This is acceptable for an unlucky race between a click and a live reload.

The alternatives are worse or more complex:

- Have the live call re-read page one when it is superseded: this silently discards the page the user just asked for.
- Have Load more refuse to run while a journal revision is pending: this needs a new UI state.
- Have the `more` call return false so the pending state is visible: this is what the fix does.

**Superseded `more` returning false.** This does not matter. The only caller is the template click handler, which ignores the result. The `more` call is never on the live path, because `loadVisibleJournal` always calls `loadHistory()`.

**741 stale-401 note (no finding).** A superseded call now reports its own 401. In theory a 401 that arrives after a re-authorization could flip the session back to unauthorized. The current-epoch path already behaves this way at baseline. The server answers an invalid cookie before running the handler, so the window is negligible.

**Coverage note.** Only the search catch is pinned by the 741 test. The other six catches use the same central `newest(error)` path, so a per-loader revert to `newest()` would not be caught. This was not raised, because a recheck raises no new suggestions.

**Limitations.**

- No hosts, browsers, e2e or smoke were run, by assignment. ORCH-022 is verified from source only.
- Writes: ignored scratch under `artifacts/realtime-dashboard-updates/reviews/analysis-034/`. That scratch is the dashboard copy without `node_modules`, `app.ts.orig`, `mutation-740.txt` and `mutation-741.txt`.
- The report write was refused by the client, so this text is returned inline.
- No tracked files were edited.
