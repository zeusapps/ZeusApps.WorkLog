# Findings: add-ticket-and-repository-links

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

Advisory marker; resume and hand-off rules are in the [workflow](../../../../../.agents/workflows/openspec-delegation.md#mixed-client-operation).

- Client / version: Claude Code 2.1.312
- Session identity: 7b4534dc-9e1e-4f7c-aa54-9b7bc061c7e7
- Since (UTC): 2026-10-28T19:42Z
- Previous owner: none

## Scope decisions

- 2026-10-28, prerequisite check: `fix-dashboard-reliability` and `add-archive-browser-and-editor-open` are archived. `/api/activity/changes` exists (`DashboardHost.cs`) and the client already loads it into `activity`. No acceptance change.
- 2026-10-28, sequencing: the untracked `openspec/changes/add-ongoing-board/` proposal depends on this change. It is unrelated planning work and is excluded from this change's implementation scope and identity.
- 2026-10-28, A1 developer deviations, accepted provisionally by the orchestrator for reviewers to check:
  1. `InsightItem` gains `Ticket[] Tickets` (additive API field) so completion and revisited insights can show tickets; client `Row` exposes `tickets` and `Entry` exposes `extractedTicketId`.
  2. `TicketFilter.Matches` mirrors SQLite `trim()` (spaces only) on stored parts.
  3. Quiet boundary: calendar days in the selected timezone, strictly more than 14 days is quiet.
  4. Card footer for a change without a live row shows the latest recorded status or "No observations"; the chip carries "No recorded activity".
  5. Azure DevOps branch links encode the branch as one query value; Azure ticket links require numeric IDs and an organization matching `[A-Za-z0-9_-]+`.
  6. Copy as Markdown copies the current drill-down scope (day-scoped drill-downs state the day); clipboard failure opens a manual-copy textarea.
  7. Links are rendered beside row buttons, not inside them; external clicks use a new `openExternal` helper.
  8. Fixture adds a `links-demo` repository; History "Load more" count changes from 61 to 68. The 1,000-observation cap is unit-tested only.
  9. Developer evidence: e2e 25/25 with the pre-existing declared-store scenario (QA-007 follow-up from `fix-dashboard-reliability`) excluded; the developer reports it fails identically on baseline source. Packaged e2e not run.
- Baseline commit: `9243722` on branch `change/add-ticket-and-repository-links` (created from `main`).

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A1 implementation | developer | Claude Code / role default | none: role default | tasks 1.1–5.2; baseline `9243722` | implementation paths only | none | done 2026-10-28, model claude-sonnet-5-5; identity `head=9243722 baseline=9243722 files=19 diff=49cfb5bf5b38 untracked=10/a4a3b4869b93` (paths: README.md docs/dashboard-validation.md dashboard/e2e/dashboard.spec.ts dashboard/src src/WorkLog tests/WorkLog.Tests tools/WorkLog.Smoke), confirmed by the orchestrator |
| A2 review QA | qa | Claude Code / role default | none: role default | all tasks 1.1–5.2; identity of A1 | `qa-001.md`; screenshots `../screenshots/qa-001-*.png` | QA-001 – QA-040 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report; QA-001 new (002–040 unused, retired); pre-existing declared-store e2e failure verified on baseline; packaged e2e 25/25; identity unchanged |
| A4 fix round 1 | developer | Claude Code / role default | none: role default | QA-001, ANALYSIS-001; validation-doc packaged-run lines and unit-test count; identity of A1 | implementation paths only | none | done 2026-10-28, model claude-sonnet-5-5; recorded URLs now return normalized `href`; plain ticket IDs and repository labels also escaped; identity `head=9243722 baseline=9243722 files=19 diff=5d553ba5e8e5 untracked=10/c3abeeaa9b0e`, confirmed by the orchestrator |
| A5 recheck QA | qa | Claude Code / role default | none: role default | QA-001 plus regression; identity of A4 | `qa-002.md`; screenshots `../screenshots/qa-002-*.png` | QA-041 – QA-060 | done 2026-10-28, model claude-sonnet-5-5; QA wrote its own report; QA-001 verified; no new findings (QA-041..060 unused, retired); identity unchanged |
| A6 recheck analysis | code-analyzer | Claude Code / role default | none: role default | ANALYSIS-001 plus regression; identity of A4 | `analysis-002.md` | ANALYSIS-041 – ANALYSIS-060 | done 2026-10-28, model claude-opus-5-5; no write attempted (client subagent instruction to return text), saved by orchestrator under a header stating that case; ANALYSIS-001 verified (both parts); no new findings (ANALYSIS-041..060 unused, retired); two residual edge cases reported without IDs; identity unchanged |
| A3 review analysis | code-analyzer | Claude Code / role default | none: role default | all tasks 1.1–5.2; identity of A1 | `analysis-001.md` | ANALYSIS-001 – ANALYSIS-040 | done 2026-10-28, model claude-opus-5-5; no write attempted (client subagent instruction to return text), saved by orchestrator under a header stating that case; ANALYSIS-001..005 new (006–040 unused, retired); identity unchanged |

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| QA-001 | [qa-001](qa-001.md#qa-001-markdown-escaping-helper-is-a-no-op-so-brackets-in-titles-and-ticket-ids-stay-live-markdown) | 3.1; Copy a work item as Markdown | warning | yes: copied Markdown is malformed for bracketed text | verified | developer (A4) | [qa-002](qa-002.md) |
| ANALYSIS-001 | [analysis-001](analysis-001.md#analysis-001-markdown-bracket-escape-is-a-no-op-and-recorded-urls-are-inserted-unnormalized) | 3.1; Copy a work item as Markdown | warning | yes: escape part duplicates QA-001 (canonical); the unnormalized recorded-URL part is additional and blocking | verified | developer (A4) | [analysis-002](analysis-002.md#re-verification) |
| ANALYSIS-002 | [analysis-001](analysis-001.md#analysis-002-two-clipboard-fallback-mechanisms-coexist) | 1.2, 3.1 | suggestion | no: severity gate | deferred | none | n/a |
| ANALYSIS-003 | [analysis-001](analysis-001.md#analysis-003-ongoing-cards-repeat-activity-lookups-and-formatter-construction-on-every-render) | 4.1 | suggestion | no: severity gate | deferred | none | n/a |
| ANALYSIS-004 | [analysis-001](analysis-001.md#analysis-004-the-duplicated-ticket-predicate-is-linked-only-by-a-test-the-architecture-doc-does-not-mention-it) | 2.1–2.2 | suggestion | no: severity gate | deferred | none | n/a |
| ANALYSIS-005 | [analysis-001](analysis-001.md#analysis-005-copy-as-markdown-repeats-a-full-journal-read-for-each-100-entry-page) | 3.1 | suggestion | no: severity gate | deferred | none | n/a |

## Finding details and history

### ANALYSIS-001: Markdown bracket escape is a no-op, and recorded URLs are inserted unnormalized

- Sources and evidence: [analysis-001](analysis-001.md) (Node probe of the exact expression)
- Required behavior: task 3.1; the copied Markdown includes tickets with links and must be well-formed for any recorded title, label, ticket ID, and URL
- Triage: blocking; warning that breaks the requirement's acceptance with a clear fix in scope
- Action / assignment: A4 fix round 1: return the normalized `href` for recorded URLs (or wrap Markdown destinations in `<...>`), with a unit case for a recorded URL containing a space; recheck by the analyzer
- Duplicate of: the bracket-escape part duplicates QA-001 (canonical); the recorded-URL part is tracked here
- Deferral: two residual edge cases reported without IDs in [analysis-002](analysis-002.md#assessment-and-limitations), present since A1 and outside this finding's stated cases: an unbalanced `)` in a recorded URL ends the Markdown link destination early, and a ticket ID or label ending in `\` escapes the closing `]`. Both need unusual recorded values. Treated as suggestion-level known limits under the severity gate; follow-up: wrap Markdown destinations in `<...>` and escape `\` in a later dashboard maintenance change, or earlier if the user asks.
- User decision: not needed
- Verification: A6, identity `diff=5d553ba5e8e5 untracked=10/c3abeeaa9b0e`, [analysis-002](analysis-002.md#re-verification): verified for both parts

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | orchestrator | analysis-001 |
| 2026-10-28 | reported | assigned | orchestrator | A4 fix round 1 |
| 2026-10-28 | assigned | awaiting-verification | developer | A4 handoff, identity diff=5d553ba5e8e5 |
| 2026-10-28 | awaiting-verification | verified | orchestrator | analysis-002 |

### QA-001: Markdown escaping helper is a no-op, so brackets in titles and ticket IDs stay live Markdown

- Sources and evidence: [qa-001](qa-001.md), also [analysis-001](analysis-001.md) ANALYSIS-001 (Node probe)
- Required behavior: task 3.1; copied Markdown is well-formed for bracketed titles, ticket IDs, and repository labels
- Triage: blocking; warning that breaks acceptance with a clear in-scope fix
- Action / assignment: A4 fix round 1: replacement `"\\$1"` and a unit test with bracketed title, ticket ID, and label; recheck by QA
- Duplicate of: none (canonical; ANALYSIS-001 partially duplicates it)
- Deferral: none
- User decision: not needed
- Verification: A5, identity `diff=5d553ba5e8e5 untracked=10/c3abeeaa9b0e`, [qa-002](qa-002.md): verified

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | reported | orchestrator | qa-001 |
| 2026-10-28 | reported | assigned | orchestrator | A4 fix round 1 |
| 2026-10-28 | assigned | awaiting-verification | developer | A4 handoff, identity diff=5d553ba5e8e5 |
| 2026-10-28 | awaiting-verification | verified | orchestrator | qa-002 |

### ANALYSIS-002 – ANALYSIS-005: deferred suggestions

- Sources and evidence: [analysis-001](analysis-001.md)
- Triage: suggestions under the severity gate; recorded as follow-ups without a fix round unless the user asks.
- Deferral destination: ANALYSIS-003 is reconsidered in `add-ongoing-board`, which adds more cards using the same quiet and activity helpers. ANALYSIS-002 and ANALYSIS-004 are low-risk cleanups for a later dashboard maintenance change. ANALYSIS-005 is reconsidered only if a measurement shows noticeable Copy as Markdown latency.

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| 2026-10-28 | — | deferred | orchestrator | severity gate |

## Completion assessment

- Current QA / analysis reports: [qa-002](qa-002.md) and [analysis-002](analysis-002.md) on identity `head=9243722 baseline=9243722 files=19 diff=5d553ba5e8e5 untracked=10/c3abeeaa9b0e`; full-scope coverage from [qa-001](qa-001.md) and [analysis-001](analysis-001.md) carries forward because A4 changed only `workitem-markdown.ts`, `links.ts`, their specs, and `docs/dashboard-validation.md`, all rechecked.
- Required acceptance: satisfied. Unit 122/122, .NET 353/353 (A1 target; no C# changed since), Release build e2e 25/25 on the fixed target, packaged e2e 25/25 on the A1 target (qa-001; not repeated after the client-only fix), all with the pre-existing declared-store scenario excluded.
- Findings: QA-001 and ANALYSIS-001 verified; ANALYSIS-002..005 deferred suggestions; no open blocking findings.
- Nonblocking follow-ups: ANALYSIS-002 and ANALYSIS-004 (later maintenance change); ANALYSIS-003 (reconsider in `add-ongoing-board`); ANALYSIS-005 (only if measured latency warrants); ANALYSIS-001 residual edge cases (unbalanced `)` in recorded URLs, trailing `\` in IDs or labels).
- Evidence gaps / provider restrictions: the declared-store e2e scenario fails identically on baseline `9243722` (QA-007 follow-up from `fix-dashboard-reliability`, pre-existing). Packaged e2e not repeated after A4. Not performed: live midnight rollover with Ongoing open, key probes inside the ticket input, and the 1,000-observation cap end to end (unit-tested only).
- Commit: `eb010e0` on branch `change/add-ticket-and-repository-links` (not pushed; push needs separate authorization)
- OpenSpec task reconciliation: tasks 1.1–5.2 verified by qa-001 and qa-002 evidence.
