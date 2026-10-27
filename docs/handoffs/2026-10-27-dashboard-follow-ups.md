# Handoff: dashboard follow-ups and agent workflow

Captured on 2026-10-27 from an exploration of the v0.3.0 dashboard (commit `582f8bc`, change archived as `openspec/changes/archive/2026-10-26-add-work-dashboard`). This document records the decisions, the sequence of work, and the calibration answer key. It contains no implementation. Seven OpenSpec changes were proposed; each is self-contained (proposal, specs, design where needed, tasks) and names its prerequisites.

> **Reviewers:** if you are a QA or code-analyzer specialist running the `add-agent-workflow` calibration, stop reading. The answer key below is for the orchestrator only.

## Sequence

```
add-agent-workflow ──> calibration run (inside add-agent-workflow, group 5)
        │
        v
fix-dashboard-reliability ──> add-review-inbox ──> add-archive-browser-and-editor-open
        │                                                   │
        │                                                   v
        │                                    add-ticket-and-repository-links ──> add-daily-prep
        │
        └──> improve-observation-summaries  (independent; recommended before add-daily-prep)
```

| Order | Change | Prerequisites | Main surfaces |
| --- | --- | --- | --- |
| 1 | `add-agent-workflow` | none | `AGENTS.md`, `.agents/`, `.claude/agents/`, `.codex/agents/`, `scripts/review-identity.ps1`, `docs/agent-workflow.md` |
| 2 | `fix-dashboard-reliability` | 1 | `src/WorkLog/Dashboard/*`, `dashboard/src/*`, archived proposal cleanup |
| 3 | `add-review-inbox` | 1, 2 | `/api/reviews`, `/api/reads/*`, reader diff, Ongoing panel |
| 4 | `add-archive-browser-and-editor-open` | 1, 2, 3 | `/archive`, `/api/activity/changes`, `/api/open` |
| 5 | `add-ticket-and-repository-links` | 1, 2, 4 | `links.ts`, ticket filter, Copy as Markdown, quiet chips |
| 6 | `add-daily-prep` | 1, 2, 5 | `/daily`, `/api/daily` |
| 7 | `improve-observation-summaries` | 1 | `AgentInstructions.cs`, `LogRequest.cs` |

Changes 3–6 all edit `dashboard/src/app.ts` and `app.html`, so apply them one at a time in this order. Change 7 touches different files and can run in parallel with any of 2–6.

## Starting a change from a fresh context

1. Read `AGENTS.md` (after change 1 exists) and this handoff.
2. Run `openspec status --change "<name>" --json` and read every artifact in the change root.
3. Check the prerequisite note at the top of the change's `tasks.md`. Each prerequisite must be applied, and archived or at least merged, first.
4. Apply with `/opsx:apply <name>` (or "apply <name> using the developer, QA, and code-analyzer roles"). The orchestrator owns the ledger at `<change>/reports/findings.md`.
5. Do not archive while REVIEW markers remain in the change's documents (rule added by change 1).

`add-agent-workflow` is the bootstrap: no orchestrator exists until it is applied, so its groups 1–4 are done by the main agent and reviewed by the new roles in group 5, as recorded in its tasks.

## Decisions from the exploration

- Agent workflow: port Bell Tower's orchestrator, developer, QA, and code-analyzer setup (`zeusapps/ZeusApps.BellTower` at `aadbfb3`, usually checked out at `../ZeusApps.BellTower`) and adapt it.
  - Add the heuristics described in `add-agent-workflow` design Decision 4: time passing, long-lived state, input focus, scope filters, test residue, and idle steady-state cost.
  - Run a calibration review of the current dashboard before fixing it.
- Fixes A–E plus the poll cost are required (see the answer key).
- Review loop: the open-comments overview and "changed since last read" were both wanted.
- Browsing: the Archive browser and open-in-editor were wanted. Main-spec (`openspec/specs`) browsing was not requested and is parked.
- Source links: jump to ticket is wanted. "Jump to code" was only an example; commit discovery and commit-SHA recording are parked.
  - Other low-cost items accepted: repository and branch links, ticket filter, Copy as Markdown, quiet-change indicator.
- Daily prep:
  - Classic yesterday / today / blockers. Yesterday is the last working day, implemented as the last day with recorded observations, so no working-day calendar is needed.
  - Must be fast: no agent, no ticket lookups, no language model.
  - Option B: a fact sheet plus a copy-for-daily button.
  - The user works for a single client, usually in one repository, so the daily view uses the shared repository filter with no separate scope.
  - The user's existing agent skills for daily, weekly-update, and weekly-summary reports live on another machine and stay as they are; the dashboard complements them.
- Summary quality: an optional change to make agent-written summaries short and result-focused. It improves the Daily view and the user's report skills alike.

## Parked ideas

- Browsing main specs under `openspec/specs/` (and including them in search).
- Recording commit SHAs at logging time, or deriving commits from local `git log`.
- A Reports tab that stores dailies and weeklies generated by skills (would need a new MCP tool).
- Local small-language-model summarization.
- Splitting `dashboard/src/app.ts` (1,124 lines) into components. Do it incrementally when a change needs it.
- Multi-tab preference merging. Week and month timeline scales that actually aggregate.

## Calibration answer key (orchestrator only)

Defects identified by manual review at `582f8bc`. In `add-agent-workflow` task 5.3, compare the calibration reports with this list. Record each item as caught, missed (with the heuristic that should have caught it), or not applicable.

| Key | Defect | Location | Expected catcher |
| --- | --- | --- | --- |
| A | History range freezes: `loadHistory` saves absolute `from`/`to` on every load, and `start` restores them, so "last 30 days" never rolls forward. Day focus is also persisted. No presets. | `dashboard/src/app.ts:197-201`, `:352`, `:394-398`, `:959-978` | QA time-passing check |
| B | Reading positions and document choices grow without bound; the server rejects state over 64 KiB, after which every scroll shows an error banner | `app.ts:576-578`, `:843-851`; `DashboardState.cs:29` | QA long-lived state; analyzer unbounded collections |
| C | Escape inside the comment textarea closes the reader | `app.ts:1018-1030` | QA input-focus check |
| D | Test REVIEW markers committed in the archived proposal | `openspec/changes/archive/2026-10-26-add-work-dashboard/proposal.md:11,14` | QA residue/hygiene check |
| E | Search `unavailable` includes catalog notices for unrelated repositories, so searches always look incomplete | `DocumentService.cs:173` | QA scope-filter check |
| P | Poll cost: the 2 s poll plus the 2 s catalog cache cause near-continuous sequential discovery (about 1 s per `openspec` call, measured); search re-reads every file every poll; timeline queries read the whole journal | `app.ts:211`, `:979-1017`; `SourceCatalog.cs:29,54,84,127`; `DocumentService.cs:183` | Analyzer steady-state cost |
| F1 | `LinkAsync` uses `Single`, so an unknown source maps to 503 instead of 404 | `DocumentService.cs:206` | QA error handling |
| F2 | Search section labels pick up `#` lines inside fenced code | `DocumentService.cs:187` | QA |
| F3 | The timezone field accepts invalid zones, which then fail every query; From after Through is not validated | `app.html` History controls | QA input validation |
| F4 | The Windows OpenSpec CLI lookup only finds npm-global installs | `LocalCommand.cs:17-23` | Analyzer or QA environment check (not fixed by `fix-dashboard-reliability`; triage on discovery) |

## Follow-up list for `fix-dashboard-reliability`

Calibration findings that are confirmed, not in the key above, and within its scope are added here with a link to the source report (`add-agent-workflow` task 5.6).

- Browser scenario "keeps history available when a declared store cannot be resolved" fails when OpenSpec CLI 1.13.1 is on PATH. The CLI treats a directory with real OpenSpec content as its own root and ignores the `store:` declaration, and the dashboard follows the CLI. Decide whether the dashboard should still refuse a local fallback when a store is declared, or whether the fixture and spec scenario should follow current CLI semantics. Source: `openspec/changes/archive/2026-10-27-add-agent-workflow/reports/findings.md` (Orchestrator verification notes). Found by the orchestrator while verifying commands, not by calibration. Independently reproduced in calibration as QA-007 ([qa-001](../../openspec/changes/archive/2026-10-27-add-agent-workflow/reports/qa-001.md)), which also notes that `docs/dashboard-validation.md` claims all eleven scenarios pass; correct that record when the scenario is resolved.
- QA-006: `boundary()` in `dashboard/src/api.ts:47` returns the previous day's 23:00 for zones that skip midnight (Africa/Cairo 2026-04-24, Asia/Beirut 2026-03-29), so History ranges gain or lose an hour on those days. Fits the History range and timezone work (group 1). Source: [qa-001](../../openspec/changes/archive/2026-10-27-add-agent-workflow/reports/qa-001.md). Taken into `fix-dashboard-reliability` as task 1.5 (2026-10-27).
- ANALYSIS-002: the open document's full text is resent and rescanned on every 2 s poll even when its revision is unchanged (measured about 1.38 GB/h for a 740 KiB document). Fits the poll-cost work (group 5); task 5.3 does not yet cover the document request. Source: [analysis-001](../../openspec/changes/archive/2026-10-27-add-agent-workflow/reports/analysis-001.md). Taken into `fix-dashboard-reliability` as task 5.6, using a `knownRevision` request field (2026-10-27).
- ANALYSIS-003: an idle History refresh rewrites `dashboard-state.json` with fsync about every 10 s. Task 1.2 (no date persistence on load) and task 5.3 (no every-5th-poll refresh) should remove it; add an idle no-write check to acceptance. Source: [analysis-001](../../openspec/changes/archive/2026-10-27-add-agent-workflow/reports/analysis-001.md). Covered by `fix-dashboard-reliability` tasks 1.2 and 5.3, with the idle no-write check added to 5.3 and 5.5 (2026-10-27).
- Optional, suggestion severity, triage on application:
  - QA-009: a spent or expired `#connect=` link shows the connect screen although the session cookie is valid. Source: [qa-001](../../openspec/changes/archive/2026-10-27-add-agent-workflow/reports/qa-001.md).
  - QA-010: a literal `-&#45;` typed in a review comment reads back as `--`; pair with ANALYSIS-012 (marker parsing duplicated in C# and TypeScript). Sources: [qa-001](../../openspec/changes/archive/2026-10-27-add-agent-workflow/reports/qa-001.md), [analysis-001](../../openspec/changes/archive/2026-10-27-add-agent-workflow/reports/analysis-001.md).
  - QA-011: `worklog dashboard --help` describes the `--repository` option with init and serve text. Outside the dashboard client; may be a separate small fix. Source: [qa-001](../../openspec/changes/archive/2026-10-27-add-agent-workflow/reports/qa-001.md).
  - ANALYSIS-009 (unconfirmed): settings are read once at dashboard start, so a repository saved later may not appear until restart. Confirm while implementing the cheap-tier fingerprint (task 5.1). Source: [analysis-001](../../openspec/changes/archive/2026-10-27-add-agent-workflow/reports/analysis-001.md).
