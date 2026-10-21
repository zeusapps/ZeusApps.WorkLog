# Proposal

## Why

F6 suggests a Claude session in a fresh worktree can lack the WorkLog server, but it has not been reproduced. Existing smoke checks run from the initialized checkout, so they cannot establish whether another worktree discovers the registration or whether a discovered server starts successfully.

## What Changes

- Add isolated acceptance coverage that initializes a main checkout, creates a fresh worktree, and checks discovery and connection from the worktree before and after explicit worktree initialization.
- Separate client discovery, executable startup, MCP handshake/tool discovery, and repository-context failures in captured results.
- Use the existing packaged smoke harness and temporary client homes/data, without authenticated agent prompts or modifications to real client configuration.
- Record client/application versions and document the verified worktree setup procedure and limitations.
- If a production defect is reproduced, describe the failing case and propose a separately scoped remedy; this verification change does not preselect a global server, daemon, hook, or registration redesign.

## Capabilities

### New Capabilities

None. This change adds tests and documentation only, with no new runtime behavior.

### Modified Capabilities

None. The change declares `skip_specs: true`; runtime requirement deltas would be speculative before F6 is reproduced.

## Impact

Changes are limited to tools/WorkLog.Smoke, relevant regression-test fixtures, and installation/validation documentation. SmokeProcessRunner currently fixes its working directory to the original repository and must support explicit worktree selection. ClientRegistrationChecks currently verifies Codex discovery/instructions and a Claude connection only in that original checkout. The change may be implemented independently; when run after `fix-repository-integration-context`, it must also verify the resulting shared-context behavior without taking ownership of those fixes.
