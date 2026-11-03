<!-- Saved by the orchestrator (Claude Code 2.1.316, session 47967d0e). Reviewer: code-analyzer subagent, assignment T2; client Claude Code 2.1.316, subagent mode, light review; model claude-sonnet-5-5 (recorded override: docs and config target). Case: the client refused the reviewer's write of this report. Exact refusal message as returned by the reviewer: "Subagents should return findings as text, not write report files". The text below is the reviewer's returned report, saved verbatim. -->

# Analysis report: add-day-digests / T2 task 5.6 release prep v0.4.0 (light review)

## Review target

- Reviewer / role: code-analyzer (T2) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.316 (as stated by the orchestrator; subagent mode, read-only except the refused report write) / claude-sonnet-5-5
- Date: 2026-11-02T23:12Z (local 2026-11-03)
- Change root / assignment: `openspec/changes/add-day-digests` / T2, finding IDs ANALYSIS-141 to ANALYSIS-150
- Task and requirement references: [tasks.md](../tasks.md) 5.6; [design.md](../design.md) Decision 7; [spec](../specs/day-digests/spec.md) "Digest storage compatibility"; [findings.md](findings.md) entries S1, S2 and the 2026-11-03 S1 triage
- Scope and relevant neighboring paths: `Directory.Build.props`, `docs/releases.md`, `docs/installation.md`, `README.md`, `dashboard/package.json`, `dashboard/package-lock.json`, `.github/workflows/release.yml`, `scripts/package.ps1`. Neighbors: `src/WorkLog/Updates/PayloadInstaller.cs` (HEAD and tag `v0.3.0`), `v0.3.0:src/WorkLog/Cli/UpdateCommand.cs`, `v0.3.0:src/WorkLog/Updates/UpdatePlan.cs`, `src/WorkLog/Dashboard/ReadBaselineStore.cs`, `docs/dashboard-validation.md`.
- Reading limits and paths read outside scope: did not read `reports/qa-012.md`. Also read, for metadata only, `tests/WorkLog.Tests/packages.lock.json` and the `obj` restore outputs of `tests/WorkLog.Tests`. One grep each in `dashboard/src/board.ts` and `src/WorkLog/Mcp/JournalTools.cs`.
- Baseline commit: `abe5d5d`
- Implementation identity (before): `head=abe5d5d baseline=abe5d5d files=7 diff=fe6a22604be0 untracked=0/e3b0c44298fc`. Manifest: `Directory.Build.props`, `dashboard/e2e/dashboard.spec.ts`, `dashboard/package-lock.json`, `dashboard/package.json`, `dashboard/src/styles.css`, `docs/installation.md`, `docs/releases.md` (all modified). Tracked diff SHA-256 `fe6a22604be04f0781b94192114dd7707873c2a2f01347053503823f0a58fd31`.
- Before/after identity: the after summary line is identical (`files=7 diff=fe6a22604be0 untracked=0/e3b0c44298fc`). No unexpected mutation.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Version bump | `git diff abe5d5d` on `Directory.Build.props` and the dashboard package files; `git grep` for `0.3.0` over tracked files | `Directory.Build.props:7`, `dashboard/package.json:3`, and `dashboard/package-lock.json` root and `packages[""]` all read 0.4.0. Remaining `0.3.0` hits are historical: older release sections, validation records, dev-build examples with base 0.3.0, test fixtures, and change documents. | No missed current-release reference. |
| Release workflow | Read `.github/workflows/release.yml` and `scripts/package.ps1:17-28` | The tag must match `^v\d+\.\d+\.\d+$`. `package.ps1 -Version` throws unless it equals the `Directory.Build.props` Version (`:26`) and refuses the dev range (`:25`). A `v0.4.0` tag passes, any other tag fails. The workflow does not check `dashboard/package.json`, so no gate is lost. | OK |
| Lock file after bump | `tests/WorkLog.Tests/packages.lock.json:287` | The project-reference entry `worklog.devinstall -> worklog "[0.3.0, )"` (added by `3c7dfd1` at version 0.3.0) was not regenerated. The `obj` dgspec and `project.assets.json` of the tests project were regenerated at 0.4.0 (mtime 23:53:33). No tracked lock file is in the diff, and S1 records that `dotnet restore --locked-mode` passed. I did not run restore. | Consistent with S1. QA should confirm a CI-equivalent locked restore on the final tree. Not a finding. |
| v0.3.0 decline (source) | `git show v0.3.0:src/WorkLog/Updates/PayloadInstaller.cs`, `UpdateCommand.cs`, `UpdatePlan.cs` | See "Source-evidence check". | Confirmed for selection and installed payloads. "Data unchanged" needs a qualification (ANALYSIS-143). |
| Release notes content | Read `docs/releases.md` in full and compared it with `README.md`, `docs/installation.md`, `docs/dashboard-validation.md` and design Decision 7 | ANALYSIS-141 to ANALYSIS-144 | Details in Findings. |
| Numeric claims in the notes | `ReadBaselineStore.cs:30-31` (300 keys, 64 MiB); `JournalTools.cs:28` (2 to 30 entry IDs); `board.ts:51-53` (7 days); `docs/dashboard-validation.md:56-60,100-113` (idle cost before and after, measured) | The claims match source or recorded measurements. The notes make no test-count, timing, or published-asset claim, so nothing unmeasured or post-publication is asserted. | OK |
| Anchor | Link at `docs/releases.md:65` against the heading at `docs/installation.md:127` | The GitHub slug of "Installing the release with day digests (from v0.3.0 and earlier)" is the linked fragment `installing-the-release-with-day-digests-from-v030-and-earlier`. | OK |
| Structure | Compared against the release procedure (`docs/releases.md:7-20`), Development builds (`:30-47`) and earlier sections | Newest-first order is kept. The section carries no publication outcome, which belongs in the GitHub release notes after publication (procedure step 6). The unchanged contracts and the `digests.db` rollback statement agree with `installation.md:131` and the design Migration Plan. | OK apart from the findings. |
| Duplication, complexity, performance | Not applicable to a docs and version-config diff | n/a | Light review covers correctness and coherence only. |

### Source-evidence check (findings.md 2026-11-03, S1 triage)

Claim: v0.3.0's probe runs before any selection commit and leaves selection and data unchanged on decline. Result: confirmed for the selection and installed payloads, with two qualifications for "data".

Quoting `v0.3.0:src/WorkLog/Updates/PayloadInstaller.cs`:

- `:19-20`: the default constructor binds `verify` to `VerifyHealthAsync`.
- `:22-37` `DownloadAsync` creates the stage `<data>/updates/staging/<guid>` (`:29`), downloads the ZIP and checksum into it (`:35-36`) and calls `PrepareDownloadedAsync` (`:37`).
- `:51-75` `PrepareDownloadedAsync` verifies the checksum (`:67`), extracts into `<stage>/package` (`:69`), then runs `await verify(payload, version, token)` at `:70`. Only after that does it call `WriteOutcome(... "staged" ...)` (`:73`). Nothing in the installation root is touched before `:70`.
- `:194-216` `VerifyHealthAsync` runs the candidate with `--data-dir` set to a throwaway `.health-<guid>` directory inside the stage (`:197`, `:206-207`). It rejects at `:213-215` (`tools.Count != 2 ... throw "Candidate health probe did not advertise the expected journal tools."`). A candidate listing `log_work`, `query_work` and `save_digest` has `Count == 3` and is rejected. The probe never touches the real journal or settings.
- `:77-127` `ActivateAsync` is the only place that writes `versions/<v>` (`:106-119`) and calls `Commit` (`:123`). `Commit` writes `current.previous.json` at `:163` and `current.json` at `:164`. `v0.3.0:src/WorkLog/Cli/UpdateCommand.cs:79-81` calls `DownloadAsync` and then `ActivateAsync`. The exception from `:215` propagates out of `DownloadAsync`, so `ActivateAsync` is never reached.

Qualifications:
- (a) The catch at `PayloadInstaller.cs:39-48` writes a `failed` outcome record ("Preparation failed before selection", via `UpdatePlan.StatusFile`, `UpdatePlan.cs:18`) under `<data>/updates/`.
- (b) There is no cleanup of the stage. The downloaded ZIP and the extracted package stay under `<data>/updates/staging/<guid>`. The only `staging` references in v0.3.0 are creation and validation.

Journal, settings, `current.json`, `current.previous.json` and `versions/` are unchanged. Evidence type: source observation. No old binary was run, as the findings entry already states.

## Steady-state cost and bounds

The target is a version bump plus release documentation, with no timer, poll, cache or accumulating component. I checked only the facts the notes state.

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | none in target | n/a | The notes' idle claim ("cheaper and more reliable") is backed by the measured before/after tables in `docs/dashboard-validation.md:56-60,100-113`. |
| Cache lifetime versus interval | none in target | n/a | Not applicable (docs and config). |
| Unbounded state | `dashboard-reads/` (described in the notes) | source observation | Bounded at 300 keys and 64 MiB (`ReadBaselineStore.cs:30-31`), matching the notes. |
| Repeated full reads | none in target | n/a | Not applicable. |
| External tool discovery | none in target | n/a | Not applicable. |

## Findings

### ANALYSIS-141: Release intro claims the whole dashboard "calls no ... Git service and reads recorded facts only"

- Severity: warning
- Related task / requirement: 5.6 (release notes accuracy); one home per fact with `README.md`
- Locations: `docs/releases.md:51`; contrast `README.md:67` and `docs/dashboard-validation.md:113`
- Observation: the sentence "the dashboard stays local, calls no agent, issue service or Git service, and reads recorded facts only" is a whole-dashboard claim. README scopes the equivalent statement to the Daily view ("calls no agent, issue service or OpenSpec command"). The dashboard as a whole runs `openspec` and `git` per refresh round (8 OpenSpec and 4 git spawns, `docs/dashboard-validation.md:113`). It also reads OpenSpec documents, and the notes' own bullets add document reading and Open in editor. "Git service" is ambiguous next to local `git` spawns. As worded the claim is wrong or misleading.
- Proposed improvement and benefit: drop the sentence, or scope it to what is true and already documented (for example "the dashboard stays local and calls no agent or issue service"). Keep the Daily-view-specific statement in README as its one home.
- Behavior-preservation risks: none (text only).
- Evidence type: source observation (docs cross-check)
- Verification / measurement plan: compare the final wording with `README.md` and `docs/dashboard-validation.md`.
- Relationship: new

### ANALYSIS-142: "From v0.3.0 and earlier" omits dev-range payloads with the old probe; the Development builds text still describes stable updates over them as ordinary

- Severity: warning
- Related task / requirement: 5.6 (update path); design Decision 7; `docs/releases.md` Development builds
- Locations: `docs/releases.md:65`, `:37`, `:46`; `docs/installation.md:127-131`. The probe change is in `a51a14a`: HEAD `src/WorkLog/Updates/PayloadInstaller.cs:225` reads `tools.Count < 2`, while `3c7dfd1:src/WorkLog/Updates/PayloadInstaller.cs:219` still reads `Count != 2`.
- Observation: Decision 7 and the notes name v0.2.0 and v0.3.0 as the releases whose probe declines. Any payload built before `a51a14a` has the same exact-two-tools probe and runs it itself during `worklog update`. That includes the documented manual `0.3.9001` payload (`docs/releases.md:46`) and any dev build made from `3c7dfd1` up to `a51a14a`. For those installations `worklog update` to v0.4.0 declines exactly like v0.3.0. Yet the Development builds text (`:37`, `:46`) says a stable release is offered and installable over dev payloads, and the Retire sequence at `:46` step (1) relies on "a stable update". The next stable is 0.4.0 (there is no 0.3.1), so that sequence no longer works from a pre-`a51a14a` dev payload without the manual installation. Dev builds installed with `install-dev.ps1 Switch` run the repository's current installer and are not affected. Only the update run by the old running payload is.
- Proposed improvement and benefit: add one sentence to the v0.4.0 manual-installation paragraph (its home is `docs/releases.md`), for example "This includes development builds made before this release, such as the manual `0.3.9001` payload". Point `:46` step (1) to a Switch of a new dev build or the manual v0.4.0 installation. The `installation.md:127` heading may stay.
- Behavior-preservation risks: none (text). Decision 7 does not change; this clarifies its scope.
- Evidence type: source observation (git history of the probe line)
- Verification / measurement plan: `git show 3c7dfd1:src/WorkLog/Updates/PayloadInstaller.cs | grep -n "Count != 2"` (line 219) versus HEAD line 225.
- Relationship: new

### ANALYSIS-143: "decline ... and change nothing" is not exact for data

- Severity: suggestion
- Related task / requirement: 5.6; Decision 7 ("they decline activation and change nothing"); spec scenario "Update from a release that expects two tools"
- Locations: `docs/releases.md:65`, `docs/installation.md:129`, `design.md` Decision 7. Source: `v0.3.0:src/WorkLog/Updates/PayloadInstaller.cs:39-48`, `:70`, `:213-215`.
- Observation: see the source-evidence check. Selection, installed payloads, journal and settings are unchanged. A `failed` update status record is written under `<data>/updates/`, and the downloaded ZIP plus extracted package remain in `<data>/updates/staging/<guid>` (no cleanup in v0.3.0). A user who reads "change nothing" and then finds a staging folder (up to the 256 MiB package bound) or a failed status in `update --status` sees a mismatch.
- Proposed improvement and benefit: say "leave the installed payloads, selection and your data unchanged (a failed-update status and a staging folder under `updates/` remain and can be ignored)" in the two release-facing docs. Qualify the ledger sentence ("selection and data unchanged") the same way when it is recorded.
- Behavior-preservation risks: none (text).
- Evidence type: source observation
- Verification / measurement plan: the quoted lines suffice. A live check would need the old binary, which no harness runs.
- Relationship: new

### ANALYSIS-144: Post-install steps differ between the two homes

- Severity: suggestion
- Related task / requirement: 5.6; AGENTS.md parity (managed instructions for both clients)
- Locations: `docs/releases.md:65` versus `docs/installation.md:131`
- Observation: releases.md says to "Rerun `worklog init` afterwards to receive the digest and summary instructions, and reconnect agent clients to refresh tool discovery". installation.md says to rerun `worklog init` "to receive the digest instructions" and has no reconnect step. Yet a new MCP tool (`save_digest`) is visible to clients only after reconnect. The summary instructions (a v0.4.0 change) are missing there, so the procedure document is the less complete of the two.
- Proposed improvement and benefit: make `installation.md:131` carry the full step (init for the digest and summary instructions, then reconnect clients, covering both Claude Code and Codex). Let releases.md keep one short sentence linking to it, so the installation procedure has one home.
- Behavior-preservation risks: none (text).
- Evidence type: source observation
- Verification / measurement plan: re-read both sentences after the edit.
- Relationship: new

## Re-verification

None assigned. No prior findings rechecked.

## Assessment and limitations

- Scope covered: the 7-file diff for task 5.6, the release notes against the rest of `docs/releases.md`, `docs/installation.md`, `README.md`, design Decision 7, and the v0.3.0 source at tag `v0.3.0`.
- Version references: complete for the current release (`Directory.Build.props`, dashboard package and lock). The release workflow and `package.ps1` accept a `v0.4.0` tag against the 0.4.0 props and reject mismatches. I left the `[0.3.0, )` project-reference range in `tests/WorkLog.Tests/packages.lock.json:287` for QA to confirm with a CI-equivalent locked restore. S1 reports it passed, and I did not restore.
- Orchestrator source argument: confirmed for selection and payloads, qualified for data (ANALYSIS-143). It is source evidence only. No old binary ran, no live agents ran, and nothing was built, tested or packaged.
- Unmeasured or post-publication claims in the new section: none found.
- Writes: the single report write was refused by the client, so nothing was written. The identity check shows no mutation. Triage and ledger status belong to the orchestrator.
- Decision needed: ANALYSIS-141 and ANALYSIS-142 change release-facing wording the user may want to approve before tagging. The other two are optional.
