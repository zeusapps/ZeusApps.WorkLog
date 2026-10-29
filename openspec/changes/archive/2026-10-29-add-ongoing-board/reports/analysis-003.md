<!--
Saved by the orchestrator (Claude Code 2.1.312, session 1570e404-00dd-4689-9952-af0e082ed1e3) on 2026-10-29.
Reviewer: A9 code-analyzer, Claude Code 2.1.312, agent subagent (non-interactive), claude-opus-5-5.
Case: the client refused the reviewer's write. Exact refusal message as reported by the reviewer: "Subagents should return findings as text, not write report files. Include this content in your final response instead." Text below is the reviewer's report verbatim.
-->

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.312 (as recorded in findings.md) / claude-opus-5-5
task / finding IDs: recheck ANALYSIS-041 (verified), ANALYSIS-002 leftover (partly verified); new ANALYSIS-061 (warning); ANALYSIS-062 – 080 unused
changed or report paths: none written. Intended path D:\Code\ZeusApps.WorkLog\openspec\changes\add-ongoing-board\reports\analysis-003.md. The write was refused; the full report text is below
checks: npm test --prefix dashboard -> passed (16 files, 189/189); System.Text.Json encoding probe (PowerShell 7, .NET 10.0.12, outside the repository) -> run; e2e / packaged / dotnet -> not run (QA A8 owns functional validation; no C# changed this round)
target identity: before head=607df5f baseline=607df5f files=20 diff=8d6264af661d untracked=7/a623002741a1; after head=607df5f baseline=607df5f files=20 diff=8d6264af661d untracked=7/a623002741a1 (match, as expected)
limitations / decisions needed: the client refused the report write with the exact message "Subagents should return findings as text, not write report files. Include this content in your final response instead." The report text is inline for the orchestrator to save. Decision for ANALYSIS-061: make the server count the received bytes (or use relaxed escaping), or have the client count the default encoder's escapes or keep a margin
next: orchestrator saves the report and triages ANALYSIS-061
```

---

# Analysis report: add-ongoing-board / analysis-003

## Review target

- Reviewer / role: code-analyzer subagent (assignment A9, recheck of fix round 2) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312 (version as recorded in [findings.md](findings.md), not independently observed) / agent subagent, non-interactive / claude-opus-5-5
- Date: 2026-10-29T06:48Z
- Change root / assignment: `openspec/changes/add-ongoing-board` / A9
- Task and requirement references: tasks 2.1, 2.2, 3.1, 3.2 ([tasks.md](../tasks.md)); [spec delta](../specs/work-dashboard/spec.md) "Ongoing board" (one card per change, columns from recorded facts) and "Bounded dashboard preferences" (preferences fit the storage limit)
- Scope and relevant neighboring paths: `dashboard/src/board.ts`, `board.spec.ts`, `app.ts`, `app.html`, `app.board.spec.ts`, `dashboard/e2e/dashboard.spec.ts`. Neighbors read: `src/WorkLog/Dashboard/SourceCatalog.cs` (how copies and source IDs arise), `src/WorkLog/Dashboard/DashboardState.cs` and `DashboardHost.cs:143–144` (the server's size check), `dashboard/src/api.ts`, `dashboard/src/models.ts` (preference shape)
- Reading limits and paths read outside scope: recheck of ANALYSIS-041 and the ANALYSIS-002 leftover, plus regression. Outside the listed files: the neighbors above only
- Baseline commit: `607df5f`
- Implementation identity (before): `head=607df5f baseline=607df5f files=20 diff=8d6264af661d untracked=7/a623002741a1`. Tracked diff SHA-256 `8d6264af661dba4290438a51454a9a7433489b19215c27801b0c02ea87ff9d0a`. Untracked: `app.board.spec.ts` `705d147c…`, `board.spec.ts` `1821eb27…`, `board.ts` `6a41ad03…`, `OngoingUnlinkedTests.cs` `a47f11d1…`, screenshots `fd0b5fb6…`, `e2d533b7…`, `a891a466…`. Matches the expected value.
- Before/after identity: after the analysis, `head=607df5f baseline=607df5f files=20 diff=8d6264af661d untracked=7/a623002741a1`. It matches, with no unexpected mutation; `git status --short` has 23 lines, as before.
- Prior reports / rechecked findings: [analysis-002](analysis-002.md): ANALYSIS-041 and the ANALYSIS-002 leftover; [analysis-001](analysis-001.md): ANALYSIS-002

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-041: lead copy | Inspected `board.ts:89–129` and `SourceCatalog.cs:139–175, 273` | Copies are sorted by most ticked tasks, then by `sourceId`. Active changes are directories under one `openspec/changes`, and source IDs are dictionary keys, so two active copies with one change key always have different `sourceId`s. The comparator is therefore a total order, and the result does not depend on input order | Verified |
| ANALYSIS-041: documents | Inspected `board.ts:102–103, 124`, `app.html` card documents | The card's documents are the union of all copies by path, first copy wins, in sorted copy order. The template now uses `card.documents` | Verified |
| ANALYSIS-041: chooser | Inspected `app.ts` `openCard` | With a path, only copies that contain it are offered. With one such copy, that copy opens directly. `copyPath` is reset on each `openCard` call. A path always exists in at least one copy, because the documents are a union | Verified |
| ANALYSIS-002 leftover: `fitOrder` | Inspected `board.ts:316–321`, `app.ts` `persist()` (lines 1825–1835), `DashboardState.cs:26–33` | The client pops the lowest ranks until the UTF-8 length of `JSON.stringify(preferences)` is at most 65,536. The server checks the UTF-8 length of `JsonObject.ToJsonString()`, a re-serialization with the default encoder | The client and server measure different things (ANALYSIS-061) |
| Encoding probe | PowerShell 7 on .NET 10.0.12 (the project targets `net10.0`): `JsonNode.Parse(...).ToJsonString()` on sample text | `&`, `+`, `<`, `>` and non-ASCII characters come back as `\uXXXX`: 86 client bytes become 118. A document of exactly 65,536 client bytes, with a `lastRoute` of the shape the app writes (`?source=…&change=…&doc=…&variant=…`), becomes 65,551 server bytes | Measured. The server rejects it with 413 |
| Regression: unit tests | `npm test --prefix dashboard` | 16 files, 189/189 passed (184 before, plus the new merge, fit and chooser cases) | Passed |
| Regression: e2e | Inspected the `openTimeline` chooser step (`e2e/dashboard.spec.ts:38–52`) | The fixture's change has `design.md` in both copies, so the chooser still appears for the Design button, as the helper expects | e2e not run. QA A8 owns functional validation |
| Regression: chooser tracking | Inspected `app.html:813` | `track copy.variant` is unchanged, so ANALYSIS-042 (deferred) still applies to merged copies | No change; no new finding |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Client `poll()`, `fullBoard` | Source observation | Unchanged from analysis-002. The copy sort and the document union are linear in the copies of each change. No new timer, request or process |
| Cache lifetime versus interval and refresh duration | None added | Source observation | Not applicable: the corrections add no cache |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | `board.order`, the preferences document | Source observation plus measured encoding probe | Bounded at 200 entries and, on the client's measure, at 65,536 bytes. The client's measure can be lower than the server's (ANALYSIS-061) |
| Repeated full reads where a change check would do | `persist()` | Source observation | Each save serializes the preferences one more time to measure them. When the order must shrink, `fitOrder` serializes once per dropped key, at most 200 times and only while over the limit. The cost is small next to the debounced save |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | None | Source observation | Not applicable: the corrections add no tool lookup |

## Findings

### ANALYSIS-061: The client fits the order to its own byte count, which can be smaller than the server's count

- Severity: warning
- Related task / requirement: 2.2; "Bounded dashboard preferences" (preferences SHALL fit the storage limit); the ANALYSIS-002 leftover
- Locations: `dashboard/src/app.ts:1825–1835` (`persist()`, `bytes` = `TextEncoder` over `JSON.stringify`); `dashboard/src/board.ts:316–321` (`fitOrder` stops at the limit, with no margin); `src/WorkLog/Dashboard/DashboardState.cs:28–29` (`state.ToJsonString()`, then `UTF8.GetByteCount(...) > 65_536`)
- Observation:
  - The server does not measure the bytes it receives. It parses the body into a `JsonObject` and measures `ToJsonString()`. That method uses the default `JavaScriptEncoder`, which writes `&`, `+`, `<`, `>`, `'` and every non-ASCII character as a 6-byte `\uXXXX` escape. `JSON.stringify` writes them as is.
  - `fitOrder` keeps as many ranks as fit within 65,536 bytes by the client's count, so the result can land within a few bytes of the limit.
  - `lastRoute` holds `router.url`. For an open document that URL has three `&` (`source`, `change`, `doc`, `variant`), which adds 15 bytes. The probe gave 65,551 server bytes for 65,536 client bytes. Non-ASCII text in the history ticket, or in a document path, adds 4 bytes for each 2-byte character.
  - When this happens, the save fails with 413 and the one-time "Preferences could not be saved" notice appears. The board order was already shortened in memory. The tests (`board.spec.ts:406–417`, `app.board.spec.ts:109–122`) measure with the client's count and ASCII filler, so they do not catch this.
- Proposed improvement and benefit: use the same count on both sides. Two bounded options:
  1. Server: check the size of the body that was received (or of a re-serialization with `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, which does not escape these characters). The client's count then matches for ordinary text.
  2. Client: count each character the default encoder escapes as 6 bytes, or fit to a fixed margin below 65,536 (for example 60 KiB).

  Either option keeps the fitted order accepted. Option 1 also removes the gap for preferences outside the board.
- Behavior-preservation risks: option 1 changes a size check that is shared by all preferences. Its existing tests for 64 KiB rejection need to keep passing, and the stored file can then hold up to 64 KiB of unescaped text. `Read` limits the file length to 65,536 bytes, so the file written must stay within that limit. Option 2 drops a few more ranks than needed.
- Evidence type: measured result (encoding probe on .NET 10.0.12). That a fitted document lands within 15 bytes of the limit is a source observation; how often that happens with real data is a hypothesis
- Verification / measurement plan: an `app.board.spec.ts` case whose preferences include a `lastRoute` with `&` and a non-ASCII ticket, and which asserts that the saved body is within the limit by the server's count. Alternatively, a `DashboardState` test that writes a document of exactly 65,536 client bytes containing `&` and expects it to be accepted.
- Relationship: arises from the ANALYSIS-002 leftover correction (fix round 2)

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-041 | `diff=8d6264af661d untracked=7/a623002741a1` | `board.spec.ts:381–404` passes. Three input orders give the same lead copy, stage, task count, copy order and documents, and a 9/9 copy makes the card Done whatever the order. Source check: `sourceId` is unique among active copies of one change key, so the tie-break is total. `app.board.spec.ts:124–135` passes: with `tasks.md`, which only one copy has, that copy opens without the chooser; with `design.md`, both copies are offered | Verified. When two copies have the same number of ticked tasks but different totals, the copy with the lower `sourceId` decides the stage. That is deterministic, as approved |
| ANALYSIS-002 (leftover: byte pruning) | same | `fitOrder` drops the lowest ranks and keeps the order's prefix (`board.spec.ts:406–417`). `persist()` applies it to the whole document and updates `boardOrder` (`app.board.spec.ts:109–122`). Both measure with the client's count. The server's count is larger whenever the document contains `&`, `+`, `<`, `>`, `'` or non-ASCII text (measured) | Partly verified. Pruning works by the client's count, but the server's count can still exceed the limit (ANALYSIS-061) |

## Assessment and limitations

ANALYSIS-041 is resolved. The merged card's lead copy, column, task count and documents no longer depend on catalog order, and the chooser offers only copies that contain the requested document. The byte pruning for the ANALYSIS-002 leftover is in place, but it measures differently from the server. A document fitted to the limit can still be rejected (ANALYSIS-061, warning). Fixing that needs a decision: change the server's count, or keep a margin on the client. Unit tests pass 189/189.

Not performed: e2e and packaged runs (QA A8 owns them), and `dotnet` checks (this round changed no C#). The encoding probe ran in PowerShell on the local .NET 10 runtime, outside the repository, and did not start the dashboard. One write was attempted, for this report, and the client refused it; no repository file was written. The orchestrator owns triage and ledger status.
