# Realtime dashboard updates: resume handoff

> **Superseded (2026-11-11).** Do not resume from this file. The authoritative resume point is the **Current state** section at the top of [reports/findings.md](reports/findings.md), per the updated workflow. Everything below is historical.

## Active resumed checkpoint: 2026-11-11

This checkpoint supersedes the historical November9 checkpoints below. The user explicitly requested continuing implementation and clarified “Don’t ask; run required tests.” Required builds/tests remain authorized, subject to tool-enforced permissions. Current orchestrator is `codex-realtime-resume-20261111`; authoritative assignments and triage are at the top of `reports/findings.md`.

- Branch remains `change/realtime-dashboard-updates`, HEAD/baseline `0d7768581a0305725454dc33d610924cce9d61ef`; all implementation/artifacts are uncommitted. No version/install/publication/push/merge/archive action is authorized.
- QA024 and analysis026 independently verify the final disconnect-fixture correction on five-script identity `539857b9a06c`; actual target/reader recovery37/55ms. QA025 corrects stale factual prose and false-positive hygiene candidates from024 without altering the old report.
- Current backend ten-file identity is `head=0d77685 baseline=0d77685 files=10 diff=0a9a5cbc8458 untracked=6/00d0cbabee87`. Current full .NET454/454, no skips, and warning-free Release pass. Analysis029 maps backend coverage with no new source defect, retaining missing secret-replacement, digest-lock, actual sidecar-quiescence and package/performance evidence.
- DEV033 corrected ANALYSIS560, duplicate Daily journal/digest loads causing false paused status. Independent QA025310/310 and analysis028 actual-method checks verify that mechanism. Frozen first-fix App target is five paths (App/reliability/live connection/live updates/live helper), `files5 diff7652a7629e38 untracked3/3da791212782`. Current built coreDLL `f0baeb170ba1b2b141127fd7b1a7e6b7cc71566aac809f4e80bdb4a8981571ef`, SmokeDLL `f9650189267bdbd8dfb6d9226d1ed91b4d0db432847536d49e99417531c8b749`.
- Actual five-category raw `fault-matrix/2026-11-11T10-31-17-197Z-e556f130.json` passes disconnect1/wire26/restart51/watcher71; secret-reauthorization86 fails awaiting a new handshake. Cleanup passes. No full smoke/100-cycle pass is claimed. Historical status3 timeout ORCH008 did not recur; do not redesign that protocol speculatively.
- DEV034 is implementing the SECOND resumed correction batch: ANALYSIS580 (serialized Daily scope changes can publish old data before follow-up) in App/reliability tests, and ORCH009 (reader handshake measurement starts before its explicit bounded authorization operation) in `scripts/c06-fault-matrix.mjs`. Preserve serialized ownership, suppress stale data/error/loading, drain latest queued scope, retain real failures and timing thresholds. QA026 (QA560..599) and analysis030 (ANALYSIS640..679) are reserved for stable handoff. No third correction cycle automatically follows if required blockers remain after that recheck.
- QA backend probes use an immutable whole-Smoke copy under ignored `reviews/qa-025/runtime`. Copy coreDLL `37dc9e451ec42b94a0a58ef89679566fcc41ebd5ff489852a3f1ca2d57445f84` and same SmokeDLL match historical accepted manifests on unchanged backend; this is pre-Daily-fix frontend and backend-only evidence. Copy-source equality was verified, first attempted beforemanifest was not saved, and later shared-output changes are expected. Replacement probe must publish immediately after replacement before waiting close; digest lock probe compares main DB before independent writer, sidecars separately. No user data/settings/port47831/processes may be touched.
- Pre-fix full normal browser run was interrupted after >10 silent minutes, no usable results; outside-sandbox port47942 release verified. Future full suites need captured logs and a bounded overall timeout. Sandbox live-host startup consistently times out before any cycle; use required outside-sandbox retry rather than infer product failure or bypass permissions.
- Still required: stable second-fix reviews, corrected five-category/100-cycle matrix, full final normal browser suite, fresh self-contained no-Node/offline suite and bootstrap smoke, uninstrumented after latency plus both5minute idle windows, separate guarded-copy server-work capture, final docs/hygiene/task reconciliation and scoped local commits. Task count remains7/29 until named evidence is accepted. Existing MCP four-fixture acceptance remains valid on unchanged contract; no authenticated native agents are planned.

All active work continues; this checkpoint does not pause the user's request. Old exec IDs below are historical.

Latest resumed checkpoint: 2026-11-09, after 15:58 UTC. This is an orchestrator handoff, not an additional task tracker. Authoritative tasks and findings remain [tasks.md](tasks.md) and [reports/findings.md](reports/findings.md). Read the ledger's **Resumed checkpoint** first. The older snapshot below is retained historical context and does not override this checkpoint.

## Latest resumed checkpoint

- User explicitly authorized continuation after the handoff. Two bounded correction/recheck rounds have now run: DEV-024 corrected ANALYSIS-300/360; DEV-026 corrected canonical QA-400 (ORCH-007 duplicate). The workflow requires a user decision before another implementation correction round; no third round is assigned automatically.
- Persistent root AGENTS.md now records that required builds/tests/validation are included in authorized implementation, with no conversational permission questions, and specialists inherit that authorization. QA019 and analysis020 independently reviewed the rule. Tool-enforced sandbox permissions remain controlling; never disguise commands or use another tool to bypass them. User reiterated this instruction twice during the resumed session.
- Current native watcher/archive and full .NET suite pass independently **454/454**, no skips. QA018 TRX: `artifacts/realtime-dashboard-updates/reviews/qa-018/qa-018.trx`. Corrected archive fixture waits Documents advancement and settled dirty state, preserving authoritative archived identity/text/read-only assertions.
- Actual startup-failure cleanup passes after reaching the intentional injected failure: graceful ACK/exit, final source disposal, zero connections, port release and no wrappers. Raw: `artifacts/realtime-dashboard-updates/fault-matrix/2026-11-09T15-44-51-148Z-34310e02.json`. Analysis019 independently verifies bounded delayed/unresponsive/rejected/unknown-ready helper cases. ANALYSIS-300/360 are verified; the older proposed fixes below are already completed.
- Browser archive initially exposed a real client gap: Documents-only archive refresh sent knownRevision, whose unchanged-text response retained Active/commentable metadata. DEV-026 changed only app.ts and app.reliability.spec.ts to compare archive metadata and request a full response only on relevant transitions. Stable Documents-only metadata still uses knownRevision. Developer and QA independently pass **307/307 dashboard units** and exact archive browser **1/1**; analysis021 passes ten actual-method cases with no findings.
- Current corrected MCP runner passes **all four preserved/current × Claude Code/Codex protocol fixtures**, twelve wrappers exit cleanly, hashes unchanged. Raw: `artifacts/realtime-dashboard-updates/mcp-compat/2026-11-09T15-51-32-139Z-57010dca.json`; paired provenance: `dev027-identities-20261109.json`. These are fixtures, without authenticated native clients. Task7.3 is accepted; progress **7/29**.
- New five-category diagnostic failure: raw `artifacts/realtime-dashboard-updates/fault-matrix/2026-11-09T15-54-22-394Z-a8b069ba.json` fails in the first disconnect case waiting five seconds for Reconnecting/Paused after contextA.setOffline(true). **Zero cycles completed**, no outage-confirmed write. Cleanup passes. Existing source/raw investigation finds that target-tab reconnect/Live transitions precede actual faultStart; no post-offline A transport-close/status/re-establishment proof exists. This points to unproven fault injection, rather than a demonstrated product recovery defect. Read the final QA020 and analysis022 reports for attribution and recommendation.
- Recommended next bounded work, subject to the required next-cycle decision: make the disconnect fixture prove closure of its owned target and reader transports, hold the actual outage through an independent committed write, then prove a fresh post-release handshake and marker+Live reconciliation while preserving the same draft/context and all timing gates. Do not merely raise the five-second timeout, fabricate an offline status or count preparation handshakes as recovery.
- Remaining runtime gates: corrected five-category and full100-cycle matrix; uninstrumented after latency and both five-minute idle windows; separate copy-only instrumented server-work capture; full normal browser suite; fresh self-contained package, no-Node/offline browser and packaged bootstrap acceptance. No final performance values or packaged pass exist.
- Current branch/HEAD remain `change/realtime-dashboard-updates` / `0d7768581a0305725454dc33d610924cce9d61ef`. All implementation/artifacts remain uncommitted; source version0.4.1 unchanged. No installation/publication/push/merge/archive or user-process changes occurred.
- Latest identities: reader/backend fifteen-path target `files15 diff1ff82d0623d8 untracked8/2590af010779`; lifecycle/backend seventeen-path target `files17 diff0028b95318a8 untracked12/cd3ab06967b3`; AGENTS.md `files1 difff7fd61e4cd9c`. Recompute with pwsh before assigning. Reviews use role-default models. Preserve prior reports and ignored failures; QA018 archive failure was copied to `artifacts/realtime-dashboard-updates/reviews/qa-018/archive-browser-failure/` before Playwright output reuse.
- Reporting-only docs update is complete: QA021 and analysis023 pass the two frozen validation docs (`files2 diff6b1928ac6da9`) without findings. Highest written reports are QA021 and analysis023; continue numbering and never reuse allocations. Canonical remaining blocker is QA-440; ANALYSIS-460 records duplicate/root-cause detail. Strict OpenSpec validation and tracked whitespace checks pass. All specialists finished; no owned runtime host/probe remains. Do not infer a running probe from an old exec ID. WorkLog remains in_progress.

## Historical snapshot before resumed work

## Start here

1. Read this file, the current ledger sections, and `.agents/workflows/openspec-delegation.md`.
2. Check Git status and OpenSpec status/instructions for `realtime-dashboard-updates`. Preserve all dirty/untracked work.
3. The previous turn stopped at the repository's two-correction-cycle limit. The user requested this handoff because of token limits; **that request did not approve another implementation round**. A subsequent explicit request to resume the recommended fixes should be treated as the next-cycle decision, without asking the same question again.
4. The next bounded corrections are **ANALYSIS-300** and **ANALYSIS-360**, described below. Delegate implementation to the developer role, followed by independent QA and code analysis. Do not redo the completed exploratory work.

## User instructions that persist

- The user explicitly said **"don't ask about builds and tests!!!"** Required builds/tests are authorized; do not ask redundant confirmation questions.
- The user explicitly said **"stop asking me about context7!!!"** Do not start further Context7 approval requests for this change. Use already retrieved documentation, repository sources, and actual isolated probes.
- Respect tool/sandbox restrictions. Do not disguise commands under an approved prefix or use another tool to bypass approval. If execution is unavailable, report the concrete gap and continue independent work. Do not silently wait many minutes on a pending approval.
- Do not install builds, change versions, publish, push, merge, archive, alter personal client settings, or stop the user's processes. No authenticated native agent evaluation is planned.
- Test only disposable data and owned non-default ports. Never probe the user's dashboard port or real WorkLog data.

## Checkout and completion state

- Branch: `change/realtime-dashboard-updates`.
- HEAD/baseline: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Implementation and OpenSpec artifacts are **uncommitted**, with many tracked modifications and untracked files. None should be discarded or assumed disposable simply because untracked.
- Source version remains `0.4.1`; no installation/release action occurred.
- OpenSpec progress: **6/29 accepted tasks**: 1.1–1.3, 5.1, 6.1–6.2. Strict validation and scoped whitespace checks passed.
- Feature is incomplete. All specialists finished their assigned turns. Developer handoffs report no owned running test host/process; no runtime command remains pending. Ignored fixtures, the existing junction and raw failures are intentionally retained.

## What is implemented and verified

- Server: host-lifetime filesystem detection, monotonic domain revisions, five-second reconciliation/checkpoints, bounded independent refresh lanes, authenticated SignalR WebSockets/SSE, pre-send authorization and two-second idle revocation.
- Browser: targeted desired/applied revision coordination, retained dirty work, bounded reconnect/Retry, truthful status, source/navigation/draft/focus protections, explicit temporal invalidation, removal of two-second browser polling.
- Independent dashboard suite: **306/306**. Status surface passes light/dark at 390/768/1440px; keyboard Retry restores one connection. Refresh text contrast is 12.16:1 light and 10.72:1 dark.
- Scheduled Daily fixture now proves successful changed data, no error/loading, exact Warsaw midnight bounds and preserved explicit past day. History DST/custom-range/resume and board expiry/quiet cases pass. Reports 013 cover status/Daily corrections.
- Full .NET **452/452** passed independently **before the latest watcher topology correction**. Do not apply that count to the final backend target.
- Runner defects ANALYSIS-320/321 are independently verified corrected in reports 017: fresh lifecycle-test state per case and shared actual child outcome metadata for signal/timeout/nonzero/success.
- Four preserved/current Claude Code/Codex MCP **protocol fixtures** passed before the latest outcome correction. Native authenticated clients were not run; the corrected runner has not been rerun.

## Windows archive correction: do not repeat investigation

Original ORCH-003: actual browser archive `rename` failed with `EPERM`, even after relevant reads finished and Live appeared. Windows probes established that a watcher attached to a nested child directory blocks moving its ancestor; watching the change root alone permits the move. Restart Manager found no registered locking process.

Implemented correction:

- Keep all coordinator logical watch roots/IDs/domains and existing SourcePath containment/rebinding.
- Set an explicit recursive flag only on currently validated, existing `openspec/changes` roots; missing-root nearest parents stay nonrecursive.
- Coalesce descendant physical watchers under that approved recursive root and route events through the deepest contained logical root. Other roots remain nonrecursive.

Analysis 016 used an existing disposable junction: **two contained nested events, zero events for five outside-junction-target mutations, zero watcher errors**. This is fixture/platform evidence, not a universal OS guarantee. New source regression also covers outside-junction handling. Do not weaken containment or replace it with an assumption about native recursion.

Current Release build passes with **zero warnings/errors**. Developer's actual focused run (tool session `49843`) completed with **three DashboardChangeSourceTests passing**, including the new coalescing/junction case. The new coordinator test successfully performs `Directory.Move`, then fails its counter wait. No saved TRX was found for that focused run; QA 014 labels it supplied developer execution evidence. The current full .NET suite and browser archive scenario remain unrun.

## Next fix 1: ANALYSIS-300 (QA-300 duplicate)

Read [analysis-014.md](reports/analysis-014.md) and [qa-014.md](reports/qa-014.md).

- File: `tests/WorkLog.Tests/Dashboard/DashboardLiveCoordinatorTests.cs`, method `RecursiveFilesystemWatchAllowsExternalArchiveMoveAndRefreshesOpenDocument`, approximately lines 351–353.
- The new test wrongly waits for **both Catalog and Documents** revisions to advance after a pure archive move.
- `CatalogProjectionStamp` deliberately excludes change metadata; the Documents stamp includes change directory/archive metadata. Browser catalog/reader refresh depends on either domain. Catalog need not change for a pure archive move.
- Correct the fixture to wait for the relevant Documents generation and settled dirty state; retain or strengthen the authoritative archived document/text/identity/permissions assertions. Capture before/after vector diagnostics if needed.
- Do **not** force unrelated product Catalog increments, increase the timeout, remove real archive assertions, suspend transport, add rename retries, or skip the case.
- The failure at this extra gate is **not proof of a current reader-convergence defect**. Actual convergence is still unverified because those assertions were never reached.
- Then run the corrected native coordinator case, relevant watcher/containment regressions, full current .NET suite and exact existing browser archive scenario when execution permits.

## Next fix 2: ANALYSIS-360 (QA-360 duplicate)

Read [analysis-017.md](reports/analysis-017.md) and [qa-017.md](reports/qa-017.md).

- File: `scripts/c06-host-lifecycle.mjs`, `stopTrackedHost`, approximately lines 24–31.
- Current order is **stop ACK → immediately kill still-live wrapper → wait for exit**.
- `tools/WorkLog.Smoke/C06LiveAcceptanceHost.cs` acknowledges stop **before** awaiting `app.StopAsync` and writing `final.json`.
- A healthy delayed shutdown can be killed before required disposal evidence exists. Both reviewers reproduced this with the actual helper and a controlled delayed-shutdown model. No actual leak was observed.
- Offer a bounded graceful-exit opportunity after a successful stop request, then force-kill only the exact retained wrapper if necessary. Retain bounded forced-stop/exit/port proof for failed or unresponsive startup.
- Add meaningful delayed-healthy-shutdown and unresponsive-stop coverage using actual harness-used functions. Preserve final disposal requirements; an ACK alone is insufficient.
- Current `node scripts/c06-host-lifecycle.spec.mjs` passes but misses this race. The real `--startupFailureSmoke` probe remains unrun.

## Stable review targets at handoff

Use PowerShell 7 (`pwsh`) for `scripts/review-identity.ps1`; the PowerShell 5.1 attempt was incompatible. Recompute identities before assignment; these are reference snapshots, not locks.

| Scope | Paths | Identity after `head=0d77685 baseline=0d77685` |
| --- | --- | --- |
| Backend plus archive E2E | `src/WorkLog/Dashboard`, `tests/WorkLog.Tests/Dashboard`, `dashboard/e2e/dashboard.spec.ts` | `files=11 diff=0028b95318a8 untracked=6/fa18895730a1` |
| Runner correction | `scripts/c06-acceptance-invariants.mjs`, `c06-fault-matrix.mjs`, `c06-host-lifecycle.mjs`, `c06-host-lifecycle.spec.mjs`, `c06-mcp-compat.mjs`, `c06-process-outcome.mjs` | `files=6 diff=e3b0c44298fc untracked=6/9418f6589b14` |
| Current documentation | `docs/architecture.md`, `docs/dashboard-validation.md`, `docs/mcp-validation.md` | `files=3 diff=0b1ff1d65f56 untracked=0/e3b0c44298fc` |
| Status/temporal browser review | `dashboard/src/app.ts`, `app.html`, `styles.css`, `live-connection.ts`, `live-updates.ts`, `app.reliability.spec.ts` | `files=6 diff=0488e00a7095 untracked=2/4312dffecc75` |

Latest reviews: QA 014 + analysis 014 for backend/E2E; QA 017 + analysis 017 for runners; QA 014 + analysis 018 for final docs. QA 014 passed full committable change-root hygiene and screenshot review. It records the announced docs transition from draft `4d001...` to frozen `0b1ff1...`. All earlier reports/screenshots are immutable history.

Continue report numbering/reservations from the ledger, independently per role; highest written reports here are QA 017 and analysis 018. Do not reuse interrupted/retired finding allocations. Give reviewers all required finding text directly and prohibit current peer report/ledger reads when independence matters; some earlier scans incidentally exposed peer summaries and disclose that limitation.

## Runtime gates still required

Use commands and exact provenance in `docs/dashboard-validation.md` and `docs/mcp-validation.md`. Coordinate builds/hosts; do not overlap performance capture with builds or competing probes.

- Actual revised archive/reader/browser recovery; current full .NET suite.
- Corrected MCP protocol fixtures against current/preserved executables. Apphost hash alone does not identify the managed implementation; record paired `worklog.dll`/Smoke identity or the actual self-contained package hash.
- Actual startup-failure cleanup, five-category diagnostic smoke, then full **100 cycles** (25 disconnect, 25 wire faults, 20 restarts, 15 watcher faults, 15 revocation/reauthorization), retaining the same draft and independent browser contexts.
- Two **five-minute** connected idle windows, no reader and reader; zero periodic catalog/document/search/timeline reads. Freshness **p95 ≤1s per domain**, three warmups plus at least 30 samples each. Successful representative recovery **≤5s**. Preserve and report any miss; never weaken targets.
- Separate copy-only instrumented server CPU/GC/metadata/hash/DB/CLI measurement, not just reduced browser traffic.
- Fresh actual self-contained package, full normal and no-Node/offline browser suites and applicable packaged MCP/bootstrap smoke; no exclusions or inferred pass.

Useful existing no-host commands:

```powershell
node scripts/c06-host-lifecycle.spec.mjs
node scripts/c06-acceptance-invariants.mjs
```

Existing runtime commands (unrun on final target):

```powershell
node scripts/c06-fault-matrix.mjs --startupFailureSmoke --phase startup-cleanup
node scripts/c06-fault-matrix.mjs --smoke --phase five-category-smoke --deadlineMinutes 10
node scripts/c06-fault-matrix.mjs --phase final --deadlineMinutes 90
node scripts/c06-mcp-compat.mjs
```

Do not run these merely to collect more historical failures before the two required corrections are verified.

## Evidence and execution limitations

- Correct instrumented baseline: `artifacts/realtime-dashboard-updates/instrumented-baseline/2026-11-06T17-39-16-472Z-603eea45.json`.
- Preserved executable: `artifacts/realtime-dashboard-updates/baseline/package-retry/worklog-0.4.1-win-x64/worklog.exe`. Baseline package and raw results must remain unchanged. The earlier raw phase named `true` was an invocation-label error; do not rewrite it.
- Historical MCP success: `artifacts/realtime-dashboard-updates/mcp-compat/2026-11-09T08-52-11-830Z-839b460f.json` (four fixtures, earlier runner/managed assembly).
- Historical fault attempts `.../fault-matrix/2026-11-09T08-39-17-798Z-8fc7f8a8.json` and `2026-11-09T08-43-15-836Z-7b351eac.json` failed **before cycle 1**. The latter reached Live in all three tabs, then lacked the conditional comment editor, and proved owned cleanup. Do not count either as completed fault cycles.
- A subsequent corrected smoke escalation was user-aborted after roughly 928 seconds **before execution**: no process session/new raw file. Do not repeat an indefinite pending request or infer a test outcome.
- A sandboxed focused VSTest attempt could not connect to testhost within 90 seconds. Its outside-sandbox retry later actually completed (session 49843) with the three source passes and archive fixture failure above; it is no longer pending.
- No current after metrics, actual corrected fault cycles, fresh package/offline acceptance or latest corrected MCP rerun exists.

## Journal and stopping point

WorkLog remains `in_progress`; no feature-completion observation was logged. The 2026-11-09 observations and their digest are saved. Current result is reviewable but incomplete and uncommitted. Resume from the two named corrections and existing evidence rather than restarting baseline capture, repeating resolved investigations, or treating checked boxes/archival as proof of completion.
