import assert from "node:assert/strict";
import { acceptedHandshakeAfterRelease, createDisconnectControl } from "./c06-disconnect-control.mjs";

class FakeSocket {
  listeners = new Map();
  on(event, handler) { this.listeners.set(event, handler); }
  close() { this.listeners.get("close")?.(); }
}

class FakeRoute {
  closeHandler = null;
  closed = false;
  failClose = false;
  onClose(handler) { this.closeHandler = handler; }
  connectToServer() { const server = new FakeRoute(); this.server = server; return server; }
  async close(options) {
    if (this.failClose) throw new Error("fixture rejected close forwarding");
    const code = options?.code;
    if (code !== undefined && code !== 1000 && !(Number.isInteger(code) && code >= 3000 && code <= 4999))
      throw new RangeError(`invalid WebSocket close code ${code}`);
    if (this.closed) return;
    this.closed = true;
    this.closeOptions = options;
    this.closeHandler?.(code, options?.reason);
  }
  emitPeerClose(code, reason) {
    this.closed = true;
    this.closeHandler?.(code, reason);
  }
}

function routedPage() { return { routeWebSocket(_url, handler) { this.handler = handler; } }; }
async function route(control, label) {
  const page = routedPage();
  await control.routePage(page, label);
  const client = new FakeRoute();
  const handlerPromise = page.handler(client);
  return { client, handlerPromise };
}
async function openPair(control, label) {
  const routed = await route(control, label);
  await routed.handlerPromise;
  const socket = new FakeSocket();
  const generation = control.attachPageSocket(label, socket);
  return { ...routed, socket, generation, captured: control.captureCurrent([label]) };
}
async function completePageClose(socket) { socket.close(); }

const events = [];
const control = createDisconnectControl({ emit: event => events.push(event), now: () => events.length });
const normal = await openPair(control, "reader");
assert.equal(normal.generation.phase, "connected");
await control.closeCaptured(normal.captured, 1000);
await completePageClose(normal.socket);
assert.equal(control.requireClosed(normal.captured).reader.id, normal.generation.id);
assert.equal(normal.generation.server.closed, true, "client route close is forwarded upstream with a valid code");
assert.equal(normal.generation.server.closeOptions.code, 1000);

const custom = await openPair(control, "target");
await control.closeCaptured(custom.captured, 3001, "fixture custom close");
await completePageClose(custom.socket);
assert.equal(control.requireClosed(custom.captured).target.serverRouteClosed, true);
assert.equal(custom.generation.server.closeOptions.code, 3001, "valid custom close code is preserved");

const normalized = await openPair(control, "normalize");
normalized.generation.server.emitPeerClose(1006, "transport-only close code");
await completePageClose(normalized.socket);
assert.equal(normalized.client.closeOptions.code, 1000, "invalid incoming close code is normalized before native close");
assert.equal(normalized.generation.events.find(event => event.kind === "server-route-closed").code, 1006, "original peer close metadata is retained");
assert.equal(control.requireClosed(normalized.captured).normalize.serverRouteClosed, true);

const rejected = await openPair(control, "rejected");
rejected.generation.server.failClose = true;
rejected.client.emitPeerClose(1001, "forwarding rejection probe");
await completePageClose(rejected.socket);
assert.throws(() => control.requireClosed(rejected.captured), /incomplete/);
assert.equal(rejected.generation.forwardingFailures.length, 1);
assert(events.some(event => event.kind === "close-forward-failed" && event.generationId === rejected.generation.id));

const interleaved = await openPair(control, "reader2");
await control.closeCaptured(interleaved.captured, 1000);
control.setHoldReplacements(true);
const firstHeld = await route(control, "reader2");
await completePageClose(interleaved.socket);
const closedOriginal = control.requireClosed(interleaved.captured);
assert.equal(closedOriginal.reader2.id, interleaved.generation.id, "proof remains bound to the captured generation after current changes");
const secondHeld = await route(control, "reader2");
assert.equal(firstHeld.client.closed, true, "superseded held route is cancelled before release");
const currentHeld = control.heldCurrent(["reader2"], [interleaved.generation.id]);
assert.notEqual(currentHeld.reader2.id, interleaved.generation.id);
assert.equal(currentHeld.reader2.phase, "held");
assert.equal(currentHeld.reader2.routeHandlerReleased, false);
assert.equal(currentHeld.reader2.upstreamConnected, false);
let heldRouteHandlerSettled = false;
secondHeld.handlerPromise.then(() => { heldRouteHandlerSettled = true; });
await Promise.resolve();
assert.equal(heldRouteHandlerSettled, false, "the real routed WebSocket handshake stays gated until upstream release");
const released = await control.releaseHeld();
assert.deepEqual(released, [{ label: "reader2", id: currentHeld.reader2.id }]);
await secondHeld.handlerPromise;
assert.equal(heldRouteHandlerSettled, true, "releasing the held upstream resolves the actual Playwright route callback");
assert.equal(firstHeld.client.server, undefined, "superseded held route never connects upstream");
assert.ok(secondHeld.client.server, "only the current eligible held route connects on release");
assert.throws(() => control.requireClosed([{ label: "reader2", generationId: currentHeld.reader2.id }]), /incomplete/);

const acks = [
  { kind: "signalr-handshake-accepted", generationId: "old", monotonicMs: 99 },
  { kind: "signalr-handshake-accepted", generationId: "new", monotonicMs: 99 },
  { kind: "signalr-handshake-accepted", generationId: "other", monotonicMs: 120 },
  { kind: "signalr-handshake-accepted", generationId: "new", monotonicMs: 101 },
];
assert.equal(acceptedHandshakeAfterRelease(acks, "new", 100, 0), acks[3], "only exact generation acknowledgement at/after release is accepted");
assert.equal(acceptedHandshakeAfterRelease(acks.slice(0, 3), "new", 100, 0), null, "old/pre-release/wrong-generation acknowledgements are rejected");
assert.equal(acceptedHandshakeAfterRelease(acks, "new", 100, 3), acks[3]);
assert.equal(acceptedHandshakeAfterRelease(acks, "new", 100, 4), null);

await control.cleanup();
process.stdout.write("C06 disconnect control regressions passed: valid close codes, normalized peer codes, failed forwarding proof, captured generations under held replacement, stale route cancellation, and post-release handshake correlation.\n");
