# Design

## Context

See proposal.md for the motivation and specs/work-dashboard/spec.md for acceptance requirements. A design is included because identity stability, shared cards, and theme contrast need explicit choices before presentation work starts.

- `dashboard/src/styles.css` assigns sidebar dots one of three muted colors through `.repo-link:nth-of-type(...)`. These are positions, not repository assignments.
- `dashboard/src/app.html` renders sidebar repositories from the catalog and three board card kinds. Repository names are currently plain text, with change-card names joined into one string.
- `BoardCard.repositories` in `dashboard/src/board.ts` already carries repository identities. `Repository` in `dashboard/src/models.ts` has `id` and `label`; no server-side color data is needed.
- Board footers already span the card width through `.board-card .card-footer`, with a shared neutral background in `.card-footer`. Archive and other views reuse card classes, so accent rules must be scoped to the board.
- Theme values live on `:root` and `:root[data-theme="light"]`. Sidebar backgrounds use their own dark styling even in the light theme.
- Existing board coverage lives in `dashboard/src/app.board.spec.ts` and `dashboard/e2e/dashboard.spec.ts`, including both themes, reorder behavior, and overflow. `tools/WorkLog.Smoke/DashboardFixture.cs` supplies synthetic browser data.

## Goals / Non-Goals

**Goals:**
- Resolve identity colors in one pure client-side helper, independent of mutable view state.
- Use a small shared palette with explicit theme tokens so all footer combinations can be checked for contrast.
- Keep existing full-width footer geometry, reorder indicators, hover/focus treatment, and wrapped labels usable.

**Non-Goals:**
- User-selected colors, stored color assignments, or a promise of globally unique colors.
- Changes to board grouping, column derivation, rank keys, fetching, polling, server contracts, or client integrations.
- Extending repository accents to other dashboard card surfaces in this change.

## Decisions

### 1. Map the existing repository identity to a fixed palette

Add a small pure helper, for example `dashboard/src/repository-colors.ts`, that deterministically hashes the exact repository ID to one of eight fixed semantic palette keys. Keep the algorithm and palette order fixed, with representative identity-to-key fixtures to catch unintended reassignment. Treat the existing ID as authoritative; do not independently normalize labels, Git URLs, paths, or case in the presentation layer.

Expose the helper through `App` so the sidebar and board consume the same result. Missing or whitespace-only IDs return a neutral fallback; a valid ID absent from the current catalog still resolves normally. This requires no persistence and assigns the same identity consistently across browsers.

Use the current muted teal, blue, and sand families as a visual starting point and add five distinguishable muted families. Exact shades and tint strengths are implementation tuning choices, constrained by the acceptance checks below. A finite palette can repeat; names remain visible.

Alternatives: catalog index repeats the existing instability when discovery order changes; stored assignments add preference lifecycle work without improving this bounded change; unconstrained generated hues make contrast harder to verify.

### 2. Share palette keys and define theme-specific dot and footer tokens

Use explicit palette classes or data attributes backed by CSS custom properties for a dot color, a light-theme footer tint, and a dark-theme footer tint. Remove the sidebar `nth-of-type` color rules. Keep All repositories as its existing neutral outline.

Bind only trusted palette keys produced by the helper. Apply footer background overrides under `.board-card`, preserving the existing padding, border radius, divider, and footer layout. Do not recolor generic `.card-footer`, status dots, or badges. Retain neutral body backgrounds and the current drag/drop indicators.

Dots in the sidebar and on the board use the same palette family; select shades that remain visible on both the permanently dark sidebar and the theme-specific card body. Footer backgrounds use a soft shade of that family, with text tokens adjusted locally where needed to meet 4.5:1 contrast, including the date, task count, and status text. Theme switching changes shades without changing palette assignment.

Alternatives: tinting the entire card makes dense board text compete with the accent; a left stripe was considered during exploration and the user selected the footer instead; using one raw background color in both themes risks poor contrast.

### 3. Render repository labels individually and handle shared cards neutrally

Within board cards, render a label-and-dot pair for each distinct repository ID, preserving existing label fallback and source text. Hide decorative dots from assistive technology and retain readable repository names. Keep wrapping and long-name behavior usable.

For exactly one distinct usable repository ID, set the card's footer palette key. For several distinct IDs, retain the neutral footer and show the dot for each repository. With no usable ID, retain the existing fallback identification and neutral footer. Deduplicate before counting or rendering; duplicate references do not make a card shared. This remains a presentation concern and does not change the board's identity or ranking rules.

The neutral shared footer is a documented default for an existing model case, consistent with showing all repository associations. Choosing the first repository would falsely imply ownership and depend on array order; gradients introduce an extra visual language that the user has not requested.

### 4. Verify behavior and actual rendering proportionally

Add focused helper coverage for determinism, label/order independence, missing IDs, catalog-independent resolution, and fixed representative mappings. Extend board component fixtures to verify matching sidebar/card keys for all three card kinds and neutral shared-card handling. Reuse existing board ordering and no-save checks rather than duplicating those implementations.

Use the existing browser fixture to inspect rendered dot colors and footer backgrounds in both themes, including selection changes, refresh/reload, and a narrow viewport. Ensure every palette entry appears in synthetic test data or an equivalent fixture so all footer text/background combinations are checked at 4.5:1 or better. Capture light and dark screenshots in the change's screenshots directory for visual review. Confirm no horizontal overflow or clipped footer content and that the neutral shared footer remains neutral. Extend the existing smoke fixture only if its current synthetic data cannot cover these cases.

The browser harness keeps port 47942 as its default and accepts a test-only port override shared by its server, configuration, and test URL. This allows isolated acceptance when that port is already occupied without probing the user's dashboard or stopping another process. Screenshot output accepts a test-only destination or prefix so independent reviewers can rerun acceptance without overwriting earlier evidence. Contrast coverage includes neutral shared/missing-identity footers as well as all eight colored footers.

The additional user-authorized layout round measured document width at 423px in a 390px viewport. The Review inbox controls extended to 423.4px, and the fixed-width board filter extended to 410.4px; every real card footer fit within its card. Apply narrowly scoped responsive wrapping to these two Ongoing page control groups, preserving card/footer geometry and avoiding overflow hiding. This is the minimal responsive exception for Review inbox controls, without adding repository accents to that surface. The narrow browser acceptance must verify the controls fit as well as the full document and footers.

The column-movement browser fixture temporarily augments the activity API response for one synthetic proposal, then removes the exact interception handler in finally. The actual app derives the new column from that activity record. This avoids persistent journal mutations and task-progress dependencies, preserving subsequent test counts and allowing the same assertions in normal and packaged no-Node runs. This is synthetic API fixture evidence, not a persisted journal observation. Verify both complete board sequences, including later filtering and ordering checks, against the same source target.

Developer implementation is followed by independent QA and code-analyzer review under the repository workflow; QA checks the observable spec scenarios and browser evidence, and analysis checks shared resolution and scope. No live agent evaluation or personal installation is required for this presentation-only change.

## Risks / Trade-offs

- [Existing sidebar colors can change on first upgrade] -> Document the switch from list position to stable identity; no durable color preference exists to migrate.
- [Two repositories can map to the same palette entry] -> Retain visible names; eight curated entries improve variety without promising uniqueness or adding persistent assignments.
- [Footer tint can reduce small-text contrast] -> Check every palette/theme combination in the browser and adjust tint or footer text tokens before acceptance.
- [Shared card classes can leak accents into other views] -> Scope footer and dot-label styling to the board and inspect archive and daily surfaces for unintended styling changes.
- [Long repository names or several labels can wrap] -> Keep label-and-dot pairs together where practical while permitting text wrapping, and verify a narrow viewport.

## Migration Plan

Ship through the normal dashboard asset build. No data or preference migration is needed. A normal page reload picks up the assets; rolling back restores the previous presentation without changing stored data. Installing or switching a local development build remains a separate user-authorized operation.
