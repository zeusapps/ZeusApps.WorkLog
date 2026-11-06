# Analysis report: improve-dashboard-reading-and-reliability / 003

## Review target

- Reviewer / role: delegated independent reviewer / code-analyzer.
- Client / version / mode / model (resolved ID): Codex / version unavailable to this subagent / delegated Default mode / `gpt-6.1-sol`, high reasoning, assigned role default.
- Date: 2026-11-05, final documentation review at 20:01 UTC.
- Change root / assignment: `openspec/changes/improve-dashboard-reading-and-reliability` / ANALYSIS-REVIEW-003, first independent correction cycle.
- Task and requirement references: canonical [QA-020](qa-002.md), task 2.1 Accurate read-status outcomes and final documentation task 6.1; [tasks](../tasks.md), [design](../design.md), [openspec-browser delta](../specs/openspec-browser/spec.md).
- Scope and relevant neighboring paths: correction in `dashboard/src/app.ts` and `dashboard/src/app.reviews.spec.ts`; final safety/correctness review of `docs/dashboard-validation.md` and `docs/mcp-validation.md`. Earlier coverage of unchanged areas is carried from [analysis-001](analysis-001.md) and [analysis-002](analysis-002.md).
- Reading limits and paths read outside scope: only QA-020 plus regression and final docs; no new suggestions. Relevant outside reads were the canonical QA report and ignored `artifacts/dashboard-reading-reliability/fix-1/` red/green, build, .NET, browser and smoke evidence. Review identity and helper/new-file hashes were read to support coverage carry. No deferred finding was rechecked; no unrelated source analysis was restarted. Assigned role/facts/template instructions remain applicable.
- Baseline and HEAD: `ac6d7f075a4a52e5eeef5896d034ae3c35a504fc` throughout.
- Prior reports / rechecked findings: QA-020 only. ANALYSIS-001 and ANALYSIS-020 remain deferred without recheck or closure. Earlier reports are immutable.

### Implementation identity

Stage A correction command, run before source review, after it and again after final documentation review:

```powershell
./scripts/review-identity.ps1 -Baseline ac6d7f0 -Paths dashboard/src/app.ts dashboard/src/app.reviews.spec.ts
```

All three outputs matched:

```text
HEAD: ac6d7f075a4a52e5eeef5896d034ae3c35a504fc
Baseline: ac6d7f075a4a52e5eeef5896d034ae3c35a504fc
Manifest:
  M dashboard/src/app.reviews.spec.ts
  M dashboard/src/app.ts
Tracked diff SHA-256: b5e7fc93006edf38d38f5843d098af08bc7ee290888a7d45914fa0a162781a1e
Summary: head=ac6d7f0 baseline=ac6d7f0 files=2 diff=b5e7fc93006e untracked=0/e3b0c44298fc
```

The initial whole target was `head=ac6d7f0 baseline=ac6d7f0 files=32 diff=d9f5e6948847 untracked=3/16c9bd9c4903`, full tracked hash `d9f5e6948847ad3cbc4839b206a13eae7361b7658cb582cc429806e3630dc136`. The orchestrator then authorized a documentation-only update after completed acceptance. That expected Stage B transition is distinct from mutation during a frozen review.

Final Stage B command, run before and after reviewing the completed documentation/evidence:

```powershell
./scripts/review-identity.ps1 -Baseline ac6d7f0 -Paths README.md dashboard docs scripts src tests tools
```

Both final outputs matched:

```text
HEAD: ac6d7f075a4a52e5eeef5896d034ae3c35a504fc
Baseline: ac6d7f075a4a52e5eeef5896d034ae3c35a504fc
Tracked diff SHA-256: 6ad03108e2eee35c36aec2a941ef5c1693d2dd2a37f86f36c320d9714e84c3d1
Summary: head=ac6d7f0 baseline=ac6d7f0 files=32 diff=6ad03108e2ee untracked=3/16c9bd9c4903
```

The final manifest is the same 32-path manifest recorded in [analysis-002](analysis-002.md#implementation-identity): 29 modified tracked files and these three untracked files, with unchanged hashes:

| File | SHA-256 |
| --- | --- |
| `dashboard/src/document-navigation.spec.ts` | `9ab6a6cee89b7368fcc851640353c375a09ee2b14214ac76d81111497366121b` |
| `dashboard/src/document-navigation.ts` | `cdf933b478bf3e7d54621b0a65b0acede883ce4e39ea5db4d4a71b43de1c771f` |
| `scripts/benchmark-review-comparison.mjs` | `481a15e4ef04c0d1e6dfd4756b93cecb28378721a352574089aa08a5c56e169b` |

Backend carry was confirmed with the original backend-path command from analysis-001: nine files, tracked hash `f736d5e40c9feb140b6be6e356a3d259f1732fe61cc755bc4f4837e19f9afcc7`, no untracked files. The comparison helper remains `9102bb3ebf64aa33084784bf50fe2b671a1ef4690a8652319a7225a2964ab92a`; the harness remains the hash above. Thus earlier backend and comparison evidence is carried, not claimed as a fresh execution. The correction source stayed unchanged across both stages. No unexpected mutation was observed.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| QA-020 correction | Inspected `markChangeRead` and compared the original canonical observation with current line 1528 | Generic failures now say to refresh the change, confirm documents/read-state availability, and retry | Verified by independent source inspection. The message no longer attributes a document 404, storage 503 or unknown error to connectivity loss. |
| Classification and aggregation regression | Inspected the complete batch loop and focused tests | Success counter and `noteRead` remain; only 409 increments conflicts; 401 stops the batch and provides the existing authorization action; other failures count once and share one neutral summary | Passed source regression review. No response contract, route, document mutation or persistence behavior changed. |
| Successful marks and drafts | Inspected new 404/503 cases at `app.reviews.spec.ts:282` and `:310`, plus existing mixed and authorization cases | Both new cases retain successful design fingerprint, selection and draft; 503 case asserts one failed-document aggregate and no raw error text; mixed 409/network and 503/401 cases retain their original outcomes | Passed source/test review. The new cases exercise distinct document-read and baseline-save failure boundaries. |
| Duplication, simplification and architecture | Reviewed correction scope against prior report and existing architecture coverage | One shared generic summary literal changed; two focused response regressions added; no provider-specific branch or new subsystem | Passed. No new abstraction or complexity. Broader architecture coverage is carried from prior reports. |
| Functional evidence | Read `fix-1/red.log` and `green.log` recorded summaries; completed build/.NET/browser/smoke log summaries | Red: two new failures with 268 passing. Green: 270/270 in 20 files. Clean build, .NET 427/427 no skips, normal 44/44 in 4.8m, packaged no-Node 44/44 in 2.3m, packaged `BOOTSTRAP PASSED` | Passed available evidence. These executions were performed by developer/orchestrator, not this analyzer. Red/green files are recorded summaries; build/.NET/browser/smoke files contain execution output. |
| Performance and bounds | Compared current helper/harness hashes with analysis-002 evidence | Helper/harness and navigator hashes unchanged; correction changes error wording only | No new timing run warranted. Earlier <=100 ms comparison-helper result is carried with the original measurement limitations. |
| Final docs correctness | Inspected final two-doc deltas against completed correction-round logs | 270 unit tests, 427 .NET, final normal/package browser counts and times, only `--updates-only` bootstrap smoke, unchanged source version 0.4.1 and unpublished review candidate | Passed. Comparison scope/precision tradeoff remains explicit; shared-server fixtures remain labeled as protocol evidence rather than native-agent tests. No unsupported separate default smoke claim. |
| Whitespace | `git diff --check ac6d7f0 -- dashboard/src/app.ts dashboard/src/app.reviews.spec.ts docs/dashboard-validation.md docs/mcp-validation.md` | Exit 0 | Passed. No build/package/browser run was started by this analyzer. |
| Documentation hygiene | Added-line scan of the final validation docs for current personal/machine values, personal paths, email and secret patterns; source inspection of synthetic regression values | Zero matches; new test values are synthetic | Passed in reviewed scope. Own report receives a hygiene check after its single reserved write. No historical reports or fixture data were edited. |

## Steady-state cost and bounds

Every row is assessed proportionally to this wording-only correction; detailed unchanged component evidence remains in analysis-001/002.

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | App polling and backend catalog | Source observation / carried calculated rates | Correction adds no timer, request or background task. Existing two-second visible/nonbusy polling remains nominally up to 30 catalog requests/minute plus 30 document checks/minute with an open reader. Earlier backend costs and C06 deferral are unchanged; no new idle trace was run. |
| Cache lifetime versus interval and refresh duration | Catalog, document and comparison caches | Carried source observation | No cache code changed. Prior two-second catalog / 60-second successful command cache observations and one-revision-pair reader memos remain applicable. No latency or cache-relief improvement is attributed to this correction. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Batch counters, draft and preference state | Source observation / carried coverage | Batch still uses four scalars and one final notice. Neutral text adds no retained collection or error history. Existing draft-map and bounded preference/baseline observations are carried; no new growth path is introduced. |
| Repeated full reads where a change check would do | Change-wide mark operation | Source observation | Existing explicit batch action still reads each selected document and requests a mark sequentially; correction does not add or duplicate either operation. Background unchanged-document checks and baseline token extraction are unchanged. No new performance hypothesis is raised in this recheck. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Product catalog and acceptance harness | Carried source observation | No executable discovery, launch, timeout or teardown code changed. Existing normal/packaged fixture modes and prior review limits remain applicable. Deferred ANALYSIS-020 was not rechecked. |

## Findings

**No new findings.** This is a recheck of canonical QA-020 plus regression and final documentation. The cause-neutral guidance addresses the original inaccurate connection-restoration message. Existing deferred suggestions were outside this recheck and remain unchanged.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| QA-020 | Source `b5e7fc93006e`, carried unchanged into final whole target `6ad03108e2ee` | Independently inspected line 1528, 404/503 regressions and mixed/401/success/draft behavior. Completed red/green summaries record failing new cases before correction and 270 passing afterward. Final docs match corrected behavior and completed acceptance. | Verified by source/regression review with recorded execution evidence. Orchestrator owns final ledger closure. |
| ANALYSIS-001 / ANALYSIS-020 | No correction assigned | Explicitly excluded by recheck assignment | Not performed; remain deferred. No closure or renewed severity decision. |

## Assessment and limitations

QA-020's correction is verified within the assigned scope: a generic batch failure now receives actionable cause-neutral guidance, while conflict/authentication classification, successful marks and unsaved reader context are preserved. Final two-doc guidance and actual acceptance labels are consistent with the completed correction-round evidence. No additional required correction was identified in this recheck.

Source before/after identity and final whole-target before/after identity match. The expected documentation transition was reviewed only after its final freeze. Earlier coverage is carried for unchanged areas; benchmark code and evidence were not rerun, and no native-client or live-agent evidence is claimed. This reviewer ran only read-only identity, hash, whitespace, evidence and hygiene checks. Functional executions belong to the named developer/orchestrator evidence; independent QA supplies its separate report.

The only intended write is this reserved report. No source, tests, docs, configuration, tasks, ledger, earlier reports, installation or user data were changed. No build, package, process launch, branch, commit, push or release action was performed. The orchestrator owns final triage, closure and completion decisions.
