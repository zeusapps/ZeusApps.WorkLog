# Proposal

## Why

Ongoing board cards share a neutral appearance, making it harder to connect work to the colored repository entries in the sidebar. Reusing repository colors on card footers and beside repository names will help users scan a board containing several repositories.

## What Changes

- Give single-repository cards a softly tinted footer using the same repository color as the sidebar.
- Add a matching dot beside each repository name on change, ticket, and ad-hoc board cards.
- Replace sidebar position-based colors with one stable mapping from repository identity to a shared palette, independent of selection, order, and refresh.
- Keep card bodies neutral and preserve the meaning of status indicators; support readable text in light and dark themes.
- For cards associated with several repositories, show each repository's dot and use a neutral footer rather than implying one repository owns the card.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `work-dashboard`: Add consistent repository color cues to the sidebar and Ongoing board card footers and repository labels.

## Impact

- Dashboard presentation: `dashboard/src/app.html`, `dashboard/src/styles.css`, and shared color resolution exposed through `dashboard/src/app.ts`.
- Focused coverage in dashboard unit/component tests and the existing browser fixture for actual styles and theme readability.
- No new dependency, server API, journal format, preference field, or migration. Integration and MCP behavior are unaffected, so this presentation applies equally regardless of which client recorded the work.
- History, archive, and daily preparation styling are outside this change. Review inbox accents are also outside scope; minimal responsive wrapping of its controls is included to satisfy the Ongoing page's narrow-viewport acceptance, as authorized in the additional layout-fix round.
