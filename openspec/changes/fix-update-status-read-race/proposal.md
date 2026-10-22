# Proposal

## Why

F9 reports that following the staged message and running the installed `worklog update --status` can make the helper fail: the status reader itself is a target-installation process. Both process enumeration and exclusive executable access can reject this transient overlap. See [the saved report](../../../docs/handoffs/2026-10-21-claude-integration-flaws.md).

## What Changes

- Give the helper a bounded readiness wait for transient target processes and sharing conflicts before replacement, covering both process checks and exclusive-file preflight.
- Keep replacement conditional on the installation actually becoming idle; persistent use, a competing updater, permanent errors, and deadline expiry remain safe failures without terminating processes.
- Correct both staged messages and installation guidance to stop affected clients before updating, wait for the helper, and inspect its eventual outcome rather than encourage tight polling.
- Add deterministic coordination tests and packaged Windows acceptance covering real installed status processes, persistent active-server refusal, and preservation/rollback behavior.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `application-distribution`: recoverable completion tolerates transient status readers through a bounded idle wait while retaining all replacement safeguards.

## Impact

Primary code: `src/WorkLog/Updates/UpdateHelper.cs`, `UpdateInstallation.cs`, `ApplicationReplacement.cs`, `UpdateInstaller.cs`, and `src/WorkLog/Cli/UpdateCommand.cs`. Extend updater tests and `tools/WorkLog.Smoke/UpdateAcceptanceChecks.cs`; update installation/validation docs. No journal migration, command-line process inspection, PID allowlist for status readers, background service, or automatic process termination. Independently applicable to v0.1.1.
