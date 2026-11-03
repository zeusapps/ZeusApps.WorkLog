<!-- Saved by the orchestrator (Claude Code, Opus 5.5, session f406ff60). Reviewer: code-analyzer subagent, assignment X2; client Claude Code (version not exposed to the agent), subagent mode, light recheck; model claude-sonnet-5-5 (recorded override: docs and test-helper recheck). Case: the client refused the reviewer's write of this report. Exact refusal message as returned by the reviewer: "Subagents should return findings as text, not write report files. Include this content in your final response instead." The text below is the reviewer's returned report, saved verbatim. -->

```
status: done
role / client / model: code-analyzer / Claude Code (version not exposed to the agent) / claude-sonnet-5-5
task / finding IDs: X2 light recheck / ANALYSIS-151 rechecked (verified); ANALYSIS-161 to ANALYSIS-170 unused (no new findings)
changed or report paths: none written. Intended path D:\Code\ZeusApps.WorkLog\openspec\changes\add-day-digests\reports\analysis-012.md; the write was refused and the report text is below.
checks: source reading only (git diff against abe5d5d, grep of dashboard/src and dashboard/e2e); builds, tests and e2e -> not run (QA owns execution)
target identity: before head=abe5d5d baseline=abe5d5d files=7 diff=d9493c4d0bed untracked=0/e3b0c44298fc ; after identical, no unexpected mutation
limitations / decisions needed: the report write was refused and the report text is inline below. The orchestrator saves it to the path above. No decisions needed.
next: the orchestrator saves the report and updates ledger status for ANALYSIS-151.
```

---

# Analysis report: add-day-digests / X2 light recheck

## Review target

- Reviewer / role: code-analyzer (X2) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (version not exposed to the agent) / subagent, read-only checks / claude-sonnet-5-5
- Date: 2026-11-03 (date only; time not recorded)
- Change root / assignment: `openspec/changes/add-day-digests` / X2, finding IDs ANALYSIS-161 to ANALYSIS-170
- Task and requirement references: [tasks.md](../tasks.md) 5.6; [design.md](../design.md) Decision 7
- Scope and relevant neighboring paths: `docs/releases.md` (Updates bullet, manual `0.3.9001` bullet, Release v0.4.0 manual-install paragraph), `docs/installation.md` (day-digests section, ~126-131), `dashboard/e2e/dashboard.spec.ts` (`putState` and its call sites), `dashboard/src/app.ts` ~1920-1950 (`remember`, `persist`).
- Reading limits and paths read outside scope: did not read `reports/qa-014.md`. Read [analysis-011.md](analysis-011.md) (own prior report) for ANALYSIS-151 only. Also grepped `dashboard/src/*.ts` for unload-time flush handlers and `dashboard/e2e/*.ts` for other `/api/state` writers. No other out-of-scope reads.
- Baseline commit: `abe5d5d`
- Implementation identity (before): `head=abe5d5d baseline=abe5d5d files=7 diff=d9493c4d0bed untracked=0/e3b0c44298fc` (tracked diff SHA-256 `d9493c4d0bed20b8b35d2f4c0d3228caebae860535f0290b234dc9cde058cb55`).
- Before/after identity: after the analysis the summary line is identical (`head=abe5d5d baseline=abe5d5d files=7 diff=d9493c4d0bed untracked=0/e3b0c44298fc`). No unexpected mutation.
- Prior reports / rechecked findings: [analysis-011.md](analysis-011.md): ANALYSIS-151.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-151 correctness | Read `docs/releases.md` manual `0.3.9001` bullet step (1) in the diff against `abe5d5d` | Step (1) now reads "a Switch of a new dev build selects another payload"; the "(or a stable update ...)" and "install that manually" clauses are gone. Step (1) no longer routes through the pre-`a51a14a` exactly-two-tools probe, because Switch runs the repository's own `PayloadInstaller` via `tools/WorkLog.DevInstall`, not the running payload's update client. | Executable as written. |
| Coherence with Updates bullet exception | Compared with the Updates bullet | The exception says a pre-`a51a14a` payload (including the manual `0.3.9001`) declines a stable v0.4.0+ in `worklog update`, and directs the reader to "Switch to a new dev build or install v0.4.0 manually". Step (1) uses the first of those two routes, and the second remains in the exception and in the manual-install paragraph. | Consistent. |
| Steps (2) and (3) | Read the rest of the bullet | After step (1) the selected payload is a Switch-built dev build from the current repository, so it carries the new probe; "a further stable update or Switch" in step (2) is therefore valid, and step (2) replaces both `previous` and the recovery selection, after which step (3) Retire is accepted. The closing "A Rollback in between does not help" sentence is unchanged and still true. | Consistent. |
| `docs/installation.md` ~131 | Read the day-digests section | States the same payload set (pre-`a51a14a` payloads, manual `0.3.9001`, dev builds `3c7dfd1` to `a51a14a`) and that dev builds installed with `install-dev.ps1 Switch` from the current repository are not affected. Matches the Updates exception and the v0.4.0 paragraph in `releases.md`. | Consistent. |
| `putState` correctness | Read the diff, `app.ts` `remember`/`persist`, grep for `pagehide`/`beforeunload`/`visibilitychange`/`sendBeacon`/`keepalive` in `dashboard/src` | The only writer is the 350 ms `saveTimer` in `remember`; there is no unload-time flush. `page.goto("about:blank")` unloads the app, so the pending timer is dropped and cannot overwrite the later PUT. `page.request` shares the context cookies and `baseURL` that the previous helper used. The PUT is asserted 204 and read back with `toMatchObject(JSON.parse(JSON.stringify(data)))`, which normalizes `undefined` and keeps `null` such as `repositories: null`. | Correct for the intended race. |
| `putState` call sites | Read all four sites | `reload()` became `goto("/ongoing")` (the page is now at `about:blank`, so reload would be wrong); the Daily `open` already navigated to `/daily` afterwards. `reset` now returns `stateWith(...)` with the assertions inside `putState`. | Correct. |
| Duplication | Compared `stateWith` in the Ongoing board and Daily describe blocks | Both are identical one-line wrappers over `putState`; they were already duplicated before this change (identical `page.request.put` bodies in `abe5d5d`). `putState` itself removes the repeated 204 assertion and reload. | Pre-existing, not introduced; not raised. |
| Performance | Not applicable | Docs and a test helper; no benchmark. | n/a |

## Steady-state cost and bounds

The target is documentation text and a Playwright helper. It has no timer, poll, cache, or accumulating component (the debounce timer belongs to the unchanged app source).

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | none in target | n/a | Not applicable (docs and test helper). |
| Cache lifetime versus interval and refresh duration | none in target | n/a | Not applicable. |
| Unbounded state | none in target | n/a | Not applicable. |
| Repeated full reads | none in target | n/a | Not applicable. |
| External tool discovery | none in target | n/a | Not applicable. |

## Findings

**No findings.**

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-151 | `head=abe5d5d baseline=abe5d5d files=7 diff=d9493c4d0bed untracked=0/e3b0c44298fc` | `docs/releases.md` step (1) now names only a Switch of a new dev build. Cross-read against the Updates bullet exception, steps (2) and (3), the closing Rollback sentence, the Release v0.4.0 manual-install paragraph and `docs/installation.md` ~131; no contradiction, and the procedure no longer depends on a stable update through a declining probe. | verified (orchestrator owns ledger status) |

## Assessment and limitations

- Scope covered: ANALYSIS-151 plus a regression read of the changed docs text and the `putState` helper with its four call sites.
- Residual note, not a finding: `putState` cannot cancel a save request already sent before `about:blank` is reached. If such a PUT lands after the helper's read-back it could still overwrite the seeded state. The window is a single in-flight request after a 350 ms debounce, and every call site starts right after `authorize` or a completed navigation, so this is unlikely in practice; it would show up as a flaky seed mismatch, not a silent pass.
- Not performed: no build, test, e2e, or package run (QA owns execution); the e2e helper was judged by source reading only.
- Unexpected writes: none; the identity summary is identical before and after. The report write was refused by the client and not retried.
- Decisions needed: none. Triage and ledger status belong to the orchestrator.
