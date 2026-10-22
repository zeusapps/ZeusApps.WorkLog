# Design

## Context

See proposal.md and [the handoff](../../../../docs/handoffs/2026-10-21-claude-integration-flaws.md). Baseline is v0.1.1 / `fa48ada`. `RepositoryInitializer.Run` obtains Git's root, loads `.worklog/integration.json`, plans client edits against copied settings, validates private targets, and applies only after preflight. `ClaudeIntegration.Plan` currently accesses `projects[setup.Root.Replace('\\', '/')]` exactly, then calls `ValidateOwnership` only on that object's `mcpServers`.

The reporter observed `C:/Code/AnalyticsPlatform` versus `C:/Code/analyticsplatform`; client lookup behavior has not been independently reproduced. Official [Claude MCP docs](https://code.claude.com/docs/en/mcp-quickstart), consulted through Context7 on 2026-10-21, establish local-scope project entries, isolated `CLAUDE_CONFIG_DIR`, and `mcp get/list`, but do not guarantee Windows case normalization. Native acceptance must record the tested CLI version.

Implementation on 2026-10-22 uses the current `cb9e7b7` source baseline, including the stable launcher and coordinated settings writes introduced after the original planning baseline above. The isolated `AliasRepo`/`aliasrepo` fixture independently reproduced the client lookup difference on Claude Code 2.1.306; both spellings connected after reconciliation. [Validation evidence](../../../../docs/mcp-validation.md#windows-claude-project-alias-validation) records versions, launch paths, logs and the remaining limits. The original reporter's path is historical context only.

## Goals / Non-Goals

**Goals:** Reconcile known aliases without bypassing ownership or changing unrelated project settings. Make fixtures reproducible with no reporting-machine files.

**Non-Goals:** Global registration/F6, enumerating every possible letter-case permutation, filesystem link resolution, combining separate checkouts, copying a project's trust settings to aliases, or managing `worklog-legacy`. That name belongs to the reporter's workaround and is unowned here.

## Decisions

### Resolve aliases only within the selected checkout

On Windows compare existing project keys to the normalized Git-root key after converting backslashes to forward slashes, using ordinal case-insensitive equality. Compare full keys; do not use prefix matching, repository identity, or shared Git common directory. Another worktree is a different client project even though it shares ticket context. Outside Windows retain exact-case behavior. Exotic case-sensitive Windows directory configurations are outside this narrow compatibility fix; do not silently broaden to symlinks or path traversal normalization.

Use the union of existing equivalent keys and the canonical forward-slash Git-root key as write targets. Keep existing key spellings; add the canonical key when absent. Set only each target's `mcpServers.worklog` using independently constructed/cloned JSON nodes. Preserve all other fields and server names under every target. Reusing one lowercase key alone was rejected because it leaves the reported canonical-case launch unserved; deleting/merging alias objects could lose unrelated Claude settings. Do not create unseen case variants. Document that a future, previously unseen launch spelling may require rerunning init after Claude has created its project entry.

### Validate all aliases before planning the write

Collect and validate matching project/mcpServers object shapes and ownership before changing any alias. Apply existing ownership rules to every alias: an occupied `worklog` without a Claude ownership manifest, or an owned command changed from the previous manifest, is a conflict even if another alias is clean. Do not infer ownership from the server name, command resemblance, or the existence of a matching sibling alias. Errors identify the offending key without exposing unrelated configuration.

Keep planning and persistence in the existing initialization flow. A conflict must leave the manifest, settings on disk and in memory, client config, instruction files, and exclusions unchanged. A dry-run reports all affected keys without writes. Instruction preference still comes from the shared context; alias reconciliation always runs regardless of `--no-instructions`. Existing `worklog-legacy` and other server names remain unchanged.

### Verification and fresh-session entry points

Extend `tests/WorkLog.Tests/Integration` using `Testing/TestWorkspace.cs` and the existing `RepositoryInitializer` injection of a test home and executable. Build fixtures for lowercase-only, canonical-only, both, backslash aliases, unrelated projects, distinct worktrees, conflicting alias ownership/shape, dry-run, repeat init, and opt-out. Platform-specific assertions must explicitly exercise the Windows comparer; non-Windows exact-case behavior needs a focused test if the helper accepts a comparer for testing.

Extend `tools/WorkLog.Smoke/InitializationAcceptanceChecks.cs` and/or `ClientRegistrationChecks.cs` with isolated `.claude.json` entries containing sentinel project fields and servers. Invoke the packaged init, then native `claude mcp get worklog` and the existing connection probe from both stored case spellings of the same disposable Windows checkout. Record Git/Claude/WorkLog versions, exact cwd, discovered command/args, and connection results; no authenticated prompts. The native checks must demonstrate effective registration, not merely edited JSON. If unavailable, report the prerequisite and keep required acceptance pending. Do not change the real client profile.

From the root, use the established checks:

```powershell
dotnet restore --locked-mode
dotnet test tests/WorkLog.Tests/WorkLog.Tests.csproj -c Release --filter FullyQualifiedName~Integration
powershell -NoProfile -File scripts/package.ps1
dotnet run --project tools/WorkLog.Smoke -c Release
```

Run the full test suite before completion; package smoke requires installed Claude and Codex CLIs. Consult current official docs before choosing native command options. Update `docs/installation.md` and `docs/mcp-validation.md` with tested behavior and remaining unseen-alias limitations.

## Risks / Trade-offs

- Multiple alias objects remain -> keep their owned registration consistent; do not claim duplicate project entries were removed.
- An alias contains an unrelated server -> fail the entire preflight; preserve the user's conflict for explicit reconciliation.
- Client lookup differs by version -> native checks record versions and launch paths; no universal inheritance claim.
- Shared smoke files may also be touched by F8/F6 -> keep this fixture focused and rebase normally; no runtime dependency on those changes.

## Migration Plan

No journal migration. Users rerun `worklog init --clients claude` with their saved instruction preference; aliases already present are reconciled. Restart Claude afterward. Rollback restores backed-up private client config and the previous executable; never rewrite shared repository files. Completion requires regression tests and native packaged acceptance, not creation of these artifacts.
