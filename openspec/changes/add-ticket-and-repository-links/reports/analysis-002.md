<!--
Saved by the orchestrator (Claude Code 2.1.312, session 7b4534dc-9e1e-4f7c-aa54-9b7bc061c7e7) on 2026-10-28.
Reviewer: A6 code-analyzer recheck, Claude Code, agent subagent (non-interactive), claude-opus-5-5.
Case: no write attempted. The reviewer stated that its client subagent instructions say to return analysis reports as text and not to write report files; no refusal message exists. Text below is the reviewer's report verbatim.
-->

# Analysis report: add-ticket-and-repository-links / 002

## Review target

- Reviewer / role: A6 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (the orchestrator ledger records 2.1.312; the subagent cannot observe it independently) / agent subagent (non-interactive) / claude-opus-5-5
- Date: 2026-10-28T20:30Z
- Change root / assignment: `openspec/changes/add-ticket-and-repository-links` / A6 (recheck)
- Task and requirement references: task 3.1 ([tasks.md](../tasks.md)); requirement "Copy a work item as Markdown" ([specs/work-dashboard/spec.md](../specs/work-dashboard/spec.md)); design Decision 3 ([design.md](../design.md)); A4 fix round 1 in [findings.md](findings.md)
- Scope and relevant neighboring paths:
  - Fix: `dashboard/src/workitem-markdown.ts`, `dashboard/src/links.ts`, and the new cases in `dashboard/src/workitem-markdown.spec.ts` and `dashboard/src/links.spec.ts`.
  - Checked for regressions: the callers of `ticketUrl`, `repositoryUrl` and `branchUrl` (`dashboard/src/app.ts:25,180-183` and `dashboard/src/app.html:1018,1086,1151`), the e2e `href` and Markdown expectations in `dashboard/e2e/dashboard.spec.ts:1013-1138`, and the recorded fixture URLs in `tools/WorkLog.Smoke/DashboardFixture.cs`.
- Reading limits and paths read outside scope:
  - I did not read `openspec/changes/add-ongoing-board/`.
  - Outside scope I read `.agents/roles/code-analyzer.md`, `.agents/roles/project-facts.md`, `.agents/templates/analysis-report.md`, [analysis-001.md](analysis-001.md) and [findings.md](findings.md).
- Baseline commit: `9243722`
- Implementation identity (before), from `./scripts/review-identity.ps1 -Baseline 9243722 -Paths README.md docs/dashboard-validation.md dashboard/e2e/dashboard.spec.ts dashboard/src src/WorkLog tests/WorkLog.Tests tools/WorkLog.Smoke`:
  - HEAD `9243722dda0a`; manifest of 9 modified and 10 untracked files.
  - Tracked diff SHA-256 `5d553ba5e8e51a56ba5ddc0d8d6590801bf84ed5838a6fca887bfa76429dc5eb`.
  - Untracked hashes include `workitem-markdown.ts` `9dc6868d…`, `workitem-markdown.spec.ts` `923cb2bb…`, `links.ts` `56d017de…` and `links.spec.ts` `0826d38a…`.
  - Summary: `head=9243722 baseline=9243722 files=19 diff=5d553ba5e8e5 untracked=10/c3abeeaa9b0e`. This matches the expected A4 identity.
- Before/after identity: the summary after the analysis was `head=9243722 baseline=9243722 files=19 diff=5d553ba5e8e5 untracked=10/c3abeeaa9b0e`, the same as before. `git status --short` showed no changes beyond the expected manifest and the untracked reports, screenshots and `add-ongoing-board` directories.
- Prior reports / rechecked findings: [analysis-001](analysis-001.md); rechecked ANALYSIS-001.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Correctness of the escape fix | Inspected `dashboard/src/workitem-markdown.ts:29` and ran a Node v24.13.0 probe script, run from a file because Git Bash strips backslashes from `node -e` arguments. | `escapeText` is now `text.replace(/([\[\]])/g, "\\$1")`. `"a [b] c"` becomes `a \[b\] c`, and `"DEP[1]"` becomes `DEP\[1\]`. | Fixed. |
| Coverage of the escape | Inspected the call sites at lines 42, 46-47 and 52. | The escape now covers a linked or plain ticket ID, a linked or plain repository label, and the title. | Fixed. Plain IDs and labels are now included as well. |
| URL normalization | Inspected `dashboard/src/links.ts:13-21` and ran the probe. | `absoluteWebUrl` returns `new URL(text).href`. `.../a b` becomes `.../a%20b`, `<` and `>` are percent-encoded, and the fixture URL `https://tracker.example.invalid/browse/REC-5` is unchanged. | Fixed for the stated case (an inner space). |
| Regression tests | Read the specs. | `workitem-markdown.spec.ts:46-58` covers a bracketed title, linked ticket ID, linked label and plain label. `workitem-markdown.spec.ts:59-62` covers a recorded URL with a space inside Markdown. `links.spec.ts:13-15` covers the `href` normalization. Existing recorded-URL expectations (`links.spec.ts:8-12`) still hold because those URLs are already normalized. | Present for both parts. |
| Duplication / drift from the fix | Searched `dashboard/` (excluding `node_modules`) for `escapeText`, `absoluteWebUrl`, the URL builders and `replace(/` helpers. | There is one escape helper, private to `workitem-markdown.ts`, and one URL normalizer, private to `links.ts`. The fix adds no second copy. Template and Markdown code share `ticketUrl`/`repositoryUrl` as before. | No new duplication or drift. |
| Behavior change on rendered links | Compared the callers and e2e expectations (`app.html:1151`, `dashboard.spec.ts:1013-1138`). | Displayed `.ticket-link` `href`s now use the normalized form. Every e2e expectation and fixture URL is already normalized. A bare origin gains a trailing `/`, a risk analysis-001 already recorded. | No regression in the checked expectations. |
| Unit tests | `npm test --prefix dashboard` | 14 files and 122 tests passed. A1 had 119; the new cases account for the rest. | Passed. |
| Steady-state and performance | Inspected the fix delta. | Pure string functions. One `new URL` call per recorded URL, which `absoluteWebUrl` already made before the fix. | No change. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | None changed by A4. The fix touches only `escapeText` and `absoluteWebUrl`. | Source observation | Not applicable. The fix adds no timer, poll or request. The A1 poll analysis in [analysis-001](analysis-001.md) still stands. |
| Cache lifetime versus interval and refresh duration | None changed | Source observation | Not applicable. The fix adds no cache. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | None changed | Source observation | Not applicable. Both functions are pure and keep no state. |
| Repeated full reads where a change check would do | None changed | Source observation | Not applicable. The same `new URL` parse is done once per ticket render or copy, as before. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | None | Source observation | Not applicable. Nothing is spawned and no network calls are made. |

## Findings

**No findings.** The fix introduced no regressions; ANALYSIS-041 to ANALYSIS-060 are unused.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 (escape part, duplicate of the canonical QA-001) | `head=9243722 diff=5d553ba5e8e5 untracked=10/c3abeeaa9b0e` | The Node probe of the exact corrected expression escapes each bracket. `workitem-markdown.spec.ts:46-58` asserts the escaped title, linked ticket ID, linked label and plain label. `npm test` passed (122). | verified |
| ANALYSIS-001 (unnormalized recorded-URL part) | same | `links.ts:17` returns `new URL(text).href`. The probe turns `.../a b` into `.../a%20b`. `links.spec.ts:13-15` and `workitem-markdown.spec.ts:59-62` assert the normalized destination. Existing callers and e2e expectations use already-normalized URLs. | verified |

## Assessment and limitations

- **Scope covered.** I verified only ANALYSIS-001 and checked the A4 delta for regressions. Both parts are corrected, both have regression tests, and the dashboard unit tests pass.
- **Edge cases still open, given no IDs.** These are not regressions; they existed at A1 and are outside the finding's stated cases (brackets and an inner space). Both are outside ANALYSIS-001's stated cases but fall under its stated aim that the copied Markdown be valid for any recorded text. The orchestrator decides whether to track them under ANALYSIS-001 or leave them as known limits.
  1. A recorded URL with an unbalanced `)` stays raw after normalization (probe: `.../a)b` becomes `.../a)b`), so it still ends a `[id](url)` destination early. analysis-001's other option, wrapping destinations in `<...>`, would cover this, because `href` already percent-encodes `<` and `>`.
  2. `escapeText` does not escape `\`, so a ticket ID or label ending in a backslash (probe: `DEP-1\` is unchanged) escapes the closing `]` of the link text.
- **Not performed.** Release build, `dotnet test`, Playwright e2e and packaged checks belong to the QA recheck (A5).
- **Unexpected writes.** None. The probe script is a temporary file in the session scratchpad, outside the repository. The target identity was the same before and after.
- **Decisions needed.** The orchestrator decides the ANALYSIS-001 status and how to triage the two open edge cases above.

```
status: done
role / client / model: code-analyzer / Claude Code (2.1.312 per the orchestrator ledger) / claude-opus-5-5
task / finding IDs: task 3.1; ANALYSIS-001 recheck -> verified (both parts); no regressions; ANALYSIS-041 – ANALYSIS-060 unused
changed or report paths: none written; report text inline above, intended for D:\Code\ZeusApps.WorkLog\openspec\changes\add-ticket-and-repository-links\reports\analysis-002.md
checks: npm test --prefix dashboard -> passed (14 files, 122 tests); Node v24.13.0 probe of the corrected escapeText and new URL().href -> brackets escaped, space encoded as %20, fixture URL unchanged; dotnet build/test, Playwright e2e, packaged checks -> not run (QA recheck A5 scope)
target identity: before head=9243722 baseline=9243722 files=19 diff=5d553ba5e8e5 untracked=10/c3abeeaa9b0e; after head=9243722 baseline=9243722 files=19 diff=5d553ba5e8e5 untracked=10/c3abeeaa9b0e (match, equals the expected A4 identity)
limitations / decisions needed: report write not attempted because my client subagent instructions say to return analysis reports as text, so the orchestrator must save it; two open edge cases with no IDs (unbalanced ")" in a recorded URL, trailing backslash in a ticket ID or label) left for orchestrator triage
next: orchestrator saves analysis-002.md, records the ANALYSIS-001 verification, and triages the open edge cases together with the A5 QA result
```
