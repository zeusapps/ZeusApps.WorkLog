# Tasks

## 1. Alias resolution and safe ownership

- [x] 1.1 Add selected-checkout project-alias resolution in `ClaudeIntegration` using Windows slash-normalized ordinal case-insensitive equality and existing exact-case behavior elsewhere; verify lowercase-only, canonical-only, multiple aliases, unrelated projects, and distinct worktree fixtures in integration tests.
- [x] 1.2 Validate every matching project's shape and WorkLog ownership before planning writes; verify conflicts under a noncanonical alias leave client config, manifest, exclusions, instruction files, and saved/in-memory preferences unchanged and identify the offending key.
- [x] 1.3 Reconcile only `mcpServers.worklog` under existing aliases plus the canonical root key while preserving each object's unrelated fields, other servers including `worklog-legacy`, and spelling; verify repeated init creates no new aliases or duplicate owned definitions and no trust fields are copied.

## 2. Integration compatibility

- [x] 2.1 Make dry-run identify affected aliases without writes and keep instruction controls independent of reconciliation; verify default, persistent `--no-instructions`, re-enable, and dry-run tests preserve existing F1-F5 behavior.
- [x] 2.2 Add isolated packaged/native Claude acceptance for lower/mixed-case project entries and launch paths in `tools/WorkLog.Smoke`; verify `mcp get` discovery and the existing connection probe succeed from both stored spellings, preserve sentinel settings, and record versions/cwd/logs without real-profile access.

## 3. Documentation and completion

- [x] 3.1 Update installation and MCP validation docs with tested Windows alias behavior, rerun-init/restart procedure, conflict handling, and unseen-spelling/F6 limits; verify every native behavior claim cites the captured fixture result and no global registration fix is claimed.
- [x] 3.2 Run the design's integration checks, full test suite, package script, and packaged client smoke; verify required native results passed, preserve logs, and keep completion pending if a required CLI/check is unavailable. Validate this OpenSpec change strictly after any artifact edits.
