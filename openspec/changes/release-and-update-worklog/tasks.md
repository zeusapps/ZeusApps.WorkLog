# Tasks

## 1. Release discovery and shared cache

- [x] 1.1 Add additive update settings and typed release metadata, including `updates.enabled` defaulting to true; verify old config files still load and automatic checks can be disabled without affecting journal settings.
- [x] 1.2 Implement fixed-source public GitHub release discovery with bounded HTTPS requests and semantic version comparison; verify mocked responses cover newer/equal/older versions, drafts/prereleases, malformed tags, absent assets, timeouts, rate limits, and invalid metadata, and assert requests contain no journal or repository data.
- [x] 1.3 Implement atomic local cache persistence, shared attempt coordination, 24-hour automatic throttling, and seven-day notice expiry; verify concurrent processes sharing a data directory make one automatic attempt and corrupt/unwritable caches fail without affecting journaling.
- [x] 1.4 Schedule cancellable background checks independently of MCP startup/tool execution; verify an unresolved request does not delay logging, querying, or shutdown, and disabled checking generates no requests or notices.

## 2. MCP hints and private client instructions

- [x] 2.1 Enrich successful log_work/query_work results with optional typed updateHint metadata from the local snapshot; verify advertised output schemas and JSON text fallback match with and without hints, tool errors remain intact, and stdout remains protocol-only.
- [x] 2.2 Keep hints outside journal persistence and omit them when a complete report would exceed 64 KiB; verify logging retries retain their observation IDs/timestamps and all report views, page boundaries, cursor reuse, and byte limits remain valid.
- [x] 2.3 Add shared private agent instructions for announcing each version once per session, the explicit update command, and restart requirements without implying installation consent; verify rerunning init refreshes owned Codex/Claude blocks and preserves unrelated private/shared settings.

## 3. Explicit checks and verified staging

- [x] 3.1 Register update command parsing, `--check`, and `--status` with appropriate data-directory support and mutually exclusive modes; verify help, available/up-to-date/failed results, exit codes, explicit checks when automatic checks are disabled, and status reads without network/downloads.
- [x] 3.2 Detect supported packaged Windows installations and validate the canonical target path, existing manifest, and writable destination; verify source/dotnet-host invocation, unsupported platforms/layouts, and unsafe targets receive manual-install guidance without file replacement.
- [x] 3.3 Download and stage the release ZIP/checksum with bounded sizes, verify SHA-256 before extraction, reject path traversal and link entries, and validate the staged version/runtime/executable; verify corrupt, mismatched, oversized, or unsafe packages leave the installation and journal untouched.

## 4. Windows replacement and recovery

- [x] 4.1 Add a helper apply mode that runs from staging, accepts a validated target-bound plan, waits for the originating updater to exit, and serializes updates per installation; verify packaged-process tests reject concurrent updates, active target processes, stale/invalid plans, and locked files without terminating any process.
- [x] 4.2 Implement application-file backup, replacement, post-install version verification, and recovery while preserving unowned files and the stable executable path; verify failure injection restores the prior version or retains documented recovery files and does not change journal/configuration/client registrations.
- [x] 4.3 Persist target-specific staged/installed/failed outcomes and expose them through `update --status`; verify the original command reports only staging before helper completion, helper failure is observable, and successful status explains restart/init steps.

## 5. Documentation and packaged acceptance

- [x] 5.1 Update README, installation/upgrade documentation, and architecture notes for the fixed public repository, automatic checks/opt-out, best-effort hints, explicit update/check/status, bootstrap installation, active-process refusal, recovery, and release operations; verify examples match CLI help and installation/data paths remain separate.
- [x] 5.2 Extend isolated packaged smoke checks to exercise staging/helper replacement using a prior-install fixture and controlled release responses; verify current source tests, local cache/disabled behavior, application-file recovery, unchanged journal data, and stable client registration paths without using authenticated live agent prompts.
- [x] 5.3 Run locked restore, Release build/test, packaging, and packaged acceptance checks; verify all required checks pass and the ZIP's reported version matches Directory.Build.props, release.json, and its checksum before any publication.

## 6. GitHub repository and first release

- [x] 6.1 Check GitHub authentication/account access and whether `zeusapps/ZeusApps.WorkLog` already exists, inspect outgoing tracked files/history for private content, and resolve conflicting remotes/repositories; verify the intended public personal-account target and releasable file set are recorded before pushing.
- [ ] 6.2 Align the primary branch with `main`, create the public repository if absent, configure origin, and push the prepared source without rewriting history; verify the remote default branch, public visibility, expected commit, and preservation of this checkout's existing saved journal identity.
- [ ] 6.3 Observe GitHub primary-branch validation and resolve failures; verify successful remote build, test, and packaging jobs before release tagging.
- [ ] 6.4 Push the matching first unpublished version tag (`v0.1.0` when still available), observe the gated release workflow, and resolve publication failures without moving published tags; verify a public release with the expected ZIP and checksum exists.
- [ ] 6.5 Download the published assets independently, verify checksum/version and packaged startup against an isolated journal, and check the real public release endpoint; verify equal installed/released versions produce no update notice and document the release URL and validation outcome before reporting feature completion.
