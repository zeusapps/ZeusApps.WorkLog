# Analysis report: standup-ready-digests / milestone 001

> Saved by the orchestrator as a reviewer-refused-write fallback (the client refused the reviewer's write). Condensed only in formatting; findings, locations, measurements and evidence are unchanged.

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model: Claude Code 2.1.322, subagent (non-interactive), claude-opus-5-5
- Date: 2026-11-12T22:12Z
- Assignment: milestone review `analysis-001` (ANALYSIS-001..029); tasks 1.1, 2.1, 2.2, 3.1, 4.1, 4.2; design D1–D6; spec deltas day-digests, daily-preparation, local-agent-integration
- Scope: `git diff f6ea04e` for `src/WorkLog`, `tests/WorkLog.Tests`, `tools/WorkLog.Smoke`, `dashboard/src`, `dashboard/e2e`, `docs` (29 files). Neighbors read: `DigestRequest.cs`, `EntryFactory.cs`, `ServeCommand.cs`, `WorkLog.csproj`, `C06BaselineFixture.cs`. QA report not read.
- Baseline commit: `f6ea04e`
- Identity before and after (identical): `head=f6ea04e baseline=f6ea04e files=29 diff=9c59a9b0a391 untracked=0/e3b0c44298fc` (tracked diff `9c59a9b0a39187d4b1c029e6713981512f6f6c47c80ec43c5fcc8d268186fd45`)

## Analysis performed

| Area | Evidence | Outcome |
| --- | --- | --- |
| Two-table write atomicity | Both writes on one connection in one non-deferred transaction | Atomic |
| Mixed-version stale parts | v0.4.1 upsert touches only `digests`; DEV-001 text check closes the same-covers case | Correct today; tied to `Flatten` (ANALYSIS-001) |
| Migration-free `digest_parts` | `CREATE TABLE IF NOT EXISTS` once per writer under the init mutex; read-only readers skip a missing table; `user_version` stays 1 | Correct; no version signal (ANALYSIS-001) |
| Derived-text determinism | `Flatten` pure over cleaned bullets; exact JSON/SQLite round trip | Deterministic within a release, not across releases |
| Read consistency | Two statements without a read transaction | Transient self-healing skew (ANALYSIS-002) |
| Legacy vs structured validation | One `Normalize`; shared `Clean` and stale path | Minimal duplication; legacy parts row optional (ANALYSIS-004) |
| DEV-002 | Callers in `DashboardFixture.cs:173`, `C06BaselineFixture.cs:72,127` | Acceptable |
| DEV-003 | Live `tools/list` probe on a disposable data dir: nullable arrays and strings, `default: null` | Matches the `log_work` precedent for both clients |
| DEV-004 | `EnsureAvailable` once per process on the first linked log; at most a 3 s lock wait | Acceptable and bounded |
| LoggingGuidance | All `const string`; sizes HEAD vs base: ContentRule 575/423, Digest 1028/630, DigestInstruction 1233/801, save_digest parameter descriptions 2348/630; serialized save_digest 3898 | Compile-time; ContentRule repeated three times (ANALYSIS-005); count literals untied (ANALYSIS-006) |
| Errors and descriptions | Label names, no index; subset IDs reported as "a later observation exists"; unlinked exclusion missing from the description | ANALYSIS-007 |
| Per-request payload | DigestInstruction (~300 tokens) plus up to 30 entries | Bounded by the 30 cap |
| `daily.ts` purity | View and copy share `linePresentation(digestFor(...))` | Pure; single-bullet copy shape differs (ANALYSIS-008) |
| Copy edge cases | Empty lists omitted; Blockers fallback; links; valid CommonMark nesting | Correct |
| CSS scoping | All new selectors `daily-` prefixed | Scoped; one dead rule (ANALYSIS-009) |
| Mixed-version tests | Replays baseline SQL for old store, different/same-covers upserts, text-only delete, repeats, wire shape | Strong for D1; missing punctuation round trip and `Flatten` freeze |
| Unit suites | .NET 496/496; dashboard 374/374 | Passed; Playwright not run (QA) |

## Steady-state cost and bounds

No new timer or poll; a change-triggered Daily refresh adds two cheap statements; `digest_parts` grows like `digests` (one row per item-day, at most 9×120 characters); `Request` adds `EnsureAvailable` once per process; no external tools.

## Findings

### ANALYSIS-001 (warning): Stale-parts detection depends on the exact `Flatten` format, and `digest_parts` has no version signal
- Locations: `src/WorkLog/Persistence/DigestStore.cs:11-27`, `:62-67`, `:195-196`; `src/WorkLog/Journaling/DigestService.cs:133-147`
- Observation: DEV-001 is necessary (v0.4.1 and v0.5.0 produce identical covers from the second observation), but comparing recomputed `Flatten()` with stored text makes the flattened format a persisted contract. A later format change silently drops every stored structured digest to text. With `user_version` at 1, `digest_parts` cannot evolve with a version check.
- Proposal: before v0.5.0 ships, add `saved INTEGER NOT NULL` to `digest_parts` written with `digest.Saved.UtcTicks` in the same transaction; show parts only when covers and saved match; drop the second clause of `Describes`. Otherwise freeze `Flatten` with a contract test.
- Verification: store tests for a v0.4.1 same-covers upsert whose text equals the flattened form, and a `Save`→`Read` round trip of bullets ending in ".", "?" or "...".

### ANALYSIS-002 (suggestion): `Read` runs two unsnapshotted statements with duplicated parameter building
- Location: `DigestStore.cs:167-200`
- Proposal: one `LEFT JOIN` select when the table exists; keep the plain select otherwise.

### ANALYSIS-003 (suggestion): `DigestStore.Save` accepts parts whose text does not describe them
- Location: `DigestStore.cs:34`, `:120-141`
- Proposal: reject or derive the text in the store. Moot if ANALYSIS-001's stamp is adopted.

### ANALYSIS-004 (suggestion): Legacy `text` saves create a redundant parts row
- Locations: `DigestService.cs:137-142`; `DigestStore.cs:26-27`; `dashboard/src/daily.ts:123-131`
- Proposal: pass `Parts = null` for the legacy form and drop the second `Describes` clause; a legacy v0.5.0 save then writes exactly what v0.4.1 writes. The presentation is unchanged.

### ANALYSIS-005 (suggestion): The content rule is repeated in all three list descriptions of `save_digest`
- Locations: `LoggingGuidance.cs:52-65`; `JournalTools.cs:29-31`
- Measured: parameter descriptions grew from 630 to 2348 characters, 1725 of them from ContentRule repeated three times; serialized save_digest is 3898 characters.
- Proposal: state ContentRule and the secrets sentence once (on `done` or the tool description); have next and blockers refer to it. About 1.15k characters (~290 tokens, estimate) saved per listing.

### ANALYSIS-006 (suggestion): Bullet counts in the guidance are literals not tied to the enforced constants
- Locations: `LoggingGuidance.cs:33-65`; `DigestService.cs:18-21`
- Proposal: `const string` count values cross-checked against `MaximumDone`/`MaximumNext`/`MaximumBlockers`.

### ANALYSIS-007 (suggestion): Error and description wording does not quite match the contract
- Locations: `DigestService.cs:120,153,158,160`; `LogResult.cs:16`
- Proposal: parameter names with index and length (`done[2] is 134 characters; at most 120`); stale message "the IDs are not exactly the item's observations for that day (one is missing or a later one exists)"; add the unlinked exclusion to the `digestRequest` description. Keep test substrings.

### ANALYSIS-008 (suggestion): Lines showing one Done bullet are copied in two shapes
- Locations: `dashboard/src/daily.ts:177-198`, `:221-234`
- Proposal: copy any presentation with exactly one Done bullet and no Next or Blockers inline regardless of source; labelled lists otherwise.

### ANALYSIS-009 (suggestion): The `.daily-blockers` grid-column rule is dead
- Locations: `dashboard/src/styles.css:3251-3253`; `app.html:539,613`
- Proposal: remove it.

## Assessment and limitations

The two-table write is atomic; table creation is migration-free and safe for read-only readers; every analyzed mixed-version interleaving behaves correctly and the tests replay the baseline's SQL. DEV-001 is necessary and correct but format-coupled (ANALYSIS-001, the only item worth fixing before v0.5.0). DEV-002 and DEV-004 are acceptable; DEV-003 matches precedent (live probe). Guidance is compile-time and identical for both clients; most growth comes from ANALYSIS-005. Not performed: Playwright, `dotnet format`, live agents, skew-window measurement. The schema probe used a disposable scratch data directory and left no process running.
