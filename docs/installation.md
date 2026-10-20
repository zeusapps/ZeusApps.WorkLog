# Windows installation and upgrades

1. Download `worklog-VERSION-win-x64.zip` and its `.sha256` file from the project's GitHub Releases page.
2. Compare `Get-FileHash .\worklog-VERSION-win-x64.zip -Algorithm SHA256` with the checksum file.
3. Extract into a stable directory such as `%LOCALAPPDATA%\Programs\WorkLog` and add that directory to your user PATH, or invoke worklog.exe by its full path. Keep data outside that installation directory.
4. Run `worklog --version`, then `worklog init --repository D:\Code\MyProject --dry-run` to review setup.
5. Run the same init command without `--dry-run`. Restart Codex/Claude Code and use their normal trust prompts. Init never grants trust or changes permission policies.

Windows x64 is the supported release target. The packaged executable includes .NET and SQLite native dependencies. Building from source requires the SDK selected by global.json, Git, and NuGet access.

## Private client integration

`worklog init` initializes the current Git repository; `--repository` selects another. `--clients both|codex|claude` defaults to both. Both client CLIs must be installed when selecting both; minimum tested versions are Codex 0.162.1 and Claude Code 2.1.289.

- Claude Code: local-scope MCP registration under the project path in your private `~/.claude.json`, plus a managed block in `CLAUDE.local.md`. Explicit imports preserve existing ancestor AGENTS.md instructions.
- Codex: a private `.codex/config.toml` registration and additive `developer_instructions`, preserving existing user/project instructions. This does not replace model base instructions.
- `.worklog/integration.json` records integration ownership/version. Generated project files are excluded via local Git info/exclude, including worktree-aware paths. Team CLAUDE.md, AGENTS.md, .mcp.json, .gitignore, and OpenSpec skills/configuration are not edited.

An already tracked private target, malformed settings, or an unowned worklog server registration is a conflict: init reports it without overwriting it. Do not untrack team settings automatically; resolve the conflict or select the other client. Init backs up changed existing content under the data directory's integration-backups folder. Partial failures list applied steps; fix the problem and rerun init. Keep backups private because client settings can contain personal information.

Init can be rerun after an application upgrade to update managed instruction blocks. Preserve the ownership manifest. Journal data is shared across initialized repositories on this machine only. Repositories without origin receive a persisted local identity; explicitly set identity in private config when independent checkouts should refer to the same repository. Use distinct OpenSpec change names for new work rather than recycling completed names.

## Upgrade and restore

Stop agent sessions using WorkLog before replacing the executable. Extract the new release to the same stable installation location; registered MCP paths then remain valid. Run `worklog --version`, rerun init for repositories needing updated instructions, and restart clients. Do not replace the data directory with release contents.

Journal upgrades run transactionally. Before upgrading an existing schema, WorkLog creates a consistent `journal.db.vN.TIMESTAMP.bak` in the data directory. A binary encountering a newer unsupported schema refuses to migrate it.

To roll back a schema upgrade: stop **all** WorkLog processes; keep a separate copy of the current journal and any `-wal`/`-shm` files for recovery; restore the chosen consistent `.bak` as `journal.db` in a clean data directory with no stale WAL/SHM files; copy the private config there; run the older executable with `--data-dir` pointing to that restored directory. Entries recorded after the backup are absent from that restored snapshot. Reinitialize client registrations if the data-directory path changes. Never copy only a live database file while its WAL may contain committed work.

## Troubleshooting

Run `worklog --help` for command syntax. Diagnostics go to stderr so MCP stdout remains valid protocol traffic. Verify client versions, executable path, repository trust, and private config syntax if tools do not appear. Use an explicit `worklog serve --repository D:\Code\MyProject --data-dir D:\Private\WorkLog` command in a stdio MCP client to isolate startup issues. A server without repository context requires explicit repository identities in logging calls.
