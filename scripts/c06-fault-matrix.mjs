import { createHmac, randomBytes, randomUUID } from "node:crypto";
import { spawn, spawnSync } from "node:child_process";
import { createRequire } from "node:module";
import { createServer } from "node:net";
import { mkdir, readFile, rename, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { performance } from "node:perf_hooks";
import { checkpointRecoversGeneration, isCheckpointRecoveryDelivery, isTargetAndLive, monotonicElapsedMs, planRevisionInjection } from "./c06-acceptance-invariants.mjs";
import { hostCleanupComplete, startTrackedHost, stopTrackedHost } from "./c06-host-lifecycle.mjs";
import { acceptedHandshakeAfterRelease, createDisconnectControl } from "./c06-disconnect-control.mjs";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const artifacts = join(root, "artifacts", "realtime-dashboard-updates");
const require = createRequire(join(root, "dashboard", "package.json"));
const { chromium } = require("@playwright/test");
const smokeDll = join(root, "tools", "WorkLog.Smoke", "bin", "Release", "net10.0", "WorkLog.Smoke.dll");
const phase = "fault-matrix";
const timestamp = new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
const outputDirectory = join(artifacts, phase);
await mkdir(outputDirectory, { recursive: true });
const output = join(outputDirectory, `${timestamp}-${randomUUID().slice(0, 8)}.json`);
const fixture = join(artifacts, "fixtures", `${phase}-${randomUUID()}`);
const controls = join(fixture, "controls");
const logs = join(fixture, "logs");
const repositories = ["example/c06-1", "example/c06-2"];
const domains = ["Documents", "Journal", "Digests"];
const revisionKeys = { Documents: "documents", Journal: "journal", Digests: "digests" };
const args = parseArgs(process.argv.slice(2));
const browserChannel = process.env.WORKLOG_BROWSER_CHANNEL ?? "msedge";
const deadlineMs = Number(args.deadlineMinutes ?? 60) * 60_000;
const smokeMode = args.smoke === true || args.smoke === "true";
const disconnectOnly = args.disconnectOnly === true || args.disconnectOnly === "true";
const cyclePlan = disconnectOnly ? [1] : smokeMode ? [1, 26, 51, 71, 86] : Array.from({ length: 100 }, (_, index) => index + 1);
if (!/^[a-z0-9-]{1,24}$/i.test(String(args.phase ?? "final"))) throw new Error("--phase must be a short alphanumeric label.");
if (!Number.isFinite(deadlineMs) || deadlineMs < 60_000 || deadlineMs > 90 * 60_000) throw new Error("--deadlineMinutes must be from 1 through 90.");
if (!existsSync(smokeDll)) throw new Error("The Smoke Release DLL is missing; build only after the parent grants the shared build slot.");

const result = {
  schemaVersion: 1,
  state: "running",
  phase: String(args.phase ?? "final"),
  startedAtUtc: new Date().toISOString(),
  matrix: { mode: disconnectOnly ? "bounded-single-disconnect-diagnostic" : smokeMode ? "bounded-five-category-smoke-not-acceptance" : "full-acceptance", cycles: cyclePlan.length, categories: disconnectOnly ? { disconnect: 1 } : { disconnect: 25, revisionWire: 25, serverRestart: 20, watcherGapOverflow: 15, secretRevokeReauthorize: 15 }, independentBrowserContexts: 2, pages: 3, liveConnections: 3, domainsRotated: disconnectOnly ? ["Documents"] : domains },
  scope: "isolated Smoke host using internal change-source seam; real SignalR transport; synthetic C06 fixture",
  fixture: relative(root, fixture),
  environment: { platform: process.platform, architecture: process.arch, nodeVersion: process.version, browser: browserChannel },
  records: [],
  browserDiagnostics: [],
  connectionLifecycle: [],
  statusTransitions: [],
  topology: { independentBrowserContexts: 2, pages: 3, liveConnections: 3, contextA: ["reader-with-draft", "domain-target"], contextB: ["domain-target"] },
  assertions: { incorrectFinalStates: 0, lostSavedObservations: 0, discardedDrafts: 0, accumulatedNotices: 0, leakedProcesses: 0, maxPendingHintCount: 0, maxPendingNotifications: 0, maxOverflowBatchCount: 0, persistentUnavailable: [] },
  references: { nativeClaudeCodeSessions: "not run", nativeCodexSessions: "not run", fixtureProtocolLabels: ["Claude Code protocol fixture", "Codex protocol fixture"] },
  cleanup: null,
};
await saveResult();

let browser, contextA, contextB, pageA, pageATarget, pageB, host = null;
let disconnectControl = null;
let cancelReason = null;
const ownedChildren = new Set();
const controller = new AbortController();
let sequence = 0;
let activeCycle = null;
let activeFaultStartMonotonicMs = null;
let activeWireFault = null;
// One 5000 ms checkpoint period plus 1000 ms client refresh, measured from the fault (drop/gap) to browser-visible.
const CHECKPOINT_RECOVERY_BUDGET_MS = 6000;
let routeMetrics = { revisions: 0, snapshots: 0, dropped: 0, duplicated: 0, reordered: 0, held: null };
const transportA = createTransportTracker(), transportAReader = createTransportTracker(), transportB = createTransportTracker();
let readerContextBefore = null;
const totalDeadline = setTimeout(() => cancel("deadline"), deadlineMs);
totalDeadline.unref();
const onSignal = (signal) => () => cancel(signal);
const onSigint = onSignal("SIGINT"), onSigterm = onSignal("SIGTERM");
process.on("SIGINT", onSigint);
process.on("SIGTERM", onSigterm);

function parseArgs(values) {
  const parsed = {};
  for (let i = 0; i < values.length; i++) {
    const part = values[i].replace(/^--/, "");
    const equals = part.indexOf("=");
    if (equals >= 0) parsed[part.slice(0, equals)] = part.slice(equals + 1);
    else if (values[i + 1] && !values[i + 1].startsWith("--")) parsed[part] = values[++i];
    else parsed[part] = true;
  }
  return parsed;
}

function cancel(reason) {
  if (controller.signal.aborted) return;
  cancelReason = reason;
  controller.abort();
  for (const child of ownedChildren) if (!childHasExited(child)) child.kill();
  void browser?.close();
}

function childHasExited(child) {
  return child.exitCode !== null || child.signalCode !== null;
}

function delay(ms) {
  return new Promise((resolveDelay, reject) => {
    if (controller.signal.aborted) { reject(new Error(`Cancelled (${cancelReason ?? "cancelled"}).`)); return; }
    const timer = setTimeout(done, ms);
    const abort = () => { clearTimeout(timer); reject(new Error(`Cancelled (${cancelReason ?? "cancelled"}).`)); };
    function done() { controller.signal.removeEventListener("abort", abort); resolveDelay(); }
    controller.signal.addEventListener("abort", abort, { once: true });
  });
}

function spawnOwned(executable, args, options = {}) {
  const child = spawn("dotnet", [smokeDll, "--c06-owned-process", executable, ...args], {
    cwd: root,
    windowsHide: true,
    env: { ...process.env, DOTNET_BUNDLE_EXTRACT_BASE_DIR: join(fixture, "bundle") },
    stdio: ["ignore", "pipe", "pipe"],
    ...options,
  });
  ownedChildren.add(child);
  child.once("exit", () => ownedChildren.delete(child));
  child.stdout?.on("data", (data) => appendLog("process.stdout.log", sanitize(data.toString())));
  child.stderr?.on("data", (data) => appendLog("process.stderr.log", sanitize(data.toString())));
  return child;
}

function runOwned(executable, parameters, timeout = 120_000) {
  const completed = spawnSync("dotnet", [smokeDll, "--c06-owned-process", executable, ...parameters], {
    cwd: root, encoding: "utf8", windowsHide: true, timeout,
    env: { ...process.env, DOTNET_BUNDLE_EXTRACT_BASE_DIR: join(fixture, "bundle") },
  });
  if (completed.error || completed.status !== 0) {
    const detail = sanitize(completed.stderr || completed.stdout || completed.error?.message || "no child diagnostic output").trim().slice(-1200);
    throw new Error(`Owned fixture process failed (${parameters[0] ?? "process"}, exit ${completed.status ?? "unknown"}): ${detail}`);
  }
  return completed.stdout.trim().split(/\r?\n/).at(-1) ?? "";
}

async function appendLog(name, value) {
  await mkdir(logs, { recursive: true });
  await writeFile(join(logs, name), value, { flag: "a" });
}

function sanitize(value) {
  return String(value)
    .replaceAll(root, "%WORKSPACE%")
    .replace(/#connect=[a-f0-9]+/gi, "#connect=[redacted]")
    .replace(/(dashboard\.secret|Cookie|Set-Cookie)\s*[:=]\s*[^\s]+/gi, "$1=[redacted]");
}

async function saveResult() {
  await writeFile(output, JSON.stringify(result, null, 2));
}

async function freePort() {
  const server = createServer();
  await new Promise((resolveListen, reject) => server.once("error", reject).listen(0, "127.0.0.1", resolveListen));
  const address = server.address();
  if (!address || typeof address === "string") throw new Error("Could not reserve the isolated C06 port.");
  await new Promise((resolveClose, reject) => server.close((error) => error ? reject(error) : resolveClose()));
  return address.port;
}

async function portReleased(port, timeoutMs = 5000) {
  const deadline = Date.now() + timeoutMs;
  let lastError = null, attempts = 0;
  while (Date.now() < deadline) {
    attempts++;
    const server = createServer();
    try {
      await new Promise((resolveListen, reject) => server.once("error", reject).listen(port, "127.0.0.1", resolveListen));
      await new Promise((resolveClose, reject) => server.close((error) => error ? reject(error) : resolveClose()));
      return { released: true, attempts, lastError: null };
    } catch (error) {
      lastError = error?.code ?? error?.name ?? "unknown";
      await new Promise((resolveWait) => setTimeout(resolveWait, 100));
    }
  }
  return { released: false, attempts, lastError };
}

async function waitFor(predicate, label, timeoutMs = 10_000, intervalMs = 50) {
  const deadline = Date.now() + timeoutMs;
  let last = null;
  while (Date.now() < deadline) {
    if (controller.signal.aborted) throw new Error(`Cancelled while waiting for ${label}.`);
    try { last = await predicate(); if (last) return last; } catch { /* bounded readiness loop */ }
    await delay(intervalMs);
  }
  throw new Error(`Timed out waiting for ${label}.`);
}

function createTransportTracker() {
  const accepted = [];
  return {
    accepted,
    latestVector: null,
    latestCheckpointSequence: -1,
    updateVector(next) {
      if (!next) return;
      if (!this.latestVector || this.latestVector.instance !== next.instance) {
        this.latestVector = next;
        this.latestCheckpointSequence = Number.isFinite(next.checkpoint) ? next.checkpoint : -1;
        return;
      }
      this.latestVector = { ...next, catalog: Math.max(this.latestVector.catalog, next.catalog), documents: Math.max(this.latestVector.documents, next.documents), journal: Math.max(this.latestVector.journal, next.journal), digests: Math.max(this.latestVector.digests, next.digests), reads: Math.max(this.latestVector.reads, next.reads) };
      if (Number.isFinite(next.checkpoint)) this.latestCheckpointSequence = Math.max(this.latestCheckpointSequence, next.checkpoint);
    },
    note(message, generationId = null) {
      if (typeof message !== "string") return;
      for (const record of message.split("\u001e")) {
        if (!record) continue;
        if (record === "{}") accepted.push({ atUtc: new Date().toISOString(), monotonicMs: performance.now(), generationId, kind: "signalr-handshake-accepted" });
        else {
          try {
            const item = JSON.parse(record);
            if (item.type === 1 && item.target === "Revision") {
              this.updateVector(item.arguments?.[0]);
              accepted.push({ atUtc: new Date().toISOString(), monotonicMs: performance.now(), kind: "revision-frame", vector: this.latestVector });
            }
            if (item.type === 3 && item.result?.instance) {
              this.updateVector(item.result);
              accepted.push({ atUtc: new Date().toISOString(), monotonicMs: performance.now(), kind: "snapshot-result", vector: this.latestVector });
            }
          } catch { /* Ignore non-JSON or control frames. */ }
        }
      }
    },
  };
}

function wireRoute(page) {
  return page.routeWebSocket("**/api/live**", (route) => {
    const upstream = route.connectToServer();
    upstream.onMessage((message) => {
      if (typeof message !== "string") { route.send(message); return; }
      const records = message.split("\u001e");
      const outgoing = [];
      for (const record of records) {
        if (!record) continue;
        if (record === "{}") { outgoing.push(record); continue; }
        let item;
        try { item = JSON.parse(record); } catch { outgoing.push(record); continue; }
        if (item.type === 1 && item.target === "Revision") {
          routeMetrics.revisions++;
          const vector = item.arguments?.[0];
          const generation = vector?.[activeWireFault?.revisionKey];
          const targetGenerationAdvanced = activeWireFault && Number.isFinite(generation) && generation > activeWireFault.baselineGeneration;
          const injection = activeWireFault ? planRevisionInjection({
            kind: activeWireFault.kind,
            baselineGeneration: activeWireFault.baselineGeneration,
            generation,
            handled: activeWireFault.handled,
            heldGeneration: activeWireFault.held?.generation ?? null,
            lossOpen: activeWireFault.lossOpen,
            lossGeneration: activeWireFault.droppedGeneration,
            checkpointSequence: vector?.checkpoint,
            baselineCheckpointSequence: activeWireFault.baselineCheckpointSequence,
          }) : { action: "pass" };
          if (activeWireFault) {
            if (targetGenerationAdvanced) routeMetrics.targetDomainAdvancedFrames++;
            else routeMetrics.targetDomainNonAdvancedFrames++;
          }
          const lossFrame = { generation, checkpointSequence: Number.isFinite(vector?.checkpoint) ? vector.checkpoint : null,
            dirty: Array.isArray(vector?.dirty) ? vector.dirty : [], atMonotonicMs: performance.now(), action: injection.action };
          if (activeWireFault?.kind === "drop" && (activeWireFault.lossOpen || injection.action === "drop"))
            routeMetrics.lossWindowFrames.push(lossFrame);
          if (injection.action === "drop") {
            activeWireFault.handled = true;
            activeWireFault.lossOpen = true;
            activeWireFault.droppedGeneration = generation;
            activeWireFault.droppedAtMonotonicMs = performance.now();
            routeMetrics.dropped++;
            continue;
          }
          if (injection.action === "suppress") {
            routeMetrics.suppressedOrdinaryOrStaleFrames++;
            continue;
          }
          if (injection.action === "recover") {
            if (!checkpointRecoversGeneration(vector, activeWireFault.revisionKey, activeWireFault.droppedGeneration, activeWireFault.baselineCheckpointSequence))
              throw new Error("A lost-generation window can close only on a newer tagged checkpoint carrying the dropped generation.");
            activeWireFault.lossOpen = false;
            activeWireFault.lossCompleted = true;
            activeWireFault.checkpointRecoveryCandidate ??= { sequence: injection.checkpointSequence, generation, observedByProxyAtMonotonicMs: performance.now() };
            routeMetrics.checkpointRecoveryFrame = lossFrame;
          }
          if (injection.action === "duplicate") {
            activeWireFault.handled = true; activeWireFault.targetGeneration = generation; routeMetrics.duplicated++; outgoing.push(record, record); continue;
          }
          if (injection.action === "hold") {
            activeWireFault.held = { record, generation, repeats: [] };
            routeMetrics.held = recordShape(item);
            continue;
          }
          if (activeWireFault?.kind === "reorder" && !activeWireFault.handled && activeWireFault.held &&
              targetGenerationAdvanced && generation === activeWireFault.held.generation) {
            activeWireFault.held.repeats.push(record);
            routeMetrics.heldRepeatedFrames = activeWireFault.held.repeats.length;
            continue;
          }
          if (injection.action === "reorder") {
              activeWireFault.handled = true; routeMetrics.reordered++;
              activeWireFault.reorderedGenerations = injection.generations;
              outgoing.push(record, activeWireFault.held.record, ...activeWireFault.held.repeats);
              activeWireFault.held = null; routeMetrics.held = null; continue;
          }
        }
        if (item.type === 3 && item.result?.instance) {
          routeMetrics.snapshots++;
        }
        outgoing.push(record);
      }
      if (records.at(-1) === "" && outgoing.length) route.send(outgoing.join("\u001e") + "\u001e");
      else if (outgoing.length) route.send(outgoing.join("\u001e"));
    });
  });
}

function recordShape(message) {
  const vector = message?.arguments?.[0];
  return vector ? { target: message.target, instance: vector.instance, catalog: vector.catalog, documents: vector.documents, journal: vector.journal, digests: vector.digests, reads: vector.reads, dirty: vector.dirty } : { target: message?.target };
}

async function renameWithRetry(from, to) {
  for (let attempt = 0; ; attempt++) {
    try { return await rename(from, to); }
    catch (error) {
      // Windows denies a replace while the host holds the target open; retry briefly.
      if (!(["EPERM", "EBUSY", "EACCES"].includes(error?.code)) || attempt >= 100) throw error;
      await new Promise((resolveWait) => setTimeout(resolveWait, 10));
    }
  }
}

async function sendControl(command, payload = {}) {
  const id = ++sequence;
  const target = join(controls, "command.json"), temp = target + ".tmp";
  await writeFile(temp, JSON.stringify({ id, command, payload }));
  await renameWithRetry(temp, target);
  const ackPath = join(controls, "ack.json");
  return await waitFor(async () => {
    try {
      const ack = JSON.parse(await readFile(ackPath, "utf8"));
      return ack.commandId === id ? ack : null;
    } catch { return null; }
  }, `C06 host command ${command} ${id}`, 10_000, 20);
}

async function sendCleanupControl(command, payload = {}, timeoutMs = 3000) {
  const id = ++sequence;
  const target = join(controls, "command.json"), temp = target + ".tmp";
  await writeFile(temp, JSON.stringify({ id, command, payload }));
  await renameWithRetry(temp, target);
  const ackPath = join(controls, "ack.json");
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const ack = JSON.parse(await readFile(ackPath, "utf8"));
      if (ack.commandId === id) return ack;
    } catch { /* Continue the bounded cleanup wait. */ }
    await new Promise((resolveWait) => setTimeout(resolveWait, 25));
  }
  throw new Error(`Timed out during owned host cleanup (${command}).`);
}

async function startHost(port, watcherMode = "fake", { injectReadinessFailure = false } = {}) {
  const base = `http://127.0.0.1:${port}`;
  const waitForHealth = (ownedHost) => waitFor(async () => {
    if (childHasExited(ownedHost.child)) throw new Error("C06 host wrapper exited during startup.");
    try {
      const response = await fetch(base + "/health", { signal: AbortSignal.timeout(1500) });
      return response.ok ? await response.json() : null;
    } catch { return null; }
  }, "C06 host readiness", 30_000, 100);
  const waitForRoots = () => waitFor(async () => {
    try { const value = JSON.parse(await readFile(join(controls, "ready.json"), "utf8")); return value.port === port && value.logicalWatchRootCount > 0 ? value : null; }
    catch { return null; }
  }, "host-derived watch roots", 15_000, 25);
  return await startTrackedHost({
    spawn: () => {
      const payload = [smokeDll, "--c06-live-host", join(fixture, "data"), join(fixture, "repositories", "c06-1"), String(port), controls, watcherMode];
      return spawnOwned("dotnet", payload);
    },
    port,
    base,
    watcherMode,
    onOwned: (ownedHost) => { host = ownedHost; },
    waitForHealth: async (ownedHost) => {
      const health = await waitForHealth(ownedHost);
      if (injectReadinessFailure) {
        await waitForRoots();
        throw new Error("C06 deterministic startup-failure probe after host readiness.");
      }
      return health;
    },
    waitForRoots,
  });
}

async function stopHost(currentHost) {
  return await stopTrackedHost(currentHost, {
    childHasExited,
    sendStop: () => sendCleanupControl("stop"),
    killChild: (child) => child.kill(),
    waitChildExit,
    readReady: async () => JSON.parse(await readFile(join(controls, "ready.json"), "utf8")),
    readFinal: async () => JSON.parse(await readFile(join(controls, "final.json"), "utf8")),
    portReleased,
    sanitize,
  });
}

async function waitChildExit(child, timeoutMs) {
  if (childHasExited(child)) return true;
  return Promise.race([
    new Promise((resolveExit) => child.once("exit", () => resolveExit(true))),
    new Promise((resolveTimeout) => setTimeout(() => resolveTimeout(false), timeoutMs)),
  ]);
}

async function authorizeContext(context, page, currentHost, dataDirectory, navigate = true) {
  const secret = await readFile(join(dataDirectory, "dashboard.secret"));
  const signature = createHmac("sha256", secret).update("launch:" + currentHost.health.instance).digest("hex");
  if (navigate) {
    const navigationStarted = performance.now();
    const launch = await fetch(currentHost.base + "/api/launch", { method: "POST", headers: { "X-WorkLog-Launch": signature, Origin: currentHost.base } });
    if (!launch.ok) throw new Error(`Fixture authorization launch failed (${launch.status}).`);
    const { token } = await launch.json();
    await page.goto(currentHost.base + "/ongoing#connect=" + token, { timeout: 30_000 });
    await page.getByRole("heading", { name: "Ongoing board" }).waitFor({ timeout: 30_000 });
    result.browserDiagnostics.push({ kind: "initial-page-rendered", label: page === pageA || page === pageATarget ? "browserA" : "browserB", monotonicMs: performance.now(), elapsedMs: Math.round(performance.now() - navigationStarted) });
  } else {
    const launch = await context.request.post(currentHost.base + "/api/launch", { headers: { "X-WorkLog-Launch": signature, "Origin": currentHost.base } });
    if (!launch.ok()) throw new Error(`Fixture authorization launch failed (${launch.status()}).`);
    const { token } = await launch.json();
    const bootstrap = await context.request.post(currentHost.base + "/api/bootstrap", { data: { token }, headers: { Origin: currentHost.base } });
    if (!bootstrap.ok()) throw new Error(`Fixture authorization bootstrap failed (${bootstrap.status()}).`);
  }
  await page.locator(".live-status").waitFor({ state: "visible", timeout: 30_000 });
  if (await page.locator(".live-status[data-state='paused']").count())
    // The modal reader covers the header status; its own Retry control is the reachable one while it is open.
    await page.locator(".reader-live, .live-status").and(page.locator(":visible")).last().getByRole("button", { name: "Retry updates" }).click();
  await waitForLive(page, 30_000);
  secret.fill(0);
}

async function waitForLive(page, timeoutMs = 10_000) {
  try {
    await page.locator(".live-status[data-state='live']").waitFor({ state: "visible", timeout: timeoutMs });
  } catch (error) {
    const detail = await page.locator(".live-status").evaluateAll(nodes => nodes.map(node => ({ state: node.getAttribute("data-state"), text: (node.textContent ?? "").trim() })).slice(0, 3)).catch(() => []);
    result.browserDiagnostics.push({ kind: "live-status-timeout", urlPath: new URL(page.url()).pathname, detail });
    throw error;
  }
}

async function observeBrowser(page, label) {
  await page.exposeFunction("__c06ReportStatus", (transition) => {
    if (result.statusTransitions.length < 10_000)
      result.statusTransitions.push({ ...transition, label, cycle: activeCycle, atUtc: new Date().toISOString(), monotonicMs: performance.now() });
  });
  await page.addInitScript(() => {
    const attach = () => {
      let previous = "";
      const report = () => {
        const node = document.querySelector(".live-status");
        if (!node) return;
        const current = `${node.getAttribute("data-state") ?? "unknown"}\n${(node.textContent ?? "").trim()}`;
        if (current === previous) return;
        previous = current;
        void window["__c06ReportStatus"]?.({ state: node.getAttribute("data-state") ?? "unknown", text: (node.textContent ?? "").trim() });
      };
      const observer = new MutationObserver(report);
      observer.observe(document.documentElement, { childList: true, subtree: true, attributes: true, characterData: true });
      report();
    };
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", attach, { once: true });
    else attach();
  });
  page.on("request", request => {
    const pathname = new URL(request.url()).pathname;
    if (pathname.startsWith("/api/") && result.browserDiagnostics.length < 5000)
      result.browserDiagnostics.push({ kind: pathname.startsWith("/api/live") ? "live-request" : "api-request", label, method: request.method(), path: pathname, atUtc: new Date().toISOString(), monotonicMs: performance.now() });
  });
  page.on("console", message => {
    if (message.type() === "error" || message.type() === "warning") result.browserDiagnostics.push({ kind: `console-${message.type()}`, label, message: sanitize(message.text()).slice(0, 300) });
  });
  page.on("pageerror", error => result.browserDiagnostics.push({ kind: "page-error", label, message: sanitize(error.message).slice(0, 300) }));
  page.on("requestfailed", request => result.browserDiagnostics.push({ kind: "request-failed", label, method: request.method(), path: new URL(request.url()).pathname, failure: sanitize(request.failure()?.errorText ?? "unknown").slice(0, 200) }));
  page.on("response", response => {
    const pathname = new URL(response.url()).pathname;
    if (pathname.startsWith("/api/") && result.browserDiagnostics.length < 5000)
      result.browserDiagnostics.push({ kind: pathname.startsWith("/api/live") ? "live-response" : response.status() >= 400 ? "http-error" : "api-response", label, method: response.request().method(), path: pathname, status: response.status(), atUtc: new Date().toISOString(), monotonicMs: performance.now() });
  });
  page.on("websocket", socket => {
    if (!socket.url().includes("/api/live")) return;
    const connectionId = randomUUID();
    const routedGeneration = disconnectControl?.attachPageSocket(label, socket) ?? null;
    const openedMonotonicMs = performance.now();
    result.connectionLifecycle.push({ event: "open", connectionId, generationId: routedGeneration?.id ?? null, label, cycle: activeCycle, phase: activeCycle === null ? "preparation" : activeFaultStartMonotonicMs !== null && openedMonotonicMs >= activeFaultStartMonotonicMs ? "fault" : "cycle-preparation", urlPath: "/api/live", atUtc: new Date().toISOString(), monotonicMs: openedMonotonicMs, closeCode: null, closeReason: null });
    socket.on("framereceived", frame => {
      const payload = typeof frame.payload === "string" ? frame.payload : "binary";
      if (payload === "{}\u001e" || payload.includes("\"target\":\"Revision\"") || payload.includes("\"result\":{\"instance\""))
        result.browserDiagnostics.push({ kind: "live-websocket-frame", label, frame: payload.slice(0, 120), atUtc: new Date().toISOString() });
    });
    socket.on("close", () => {
      const closedMonotonicMs = performance.now();
      result.connectionLifecycle.push({ event: "close", connectionId, generationId: routedGeneration?.id ?? null, label, cycle: activeCycle, phase: activeCycle === null ? "preparation" : activeFaultStartMonotonicMs !== null && closedMonotonicMs >= activeFaultStartMonotonicMs ? "fault" : "cycle-preparation", urlPath: "/api/live", atUtc: new Date().toISOString(), monotonicMs: closedMonotonicMs, closeCode: null, closeReason: null, closeDetailsAvailable: false, limitation: "Playwright WebSocket close event exposes no close code or reason." });
    });
  });
}

async function openDocument(page, base) {
  await page.goto(base + "/ongoing", { timeout: 30_000 });
  await page.getByRole("textbox", { name: "Search OpenSpec documents" }).fill("C06_TARGET_DOCUMENT");
  await page.locator(".search-result").first().waitFor({ timeout: 20_000 });
  await page.locator(".search-result").first().click();
  await page.getByRole("dialog", { name: "OpenSpec document reader" }).waitFor({ timeout: 15_000 });
  await page.locator(".markdown").getByText("C06_TARGET_DOCUMENT", { exact: false }).waitFor({ timeout: 15_000 });
  await page.locator(".markdown [data-start]").first().click();
  await page.getByRole("textbox", { name: "Review comment" }).waitFor({ timeout: 5000 });
}

async function navigateTarget(page, currentHost, domain) {
  if (domain === "Documents") {
    await openDocument(page, currentHost.base);
  } else if (domain === "Journal") {
    await page.goto(currentHost.base + "/history", { timeout: 30_000 });
    await page.getByRole("heading", { name: "Work over time" }).waitFor({ timeout: 15_000 });
  } else {
    await page.goto(currentHost.base + "/daily", { timeout: 30_000 });
    await page.getByRole("heading", { name: "Daily", exact: true }).waitFor({ timeout: 15_000 });
  }
  await waitForLive(page, 20_000);
}

async function waitForTarget(page, domain, value, timeoutMs = 5_000) {
  if (domain === "Documents") await waitFor(() => page.locator(".document-scroll .markdown, .source-text").first().evaluate(node => node.textContent?.includes(value) ?? false).catch(() => false), `document marker ${value}`, timeoutMs, 25);
  else if (domain === "Journal") await page.locator(".timeline-row").filter({ hasText: value }).first().waitFor({ state: "visible", timeout: timeoutMs });
  else await page.getByTestId("daily-today-summary").filter({ hasText: value }).waitFor({ state: "visible", timeout: timeoutMs });
}

async function waitForTargetAndLive(page, domain, value, timeoutMs = 5_000) {
  const state = await waitFor(async () => page.evaluate(({ targetDomain, marker }) => {
    const statuses = Array.from(document.querySelectorAll(".live-status"));
    const visible = node => {
      const box = node.getBoundingClientRect();
      return box.width > 0 && box.height > 0 && getComputedStyle(node).visibility !== "hidden";
    };
    const live = statuses.length > 0 && statuses.every(node => node.getAttribute("data-state") === "live" && visible(node));
    let markerVisible = false;
    if (targetDomain === "Documents") {
      markerVisible = Array.from(document.querySelectorAll(".document-scroll .markdown, .source-text"))
        .some(node => (node.textContent ?? "").includes(marker) && visible(node));
    } else if (targetDomain === "Journal") {
      markerVisible = Array.from(document.querySelectorAll(".timeline-row"))
        .some(node => (node.textContent ?? "").includes(marker) && visible(node));
    } else {
      markerVisible = Array.from(document.querySelectorAll('[data-testid="daily-today-summary"]'))
        .some(node => (node.textContent ?? "").includes(marker) && visible(node));
    }
    return { markerVisible, live };
  }, { targetDomain: domain, marker: value }).then(sample => isTargetAndLive(sample) ? sample : null), `marker and Live together for ${value}`, timeoutMs, 25);
  return { ...state, atUtc: new Date().toISOString(), monotonicMs: performance.now() };
}

async function assertMarkerAbsent(page, marker) {
  const text = await page.locator("body").innerText();
  if (text.includes(marker)) throw new Error(`C06 marker was visible before its independent writer commit (${marker}).`);
}

async function writeExternal(kind, cycle, metadata, marker = `C06-CYCLE-${String(cycle).padStart(3, "0")}-${randomUUID().slice(0, 8)}`) {
  await Promise.all([assertMarkerAbsent(pageATarget, marker), assertMarkerAbsent(pageB, marker), ...(kind === "Documents" ? [assertMarkerAbsent(pageA, marker)] : [])]);
  if (kind === "Documents") {
    const file = join(metadata.repositories[0].path, "openspec", "changes", "c06-active-1", "design.md");
    const contents = `# C06 Active Design\n\nC06_TARGET_DOCUMENT\n${marker}\n` + "x".repeat(100 * 1024 - 80);
    const temp = file + ".c06-replacement";
    await writeFile(temp, contents, "utf8");
    await rename(temp, file);
    return { marker, precommitMarkerAbsentInPages: true, committedAtUtc: new Date().toISOString(), commitAcknowledgedMonotonicMs: performance.now(), process: "independent atomic file writer" };
  }
  const repository = metadata.repositories[0].id;
  const args = kind === "Journal"
    ? [smokeDll, "--c06-log", fixture, repository, "c06-active-1", marker, marker]
    : [smokeDll, "--c06-digest-save", fixture, repository, metadata.dailyDigestTickets[repository], marker];
  const ack = JSON.parse(runOwned("dotnet", args, 60_000));
  return { ...ack, marker, precommitMarkerAbsentInPages: true, commitAcknowledgedMonotonicMs: performance.now(), process: "independent fixture writer process" };
}

async function writeSecond(kind, cycle, metadata, marker = `C06-CYCLE-${String(cycle).padStart(3, "0")}-FOLLOWUP-${randomUUID().slice(0, 8)}`) {
  await Promise.all([assertMarkerAbsent(pageATarget, marker), assertMarkerAbsent(pageB, marker), ...(kind === "Documents" ? [assertMarkerAbsent(pageA, marker)] : [])]);
  const repository = metadata.repositories[0].id;
  if (kind === "Documents") {
    const file = join(metadata.repositories[0].path, "openspec", "changes", "c06-active-1", "design.md");
    const contents = `# C06 Active Design\n\nC06_TARGET_DOCUMENT\n${marker}\n` + "y".repeat(100 * 1024 - 80);
    const temp = file + ".c06-replacement";
    await writeFile(temp, contents, "utf8");
    await rename(temp, file);
    return { marker, committedAtUtc: new Date().toISOString(), commitAcknowledgedMonotonicMs: performance.now(), process: "independent atomic file writer" };
  }
  const parameters = kind === "Journal"
    ? [smokeDll, "--c06-log", fixture, repository, "c06-active-1", marker, marker]
    : [smokeDll, "--c06-digest-save", fixture, repository, metadata.dailyDigestTickets[repository], marker];
  return { ...JSON.parse(runOwned("dotnet", parameters, 60_000)), marker, commitAcknowledgedMonotonicMs: performance.now(), process: "independent fixture writer process" };
}

function chooseCategory(cycle) {
  if (cycle <= 25) return "disconnect";
  if (cycle <= 50) return "revisionWire";
  if (cycle <= 70) return "serverRestart";
  if (cycle <= 85) return "watcherGapOverflow";
  return "secretRevokeReauthorize";
}

function chooseWireKind(cycle) {
  const offset = cycle - 25;
  if (offset <= 9) return "drop";
  if (offset <= 17) return "duplicate";
  return "reorder";
}

function chooseWatcherKind(cycle) {
  const offset = cycle - 70;
  if (offset <= 5) return "gap";
  if (offset <= 10) return "overflow";
  return "failure";
}

async function prepareVisibleState(domain, metadata) {
  await Promise.all([navigateTarget(pageATarget, host, domain), navigateTarget(pageB, host, domain)]);
  const draftBox = pageA.getByRole("textbox", { name: "Review comment" });
  const draft = result.initialDraft?.value;
  if (!draft || await draftBox.inputValue() !== draft)
    throw new Error("The initial reader draft was not present before fault injection.");
  await pageA.evaluate(() => {
    const scroll = document.querySelector(".document-scroll");
    if (scroll instanceof HTMLElement) scroll.scrollTop = Math.min(300, scroll.scrollHeight - scroll.clientHeight);
  });
  readerContextBefore = await pageA.evaluate(() => {
    const editor = document.querySelector('[aria-label="Review comment"]');
    const scroll = document.querySelector(".document-scroll");
    return {
      focusedReviewComment: document.activeElement === editor,
      selectionStart: editor instanceof HTMLTextAreaElement ? editor.selectionStart : null,
      selectionEnd: editor instanceof HTMLTextAreaElement ? editor.selectionEnd : null,
      readerScrollTop: scroll instanceof HTMLElement ? scroll.scrollTop : null,
      documentTitle: document.querySelector(".reader-header h2")?.textContent?.trim() ?? null,
      documentProvenance: document.querySelector(".reader-provenance")?.textContent?.trim() ?? null,
      renderedMode: document.querySelector('.document-mode button[aria-pressed="true"]')?.textContent?.trim() ?? null,
    };
  });
  return draft;
}

async function measureConnectedAt(context, tracker, beforeIndex, generationId = null, releaseMonotonicMs = null) {
  const entry = await waitFor(() => generationId && Number.isFinite(releaseMonotonicMs)
    ? acceptedHandshakeAfterRelease(tracker.accepted, generationId, releaseMonotonicMs, beforeIndex)
    : tracker.accepted.slice(beforeIndex).reverse().find(item => item.kind === "signalr-handshake-accepted") ?? null,
  generationId ? `post-release SignalR handshake for ${generationId}` : "new SignalR handshake acknowledgement", 15_000, 25);
  return { atUtc: entry.atUtc, monotonicMs: entry.monotonicMs, kind: entry.kind, generationId: entry.generationId ?? null };
}

async function requestCounts(page) {
  return await page.evaluate(() => ({
    statusCount: document.querySelectorAll(".live-status").length,
    alertCount: document.querySelectorAll('[role="alert"]').length,
    statusText: Array.from(document.querySelectorAll(".live-status")).map(node => (node.textContent ?? "").trim()).join(" | "),
  }));
}

async function cycleCommonStart(cycle, domain, category) {
  await prepareVisibleState(domain, metadata);
  const rootStatus = await sendControl("status");
  const beforeA = transportA.accepted.length, beforeB = transportB.accepted.length;
  const initial = await requestCounts(pageB);
  const record = { cycle, category, domain, startedAtUtc: new Date().toISOString(), statusBefore: initial, sourceBefore: sourceCounters(rootStatus) };
  return { record, beforeA, beforeB };
}

function sourceCounters(status) {
  return {
    revision: status.snapshot,
    activeConnections: status.activeConnectionCount,
    logicalWatchRootCount: status.logicalWatchRootCount ?? null,
    activeOsWatcherCount: status.activeWatcherCount,
    pendingHints: status.pendingHintCount,
    maxPendingHints: status.maxPendingHintCount ?? status.source?.maxObservedPendingHintCount ?? 0,
    overflows: status.overflowBatchCount ?? 0,
    processedBatches: status.processedBatchCount ?? 0,
    lastBatchHintHighWater: status.lastBatchHintHighWater ?? 0,
    lastBatchFirstDelayMilliseconds: status.lastBatchFirstDelayMilliseconds ?? null,
    lastBatchQuietDelayMilliseconds: status.lastBatchQuietDelayMilliseconds ?? null,
    lastBatchOverflow: status.lastBatchOverflow ?? false,
    maxPendingNotifications: status.maxPendingNotificationCount ?? 0,
    checkpointCount: status.checkpointCount,
  };
}

async function emitHint(domain, metadata, extra = {}) {
  const status = await sendControl("status");
  const root = status.watchRoots?.find(item => item.domains?.includes(domain.toLowerCase())) ?? null;
  return sendControl("emit", { domain, rootId: root?.id ?? null, relativePath: null, ...extra });
}

async function waitForCoordinatorSettled() {
  return await waitFor(async () => {
    const current = await sendControl("status");
    return current.pendingHintCount === 0 ? current : null;
  }, "coordinator queues to drain", 10_000, 50);
}

async function assertCycleState(record, domain, expected, draft, transportA, transportB, transportAReader = null) {
  const targetStarted = performance.now();
  let visibleA, visibleB;
  try {
    [visibleA, visibleB] = await Promise.all([
      waitForTargetAndLive(pageATarget, domain, expected, 5_000),
      waitForTargetAndLive(pageB, domain, expected, 5_000),
    ]);
  } catch (error) {
    if (domain === "Journal") result.assertions.lostSavedObservations++;
    throw error;
  }
  const readerVisible = await pageA.locator(".reader").isVisible();
  if (!readerVisible) throw new Error(`Cycle ${record.cycle} closed the first browser's document reader.`);
  const draftAfter = await pageA.getByRole("textbox", { name: "Review comment" }).inputValue();
  if (draftAfter !== draft) {
    result.assertions.discardedDrafts++;
    throw new Error(`Cycle ${record.cycle} discarded the unsaved reader draft.`);
  }
  let readerDocumentVisible = null;
  if (domain === "Documents") {
    readerDocumentVisible = await waitForTargetAndLive(pageA, domain, expected, 5_000);
  } else {
    await waitForLive(pageA, 5_000);
  }
  const readerContextAfter = await pageA.evaluate(() => {
    const editor = document.querySelector('[aria-label="Review comment"]');
    const scroll = document.querySelector(".document-scroll");
    return {
      focusedReviewComment: document.activeElement === editor,
      selectionStart: editor instanceof HTMLTextAreaElement ? editor.selectionStart : null,
      selectionEnd: editor instanceof HTMLTextAreaElement ? editor.selectionEnd : null,
      readerScrollTop: scroll instanceof HTMLElement ? scroll.scrollTop : null,
      documentTitle: document.querySelector(".reader-header h2")?.textContent?.trim() ?? null,
      documentProvenance: document.querySelector(".reader-provenance")?.textContent?.trim() ?? null,
      renderedMode: document.querySelector('.document-mode button[aria-pressed="true"]')?.textContent?.trim() ?? null,
    };
  });
  if (readerContextBefore?.focusedReviewComment && !readerContextAfter.focusedReviewComment)
    throw new Error(`Cycle ${record.cycle} moved focus away from the active review draft.`);
  if (readerContextBefore?.selectionStart !== readerContextAfter.selectionStart || readerContextBefore?.selectionEnd !== readerContextAfter.selectionEnd)
    throw new Error(`Cycle ${record.cycle} moved the caret inside the active review draft.`);
  if (readerContextBefore?.documentTitle !== readerContextAfter.documentTitle)
    throw new Error(`Cycle ${record.cycle} changed the selected document in the reader.`);
  if (readerContextBefore?.documentProvenance !== readerContextAfter.documentProvenance || readerContextBefore?.renderedMode !== readerContextAfter.renderedMode)
    throw new Error(`Cycle ${record.cycle} changed the reader's source identity or rendered/source mode.`);
  if (readerContextBefore?.readerScrollTop !== null && readerContextAfter.readerScrollTop !== null && Math.abs(readerContextAfter.readerScrollTop - readerContextBefore.readerScrollTop) > 32)
    throw new Error(`Cycle ${record.cycle} moved the reader position by more than 32 pixels.`);
  const statusAfter = {
    browserAReader: await requestCounts(pageA),
    browserATarget: await requestCounts(pageATarget),
    browserBTarget: await requestCounts(pageB),
  };
  const initialStatus = result.initialBrowserStatus;
  if ([statusAfter.browserAReader, statusAfter.browserATarget, statusAfter.browserBTarget].some((status, index) =>
    status.statusCount !== 1 || status.alertCount > [initialStatus.browserA, initialStatus.browserATarget, initialStatus.browserB][index].alertCount)) {
    result.assertions.accumulatedNotices++;
    throw new Error(`Cycle ${record.cycle} accumulated duplicate live notices.`);
  }
  const current = await waitForCoordinatorSettled();
  if (current.maxPendingHintCount > 128 || current.pendingHintCount > 128) {
    result.assertions.maxPendingHintCount = Math.max(result.assertions.maxPendingHintCount, current.maxPendingHintCount);
    throw new Error(`Cycle ${record.cycle} exceeded the 128-hint coordinator bound.`);
  }
  if ((current.maxPendingNotifications ?? 0) > 3)
    throw new Error(`Cycle ${record.cycle} exceeded the one-notification-per-connection delivery bound for three owned tabs.`);
  result.assertions.maxPendingHintCount = Math.max(result.assertions.maxPendingHintCount, current.maxPendingHintCount ?? 0);
  result.assertions.maxPendingNotifications = Math.max(result.assertions.maxPendingNotifications, current.maxPendingNotifications ?? 0);
  result.assertions.maxOverflowBatchCount = Math.max(result.assertions.maxOverflowBatchCount, current.overflowBatchCount ?? 0);
  record.expectedMarker = expected;
  record.visibleReconciledAtUtc = { browserA: visibleA.atUtc, browserB: visibleB.atUtc };
  const sourceWrite = record.followupWrite ?? record.write;
  record.commitToBrowserAVisibleMs = Math.round(visibleA.monotonicMs - sourceWrite.commitAcknowledgedMonotonicMs);
  record.commitToBrowserBVisibleMs = Math.round(visibleB.monotonicMs - sourceWrite.commitAcknowledgedMonotonicMs);
  if (record.wireFault === "drop" || record.watcherFault === "gap") {
    // Drop/gap recovery waits for the next 5 s checkpoint, so measure from the fault (wire drop time, or gap injection start), not from the writer ack.
    const faultOriginMonotonicMs = record.wireFault === "drop" ? record.wireDroppedAtMonotonicMs : record.faultStartMonotonicMs;
    if (!Number.isFinite(faultOriginMonotonicMs)) throw new Error(`Cycle ${record.cycle} has no recorded fault time for the checkpoint recovery gate.`);
    record.dropToBrowserAVisibleMs = Math.round(visibleA.monotonicMs - faultOriginMonotonicMs);
    record.dropToBrowserBVisibleMs = Math.round(visibleB.monotonicMs - faultOriginMonotonicMs);
    if (Math.max(record.dropToBrowserAVisibleMs, record.dropToBrowserBVisibleMs) > CHECKPOINT_RECOVERY_BUDGET_MS)
      throw new Error(`Cycle ${record.cycle} checkpoint recovery exceeded ${CHECKPOINT_RECOVERY_BUDGET_MS} ms after the fault.`);
  }
  record.reconcileLatencyMs = { browserA: record.commitToBrowserAVisibleMs, browserB: record.commitToBrowserBVisibleMs };
  const recoveryA = transportA ? monotonicElapsedMs(transportA.monotonicMs, visibleA.monotonicMs) : null;
  const recoveryB = transportB ? monotonicElapsedMs(transportB.monotonicMs, visibleB.monotonicMs) : null;
  const recoveryReader = transportAReader && readerDocumentVisible ? monotonicElapsedMs(transportAReader.monotonicMs, readerDocumentVisible.monotonicMs) : null;
  if (recoveryA !== null && recoveryA > 5000) throw new Error(`Cycle ${record.cycle} browser A target recovery exceeded five seconds after transport re-establishment.`);
  if (recoveryB !== null && recoveryB > 5000) throw new Error(`Cycle ${record.cycle} browser B target recovery exceeded five seconds after transport re-establishment.`);
  if (recoveryReader !== null && recoveryReader > 5000) throw new Error(`Cycle ${record.cycle} browser A reader recovery exceeded five seconds after transport re-establishment.`);
  record.browserA = { transportReestablishedAtUtc: transportA?.atUtc ?? null, transportReestablishedMonotonicMs: transportA?.monotonicMs ?? null, reconciledAtUtc: visibleA.atUtc, reconciledMonotonicMs: visibleA.monotonicMs, reestablishedToReconciledMs: recoveryA, status: "Live", visibleTarget: true, targetTab: "browserA-target", readerTab: "browserA-reader", readerTransportReestablishedAtUtc: transportAReader?.atUtc ?? null, readerTransportReestablishedMonotonicMs: transportAReader?.monotonicMs ?? null, readerReconciledAtUtc: readerDocumentVisible?.atUtc ?? null, readerReconciledMonotonicMs: readerDocumentVisible?.monotonicMs ?? null, readerReestablishedToReconciledMs: recoveryReader, readerRetained: readerVisible, readerDocumentVisibleAt: readerDocumentVisible?.atUtc ?? null, commitToReaderVisibleMs: readerDocumentVisible ? Math.round(readerDocumentVisible.monotonicMs - sourceWrite.commitAcknowledgedMonotonicMs) : null, readerContextBefore, readerContextAfter, draftRetained: true };
  record.browserB = { transportReestablishedAtUtc: transportB?.atUtc ?? null, transportReestablishedMonotonicMs: transportB?.monotonicMs ?? null, reconciledAtUtc: visibleB.atUtc, reconciledMonotonicMs: visibleB.monotonicMs, reestablishedToReconciledMs: recoveryB, status: "Live", visibleTarget: true };
  record.statusAfter = statusAfter;
  record.coordinatorAfter = sourceCounters(current);
  record.elapsedFromTargetWaitStartMs = Math.round(performance.now() - targetStarted);
}

async function cycleDisconnect(cycle, domain, record, draft, metadata) {
  const beforeTarget = transportA.accepted.length, beforeReader = transportAReader.accepted.length;
  const beforeB = transportB.accepted.length;
  const baselineBGeneration = transportB.latestVector?.[revisionKeys[domain]];
  const bSocket = result.connectionLifecycle.findLast(item => item.event === "open" && item.label === "browserB-target");
  if (!bSocket || !Number.isFinite(baselineBGeneration)) throw new Error("Browser B lacks an identified live baseline socket for the disconnect isolation proof.");
  const pageLabels = ["browserA-reader", "browserA-target"];
  const closingPairs = disconnectControl.captureCurrent(pageLabels);
  const closedGenerationIds = Object.fromEntries(closingPairs.map(pair => [pair.label, pair.generationId]));
  const faultStart = { atUtc: new Date().toISOString(), monotonicMs: performance.now() };
  activeFaultStartMonotonicMs = faultStart.monotonicMs;
  record.faultStartAtUtc = faultStart.atUtc;
  record.faultStartMonotonicMs = faultStart.monotonicMs;
  disconnectControl.setHoldReplacements(true);
  await disconnectControl.closeCaptured(closingPairs);
  const [closedPairs, heldReplacements] = await Promise.all([
    waitFor(() => {
      try { return disconnectControl.requireClosed(closingPairs); }
      catch { return null; }
    }, "actual browser A socket and captured routed-pair close events", 5000, 25),
    waitFor(() => disconnectControl.heldCurrent(pageLabels, Object.values(closedGenerationIds)), "real browser A replacement handshakes held before outage write", 5000, 25),
  ]);
  await Promise.all([
    pageA.locator(".live-status[data-state='reconnecting'],.live-status[data-state='paused']").waitFor({ timeout: 5000 }),
    pageATarget.locator(".live-status[data-state='reconnecting'],.live-status[data-state='paused']").waitFor({ timeout: 5000 }),
  ]);
  record.outageConfirmedAtUtc = new Date().toISOString();
  record.outageConfirmedMonotonicMs = performance.now();
  record.browserASocketClosures = closedPairs;
  record.browserASocketCloseGenerationIds = closedGenerationIds;
  record.browserAHeldReplacementGenerations = heldReplacements;
  record.browserBConnectionIdDuringOutage = bSocket.connectionId;
  const first = await writeExternal(domain, cycle, metadata);
  record.write = first;
  record.writeCommittedDuringOutage = first.precommitMarkerAbsentInPages && first.commitAcknowledgedMonotonicMs >= record.outageConfirmedMonotonicMs;
  if (!record.writeCommittedDuringOutage) throw new Error(`Cycle ${cycle} write was not committed after its confirmed context outage.`);
  const bFrame = await waitFor(() => transportB.accepted.slice(beforeB).find(item => item.kind === "revision-frame" && item.vector?.[revisionKeys[domain]] > baselineBGeneration) ?? null,
    "a real revision frame on unchanged browser B socket during the A outage", 5000, 25);
  const bStillOpen = result.connectionLifecycle.some(item => item.event === "open" && item.connectionId === bSocket.connectionId) &&
    !result.connectionLifecycle.some(item => item.event === "close" && item.connectionId === bSocket.connectionId);
  if (!bStillOpen) throw new Error("Browser B's original socket did not remain open throughout the A-side outage.");
  record.browserBFrameDuringOutage = { kind: bFrame.kind, vector: bFrame.vector, connectionId: bSocket.connectionId, atUtc: bFrame.atUtc, monotonicMs: bFrame.monotonicMs };
  await delay(100);
  const heldBeforeRelease = disconnectControl.heldCurrent(pageLabels, Object.values(closedGenerationIds));
  if (!heldBeforeRelease) throw new Error("Browser A no longer owns both eligible held replacement handshakes at release.");
  const preReleaseAccepted = [
    ...transportA.accepted.slice(beforeTarget).filter(item => item.kind === "signalr-handshake-accepted" && item.monotonicMs >= faultStart.monotonicMs),
    ...transportAReader.accepted.slice(beforeReader).filter(item => item.kind === "signalr-handshake-accepted" && item.monotonicMs >= faultStart.monotonicMs),
  ];
  if (preReleaseAccepted.length) throw new Error("A Browser A SignalR handshake was accepted before the held reconnect gate was released.");
  record.browserAHandshakesBeforeRelease = 0;
  const releaseBoundaryMonotonicMs = performance.now();
  record.transportReleaseBoundaryMonotonicMs = releaseBoundaryMonotonicMs;
  const released = await disconnectControl.releaseHeld();
  const releasedIds = Object.fromEntries(released.map(item => [item.label, item.id]));
  if (pageLabels.some(label => releasedIds[label] !== heldBeforeRelease[label].id))
    throw new Error("The reconnect gate did not release the exact currently held Browser A generations.");
  const reopened = await waitFor(() => {
    const target = disconnectControl.currentSnapshot("browserA-target");
    const reader = disconnectControl.currentSnapshot("browserA-reader");
    if (!target?.socketOpened || target.socketClosed || target.id !== releasedIds["browserA-target"] ||
        !reader?.socketOpened || reader.socketClosed || reader.id !== releasedIds["browserA-reader"]) return null;
    return { target, reader };
  }, "the released browser A socket generations to open in their pages", 5000, 25);
  const [transport, readerTransport] = await Promise.all([
    measureConnectedAt(contextA, transportA, beforeTarget, reopened.target.id, releaseBoundaryMonotonicMs),
    measureConnectedAt(contextA, transportAReader, beforeReader, reopened.reader.id, releaseBoundaryMonotonicMs),
  ]);
  record.browserAReconnectedSocketGenerations = reopened;
  record.heldReplacementRelease = released;
  record.transportReestablishedAtUtc = transport.atUtc;
  record.transportReestablishedMonotonicMs = transport.monotonicMs;
  await assertCycleState(record, domain, first.marker, draft, transport, null, readerTransport);
}

async function cycleWire(cycle, domain, record, draft, metadata) {
  const kind = chooseWireKind(cycle);
  const before = transportB.accepted.length;
  const revisionKey = revisionKeys[domain];
  const baselineGeneration = transportB.latestVector?.[revisionKey];
  const baselineCheckpointSequence = transportB.latestCheckpointSequence;
  if (!Number.isFinite(baselineGeneration)) throw new Error(`Cycle ${cycle} has no authoritative ${domain} baseline revision for targeted wire injection.`);
  activeWireFault = { kind, domain, revisionKey, baselineGeneration, baselineCheckpointSequence, handled: false, held: null, lossOpen: false, lossCompleted: false, droppedGeneration: null };
  routeMetrics = { revisions: 0, snapshots: 0, targetDomainAdvancedFrames: 0, targetDomainNonAdvancedFrames: 0, dropped: 0,
    suppressedOrdinaryOrStaleFrames: 0, lossWindowFrames: [], checkpointRecoveryFrame: null, duplicated: 0, reordered: 0, held: null };
  record.wireFault = kind;
  record.wireTarget = { domain, revisionKey, baselineGeneration, baselineCheckpointSequence };
  record.faultStartAtUtc = new Date().toISOString();
  record.faultStartMonotonicMs = performance.now();
  const first = await writeExternal(domain, cycle, metadata);
  record.write = first;
  if (kind === "drop") {
    await emitHint(domain, metadata);
    await waitFor(() => activeWireFault.lossOpen, "selected advanced generation to enter its bounded wire-loss window", 5000, 20);
    if (routeMetrics.dropped !== 1) throw new Error(`Cycle ${cycle} did not drop exactly one targeted Revision frame.`);
    record.wireTarget.targetGeneration = activeWireFault.droppedGeneration;
    record.wireDroppedAtMonotonicMs = activeWireFault.droppedAtMonotonicMs;
    const candidate = await waitFor(() => activeWireFault.checkpointRecoveryCandidate ?? null,
      "tagged checkpoint vector carrying the selected generation", 10_500, 20);
    const deliveredCheckpoint = await waitFor(() => transportB.accepted.slice(before).find(item =>
      isCheckpointRecoveryDelivery(item, candidate.sequence, revisionKey, activeWireFault.droppedGeneration, baselineCheckpointSequence)) ?? null,
    "browser to receive the selected generation on its tagged checkpoint Revision", 1500, 20);
    routeMetrics.recoveredGeneration = deliveredCheckpoint.vector[revisionKey];
    routeMetrics.recoveryCheckpointSequence = deliveredCheckpoint.vector.checkpoint;
    routeMetrics.recoveredAtMonotonicMs = deliveredCheckpoint.monotonicMs;
    routeMetrics.checkpointReceivedByBrowser = true;
    if (!activeWireFault.lossCompleted || routeMetrics.recoveredGeneration < activeWireFault.droppedGeneration ||
        routeMetrics.recoveredGeneration !== candidate.generation ||
        routeMetrics.recoveryCheckpointSequence <= baselineCheckpointSequence)
      throw new Error(`Cycle ${cycle} did not receive a newer tagged checkpoint carrying the dropped target generation.`);
    record.wireMetrics = { ...routeMetrics };
  } else if (kind === "duplicate") {
    await emitHint(domain, metadata);
    await waitFor(() => activeWireFault.handled, "duplicated Revision frame", 5000, 20);
    if (routeMetrics.duplicated !== 1) throw new Error(`Cycle ${cycle} did not duplicate exactly one targeted Revision frame.`);
    await waitForTarget(pageB, domain, first.marker, 5000);
    record.wireTarget.targetGeneration = activeWireFault.targetGeneration;
    record.wireMetrics = { ...routeMetrics };
  } else {
    await emitHint(domain, metadata);
    await waitFor(() => activeWireFault.held !== null, "first out-of-order Revision to be held", 5_000, 20);
    const second = await writeSecond(domain, cycle, metadata);
    record.followupWrite = second;
    await emitHint(domain, metadata);
    await waitFor(() => activeWireFault.handled, "out-of-order Revision frames", 5000, 20);
    if (routeMetrics.reordered !== 1 || activeWireFault.reorderedGenerations?.length !== 2 || activeWireFault.reorderedGenerations[1] <= activeWireFault.reorderedGenerations[0])
      throw new Error(`Cycle ${cycle} did not reorder exactly one pair of increasing target generations.`);
    await waitForTarget(pageB, domain, second.marker, 5000);
    record.wireTarget.reorderedGenerations = [...activeWireFault.reorderedGenerations];
    record.wireMetrics = { ...routeMetrics, held: routeMetrics.held };
    record.expectedMarker = second.marker;
  }
  const expected = record.followupWrite?.marker ?? first.marker;
  record.faultStartAtUtc ??= new Date().toISOString();
  record.faultStartMonotonicMs ??= performance.now();
  await assertCycleState(record, domain, expected, draft, null, null);
  activeWireFault = null;
  record.transportReestablishedAtUtc = null;
  record.transportAcceptedCountBefore = before;
}

async function cycleRestart(cycle, domain, record, draft, metadata) {
  const beforeA = transportA.accepted.length, beforeReader = transportAReader.accepted.length, beforeB = transportB.accepted.length;
  record.faultStartAtUtc = new Date().toISOString();
  record.faultStartMonotonicMs = performance.now();
  const old = host;
  const shutdownStarted = performance.now();
  const shutdown = await stopHost(old);
  // Keep the stop outcome and duration: a stop that outlasts the clients' reconnect budget changes what this cycle measures.
  record.oldHostShutdown = {
    instance: old.health.instance, pid: old.health.pid, portReleased: shutdown.portReleased,
    durationMs: Math.round(performance.now() - shutdownStarted),
    gracefulStopAcknowledged: shutdown.gracefulStopAcknowledged, gracefulStopError: shutdown.gracefulStopError,
    gracefulExitObserved: shutdown.gracefulExitObserved, forcedTermination: shutdown.forcedTermination,
    wrapperExitCode: shutdown.wrapperExitCode, finalSnapshotWritten: shutdown.final !== null,
  };
  if (!shutdown.wrapperExitObserved || !shutdown.portReleased?.released) throw new Error(`Cycle ${cycle} did not fully release the owned restarted host.`);
  host = null;
  await Promise.all([
    pageA.locator(".live-status[data-state='reconnecting'],.live-status[data-state='paused']").waitFor({ timeout: 5000 }),
    pageATarget.locator(".live-status[data-state='reconnecting'],.live-status[data-state='paused']").waitFor({ timeout: 5000 }),
    pageB.locator(".live-status[data-state='reconnecting'],.live-status[data-state='paused']").waitFor({ timeout: 5000 }),
  ]);
  record.outageConfirmedAtUtc = new Date().toISOString();
  record.outageConfirmedMonotonicMs = performance.now();
  const saved = await writeExternal(domain, cycle, metadata);
  record.write = saved;
  record.writeCommittedDuringOutage = saved.precommitMarkerAbsentInPages && saved.commitAcknowledgedMonotonicMs >= record.outageConfirmedMonotonicMs;
  if (!record.writeCommittedDuringOutage) throw new Error(`Cycle ${cycle} write was not committed after its confirmed host outage.`);
  const nextPort = old.port;
  host = await startHost(nextPort, "fake");
  const [reconnectedA, reconnectedReader, reconnectedB] = await Promise.all([
    measureConnectedAt(contextA, transportA, beforeA),
    measureConnectedAt(contextA, transportAReader, beforeReader),
    measureConnectedAt(contextB, transportB, beforeB),
  ]);
  await assertCycleState(record, domain, saved.marker, draft, reconnectedA, reconnectedB, reconnectedReader);
  record.newHostInstance = host.health.instance;
}

async function cycleWatcher(cycle, domain, record, draft, metadata) {
  const kind = chooseWatcherKind(cycle);
  const before = await sendControl("status");
  record.faultStartAtUtc = new Date().toISOString();
  record.faultStartMonotonicMs = performance.now();
  if (kind === "gap") {
    await sendControl("drop");
    record.watcherHintDroppedBeforeWrite = true;
  }
  const saved = await writeExternal(domain, cycle, metadata);
  record.write = saved;
  record.watcherFault = kind;
  if (kind === "overflow") await sendControl("overflow", { domains: [domain] });
  else if (kind === "failure") await sendControl("failure", { domains: [domain] });
  if (kind === "overflow") {
    const controlStatus = await sendControl("status");
    if (controlStatus.maxPendingHintCount > 128) throw new Error(`Cycle ${cycle} exceeded the hint bound during overflow.`);
  }
  if (kind === "gap") record.recovery = "ordinary-five-second-reconciliation";
  else if (kind === "overflow") record.recovery = "overflow-promotion";
  else record.recovery = "watcher-failure-reconciliation";
  if (kind === "gap") {
    await waitFor(async () => {
      const status = await sendControl("status");
      return status.checkpointCount > before.checkpointCount ? status : null;
    }, "periodic checkpoint after the watcher hint was dropped", 6500, 25);
  }
  await waitForTargetAndLive(pageB, domain, saved.marker, kind === "gap" ? 6500 : 7000);
  await assertCycleState(record, domain, saved.marker, draft, null, null);
  record.checkpointCountBefore = before.checkpointCount;
}

async function cycleReauthorize(cycle, domain, record, draft, metadata) {
  const beforeA = transportA.accepted.length, beforeReader = transportAReader.accepted.length, beforeB = transportB.accepted.length;
  record.faultStartAtUtc = new Date().toISOString();
  record.faultStartMonotonicMs = performance.now();
  const secretPath = join(metadata.data, "dashboard.secret");
  await writeFile(secretPath + ".c06-next", randomBytes(32));
  await rename(secretPath + ".c06-next", secretPath);
  await Promise.all([
    pageA.locator(".live-status[data-state='paused']").waitFor({ timeout: 5000 }),
    pageATarget.locator(".live-status[data-state='paused']").waitFor({ timeout: 5000 }),
    pageB.locator(".live-status[data-state='paused']").waitFor({ timeout: 5000 }),
  ]);
  const revokeState = await sendControl("status");
  if (revokeState.activeConnectionCount !== 0) throw new Error(`Cycle ${cycle} retained an unauthorized live connection.`);
  record.outageConfirmedAtUtc = new Date().toISOString();
  record.outageConfirmedMonotonicMs = performance.now();
  const saved = await writeExternal(domain, cycle, metadata);
  record.write = saved;
  record.writeCommittedDuringOutage = saved.precommitMarkerAbsentInPages && saved.commitAcknowledgedMonotonicMs >= record.outageConfirmedMonotonicMs;
  if (!record.writeCommittedDuringOutage) throw new Error(`Cycle ${cycle} write was not committed after authorization revocation.`);
  const old = host;
  const shutdown = await stopHost(old);
  if (!shutdown.wrapperExitObserved || !shutdown.portReleased?.released) throw new Error(`Cycle ${cycle} did not release the revoked host before reauthorization.`);
  host = await startHost(old.port, "real");
  await Promise.all([
    (async () => {
      await authorizeContext(contextA, pageA, host, metadata.data, false);
      await authorizeContext(contextA, pageATarget, host, metadata.data, false);
    })(),
    authorizeContext(contextB, pageB, host, metadata.data, false),
  ]);
  const [connectedA, connectedReader, connectedB] = await Promise.all([
    measureConnectedAt(contextA, transportA, beforeA),
    measureConnectedAt(contextA, transportAReader, beforeReader),
    measureConnectedAt(contextB, transportB, beforeB),
  ]);
  await assertCycleState(record, domain, saved.marker, draft, connectedA, connectedB, connectedReader);
  record.reauthorization = "new secret file, new host instance, explicit launch/bootstrap fixture and Retry";
  record.newHostInstance = host.health.instance;
}

function verifyWirePlan() {
  const count = Array.from({ length: 25 }, (_, i) => chooseWireKind(i + 26)).reduce((sum, kind) => { sum[kind] = (sum[kind] ?? 0) + 1; return sum; }, {});
  if (count.drop !== 9 || count.duplicate !== 8 || count.reorder !== 8) throw new Error("The Revision wire fault allocation must remain 9 drop / 8 duplicate / 8 reorder.");
}

let metadata;
runMain: try {
  verifyWirePlan();
  const created = JSON.parse(runOwned("dotnet", [smokeDll, "--c06-fixture", fixture], 120_000));
  metadata = JSON.parse(await readFile(join(fixture, "fixture.json"), "utf8"));
  await mkdir(controls, { recursive: true });
  await mkdir(logs, { recursive: true });
  const port = await freePort();
  if (args.startupFailureSmoke === true || args.startupFailureSmoke === "true") {
    result.matrix.mode = "owned-startup-failure-cleanup-probe-not-acceptance";
    let startupFailure = null;
    try { await startHost(port, "fake", { injectReadinessFailure: true }); }
    catch (error) { startupFailure = sanitize(error?.message ?? "host startup failed"); }
    if (!startupFailure?.includes("C06 deterministic startup-failure probe"))
      throw new Error("The C06 startup-failure probe did not reach its intentional readiness failure.");
    const failedStartHost = host;
    const cleanup = await stopHost(failedStartHost);
    host = null;
    if (!hostCleanupComplete(cleanup)) throw new Error("The owned startup-failure probe did not prove wrapper exit and fixture-port release.");
    if (!cleanup.hostPid || !cleanup.final?.source?.isDisposed || cleanup.final.activeConnectionCount !== 0)
      throw new Error("The startup-failure probe did not prove its host was identified, disposed, and connection-free.");
    result.startupFailureProbe = { startupFailure, cleanup };
    result.state = "passed";
    break runMain;
  }
  host = await startHost(port, "fake");
  const launch = await chromium.launch({ channel: browserChannel, headless: true });
  browser = launch;
  contextA = await browser.newContext();
  contextB = await browser.newContext();
  pageA = await contextA.newPage();
  pageB = await contextB.newPage();
  disconnectControl = createDisconnectControl({ emit: event => result.connectionLifecycle.push({ ...event, cycle: activeCycle, phase: activeCycle === null ? "preparation" : activeFaultStartMonotonicMs !== null && event.atMonotonicMs >= activeFaultStartMonotonicMs ? "fault" : "cycle-preparation" }) });
  await Promise.all([observeBrowser(pageA, "browserA-reader"), observeBrowser(pageB, "browserB-target")]);
  pageA.on("websocket", (socket) => socket.on("framereceived", (frame) => result.browserDiagnostics.push({ kind: "reader-live-frame", label: "browserA-reader", atUtc: new Date().toISOString() })));
  pageA.on("websocket", (socket) => socket.on("framereceived", (frame) => transportAReader.note(frame.payload, disconnectControl.generationForSocket("browserA-reader", socket)?.id ?? null)));
  pageB.on("websocket", (socket) => socket.on("framereceived", (frame) => transportB.note(frame.payload)));
  await wireRoute(pageB);
  await disconnectControl.routePage(pageA, "browserA-reader");
  await Promise.all([authorizeContext(contextA, pageA, host, metadata.data), authorizeContext(contextB, pageB, host, metadata.data)]);
  await openDocument(pageA, host.base);
  const initialDraft = `C06-UNSAVED-DRAFT-${randomUUID().slice(0, 8)}`;
  await pageA.getByRole("textbox", { name: "Review comment" }).fill(initialDraft);
  result.initialDraft = { value: initialDraft, presentBeforeFaultInjection: true, createdAtUtc: new Date().toISOString() };
  pageATarget = await contextA.newPage();
  await observeBrowser(pageATarget, "browserA-target");
  pageATarget.on("websocket", (socket) => socket.on("framereceived", (frame) => transportA.note(frame.payload, disconnectControl.generationForSocket("browserA-target", socket)?.id ?? null)));
  await disconnectControl.routePage(pageATarget, "browserA-target");
  await authorizeContext(contextA, pageATarget, host, metadata.data);
  result.initialBrowserStatus = {
    browserA: await requestCounts(pageA),
    browserATarget: await requestCounts(pageATarget),
    browserB: await requestCounts(pageB),
    topology: { independentBrowserContexts: 2, pages: 3, liveConnections: 3, contextA: ["reader-with-draft", "domain-target"], contextB: ["domain-target"] },
    atUtc: new Date().toISOString(),
  };

  for (const cycle of cyclePlan) {
    activeCycle = cycle;
    activeFaultStartMonotonicMs = null;
    const category = chooseCategory(cycle);
    const domain = domains[(cycle - 1) % domains.length];
    const draft = await prepareVisibleState(domain, metadata);
    const before = await sendControl("status");
    if (before.activeConnectionCount !== 3)
      throw new Error(`Cycle ${cycle} expected exactly three owned live connections across two browser contexts; found ${before.activeConnectionCount}.`);
    const record = {
      cycle,
      category,
      domain,
      startedAtUtc: new Date().toISOString(),
      initialDraftPresentBeforeFaultInjection: await pageA.getByRole("textbox", { name: "Review comment" }).inputValue() === result.initialDraft.value,
      statusBefore: await requestCounts(pageB),
      sourceBefore: sourceCounters(before),
      transportAcceptedAtStart: { browserA: transportA.accepted.at(-1) ?? null, browserB: transportB.accepted.at(-1) ?? null },
    };
    try {
      if (category === "disconnect") await cycleDisconnect(cycle, domain, record, draft, metadata);
      else if (category === "revisionWire") await cycleWire(cycle, domain, record, draft, metadata);
      else if (category === "serverRestart") await cycleRestart(cycle, domain, record, draft, metadata);
      else if (category === "watcherGapOverflow") await cycleWatcher(cycle, domain, record, draft, metadata);
      else await cycleReauthorize(cycle, domain, record, draft, metadata);
    } catch (error) {
      result.incompleteCycle = record;
      throw error;
    }
    record.completedAtUtc = new Date().toISOString();
    record.durationMs = Math.round(performance.now() - record.faultStartMonotonicMs);
    result.records.push(record);
    activeCycle = null;
    activeFaultStartMonotonicMs = null;
    await saveResult();
  }

  const finalStatusBeforeClose = await sendControl("status");
  if (finalStatusBeforeClose.activeConnectionCount !== 3)
    throw new Error(`The matrix ended with ${finalStatusBeforeClose.activeConnectionCount} owned live connections instead of three.`);
  await Promise.all([contextA.close(), contextB.close()]);
  await waitFor(async () => (await sendControl("status")).activeConnectionCount === 0, "live connection registry to empty", 10_000, 50);
  result.finalCoordinator = await sendControl("status");
  result.assertions.completedCycles = result.records.length;
  result.assertions.incorrectFinalStates = result.records.filter(record => !record.browserA?.visibleTarget || !record.browserB?.visibleTarget || !record.browserA?.readerRetained || !record.browserA?.draftRetained).length;
  result.assertions.connectionRegistryBeforeClose = finalStatusBeforeClose.activeConnectionCount;
  result.assertions.connectionRegistryAfterClose = result.finalCoordinator.activeConnectionCount;
  result.state = "passed";
  result.completedAtUtc = new Date().toISOString();
} catch (error) {
  result.state = "failed";
  result.failure = sanitize(`${error?.name ?? "Error"}: ${error?.message ?? "acceptance failure"}`).slice(0, 1200);
} finally {
  clearTimeout(totalDeadline);
  process.off("SIGINT", onSigint);
  process.off("SIGTERM", onSigterm);
  await disconnectControl?.cleanup().catch(() => {});
  await browser?.close().catch(() => {});
  let cleanupHost = null;
  if (host) {
    try { cleanupHost = await stopHost(host); }
    catch (error) { cleanupHost = { error: sanitize(error?.message ?? "cleanup failed") }; }
    if (!hostCleanupComplete(cleanupHost)) result.assertions.leakedProcesses++;
  }
  for (const child of [...ownedChildren]) if (!childHasExited(child)) child.kill();
  const wrapperExits = await Promise.all([...ownedChildren].map(child => waitChildExit(child, 5000)));
  const ownedWrappersRemaining = wrapperExits.filter(exited => !exited).length;
  if (ownedWrappersRemaining) result.assertions.leakedProcesses += ownedWrappersRemaining;
  result.cleanup = { host: cleanupHost, ownedWrappersRemaining, cancellationReason: cancelReason, fixtureRetained: true, fixture: relative(root, fixture) };
  if (result.state === "running") result.state = "failed";
  await saveResult();
  process.stdout.write(JSON.stringify({ state: result.state, cycles: result.records.length, output: relative(root, output), fixture: relative(root, fixture), port: cleanupHost?.port ?? host?.port ?? null }) + "\n");
  if (result.state !== "passed" || result.assertions.leakedProcesses !== 0) process.exitCode = 1;
}
