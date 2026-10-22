# Tasks

## 1. Request and identity selection

- [ ] 1.1 Append optional `GroupBy = "change"` to `QueryRequest` with MCP input guidance and normalize/validate it in `ReportPager`; verify omitted and explicit change agree and null, blank, unsupported values, and misspelled fields return useful errors without journal writes.
- [ ] 1.2 Pass grouping mode through `ReportBuilder` to `WorkIdentity`, using the existing qualified ticket-set encoding for ticket mode and existing identity fallback without qualified tickets; verify the F8 single-ticket example, multiple changes, sorted/deduplicated sets, namespace/repository isolation, and unresolved-ID fallback in reporting tests.
- [ ] 1.3 Update daily identity descriptions without changing report shape/version or stored metadata; verify default identities are unchanged, each observation has one group assignment, and all four views retain their existing observation projections.

## 2. Continuation and protocol acceptance

- [ ] 2.1 Bind normalized grouping mode into cursor matching; verify omitted/explicit change continuations agree, switching modes fails, and ticket-mode pages preserve every snapshot observation once across filters, timestamp ties, concurrent backdated writes, and timezone day boundaries.
- [ ] 2.2 Extend MCP discovery and schema-validation tests for the new request property, both modes, invalid inputs, all views, summary omission, and 64 KiB bounds; verify structured content matches JSON text fallback and subsequent queries work after a rejected request.
- [ ] 2.3 Add an isolated packaged journal smoke fixture containing same-ticket observations with and without change names; verify native tool calls combine them only in ticket mode and preserve original fields, pagination, and journal data.

## 3. Documentation and completion

- [ ] 3.1 Update README reporting examples, architecture, and validation docs with `period.groupBy`, compatible defaults, ticket-set semantics, unresolved fallback, page-fragment handling, and restart-query guidance after upgrade; verify examples match protocol fixtures.
- [ ] 3.2 Run the design's focused reporting/MCP checks, full test suite, package script, and packaged smoke; verify recorded results pass and required unavailable checks remain pending. Validate this OpenSpec change strictly after any artifact edits.
