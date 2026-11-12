# Analysis report: standup-ready-digests / fix-tier recheck 002

> Saved by the orchestrator as a reviewer-refused-write fallback (the client refused the reviewer's write). Condensed only in formatting; verdicts and evidence are unchanged.

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model: Claude Code 2.1.322, subagent (non-interactive), claude-opus-5-5
- Date: 2026-11-12T22:32Z
- Assignment: fix-tier recheck of ANALYSIS-001, 002, 004–009 (003 skipped as moot) and QA-001, QA-002, QA-003; design D1 and D4; day-digests "Saving a digest" and "Digest writing guidance"
- Baseline commit: `f6ea04e`
- Identity before and after (identical): `head=f6ea04e baseline=f6ea04e files=29 diff=6992788aa039 untracked=0/e3b0c44298fc`

## Analysis performed

| Area | Evidence | Outcome |
| --- | --- | --- |
| Store | `digest_parts.saved`; one LEFT JOIN on item, day, covers, saved; `Describes` removed | ANALYSIS-001, 002 verified |
| Service | Legacy saves return null parts; errors name the input and index | ANALYSIS-004, 007 verified |
| Guidance | Content rule only on `done`; count constants | ANALYSIS-005, 006 verified |
| Tool listing | `tools/list` probe (disposable scratch data dir, 25 s limit): parameter descriptions 1256 characters (was 2348); serialized `save_digest` 2740 (was 3898) | Developer note (c) confirmed |
| Dashboard | One-bullet lines copied inline; Blockers loading state; dead rule removed | ANALYSIS-008, 009, QA-001 verified |
| e2e and docs | Layout tests loop both themes at 1440/390/768; docs record 377/377, 60/60, 500/500 and the structured browser scenario | QA-002, QA-003 verified |
| Unit suites | Filtered .NET 105/105; dashboard 377/377 | Passed |

## Findings

No new findings.

## Re-verification

| Finding | Evidence | Outcome |
| --- | --- | --- |
| ANALYSIS-001 | `saved INTEGER NOT NULL` written in the same transaction; join on covers and saved; tests for a same-covers flattened-text v0.4.1 upsert and a `.`/`?`/`...` round trip | verified |
| ANALYSIS-002 | One JOIN when the stamped table exists, plain SELECT otherwise; one parameter builder | verified |
| ANALYSIS-004 | Legacy text gives null parts and deletes any parts row; test asserts `Parts == null`; spec wording updated | verified |
| ANALYSIS-005 | Content rule only in `DigestDoneDescription`; next and blockers refer to it | verified |
| ANALYSIS-006 | Count constants cross-checked in `LoggingGuidanceTests` and `DigestTests` | verified |
| ANALYSIS-007 | Index and length in errors; reworded stale message (subset test); unlinked exclusion in the description | verified |
| ANALYSIS-008 | Single Done bullet copied inline for all three sources; `it.each` in both formats | verified |
| ANALYSIS-009 | Rule removed; 1440 px e2e still asserts Blockers span below both columns | verified |
| QA-001 | `dailyBlockerGroups` null until Today loads; loading text shown; unit test asserts no "No blockers recorded" | verified |
| QA-002 | Both themes at all three widths | verified (source; run reported by orchestrator) |
| QA-003 | Doc counts and structured-scenario text match the runs | verified |

Developer notes:
- (a) Dropping and recreating an unstamped `digest_parts` table is acceptable (derived data, dev-build-only shape; a crash between the two statements self-heals on the next writer; a concurrent old-shape dev process could fail a save, dev builds only).
- (b) Judged from source, the QA-001 and ANALYSIS-008 tests fail on the previous code (the previous code rendered "No blockers recorded" without a loading branch, and nested text-only and single-bullet structured digests).
- (c) Confirmed by probe.

## Assessment and limitations

All eleven rechecked findings are verified; the regression scan found nothing. Not performed: the full .NET suite and Playwright (orchestrator reports 500/500 and 60/60). No repository writes; the probe exited within its limit.
