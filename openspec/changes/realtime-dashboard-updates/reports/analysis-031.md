<!-- Saved by the orchestrator (Claude Code, claude-opus-5-5) verbatim from the reviewer's returned text. Reviewer: code-analyzer subagent, Claude Code 2.1.322, subagent auto mode, model claude-opus-5-5. Case: the client refused the reviewer's write to this path; the reviewer reported the refusal ("the client refused the write") and did not retry. Exact refusal message not supplied by the reviewer. -->

# Analysis report: realtime-dashboard-updates / ANALYSIS-INVESTIGATION-031

## Review target

- Reviewer / role: Claude subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.322 / subagent, auto mode / claude-opus-5-5
- Date: 2026-11-11T11:27Z
- Change root / assignment: `openspec/changes/realtime-dashboard-updates` / ANALYSIS-INVESTIGATION-031 (task 7.2, ORCH-011)
- Task and requirement references: task 7.2; [design.md](../design.md) lines 66 and 68 (checkpoint tag, 5 s cadence, slow connections keep only the newest vector)
- Scope and relevant neighboring paths:
  - Source: `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs`, `src/WorkLog/Dashboard/DashboardLiveConnections.cs`
  - Harness: `scripts/c06-fault-matrix.mjs` (route handler), `scripts/c06-acceptance-invariants.mjs` (planner)
  - Raw files under `artifacts/realtime-dashboard-updates/fault-matrix/`: `2026-11-11T11-19-17-025Z-e5f7a3b4.json` (failing), `2026-11-11T10-31-17-197Z-e556f130.json` and `2026-11-11T10-47-45-775Z-00b4434f.json` (passing)
- Reading limits and paths read outside scope: read-only, source and existing raw files only. Also read `design.md` for the checkpoint contract. No builds or runs.
- Baseline commit: 0d77685
- Implementation identity (before): `review-identity.ps1 -Baseline HEAD -Paths src/WorkLog/Dashboard scripts/c06-fault-matrix.mjs scripts/c06-acceptance-invariants.mjs` -> `head=0d77685 baseline=0d77685 files=8 diff=41086943032f untracked=5/657e4676f3b1`
- Before/after identity: identical after the analysis; no unexpected mutation.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Timer phase (candidate 4) | Timing arithmetic on the raw loss-window frames and `sourceBefore.checkpointCount` | Failing run: checkpoint count is 2 at fault start (17561 ms); drop at 17983 ms; seq 4 at 26567 ms, so seq 3 was due at about 21.56 s. Four untagged gen-2 frames arrive at 21562.8–21596.6 ms, 3579 ms after the drop. Passing runs: seq 3 recovered 3835 ms and 3587 ms after the drop, the same phase. | Excluded. The seq-3 tick happened inside the window at the expected phase; its frames arrived untagged. |
| Tag shape (candidate 1) | Route handler `scripts/c06-fault-matrix.mjs:246-263` reads `item.arguments[0].checkpoint`; the C# record serializes `Checkpoint` as camelCase `checkpoint` (`DashboardLiveConnections.cs:9-10`) | The same path recognized seq 4 in this run and seq 3 in both passing runs | Excluded |
| Baseline sequence (candidate 3) | Planner `c06-acceptance-invariants.mjs:9`; `checkpointRecoversGeneration` at :32-35 | The harness baseline of 2 matches the server count of 2. A seq-3 frame carrying gen 2 would have passed (3 > 2 and 2 ≥ 2). No tagged frame of any kind reached the proxy before 26.5 s. | Excluded; nothing was misclassified |
| Publish path (candidate 2) | Source inspection | See ANALYSIS-680 | Most likely cause. Inferred from source and raw timing; the server has no counter for displaced checkpoints. |

## Steady-state cost and bounds

| Question | Component(s) | Evidence type | Answer |
| --- | --- | --- | --- |
| Idle work per interval | `PublishCheckpointsAsync`, 5 s tick | source observation | Each tick sends one tagged vector plus an immediate full check. The full check sends at least one more untagged vector per connection (`:367`, plus one per lane at `:444`). Not measured; cost review is outside this assignment. |
| Cache lifetime versus interval | n/a | n/a | Not applicable to this single attribution question |
| Unbounded state | Per-connection channel | source observation | Bounded at capacity 1 with DropOldest. That bound is the mechanism behind ANALYSIS-680. |
| Repeated full reads | n/a | n/a | Not applicable; not examined |
| External tool discovery | n/a | n/a | Not applicable; no tool spawning in scope |

## Findings

### ANALYSIS-680: The checkpoint tag is evicted by the tick's own full-check publishes

- Severity: warning
- Related task / requirement: 7.2; design.md:66 and :68
- Locations:
  - `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs:173-181`: checkpoint publish, then `QueueFullCheck(immediate: true)`
  - `src/WorkLog/Dashboard/DashboardLiveCoordinator.cs:367` and `:444`: untagged publishes from the full check
  - `src/WorkLog/Dashboard/DashboardLiveConnections.cs:20-26`: capacity 1, `DropOldest`, `AllowSynchronousContinuations = false`
  - `src/WorkLog/Dashboard/DashboardLiveConnections.cs:92-103`: `Publish` tags the vector and calls `TryWrite`
- Observation:
  - Each tick's `Publish(CurrentSnapshot, checkpoint: true)` puts the tagged vector in each connection's one-item buffer.
  - Because continuations are asynchronous, the waiting send loop resumes on the thread pool and has not yet called `TryRead`.
  - The immediate full check (no debounce) then publishes untagged vectors from `ProcessBatch` (`:367`) and from each lane (`:444`). The first of these evicts the tagged vector.
  - The raw matches this. At about 21.56 s (the seq-3 phase), four untagged gen-2 frames arrive within 34 ms, followed by gen-3 frames. No tagged seq 3 appears in the loss window. In the passing runs, the tagged vector happened to be read first.
  - The server's `CheckpointCount` counts publications, not deliveries, so it advanced to 3 even though this connection never received seq 3.
- Classification:
  - No user data is lost. The untagged vectors that replaced the checkpoint are complete and at least as new (design.md:66), and the real client compares revisions, not tags.
  - The harness's drop fault suppresses every untagged frame until a tagged one arrives. That assumes every tick delivers a tagged frame to each connection. The product does not guarantee this, because the design lets a newer vector replace a pending one.
  - Result: a gap in the product contract that breaks the harness assumption. The fix belongs in the product publish path. A harness-only fix cannot spot an evicted checkpoint without weakening the tagged-checkpoint proof.
- Proposed improvement and benefit (minimal):
  - When a pending vector is replaced, keep the larger checkpoint number: `Checkpoint = max(old.Checkpoint, new.Checkpoint)`.
  - Implement it in `Publish`, either with a per-connection "latest vector" holder under a lock in place of the DropOldest channel, or with a read-merge-write under a lock.
  - This is valid because the tag is publication metadata and the newer vector is complete and at least as new.
  - Every tick then produces a tagged delivery within one period, and the harness model holds unchanged.
- Budget question: the agreed gate (measured from drop time, 5 s period + 1 s refresh = 6 s) is **not sufficient on current code**. In this run the next tagged frame arrived 8584 ms after the drop (26567 − 17983), and each further eviction adds 5 s. After the fix, expected recovery is about 3.6 s after the drop (21.56 s here) plus the client refresh, which fits within 6 s.
- Behavior-preservation risks: keep "newest vector only" for slow consumers, the authorization check at send time and the 2 s send timeout. Snapshot results must still omit `checkpoint`.
- Evidence type: source observation plus raw timing correlation. The eviction itself is inferred, not measured. Unproven whether browser A's connection also lost seq 3, because the transport arrays in the raw do not record the checkpoint value.
- Verification / measurement plan:
  - Manual-clock unit test: publish a checkpoint, then an untagged vector before the reader runs; assert that the delivered vector carries the checkpoint.
  - Rerun the fault matrix and confirm that drop cycles recover on seq = baselineCheckpointSequence + 1.
- Relationship: new

## Re-verification

None.

## Assessment and limitations

The most likely cause is the product's one-item replace-oldest buffer evicting the tagged checkpoint (candidate 2), not ordinary timer phase. Candidates 1, 3 and 4 are excluded using the raw files. I read source and raw files only; eviction could not be measured without a run. The orchestrator owns triage.
