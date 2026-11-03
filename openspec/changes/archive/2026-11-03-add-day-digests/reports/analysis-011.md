<!-- Saved by the orchestrator (Claude Code, Opus 5.5, session f406ff60). Reviewer: code-analyzer subagent, assignment V2; client Claude Code (version not observable from the subagent), subagent mode, light recheck; model claude-sonnet-5-5 (recorded override: docs-only recheck). Case: the client refused the reviewer's write of this report. Exact refusal message as returned by the reviewer: "Subagents should return findings as text, not write report files. Include this content in your final response instead." The text below is the reviewer's returned report, saved verbatim. -->

# Analysis report: add-day-digests / V2 task 5.6 release prep v0.4.0 (light recheck of U1)

## Review target

- Reviewer / role: code-analyzer (V2) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (version not observable from the subagent; previous report recorded 2.1.316) / subagent mode, light recheck, read-only except this report / claude-sonnet-5-5
- Date: 2026-11-03
- Change root / assignment: `openspec/changes/add-day-digests` / V2, finding IDs ANALYSIS-151 to ANALYSIS-160
- Task and requirement references: [tasks.md](../tasks.md) 5.6; [design.md](../design.md) Decision 7
- Scope and relevant neighboring paths: `docs/releases.md`, `docs/installation.md` (changed text only); compared with `README.md`, `docs/dashboard-validation.md`, Decision 7. Source for the probe lines: `src/WorkLog/Updates/PayloadInstaller.cs` at `3c7dfd1`, `a51a14a^`, `a51a14a`.
- Reading limits and paths read outside scope: did not read `reports/qa-013.md`. Read [analysis-010.md](analysis-010.md) (prior report). No other out-of-scope reads.
- Baseline commit: `abe5d5d`
- Implementation identity (before): `head=abe5d5d baseline=abe5d5d files=7 diff=a3af46d055d6 untracked=0/e3b0c44298fc` (tracked diff SHA-256 `a3af46d055d60fefc5c1463038b663b2723cadbee8c30b7440c1bc7939291549`). Previous target: `diff=fe6a22604be0`.
- Before/after identity: after the analysis the summary line is identical. No unexpected mutation.
- Prior reports / rechecked findings: [analysis-010.md](analysis-010.md): ANALYSIS-141, ANALYSIS-142. ANALYSIS-143 and -144 deferred and not re-raised.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| U1 change set | `git diff abe5d5d -- docs/releases.md docs/installation.md` | Changes: releases.md intro sentence and the "Updates" bullet exception (Development builds), the manual `0.3.9001` bullet step (1), the manual-installation paragraph; installation.md heading paragraph (v0.4.0 wording, pre-`a51a14a` payloads). | Covered below. |
| ANALYSIS-141 | Read `docs/releases.md:51` against `README.md:67` and `docs/dashboard-validation.md:113` | Intro now reads "the dashboard stays local and calls no agent or issue service". The "Git service" and "reads recorded facts only" claims are gone. The README Daily-view sentence (agent, issue service, OpenSpec command) stays the one home of the Daily-specific claim. | Verified. |
| ANALYSIS-142 probe fact | `git show <rev>:src/WorkLog/Updates/PayloadInstaller.cs` grep | `3c7dfd1` and `a51a14a^` read `tools.Count != 2` (line 219); `a51a14a` reads `tools.Count < 2` (line 225). `3c7dfd1` is an ancestor of `a51a14a`. | The commit range stated in the docs is correct. |
| ANALYSIS-142 text | Read the changed paragraphs in both files | Pre-`a51a14a` payloads, including the manual `0.3.9001` payload and dev builds from `3c7dfd1` up to `a51a14a`, are named as declining. Switch-installed dev builds from the current repository are excluded. The Development builds "Updates" bullet carries the exception, and the manual payload sequence is amended. The `installation.md` heading and the releases.md link fragment `installing-the-release-with-day-digests-from-v030-and-earlier` are unchanged. | Verified, with one regression in the amended sequence (ANALYSIS-151). |
| Consistency | Compared releases.md:65, installation.md:129-131, Decision 7 and the Migration Plan | Same payload set, same commit anchors, same "later updates are ordinary" statement. The v0.4.0 manual-install paragraph is still the home of the procedure, linked from releases.md. Rollback and `digests.db` statements unchanged and consistent. | OK. |
| Anchors | grep for `Upcoming release` and `releases.md#`/`installation.md#` links | Only `tasks.md` 5.6 mentions the old "Upcoming release" heading (the task text, change-local). `README.md:219` links `releases.md#development-builds`, which still exists. | OK. |
| Duplication, complexity, performance | Not applicable to a docs-only target | n/a | Light review covers correctness and coherence only. |

## Steady-state cost and bounds

The target is release documentation text with no timer, poll, cache or accumulating component.

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | none in target | n/a | Not applicable (docs only). |
| Cache lifetime versus interval and refresh duration | none in target | n/a | Not applicable. |
| Unbounded state | none in target | n/a | Not applicable. |
| Repeated full reads | none in target | n/a | Not applicable. |
| External tool discovery | none in target | n/a | Not applicable. |

## Findings

### ANALYSIS-151: Manual `0.3.9001` sequence step (1) offers "install that manually" as a way to select another payload

- Severity: warning
- Related task / requirement: 5.6; `docs/releases.md` Development builds, manual `0.3.9001` payload bullet
- Locations: `docs/releases.md:46` (step (1)); `docs/installation.md:131` for what the manual installation is
- Observation: step (1) says a Switch of a new dev build "(or, once it is running a payload from `a51a14a` or later, a stable update; this payload declines a stable v0.4.0, so install that manually) selects another payload; `0.3.9001` becomes `previous` and the recovery selection". The manual installation (`installation.md:131`) extracts v0.4.0 into a clean installation directory; it does not perform a selection change in the installation that holds `0.3.9001`, so it does not make `0.3.9001` the `previous` payload or the recovery selection and does not unblock Retire. Only the Switch route satisfies the step's own postcondition. The parenthetical also mixes two cases (a stable update after a Switch, and a manual install) and "it" is ambiguous. In practice, from a running `0.3.9001`, the route that works is Switch of a new dev build, and after that a stable update is ordinary.
- Proposed improvement and benefit: in step (1) drop the manual-installation clause (it already belongs to the `:37` exception and the manual-installation paragraph) and keep "a Switch of a new dev build selects another payload"; mention the stable update only for step (2), where the running payload is already new. This keeps the Retire procedure executable as written.
- Behavior-preservation risks: none (text only).
- Evidence type: source observation (docs cross-check)
- Verification / measurement plan: re-read step (1) against `installation.md:131` after the edit.
- Relationship: new; regression of the U1 edit for ANALYSIS-142.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-141 | `head=abe5d5d diff=a3af46d055d6` | `docs/releases.md:51` now says "calls no agent or issue service" only. It no longer claims no Git service or recorded facts only. Compared with `README.md:67` (Daily-view scope kept there) and `docs/dashboard-validation.md:113` (git and openspec spawns per refresh round); no contradiction. | verified |
| ANALYSIS-142 | `head=abe5d5d diff=a3af46d055d6` | The v0.4.0 manual-installation paragraph and `installation.md:129` name every payload built before `a51a14a`, including the manual `0.3.9001` payload and dev builds from `3c7dfd1`; the probe lines were confirmed by git history (`Count != 2` at `3c7dfd1` and `a51a14a^`, `Count < 2` at `a51a14a`). The Development builds "Updates" bullet carries the exception, so ordinary stable-update wording is no longer unqualified. The `:46` sequence amendment has a follow-on defect (ANALYSIS-151). | verified, with ANALYSIS-151 open for the amended step |

## Assessment and limitations

- Scope covered: the U1 text changes in `docs/releases.md` and `docs/installation.md`, the anchors and links, and consistency with `README.md`, `docs/dashboard-validation.md` and Decision 7. ANALYSIS-143 and -144 are unchanged by U1 (the "change nothing" wording now also covers pre-`a51a14a` dev payloads, the same imprecision; the post-install step difference between the two homes is as before) and are not re-raised.
- Not performed: no build, test or package run, no old binary run, no live agents. Source evidence only for the probe lines.
- Unexpected writes: none; the identity summary is identical before and after. The single report write was refused by the client and not retried.
- Decision needed: ANALYSIS-151 is a one-clause edit in a maintainer-facing procedure; the user decides whether to fix it before tagging. Triage and ledger status belong to the orchestrator.
