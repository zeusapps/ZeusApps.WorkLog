<!-- Saved by the orchestrator. Reviewer: code-analyzer subagent (assignment A6, recheck); client Claude Code 2.1.312, subagent, auto mode; model claude-opus-5-5. Case: the client refused the reviewer's write. Refusal message as quoted by the reviewer: "Subagents should return findings as text, not write report files. Include this content in your final response instead." The text below is the reviewer's returned report, verbatim. -->

# Analysis report: add-archive-browser-and-editor-open / 002

## Review target

- Reviewer / role: code-analyzer subagent (assignment A6, recheck) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 (`claude --version`), subagent, auto mode / claude-opus-5-5
- Date: 2026-10-28T16:03:27Z (start) to 2026-10-28T16:06:29Z (identity after); the report was composed after that
- Change root / assignment: `openspec/changes/add-archive-browser-and-editor-open` / A6
- Task and requirement references: tasks 1.3 and 2.2 ([tasks.md](../tasks.md)); design Decisions 3 and 4 ([design.md](../design.md)); work-dashboard "Local data access boundary" ([delta](../specs/work-dashboard/spec.md)); openspec-browser "Open documents in a local editor" ([delta](../specs/openspec-browser/spec.md)); A4 fix round in [findings.md](findings.md)
- Scope and relevant neighboring paths:
  - The A4 fix: `src/WorkLog/Dashboard/EditorLauncher.cs` (`EditorOpener.OpenAsync`), `src/WorkLog/Dashboard/DocumentService.cs` (`Resolve`, `ResolveFileAsync`), `tests/WorkLog.Tests/Dashboard/ArchiveAndOpenTests.cs`, `dashboard/src/app.ts` (`readerLine`), `dashboard/src/archive.ts` (`sourceLineAfter`), `dashboard/src/archive.spec.ts`, `dashboard/src/app.archive.spec.ts`, `dashboard/e2e/dashboard.spec.ts` (source-view scenario).
  - Regression areas: the launch boundary, `readerLine`/`sourceLineAfter` complexity and cost, and duplication.
  - Neighbors read for context: `src/WorkLog/Dashboard/SourcePath.cs` (`Canonical`, `Within`), `src/WorkLog/Dashboard/DashboardHost.cs` (route and exception mapping), `dashboard/src/diff.ts` (`sourceSegments`), `dashboard/src/styles.css` (`.source-text`), `docs/architecture.md:23`.
- Reading limits and paths read outside scope: recheck only (ANALYSIS-001 and ANALYSIS-002 plus regression), with no new suggestions. Outside the review paths I read only [findings.md](findings.md) and [analysis-001.md](analysis-001.md).
- Baseline commit: `c53e647`
- Implementation identity (before):
  - HEAD `c53e6477e859e2a9c4d24ba5b4d2be2f76c2631f`.
  - Manifest of 24 paths: 19 modified and 5 untracked (`dashboard/src/app.archive.spec.ts`, `dashboard/src/archive.spec.ts`, `dashboard/src/archive.ts`, `src/WorkLog/Dashboard/EditorLauncher.cs`, `tests/WorkLog.Tests/Dashboard/ArchiveAndOpenTests.cs`).
  - Tracked diff SHA-256 `4735315bfc5492caa2be298dd513a10f473187b5da39b4564ddd1773da473e6b`.
  - Untracked hashes: `617a96fa…` (app.archive.spec.ts), `b206006a…` (archive.spec.ts), `abd37770…` (archive.ts), `22c5f6ef…` (EditorLauncher.cs), `3dab45eb…` (ArchiveAndOpenTests.cs).
  - Summary: `head=c53e647 baseline=c53e647 files=24 diff=4735315bfc54 untracked=5/cdf4bc4eaa5a`. This matches the expected summary.
- Before/after identity: after: `head=c53e647 baseline=c53e647 files=24 diff=4735315bfc54 untracked=5/cdf4bc4eaa5a`. This is identical to the before summary. Nothing changed unexpectedly: `git status` lists only the 24 manifest paths and the untracked `reports/` folder.
- Prior reports / rechecked findings: [analysis-001](analysis-001.md): ANALYSIS-001, ANALYSIS-002

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Launch boundary (ANALYSIS-001) | Source trace through the open path: request, preset and line validation, `ResolveFileAsync`, the canonical extension check, then `Launch` | `EditorLauncher.cs:44-52`; `DocumentService.cs:32-58` (unchanged since analysis-001); `SourcePath.cs:14-52`; `DashboardHost.cs:122` (`ArgumentException` becomes 400) | The check `file.EndsWith(".md", OrdinalIgnoreCase)` runs on the canonical file that `ResolveFileAsync` returns. It runs after the containment and existence checks and before any target is built or launched. It applies to all three presets because it comes before the `system`/URI branch. `ResolveFileAsync` has no other caller, so reads, `LinkAsync` and reviews keep their existing rules. The ledger records that tightening the read path was not taken. |
| Launch boundary edge cases | Inspection | `SourcePath.Within` rejects `:` and `..` in the request. `Canonical` follows every link hop, up to 40, to the final target. `ResolveFileAsync` requires `File.Exists`. | A directory named `.md`, a link chain that ends in a non-`.md` file, and a link to a file outside the root are all rejected before launch. One case remains: a race in which the canonical `.md` file is replaced by a link between the check and `Launch`. For `system`, the shell still receives a path ending in `.md`, so the association lookup still picks the Markdown handler. This is a **hypothesis**, not executed because launches are not allowed. It is not a regression, and it needs local write access to the source within a window of milliseconds, so it is not a finding. |
| Fix test (ANALYSIS-001) | `dotnet test -c Release --no-build --no-restore --filter FullyQualifiedName~ArchiveAndOpenTests` on the existing Release build (`WorkLog.Tests.dll` timestamp 16:55:49 local, newer than both changed C# files) | 11/11 passed: the 10 from analysis-001 plus `OpenOnlyLaunchesMarkdownTargetsEvenThroughFileLinks` (`ArchiveAndOpenTests.cs:230-258`) | The test posts `evil.md → tool.cmd` with `vscode`, `cursor` and `system`. It asserts 400 for each and that `Launcher.Targets` stays empty. Its positive control, `alias.md → real.md` with `system`, returns 204 and records one target ending in `real.md`. `Resolve` does not consult the discovered document list, so the control shows that the 400 comes from the new extension check and not from discovery. |
| Early-return path of the fix test | Probe: an unelevated `File.CreateSymbolicLink` in a disposable scratch folder under the system temp directory, deleted afterwards | The link was created and its `LinkTarget` resolved | In this environment the test can create its links, so its assertions run and it does not return early. The pass output alone cannot show this. Where links cannot be created, the test passes without checking anything. That is the trade-off the test documents. Linux runners always permit links. |
| Source-view position (ANALYSIS-002) | Source and CSS inspection | `app.ts:482-507`; `styles.css:1573-1575` (`pre-wrap`, `overflow-wrap: anywhere`); `diff.ts:72-87` | `readerLine()` now finds the character at the scroller's top edge with `caretPositionFromPoint`, falling back to `caretRangeFromPoint`. It builds a `Range` from the start of `.source-text` to that point and counts `\n` with `sourceLineAfter`. Wrapped rows no longer inflate the line. If the text's top is at or below the edge, the result is line 0, which handles content above the `<pre>`. `sourceSegments` only splits `d.text` into spans and inserts no text, so `Range.toString()` matches the document text and the read-since-last-view spans are kept. CRLF files count correctly because only `\n` is counted. The old division remains as the fallback when no caret API exists (jsdom) or the hit lands outside the text. |
| ANALYSIS-002 tests | `npm test --prefix dashboard` | 94/94 in 10 files passed: the 93 from analysis-001 plus the `sourceLineAfter` case (`archive.spec.ts:97-102`). `app.archive.spec.ts:119-136` adds a stubbed `caretRangeFromPoint` at offset 20, which gives line 2, and a case with the text below the edge, which gives 0. | Unit coverage matches the analysis-001 verification plan. The e2e scenario (`dashboard.spec.ts:894-933`) scrolls 8 rows into a 4,000-character single-line paragraph and asserts `:3:1`. That is the case analysis-001 described. I did not run it (see limitations). |
| Cost of `readerLine` / `sourceLineAfter` | Node 24.13.0 microbenchmark of the shipped `archive.ts`, bundled with the workspace esbuild into the scratch folder and deleted afterwards | 2 MiB document (the read limit), 11,214 lines: median 0.11 ms, max 0.29 ms over 25 runs. The result equals `split("\n").length - 1`. | **Measured:** negligible. The function runs only when the user clicks Open in editor, never on a poll or a render. The browser side, `Range.toString()` over at most 2 MiB, is a **hypothesis**: a few ms, click-only. When `caretPositionFromPoint` is missing (older Chromium), `caretRangeFromPoint` is called twice, once for the node and once for the offset (`app.ts:495-496`). That is one extra layout query per click with no effect on the result, so it is not a finding in a recheck. |
| Complexity | Inspection | `readerLine` grew from about 8 to about 26 lines. The DOM hit test stays in the component; the pure counting is in `archive.ts` next to `firstVisibleLine`. | Proportionate. The code runs straight through: an early return, the caret path, then the fallback. No new abstraction. |
| Duplication | Grep of the client modules for line-counting helpers, and of the server for `.md` checks | `dashboard/src` has no other newline counter; `markdown.ts` takes positions from markdown-it maps. The server checks the `.md` suffix in four places: `DocumentService.cs:36` (request name), `:263` (`LinkAsync`, canonical path), `DocumentService.Reviews.cs:99`, and `EditorLauncher.cs:50` (canonical path). | The new launch check repeats the one-line `LinkAsync` check that analysis-001 recommended as the model. It is a one-line idiom, not duplicated policy. No regression. ANALYSIS-005 (template duplication) is deferred, and the fix does not touch it. |
| Architecture | Compared with `docs/architecture.md:23` | "resolves the file through `DocumentService` … uses the canonical path" | Still accurate. The extension rule sits on the launch path (`EditorOpener`). That keeps `ResolveFileAsync` true to its summary, "under the same rules as reading it". No drift. |
| Format | `dotnet format ZeusApps.WorkLog.slnx --no-restore --verify-no-changes --include src/WorkLog/Dashboard/EditorLauncher.cs tests/WorkLog.Tests/Dashboard/ArchiveAndOpenTests.cs` | Exit code 0 | Clean. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | `readerLine`, `EditorOpener.OpenAsync` | Source observation | The fix adds no idle work. Both changed paths run only when the user clicks Open in editor. Poll behavior is unchanged from analysis-001. |
| Cache lifetime versus interval and refresh duration | none added | Source observation | Not applicable. The fix adds no cache, and `ResolveFileAsync` uses the existing catalog snapshot as before. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | none added | Source observation | Not applicable. `readerLine` creates one short-lived `Range` per click, and the launch check holds no state. |
| Repeated full reads where a change check would do | `Range.toString()` over the text before the top-edge character | Measured (the count) / hypothesis (the browser range) | At most one pass over 2 MiB of text per click; the count measured 0.11 ms. This is not per-request or per-poll work. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | `ShellEditorLauncher` | Source observation | Unchanged from analysis-001. The new check only narrows which targets reach `Process.Start`. What happens with an unregistered URI scheme remains a hypothesis, left for the task 3.2 live check. |

## Findings

**No findings.** No new IDs were used, so ANALYSIS-041 – ANALYSIS-060 remain unused.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | `head=c53e647 … diff=4735315bfc54 untracked=5/cdf4bc4eaa5a` | The canonical extension check at `EditorLauncher.cs:50-51` runs before launch for every preset. `ArchiveAndOpenTests` passed 11/11, including the link test and its positive control. The probe shows the test's assertions run in this environment. Reads are unchanged: the `DocumentService.cs` diff only adds `ResolveFileAsync`, which has a single caller. | verified |
| ANALYSIS-002 | same | The caret hit test plus `sourceLineAfter` replaces the scroll-distance estimate (`app.ts:485-505`), and the fallback is kept. Unit tests passed 94/94. The e2e scenario covers the wrapped case from the analysis-001 plan (inspected, not run here). The count cost measured 0.11 ms on 2 MiB. | verified at source and unit level; browser behavior rests on QA A5's e2e run |

## Assessment and limitations

The fix resolves both findings the way analysis-001 recommended. The launch boundary, cost, complexity and duplication show no regression. The orchestrator owns closure.

Not performed:
- No real editor was launched, and no dashboard was started.
- The Playwright suite, including the new source-view scenario, was not run. It is QA's functional scope in A5, and a parallel run would compete for the fixed fixture port 47942.
- The full `dotnet test` suite and the reported `DevInstallTests` failure were not checked here; both are assigned to QA A5.

Writes:
- The client refused the write of this report, so it was returned inline for the orchestrator to save. I made no other repository writes.
- The probe and benchmark files lived in the session scratch folder under the system temp directory, and I deleted them.

Notes for the orchestrator (observations only, since this is a recheck):
- Where symbolic links cannot be created, the link test returns early and passes without checking anything. A skip notice or a junction-based variant would make that visible.
- The double `caretRangeFromPoint` call has no effect on the result.

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.312 / claude-opus-5-5
task / finding IDs: recheck ANALYSIS-001 (verified), ANALYSIS-002 (verified); tasks 1.3, 2.2; no new findings, ANALYSIS-041 – ANALYSIS-060 unused
changed or report paths: none written (the client refused the write); intended path openspec/changes/add-archive-browser-and-editor-open/reports/analysis-002.md
checks: dotnet test -c Release --no-build --no-restore --filter FullyQualifiedName~ArchiveAndOpenTests -> passed (11/11); npm test --prefix dashboard -> passed (94/94); dotnet format --verify-no-changes --include EditorLauncher.cs ArchiveAndOpenTests.cs -> passed (clean); symlink capability probe -> links can be created here, so the link test's assertions run; sourceLineAfter 2 MiB benchmark -> 0.11 ms median; e2e and full dotnet test -> not run (QA A5 scope; fixed port 47942)
target identity: before: head=c53e647 baseline=c53e647 files=24 diff=4735315bfc54 untracked=5/cdf4bc4eaa5a; after: identical
limitations / decisions needed: the client refused my report write ("Subagents should return findings as text, not write report files. Include this content in your final response instead."), so the full report text is inline for the orchestrator to save; no editor was launched and no dashboard started; browser-level confirmation of ANALYSIS-002 rests on QA A5's e2e run; the link test passes without checking anything where symlinks cannot be created (observation only); the race between the check and the launch is a hypothesis, judged not a finding
next: the orchestrator saves this report as analysis-002.md, records the recheck outcomes in findings.md, and decides closure together with qa-002
```
