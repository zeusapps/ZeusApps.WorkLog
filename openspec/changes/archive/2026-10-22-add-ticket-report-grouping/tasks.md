# Tasks

## 1. Request and identity selection

- [x] 1.1 Append optional `GroupBy = "ticket"` to `QueryRequest` with MCP input guidance and normalize/validate it in `ReportPager`; verify omitted and explicit ticket agree and null, blank, unsupported values, and misspelled fields return useful errors without journal writes.
- [x] 1.2 Pass grouping mode through `ReportBuilder` to `WorkIdentity`, using the existing qualified ticket-set encoding for ticket mode and existing identity fallback without qualified tickets; verify the F8 single-ticket example, multiple changes, sorted/deduplicated sets, namespace/repository isolation, and unresolved-ID fallback in reporting tests.
- [x] 1.3 Update daily identity descriptions without changing report shape/version or stored metadata; verify explicit change preserves legacy identities and omitted grouping matches ticket, each observation has one group assignment, and all four views retain their existing observation projections.

## 2. Continuation and protocol acceptance

- [x] 2.1 Bind normalized grouping mode into cursor matching; verify omitted/explicit ticket continuations agree, switching modes fails, and ticket-mode pages preserve every snapshot observation once across filters, timestamp ties, concurrent backdated writes, and timezone day boundaries.
- [x] 2.2 Extend MCP discovery and schema-validation tests for the new request property, both modes, invalid inputs, all views, summary omission, and 64 KiB bounds; verify structured content matches JSON text fallback and subsequent queries work after a rejected request.
- [x] 2.3 Add an isolated packaged journal smoke fixture containing same-ticket observations with and without change names; verify native tool calls combine them in omitted/explicit ticket mode and separate them in explicit change mode and preserve original fields, pagination, and journal data.

## 3. Documentation and completion

- [x] 3.1 Update README reporting examples, architecture, and validation docs with `period.groupBy`, the ticket default and explicit legacy change mode, ticket-set semantics, unresolved fallback, page-fragment handling, and restart-query guidance after upgrade; verify examples match protocol fixtures.
- [x] 3.2 Run the design's focused reporting/MCP checks, full test suite, package script, and packaged smoke; verify recorded results pass and required unavailable checks remain pending. Validate this OpenSpec change strictly after any artifact edits.
