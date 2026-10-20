# Code organization

WorkLog remains one executable project. Folders and namespaces group code by responsibility; the MCP protocol, CLI commands, JSON configuration, and journal schema are the external contracts.

| Area | Responsibility |
| --- | --- |
| `Cli` | Define System.CommandLine commands and typed options, dispatch actions, report application version |
| `Mcp` | Host the stdio server and translate tool calls/errors |
| `Journaling` | Validate logging requests, resolve metadata, coordinate persistence |
| `Persistence` | SQLite connections, schema upgrades/backups, transactional entry storage |
| `Reporting` | Validate periods/options, bound pages, encode continuation, build daily groups and stable work identities |
| `Configuration` | Load/save private settings and define configuration models |
| `Repositories` | Run Git, derive stable repository identities, register checkouts |
| `Integration` | Plan/apply private initialization, track ownership, render shared instructions |
| `Integration/Clients` | Preserve and update Codex TOML and Claude JSON/instruction files |
| `Updates` | Discover stable releases, coordinate cached checks, stage verified packages, replace idle installations through a helper, and record recovery/outcomes |
| `Infrastructure` | Shared JSON serialization, atomic file replacement, content hashing |

`Program` handles process exit and error reporting. `CliApplication` registers `InitCommand` and `ServeCommand`; System.CommandLine owns parsing, validation, help, and invocation. Each command composes its application operation, and the serve action forwards cancellation to the host. MCP tools delegate to `Journal`, which prepares entries through `EntryFactory`, persists through `SqliteEntryStore`, and builds queries through the reporting components. SQL stays in Persistence; business validation and daily grouping do not depend on SQLite commands.

`UpdateCommand` adds explicit check/install/status operations. `UpdateDiscovery` coordinates release checks through a shared local cache and runs independently of the MCP host's journal calls. `JournalTools` attaches an optional typed `updateHint` from locally cached metadata; it omits the hint if it would overflow a report's byte budget. Hints are not stored observations and do not affect retry identities, report schema version, or cursor matching. The tool schemas and JSON fallback describe the same response.

The updater accepts the fixed public repository's Windows ZIP/checksum. `ReleasePackage` verifies the archive and staged executable. `UpdateInstaller` records a target-bound plan and starts a copy of the installed executable in a staging helper directory. `UpdateHelper` waits for the original process to exit, acquires the installation lease, refuses busy or changed targets, and uses `ApplicationReplacement` to replace only owned package files with backups and post-install verification. It records the eventual result for `update --status`. Packaged serve/init commands honor an existing installation lease, preventing new journal processes from starting during replacement. The helper never runs journal migrations or terminates existing clients.

Tools declare output schemas and return native MCP structured content plus backward-compatible JSON text. Tool annotations distinguish additive local logging from read-only local querying. See [MCP validation](mcp-validation.md) for the documentation review and protocol checks.

`RepositoryInitializer` coordinates initialization. Client adapters add proposed changes to `InitializationPlan`; all adapters validate before the plan writes anything. The plan owns backup creation, concurrent-edit checks, atomic replacement, and partial-failure reporting. Managed instruction blocks and templates are shared between clients.

Tests mirror the production areas, with isolated filesystem/Git setup in `Testing/TestWorkspace`. The smoke executable has separate client-registration, journal-acceptance, and opt-in live-agent checks; its Program only sequences those checks. No additional application projects, generic repository frameworks, or pass-through interfaces are needed for these boundaries.

The smoke executable also has `--updates-only PATH_TO_WORKLOG_EXE` for CI without installed agent CLIs. It creates an isolated prior-install fixture, stages a controlled package in a child process, verifies real helper handoff and file replacement, checks journal/configuration preservation, and proves a running packaged server blocks replacement. Update unit tests inject HTTP responses and post-install verification failures so rate limits, unsafe packages, and rollback do not require live GitHub access.
