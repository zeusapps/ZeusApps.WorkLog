# Proposal

## Why

WorkLog has been tested locally and already has packaging and release workflows, but has no GitHub remote or published distribution. Users also need to learn when a newer MCP server is available and upgrade it without disturbing their private journal or client registrations.

## What Changes

- Prepare and publish the source to the public personal-account repository `zeusapps/ZeusApps.WorkLog`, use `main` consistently with CI, and publish the first versioned Windows release through the existing validation workflow.
- Add background checks for newer stable releases from that fixed public repository, shared daily caching, bounded network requests, and a configuration switch to disable automatic checks.
- Add optional, bounded update hints to successful MCP tool responses and private agent instructions to surface a notice once per version per assistant session. Journal operations remain available offline.
- Add `worklog update --check`, an explicit `worklog update` command that stages and verifies the Windows package, and `worklog update --status` for the eventual outcome. Replace an idle installation through a helper while preserving data, settings, and executable paths, and report staged versus completed updates accurately.
- Document installation, update/restart behavior, recovery, and the operational release procedure.

## Capabilities

### New Capabilities

None; release discovery and upgrades extend the existing distribution capability.

### Modified Capabilities

- `application-distribution`: Fixed public release source, non-blocking release discovery, MCP update hints, and explicit verified Windows upgrades.
- `local-agent-integration`: Private instructions for surfacing update hints without treating a notice as permission to install an update.

## Impact

- CLI composition, configuration, MCP response models/output schemas, private agent instructions, and new release-check/update components within the existing executable project.
- Existing Windows packaging, GitHub workflows, README, installation documentation, and targeted CLI/MCP/updater tests and packaged smoke checks.
- Public HTTPS requests to GitHub release metadata and assets; no journal content, repository context, ticket data, or credentials are sent by update checks. No GitHub credentials are required for installed copies.
- Additive optional response metadata; existing tool arguments, journal schema, reporting pagination, and saved repository identities remain compatible.
- Repository creation, source push, tagging, and observing the published release are implementation-phase operational tasks. This proposal does not perform them.
