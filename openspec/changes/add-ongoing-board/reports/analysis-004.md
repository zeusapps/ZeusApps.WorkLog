<!--
Saved by the orchestrator (Claude Code 2.1.312, session 1570e404-00dd-4689-9952-af0e082ed1e3) on 2026-10-29.
Reviewer: A11 code-analyzer, Claude Code 2.1.312, agent subagent (non-interactive), claude-opus-5-5.
Case: the client refused the reviewer's write. Exact refusal message as reported by the reviewer: "Subagents should return findings as text, not write report files. Include this content in your final response instead." Text below is the reviewer's report verbatim.
-->

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.312 (as recorded in findings.md) / claude-opus-5-5
task / finding IDs: recheck ANALYSIS-061 (verified); no new findings; ANALYSIS-081 – 090 unused
changed or report paths: none written. Intended path D:\Code\ZeusApps.WorkLog\openspec\changes\add-ongoing-board\reports\analysis-004.md. The write was refused; the full report text is below
checks: npm test --prefix dashboard -> passed (16 files, 190/190); System.Text.Json escaping and server-count probe (PowerShell 7, .NET 10.0.12, outside the repository) -> run; new test run in a disposable dashboard copy at 61,440 -> passed, at 65,536 -> failed (expected 65542 <= 65536); e2e / packaged / dotnet -> not run (light recheck, no C# changed, QA A12 owns functional validation)
target identity: before head=607df5f baseline=607df5f files=20 diff=a0c7514b1a9c untracked=7/c84e67b6aec0; after head=607df5f baseline=607df5f files=20 diff=a0c7514b1a9c untracked=7/c84e67b6aec0 (match, as expected)
limitations / decisions needed: the client refused the report write with the exact message "Subagents should return findings as text, not write report files. Include this content in your final response instead." The report text is inline for the orchestrator to save. Two notes on the new test are recorded as observations, not findings (see Re-verification); the orchestrator decides whether to act on them
next: orchestrator saves the report and triages ANALYSIS-061
```

---

# Analysis report: add-ongoing-board / analysis-004

## Review target

- Reviewer / role: code-analyzer subagent (assignment A11, light recheck of the A10 correction) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 (version as recorded in [findings.md](findings.md), not independently observed) / agent subagent, non-interactive / claude-opus-5-5
- Date: 2026-10-29T07:50Z
- Change root / assignment: `openspec/changes/add-ongoing-board` / A11
- Task and requirement references: task 2.2 ([tasks.md](../tasks.md)); [spec delta](../specs/work-dashboard/spec.md) "Bounded dashboard preferences"
- Scope and relevant neighboring paths: `dashboard/src/app.ts` (`preferencesByteLimit`, `persist()`), `dashboard/src/app.board.spec.ts` (new escaping case), `dashboard/src/board.ts` (`fitOrder`). Neighbors read: `src/WorkLog/Dashboard/DashboardState.cs` (server count), `DashboardHost.cs:143–144`, `SourceCatalog.cs` (how source IDs, variants and repository identities are formed), `src/WorkLog/Repositories/RepositoryRegistry.cs:54`, `dashboard/src/models.ts` (`Preferences`)
- Reading limits and paths read outside scope: recheck of ANALYSIS-061 plus regression in the touched lines. Outside the listed files: the neighbors above only
- Baseline commit: `607df5f`
- Implementation identity (before): `head=607df5f baseline=607df5f files=20 diff=a0c7514b1a9c untracked=7/c84e67b6aec0`. Tracked diff SHA-256 `a0c7514b1a9cc47e9cc04c123f83c4c43242a835d02242f542be1b330e98ce6a`. Untracked: `app.board.spec.ts` `385d20c4…`, `board.spec.ts` `1821eb27…`, `board.ts` `6a41ad03…`, `OngoingUnlinkedTests.cs` `a47f11d1…`, screenshots `fd0b5fb6…`, `e2d533b7…`, `a891a466…`. Matches the expected value.
- Before/after identity: after the analysis, `head=607df5f baseline=607df5f files=20 diff=a0c7514b1a9c untracked=7/c84e67b6aec0`. It matches, with no unexpected mutation; `git status --short` has 23 lines.
- Prior reports / rechecked findings: [analysis-003](analysis-003.md): ANALYSIS-061

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Correction | Inspected `app.ts:98–99, 1825–1835` | `persist()` fits the order with `fitOrder(order, bytes, preferencesByteLimit)`, where `preferencesByteLimit = 61440`. The count is still `TextEncoder` over `JSON.stringify` of the whole document. Only the constant changed; `fitOrder` is unchanged | Matches the user's decision (client margin) |
| Server encoder | .NET probe (PowerShell 7 on .NET 10.0.12, outside the repository), `JsonObject.ToJsonString()` per character | Escaped to 6 bytes: `&`, `+`, `<`, `>`, `'`, `` ` ``, `"`, U+007F, U+0001, every non-ASCII BMP character. An astral character becomes 12 bytes. `\` and `\n` stay 2 bytes. `/ % = ? : , - ~ # @ ! $ * ( ) [ ] { } \| ^ ;` stay 1 byte | Measured |
| Test faithfulness | Compared the test's counting loop (`app.board.spec.ts:137–140`) with the probe. Ran the case in a disposable copy of `dashboard/` under the system temp directory, with the saved body written to a file. Then measured that body with `JsonNode.Parse(...)`, set `schemaVersion`, and called `ToJsonString()` | Saved body: 61,408 client bytes. Test count 61,426; server count 61,426 (equal). The loop matches the encoder for `&+<>'`, the backtick, non-ASCII characters and astral characters. It undercounts a `"` inside a string (2 instead of 6) and U+007F (1 instead of 6). The test data contains neither | Faithful for this body and for the listed characters; see the notes under Re-verification |
| Test discrimination | Same disposable copy with `preferencesByteLimit = 65536` | The case fails with `expected 65542 to be less than or equal to 65536` (client 65,524 bytes, 132 ranks kept). At 61,440 it passes (104 ranks kept) | The test catches the old behavior, by a margin of 6 bytes |
| Margin versus realistic preferences | Source inspection of every string the preferences hold | Source IDs and variants are content hashes. Repository identities come from the remote, or are `local:<guid>`. Card keys are `change:<repositories>/<name>` or `adhoc:<id>`. `lastRoute` is `router.url`, whose query values Angular percent-encodes, so only its `&` separators are escaped (3 for an open document, +15 bytes). `history.ticket` and `timezone` are short (the `+` in `Etc/GMT+3` adds 5). Overhead per character: +5 for escaped ASCII; +4, +3 and +8 for 2-, 3- and 4-byte UTF-8 | The 4,096-byte margin absorbs about 819 escaped ASCII characters or 1,024 two-byte characters. Realistic overhead is tens of bytes. The margin is exceeded only when non-ASCII change directory names or document paths repeat across many of the 200 document keys, 100 position keys and 200 order keys. That is a hypothesis, and OpenSpec names are kebab-case ASCII in practice |
| Regression: unit tests | `npm test --prefix dashboard` | 16 files, 190/190 passed (189 before, plus the new case) | Passed |
| Regression: touched lines | Inspected the `app.ts` diff around the constant and `persist()`, and `app.board.spec.ts:109–143` | The earlier pruning case still asserts at most 65,536 client bytes, and it passes. `preferencesByteLimit` is used only in `persist()`. Between 61,440 and 65,536 client bytes, the order now loses ranks that the server would accept. That is the trade-off chosen with the margin | No regression |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | `persist()` | Source observation | Unchanged from analysis-003. The correction adds no timer, request or process |
| Cache lifetime versus interval and refresh duration | None | Source observation | Not applicable: no cache added |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `board.order`, the preferences document | Source observation plus measured probe | Bounded at 200 order entries. When an order exists, the document is also bounded at 61,440 client bytes. The server's count stays within 65,536 unless escaped characters add more than about 4 KiB (see the margin row) |
| Repeated full reads where a change check would do | `persist()` | Source observation | Unchanged: one extra serialization per save, plus one per dropped rank, only while the document is over the limit |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | None | Source observation | Not applicable: no tool lookup added |

## Findings

**No findings.** ANALYSIS-081 – ANALYSIS-090 are unused.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-061 | `diff=a0c7514b1a9c untracked=7/c84e67b6aec0` | A fitted body of 61,408 client bytes measures 61,426 bytes by the server's own re-serialization (.NET 10.0.12 probe). The old limit gives 65,542 bytes, which the server would reject. The margin covers the overhead of realistic preferences. The new test passes at 61,440 and fails at 65,536 | Verified, with two notes on the test (below) |

Notes on the new test. Per the recheck rules, they are observations, not findings:

- `persist()` overwrites the test's `lastRoute` (40 `&`) with `router.url`, which is `/ongoing` in the test setup, so the saved body contains none of those `&`. Only the `positions` key (`Ü`, `ï`, `&`, `+`, +18 bytes) exercises escaping, and the test tells the old limit from the new one by only 6 bytes. A change to the filler or to key lengths could make it pass against 65,536 again.
- The counting loop and the comment at `app.ts:98` do not list `"` or U+007F, which the default encoder also escapes to 6 bytes. This does not affect the current assertion.

## Assessment and limitations

ANALYSIS-061 is resolved by the client margin the user chose. Measured with the server's encoder, a fitted document from realistic preferences stays well within the server's 64 KiB count. Only heavy non-ASCII text in change names or document paths could exceed the margin. That is a hypothesis, and the server's count is unchanged. The two test notes above are left to the orchestrator's triage.

Not performed: e2e, packaged and `dotnet` checks (this is a light recheck, no C# changed, and QA A12 owns functional validation). The probes ran on the local .NET 10 runtime and in a disposable copy of `dashboard/` in the system temp directory; its `node_modules` junction was removed afterwards. No dashboard was started. One write was attempted, for this report, and the client refused it; no repository file was written. The orchestrator owns triage and ledger status.
