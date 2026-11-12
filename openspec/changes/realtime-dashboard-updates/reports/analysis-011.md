# Analysis report: realtime-dashboard-updates / 011

## Review target

- Reviewer / role: independent code-analyzer, ANALYSIS-REVIEW-011.
- Client / version / mode / model: Codex / version not exposed / Default / assigned role model `gpt-6.1-sol`, high reasoning; runtime-resolved model introspection unavailable.
- Date: 2026-11-08.
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-REVIEW-011.
- Task references: [tasks 6.1 and 6.2](../tasks.md), scheduled time, DST/local-day choices and long suspension; three new DEV-018 cases near the end of `dashboard/src/app.reliability.spec.ts`.
- Scope: that test file only; light bounded review of the three additions. No new style/abstraction suggestion or production implementation review.
- Neighboring reads: App scheduler, Daily loader and visibility/revision lifecycle; API and live-test helper; historical [analysis 009](analysis-009.md) identity. No concurrent QA conclusions were read. Searches for the supplied developer report/log path did not locate a separate DEV-018 report; 306/306 success remains supplied evidence, not independently rerun here.
- Baseline and HEAD: `0d7768581a0305725454dc33d610924cce9d61ef`.
- Identity command: `./scripts/review-identity.ps1 -Baseline 0d77685 -Paths dashboard/src/app.reliability.spec.ts`.
- Manifest: one tracked modified file, `dashboard/src/app.reliability.spec.ts`; no untracked scoped files.
- Tracked diff SHA-256: `77d08b26eff31bf19b334dc2f95504da144ad17d137a23da756b82ed42b02dc2`.
- Before/after identity: matching `head=0d77685 baseline=0d77685 files=1 diff=77d08b26eff3 untracked=0/e3b0c44298fc`; no scoped mutation.
- Finding allocation: ANALYSIS-240 assigned below; ANALYSIS-241–259 unused.

## Analysis performed

| Area | Method / evidence | Outcome / limitation |
| --- | --- | --- |
| Warsaw DST / History | Inspect new case at line 1080 and actual `armClockTimer`/`timeChanged` | Arms actual App timer, changes fake system clock across the 23-hour local day, then advances timers. Relative range changes and one real App timeline request are asserted; explicit custom range remains fixed with no requests. It does not substitute a mocked History loader or directly invoke reevaluation for the tested rollover |
| Daily rollover | Inspect case at line 1116, shared fake fetch, actual `loadDaily` | Actual timer and request construction are exercised, including new Warsaw midnight and retained explicit past day. Successful data refresh is not exercised: missing fake endpoint is ANALYSIS-240 |
| Long hidden/resume | Inspect case at line 1146 and App `visibilityChanged` | Calls actual visibility lifecycle handler, verifies hidden cancels the timer and does not resume, then shows the App after a long fake suspension. Stubbed transport resume invokes actual revision application; callback checks date/quiet/expiry before setting Live. Route/view/document/draft, one resume, one authoritative catalog read, no preferences PUT and no observation creation are asserted |
| Authority versus local time | Trace timer/visibility/revision call boundaries | Time-only History case requires only its timeline request; custom case requires none. Daily forbids catalog polling. Resume permits one authoritative catalog reconciliation, which is distinct from a periodic TIME-triggered catalog read |
| Concrete fixture probe | `node artifacts/realtime-dashboard-updates/reviews/analysis-011/daily-fixture-probe.cjs` | Actual setup fetch callback, API module and extracted App Daily method reproduce two caught endpoint failures while every new rollover request assertion passes |
| Proportional static checks | Probe syntax, scoped whitespace and matching identities | Passed; no full suite, build, host, real data or installation used |

Evidence: [probe source](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-011/daily-fixture-probe.cjs) and [exact results](../../../../artifacts/realtime-dashboard-updates/reviews/analysis-011/daily-fixture-results.json). The probe extracts current source bodies with the TypeScript AST; it does not implement an alternative fake server or Daily loader. It calls the loader directly only to isolate the fixture defect; scheduled callback coverage is established by inspection of the actual new unit cases, not by this probe. Supplied 306/306 execution was not independently repeated.

The new cases initialize state with private evaluation/timer setup, then drive the actual scheduled timeout or actual App visibility handler for the behavior under test. The visibility test directly invokes the decorated handler rather than dispatching a DOM event, and replaces the live transport with a controlled resume stub. Its evidence therefore covers App handler/reconciliation ordering, not HostListener registration, real focus/visibility event delivery or SignalR recovery.

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | Test-only additions / existing timer | Source observation | No new production interval. Fixtures arm the existing at-most-one-minute safety callback, move fake wall time and advance the scheduled timer. Request assertions distinguish local time reevaluation from authoritative resume work |
| Cache lifetime versus interval and duration | Test fixtures | Source observation | No cache changes. The hidden case retains recorded activity/unlinked state so local date/expiry computation is observable before live reconciliation; it does not fabricate new observations to cause expiry |
| Unbounded state | Fixture calls and fake timers | Source observation | Collections are per test; each case destroys its fixture, and shared cleanup restores real timers/mocks and resets TestBed. No new long-running product collection or accumulation mechanism |
| Repeated full reads where a change check would do | History/Daily/resume assertions | Source observation | Relative History performs one boundary refresh; custom History none; resume one authoritative catalog read. Daily's count proves an attempted request only, not successful refresh, as reported below. No measured product idle-cost claim follows |
| External tool discovery / timeouts | Test-only target | Not applicable | No external executable discovery or process launch is added. Independent probe uses the already installed TypeScript dependency and exits without a host |

## Findings

### ANALYSIS-240: Daily rollover fixture passes while both Daily loads fail

- Severity: **warning**; a concrete acceptance-coverage gap, not a demonstrated production Daily defect.
- Related requirement: tasks 6.1/6.2, dependent Daily local-day freshness and preservation of an explicitly selected past day.
- Locations: `dashboard/src/app.reliability.spec.ts:75`–`:110` shared fetch implementation, and new Daily case at `:1116`–`:1144`; neighboring actual loader is `dashboard/src/app.ts:1197`–`:1227`.
- Observation: the fake fetch records `/api/daily` but provides no response branch for it. It throws `Unexpected request: /api/daily`. Actual `loadDaily` catches/reports that error and returns false, leaving `dailyData` unchanged. The new case ignores the initial return and checks only rollover request count/body and retained `dailyDay`; it does not require successful data, new Today content or an empty error. Both loads may fail while all those assertions pass.
- Exact bounded evidence: using the actual fake callback, API and loader gives `initialSuccess: false`, `rolloverSuccess: false`, `dailyData: null`, and two endpoint errors. Nevertheless, the single rollover call has the expected `{ before: "2026-03-29T22:00:00.000Z", timezone: "Europe/Warsaw", repositories: [], day: "2026-03-25" }`, with zero catalog/preference/observation calls, and the new request assertions pass.
- Proposed improvement and benefit: provide a valid Daily fixture response, distinguish before/after rollover Today data, and require successful initialized/refreshed data with no error while retaining the selected past day. This makes a passing case establish Daily freshness rather than merely attempted request construction.
- Behavior-preservation risks: keep the explicit past-day selection, new local-midnight boundary, no TIME catalog poll, no preference write and no fabricated observation assertions. The correction belongs in the test fixture; no production change is implied by this finding.
- Evidence type: source observation plus independently executed current-function fixture probe; no Angular suite or real host execution.
- Verification plan: drive the existing armed timer over rollover with a valid changed fixture response; assert current Daily/Today state advances, selected past day remains, loading succeeds and no unexpected error is reported. Retain the isolated request constraints.
- Relationship: new bounded finding within the assigned coverage review; no historical finding reopened.

## Re-verification

| Item | Corrected / reviewed target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| Scheduled History / DST gap | First new fixture | Actual timeout-driven App path, relative/custom choices and request assertions inspected | Source-level coverage present; full execution supplied only |
| Daily local-day gap | Second new fixture | Missing endpoint plus actual loader probe | Incomplete successful-refresh evidence, ANALYSIS-240 |
| Long suspension / choice preservation gap | Third new fixture | Actual visibility handler and revision application, local-time-before-Live checks inspected | Source-level App coverage present; mocked transport/event registration limits apply |
| Analysis 009 production conclusions | Production outside this test-only addition | Current controller `78db24467ab95c0221d16c986c57ee9e59ff27a35a7140cfd032228b6783e5ac`, queue `1430c9e42ae7790af48e9c48af8eefbd6decf65faf94f085b0f7c5319ee16d54` and helper `0f57c6112a8ddce266144e1430daf13b0a918cfef061744d53147efface636d1` match 009's explicit hashes | No changed production behavior in this test-only assignment; prior source/probe conclusions retain their original scope |

## Assessment and limitations

These additions use actual App timer and visibility/reconciliation paths, and do not merely repeat date-helper tests. One new case still permits failure of the behavior it claims to cover: ANALYSIS-240 leaves successful Daily rollover evidence incomplete despite supplied 306/306 unit success. The orchestrator owns triage and any subsequent correction assignment.

The assignment identifies DEV-018 as test-only with production unchanged. Controller/queue/helper hashes were independently matched to 009. Current App SHA-256 is `368f511d04178df2794c0620bb0988f8f0ab8f87c6922c1f5ecf323c276bc3a4`; the 009 report records its aggregate tracked diff rather than an individual App hash, so exact historical App-byte equality is an owner-supplied assertion here, not independently established by that report alone. This review does not extend 009 into live-browser approval or claim final runtime acceptance.

Only this report and ignored 011 evidence were written. Scoped whitespace/syntax, before/after identity and category-only report hygiene checks passed. No implementation, task state, ledger, historical report/evidence, real data, native agent or installation was changed; no approval question issued. Current live-host/layout and backend archive work are outside scope. Real scheduled/browser/transport behavior, final package and performance gates remain separate acceptance.
