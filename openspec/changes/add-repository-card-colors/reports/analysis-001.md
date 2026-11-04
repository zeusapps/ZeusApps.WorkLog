# Analysis report: add-repository-card-colors / 001

## Review target

- Reviewer / role: independent assigned reviewer / code-analyzer
- Client / version / mode / model (resolved ID): Codex CLI 0.165.0 / native agent mode / gpt-6.1-sol, high reasoning. Client and version supplied by the orchestrator; model is the role's resolved default.
- Date: 2026-11-04 19:29:07 UTC
- Change root / assignment: `openspec/changes/add-repository-card-colors` / ANALYSIS-REVIEW-001
- Task and requirement references: [tasks 1.1–3.3](../tasks.md); all three requirements in [spec delta](../specs/work-dashboard/spec.md): Stable repository color cues; Repository-colored Ongoing board footers; Readable repository accents in both themes. Context: [proposal](../proposal.md), [design](../design.md).
- Scope and relevant neighboring paths: `dashboard/src`, `dashboard/e2e`, `tools/WorkLog.Smoke/DashboardFixture.cs`, and `docs/architecture.md`. Source inspection covered `app.ts`, `app.html`, `styles.css`, `board.ts`, `api.ts`, `app.board.spec.ts`, both new repository-color files, and `e2e/dashboard.spec.ts`. Fixture file identity was checked; fixture contents were not needed because the implementation does not modify it.
- Reading limits and paths read outside scope: assigned planning artifacts above; `.agents/roles/code-analyzer.md`, `.agents/roles/project-facts.md`, `.agents/templates/analysis-report.md`; executed the assigned `scripts/review-identity.ps1`. No other paths read. No orchestrator workflow, live installation, personal settings, or default dashboard port inspected.
- Baseline commit: `7f45c373a6e4637edf8fd4a5853d11b5170bc1c5`
- Implementation identity: command below, before analysis:

```text
./scripts/review-identity.ps1 -Baseline 7f45c373a6e4637edf8fd4a5853d11b5170bc1c5 -Paths dashboard/src dashboard/e2e tools/WorkLog.Smoke/DashboardFixture.cs
HEAD: 7f45c373a6e4637edf8fd4a5853d11b5170bc1c5
Baseline: 7f45c373a6e4637edf8fd4a5853d11b5170bc1c5
Paths: dashboard/e2e dashboard/src tools/WorkLog.Smoke/DashboardFixture.cs
Manifest:
  ? dashboard/src/repository-colors.spec.ts
  ? dashboard/src/repository-colors.ts
  M dashboard/e2e/dashboard.spec.ts
  M dashboard/src/app.board.spec.ts
  M dashboard/src/app.html
  M dashboard/src/app.ts
  M dashboard/src/styles.css
Tracked diff SHA-256: 77e6ccd1c7e4fac4d05567ab3af81e4ab87de523de668ecc1a6b26e61bbf3711
Untracked file SHA-256:
  7189c8dbf8a47d0842fe6d7cf0aa3ab0411a073b326db79ec3a5c365ad6b5a74  dashboard/src/repository-colors.spec.ts
  dddebb1b52235f220e15d706e4121129b88952e643521698ec8579700d7934b9  dashboard/src/repository-colors.ts
Summary: head=7f45c37 baseline=7f45c37 files=7 diff=77e6ccd1c7e4 untracked=2/c03c9bfee23b
```

- Before/after identity: repeated the identical command after source inspection and the contrast calculation. HEAD, baseline, complete manifest, tracked hash, both untracked hashes, and summary matched exactly. After summary: `head=7f45c37 baseline=7f45c37 files=7 diff=77e6ccd1c7e4 untracked=2/c03c9bfee23b`. No unexpected mutation. The assigned report is excluded from implementation identity.
- Prior reports / rechecked findings: none.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Identity and scope | Assigned identity command before and after; `git diff -- dashboard/src dashboard/e2e tools/WorkLog.Smoke/DashboardFixture.cs` | Exact identity above; only seven assigned dashboard files changed | Stable review target; no integration/MCP changes. Dashboard is client-neutral, so provider parity checks are not applicable. |
| Shared resolution and determinism | Inspect `repository-colors.ts`, its spec, and `app.ts:476` | Eight fixed palette keys; hash consumes exact ID characters; only missing/whitespace IDs return null; helper needs no catalog, label, rank, selection, or theme | Identity remains independent of mutable board/catalog state and persistence. Representative mappings are pinned. Valid IDs absent from catalog still resolve. |
| Card identity and labels | Inspect `app.html:376`, `app.html:401`, `app.html:435`, `app.html:451`, `app.ts:483`, and `app.board.spec.ts:217` | Same helper drives sidebar and board; usable exact IDs deduplicated before label rendering and footer choice; shared and empty sets omit footer palette attribute; labels retain catalog fallback to ID | All three card kinds use decorative `aria-hidden` dots and keep names. Duplicates do not make a card shared; missing IDs receive no invented color. Component source covers shared duplicate references, missing associations, selection, catalog reorder, and all card kinds. No independent browser assertion of those cases performed here. |
| Duplication and simplification | Inspect helper, three label renderings, and CSS palette definitions | Mapping logic centralized; three short label fragments repeat the same presentation; explicit theme tables are finite and inspectable | No extraction or extra abstraction justified by a concrete defect. Repeated `distinctRepositories` calls allocate temporary sets and hash small ID lists; no measured performance problem warrants memoization or modifying board models. |
| Architecture and preserved behavior | Compare diff with `docs/architecture.md`, `board.ts`, order/persistence code, and existing board tests | Presentation remains client-side; source model, stage derivation, rank keys, API, and preference writes unchanged; footer override requires `.board-card[data-repository-color]`; bodies retain `--surface`; status selectors untouched | No architectural drift found. Archive/daily/review surfaces receive no new palette attribute. New label class appears only on the board. Source supports preserved actions, full-width footer geometry, and drag indicators; visual acceptance remains with QA/developer. |
| Theme readability | Node script via stdin reads only `dashboard/src/styles.css`, extracts all 16 palette/theme pairs, and calculates sRGB relative luminance/contrast; inspect footer inheritance | Minimum tinted normal/strong text contrast: dark 10.9236/7.3641; light 10.7578/4.7348. Neutral dark 7.0980; neutral light 4.3729 | Calculation completed successfully for every tinted combination; neutral light fails the new 4.5:1 requirement. This is a source-derived color calculation, not a rendered-browser measurement. ANALYSIS-001. |
| Acceptance coverage | Inspect `dashboard/e2e/dashboard.spec.ts:60` and added test at `:1312` | Synthetic cloned cards cover all palette CSS entries; test checks identity through selection, filtering, reload, and recorded column movement; neutral check compares background only | No E2E run by this reviewer, as assigned. Neutral footer text is excluded from the contrast query at `:103`, leaving ANALYSIS-001 undetected. Root reports 19 unit files / 250 passing tests and developer reports Release build passed; these are supplied results, not independently rerun checks. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval (requests, spawned processes, file reads/hashes, journal reads) | Existing dashboard poll; new color rendering | Source observation, not runtime measurement | `app.ts:450` polls every 2 seconds. Visible, non-overlapping idle board intervals request one catalog snapshot: about 30 requests/minute while inputs may remain unchanged. Overview requests follow revision changes; journal/day changes trigger activity/data refresh. Hidden, busy, or saving states skip intervals (`:2000`). Color additions cause zero HTTP requests, process launches, filesystem reads, or journal reads; they only hash currently rendered ID strings. Server internals are outside reading scope, so catalog-side tool/read counts are unmeasured. |
| Cache lifetime versus interval and refresh duration | Palette helper; existing revision keys and API client | Source observation | Palette has no cache or expiry and does bounded work proportional to ID characters. Existing journal/revision keys prevent unchanged activity/data reloads; `api.ts` uses `cache: no-store` and the catalog request still occurs each poll. Poll overlap is suppressed until completion. Server cache lifetimes and refresh duration were not measured; no cache or interval changed. |
| Unbounded state (collections, maps, files growing with use, the limit they hit) | Palette table and temporary distinct-ID sets; existing board order | Source observation | New persistent state is a fixed eight-entry array; temporary lists/sets are bounded by each card's associations and discarded after evaluation. No assignment map, preference field, or growing color file introduced. Catalog/card collections remain sized by existing input snapshots. Existing saved order has a 200-entry cap (`board.ts:8`) and is fitted to a 61,440-byte preference budget (`app.ts:105`, `:1955`) below the existing 64-KiB server limit. No broader preference-state audit performed. |
| Repeated full reads where a change check would do | Palette evaluation and existing catalog polling | Source observation | No new full file/journal/document reads. Hashing is repeated per render and distinct-list evaluation, but each operation reads only an ID string. Existing catalog refresh still occurs every 2 seconds; document/data refreshes use revision/snapshot checks. Server full-read behavior cannot be inferred from this client-only scope. No performance hypothesis was presented as measured. |
| External tool discovery (fixed paths, PATH search, package-manager locations, timeouts) | Runtime dashboard; added browser column-movement fixture | Source observation | New runtime presentation discovers or spawns no external tools. The browser test uses `spawnSync("dotnet", ...)` with a repository-relative Release smoke DLL (`dashboard.spec.ts:1340`): runtime executable is resolved through PATH, the DLL location is fixed to the build output, and no spawn timeout is supplied. This is test-only, was not run here, and is outside steady-state product work. No install-method or executable-discovery behavior changed. |

## Findings

### ANALYSIS-001: Neutral light-theme board footers miss the required text contrast

- Severity: warning
- Related task / requirement: 2.2, 3.1; Readable repository accents in both themes, including the shared/missing-identity footer scenarios required by Repository-colored Ongoing board footers.
- Locations: at the reviewed identity above, `dashboard/src/styles.css:33`, `:38`, `:243`, `:873`, `:882`; `dashboard/src/app.html:376`; `dashboard/e2e/dashboard.spec.ts:103`, `:117`, `:1360`.
- Observation: a shared or missing-identity card intentionally has no `data-repository-color`, so its neutral footer inherits `--muted: #657987` over `--surface-subtle: #fafbfd` in the light theme. The sRGB contrast ratio is **4.3728794268:1**, below the specified 4.5:1 for all footer text. This affects ordinary task wording, recorded status, and date text; the `strong` count has a separate color. These generic tokens predate the change, but the newly specified neutral-card cases still need to meet the explicit acceptance threshold. The added browser contrast query selects only cards with a palette attribute; its neutral check verifies the background, so passing that test does not cover neutral text readability.
- Proposed improvement and benefit: give neutral board-footer text a board-scoped readable color while keeping its neutral background and existing status-dot colors. Extend the browser measurement to include neutral/shared and missing-identity footers in both themes. This closes the explicit requirement without changing generic footer styling in archive or other surfaces.
- Behavior-preservation risks: keep background neutrality, distinct task-count styling, status-dot colors, footer wrapping, and other views intact; avoid changing global `--muted` to solve a board-only requirement.
- Evidence type: source observation plus executed source-derived colorimetric calculation; no rendered-browser measurement claimed. Calculation uses each sRGB channel normalized to 0–1, linearized at 0.04045 with the 2.4 exponent, luminance weights 0.2126/0.7152/0.0722, then `(lighter + 0.05) / (darker + 0.05)`.
- Verification / measurement plan: create or reuse synthetic shared and missing-ID board cards; measure computed footer backgrounds and all visible text colors in light and dark themes, including nested `strong` counts; assert every ratio is at least 4.5 and the neutral background remains equal to the neutral reference. Re-run relevant board/theme browser checks and inspect generic archive/daily surfaces for style leakage.
- Relationship: new finding; no earlier finding IDs assigned.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| None | No correction assigned | Initial review only | Not applicable |

## Assessment and limitations

Shared identity resolution, presentation scope, and unchanged board/status/preference behavior are proportionate to the design. No separate duplication, architecture, or measured performance finding is justified. ANALYSIS-001 requires orchestrator triage before declaring the contrast requirement complete.

Only this assigned report was written. Implementation identity matched before and after. No unit/browser/build suite was rerun, no browser or live installation was accessed, and no server-side performance measurement was attempted. No library/platform API claim requiring current documentation lookup was needed: checks used repository source and generic color arithmetic. QA/developer own rendered acceptance evidence; the orchestrator owns finding status and ledger/task updates.
