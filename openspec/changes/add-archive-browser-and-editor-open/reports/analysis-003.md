<!-- Saved by the orchestrator. Reviewer: code-analyzer subagent (assignment A9, light docs review); client Claude Code 2.1.312, subagent, auto mode; model claude-sonnet-5-5 (recorded cost-efficient override for a docs-only target). Case: the client refused the reviewer's write. Refusal message as quoted by the reviewer: "Subagents should return findings as text". The text below is the reviewer's returned report, verbatim. -->

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.312 (subagent, auto mode) / claude-sonnet-5-5
task / finding IDs: A9 (light review of docs/dashboard-validation.md) / ANALYSIS-061 to ANALYSIS-064 used; 065 to 070 unused
changed or report paths: none written (write refused). Report text is inline below for saving as D:\Code\ZeusApps.WorkLog\openspec\changes\add-archive-browser-and-editor-open\reports\analysis-003.md
checks: ./scripts/review-identity.ps1 (before and after) -> passed; git diff c53e647 -- docs/dashboard-validation.md and comparison with architecture.md, installation.md, EditorLauncher.cs, analysis-001/002, qa-001/002, findings.md -> passed (inspection); tests/e2e/ctx7/live checks -> not run (docs-only light review)
target identity: before head=c53e647 baseline=c53e647 files=24 diff=5143e137bb4a untracked=5/cdf4bc4eaa5a; after identical (matches expected)
limitations / decisions needed: the report write was refused, so the text is inline. Decisions for the orchestrator: ANALYSIS-061 (reword the DevInstallTests cause), ANALYSIS-062 (confirm the dashboard restart with the user, name the identity), ANALYSIS-064 (docs/installation.md is outside the target; decide whether to route it).
next: orchestrator saves the report, triages ANALYSIS-061 to ANALYSIS-064, and has a developer apply any accepted doc rewording in docs/dashboard-validation.md (and installation.md for 064); a recheck is only needed for 061 if accepted
```

---

# Analysis report: add-archive-browser-and-editor-open / 003

## Review target

- Reviewer / role: code-analyzer subagent (assignment A9) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.312, subagent, auto mode / claude-sonnet-5-5 (cost-efficient model for a docs-only target, as recorded by the orchestrator)
- Date: 2026-10-28T18:04Z (end)
- Change root / assignment: `openspec/changes/add-archive-browser-and-editor-open` / A9 (light review, docs only)
- Task and requirement references: tasks 1.4, 3.2, 3.3 ([tasks.md](../tasks.md)); [findings.md](findings.md) (A4 and A7 rows; 2026-10-28 live-observation and DevInstallTests entries)
- Scope and relevant neighboring paths: the change to `docs/dashboard-validation.md` since identity `diff=4735315bfc54` (task 3.2 live-observation record, task 3.3 packaged e2e row, DevInstallTests note)
- Reading limits and paths read outside scope: light review, correctness and safety only. Read outside the assigned file: `docs/architecture.md` and `docs/installation.md` (diffs), `README.md` (diff), `src/WorkLog/Dashboard/EditorLauncher.cs`, `scripts/install-dev.ps1` and `.claude/skills/install-dev-build/SKILL.md` (restart statements, via grep), `tools/WorkLog.DevInstall/Staleness.cs` (grep), [analysis-001.md](analysis-001.md), [analysis-002.md](analysis-002.md), [qa-001.md](qa-001.md), [qa-002.md](qa-002.md), [findings.md](findings.md)
- Baseline commit: c53e647
- Implementation identity (before): `head=c53e647 baseline=c53e647 files=24 diff=5143e137bb4a untracked=5/cdf4bc4eaa5a` (tracked diff SHA-256 `5143e137bb4a91942ef8997ba8dea083d8efa2ec09764f58f21b1564c4575ef5`; matches the expected value)
- Before/after identity: after the analysis the summary line is identical (`diff=5143e137bb4a untracked=5/cdf4bc4eaa5a`); no unexpected mutation
- Prior reports / rechecked findings: [analysis-001.md](analysis-001.md) and [analysis-002.md](analysis-002.md) were used as the measurement/hypothesis reference; no findings rechecked

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Consistency with `docs/architecture.md` | Diff of architecture.md read against the new doc section | The new section adds no architecture claims. The launch boundary and security checks are described in architecture.md and installation.md and are not restated differently | No contradiction found |
| Measurements versus hypotheses | Compared the new text with analysis-001 line 143 and the analysis-002 table rows (unregistered-scheme ShellExecute stays a hypothesis) | The new section makes no performance claim and states no outcome for an unregistered scheme. The 503 "Could not open Cursor. Choose another editor." appears only as a server test result (fake launcher), not as observed shell behavior | OK. The hypothesis is not presented as a measurement in the target file (see ANALYSIS-064 for a neighbor) |
| Format claims vs code | Compared the 1.4 decision sentence (`line+1`, column 1, forward slashes, percent-encoding except a leading drive) with `EditorLauncher.cs` `Uri()` | Matches | OK |
| Test counts | 94 of 94 in 10 files (10 `dashboard/src/*.spec.ts`); fixture e2e 15 passed + 1 failed + 6 not run = 22, and 21 of 21 with the exclusion; packaged 22 of 22; .NET 350 of 351 | Consistent with qa-002 (350 of 351) and findings.md (A1 packaged 21/21 and fixture 20/20; the third scenario added in A4 gives 21/22/22) | Not re-run (docs-only target) |
| Live observation provenance | Compared the task 3.2 paragraph with the 2026-10-28 ledger entry | See ANALYSIS-062 | Finding below |
| DevInstallTests note | Compared with qa-002 Investigation 1 | See ANALYSIS-061 | Finding below |
| Packaged e2e row | Compared with the ledger rows A4 and A7 | See ANALYSIS-063 | Finding below |
| Hygiene | Read the section for secrets, user or machine data, and test residue | None found. The operation-record path from the ledger is not copied into the doc. "maintainer" is a role, not an account name | OK |
| Internal duplication/staleness | Read the whole new section | The Cursor "unverified" statement appears in the decision text and again at the end of the live-observation paragraph, and both agree | OK |

## Steady-state cost and bounds

Not applicable to this target: the reviewed change is documentation only (no timer, poll, cache, accumulating state, or tool lookup). Runtime behavior of the feature is covered in [analysis-001.md](analysis-001.md) and [analysis-002.md](analysis-002.md).

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | none (docs) | n/a | Not applicable |
| Cache lifetime versus interval | none (docs) | n/a | Not applicable |
| Unbounded state | none (docs) | n/a | Not applicable |
| Repeated full reads | none (docs) | n/a | Not applicable |
| External tool discovery | none (docs) | n/a | Not applicable |

## Findings

### ANALYSIS-061: DevInstallTests note attributes the failure to in-flight edits, contradicting the clean-baseline reproduction

- Severity: warning
- Related task / requirement: 3.3
- Locations: `docs/dashboard-validation.md`, the `dotnet test` row of the validation table
- Observation: the row says the test "depends on files edited in `dashboard/src` by the in-flight work, not on this change's launch code", and in the same cell that the failure "was reproduced on a clean baseline worktree". Both cannot be the cause. [qa-002.md](qa-002.md) Investigation 1 shows the test fails on a clean `c53e647` checkout too, with "Source directory src\WorkLog changed after ...". The test compares input-directory and file modification times in whatever tree it runs in. The edits are only what made `dashboard/src` the newest item in this tree. [qa-001.md](qa-001.md) saw the same test pass, which the row also omits.
- Proposed improvement and benefit: reword to say the result depends on directory-versus-file modification-time ordering in the tree under test (it passed in one QA run, failed in another, and fails on a clean baseline), and is unrelated to this change. This keeps the evidence record from implying that the failure disappears once the in-flight work is committed.
- Behavior-preservation risks: none (documentation).
- Evidence type: source observation (qa-002 investigation; `tools/WorkLog.DevInstall/Staleness.cs:50` reads last-write times)
- Verification / measurement plan: re-read the reworded cell against qa-002.
- Relationship: none

### ANALYSIS-062: Live-observation record states details the ledger does not record and omits the identity it applies to

- Severity: suggestion
- Related task / requirement: 3.2
- Locations: `docs/dashboard-validation.md`, "Live observation (task 3.2)"
- Observation: three points.
  - (a) The text says the maintainer "restarted their dashboard"; the ledger entry says the user installed the build via Switch and reported the result, with no restart. The install scripts restart nothing (install-dev-build SKILL.md rule 2), and a running dashboard keeps its old payload, so a restart is needed in practice, but it is an inference rather than a recorded fact.
  - (b) "built from the reviewed working tree" does not name the identity. The ledger gives `diff=4735315bfc54`; the tree is now `5143e137bb4a` (docs only), so the code the user observed is the current code, but a reader cannot tell.
  - (c) The text does not say which document was opened. Task 3.2 asks for a fixture document through the packaged build; the record does not say whether this deviates.
  - Everything else in the paragraph matches the ledger: reader header and heading pencil observed; source view, Cursor and System default app not observed; Cursor kept unverified. No ShellExecute or unregistered-scheme claim is made.
- Proposed improvement and benefit: have the orchestrator confirm the restart with the user (or drop the clause), add `diff=4735315bfc54`, and state in one clause what document was opened. This makes the evidence reproducible and keeps recorded fact apart from inference.
- Behavior-preservation risks: none (documentation).
- Evidence type: source observation (ledger comparison)
- Verification / measurement plan: orchestrator confirmation; no tooling needed.
- Relationship: none

### ANALYSIS-063: Packaged e2e row attributes the 22/22 rerun to the "fix round"

- Severity: suggestion
- Related task / requirement: 3.3
- Locations: `docs/dashboard-validation.md`, packaged browser acceptance row
- Observation: the row says "rerun in the fix round from a fresh `scripts/package.ps1` build". The ledger attributes 22/22 to the A7 validation-docs round (after the A4 fix round), A1 having reported 21/21, and qa-002 did not run packaged e2e. The numbers are consistent with the tree (the third scenario was added in A4); only the round label is off.
- Proposed improvement and benefit: say "rerun after the fix round" or "on identity `diff=4735315bfc54`". This avoids a reader looking for a fix-round packaged run that does not exist.
- Behavior-preservation risks: none.
- Evidence type: source observation
- Verification / measurement plan: compare with findings.md rows A4 and A7.
- Relationship: none

### ANALYSIS-064: Neighbor outside the target states the unregistered-handler outcome as fact and omits the Cursor caveat

- Severity: suggestion
- Related task / requirement: 3.3, deviation 6
- Locations: `docs/installation.md:35` (outside the review target; read for consistency only)
- Observation: "If the editor's URL handler is not registered (for example a portable install) the dashboard reports that the editor could not be opened" describes what ShellExecute does for an unregistered scheme. analysis-001 (line 143) and analysis-002 keep that as a hypothesis, and no live check covered it: the validation doc only shows the 503 path with a fake launcher. The same paragraph lists the Cursor preset without the "unverified" status that dashboard-validation.md records.
- Proposed improvement and benefit: soften to "may report" (or add an unverified note) and mark Cursor as not yet confirmed, so the user-facing doc does not exceed the evidence. The orchestrator decides whether to route it to a developer.
- Behavior-preservation risks: none.
- Evidence type: source observation
- Verification / measurement plan: none beyond a reread; a real check would need an editor-less Windows profile (a hypothesis test, not performed).
- Relationship: none

## Re-verification

None assigned.

## Assessment and limitations

The new section is consistent with `docs/architecture.md`, with the format implemented in `EditorLauncher.cs`, and with the analysis-001/002 measurements: it presents no hypothesis as a measurement and makes no performance claims. The only substantive defect is the self-contradictory causal sentence in the DevInstallTests note (ANALYSIS-061). The other items are provenance and wording. Not performed: re-running any tests or e2e, re-verifying vendor documentation with `ctx7`, and any live check. Writes: none (the report write was refused by the client). The identity was unchanged before and after.
