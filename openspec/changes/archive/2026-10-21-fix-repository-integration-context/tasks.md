# Tasks

## 1. Shared repository configuration

- [x] 1.1 Add additive common-directory repository context and per-client instruction preferences while retaining legacy path records; verify old configuration loads, new settings round-trip, and absent preferences default to enabled.
- [x] 1.2 Resolve shared context in RepositoryRegistry through the canonical common Git directory and integrate it with init/server startup; verify main-first and worktree-first setup, with and without origin, preserve one identity and ticket pattern/provider/scope across restarts.
- [x] 1.3 Promote compatible legacy context without erasing known values and reject conflicting nonempty identities/settings; verify incomplete records merge, conflicting scopes/identities fail without persistence, missing legacy paths remain untouched, and independent clones do not inherit settings by remote URL.
- [x] 1.4 Adapt EntryFactory's effective-repository lookup to shared and legacy context; verify explicit repository overrides retain correct scope, conflicting contexts still fail, and ticket-only observations with only an unresolved ID still reject without writes.

## 2. Init options and saved preferences

- [x] 2.1 Add paired `--ticket-provider`/`--ticket-scope` options and mutually exclusive `--instructions`/`--no-instructions` options; verify CLI help, normalization, unsupported/blank/partial ticket values, and conflicting instruction options fail before writes.
- [x] 2.2 Apply explicit ticket values and selected-client instruction preferences through InitializationPlan; verify omission preserves context, pattern, and preferences, dry-run changes no files, and outputs describe effective instruction mode and restart requirements.

## 3. Optional instruction management

- [x] 3.1 Add validated owned-block removal to ManagedBlock; verify well-formed block removal preserves surrounding content, missing blocks are unchanged, and malformed/duplicate markers fail during preflight.
- [x] 3.2 Separate Claude server registration from instruction planning; verify fresh opt-out creates no instruction file/imports, existing blocks are removed while unrelated instructions remain, and repeated init retains the server and disabled preference.
- [x] 3.3 Separate Codex server registration from instruction merging; verify opt-out removes only its owned block, preserves surrounding TOML and developer instructions, and does not copy inherited global instructions into a fresh local value.
- [x] 3.4 Update private-target validation/exclusions and ownership handling for disabled mode; verify an untouched tracked Claude instruction file is allowed, a tracked mutation target remains a conflict, enabled-mode behavior is preserved, and interrupted initialization remains safely rerunnable.
- [x] 3.5 Cover mixed-client preferences, explicit re-enable, and opt-out inheritance during worktree init; verify changing Claude does not change Codex, `--instructions` restores one block, and other worktrees' existing files are not silently modified.

## 4. Reporting and branch classification

- [x] 4.1 Extend SqliteEntryStore's bare-ID ticket condition to match extractedTicketId with correct SQL grouping and update the advertised QueryRequest description; verify unresolved historical entries match bare IDs, remain excluded by qualified filters, and all other filters and cursor snapshots still constrain results.
- [x] 4.2 Normalize the missing `bugfix` alias for fresh and deserialized configurations; verify case-insensitive custom overrides survive, bugs/hotfix still work, and previously stored unclassified observations retain their classification.

## 5. Documentation and integration validation

- [x] 5.1 Update README, installation/configuration, and architecture documentation with existing-app opt-out, paired ticket setup, shared worktree context, conflict reconciliation, query semantics, and restart/rollback steps; verify examples match CLI help and clearly distinguish unresolved metadata from qualified references.
- [x] 5.2 Reconcile enabled/disabled instruction behavior with the existing release change's update-hint guidance when synchronizing specs; verify enabled blocks retain update instructions and upgrade/init refresh does not recreate opted-out blocks.
- [x] 5.3 Run the Release build/test suite and isolated packaged integration checks; verify all required checks pass, generated MCP registration remains valid in enabled and disabled modes, and only the separate F6 change owns fresh-worktree discovery findings.
