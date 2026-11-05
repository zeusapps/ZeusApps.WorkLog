# Findings: improve-dashboard-reading-and-reliability

Orchestrator-owned ledger. Planning and implementation are separate; unchecked tasks remain pending until their specified evidence passes.

## Current orchestrator

- Client / version: Codex / version not exposed
- Session identity: codex-dashboard-reading-apply-20261105
- Since (UTC): 2026-11-05T10:08:44Z
- Previous owner: none

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| DEV-001 | developer | Codex / role default gpt-6-luna | none | 1.1-1.4; baseline ac6d7f0; final C05 identity files=3 diff=0b62e486064e untracked=0/e3b0c44298fc | ignored artifacts/dashboard-reading-reliability/c05/ | none; return issues to orchestrator | returned; C05 acceptance and independent QA 002 passed |
| DEV-002 | developer / same writer | Codex / role default gpt-6-luna | none | 2.1-6.3 in agreed order; assignment identity files=3 diff=4a5e0243900c, updated handoff files=3 diff=0b62e486064e, untracked=0/e3b0c44298fc | ignored artifacts/dashboard-reading-reliability/ | none; return issues to orchestrator | returned; implementation handoff completed by subsequent scoped assignments |
| DEV-003 | developer / same writer | Codex / role default gpt-6-luna | none | bounded C03 corrections before build/checks; identity files=22 diff=8126a8273f02 untracked=2/06b5b26cb701 | ignored artifacts/dashboard-reading-reliability/ | none; in-flight corrections, no independent review yet | returned; shared diagnostic and recovery changes independently covered |
| DEV-004 | developer / same writer | Codex / role default gpt-6-luna | none | ORCH-001, task 3.1 test compilation; identity files=22 diff=ddc1e7096c6a untracked=2/06b5b26cb701 | dashboard/src/app.board.spec.ts only | none | returned; ORCH-001 independently verified in QA 002 |
| DEV-005 | developer / same writer | Codex / role default gpt-6-luna | none | 5.1 isolated benchmark harness only; baseline ac6d7f0; assignment identity files=22 diff=9440a4cea3e7 untracked=2/06b5b26cb701 | scripts/benchmark-review-comparison.mjs; ignored comparison evidence | none | returned; before/after harness evidence complete |
| DEV-006 | developer / same writer | Codex / role default gpt-6-luna | none | ORCH-002, ORCH-003; C03 digest-read boundary and contract assertions; scoped identity files=4 diff=98cb5931d528 untracked=0/e3b0c44298fc | DigestStore.cs, DailyDigestResponseTests.cs, SourceCatalogTests.cs | none | returned; backend QA 001 verified corrections |
| DEV-007 | developer / same writer | Codex / role default gpt-6-luna | none | ORCH-004 / 4.3, 6.3; baseline ac6d7f0; scoped identity files=1 diff=0614539e1119 untracked=0/e3b0c44298fc | dashboard/e2e/dashboard.spec.ts | none | returned; current normal/package reader scenarios passed; final QA pending |
| DEV-008 | developer / same writer | Codex / role default gpt-6-luna | none | ORCH-003 test recovery setup; scoped identity files=1 diff=e9277f4c4340 untracked=0/e3b0c44298fc | tests/WorkLog.Tests/Dashboard/SourceCatalogTests.cs | none | returned; backend QA 001 verified recovery fixture |
| DEV-009 | developer / same writer | Codex / role default gpt-6-luna | none | ORCH-004 plus named 2.1/2.5 component coverage; scope files=3 diff=b7e8269dfee2 untracked=0/e3b0c44298fc | dashboard/e2e/dashboard.spec.ts, app.reviews.spec.ts, app.daily.spec.ts | none | returned; units and normal/package browser acceptance passed |
| DEV-010 | developer / same writer | Codex / role default gpt-6-luna | none | named 2.3 startup negative/network/timer coverage; scope files=1 diff=c68036d8b134 untracked=0/e3b0c44298fc | dashboard/src/app.reliability.spec.ts | none | returned; startup regression cases passed in 268-unit suite |
| DEV-011 | developer / same writer | Codex / role default gpt-6-luna | none | ORCH-005, ORCH-006; scoped target files=3 diff=0200ee1eea6a untracked=0/e3b0c44298fc | dashboard/src/app.reliability.spec.ts, styles.css; app.html only if necessary for responsive structure | none | returned; units and six viewport/theme cases passed |
| DEV-012 | developer / same writer | Codex / role default gpt-6-luna | none | 6.3 evidence hygiene; scoped target files=1 diff=a364eef11f16 untracked=0/e3b0c44298fc | dashboard/e2e/dashboard.spec.ts reader screenshot target only | none | returned; cropped synthetic screenshot output passed browser capture |
| DEV-013 | developer / same writer | Codex / role default gpt-6-luna | none | ORCH-007 / 4.2, 4.4; scope files=3 diff=e9a148d36644 untracked=0/e3b0c44298fc | dashboard/src/app.ts focus handling only | none | returned; keyboard browser case passed; later DEV-016 adds newer-input guard |
| DEV-014 | developer / same writer | Codex / role default gpt-6-luna | none | 5.2-5.3, implementation of 5.4 browser behavior; baseline ac6d7f0; files=24 diff=b2c89d381df4 untracked=3/16c9bd9c4903 | focused dashboard comparison, parser, reader fallback and regression paths | none; planned implementation, no independent review yet | returned; bounded comparison and token parser implemented; helper p95 target met |
| QA-REVIEW-001 | QA | Codex / role default gpt-6-luna | none | stable C03 backend, 2.2/2.4/2.5 server; ORCH-002/003; files=9 diff=f736d5e40c9f untracked=0/e3b0c44298fc | reports/qa-001.md; ignored reviews/qa-001-backend.log | QA-001 through QA-019 | returned; no findings; 72/72 focused tests and hygiene passed |
| ANALYSIS-REVIEW-001 | code-analyzer | Codex / role default gpt-6.1-sol high | none | stable C03 backend, 2.2/2.4/2.5 server; files=9 diff=f736d5e40c9f untracked=0/e3b0c44298fc | reports/analysis-001.md; ignored reviews/analysis-001-backend.log | ANALYSIS-001 through ANALYSIS-019 | returned; no production blocker; ANALYSIS-001 deferred suggestion |
| DEV-015 | developer / same writer | Codex / role default gpt-6-luna | none | ORCH-008 / 5.2 empty-document browser precondition; scoped files=1 diff=7fca43ac5056 untracked=0/e3b0c44298fc | dashboard/e2e/dashboard.spec.ts only | none | returned; current normal/package large and empty fallback passed |
| DEV-016 | developer / same writer | Codex / role default gpt-6-luna | none | ORCH-007 latest-input focus regression investigation; scope files=2 diff=5fa418d7670b untracked=0/e3b0c44298fc | dashboard/src/app.ts, app.reliability.spec.ts only | none | returned; newer-input regression reproduced before fix; current 268 units passed |
| DEV-017 | developer / same writer | Codex / role default gpt-6-luna | none | 6.1 documentation draft; baseline ac6d7f0; README/docs initially unchanged (diff e3b0c44298fc) | README.md, docs/architecture.md, docs/dashboard-validation.md, docs/mcp-validation.md | none | returned; documentation draft finalized by DEV-020/021 |
| DEV-018 | developer / same writer | Codex / role default gpt-6-luna | none | ORCH-009 / 3.2, 4.4, 6.2; current browser assertion target recorded in handoff | dashboard/e2e/dashboard.spec.ts only | none | returned; full normal/package suites passed after path selector correction |
| DEV-019 | developer / same writer | Codex / role default gpt-6-luna | none | ORCH-009 / 4.2, 6.2; browser diff d45c42057574 | dashboard/e2e/dashboard.spec.ts only | none | returned; full normal/package suites passed after outline preconditions |
| DEV-020 | developer / same writer | Codex / role default gpt-6-luna | none | 6.1 final validation counts; docs identity files=4 diff=093f7c30519d untracked=0/e3b0c44298fc | README.md, docs/architecture.md, docs/dashboard-validation.md, docs/mcp-validation.md | none | returned; actual counts added; ORCH-010 label correction assigned separately |
| DEV-021 | developer / same writer | Codex / role default gpt-6-luna | none | ORCH-010 / 6.1; docs identity files=4 diff=e35d6b8bf51d untracked=0/e3b0c44298fc | docs/dashboard-validation.md, docs/mcp-validation.md | none | returned; ORCH-010 independently verified by QA/analysis 002 |
| DEV-022 | developer / same writer | Codex / role default gpt-6-luna | none | QA-020 / 2.1; first independent fix cycle; whole target 32 files diff=11c3f4732c55 untracked=3/16c9bd9c4903 | dashboard/src/app.ts, dashboard/src/app.reviews.spec.ts only | none | returned; QA-020 independently verified in QA/analysis 003 |
| DEV-023 | developer / same writer | Codex / role default gpt-6-luna | none | 6.1 final correction-round counts; docs identity files=2 diff=45f4cce7f68a untracked=0/e3b0c44298fc | docs/dashboard-validation.md, docs/mcp-validation.md only | none | returned; final documentation verified in QA/analysis 003 |
| QA-REVIEW-002 | QA | Codex / role default gpt-6-luna | none | staged final review: stable implementation files=28 diff=00fcf593bdd8 untracked=3/16c9bd9c4903, then finalized docs and full identity supplied before report | reports/qa-002.md; reports/comparison-before.json; reports/comparison-after.json; screenshots/qa-002-reader-{390,768,1440}-{dark,light}.png; screenshots/qa-002-reader-1440-dark-all-documents.png; ignored reviews/qa-002/ | QA-020 through QA-039 | accepted; ORCH-001 through ORCH-010 verified; QA-020 required cycle 1 |
| ANALYSIS-REVIEW-002 | code-analyzer | Codex / role default gpt-6.1-sol high | none | staged final review: stable implementation files=28 diff=00fcf593bdd8 untracked=3/16c9bd9c4903, then finalized docs and full identity supplied before report | reports/analysis-002.md; ignored reviews/analysis-002/ | ANALYSIS-020 through ANALYSIS-039 | accepted; ANALYSIS-020 deferred; QA-020 corroborated |
| QA-REVIEW-003 | QA | Codex / role default gpt-6-luna | none | QA-020 plus regression/current hygiene; cycle 1 source target files=2 diff=b5e7fc93006e untracked=0/e3b0c44298fc; initial whole target d9f5e6948847; final docs supplied before report | reports/qa-003.md; ignored fix-1/qa/ | QA-040 through QA-059; no new suggestions in recheck | accepted; QA-020 verified; current hygiene passed; final target 6ad03108e2ee |
| ANALYSIS-REVIEW-003 | code-analyzer | Codex / role default gpt-6.1-sol high | none | QA-020 plus regression/docs correctness/safety; cycle 1 source files=2 diff=b5e7fc93006e untracked=0/e3b0c44298fc; initial whole target d9f5e6948847 | reports/analysis-003.md; ignored fix-1/analysis/ | ANALYSIS-040 through ANALYSIS-059; no new suggestions in recheck | accepted; QA-020 verified; no new findings; final target 6ad03108e2ee |

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ORCH-001 | Orchestrator dashboard unit execution | 3.1 / source-qualified copy selection | warning | no: independently verified | verified | orchestrator | QA 002, matching target 11c3f4732c55; prior correction verified |
| ORCH-002 | Orchestrator Release .NET suite; [QA 001](qa-001.md) | 2.5 / digest failure visibility | warning | no: independently verified | verified | orchestrator | QA 001, backend diff f736d5e40c9f, 72/72 focused tests |
| ORCH-003 | Orchestrator Release .NET suite; [QA 001](qa-001.md) | 2.2, 2.5 / warning and additive response contracts | warning | no: independently verified | verified | orchestrator | QA 001, backend diff f736d5e40c9f, 72/72 focused tests |
| ORCH-004 | Orchestrator reader browser acceptance | 4.3, 6.3 / narrow selector keyboard behavior | warning | no: independently verified | verified | orchestrator | QA 002, matching target 11c3f4732c55; prior correction verified |
| ORCH-005 | Orchestrator dashboard unit compilation | 2.3 / startup fixture | warning | no: independently verified | verified | orchestrator | QA 002, matching target 11c3f4732c55; prior correction verified |
| ORCH-006 | Orchestrator reader acceptance and screenshot | 4.3, 6.3 / responsive geometry | warning | no: independently verified | verified | orchestrator | QA 002, matching target 11c3f4732c55; prior correction verified |
| ORCH-007 | Orchestrator keyboard browser acceptance | 4.2, 4.4, 6.3 / navigation focus | warning | no: independently verified | verified | orchestrator | QA 002, matching target 11c3f4732c55; prior correction verified |
| ANALYSIS-001 | [Backend analysis 001](analysis-001.md#analysis-001-synchronize-stderr-completion-before-asserting-diagnostic-counts) | 2.4 / protocol test synchronization | suggestion | no: source-observed maintenance opportunity, no observed failure | deferred | unassigned | shared protocol cases and deterministic suppression tests passed; future P09 follow-up |
| ORCH-008 | Orchestrator comparison browser acceptance | 5.2 / empty-document fallback | warning | no: independently verified | verified | orchestrator | QA 002, matching target 11c3f4732c55; prior correction verified |
| ORCH-009 | Orchestrator full normal browser acceptance | 3.2, 4.4, 6.2 / current-document selectors | warning | no: independently verified | verified | orchestrator | QA 002, matching target 11c3f4732c55; prior correction verified |
| ORCH-010 | Orchestrator documentation review | 6.1 / truthful evidence labels | warning | no: independently verified | verified | orchestrator | QA 002, matching target 11c3f4732c55; prior correction verified |
| QA-020 | [QA 002](qa-002.md#qa-020-batch-read-marking-gives-network-specific-recovery-guidance-for-missing-documents); [analysis 002](analysis-002.md) corroborates | 2.1 / actionable batch read failures | warning | no: independently verified | verified | orchestrator | QA 003 and analysis 003, final target 6ad03108e2ee; 270 independent unit tests |
| ANALYSIS-020 | [Analysis 002](analysis-002.md#analysis-020-bound-each-isolated-server-health-request-during-startup) | 1.1 / disposable fixture startup | suggestion | no: resilience opportunity without an observed hang | deferred | unassigned | add per-health-request startup deadline to P09 test maintenance when touched |

## Finding details and history

### QA-020: Generic batch read failure has unsupported connectivity recovery advice

- 2026-11-05 closure: independently verified by [QA 003](qa-003.md) and [analysis 003](analysis-003.md) at final whole-target 6ad03108e2ee. The 404/503 component regressions reproduce the old error and pass with neutral refresh/check/retry guidance. Full final acceptance passed; no new findings. First independent fix/recheck cycle completed; no second cycle required.

- Source: [independent QA 002](qa-002.md#qa-020-batch-read-marking-gives-network-specific-recovery-guidance-for-missing-documents), source inspection; analysis 002 corroborates.
- Expected / actual: failed marking has accurate recovery guidance; `markChangeRead` tells every non-409/non-401 failure to wait for a restored connection, including missing document 404 and storage errors.
- Triage: warning that breaks task 2.1 acceptance; required fix. Preserve successful fingerprints, drafts, conflict distinction, authorization stop and one aggregate outcome.
- Next: developer fix after both initial reports are immutable; first independent fix/recheck cycle. Cover actual stale/missing document and storage/network failures with meaningful component regression.
- 2026-11-05: QA 002 and analysis 002 saved with matching final before/after identity 11c3f4732c55. Developer source freeze released for DEV-022 only. Neutral actionable recovery across unknown causes is accepted; status-specific wording is optional, while unsupported connectivity claims are not. QA 002 records 32 total files; its phrase 32 modified tracked files is a count-label error (29 tracked plus 3 untracked), with correct identity summary confirmed in the reviewer return. Preserve the report; next report must label counts precisely.
- 2026-11-05: DEV-022 added two component regressions; actual red run failed those two while 268 existing cases passed. The wording correction then passed 270/270 across 20 files. Source is frozen at two-file diff b5e7fc93006e, whole current diff d9f5e6948847. QA-020 enters awaiting-verification; root rebuilt Release with zero warnings/errors and is refreshing package/browser evidence before final recheck reports. Only the generic hint and related tests changed; suggestions remain deferred.

### ANALYSIS-020: Disposable fixture health probe needs an elapsed-time bound

- Source: [independent analysis 002](analysis-002.md#analysis-020-bound-each-isolated-server-health-request-during-startup), source inspection without a reproduced hang.
- Triage: suggestion, nonblocking; no hang observed. Attempt count does not bound elapsed startup if an individual fetch never completes.
- Deferral: P09 test-maintenance follow-up, estimated 0.25-0.5 engineer-day; value is faster, more reliable diagnosis of hung fixture startup. Reconsider when changing startup probes or after a reproduced nonresponse hang.
- Action: no fix or recheck under the suggestion severity gate. A future change should bound each probe/request deadline and verify owned-process cleanup under injected nonresponse.

### ORCH-010: Documentation incorrectly expands the packaged bootstrap check into default smoke coverage

- Severity: warning; blocks accurate task 6.1 documentation until independently verified.
- Expected / actual: the actual `--updates-only` run passed in 3.55 seconds; DEV-020 also claims a separate default isolated smoke passed, but that default command was not run in this change.
- Action: DEV-021 remove the unsupported default-smoke claim in both validation docs. Associate timing and sandbox retry only with the actual `--updates-only` command; preserve historical default-smoke records from earlier changes.
- Verification: final Stage B documentation review by QA and code analyzer. No additional smoke execution is necessary to repair this factual label.

### ORCH-009: Existing browser assertions reference the removed horizontal tabs

- Severity: warning; blocking required full browser acceptance; assigned to developer.
- Evidence: final normal browser run failed at the first `.document-tabs .current` assertion; 43 later cases did not run. Four such assertions remain in the test file.
- Action: use the new persistent current-document identity with exact relative paths, preserving default selection, nested selection and navigation expectations. Do not add obsolete product markup or weaken final assertions.
- Verification: full normal Release and fresh packaged no-Node browser acceptance, followed by independent QA. This is initial implementation validation, not an independent-review fix cycle.
- 2026-11-05: full rerun passed the first nine cases, including default/explicit selection and out-of-order refresh, then timed out reaching a heading-copy action inside the newly collapsed On this page section. DEV-019 must audit all existing outline actions and add the required disclosure precondition, preserving all anchors, clipboard and editor-line expectations.

### ORCH-001: New route fixture requires an iterable DOM type absent from the current compiler libraries

- Location: `dashboard/src/app.board.spec.ts:42` at the DEV-003 handoff.
- Expected / actual: the new copy-selection fixture compiles with the existing project configuration; dashboard unit compilation instead fails with TS2769 because URLSearchParams is not an Iterable in that configuration.
- Correction: construct the route query object with supported existing types, preserving the test scenarios and project configuration.
- Verification: rerun the dashboard unit suite; independent final QA must cover this correction. No source behavior, requirement, or compiler-library expansion is needed.
- 2026-11-05: assigned to DEV-004 after the orchestrator's normal sandbox unit command produced the compiler error. No unit cases executed in that failed attempt.
- 2026-11-05: DEV-004 used URLSearchParams.forEach to construct the fixture without a compiler configuration change. The orchestrator reran the dashboard unit suite in the normal sandbox: 20 files and 262 tests passed. Independent final QA must verify the current target before closure.

### ORCH-002: Invalid digest-store file type is reported as normal absence

- Location: `src/WorkLog/Persistence/DigestStore.cs`, read boundary; `DailyDigestResponseTests.MissingStoreIsNormalAndUnreadableStoreHasASafeIssue`.
- Expected / actual: a missing store is normal, while a directory occupying the store-file path is unavailable; File.Exists returns false for both and the current response has no issue for the invalid type.
- Action: DEV-006 minimal read-boundary correction; preserve normal absence, journal access and readonly reads. No repair or migration. Cover recovered reads as well as failure.
- Verification: rerun focused contracts and full required .NET suite; independent QA remains required.
- 2026-11-05: assigned after the Release suite reported this failure.

### ORCH-003: New tests assert narrower contracts than the agreed behavior

- Locations: `DailyDigestResponseTests.cs:94` requires the issue property to be absent even though the design allows absent or null; `SourceCatalogTests.cs:66` matches old colon punctuation instead of the new warning and retained-configuration meaning.
- Expected / actual: preserve missing/null compatibility and verify one persistent actionable configuration warning; the two tests currently fail despite the intended optional shape and updated wording.
- Action: DEV-006 align assertions with the agreed semantics, retaining warning counts, recovery and response checks. Do not narrow behavior to satisfy tests.
- Verification: focused contracts and independent final QA.
- 2026-11-05: assigned after the Release suite finished with 424 passed, 3 failed, none skipped; ORCH-002 accounts for the third failure.
- 2026-11-05: the next implementation check passed 426/427, with only configuration fixture recovery still failing: it constructs new settings while its own deliberately invalid JSON is still present. DEV-008 must restore valid disposable fixture bytes before testing recovery. No production settings overwrite is permitted. These are implementation checks before the initial independent QA/analysis; independent review fix-and-recheck rounds have not started.

### ORCH-004: Reader acceptance tries to focus the closed mobile selector

- Location: `dashboard/e2e/dashboard.spec.ts:1265` at scoped diff `0614539e1119`.
- Expected / actual: selection closes the mobile document selector and returns focus; the subsequent Escape test must reopen it before focusing its input. The scenario instead waits for a removed input and hits its 60-second timeout.
- Action: DEV-007 correct the precondition without weakening focus, Escape, geometry, theme or document-count assertions.
- Verification: rerun the reader scenario against the rebuilt app; independent QA must cover it on the final target.
- 2026-11-05: assigned after the scenario reached the 390px dark screenshot and failed at the next input-focus step.
- 2026-11-05: after the selector precondition fix, the trace confirmed input Escape and selector dismissal passed; the next wait is a click on the background light-theme button while the reader covers it. DEV-009 must change themes before opening the reader. All viewport/theme, focus and navigation expectations remain required.

### ORCH-008: Empty-comparison browser fixture must select its intended initial view

- Location: `dashboard/e2e/dashboard.spec.ts:1045` at scoped diff `7fca43ac5056`.
- Evidence: the large-document rendered/source fallback and change navigation passed; after closing it in Source mode, the next document opens in that same mode and has no `.markdown` element.
- Action: DEV-015 explicitly choose Read mode before the empty-document rendered-view assertions. Preserve mode continuity in product code and all empty/source/navigation expectations.
- Verification: rerun comparison and reader browser scenarios against the current rebuilt app.

### ANALYSIS-001: Bounded stderr delivery barrier for protocol test diagnostics

- Source: [analysis-001.md](analysis-001.md#analysis-001-synchronize-stderr-completion-before-asserting-diagnostic-counts).
- Triage: suggestion, nonblocking. Both protocol labels and deterministic category suppression checks pass; the report identifies an ordering gap without a reproduced flake or production failure.
- Deferral: add to test-maintenance follow-up P09; reconsider before expanding MCP concurrency coverage or when a real stderr timing failure is observed. Expected value is more repeatable diagnostic evidence, not measured product performance.
- Action: no fix or recheck under the suggestion severity gate; preserve the report and both protocol fixture cases.
- 2026-11-05: recorded as deferred after initial backend analysis; no independent fix/recheck cycle was needed.

### ORCH-007: Loading a document overrides explicit rail focus

- Location: `dashboard/src/app.ts` loadDocument completion callback; keyboard browser assertion at `dashboard/e2e/dashboard.spec.ts:1320`.
- Expected / actual: activating a rail document by Enter preserves the active navigation control; after content loads, the existing unconditional different-document focus moves to the reader container.
- Action: DEV-013 retain connected navigation control focus, including the mobile selector opener, while preserving default focus for opening from outside the reader and existing epoch/anchor behavior.
- Verification: keyboard draft/position scenario, all geometry/theme cases, unit regressions and independent final reviews.

### ORCH-005: Startup fixture does not expose its existing router event stream

- Location: `dashboard/src/app.reliability.spec.ts:236`; test helper return value.
- Expected / actual: the new subscription-count test receives the fixture's existing Subject; compilation fails with TS2339 because the helper does not return that field.
- Action: DEV-011 expose the same fixture stream without changing product behavior or compiler configuration.
- Verification: current dashboard unit suite and final independent QA.

### ORCH-006: Reader toolbar causes horizontal overflow at the tablet viewport

- Locations: reader header/actions styles and `dashboard/e2e/dashboard.spec.ts:1263`.
- Evidence: 10/30/50 document selection and both 390px themes completed, then the strict reader scrollWidth/clientWidth assertion failed at 768px. The screenshot shows header text squeezed beside a nonshrinking action row with controls running beyond the available width.
- Action: DEV-011 responsive flow/wrapping correction; keep actions visible, keyboard reachable, and relative document identity readable. Hiding overflow or controls is not an acceptance fix.
- Verification: all six viewport/theme cases, keyboard draft/position scenario, and current independent reviews.

## Implementation history

- 2026-11-05: correction-round execution is complete: clean Release build, .NET 427/427 with no skips, dashboard 270/270 (also independent QA), full normal browser 44/44 in 4.8 minutes, fresh actual no-Node packaged browser 44/44 in 2.3 minutes with no exclusions, and packaged `--updates-only` bootstrap/MCP protocol/EOF/offline checks passed. Logs are in ignored `artifacts/dashboard-reading-reliability/fix-1/`. DEV-023 is updating only final validation documentation before current reports are saved.

- 2026-11-05: all implementation and documentation writes are frozen. Final whole-target identity: HEAD/baseline ac6d7f0; 32 implementation files; tracked diff SHA-256 `11c3f4732c553341eea72cdb885e9b4d17c2357c3a68e89e15d910c40a112b46`; three untracked implementation files aggregate `16c9bd9c4903`. Stage A source-only identity remains files=28 diff=00fcf593bdd8 untracked=3/16c9bd9c4903; backend slice f736d5e40c9f unchanged. Both final reviewers received the current whole target for Stage B and must verify unchanged identities before reporting.

- 2026-11-05: fresh actual self-contained 0.4.1 no-Node browser acceptance passed 44/44 in 2.6 minutes, including declared-store behavior without exclusions. Independent QA separately reran dashboard units: 268/268 across 20 files passed. Final normal/package logs are in ignored output; source target remains frozen at files=28 diff=00fcf593bdd8 untracked=3/16c9bd9c4903 while DEV-020 fills actual documentation outcomes. Task 6.2 acceptance passed.

- 2026-11-05: full normal Release browser acceptance passed 44/44 in 3.8 minutes on the final product and corrected reader test preconditions, with no exclusions/skips. This covers current navigation/history, source access, archived documents, missing targets, comparison fallbacks, geometry, drafts, board colors/order, themes and Daily behavior. Tasks 3.2/4.4 now have integrated regression evidence; final packaged browser acceptance and independent reviews remain. Raw ignored log: `artifacts/dashboard-reading-reliability/final-normal.log`.

- 2026-11-05: the final Release rebuild passed with zero warnings/errors, full .NET tests passed 427/427 with zero skips, and dashboard units passed 268/268. The deferred-response focus regression failed before the last guard and passed after it. Full browser acceptance remains a separate gate.
- 2026-11-05: unchanged benchmark harness/environment repeated all eight workloads with 3 warmups and 30 samples each. Full-rewrite p95 before/after at 50/200/400/800 KiB: 15.8704/16.786, 485.1853/51.5529, 1554.8551/51.0624, 3005.1003/51.973 ms. All small edits remained precise; 200/400 KiB rewrites now use honest fallback, while 800 KiB was already incomplete. Maximum helper p95 meets the 100ms target; this excludes rendering, sanitizing, review-token extraction and browser latency.
- 2026-11-05: fresh self-contained 0.4.1 validation package built under ignored output without installation or publication. Packaged update/bootstrap smoke passed outside the sandbox: self-contained launcher/version, MCP protocol/EOF and offline status. The sandbox attempt timed out; no live-agent evaluation was performed.

- 2026-11-05: the first repeated Daily run exposed shared writes in the stale-digest and digest-save scenarios as well as the observation-write scenario. DEV-001 will isolate every existing Daily journal/digest writer using the same scoped fixture context. Task 1.1 and design wording were clarified to satisfy the already-required ten-repeat acceptance without weakening assertions or changing product scope.
- 2026-11-05: the orchestrator took over focused command execution after a developer cleanup tool call stalled. Current packaged no-Node store acceptance passed (1/1), and normal store/History/link/copy checks passed across three repetitions (12/12). Final cleanup regression found a false null-exitCode assertion after a signal exit; DEV-001 is correcting lifecycle tracking before current-target repetition. Evidence remains in ignored output; no acceptance or independent review is inferred from this intermediate state.
- 2026-11-05: the orchestrator reran the full Daily group after exit-event correction: 80/80 passed in 6.9 minutes, one parent fixture lifetime, implementation diff `1ae6f9eaa6c0`. Final exception-path correction assigned: retain owned fixture data when server stop fails rather than deleting potentially live SQLite/WAL data. This leaves successful behavior unchanged; C05 review evidence will map the correction explicitly before completion. OpenSpec CLI 1.13.1 was exercised in normal mode.
- 2026-11-05: the orchestrator inspected the final exception-path correction, identity over the three C05 files `0b62e486064e`: both cleanup paths remove data only after stop/port verification; a failed stop retains data with an owned-process diagnostic. Successful lifecycle behavior is unchanged from the 80/80 run. Tasks 1.1-1.4 are reconciled with actual fixture execution and inspection; independent final QA and analysis remain required. DEV-002 began C03.
- 2026-11-05: task 5.1 fresh baseline completed before any comparison edits: eight workloads, three warmups and 30 samples each. Raw ignored `artifacts/dashboard-reading-reliability/c04/baseline-before.json` records actual helper/harness hashes and reference environment. Full-rewrite p95 at 50/200/400/800 KiB: 15.87/485.19/1554.86/3005.10 ms; all 30 largest rewrites were incomplete. Small-edit p95 stayed below 3.3 ms. Git verified diff.ts, markdown.ts and reviews.ts unchanged from ac6d7f0 before and after measurement. This measures readMarks normalization, line comparison and result processing, excluding Markdown rendering/browser work.
- 2026-11-05: task 1.3 follow-up preserved original final row/link cardinality assertions after the new discriminating waits. The orchestrator's focused rerun passed 12/12 (three repetitions each, 28.8s) against the baseline Release product; this is C05 harness evidence, not validation of the in-flight C03/C02/C01 product changes.

## Completion assessment

- Current reports: [QA 003](qa-003.md) and [analysis 003](analysis-003.md) accepted on the same final before/after target, carrying unchanged scoped coverage from rounds 001/002.
- Final reviewed implementation identity: HEAD/baseline ac6d7f0; 32 total files (29 tracked plus 3 untracked); tracked diff SHA-256 `6ad03108e2eee35c36aec2a941ef5c1693d2dd2a37f86f36c320d9714e84c3d1`; untracked aggregate `16c9bd9c4903`. Correction scope b5e7fc93006e, backend f736d5e40c9f, comparison helper/harness and three new-file hashes are unchanged from the accepted reports.
- Acceptance: locked restore passed; final Release build has zero warnings/errors; .NET 427/427 with no skips; dashboard 270/270, also independently run by QA; normal browser 44/44 in 4.8 minutes; fresh actual packaged no-Node browser 44/44 in 2.3 minutes, no exclusions; packaged `--updates-only` bootstrap/MCP protocol/EOF/offline checks passed. C05 Daily repetition 80/80 in one parent lifetime and repeated History 12/12 are carried on unchanged harness behavior.
- Reader evidence: 10/30/50 documents, full paths and filtering, both themes at 390/768/1440px, keyboard/draft/position preservation and independent rail scroll passed. Seven allocated synthetic screenshots and complete before/after comparison JSON are retained in this change.
- Performance: maximum helper p95 51.973ms meets the 100ms target; largest rewrite improved from 3005.1003ms. The comparison-helper timing excludes rendering, sanitization, removed-comment token extraction and browser latency. The documented whole-document fallback trades exact rewrite detail for bounded work.
- Findings: all ORCH-001 through ORCH-010 and QA-020 independently verified; no unresolved blocker or required deferral. One independent fix/recheck cycle completed; no second cycle started.
- Nonblocking follow-ups: ANALYSIS-001 and ANALYSIS-020 remain deferred to P09 with sources, rationale, estimated effort/value and reconsideration conditions. They were not fixed or rechecked as part of the correction round.
- Scope/evidence limits: no native-client/live-agent evaluation was planned; paired shared-server protocol fixtures cover both integrations. Source version remains 0.4.1. Publication, push, installation, archive and merge are separate actions.
- OpenSpec: 23/24 tasks checked; strict validation passed. Only commit recording remains before the final checkbox can be checked.
- Commit: pending scoped implementation commit on `change/improve-dashboard-reading-and-reliability`; review artifacts/checklist/ledger changes are excluded from implementation identities and receive their own hygiene checks.
