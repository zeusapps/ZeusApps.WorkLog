# Design

## Context

See proposal.md and [the handoff](../../../../docs/handoffs/2026-10-21-claude-integration-flaws.md). Baseline is v0.1.1 / `fa48ada`. `UpdateInstaller` stages and launches a copied helper, which waits for its original parent to exit. `UpdateHelper.ApplyAsync` acquires the installation lease, calls `EnsureIdle`, verifies metadata/package, and invokes `ApplicationReplacement`. Replacement probes each existing target with exclusive read/write access before copying backups and mutating files. Both the process scan and these probes can reject a live installed `--status` process. `UpdateCommand` and `UpdateInstaller` currently invite checking status immediately. `UpdateAcceptanceChecks` reads JSON in-process and therefore misses that race.

## Goals / Non-Goals

**Goals:** Let finite status-reader overlap clear before mutation, within a bounded readiness deadline, while persistent use remains a recoverable failure. Make the race reproducible in disposable packaged Windows tests.

**Non-Goals:** Running or replacing an executable while it remains in use, classifying processes by their command line, exempting status PIDs, killing clients, an unbounded helper, or retrying arbitrary failures after replacement starts. No real installation or journal is a test target.

## Decisions

### Wait for actual readiness instead of ignoring the reader

Keep the existing parent-exit timeout and exclusive update lease. After validating and verifying the staged payload, allow at most 30 seconds of readiness waiting, with cancellation and approximately 100 ms polling, for target-process and target-file sharing conflicts. Use one monotonic deadline across all readiness retries; acquiring a competing update lease does not become an indefinite wait. Persistent lease conflicts may retain current immediate refusal. Status calls remain local and offline and must not acquire the exclusive updater lease.

The helper must observe both an idle process scan and successful exclusive-file preflight before entering backup/mutation. Consolidate the preflight with `ApplicationReplacement` so a sharing conflict arising after the initial process scan but before mutation also consumes the same deadline. Do not leave an unhandled one-shot probe later in the call path. Add testable timing/readiness seams only where needed; avoid production switches that let callers disable checks.

Retry only known transient busy/sharing or lock violations, not every IOException. Malformed plans, checksum/version/hash changes, reparse paths, access denied, missing required files, and other permanent failures retain actionable failure behavior. Preserve target validation and original manifest/executable-hash rechecks after waiting, immediately before applying. Never exclude a process just because it might be a status reader: ignoring the scan would still leave a loaded executable lock. Waiting briefly for all target users is simpler and retains the safety contract.

### Keep the mutation boundary explicit

All readiness retries happen before the first application-file mutation. Once backup/replacement begins, keep existing rollback and final version verification; never restart the whole transaction on an arbitrary I/O error. Hold the exclusive update lease throughout readiness and application. New target users that appear after preflight may still cause a safe failure/rollback; do not promise atomicity against an uncooperative process launch. Normal finitely spaced status reads that finish within the deadline must not alone cause update failure.

On timeout, write a failed outcome explaining the installation is still in use, nothing was replaced before readiness, no processes were stopped, and how to retry after closing clients. Repeated polling that never permits an idle window is allowed to time out safely. Cancellation before mutation also leaves installed files untouched and produces an actionable outcome through the current helper error path.

### Correct user guidance in both staged messages

Update the CLI staged line and persisted staged outcome together. State: staging is not completion; affected MCP clients must be stopped before starting the update; allow the helper to finish, then run `worklog update --status` with the same data directory; restart clients only after an installed outcome. Discourage tight polling and do not promise that the helper waits indefinitely for clients. Update `docs/installation.md` to match. A message-only change would reduce the trigger but leave the timing defect, so it is insufficient acceptance for this change.

### Verification and fresh-session entry points

Add focused updater tests next to `tests/WorkLog.Tests/Updates/ReplacementTests.cs` using controlled probes/time to establish transient-busy then ready, sharing conflict after process scan, deadline exhaustion, cancellation, permanent-error non-retry, no writes before readiness, and unchanged rollback/version checks. Deterministic coordination must establish overlap; an arbitrary sleep is not evidence of the race being exercised.

Extend `tools/WorkLog.Smoke/UpdateAcceptanceChecks.cs` and its `Program.cs` fixture modes. Retain the existing staged-child/real-helper path and disposable installation, config, journal, unowned-file sentinels. Launch the **installed** executable with `update --status --data-dir <fixture data>` while the helper is in readiness; synchronize this using an internal test seam or controlled preflight blocker, and prove at least one status process overlaps that phase. Release the blocker/readers within the deadline and require an installed outcome, correct version, and unchanged data. Read outcome JSON externally for orchestration as needed, but do not substitute that for the real executable overlap. Record process timings and status outputs. Use a second case holding a target file lock past the deadline to prove safe failure. Preserve active MCP refusal without killing the fixture server to make the assertion pass; close only owned fixture processes during cleanup.

```powershell
dotnet restore --locked-mode
dotnet test tests/WorkLog.Tests/WorkLog.Tests.csproj -c Release --filter FullyQualifiedName~Updates
powershell -NoProfile -File scripts/package.ps1
dotnet run --project tools/WorkLog.Smoke -c Release -- --updates-only artifacts/releases/worklog-0.1.1-win-x64/worklog.exe
```

Substitute the version from `Directory.Build.props` if it changes; do not bump a version solely for these fixtures. Run the full suite before completion. Record Windows/application versions and logs in ignored artifacts; document actual verification in `docs/mcp-validation.md`. Packaged tests require Windows and .NET 10; the updater-only smoke does not require native Claude/Codex CLIs or authenticated prompts.

## Risks / Trade-offs

- Brief users get a grace period -> persistent use fails within the deadline, and initial explicit-update refusal can remain immediate.
- TOCTOU between probes and writes -> retain exclusive probes, the update lease, and rollback; no process exemption.
- Readiness errors differ from permanent failures -> test explicit classifications and do not catch/retry generic I/O failures.
- Packaging smoke time increases -> isolate updater-only invocation and use injected time in unit tests, real bounded time only in acceptance.

## Migration Plan

No data migration or registration change. Future upgrades use the new helper behavior only when launched by a version containing the fix; v0.1.1's copied helper is still old code. For upgrading from v0.1.1, stop affected clients, avoid installed status polling while its helper runs, and check afterward (or use the documented manual ZIP upgrade). Retain existing backups/outcomes for recovery. Publication, actual local installation, and release version changes are separate operational steps.
