<!-- Saved by the orchestrator (Claude Code 2.1.316, session 47967d0e). Reviewer: code-analyzer subagent, assignment Q2; client Claude Code 2.1.316, subagent in auto mode; model claude-opus-5-5. Case: the client refused the reviewer's write of this report. Exact refusal message as returned by the reviewer: "Subagents should return findings as text". The text below is the reviewer's returned report, saved verbatim. -->

# Analysis report: add-day-digests / Q2 (task 5.5, developer round P1)

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.316 (per the orchestrator assignment; session 47967d0e), subagent in auto mode / `claude-opus-5-5`
- Date: 2026-11-02T22:32:08Z
- Change root / assignment: `openspec/changes/add-day-digests` / Q2
- Task and requirement references: task 5.5 in [tasks.md](../tasks.md)
- Scope and relevant neighboring paths: `git diff abe5d5d -- dashboard` (`dashboard/src/styles.css`, `dashboard/e2e/dashboard.spec.ts`). Neighbors read: `dashboard/src/app.html` (board cards, archive entries), all `.change-card`, `.board-card` and `.card-footer` selectors and media rules in `dashboard/src/styles.css`, class hooks in `dashboard/src/*.spec.ts`, and `git show 573a538 -- dashboard/src/app.html` for where `.change-card` came from
- Reading limits and paths read outside scope: `reports/qa-010.md` not read (as instructed); no other paths outside scope read
- Baseline commit: `abe5d5d`
- Implementation identity (before): `head=abe5d5d baseline=abe5d5d files=2 diff=04e0ceb67ea1 untracked=0/e3b0c44298fc` (manifest: `M dashboard/e2e/dashboard.spec.ts`, `M dashboard/src/styles.css`; tracked diff SHA-256 `04e0ceb67ea10771dfaff38a76089cf1009573d12927c4b6195cad4a41401b91`)
- Before/after identity: the summary line after the analysis is identical; no unexpected mutation
- Prior reports / rechecked findings: none rechecked

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Correctness of the fix | Specificity and cascade inspection | `.board-card.change-card` (0,2,0) beats both `.board-card` (`styles.css:602`) and `.change-card` (`styles.css:746`) regardless of source order. Change cards now get `padding: 14px 16px 0`, which matches the `margin: 0 -16px` of `.board-card .card-footer` (`styles.css:610`). | Correct: the footer now reaches both inner edges. Change cards also lose 6px of top padding (20px to 14px), as task 5.5 intends ("the same ... padding as compact cards"). |
| Minimality / dead CSS | Grep of `change-card` and `card-footer` across `dashboard/src`; history of `573a538` | `change-card` is applied only at `app.html:368`, on board cards. The board replaced the standalone `<article class="change-card">` grid in `573a538`. `.card-footer` appears only at `app.html:423/436/460`, all inside board cards. | After P1, `padding: 20px 23px 0` in `.change-card` never applies, and the base `.card-footer` `margin`/`padding` are always overridden. See ANALYSIS-131. |
| Class hooks | Grep of specs | `.change-card` is a selector in `app.reviews.spec.ts:372`, `app.links.spec.ts:148` and `e2e/dashboard.spec.ts:1135/1139`. | The class must stay as a test hook and for hover styling. Only its redundant declarations could go. |
| Media rules / other selectors | Inspection of `@media` blocks at `styles.css:741, 2030, 2037, 2086, 2164` | Only `.board { grid-template-columns }` (at 1000px and below) and `.card-footer { font-size: 8px }` (at 600px and below) touch the board. Neither changes horizontal padding or margins. `.board-card.compact-card .card-footer` changes only `margin-top`. | No interaction with the fix. |
| Test robustness | Inspection of `e2e/dashboard.spec.ts:1203-1227` | The test measures only horizontal edges (`left`/`right` against the border-adjusted card box), so font metrics and line wrapping cannot affect it. A 1px tolerance absorbs subpixel rounding, while the regression produces 7px. Cards are selected by `data-kind` plus `.first()`, after `open`/`settled` confirm that the `board-demo` fixture rendered change, ticket and ad-hoc cards. State is restored in `finally`, like the neighboring tests. | Robust, and it fails on the reported regression (7px left and right). The extra `padding` equality check is a stricter invariant and does no harm. |
| Helper duplication | Grep for geometry helpers in the e2e spec | No shared geometry helper exists. The only other `getComputedStyle` geometry use (`:916`) measures line height for a different purpose. The test reuses `open` and `reset`. | No duplication. The test name claims more coverage than it has; see ANALYSIS-132. |
| Parity | Not applicable | Dashboard-only, client-neutral styling change. | Not applicable. |
| Build / test execution | Not performed | QA owns execution. | Not run. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | None in target | Source observation | Not applicable: the target is one static CSS rule and one e2e test, with no timer, poll or loop. |
| Cache lifetime versus interval and refresh duration | None in target | Source observation | Not applicable: no caches touched. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | None in target | Source observation | Not applicable: no accumulating state. The test restores board state in `finally`. |
| Repeated full reads where a change check would do | None in target | Source observation | Not applicable: no request handling changed. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | None in target | Source observation | Not applicable: no lookups or spawned tools added. |

## Findings

### ANALYSIS-131: The override rule could be a one-line deletion; the old `.change-card` box rules and base `.card-footer` offsets are dead

- Severity: suggestion
- Related task / requirement: 5.5
- Locations: `dashboard/src/styles.css:746-760` (`.change-card` and the new `.board-card.change-card`), `dashboard/src/styles.css:839-841` (base `.card-footer` `margin`/`padding`), `dashboard/src/app.html:366-368, 423, 436, 460`, at `abe5d5d` plus the working-tree diff
- Observation: `.change-card` survives from the change grid that `573a538` replaced with the board, and it is now applied only to board cards. After P1, its `padding: 20px 23px 0` is always overridden, so P1 adds a rule to cancel a declaration nothing else uses. Its `background`, `border` and `border-radius` repeat `.board-card` exactly. The base `.card-footer { margin: 0 -23px; padding: 13px 22px }` is likewise always overridden by `.board-card .card-footer`, because every `.card-footer` sits in a board card. These 23px values are the same old geometry that caused the bug. What really distinguishes change cards is `box-shadow`, `transition`, the `:hover` rule and `overflow: hidden`. The `overflow: hidden` contradicts the explicit `.board-card { overflow: visible }`: change cards clip their content, which also rounds their footer corners. Judging from the source geometry, the Move menu (about 95px tall, opening below a header near the top of the card) still fits inside a change card, so no clipping is expected today. This is not measured.
- Proposed improvement and benefit: Optional; task 5.5 does not need it. Instead of adding `.board-card.change-card`, delete `padding: 20px 23px 0;` from `.change-card`. That one-line removal renders the same as P1. For a fuller cleanup, also cut `.change-card` down to what actually differs (shadow, transition, hover, and a deliberate choice on `overflow`), and drop `margin`/`padding` from the base `.card-footer`. Benefit: board-card geometry is defined in one place, and no stale 23px values are left for a future change to bring back.
- Behavior-preservation risks: Removing only the `padding` line renders the same as P1. The fuller cleanup must keep the `.change-card` class, which `app.reviews.spec.ts`, `app.links.spec.ts` and the e2e spec use as a hook. It must also decide `overflow` on purpose. If change cards switch to `visible`, the `border-radius: 0 0 9px 9px` already on `.board-card .card-footer` keeps the footer corners rounded.
- Evidence type: source observation for the dead declarations; the Move-menu clipping estimate is a hypothesis from source geometry. Performance is not applicable.
- Verification / measurement plan: The P1 e2e test (`card footers span the full card width ...`) plus the existing board e2e and unit specs cover either form. If `overflow` changes, also open the Move menu on a change card in the e2e board fixture.
- Relationship: new

### ANALYSIS-132: The test title says "every card kind" but it checks only change and ticket cards

- Severity: suggestion
- Related task / requirement: 5.5
- Locations: `dashboard/e2e/dashboard.spec.ts:1203, 1217-1222` (working tree at `abe5d5d`)
- Observation: The test measures change and ticket cards, which is what task 5.5 asks for. Ad-hoc cards share the `compact-card` rules with ticket cards, so they are covered indirectly, but the title claims all kinds. `settled` already guarantees one ad-hoc card in the Done column.
- Proposed improvement and benefit: Add `await edges('[data-kind="adhoc"]')` to the loop, or rename the test to "change and ticket cards". The title then matches what the test guards, and a future padding rule for ad-hoc cards alone cannot break the footer unnoticed.
- Behavior-preservation risks: None; test-only.
- Evidence type: source observation
- Verification / measurement plan: Run the board e2e group once (QA).
- Relationship: new

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| None | — | — | — |

## Assessment and limitations

The P1 fix is correct and does not interact with any media rule. The test is robust and guards the regression: it measures only horizontal geometry, its 1px tolerance sits well below the 7px defect, and it selects cards by kind, so it does not depend on fixture details. The fix touches little code, but the same result is possible without an override: delete one line from `.change-card` (ANALYSIS-131). Both findings are optional suggestions, and neither blocks task 5.5. No build, test or package was run; QA owns execution. The review identity was the same before and after. The report write was refused, so this text is returned inline. The orchestrator owns triage and ledger status.
