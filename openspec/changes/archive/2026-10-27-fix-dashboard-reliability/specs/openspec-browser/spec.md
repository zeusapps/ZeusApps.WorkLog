# Spec Delta

## MODIFIED Requirements

### Requirement: Scoped document search
The dashboard SHALL search Markdown contents across discovered changes within the selected repositories and an explicit active, archived, or all-changes scope. Search SHALL be case-insensitive literal text search, return matching passages with repository, change, document, section when available, and archive state, and open the exact document location on selection. Section labels SHALL come from Markdown headings and SHALL NOT come from lines inside fenced code. Search SHALL return at most a bounded number of matches and indicate when that limit was reached, distinguish no matches from incomplete or unavailable sources, and refresh results when source content changes. Unavailable-source notices SHALL be limited to sources associated with the selected repositories and to documents within the searched scope; notices about repositories outside the selection SHALL NOT mark a search incomplete. It SHALL not search arbitrary files outside known OpenSpec roots.

#### Scenario: Search across selected repositories
- **WHEN** the user selects two repositories and searches for a phrase in active changes
- **THEN** only matching active-change documents associated with those repositories appear, with passages and source labels

#### Scenario: Open a result in a document without headings
- **WHEN** a match occurs in a Markdown document without headings
- **THEN** selecting the result opens the matching passage using a document position fallback

#### Scenario: Search is incomplete
- **WHEN** matching sources are unavailable or the result count exceeds one response
- **THEN** the interface identifies unavailable sources or indicates that the match limit was reached instead of presenting a complete empty result or silently truncating matches

#### Scenario: Unrelated repository lacks a source
- **WHEN** a repository outside the current selection has no available OpenSpec source and the user searches the selected repository
- **THEN** the results are not marked incomplete because of that repository

#### Scenario: Comment line inside code
- **WHEN** a match follows a fenced code block containing a line that starts with `#`
- **THEN** the match's section label is the nearest preceding Markdown heading

### Requirement: Keyboard-accessible reading and navigation
Repository filters, change selection, document tabs, heading navigation, search results, review comment controls, and link-copy controls SHALL be keyboard operable with visible focus. Escape SHALL dismiss the innermost active interaction first. While focus is in a text input, the review composer, or a delete confirmation, Escape SHALL cancel or leave that interaction and SHALL NOT close the reader or discard typed text. Browser back and forward navigation SHALL preserve meaningful document routes, and timeline facts SHALL remain available as readable text without relying exclusively on color, hover, or pointer interactions.

#### Scenario: Navigate without a pointer
- **WHEN** the user selects a change, opens a document, searches, and follows a result using only the keyboard
- **THEN** each action is available, focus remains visible, and the matching document location is reachable

#### Scenario: Escape while writing a comment
- **WHEN** the user is typing a review comment and presses Escape
- **THEN** the reader stays open, the typed text is retained, and focus leaves the composer; a further Escape with focus outside inputs closes the reader
