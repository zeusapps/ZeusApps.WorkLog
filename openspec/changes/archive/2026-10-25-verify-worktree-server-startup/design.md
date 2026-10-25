# Design

## Context

See proposal.md for motivation. ClientRegistrationChecks exercises Codex registration/instruction loading and Claude's MCP health check only from SmokeWorkspace.Repository. SmokeProcessRunner hard-codes that same working directory. RepositoryInitializerTests covers a worktree's local exclusions but does not exercise a real client starting from a fresh worktree. These checks cannot prove or disprove F6.

Current official Claude documentation describes project-scoped registration and several worktree inheritance behaviors, but it does not establish this application's exact local-scope registration in every launch mode. The investigation must capture actual results for recorded client versions, rather than infer a universal guarantee from one healthy session.

## Goals / Non-Goals

**Goals:** Produce a repeatable isolated check that determines whether the server is undiscovered, cannot start, or connects but has incorrect repository context. Establish a verified setup procedure for supported terminal clients.

**Non-Goals:** Production registration changes, global/user-scope installation, a persistent daemon, startup hooks, modifying real client trust/configuration, authenticated agent prompts, or a claim that Desktop and every future client version behave identically. A reproduced production defect becomes a separately scoped follow-up with its evidence.

## Decisions

### 1. Use the packaged smoke harness with explicit working directories

Extend SmokeProcessRunner with an optional working-directory argument while retaining the current repository as its default. Add a focused worktree-check component called by the existing smoke orchestration, using the packaged executable, temporary homes, explicit data directory, bounded process execution, and per-step logs. Keep existing registration and journal checks intact.

Create a disposable Git repository with an initial commit and an external sibling worktree through Git. Initialize the main checkout first; the worktree's initial probe must run before WorkLog init, copying generated files, or manual server registration in that worktree. Record WorkLog, Git, Codex, and Claude versions and exact working directories. Run Claude with the isolated CLAUDE_CONFIG_DIR and Codex with isolated CODEX_HOME, using the existing test-only trust override as needed.

A direct MCP SDK connection alone was rejected as proof of client discovery: it can show that an executable works while bypassing the registration path under investigation.

### 2. Distinguish observation from acceptance

For both terminal clients, record this matrix:

| Stage | Probe | Interpretation |
| --- | --- | --- |
| Main checkout initialized | Client discovers WorkLog and connects | Baseline must pass before interpreting worktree results |
| Fresh external worktree, no worktree init | Client discovery and connection using inherited configuration only | Record observed inheritance or its absence; missing inheritance is not automatically an application regression |
| Same worktree after explicit init for selected client | Repeat discovery and connection | Supported explicit setup must pass; compare with the initial result |
| Main checkout after worktree init | Repeat baseline discovery | Worktree setup must not remove the original registration |

Use client-native discovery/health commands for the first two layers. Where a client exposes registration without health, separately start the exact discovered command/arguments through the existing MCP acceptance machinery to verify initialize, tools/list, and availability of log_work/query_work. Label that separately; do not misreport a direct protocol connection as a client health result. Record resolved repository argument and data-directory selection. When shared-context behavior from the F1-F5 change is available, assert worktree/main identity and ticket context using that behavior; do not fix F3 here.

An absent server in the pre-init probe is a finding, not a reason to abort before testing explicit init. A broken main baseline, failed explicit setup, or failed post-setup baseline is an acceptance failure. Do not hard-code an unsupported expectation that all clients inherit private registrations.

### 3. Keep reproducible evidence local and documentation factual

Write a bounded result summary under the smoke artifact directory with the tested versions, stages, client exit codes, discovery state, connection state, and links to logs. Classify failures by configuration discovery, process launch, MCP initialization/tool discovery, or resolved context. Never collect normal user client settings, credentials, or authenticated prompt output.

Document verified behavior in installation/troubleshooting and the validation guide, including whether fresh worktrees need explicit init, client restart/trust requirements, and the distinction between no registration and a failing registered server. Note that a stdio server is launched by its client; this does not require inventing a machine-wide background service.

This change declares skip_specs because verification and documentation do not change runtime contracts. If evidence justifies a runtime remedy, report the exact reproducer and affected versions and request a follow-up change rather than slipping production edits into this work.

### 4. Bound launch-mode coverage

The mandatory automated case is a manually created external Git worktree launched through the terminal clients, which directly challenges project-path scope. Also inspect the same worktree after explicit init. Claude's managed --worktree launch and Desktop behavior may be documented as unverified unless they can be exercised without authenticated prompts or real-profile changes. They are not completion gates for this narrowly scoped change, and results from the external-worktree case must not be generalized to them.

References from the exploration: [Claude worktrees](https://code.claude.com/docs/en/worktrees) and [MCP troubleshooting](https://code.claude.com/docs/en/mcp-quickstart). Recheck client command semantics against current official docs during implementation before choosing probes.

## Risks / Trade-offs

- Client versions can alter inheritance behavior -> record versions with results and scope documentation to the tested matrix.
- A missing CLI can make a probe impossible -> fail or explicitly report the missing prerequisite; do not mark it passed or complete until the required supported-client checks have run.
- An uninitialized worktree might legitimately lack a local registration -> record the result and test the explicit-init path rather than requiring inheritance without a contract.
- Test homes or directories may leak into normal settings -> pass isolated environment and paths on every invocation; keep artifacts under ignored smoke directories.
- A runtime defect may remain after this verification is complete -> clearly distinguish completed investigation from unresolved F6, and create a separately authorized remedy proposal when needed.

## Migration Plan

No runtime, configuration, or journal migration is planned. Add the isolated checks, run the required client matrix against the packaged executable, and publish the verified procedure in repository documentation. Reverting the harness/docs has no effect on user installations. Retain failed-case logs until the finding is resolved or transferred to its follow-up.
