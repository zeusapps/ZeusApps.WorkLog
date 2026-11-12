import assert from "node:assert/strict";
import { hostCleanupComplete, startTrackedHost, stopTrackedHost } from "./c06-host-lifecycle.mjs";
import { processOutcome } from "./c06-process-outcome.mjs";

let retained = null;
const child = { pid: 517, exitCode: null };
await assert.rejects(startTrackedHost({
  spawn: () => child,
  port: 49123,
  base: "http://127.0.0.1:49123",
  watcherMode: "fake",
  onOwned: (host) => { retained = host; },
  waitForHealth: async () => { throw new Error("injected readiness failure"); },
  waitForRoots: async () => { throw new Error("must not wait for roots"); },
}), /injected readiness failure/);
assert.equal(retained?.child, child);
assert.equal(retained?.wrapperPid, 517);
assert.equal(retained?.port, 49123);
assert.equal(retained?.startupFailure, "injected readiness failure");
let wrapperKilled = false, ownedStopCount = 0, portProbe = 0;
const cleanup = await stopTrackedHost(retained, {
  childHasExited: (process) => process.exitCode !== null,
  sendStop: async () => { ownedStopCount++; child.exitCode = 0; },
  killChild: () => { wrapperKilled = true; child.exitCode = -9; },
  waitChildExit: async (process) => process.exitCode !== null,
  readReady: async () => ({ port: 49123, pid: 615 }),
  readFinal: async () => ({ kind: "stopped" }),
  portReleased: async (port) => { portProbe++; return { released: port === 49123, attempts: 1 }; },
});
assert.equal(cleanup.wrapperExitObserved, true);
assert.equal(cleanup.wrapperPid, 517);
assert.equal(cleanup.hostPid, 615);
assert.equal(cleanup.gracefulStopAcknowledged, true);
assert.equal(cleanup.gracefulExitObserved, true);
assert.equal(cleanup.forcedTermination, false);
assert.equal(ownedStopCount, 1);
assert.equal(wrapperKilled, false);
assert.equal(portProbe, 1);
assert.equal(hostCleanupComplete(cleanup), true);
const forcedChild = { pid: 518, exitCode: null };
const forcedCleanup = await stopTrackedHost({ child: forcedChild, wrapperPid: 518, port: 49124, watcherMode: "fake", startupFailure: "ready file absent" }, {
  childHasExited: (ownedChild) => ownedChild.exitCode !== null,
  sendStop: async () => { throw new Error("stop must not target an unknown host"); },
  killChild: (ownedChild) => { wrapperKilled = true; ownedChild.exitCode = -9; },
  waitChildExit: async (ownedChild) => ownedChild.exitCode !== null,
  readReady: async () => null,
  readFinal: async () => null,
  portReleased: async (port) => ({ released: port === 49124, attempts: 1 }),
});
assert.equal(forcedCleanup.wrapperExitObserved, true);
assert.equal(forcedCleanup.wrapperPid, 518);
assert.equal(forcedCleanup.gracefulStopAcknowledged, false);
assert.equal(forcedCleanup.gracefulExitObserved, false);
assert.equal(forcedCleanup.forcedTermination, true);
assert.equal(wrapperKilled, true);
assert.equal(hostCleanupComplete(forcedCleanup), true);

const delayedChild = { pid: 519, exitCode: null };
let delayedWaits = 0, delayedKilled = false;
const delayedCleanup = await stopTrackedHost({
  child: delayedChild, wrapperPid: 519, port: 49125, ready: { port: 49125, pid: 616 },
  health: { pid: 616 }, watcherMode: "fake", startupFailure: null,
}, {
  childHasExited: (ownedChild) => ownedChild.exitCode !== null,
  sendStop: async () => { /* ACK arrives before the host finishes disposing. */ },
  killChild: () => { delayedKilled = true; delayedChild.exitCode = -9; },
  waitChildExit: async (ownedChild, timeoutMs) => {
    delayedWaits++;
    assert.equal(timeoutMs, 10_000);
    ownedChild.exitCode = 0; // Healthy StopAsync completes inside the grace window.
    return true;
  },
  readReady: async () => ({ port: 49125, pid: 616 }),
  readFinal: async () => ({ source: { isDisposed: true }, activeConnectionCount: 0 }),
  portReleased: async (port) => ({ released: port === 49125, attempts: 1 }),
});
assert.equal(delayedWaits, 1);
assert.equal(delayedKilled, false);
assert.equal(delayedCleanup.gracefulStopAcknowledged, true);
assert.equal(delayedCleanup.gracefulExitObserved, true);
assert.equal(delayedCleanup.forcedTermination, false);
assert.equal(delayedCleanup.final?.source?.isDisposed, true);
assert.equal(delayedCleanup.final?.activeConnectionCount, 0);
assert.equal(hostCleanupComplete(delayedCleanup), true);

const unresponsiveChild = { pid: 520, exitCode: null };
let unresponsiveWaits = 0, unresponsiveKilled = false;
const unresponsiveCleanup = await stopTrackedHost({
  child: unresponsiveChild, wrapperPid: 520, port: 49126, ready: { port: 49126, pid: 617 },
  health: { pid: 617 }, watcherMode: "fake", startupFailure: null,
}, {
  childHasExited: (ownedChild) => ownedChild.exitCode !== null,
  sendStop: async () => { /* ACK succeeds, but the wrapper never exits gracefully. */ },
  killChild: () => { unresponsiveKilled = true; },
  waitChildExit: async (ownedChild, timeoutMs) => {
    unresponsiveWaits++;
    assert.equal(timeoutMs, 10_000);
    if (unresponsiveWaits === 2) ownedChild.exitCode = -9;
    return ownedChild.exitCode !== null;
  },
  readReady: async () => ({ port: 49126, pid: 617 }),
  readFinal: async () => null,
  portReleased: async (port) => ({ released: port === 49126, attempts: 1 }),
});
assert.equal(unresponsiveWaits, 2);
assert.equal(unresponsiveKilled, true);
assert.equal(unresponsiveCleanup.gracefulStopAcknowledged, true);
assert.equal(unresponsiveCleanup.gracefulExitObserved, false);
assert.equal(unresponsiveCleanup.forcedTermination, true);
assert.equal(unresponsiveCleanup.wrapperExitObserved, true);
assert.equal(hostCleanupComplete(unresponsiveCleanup), true);
assert.equal(hostCleanupComplete({ wrapperExitObserved: false, portReleased: { released: true } }), false);
assert.equal(hostCleanupComplete({ wrapperExitObserved: true, portReleased: { released: false } }), false);
assert.deepEqual(processOutcome({ status: null, signal: "SIGTERM", error: { code: "ETIMEDOUT" } }),
  { exitCode: null, signal: "SIGTERM", timedOut: true });
assert.deepEqual(processOutcome({ status: 2, signal: null, error: undefined }),
  { exitCode: 2, signal: null, timedOut: false });
process.stdout.write("C06 owned-host startup failure invariants passed.\n");
