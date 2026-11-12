import { pathToFileURL } from "node:url";

export function planRevisionInjection({ kind, baselineGeneration, generation, handled = false, heldGeneration = null,
  lossOpen = false, lossGeneration = null, checkpointSequence = null, baselineCheckpointSequence = -1 }) {
  if (!Number.isFinite(baselineGeneration) || !Number.isFinite(generation)) return { action: "pass" };
  if (kind === "drop") {
    if (!lossOpen) return generation > baselineGeneration && !handled ? { action: "drop", generation } : { action: "pass" };
    if (generation < lossGeneration) return { action: "pass" };
    if (Number.isFinite(checkpointSequence) && checkpointSequence > baselineCheckpointSequence && generation >= lossGeneration)
      return { action: "recover", generation, checkpointSequence };
    return { action: "suppress", generation, checkpointSequence: Number.isFinite(checkpointSequence) ? checkpointSequence : null };
  }
  if (generation <= baselineGeneration || handled) return { action: "pass" };
  if (kind === "duplicate") return { action: "duplicate", generation };
  if (kind === "reorder") {
    if (heldGeneration === null) return { action: "hold", generation };
    if (generation > heldGeneration) return { action: "reorder", generations: [heldGeneration, generation] };
  }
  return { action: "pass" };
}

export function monotonicElapsedMs(startMonotonicMs, endMonotonicMs) {
  if (!Number.isFinite(startMonotonicMs) || !Number.isFinite(endMonotonicMs) || endMonotonicMs < startMonotonicMs)
    throw new Error("Recovery timing requires ordered monotonic timestamps.");
  return Math.round(endMonotonicMs - startMonotonicMs);
}

export function isTargetAndLive(state) {
  return state?.markerVisible === true && state?.live === true;
}

export function checkpointRecoversGeneration(vector, revisionKey, droppedGeneration, baselineCheckpointSequence) {
  return Number.isFinite(vector?.checkpoint) && vector.checkpoint > baselineCheckpointSequence &&
    Number.isFinite(vector?.[revisionKey]) && vector[revisionKey] >= droppedGeneration;
}

export function isCheckpointRecoveryDelivery(frame, sequence, revisionKey, droppedGeneration, baselineCheckpointSequence) {
  return frame?.kind === "revision-frame" && frame.vector?.checkpoint === sequence &&
    checkpointRecoversGeneration(frame.vector, revisionKey, droppedGeneration, baselineCheckpointSequence);
}

export function classifyIdleEligibility({ startStatus, startTransportCount, liveSamples, statusChanges, connectionEvents, userActions }) {
  const reasons = [];
  if (startStatus !== "live") reasons.push("window did not start Live");
  if (startTransportCount < 1) reasons.push("no established WebSocket or EventSource at window start");
  if (liveSamples.some(sample => sample.status !== "live")) reasons.push("Live status was interrupted during the window");
  if (liveSamples.some(sample => sample.establishedTransportCount < 1)) reasons.push("live transport was interrupted during the window");
  if (statusChanges.some(change => change.states.length === 0)) reasons.push("Live status surface disappeared during the window");
  if (statusChanges.some(change => change.states.some(state => state !== "live"))) reasons.push("Live status changed away from Live during the window");
  if (connectionEvents.some(event => ["websocket-close", "eventsource-close", "eventsource-failed"].includes(event.kind)))
    reasons.push("transport closed or failed during the window");
  if (userActions.length) reasons.push("user interaction occurred during the window");
  return { state: reasons.length ? "ineligible" : "eligible", reasons: [...new Set(reasons)] };
}

export function isSettleQuiet({ status, nowMs, lastActivityMs, settleMs }) {
  return status === "live" && Number.isFinite(nowMs) && Number.isFinite(lastActivityMs) && nowMs - lastActivityMs >= settleMs;
}

function verifyInvariants() {
  if (!isSettleQuiet({ status: "live", nowMs: 3000, lastActivityMs: 1000, settleMs: 2000 }) ||
      isSettleQuiet({ status: "live", nowMs: 2999, lastActivityMs: 1000, settleMs: 2000 }) ||
      isSettleQuiet({ status: "reconnecting", nowMs: 9000, lastActivityMs: 1000, settleMs: 2000 }) ||
      isSettleQuiet({ status: null, nowMs: 9000, lastActivityMs: 1000, settleMs: 2000 }))
    throw new Error("The idle pre-window settle requires Live and a full quiet period since the last activity.");
  for (const kind of ["drop", "duplicate", "reorder"]) {
    if (planRevisionInjection({ kind, baselineGeneration: 7, generation: 7 }).action !== "pass")
      throw new Error(`${kind} must ignore an unchanged revision/checkpoint vector.`);
  }
  if (planRevisionInjection({ kind: "drop", baselineGeneration: 7, generation: 8 }).action !== "drop" ||
      planRevisionInjection({ kind: "drop", baselineGeneration: 7, generation: 8, handled: true }).action !== "pass")
    throw new Error("A dropped advanced generation must be injected exactly once.");
  if (planRevisionInjection({ kind: "reorder", baselineGeneration: 7, generation: 8, heldGeneration: 8 }).action !== "pass" ||
      planRevisionInjection({ kind: "reorder", baselineGeneration: 7, generation: 9, heldGeneration: 8 }).action !== "reorder")
    throw new Error("Reordering must wait for one strictly newer target generation.");
  const baselineCheckpointSequence = 4, droppedGeneration = 8;
  const startLoss = planRevisionInjection({ kind: "drop", baselineGeneration: 7, generation: 8, baselineCheckpointSequence });
  const sameGenerationOrdinary = planRevisionInjection({ kind: "drop", baselineGeneration: 7, generation: 8, handled: true, lossOpen: true, lossGeneration: droppedGeneration, baselineCheckpointSequence });
  const staleCheckpoint = planRevisionInjection({ kind: "drop", baselineGeneration: 7, generation: 7, handled: true, lossOpen: true, lossGeneration: droppedGeneration, checkpointSequence: 5, baselineCheckpointSequence });
  const recoveredCheckpoint = planRevisionInjection({ kind: "drop", baselineGeneration: 7, generation: 8, handled: true, lossOpen: true, lossGeneration: droppedGeneration, checkpointSequence: 5, baselineCheckpointSequence });
  const afterWindow = planRevisionInjection({ kind: "drop", baselineGeneration: 7, generation: 8, handled: true, lossOpen: false, lossGeneration: droppedGeneration, checkpointSequence: 5, baselineCheckpointSequence });
  if (startLoss.action !== "drop" || sameGenerationOrdinary.action !== "suppress" || staleCheckpoint.action !== "pass" ||
      recoveredCheckpoint.action !== "recover" || afterWindow.action !== "pass" ||
      checkpointRecoversGeneration({ journal: 8 }, "journal", droppedGeneration, baselineCheckpointSequence) ||
      checkpointRecoversGeneration({ checkpoint: 5, journal: 7 }, "journal", droppedGeneration, baselineCheckpointSequence) ||
      !checkpointRecoversGeneration({ checkpoint: 5, journal: 8 }, "journal", droppedGeneration, baselineCheckpointSequence) ||
      isCheckpointRecoveryDelivery({ kind: "revision-frame", vector: { journal: 8 } }, 5, "journal", droppedGeneration, baselineCheckpointSequence) ||
      isCheckpointRecoveryDelivery({ kind: "revision-frame", vector: { checkpoint: 6, journal: 8 } }, 5, "journal", droppedGeneration, baselineCheckpointSequence) ||
      isCheckpointRecoveryDelivery({ kind: "snapshot-result", vector: { checkpoint: 5, journal: 8 } }, 5, "journal", droppedGeneration, baselineCheckpointSequence) ||
      !isCheckpointRecoveryDelivery({ kind: "revision-frame", vector: { checkpoint: 5, journal: 8 } }, 5, "journal", droppedGeneration, baselineCheckpointSequence))
    throw new Error("Lost-generation recovery requires one bounded loss window ending only at a newer tagged checkpoint carrying the generation.");
  if (monotonicElapsedMs(100, 5100) !== 5000)
    throw new Error("The recovery gate must use monotonic elapsed time, independent of UTC clock changes.");
  if (!isTargetAndLive({ markerVisible: true, live: true }) ||
      isTargetAndLive({ markerVisible: true, live: false }) ||
      isTargetAndLive({ markerVisible: false, live: true }))
    throw new Error("Final reconciliation requires the current marker and Live status simultaneously.");
  const markerRemovedBeforeLive = [{ markerVisible: true, live: false }, { markerVisible: false, live: true }];
  if (markerRemovedBeforeLive.some(isTargetAndLive))
    throw new Error("A marker removed before Live must not be accepted as reconciled.");
  const healthy = classifyIdleEligibility({ startStatus: "live", startTransportCount: 1,
    liveSamples: [{ status: "live", establishedTransportCount: 1 }], statusChanges: [{ states: ["live"] }], connectionEvents: [], userActions: [] });
  const interrupted = classifyIdleEligibility({ startStatus: "live", startTransportCount: 1,
    liveSamples: [{ status: "live", establishedTransportCount: 1 }], statusChanges: [{ states: ["paused"] }], connectionEvents: [{ kind: "websocket-close" }], userActions: [{ type: "click" }] });
  const missingStatus = classifyIdleEligibility({ startStatus: "live", startTransportCount: 1,
    liveSamples: [{ status: "live", establishedTransportCount: 1 }], statusChanges: [{ states: [] }, { states: ["live"] }], connectionEvents: [], userActions: [] });
  if (healthy.state !== "eligible" || interrupted.state !== "ineligible" || interrupted.reasons.length !== 3 || missingStatus.state !== "ineligible")
    throw new Error("Idle-window eligibility must reject paused, disconnected, user-driven or missing-status observations.");
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  verifyInvariants();
  process.stdout.write("C06 no-host acceptance invariants passed.\n");
}
