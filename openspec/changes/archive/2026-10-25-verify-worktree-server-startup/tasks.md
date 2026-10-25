# Tasks

## 1. Isolated worktree fixture

- [x] 1.1 Add an optional working directory to SmokeProcessRunner and per-client trust selection for fixture paths; verify existing smoke calls retain their current behavior and explicit paths never use the real client profile.
- [x] 1.2 Add a committed disposable main repository plus an external sibling worktree with isolated homes/data and recorded application/Git/client versions; verify the first worktree probe runs before WorkLog init or generated-config copying in that worktree.

## 2. Discovery and connection checks

- [x] 2.1 Check current official client documentation and choose non-authenticated discovery/health probes; record the commands, versions, and limitations, verifying no agent prompts, account access, or production profile changes are required.
- [x] 2.2 Probe Claude and Codex from the initialized main checkout and fresh worktree, capturing discovery separately from connection; verify a missing pre-init registration is recorded as a finding while baseline failures remain actionable acceptance failures.
- [x] 2.3 Initialize each selected client explicitly in the worktree and repeat discovery/connection plus the main-checkout baseline; verify explicit setup connects and does not remove the original checkout's registration.
- [x] 2.4 Exercise initialize/tools-list with the exact discovered server command and arguments when a native client health probe is unavailable; verify both WorkLog tools are advertised and label direct protocol results separately from client discovery/health results.
- [x] 2.5 Record effective repository/data-directory selection for each connection, and assert shared identity/ticket context when the F1-F5 change is present; verify context failures are distinguished from missing registration or failed executable startup without introducing production fixes here.

## 3. Evidence and completion

- [x] 3.1 Emit a bounded stage-by-stage result summary and local log references for the required matrix; verify results distinguish configuration discovery, process launch, MCP protocol, and context failures and contain no real-profile data.
- [x] 3.2 Document the verified terminal-client worktree setup procedure, need for explicit init when observed, restart/trust steps, and untested launch modes; verify every behavior claim is supported by versioned results rather than extrapolated to Desktop or managed --worktree sessions.
- [x] 3.3 Run relevant regression tests and the packaged smoke matrix with both supported clients; verify the main baseline, explicit-worktree setup, and post-setup baseline pass, report missing prerequisites honestly, and preserve actionable findings for any production defect as a separately scoped follow-up.
