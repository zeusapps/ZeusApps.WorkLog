# Tasks

## 1. Bounded readiness before replacement

- [ ] 1.1 Introduce a helper readiness coordinator with cancellation, one monotonic deadline of at most 30 seconds, and narrow transient-busy/sharing classifications; verify controlled-probe tests for busy-then-ready, deadline exhaustion, cancellation, and immediate permanent-error failure without broad IOException retry.
- [ ] 1.2 Integrate process-idle and exclusive-file preflight into the same bounded readiness path across `UpdateHelper` and `ApplicationReplacement`, retaining the update lease and post-wait original manifest/hash validation; verify a sharing conflict after an idle process scan can clear before mutation and no one-shot later probe bypasses retry handling.
- [ ] 1.3 Keep backups/mutation outside readiness retry and retain existing verification/rollback; verify no application writes before readiness, persistent users remain unmodified, late failures restore prior managed files, and no process is terminated or exempted based on assumed command-line role.

## 2. Guidance and packaged race acceptance

- [ ] 2.1 Correct CLI and persisted staged messages together while retaining local/offline status behavior; verify messages distinguish staging from installation, require clients stopped before update, allow helper completion before checking with the same data directory, and restart only after success.
- [ ] 2.2 Extend disposable updater smoke coordination to launch real installed `update --status` processes during helper readiness; verify recorded overlap actually occurs, finite readers exit within the deadline, the helper reaches installed, and executable version/journal/config/unowned sentinels remain correct. Do not substitute in-process outcome reads for executable overlap.
- [ ] 2.3 Add packaged persistent-use/lock-timeout acceptance and preserve active-MCP refusal; verify failed outcomes remain bounded/actionable, no installed files or data change before readiness, and fixture cleanup stops only fixture-owned processes after assertions.

## 3. Documentation and completion

- [ ] 3.1 Update installation and MCP validation docs with wait/check/restart guidance, timeout behavior, captured Windows/package evidence, and the old-copied-helper limitation when upgrading from v0.1.1; verify instructions do not promise the new helper runs when the update was launched by an older binary.
- [ ] 3.2 Run the design's focused updater checks, full test suite, package script, and updater-only packaged smoke; verify real race/persistent-use/preservation cases pass with retained logs and no real installation touched. Validate this OpenSpec change strictly after any artifact edits.
