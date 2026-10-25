# Release operations

The public repository is [zeusapps/ZeusApps.WorkLog](https://github.com/zeusapps/ZeusApps.WorkLog), owned by the personal account `zeusapps`. Its primary branch is `main`. Source publication and tagging are deliberate maintenance operations; running the application does neither.

For initial publication, verify the authenticated GitHub account and target repository, review outgoing tracked files/history, create the public repository if absent, add its origin, and push main without rewriting history. Keep private client configuration, journals, caches, backups, and ignored artifacts local. An initialized checkout retains its saved local repository identity when origin is added; a new checkout can derive the GitHub identity. Existing journal observations are not automatically renamed or regrouped.

For each release:

1. Set the intended unpublished `MAJOR.MINOR.PATCH` in Directory.Build.props. Do not reuse or move a published tag.
2. Run locked restore, Release build/test, scripts/package.ps1, and both packaged acceptance checks:

   ```powershell
   dotnet run --project tools/WorkLog.Smoke -c Release --no-build -- --updates-only artifacts/releases/worklog-VERSION-win-x64/worklog.exe
   ./scripts/package-update-fixtures.ps1
   ```

3. Commit and push main. Observe the GitHub validation workflow and resolve failures.
4. Push its matching `vMAJOR.MINOR.PATCH` tag. The release workflow independently requires successful Windows validation and rejects tag/application version mismatches.
5. Observe the publish job. Verify that the public release contains `worklog-VERSION-win-x64.zip` and its `.sha256` file, download them independently, compare the hash, and check the extracted executable version/MCP startup against isolated data.
6. Verify the public latest-release endpoint returns the intended stable version and equal installed/published versions produce no update hint. Record the release link and outcome; required pending validation means release work is still in progress.

The application update feed uses the public latest stable release, excludes drafts/prereleases, and requires both the official Windows ZIP and checksum. Full agent-client smoke checks remain available locally; optional `--live-agents` checks require an explicit decision to send authenticated test prompts.

The launcher layout is a breaking package-format transition. Do not use the pre-launcher v0.1.1 updater as a migration fixture or advertise uninterrupted migration from it. The sole user's first installation follows the documented stop/reinstall/init/restart procedure. Ordinary package-format-2 updates never replace the stable launcher. Launcher contract 1/shared-data contract 1 retains journal schema 2, stored entry/retry formats, compatible settings writes and report continuations; changing that contract requires maintenance. Compatible rollback selects a previous payload and restores no data.

Production packaging enforces the application version from Directory.Build.props; the test-only fixture entry point separately compiles versions 0.0.1/0.0.2 without editing that file. Identical binaries with different manifest labels are insufficient evidence. v0.2.0 is the designated first launcher-based baseline: after publication and independent asset verification, use its verified ZIP/checksum as the pinned old-version input in subsequent release validation alongside fixture checks. The GitHub release notes record its verified checksum. Package outputs are immutable: select a fresh `-OutputDirectory` when rebuilding locally. ZIPs remain bounded at 256 MiB; source/fixtures, debug logs and client profiles are not packaged.

The planned F9 legacy update-status readiness fix is superseded by immutable activation and overlapping-status acceptance. Its old change remains unimplemented; do not sync its incompatible idle-replacement delta or mark its tasks complete merely because this feature ships. F6-F8 remain independently scoped. Actual local migration and GitHub version bump/publication require explicit operational action after validation.

## Release v0.2.0

This release introduces the stable launcher and immutable versioned payloads. Compatible future updates activate for new processes while existing MCP sessions continue on their original version. It includes explicit application rollback, offline running/selected-version status, stale-update protection, and settings merges that preserve unknown fields and independent concurrent edits.

**One-time migration required from v0.1.1 and earlier:** stop affected Codex/Claude sessions and WorkLog processes, retain the old application and consistent private-data backups, extract the checksum-verified new ZIP into a clean stable installation directory, then preview/apply init with the new launcher for each repository/client and restart normally. Preserve ownership manifests, repository identities, ticket context and instruction preferences. Follow the [migration procedure](installation.md#one-time-migration-from-the-legacy-layout). Do not use the legacy in-place updater to establish the new layout. Journal schema 2 is unchanged.

Claude initialization now reconciles owned equivalent Windows project-path spellings while preserving unrelated settings. Reports default to grouping by the complete qualified ticket set across change names; `period.groupBy: "change"` retains the previous grouping. Begin fresh report queries after upgrading, and reconnect clients to refresh tool discovery.

Worktree acceptance verifies manually created external worktrees with Claude Code 2.1.310 and Codex CLI 0.164.0: Claude inherits the main registration, while Codex needs explicit worktree init. Both retain shared identity, ticket context and history, and main-checkout connections survive setup. See the [worktree procedure](installation.md#verified-terminal-client-worktree-setup) and [versioned evidence](mcp-validation.md#terminal-client-worktree-startup-validation). Desktop and managed worktree sessions remain outside that verification.

Release gates include locked restore, a zero-warning Release build, all 196 tests, packaged/native client and worktree smoke, and launcher/mixed-version acceptance. The tag workflow independently validates the exact release source before publishing the Windows x64 ZIP and SHA-256 checksum. Published-asset verification is recorded in the GitHub release notes after publication.

v0.2.0 establishes the first public launcher-based baseline once its published assets have been independently verified. Retain that verified ZIP and checksum for future compatible-update acceptance; pre-launcher v0.1.1 is not a supported mixed-version fixture.

## Release v0.1.1

This patch release fixes F1-F5: paired ticket provider/scope setup, unresolved-ID reporting, shared Git worktree context, the bugfix classification alias for existing configurations, and persistent per-client managed-instruction opt-out/removal with explicit re-enable. The separate F6 server-startup investigation remains planned and is not claimed as fixed by this release.

Existing applications can initialize Claude with `worklog init --clients claude --no-instructions --ticket-provider jira --ticket-scope company.atlassian.net`. Later init runs preserve the opt-out; `--instructions` restores managed instructions. Restart clients after upgrading and configuring. Legacy configuration is promoted without rewriting journal observations; reconcile conflicting saved identities or ticket settings explicitly as described in [installation](installation.md#worktree-context-and-legacy-configuration).

Release validation includes the 127-test suite, Windows packaging, packaged update acceptance, and local isolated Codex/Claude checks with instructions enabled and disabled. The release workflow independently rebuilds, tests, and packages the exact version tag before publishing its ZIP and checksum. Published-asset verification and its resulting hash are recorded in the GitHub release notes after publication.

## Verified release v0.1.0

Published [v0.1.0](https://github.com/zeusapps/ZeusApps.WorkLog/releases/tag/v0.1.0) on 2026-10-20 from commit `33d620245246cd17267a18f822cc53e618790c4f`.

- [Primary-branch validation](https://github.com/zeusapps/ZeusApps.WorkLog/actions/runs/39496322452) passed all 83 tests, Windows packaging, and packaged update acceptance.
- [Release validation and publication](https://github.com/zeusapps/ZeusApps.WorkLog/actions/runs/39496648248) passed and uploaded the Windows ZIP and checksum.
- The independently downloaded ZIP matched SHA-256 `f51af262643bf908de5093d36c01e87425eb70f7db5c990dbd4993be3fe9c073` and its executable reported `0.1.0`.
- The published executable's explicit check and normal update command both reported installed/published `0.1.0` as up to date. Isolated stdio MCP initialization, logging, querying, equal-version hint suppression, and clean shutdown passed.
- The existing checkout's saved local journal identity remained unchanged after adding the GitHub origin. Local smoke checks also confirmed private Codex/Claude registration and instructions, journal migration backups, helper replacement, data/configuration preservation, and active-server refusal.
