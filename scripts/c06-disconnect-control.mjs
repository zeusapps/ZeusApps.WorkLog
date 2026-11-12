const nativeCloseCode = code => code === 1000 || (Number.isInteger(code) && code >= 3000 && code <= 4999) ? code : 1000;

export function acceptedHandshakeAfterRelease(accepted, generationId, releaseMonotonicMs, afterIndex = 0) {
  if (!Array.isArray(accepted) || typeof generationId !== "string" || !Number.isFinite(releaseMonotonicMs)) return null;
  return accepted.slice(Math.max(0, afterIndex)).find(item => item.kind === "signalr-handshake-accepted" &&
    item.generationId === generationId && Number.isFinite(item.monotonicMs) && item.monotonicMs >= releaseMonotonicMs) ?? null;
}

export function createDisconnectControl({ emit = () => {}, now = () => performance.now() } = {}) {
  const generations = new Map();
  const current = new Map();
  const bySocket = new WeakMap();
  let holdReplacements = false;
  let nextId = 0;

  function record(kind, generation, details = {}) {
    const event = { kind, label: generation.label, generationId: generation.id, atMonotonicMs: now(), ...details };
    generation.events.push(event);
    emit(event);
  }

  function releaseRouteHandler(generation) {
    if (generation.routeHandlerReleased) return;
    generation.routeHandlerReleased = true;
    generation.resolveRouteHandler?.();
  }

  function failedForward(generation, direction, originalCode, effectiveCode, error) {
    const failure = { direction, originalCode: originalCode ?? null, effectiveCode, message: String(error?.message ?? error ?? "close forwarding failed").slice(0, 200) };
    generation.forwardingFailures.push(failure);
    record("close-forward-failed", generation, failure);
  }

  async function forwardClose(generation, direction, target, code, reason) {
    const effectiveCode = nativeCloseCode(code);
    try {
      await target.close({ code: effectiveCode, reason });
      return true;
    } catch (error) {
      failedForward(generation, direction, code, effectiveCode, error);
      return false;
    }
  }

  async function closeGeneration(generation, code = 1000, reason = "C06 controlled disconnect") {
    generation.superseded = true;
    if (generation.phase === "held" || generation.phase === "pending") {
      generation.phase = "cancelled";
      record("held-handshake-cancelled", generation);
      try { await generation.client.close({ code: nativeCloseCode(code), reason }); }
      catch (error) { failedForward(generation, "cancel-held-client", code, nativeCloseCode(code), error); }
      releaseRouteHandler(generation);
      return;
    }
    if (generation.phase === "connected") await forwardClose(generation, "superseded-client", generation.client, code, reason);
  }

  function connect(generation) {
    if (generation.superseded || current.get(generation.label) !== generation || generation.phase !== "held") return false;
    generation.phase = "connecting";
    const server = generation.client.connectToServer();
    generation.server = server;
    generation.phase = "connected";
    record("upstream-connected", generation);
    generation.client.onClose((code, reason) => {
      if (generation.clientRouteClosed) return;
      generation.clientRouteClosed = true;
      record("client-route-closed", generation, { code: code ?? null, reason: reason ?? null });
      if (!generation.serverRouteClosed && !generation.forwardingClientClose) {
        generation.forwardingClientClose = true;
        void forwardClose(generation, "client-to-server", server, code, reason);
      }
    });
    server.onClose((code, reason) => {
      if (generation.serverRouteClosed) return;
      generation.serverRouteClosed = true;
      record("server-route-closed", generation, { code: code ?? null, reason: reason ?? null });
      if (!generation.clientRouteClosed && !generation.forwardingServerClose) {
        generation.forwardingServerClose = true;
        void forwardClose(generation, "server-to-client", generation.client, code, reason);
      }
    });
    return true;
  }

  function routePage(page, label) {
    return page.routeWebSocket("**/api/live**", client => new Promise(resolveRouteHandler => {
      const prior = current.get(label);
      const generation = {
        id: `a${++nextId}`, label, client, server: null, socket: null, socketClosed: false,
        clientRouteClosed: false, serverRouteClosed: false, forwardingClientClose: false,
        forwardingServerClose: false, forwardingFailures: [], superseded: false, phase: "held", events: [],
        resolveRouteHandler, routeHandlerReleased: false,
      };
      if (!generations.has(label)) generations.set(label, []);
      generations.get(label).push(generation);
      current.set(label, generation);
      record("route-created", generation);
      if (prior && prior !== generation && ["held", "pending", "connecting"].includes(prior.phase))
        void closeGeneration(prior, 1000, "C06 superseded held handshake");
      if (holdReplacements) record("replacement-held", generation);
      else {
        connect(generation);
        releaseRouteHandler(generation);
      }
    }));
  }

  function attachPageSocket(label, socket) {
    const list = generations.get(label) ?? [];
    const generation = [...list].reverse().find(candidate => !candidate.socket && !candidate.superseded && candidate.phase !== "cancelled");
    if (!generation) return null;
    generation.socket = socket;
    bySocket.set(socket, generation);
    record("page-socket-open", generation);
    socket.on("close", () => {
      if (generation.socketClosed) return;
      generation.socketClosed = true;
      record("page-socket-closed", generation);
    });
    return generation;
  }

  function generationForSocket(label, socket) {
    const generation = bySocket.get(socket);
    return generation?.label === label ? generation : null;
  }

  function setHoldReplacements(value) { holdReplacements = value === true; }
  function captureCurrent(labels) {
    return labels.map(label => {
      const generation = current.get(label);
      if (!generation) throw new Error(`No owned current WebSocket generation for ${label}.`);
      return { label, generationId: generation.id };
    });
  }
  function findCaptured(captured) {
    return captured.map(reference => ({ reference, generation: (generations.get(reference.label) ?? []).find(item => item.id === reference.generationId) ?? null }));
  }
  function currentSnapshot(label) {
    const generation = current.get(label);
    if (!generation) return null;
    return snapshot(generation);
  }
  function snapshot(generation) {
    return { id: generation.id, label: generation.label, phase: generation.phase, socketOpened: generation.socket !== null,
      socketClosed: generation.socketClosed, clientRouteClosed: generation.clientRouteClosed, serverRouteClosed: generation.serverRouteClosed,
      routeHandlerReleased: generation.routeHandlerReleased, upstreamConnected: generation.server !== null,
      forwardingFailures: [...generation.forwardingFailures] };
  }
  function capturedSnapshots(captured) {
    const entries = findCaptured(captured);
    if (entries.some(item => !item.generation)) throw new Error("A captured WebSocket generation is no longer owned.");
    return Object.fromEntries(entries.map(({ reference, generation }) => [reference.label, snapshot(generation)]));
  }
  function requireClosed(captured) {
    if (!Array.isArray(captured) || !captured.length || new Set(captured.map(item => item.label)).size !== captured.length)
      throw new Error("Closed WebSocket proof requires distinct captured generations.");
    const entries = findCaptured(captured);
    const invalid = entries.filter(({ generation }) => !generation || !generation.socket || !generation.socketClosed ||
      !generation.clientRouteClosed || !generation.serverRouteClosed || generation.forwardingFailures.length > 0);
    if (invalid.length) throw new Error(`Captured browser socket and routed pair are incomplete: ${invalid.map(item => item.reference.label).join(", ")}.`);
    return capturedSnapshots(captured);
  }
  async function closeCaptured(captured, code = 1000, reason = "C06 controlled disconnect") {
    const entries = findCaptured(captured);
    if (!captured.length || entries.some(({ generation }) => !generation || !generation.socket || generation.socketClosed || generation.phase !== "connected"))
      throw new Error("Cannot close a missing, stale, or non-open captured WebSocket generation.");
    const results = await Promise.all(entries.map(async ({ reference, generation }) => {
      try { await generation.client.close({ code: nativeCloseCode(code), reason }); return true; }
      catch (error) { failedForward(generation, "controlled-close", code, nativeCloseCode(code), error); return false; }
    }));
    if (results.some(result => !result)) throw new Error("A captured browser WebSocket close request failed.");
  }
  function heldCurrent(labels, excludedIds = []) {
    const excluded = new Set(excludedIds);
    const selected = Object.fromEntries(labels.map(label => [label, current.get(label)]));
    if (labels.some(label => !selected[label] || selected[label].phase !== "held" || selected[label].server !== null ||
      selected[label].routeHandlerReleased || excluded.has(selected[label].id))) return null;
    return Object.fromEntries(labels.map(label => [label, snapshot(selected[label])]));
  }
  async function releaseHeld() {
    holdReplacements = false;
    const opened = [];
    for (const [label, generation] of current) {
      if (generation.phase === "held" && !generation.superseded && connect(generation)) {
        releaseRouteHandler(generation);
        opened.push({ label, id: generation.id });
      }
    }
    return opened;
  }
  async function cleanup() {
    holdReplacements = false;
    for (const list of generations.values()) for (const generation of list) {
      if (generation.phase === "held" || generation.phase === "pending") await closeGeneration(generation, 1000, "C06 cleanup");
      else if (generation.phase === "connected" && !generation.clientRouteClosed) await forwardClose(generation, "cleanup-client", generation.client, 1000, "C06 cleanup");
    }
  }

  return { routePage, attachPageSocket, generationForSocket, setHoldReplacements, captureCurrent, currentSnapshot,
    capturedSnapshots, requireClosed, closeCaptured, heldCurrent, releaseHeld, cleanup };
}
