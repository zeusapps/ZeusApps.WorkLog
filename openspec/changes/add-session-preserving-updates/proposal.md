# Proposal

## Why

WorkLog's in-place Windows updater requires closing the Claude/Codex sessions that use it. Compatible releases should instead activate for new processes while existing conversations continue on their original server version, as described in the [handoff](../../../docs/handoffs/2026-10-22-session-preserving-updates.md).

## What Changes

- Introduce a stable launcher and immutable versioned payloads. Explicit updates verify a new payload and atomically select it for subsequent launches without replacing files used by existing servers.
- Distinguish the running version from the selected version in status and update discovery; suppress offers already activated and prevent stale servers/updaters from selecting an older release.
- Declare launcher and shared-data compatibility in release metadata. Keep journal schema 2, preserve unknown settings fields, and coordinate settings writes so supported old/new processes can share local data safely.
- Retain previous and unused payloads initially; automatic cleanup is deferred. Provide explicit application-selection rollback within the supported compatibility contract without restoring journal data.
- **BREAKING:** Change the Windows package layout and updater contract. The sole existing user accepts a one-time manual stop/reinstall/init/restart migration; pre-launcher releases receive no seamless upgrade guarantee. Launcher replacement and incompatible data changes remain maintenance operations.
- Supersede the planned F9 legacy status-reader race implementation with versioned activation. Do not implement that separate fix first or mark its unchecked tasks complete. Record the supersession here; disposition of the old change is a separate workflow action.
- Preserve explicit installation, package verification, private registration ownership, instruction opt-outs, repository identities, historical observations, and Windows release gates. Keep unrelated F6-F8 work separate.

## Capabilities

### New Capabilities

None; extend the existing distribution and integration contracts.

### Modified Capabilities

- `application-distribution`: versioned Windows packages, launcher forwarding, verified session-preserving activation, compatibility gates, selection-aware hints/status, rollback, and maintenance boundaries.
- `local-agent-integration`: register the stable launcher and describe activation versus client adoption in managed instructions.
- `work-journal`: support concurrent processes from explicitly compatible releases and preserve shared configuration across writers without changing journal semantics or schema.

## Impact

Add a small launcher project and installation resolver; adapt `Program`, `Updates/*`, `Integration/ServerRegistration`, managed instructions, configuration persistence and initialization, package scripts, solution/build inputs, Windows CI and packaged smoke fixtures. MCP update-hint schemas/guidance may gain bounded additive version context; report schema version, observation identity, tool names, ticket semantics, and stored journal schema remain unchanged.

No automatic installation, live MCP connection transfer, background service, Bell Tower dependency, cross-platform distribution, automatic payload deletion, production-client reconfiguration, actual local migration, version bump, or release publication is part of this planning change. Implementation and disposable validation will follow through apply; operational rollout remains explicit.
