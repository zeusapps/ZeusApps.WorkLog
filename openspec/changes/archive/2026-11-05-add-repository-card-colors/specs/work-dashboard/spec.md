# Spec Delta

## ADDED Requirements

### Requirement: Stable repository color cues
The dashboard SHALL assign each repository a color from a fixed palette based on its repository identity. Sidebar repository dots, Ongoing board repository dots, and single-repository board footer tints SHALL use the same assigned color family. Assignment SHALL be independent of repository label, catalog order, repository selection, board text filtering, card rank, and board column, and SHALL remain consistent across refreshes and later visits for the same identity. Light and dark themes SHALL preserve the color family while allowing different shades. Different repository identities can share a palette color; visible names SHALL remain the authoritative identifier. The All repositories sidebar entry SHALL remain visually distinct from a specific repository.

#### Scenario: Match a card to the sidebar
- **WHEN** a single-repository card is displayed on the Ongoing board
- **THEN** its repository dot and footer tint use the same color family as that repository's sidebar dot

#### Scenario: Selection and ordering do not change identity colors
- **WHEN** the user changes repository selection, filters or reorders board cards, or refreshes a catalog with repositories added, removed, renamed, or reordered
- **THEN** each remaining repository identity retains its assigned color family

#### Scenario: Reload and column movement
- **WHEN** a card moves to a different board column following recorded work and the dashboard is later reopened
- **THEN** its repository color family matches the assignment before the move and reload

### Requirement: Repository-colored Ongoing board footers
Every change, ticket, and ad-hoc card on the Ongoing board SHALL show a colored dot beside each repository name. A card associated with exactly one distinct repository SHALL have a softly tinted, full-width footer in that repository's color family. The main card body SHALL remain neutral and SHALL NOT gain a repository-colored edge stripe. A card associated with several distinct repositories SHALL keep a neutral footer and show a corresponding dot beside every repository name. A card with no usable repository identity SHALL keep a neutral footer without an invented repository color. Color cues SHALL preserve the card's existing content, footer layout, actions, and board behavior.

#### Scenario: All card kinds receive color cues
- **WHEN** change, ticket, and ad-hoc cards for a repository are displayed across the board columns
- **THEN** each single-repository card has the repository-colored footer and a matching dot beside its repository name

#### Scenario: Shared change has several repositories
- **WHEN** a change card is associated with two distinct repositories
- **THEN** its footer is neutral and each repository name has its own matching dot, without choosing one repository as the owner

#### Scenario: Duplicate repository references
- **WHEN** a card repeats the same repository identity in its repository associations
- **THEN** it is treated as a single-repository card for footer tinting and shows that repository label and dot once

#### Scenario: Repository identity is unavailable
- **WHEN** a card has no usable repository identity
- **THEN** it retains its neutral footer and existing fallback identification without a fabricated color assignment

### Requirement: Readable repository accents in both themes
Repository accents SHALL work in the dashboard's light and dark themes. All footer text SHALL retain a contrast ratio of at least 4.5:1 against its rendered footer background. Repository colors SHALL supplement visible repository names and SHALL NOT replace existing text or status cues. Repository dots SHALL be decorative for assistive technology. Recorded-status dots and status badges SHALL retain their existing meanings and color assignments, independently of repository colors.

#### Scenario: Change theme
- **WHEN** the user switches between light and dark themes while viewing the board
- **THEN** footer tints adapt to the theme, footer text meets the required contrast, and repository dots continue to match the sidebar color families

#### Scenario: Identify work without color
- **WHEN** repository colors cannot be distinguished by the user
- **THEN** repository names and recorded-status text still identify the repository and work status, without extra repository-dot announcements
