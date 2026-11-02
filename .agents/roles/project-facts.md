# Project facts for specialists

Shared by the developer, QA, and code-analyzer roles. Role files hold only role-specific rules and link here.

## Commands

Run from the repository root. CI runs the same sequence in `.github/workflows/validate.yml`.

- Dashboard dependencies: `npm ci --prefix dashboard --no-audit --no-fund`
- Dashboard unit tests: `npm test --prefix dashboard`
- Restore: `dotnet restore --locked-mode`
- Build: `dotnet build -c Release --no-restore` (also builds the dashboard assets embedded in `worklog`)
- Test: `dotnet test -c Release --no-build --no-restore`
- Browser acceptance against the Release build: `npm run test:e2e --prefix dashboard`. It needs the Release build above. `dashboard/e2e/server.mjs` creates an isolated fixture under `artifacts/dashboard-e2e/` and serves on port 47942.
- Package: `./scripts/package.ps1` (produces `artifacts/releases/worklog-<version>-win-x64/`).
- Browser acceptance against the packaged build: set `WORKLOG_DASHBOARD_EXECUTABLE` to the packaged `worklog.exe` and `WORKLOG_DASHBOARD_NO_NODE=true`, then run `npm run test:e2e --prefix dashboard`.
- Packaged MCP and update smoke: `dotnet run --project tools/WorkLog.Smoke -c Release --no-build -- --updates-only <packaged worklog.exe>`; mixed-version checks: `./scripts/package-update-fixtures.ps1`.
- Formatting: CI does not enforce `dotnet format`, and many existing files are not formatter-clean. Follow `.editorconfig` and the surrounding style in new and changed lines.
  - Developer: never reformat whole existing files. Run `dotnet format ZeusApps.WorkLog.slnx --no-restore --include <file>` only on files you created.
  - Reviewers: `dotnet format ZeusApps.WorkLog.slnx --no-restore --verify-no-changes --include <files>` is verify-only. Report only violations on lines the target added or changed. Treat other output as pre-existing, and never run the mutating form.
- Review identity: `./scripts/review-identity.ps1 -Baseline <commit> -Paths <scoped path> [<scoped path> ...]` (space-separated paths, relative to the current directory; run from the repository root). It only reads Git state and prints a manifest, hashes, and a summary line.

## Layout

- `src/WorkLog`: the `worklog` executable.
  - `Cli`: commands.
  - `Configuration`: private settings and repository contexts.
  - `Dashboard`: local HTTP host, catalog, documents, timeline, review markers.
  - `Infrastructure`, `Integration`: `init`, managed instructions, per-client adapters under `Clients`.
  - `Journaling`, `Persistence`: SQLite journal, schema 2.
  - `Mcp`: stdio tools `log_work` and `query_work`.
  - `Reporting`, `Repositories`, `Updates`.
- `dashboard/`: Angular client (`src/`), Playwright acceptance (`e2e/`), pinned toolchain (`.nvmrc`, `package-lock.json`).
- `tests/WorkLog.Tests`: xUnit tests. `tools/WorkLog.Smoke`: packaged acceptance and dashboard fixtures. `tools/WorkLog.LauncherProbe`.
- `openspec/specs/`: capability specs. `openspec/changes/`: active and archived changes.
- `docs/architecture.md` is the architecture reference; judge drift against it rather than restating it. Validation evidence lives in `docs/dashboard-validation.md` and `docs/mcp-validation.md`.

## Claude Code and Codex parity

Parity applies to integration and MCP behavior: `worklog init`, managed instruction text (shared in `src/WorkLog/Integration/AgentInstructions.cs`), per-client adapters (`src/WorkLog/Integration/Clients`), MCP tool contracts, and launcher behavior. For those changes, reviewers state which client(s) each check covers and flag duplicated per-client policy and unexplained parity gaps. The dashboard is client-neutral. The policy is in [AGENTS.md](../../AGENTS.md).

## Safety

- Never use the user's real data directory (`%LOCALAPPDATA%\ZeusApps.WorkLog`, or the one `WORKLOG_DATA_DIR` points to). Always pass `--data-dir` (or set `WORKLOG_DATA_DIR`) to a disposable directory under `artifacts/` or the system temp directory.
- Never bind to, connect to, or probe the user's dashboard port 47831. Launch dashboards with `--port <free port> --no-open`. The Playwright fixture uses 47942.
- Never stop, restart, or replace the user's running dashboard, MCP servers, or agent sessions.
- Never run `worklog init`, `worklog update`, or the installer against the user's repositories, client settings, or installation. Use disposable repository copies with `--data-dir`. Run `init` only with `--dry-run`: it locates Claude's `~/.claude.json` through the Windows profile folder, and overriding `USERPROFILE` or `HOME` does not redirect it. A non-dry-run init would modify the user's real Claude configuration even inside a disposable repository.
- Never edit personal client configuration (`~/.claude`, `~/.codex`). In this repository, never edit the WorkLog-owned blocks in `.codex/config.toml` (`worklog:begin`/`worklog:end`, `worklog:server:begin`/`worklog:server:end`).
- Never insert, edit, or delete review comments in this repository's own OpenSpec documents. Exercise review comments only in fixture sources.
- `tools/WorkLog.Smoke` with `--live-agents` uses the user's authenticated agent accounts and sends prompts to their services. Run it only when the assignment includes a planned live-agent evaluation (for example, judging how real clients follow managed instructions or tool-result prompts). The assignment names the clients, the number of sessions, and the per-session budget. Run only in disposable repositories and data directories with the smoke tool's isolation flags. Never use the user's real repositories, data directory, installation, or client settings, and never put secrets or personal data in prompts. Record client versions, models, and redacted prompts and outputs as evidence. Without such an assignment, do not run live agents. The default isolated smoke run is always allowed.
- Before starting a long-running probe (a dashboard or a server), arrange for it to stop by itself: prefer harness-managed lifetimes (the Playwright webServer, a script that stops the process it started in the same command, or a time limit). Never leave a probe running. If a probe cannot be stopped, report its port, process ID, and disposable data directory in the return block's limitations. Never stop processes you did not start.
- A check that requires the live installation (the user's real data directory, running dashboard or MCP servers, the installation, or client settings) is recorded as not performed, not run against it.
- Never publish releases, push, or create tags.
- Your inspection must not change the main session's OpenSpec selection.
- Fetch current library or API documentation with the `ctx7` CLI when you depend on it (`npx ctx7@latest library <name> "<question>"`, then `npx ctx7@latest docs <id> "<question>"`).

## Committable content hygiene

Everything you write that Git would commit (OpenSpec artifacts, reports, evidence, screenshots, handoffs, findings, docs, and other Markdown or text files that are not ignored) must contain no secrets, no information identifying the user or their machine, and no test residue.

- Secrets: API keys, tokens, passwords, cookies (including dashboard session values and `dashboard.secret` contents), private keys, signing material, webhook URLs or connection strings with embedded credentials, and authorization headers.
- Machine and personal information: account or user names, home-directory absolute paths (for example `C:\Users\<name>\...`), host or computer names, email addresses, IP or MAC addresses, network names, device or hardware identifiers and serials, environment-variable dumps, verbatim personal configuration (`~/.claude`, `~/.codex`), and real journal content.
- Search patterns for reviewers: the current account name, computer name, home-directory path, email addresses, IP addresses, and the secret patterns `sk-`, `ghp_`, `github_pat_`, `xox`, `AKIA`, `Bearer `, `BEGIN ... PRIVATE KEY`, `password=`, `token=`, `#connect=`.
- Test residue patterns: lines starting with `<!-- REVIEW` in documents not under review, `test comment`, `foo bar`, `lorem`, `TODO remove`, and fixture text copied into real documents. Examples inside fenced code are not residue.
- Use repository-relative paths and placeholders such as `%USERPROFILE%`, `%LOCALAPPDATA%`, `~`, `<user>`, `<host>`, or `<redacted>`. Redact command output, logs, and transcripts before quoting them, and crop or blur screenshots that show such details.
- Public project identities already in the repository, Git commit attribution, and opaque client session IDs used for ledger ownership are allowed.
- Never repeat a found secret or personal value in a report or finding; cite the file, line, and category only.
