# Claude integration follow-up handoff

Captured on 2026-10-21 from the user's Claude session report. Baseline: WorkLog v0.1.1, commit `fa48adae1fb72bb13baba9a731f39e1e93436cda`. This is historical evidence and planning context, not a claim that the pending fixes have shipped. No access to the reporting machine, its configuration, or its maintenance scripts was available during triage.

## Original report

The following register is preserved as supplied. D6, D7, D8, D12, task 5.3, `cutover.py`, and `maintenance.py` refer to the originating application's integration work; those materials are not included in this repository and are not implementation prerequisites.

| ID | Flaw | Location | Effect | Workaround here | Proposed fix | Priority |
|---|---|---|---|---|---|---|
| F1 | `init` never sets ticket provider or scope, and the managed instructions tell the model not to guess them. | `RepositoryRegistry.Get`, `AgentInstructions` | A branch ticket stays in `extractedTicketId`. With no change name, `log_work` rejects the entry. | D6 (`config.json`) | **Fixed in v0.1.1** (`init --ticket-provider --ticket-scope`) | High |
| F2 | The `ticket` filter of `query_work` reads only `tickets[]`. | `SqliteEntryStore.ReadPage` | Entries with only `extractedTicketId` drop out of ticket queries, with no warning. | D6 prevents such entries | **Fixed in v0.1.1** (a bare ID also matches `extractedTicketId`) | High |
| F3 | A new path gets a config with no provider or scope, and that config wins over the config of the same identity. | `RepositoryRegistry.Get`, `EntryFactory.ResolveTicketContext` | Worktree `init` loses ticket qualification. | Not observed: worktrees use the main server | **Fixed in v0.1.1** (`repositoryContexts` keyed by the git common directory) | Medium |
| F4 | Default `branchClassifications` has `bugs`, not `bugfix`. | `WorkLogConfiguration` | `bugfix/` branches become `unclassified`. | D6 (`config.json`) | **Fixed in v0.1.1** (`bugfix` default) | Low |
| F5 | `init` always writes the instruction block, with no opt-out. A rerun writes it again. | `ClaudeIntegration.Plan` | It duplicates user-level rules and conflicts on provider/scope. | D7 (remove after `init`) | **Fixed in v0.1.1** (`init --no-instructions`, stored as a preference) | Low |
| F6 | Registration is per project path. | `ServerRegistration` | A session in a new folder can have no server. | Not observed: worktrees use the main registration | `serve` finds the git root from its working folder; one user-scope registration | Optional |
| F7 | `init` writes the project key from `git rev-parse --show-toplevel` (`C:/Code/AnalyticsPlatform`). `~/.claude.json` has `C:/Code/analyticsplatform`. | `ClaudeIntegration.Plan` | Claude Code reads the key that matches the case of the start path. A session started in `C:\Code\AnalyticsPlatform` gets no worklog server when only the lowercase key has it. The ownership check also misses the old server. | `cutover.py --reapply-registration` registers `worklog` and `worklog-legacy` under both spellings (task 5.3) | Match an existing project key case-insensitively on Windows, or register under every case variant | Medium |
| F8 | `query_work` groups `days[]` by a work identity that prefers the change name over the ticket. | `WorkIdentity.For` | One ticket splits into two groups when some entries carry a change name and some do not, for example migrated entries and new entries. | D8: the report skills group by `tickets[].id` | Prefer the ticket in the work identity, with the change as a sub-key; or add `groupBy: ticket \| change` to `query_work` | Low |
| F9 | `worklog update --status` starts `worklog.exe` from the installation, and the update helper counts that process as "in use". | `UpdateHelper`, `UpdateCommand` | A user who polls `--status` during an update, as the `staged` message suggests, makes the update fail. The installation stays on the old version. | `maintenance.py` waits for the helper to exit before it reads `--status` (D12) | Exclude a status-only process from the in-use check, or say "wait, then check `--status`" in the `staged` message | Medium |

## Repository triage and ownership

| IDs | Repository evidence / qualification | Owning change |
|---|---|---|
| F1-F5 | Implemented and released in v0.1.1; retain regression behavior when applying follow-ups. | [Archived implementation](../../openspec/changes/archive/2026-10-21-fix-repository-integration-context/proposal.md) |
| F6 | Still an investigation. Healthy sessions do not establish inheritance in every launch mode. Global registration is only the reporter's proposed alternative. | [verify-worktree-server-startup](../../openspec/changes/verify-worktree-server-startup/proposal.md) |
| F7 | `src/WorkLog/Integration/Clients/ClaudeIntegration.cs` selects one exact-case JSON key and validates ownership there only. The client-side case-sensitive lookup is reported evidence, not independently reproduced here. | [fix-claude-project-path-aliases](../../openspec/changes/fix-claude-project-path-aliases/proposal.md) |
| F8 | `src/WorkLog/Reporting/WorkIdentity.cs` returns the change identity first. This is existing behavior, not a lost-entry bug. Optional ticket grouping preserves compatibility. | [add-ticket-report-grouping](../../openspec/changes/add-ticket-report-grouping/proposal.md) |
| F9 | `UpdateHelper.ApplyAsync` calls `EnsureIdle` without an allowed target PID; `ApplicationReplacement.ApplyAsync` also probes exclusive file access. The overlap is timing-dependent, not every status read. Existing smoke reads outcome JSON in-process, bypassing the reported executable race. | [fix-update-status-read-race](../../openspec/changes/fix-update-status-read-race/proposal.md) |

## Fresh-session instructions

1. Start in the repository root, inspect `git status`, and read this handoff plus the selected change's proposal, design, delta specs, and unchecked tasks. Use `$openspec-apply-change <name>` in Codex or the equivalent apply workflow. Planning completion does not mean implementation completion.
2. All three new changes are independently applicable to v0.1.1. Recommended order: F9, F7, then F8. None depends on implementing F6. Do not implement global registration as a side effect of F7.
3. Each design identifies source files, isolated fixtures, behavioral decisions, and runnable checks. Use temporary client homes and disposable installations; do not run tests against real Claude configuration, journal data, or the installed WorkLog binary. Current repository tooling requires Windows and the .NET 10 SDK; native client checks additionally require the relevant CLI.
4. Preserve F1-F5: shared repository contexts, qualified ticket rules, unresolved bare-ID filtering, bugfix classification, and persistent per-client instruction opt-out. No follow-up requires changing historical journal entries or its database schema.
5. Record actual work under the selected change. A pending required test means `in_progress`; never claim completion from checked tasks or archiving alone. Record versions and logs for native-client and packaged-executable checks.
6. Recheck current official Claude documentation when selecting native probes. Context7 was consulted on 2026-10-21: [MCP quickstart](https://code.claude.com/docs/en/mcp-quickstart) documents local scope under a project's entry in `~/.claude.json`, `CLAUDE_CONFIG_DIR`, and `claude mcp get/list`. It does **not** establish Windows case-variant lookup semantics; reproduce those with isolated settings and record the client version.

At capture time, only planning and source-level triage have been done for F7-F9. No implementation or publication is authorized by the existence of this handoff.
