# Proposal

## Why

The dashboard's main purpose is a review loop: the agent edits a design, the user leaves inline REVIEW comments, and the agent addresses them. The dashboard only supports the middle step. The user cannot see which comments are still open across changes, and cannot see what the agent changed in a document since they last read it, including which of their comments were addressed. Both require opening and rereading every document.

## What Changes

- Add an open review comments overview:
  - Every REVIEW marker currently present in active change documents within the selected repositories, grouped by change and document.
  - Each entry shows the quoted passage and the comment, and links to the exact location.
  - Change cards and the reader header show open-comment counts.
  - The overview can also be scoped to archived changes, to find comments left behind at archive time.
- Add "changed since last read" for documents:
  - The dashboard remembers, per document, the content the user last marked as read.
  - When the document has changed since, the reader highlights changed and added passages, marks removed passages, lists review comments that disappeared since then (likely addressed), and lets the user step through changes.
  - The user marks a document as read explicitly. The first open of a never-read document sets the baseline automatically.
  - The user's own review-comment saves do not count as changes.
  - Change cards indicate documents updated since read.
- Read baselines are stored locally in the private data directory, bounded in size and count, and removable without affecting the journal or documents.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `document-feedback`: Adds the open review comments overview and counts.
- `openspec-browser`: Adds changed-since-last-read tracking, highlighting, and addressed-comment listing.

## Impact

- Server:
  - `/api/reviews` lists markers, reusing `ReviewMarker.Parse` with a per-file fingerprint cache.
  - `/api/reads` gets, sets, and returns the baseline text per document, backed by a new store under the private data directory (`dashboard-reads/`).
  - Catalog documents expose their fingerprint (added by `fix-dashboard-reliability`).
- Client: overview panel in Ongoing work, badges on cards, reader diff highlighting and navigation, and a "Mark as read" action. Adds one pinned line-diff dependency (`diff`, jsdiff) to `dashboard/package.json`.
- Tests: .NET tests for the marker listing and the read store, Angular tests for diff-to-block mapping, and Playwright scenarios.
- No journal, MCP, or package contract changes. Rolling back ignores `dashboard-reads/`.
- Prerequisites: `add-agent-workflow`, `fix-dashboard-reliability` (catalog fingerprints, revision-driven polling, bounded preferences).
