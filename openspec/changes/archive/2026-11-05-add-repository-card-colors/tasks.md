# Tasks

## 1. Shared repository palette

- [x] 1.1 Add a pure repository-ID-to-palette helper with eight fixed keys and a neutral fallback for missing IDs; verify focused unit cases cover determinism, representative fixed mappings, catalog-independent IDs, and independence from labels or ordering.
- [x] 1.2 Define shared dot and theme-specific footer tokens and wire sidebar dots through the shared helper, replacing positional color rules; verify component cases show unchanged assignments when the catalog is reordered, repositories are added or renamed, and selection changes, while All repositories stays neutral.

## 2. Board presentation

- [x] 2.1 Render decorative matching dots beside each distinct repository name on change, ticket, and ad-hoc board cards; verify component fixtures cover all three kinds, shared cards, duplicate IDs, label fallback, and dots hidden from assistive technology.
- [x] 2.2 Tint full-width single-repository board footers in light and dark themes, keeping shared or missing-identity footers neutral; verify sidebar/card palette matching, neutral bodies, unchanged status cues, and preservation of existing board order and no-preference-write behavior through the focused component suite.
- [x] 2.3 Make the measured overflowing board filter and Review inbox controls wrap within a 390px Ongoing page, as authorized in the additional layout round; verify both control groups fit without hiding overflow and real card/footer geometry remains intact in browser acceptance.

## 3. Browser acceptance and independent review

- [x] 3.1 Extend the existing synthetic browser fixture only as needed and verify actual rendered colors, every palette's footer text contrast of at least 4.5:1 in both themes, neutral shared footers, and stable assignments after filtering, refresh, reload, and column movement; save light/dark and narrow-viewport screenshots under this change and confirm no horizontal overflow or clipped footer content.
- [x] 3.2 Run the dashboard unit suite, production asset build, and relevant existing board/theme browser checks; record passing results or unresolved failures and inspect archive and daily surfaces for accidental accent leakage without changing the user's installation.
- [x] 3.3 Obtain independent QA and code-analyzer reports against the stable implementation under the delegated repository workflow, triage all findings in reports/findings.md, and verify required spec scenarios and blocking findings are resolved before declaring the feature complete.
- [x] 3.4 Keep future browser screenshot output in ignored test results so browser acceptance cannot recreate the archived change; verify the focused browser test and independent QA/code analysis before the archive commit and merge.
