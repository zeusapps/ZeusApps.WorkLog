<!-- Saved by the orchestrator (Claude Code, Opus 5.5, session f406ff60). Reviewer: code-analyzer subagent, assignment Y3; client Claude Code 2.1.317, delegated subagent mode, light review; model claude-sonnet-5-5 (recorded override: two-file test-seam change). Case: the client refused the reviewer's write of this report. Exact refusal message as returned by the reviewer: "Subagents should return findings as text, not write report files. Include this content in your final response instead." The text below is the reviewer's returned report, saved verbatim. -->

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.317 / claude-sonnet-5-5
task / finding IDs: Y3 / none raised (ANALYSIS-171..180 unused)
changed or report paths: none written. Reserved path D:\Code\ZeusApps.WorkLog\openspec\changes\archive\2026-11-03-add-day-digests\reports\analysis-013.md; the write was refused and the report text is inline below.
checks: ./scripts/review-identity.ps1 -Baseline ffcf7f3 -Paths dashboard -> passed (before and after identical); build, type-check and tests -> not run (QA owns execution)
target identity: before head=ffcf7f3 baseline=ffcf7f3 files=2 diff=a3503dd6e9ed untracked=0/e3b0c44298fc ; after head=ffcf7f3 baseline=ffcf7f3 files=2 diff=a3503dd6e9ed untracked=0/e3b0c44298fc (unchanged)
limitations / decisions needed: the report write was refused by the client. Nothing was compiled, so the type match is by inspection of the jsdiff 9.0.0 declarations. The reason the CI bundler skipped `vi.mock` was not reproduced.
next: orchestrator saves the report below as analysis-013.md and triages. QA runs the Angular unit-test build (the type-check of record) and the CI rerun.
```

# Analysis report: add-day-digests / Y3 (light review of Y1)

## Review target

- Reviewer / role: code-analyzer (Y3) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.317 / delegated subagent / claude-sonnet-5-5
- Date: 2026-11-03T08:22Z
- Change root / assignment: `openspec/changes/archive/2026-11-03-add-day-digests` / Y3 (post-archive release blocker, CI run 41601418849)
- Task and requirement references: post-archive blocker entry in [findings ledger](findings.md); no task or requirement change
- Scope and relevant neighboring paths: `dashboard/src/diff.ts`, `dashboard/src/diff.timeout.spec.ts`. Neighbors read: `dashboard/src/app.ts` (the `readMarks` call sites only) and the jsdiff 9.0.0 type declarations under `dashboard/node_modules/diff/libesm`.
- Reading limits and paths read outside scope: `qa-015.md` was not read, per the assignment. Outside scope I read the last entries of `reports/findings.md`, the role and template files, `dashboard/package.json`, and `dashboard/src/app.ts` lines 1295-1312.
- Baseline commit: ffcf7f3
- Implementation identity: `head=ffcf7f3 baseline=ffcf7f3 files=2 diff=a3503dd6e9ed untracked=0/e3b0c44298fc`. Tracked diff SHA-256 is `a3503dd6e9ed8ca60dcc20f83b62e8223f4e546c738be6785eb8beb9564308e0`. Manifest: `M dashboard/src/diff.timeout.spec.ts`, `M dashboard/src/diff.ts`.
- Before/after identity: the summary line after the analysis is identical, so there was no unexpected mutation.
- Prior reports / rechecked findings: none. This is a light review of a new fix.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Seam type correctness | Compared `LineComparer` with the jsdiff 9.0.0 `diffLines` overloads in `libesm/diff/line.d.ts` and `libesm/types.d.ts`. | `Change = ChangeObject<string>` (`types.d.ts:20`) and the package index exports it. The fourth overload takes `DiffLinesOptionsAbortable`, which includes `TimeoutOption { timeout: number }`, and returns `ChangeObject<string>[] \| undefined`. `{ timeout: number }` is assignable to that options type and the return type matches exactly. An overloaded function is assignable to a single signature when any overload matches. | The type matches by inspection. It was not compiled (no builds per the assignment); the Angular unit-test build type-checks and QA runs it. |
| Default preserves behavior | Read the `diff.ts` diff. | The default is `diffLines`. The call keeps the same options object `{ timeout: diffTimeoutMilliseconds }`, and the `incomplete` branch is unchanged. | Behavior preserved. |
| Production callers | `grep readMarks dashboard/src`. | The only production call is `app.ts:1308`, `readMarks(baseline.text, value.text)`, with two arguments. The other hits are an unrelated signal and a template binding. | No production caller passes a comparer. |
| Root cause and robustness | Reasoned from the ledger description and the code. | The failure is `vi.mock("diff")` not applying under the `@angular/build:unit-test` bundler on the runner. The fix removes module interception and passes the test's function as an ordinary argument, so it does not depend on hoisting or bundle layout. No other `vi.mock` or `vi.doMock` exists in `dashboard/src` or `dashboard/e2e`. `app.reviews.spec.ts` uses `vi.mocked(fetch)` on an object, which is unaffected. The stub returns `undefined` synchronously, so no real clock or load-dependent diff is involved. | Insensitive to bundling and scheduling. I did not reproduce why the mock was bypassed on the runner; the fix does not need that. |
| Test quality | Read the spec. | Test 1 asserts the incomplete result, the unmarked render and the unmarked source segments. Test 2 asserts the comparer receives `{ timeout: 3000 }` via `diffTimeoutMilliseconds`, which covers the option the module mock used to hide. `toHaveBeenLastCalledWith` tolerates the shared mock accumulating calls across tests. | Adequate for the blocker. |
| Simplification | Compared with baseline. | The net change is one exported type, one defaulted parameter and one added test case. The module mock and its hoisting dependence are removed, and no abstraction is added beyond the seam. | Minimal. |
| Duplication, architecture, performance | Not applicable. | This is a test-seam change in a pure function. It has no dashboard architecture, MCP or parity impact (the dashboard is client-neutral) and no runtime path change. | None. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | `readMarks` (pure function, called from `marksFor`) | source observation | Not applicable: no timer, poll or loop is added or changed. `marksFor` already memoizes per revision pair (`app.ts:1306-1308`) and is unchanged. |
| Cache lifetime versus interval and refresh duration | none added | source observation | Not applicable: no cache is added or changed. |
| Unbounded state | none added | source observation | Not applicable: a defaulted parameter and a type add no state. |
| Repeated full reads | none | source observation | Not applicable: no request or read path is touched. |
| External tool discovery | none | source observation | Not applicable: no executable lookup or spawned tool. |

## Findings

**No findings.**

Two observations need no action. First, a genuinely slow diff that makes the real `diffLines` abort and return `undefined` is still not exercised end to end. That was already true at baseline, because the old mock replaced it too. Second, the comparer parameter is test-only API surface on an exported function, which is the intended trade for determinism.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| None assigned | `head=ffcf7f3 baseline=ffcf7f3 files=2 diff=a3503dd6e9ed untracked=0/e3b0c44298fc` | Not applicable (ANALYSIS-171 to ANALYSIS-180 unused) | Not applicable |

## Assessment and limitations

The seam is correct and minimal. The type matches the jsdiff 9.0.0 `diffLines` overload that takes a `timeout`. The default keeps production behavior and options identical, and the single production caller passes no comparer. Replacing module interception with an explicit argument removes the cause of the failure (bundler handling of `vi.mock`) instead of working around it, and the stub is synchronous, so the test does not depend on scheduling. I ran no build, type-check or test (QA owns execution; the Angular unit-test build is the type-check of record), and I did not reproduce the CI bundling behavior. The orchestrator owns triage. The review identity was unchanged before and after.
