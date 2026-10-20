# Windows installation and upgrades

1. Download `worklog-VERSION-win-x64.zip` and its `.sha256` file from [zeusapps/ZeusApps.WorkLog Releases](https://github.com/zeusapps/ZeusApps.WorkLog/releases).
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

Packaged Windows installations support explicit verified updates:

```powershell
worklog update --check
# Close Codex/Claude sessions using this installation before continuing.
worklog update
worklog update --status
```

`--check` performs a fresh check and reports the installed/published versions without downloading a package. GitHub errors are reported as failures, rather than as up-to-date. `--status` reads the latest local result without contacting GitHub. The flags cannot be combined. All three modes accept `--data-dir PATH`; use the same directory throughout so status and cache refer to the intended journal.

Plain `update` only selects a strictly newer stable release with the official Windows ZIP and checksum. It verifies SHA-256, validates archive paths and packaged version/runtime, and stages the files outside the installation. It refuses source builds, unsupported layouts, overlapping installation/data directories, unwritable destinations, concurrent updates, and busy installations. It never stops MCP servers or agent sessions for you.

The command prints `staged` and exits so a separate helper can replace its executable. This is not a successful installation report. Run `update --status` after a moment to see `installed` or `failed`. A helper failure leaves a message and recovery location. After `installed`, run `worklog --version`, restart MCP clients, and rerun init in repositories requiring refreshed instructions. The executable's registered path stays the same, and journal data/private configuration are preserved.

Automatic checks run in the background using the public GitHub release feed, without credentials or project/journal data. They are coordinated across processes sharing a data directory, with at most one automatic attempt per 24 hours. Valid cached notices expire after seven days without successful validation. Successful tool responses can include `updateHint`; managed assistant instructions ask that each available version be mentioned once per session. This is best-effort assistant behavior, not a dedicated MCP client notification. Automatic check failures do not interrupt journal operations.

To disable automatic checking and notices, merge `"updates": { "enabled": false }` into private config.json, preserving its other settings, and restart clients. Explicit update commands remain usable. Cache, staging, backup files, and outcome receipts live in the data directory's `updates` subdirectory; they are separate from journal.db and config.json. Retain recovery files until the upgrade is verified. SHA-256 checks detect corruption; distribution authenticity also depends on HTTPS and control of the official GitHub repository.

For bootstrap installation from a source build, or manual recovery:

Stop agent sessions using WorkLog before replacing the executable. Extract the new release to the same stable installation location; registered MCP paths then remain valid. Run `worklog --version`, rerun init for repositories needing updated instructions, and restart clients. Do not replace the data directory with release contents.

If automatic application replacement fails, follow its status message. With all affected clients stopped, restore prior application files from the reported backup directory if necessary. Preserve any unowned installation files. Replacing application files does not roll back a journal schema migration performed on a later startup; use the database restore procedure below for that case.

Journal upgrades run transactionally. Before upgrading an existing schema, WorkLog creates a consistent `journal.db.vN.TIMESTAMP.bak` in the data directory. A binary encountering a newer unsupported schema refuses to migrate it.

To roll back a schema upgrade: stop **all** WorkLog processes; keep a separate copy of the current journal and any `-wal`/`-shm` files for recovery; restore the chosen consistent `.bak` as `journal.db` in a clean data directory with no stale WAL/SHM files; copy the private config there; run the older executable with `--data-dir` pointing to that restored directory. Entries recorded after the backup are absent from that restored snapshot. Reinitialize client registrations if the data-directory path changes. Never copy only a live database file while its WAL may contain committed work.

## Troubleshooting

Run `worklog --help` for command syntax. Diagnostics go to stderr so MCP stdout remains valid protocol traffic. Verify client versions, executable path, repository trust, and private config syntax if tools do not appear. Use an explicit `worklog serve --repository D:\Code\MyProject --data-dir D:\Private\WorkLog` command in a stdio MCP client to isolate startup issues. A server without repository context requires explicit repository identities in logging calls.
