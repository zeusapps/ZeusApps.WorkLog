#!/usr/bin/env node
import { createHmac, randomUUID, createHash } from "node:crypto";
import { spawn, spawnSync } from "node:child_process";
import { createServer } from "node:net";
import { createRequire } from "node:module";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import os from "node:os";
import { performance } from "node:perf_hooks";
import { classifyIdleEligibility, isSettleQuiet } from "./c06-acceptance-invariants.mjs";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const artifactRoot = join(root, "artifacts", "realtime-dashboard-updates");
const require = createRequire(join(root, "dashboard", "package.json"));
const { chromium } = require("@playwright/test");
const options = {};
for (let index = 2; index < process.argv.length; index++) {
  const raw = process.argv[index].replace(/^--/, "");
  const equals = raw.indexOf("=");
  if (equals >= 0) options[raw.slice(0, equals)] = raw.slice(equals + 1);
  else if (process.argv[index + 1] && !process.argv[index + 1].startsWith("--")) options[raw] = process.argv[++index];
  else options[raw] = true;
}
if (!options.run && !options.setupSmoke && !options.smoke && !options.faultSmoke) throw new Error("Pass --run, --smoke, --faultSmoke or --setupSmoke.");
const phase = String(options.phase ?? "baseline");
const samples = Number(options.samples ?? 30), warmups = Number(options.warmups ?? 3), idleMinutes = Number(options.idleMinutes ?? 5);
if (!/^[a-z0-9-]{1,24}$/i.test(phase)) throw new Error("Invalid --phase label.");
if (!options.setupSmoke && !options.smoke && !options.faultSmoke && (!Number.isInteger(samples) || samples < 30 || warmups !== 3 || idleMinutes !== 5)) throw new Error("Run requires --samples>=30 --warmups=3 --idleMinutes=5.");
const packageExe = resolve(root, String(options.exe ?? "artifacts/realtime-dashboard-updates/baseline/package-retry/worklog-0.4.1-win-x64/worklog.exe"));
const isDll = packageExe.toLowerCase().endsWith(".dll");
const payloadExecutable = isDll ? "dotnet" : packageExe;
const payloadPrefix = isDll ? [packageExe] : [];
const preservedWriterExe = resolve(root, String(options.writerExe ?? "artifacts/realtime-dashboard-updates/baseline/package-retry/worklog-0.4.1-win-x64/worklog.exe"));
const smokeDll = join(root, "tools", "WorkLog.Smoke", "bin", "Release", "net10.0", "WorkLog.Smoke.dll");
if (!existsSync(packageExe) || !existsSync(smokeDll) || !existsSync(preservedWriterExe)) throw new Error("The selected executable, preserved writer executable or WorkLog.Smoke Release DLL is missing.");
const timestamp = new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
const artifactDir = join(artifactRoot, phase);
await mkdir(artifactDir, { recursive: true });
const output = resolve(root, String(options.output ?? relative(root, join(artifactDir, `${timestamp}-${randomUUID().slice(0, 8)}.json`))));
if (!output.startsWith(artifactRoot + "\\") || existsSync(output)) throw new Error("--output must be a new file beneath artifacts/realtime-dashboard-updates/.");
await mkdir(dirname(output), { recursive: true });
const fixture = join(artifactRoot, "fixtures", `${phase}-${randomUUID()}`);
const logs = join(fixture, "logs");
const sha = (v) => createHash("sha256").update(v).digest("hex");
const sourceHead = spawnSync("git", ["rev-parse", "HEAD"], { cwd: root, encoding: "utf8", windowsHide: true }).stdout.trim();
const fixtureBuild = runOwned("dotnet", [smokeDll, "--c06-fixture", fixture], 120000);
if (fixtureBuild.status !== 0) throw new Error(`Could not build C06 fixture: ${fixtureBuild.stderr || fixtureBuild.stdout}`);
const fixtureMetadata = JSON.parse(await readFile(join(fixture, "fixture.json"), "utf8"));
await mkdir(logs, { recursive: true });
const repositoryIds = fixtureMetadata.repositories.map((r) => r.id);
const repo = fixtureMetadata.repositories[0].path, data = fixtureMetadata.data;
const packageHash = sha(await readFile(packageExe));

if (options.setupSmoke) {
  const check = runOwned("dotnet", [smokeDll, "--c06-log", fixture, repositoryIds[0], "c06-active-1", "C06-SMOKE", "C06 writer setup smoke"], 30000);
  if (check.status !== 0) throw new Error(`C06 writer setup smoke failed: ${check.stderr || check.stdout}`);
  const ack = JSON.parse(check.stdout.trim().split(/\r?\n/).at(-1));
  process.stdout.write(JSON.stringify({ state: "setup-smoke-passed", fixture: relative(root, fixture), fixtureMetadata, writerAck: ack }) + "\n");
  process.exit(0);
}

const result = {
  schemaVersion: 1, state: "running", phase, recordedAtUtc: new Date().toISOString(),
  baseline: { sourceHead, executable: relative(root, packageExe), executableKind: isDll ? "framework-dependent DLL via dotnet" : "self-contained package executable", executableSha256: packageHash, executableVersion: (() => { const v = spawnSync(payloadExecutable, [...payloadPrefix, "--version"], { encoding: "utf8", windowsHide: true, timeout: 10000 }); return (v.stdout || v.stderr).trim(); })(), smokeDllSha256: sha(await readFile(smokeDll)), harnessPath: "scripts/c06-dashboard-acceptance.mjs", harnessSha256: sha(await readFile(fileURLToPath(import.meta.url))) },
  environment: { platform: process.platform, architecture: process.arch, nodeVersion: process.version, osRelease: os.release(), browser: process.env.WORKLOG_BROWSER_CHANNEL ?? "msedge" },
  fixture: fixtureMetadata,
  settings: { idleMinutes, warmupsPerWorkload: warmups, samplesPerWorkload: samples, fixture: relative(root, fixture) },
  metrics: { browser: "same-origin browser requests, response bytes where body completes, and WebSocket frames; transport keepalive/checkpoint semantics are separately listed", server: "Process.TotalProcessorTime and working-set delta; optional copy endpoint reports method-boundary counters, not physical I/O", instrumentedEndpointEnabled: options.metrics === true || options.metrics === "true", measurementEndpointCalls: 0, measurementEndpointOverheadMs: [], unavailable: [] },
  workloads: [],
};
await writeFile(output, JSON.stringify(result, null, 2));

let server, browser, serverProcessId = null, serverProcessIdentity = null, exitSeen = false, requestCount = 0, serverExit = null, lastHealthCheck = "not-started";
let resolveServerExit;
const serverExited = new Promise((resolveExit) => { resolveServerExit = resolveExit; });
let port;
const requests = [], socketFrames = [];
const connectionEvents = [];
const activeWebSockets = new Set(), activeEventSources = new Set();
const ownedProcesses = new Set();
const runAbort = new AbortController();
let cancellationReason = null;
const deadlineMs = Number(options.deadlineMinutes ?? 45) * 60_000;
function cancelRun(reason) {
  if (runAbort.signal.aborted) return;
  cancellationReason = reason;
  runAbort.abort();
  for (const child of ownedProcesses) if (child.exitCode === null) child.kill();
  void browser?.close();
}
const deadline = setTimeout(() => cancelRun("deadline"), deadlineMs);
deadline.unref();
const timeoutSignal = () => AbortSignal.timeout(5000);
const onSigint = () => cancelRun("SIGINT"), onSigterm = () => cancelRun("SIGTERM");
process.on("SIGINT", onSigint);
process.on("SIGTERM", onSigterm);
function delay(ms) {
  return new Promise((resolveDelay, rejectDelay) => {
    if (runAbort.signal.aborted) { rejectDelay(new Error(`Probe cancelled (${cancellationReason ?? "cancelled"}).`)); return; }
    const timer = setTimeout(done, ms);
    const abort = () => { clearTimeout(timer); rejectDelay(new Error(`Probe cancelled (${cancellationReason ?? "cancelled"}).`)); };
    function done() { runAbort.signal.removeEventListener("abort", abort); resolveDelay(); }
    runAbort.signal.addEventListener("abort", abort, { once: true });
  });
}
function spawnOwned(executable, args, extra = {}) {
  const child = spawn("dotnet", [smokeDll, "--c06-owned-process", executable, ...args], {
    cwd: root, windowsHide: true, env: { ...process.env, DOTNET_BUNDLE_EXTRACT_BASE_DIR: join(fixture, "bundle") },
    stdio: ["ignore", "pipe", "pipe"], ...extra,
  });
  ownedProcesses.add(child);
  child.once("exit", () => ownedProcesses.delete(child));
  return child;
}
async function stopOwned(child, timeoutMs = 10000) {
  if (!child || child.exitCode !== null) return child?.exitCode !== null;
  child.kill();
  return Promise.race([
    new Promise((resolveExit) => child.once("exit", () => resolveExit(true))),
    new Promise((resolveTimeout) => setTimeout(() => resolveTimeout(false), timeoutMs)),
  ]);
}
async function waitForDomText(locator, text, timeoutMs) {
  const until = Date.now() + timeoutMs;
  while (Date.now() < until) {
    if ((await locator.innerText()).includes(text)) return;
    await new Promise((resolveWait) => setTimeout(resolveWait, 50));
  }
  throw new Error(`Visible DOM did not contain the expected fixture marker within ${timeoutMs}ms.`);
}

async function freePort() {
  const socket = createServer(); await new Promise((ok, bad) => socket.once("error", bad).listen(0, "127.0.0.1", ok));
  const value = socket.address().port; await new Promise((ok, bad) => socket.close((e) => e ? bad(e) : ok())); return value;
}
async function releasePort(value) {
  const socket = createServer();
  await new Promise((ok, bad) => socket.once("error", bad).listen(value, "127.0.0.1", ok));
  await new Promise((ok, bad) => socket.close((e) => e ? bad(e) : ok()));
}
async function verifyPortReleased(value, timeoutMs = 5000) {
  const startedAt = Date.now(), deadlineAt = startedAt + timeoutMs;
  let lastError = null, attempts = 0;
  while (Date.now() < deadlineAt) {
    attempts++;
    try { await releasePort(value); return { released: true, error: null, attempts, elapsedMs: Date.now() - startedAt }; }
    catch (error) { lastError = error?.code ?? error?.name ?? "unknown"; await new Promise((resolve) => setTimeout(resolve, 100)); }
  }
  return { released: false, error: lastError, attempts, elapsedMs: Date.now() - startedAt };
}
function processSnapshot() {
  if (!serverProcessId || !serverProcessIdentity) return { cpuMs: null, workingSetBytes: null };
  const p = spawnSync("powershell", ["-NoProfile", "-Command", `$p=Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Id -eq ${serverProcessId} }; if ($p -and $p.StartTime.ToUniversalTime().Ticks -eq ${serverProcessIdentity.startTicks}) { [pscustomobject]@{cpuMs=$p.TotalProcessorTime.TotalMilliseconds; workingSetBytes=$p.WorkingSet64} | ConvertTo-Json -Compress }`], { encoding: "utf8", windowsHide: true, timeout: 5000 });
  if (p.status !== 0 || !p.stdout.trim()) return { cpuMs: null, workingSetBytes: null };
  const info = JSON.parse(p.stdout); return { cpuMs: info.cpuMs, workingSetBytes: info.workingSetBytes };
}
function runOwned(exe, args, timeout = 120000) {
  return spawnSync("dotnet", [smokeDll, "--c06-owned-process", exe, ...args], { cwd: root, encoding: "utf8", windowsHide: true, timeout });
}
function processJson(exe, args) {
  const p = runOwned(exe, args);
  if (p.status !== 0) throw new Error(`Isolated writer failed (${args[0]}): ${p.stderr || p.stdout}`);
  return JSON.parse(p.stdout.trim().split(/\r?\n/).at(-1));
}
function percentile(values, fraction) { const sorted = [...values].sort((a, b) => a - b); return sorted[Math.max(0, Math.ceil(fraction * sorted.length) - 1)]; }
async function visibleText(page, text) { await page.getByText(text, { exact: false }).first().waitFor({ timeout: 15000 }); }
function processExists(pid) {
  const check = spawnSync("powershell", ["-NoProfile", "-Command", `if (Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Id -eq ${Number(pid)} }) { 'true' } else { 'false' }`], { encoding: "utf8", windowsHide: true, timeout: 5000 });
  if (check.status !== 0) throw new Error("Could not verify the controlled child process lifetime.");
  return check.stdout.trim() === "true";
}
function processIdentity(pid) {
  const check = spawnSync("powershell", ["-NoProfile", "-Command", `$p=Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Id -eq ${Number(pid)} }; if ($p) { [pscustomobject]@{pid=$p.Id; startTicks=$p.StartTime.ToUniversalTime().Ticks} | ConvertTo-Json -Compress }`], { encoding: "utf8", windowsHide: true, timeout: 5000 });
  if (check.status !== 0) throw new Error(`Could not verify the owned process identity (PowerShell exit ${check.status}: ${(check.stderr || check.stdout).trim().slice(0, 240)}).`);
  if (!check.stdout.trim()) return null;
  return JSON.parse(check.stdout);
}
async function waitForProcessGone(pid, expectedIdentity, timeoutMs = 5000) {
  const startedAt = Date.now(), deadlineAt = startedAt + timeoutMs;
  let checks = 0;
  while (Date.now() < deadlineAt) {
    checks++;
    const current = processIdentity(pid);
    if (!current || (expectedIdentity && current.startTicks !== expectedIdentity.startTicks)) return { gone: true, pidReused: !!current, checks, elapsedMs: Date.now() - startedAt };
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  checks++;
  const current = processIdentity(pid);
  return { gone: !current || (expectedIdentity && current.startTicks !== expectedIdentity.startTicks), pidReused: !!current && expectedIdentity && current.startTicks !== expectedIdentity.startTicks, checks, elapsedMs: Date.now() - startedAt };
}
async function waitForMarker(path, timeoutMs) {
  const until = Date.now() + timeoutMs;
  while (Date.now() < until) {
    if (existsSync(path)) return JSON.parse(await readFile(path, "utf8"));
    await delay(50);
  }
  throw new Error("Controlled child did not create its readiness marker within the bound.");
}
async function waitForExit(child, timeoutMs) {
  if (child.exitCode !== null) return true;
  return Promise.race([
    new Promise((resolveExit) => child.once("exit", () => resolveExit(true))),
    new Promise((resolveTimeout) => setTimeout(() => resolveTimeout(false), timeoutMs)),
  ]);
}
async function runFaultSmoke() {
  const marker = join(fixture, "controlled-hang-child.json");
  const fixtureJson = join(fixture, "fixture.json");
  const fixtureMetadataHash = sha(await readFile(fixtureJson));
  const childWrapper = spawnOwned("dotnet", [smokeDll, "--c06-hanging-child", marker]);
  const started = await waitForMarker(marker, 15000);
  const wrapperAliveBefore = processExists(started.wrapperPid);
  const childAliveBefore = processExists(started.childPid);
  const wrapperIdentity = processIdentity(started.wrapperPid), childIdentity = processIdentity(started.childPid);
  if (!wrapperAliveBefore || !childAliveBefore || !wrapperIdentity || !childIdentity || wrapperIdentity.startTicks !== started.wrapperStartTicks || childIdentity.startTicks !== started.childStartTicks) throw new Error("The controlled child tree was not live with the expected process identities before cancellation.");
  const wrapperExitObserved = await stopOwned(childWrapper, 10000);
  const wrapperTermination = await waitForProcessGone(started.wrapperPid, wrapperIdentity, 5000);
  const childTermination = await waitForProcessGone(started.childPid, childIdentity, 5000);
  const wrapperGone = wrapperTermination.gone;
  const childGone = childTermination.gone;
  const fixtureMetadataUnchanged = fixtureMetadataHash === sha(await readFile(fixtureJson));
  if (!wrapperExitObserved || !wrapperGone || !childGone || !fixtureMetadataUnchanged)
    throw new Error("Controlled process-tree cancellation did not fully clean up its owned child and fixture marker.");
  return { mode: "cancel-owned-hanging-child", wrapperAliveBefore, childAliveBefore, wrapperExitObserved, wrapperGone, wrapperTermination, childGone, childTermination, fixtureMetadataUnchanged, wrapperPid: started.wrapperPid, childPid: started.childPid };
}

runMain: {
try {
  port = await freePort(); const base = `http://127.0.0.1:${port}`;
  await mkdir(join(fixture, "bundle"), { recursive: true });
  server = spawnOwned(payloadExecutable, [...payloadPrefix, "dashboard", "--no-open", "--port", String(port), "--data-dir", data, "--repository", repo]);
  server.once("exit", (code, signal) => { exitSeen = true; serverExit = { code, signal, atUtc: new Date().toISOString() }; resolveServerExit(serverExit); });
  const logFile = join(logs, "server.log");
  for (const stream of [server.stdout, server.stderr]) stream.on("data", (bytes) => { void writeFile(logFile, bytes.toString().replace(/#connect=[a-f0-9]+/g, "#connect=[redacted]"), { flag: "a" }); });
  let ready = false;
  for (let i = 0; i < 300; i++) {
    if (exitSeen) break;
    try { const response = await fetch(base + "/health", { signal: timeoutSignal() }); lastHealthCheck = `http-${response.status}`; if (response.ok) { ready = true; break; } }
    catch (error) { lastHealthCheck = `network-${error?.cause?.code ?? error?.name ?? "error"}`; }
    await new Promise((r) => setTimeout(r, 100));
  }
  if (!ready) throw new Error(`Packaged dashboard did not become ready within 30 seconds (sanitized check: ${lastHealthCheck}; process exited: ${exitSeen}).`);
  const secret = await readFile(join(data, "dashboard.secret"));
  const health = await (await fetch(base + "/health", { signal: timeoutSignal() })).json();
  serverProcessId = health.pid;
  serverProcessIdentity = processIdentity(serverProcessId);
  if (!serverProcessIdentity) throw new Error("Could not capture the dashboard host process identity from its health PID.");
  const tokenResponse = await fetch(base + "/api/launch", { method: "POST", signal: timeoutSignal(), headers: { "X-WorkLog-Launch": createHmac("sha256", secret).update("launch:" + health.instance).digest("hex") } });
  if (!tokenResponse.ok) throw new Error("Could not authorize the isolated browser fixture.");
  const { token } = await tokenResponse.json();
  browser = await chromium.launch({ channel: process.env.WORKLOG_BROWSER_CHANNEL ?? "msedge", headless: true });
  const page = await (await browser.newContext({ viewport: { width: 1440, height: 1000 } })).newPage();
  const requestRows = new WeakMap();
  page.on("request", (request) => {
    const url = new URL(request.url()); if (url.origin !== base) return;
    const row = { at: Date.now(), method: request.method(), path: url.pathname, resourceType: request.resourceType(), status: null, bytes: null };
    requestRows.set(request, row); requests.push(row); requestCount++;
    if (url.pathname.startsWith("/api/live") && request.resourceType() === "eventsource") {
      activeEventSources.add(request);
      connectionEvents.push({ kind: "eventsource-open", atUtc: new Date().toISOString(), monotonicMs: performance.now() });
    }
  });
  page.on("requestfinished", (request) => {
    if (activeEventSources.delete(request)) connectionEvents.push({ kind: "eventsource-close", atUtc: new Date().toISOString(), monotonicMs: performance.now() });
  });
  page.on("requestfailed", (request) => {
    if (activeEventSources.delete(request)) connectionEvents.push({ kind: "eventsource-failed", atUtc: new Date().toISOString(), monotonicMs: performance.now() });
  });
  page.on("response", async (response) => {
    const request = response.request(), row = requestRows.get(request); if (!row) return;
    row.status = response.status();
    if (row.path.startsWith("/api/live")) return;
    try { row.bytes = (await response.body()).byteLength; } catch { row.bytes = Number(response.headers()["content-length"] ?? 0) || null; }
  });
  page.on("websocket", (ws) => {
    activeWebSockets.add(ws);
    connectionEvents.push({ kind: "websocket-open", atUtc: new Date().toISOString(), monotonicMs: performance.now() });
    ws.on("close", () => {
      activeWebSockets.delete(ws);
      connectionEvents.push({ kind: "websocket-close", atUtc: new Date().toISOString(), monotonicMs: performance.now() });
    });
    ws.on("framesent", (frame) => socketFrames.push({ direction: "sent", bytes: Buffer.byteLength(String(frame.payload)), payloadKind: classifyFrame(frame.payload) }));
    ws.on("framereceived", (frame) => socketFrames.push({ direction: "received", bytes: Buffer.byteLength(String(frame.payload)), payloadKind: classifyFrame(frame.payload) }));
  });
  function classifyFrame(payload) { const text = String(payload); return /ping|keep.?alive/i.test(text) ? "possible-keepalive" : /checkpoint|revision/i.test(text) ? "possible-checkpoint" : "opaque"; }
  async function serverMetrics() {
    if (!(options.metrics === true || options.metrics === "true")) return null;
    const started = performance.now();
    const response = await page.request.get(base + "/api/c06-metrics", { timeout: 5000 });
    result.metrics.measurementEndpointCalls++;
    result.metrics.measurementEndpointOverheadMs.push(performance.now() - started);
    if (!response.ok()) throw new Error(`Instrumented metrics endpoint returned ${response.status()}.`);
    return response.json();
  }
  function counterDelta(before, after) {
    if (!before || !after) return null;
    const sharedCounters = ["metadataEnumerations", "metadataEntries", "metadataProbes", "documentContentReads", "documentContentReadBytes", "documentSha256Hashes", "documentSha256Bytes", "sqliteCommands", "journalEntryDecodes", "digestRowsMaterialized", "gitCommandStartAttempts", "gitCommandStarts", "openSpecCommandStartAttempts", "openSpecCommandStarts"];
    for (const key of sharedCounters) {
      if (!Number.isFinite(before.counters?.[key]) || !Number.isFinite(after.counters?.[key]))
        throw new Error(`Required common server counter '${key}' is unavailable or non-numeric on a measurement endpoint.`);
    }
    if (!Number.isFinite(after.counters?.afterOnlyMetadataProbes))
      throw new Error("Required after-only server counter 'afterOnlyMetadataProbes' is unavailable or non-numeric after instrumentation.");
    if (before.counters.afterOnlyMetadataProbes !== undefined && !Number.isFinite(before.counters.afterOnlyMetadataProbes))
      throw new Error("Baseline after-only server counter 'afterOnlyMetadataProbes' is present but non-numeric.");
    for (const key of ["gcAllocatedBytes", "totalProcessorTimeTicks"]) {
      if (!Number.isFinite(before.process?.[key]) || !Number.isFinite(after.process?.[key]))
        throw new Error(`Required process measurement '${key}' is unavailable or non-numeric on a measurement endpoint.`);
    }
    // The preserved pre-C06 build has no detector/watcher callsites. Normalize only that documented absence.
    const counters = Object.fromEntries(sharedCounters.map(key => [key, after.counters[key] - before.counters[key]]));
    counters.afterOnlyMetadataProbes = after.counters.afterOnlyMetadataProbes - (before.counters.afterOnlyMetadataProbes ?? 0);
    const commonMetadataProbes = counters.metadataProbes;
    const afterOnlyMetadataProbes = counters.afterOnlyMetadataProbes ?? 0;
    return {
      scope: after.scope,
      counters,
      metadataProbeTotals: {
        commonCallsites: commonMetadataProbes,
        afterOnlyDetectorAndWatcherCallsites: afterOnlyMetadataProbes,
        allAfterCallsites: commonMetadataProbes + afterOnlyMetadataProbes,
        afterOnlyBaseline: before.counters.afterOnlyMetadataProbes === undefined
          ? "not applicable: those callsites are absent from the preserved pre-C06 target"
          : "measured by the matching counter",
      },
      process: { gcAllocatedBytes: after.process.gcAllocatedBytes - before.process.gcAllocatedBytes, totalProcessorTimeTicks: after.process.totalProcessorTimeTicks - before.process.totalProcessorTimeTicks, totalProcessorTimeMs: (after.process.totalProcessorTimeTicks - before.process.totalProcessorTimeTicks) / 10000 },
    };
  }

  await page.goto(base + "/ongoing#connect=" + token, { timeout: 45000 });
  await page.getByRole("heading", { name: "Ongoing board" }).waitFor({ timeout: 45000 });
  await page.getByRole("textbox", { name: "Search OpenSpec documents" }).fill("C06_TARGET_DOCUMENT");
  await page.locator(".search-result").first().waitFor({ timeout: 20000 });
  await page.locator(".search-result").first().click();
  await page.getByRole("dialog", { name: "OpenSpec document reader" }).waitFor();
  const reader = page.locator(".markdown");
  await page.locator(".document-current strong").waitFor();
  const selectedHeading = await page.locator(".document-current strong").innerText();
  if (selectedHeading !== "design.md") throw new Error(`Expected design.md as the selected baseline document; got ${selectedHeading}.`);
  const initialDocumentText = await reader.innerText();
  if (!initialDocumentText.includes("C06_TARGET_DOCUMENT")) throw new Error(`Selected design.md did not contain the expected marker; observed ${initialDocumentText.slice(0, 120)}.`);

  if (options.smoke || options.faultSmoke) {
    const docId = `C06_SMOKE_DOCUMENT_${randomUUID().slice(0, 8)}`;
    await writeFile(join(repo, "openspec", "changes", "c06-active-1", "design.md"), `# C06 Active Design\n\nC06_TARGET_DOCUMENT\n${docId}\n` + "x".repeat(100 * 1024 - 60));
    await waitForDomText(reader, docId, 15000);
    await page.getByRole("button", { name: "Close document reader" }).click();
    await page.goto(base + "/history");
    await page.getByRole("heading", { name: "Work over time" }).waitFor({ timeout: 15000 });
    const journalId = `C06-SMOKE-${randomUUID().slice(0, 8)}`;
    processJson("dotnet", [smokeDll, "--c06-log", fixture, repositoryIds[0], "c06-active-1", journalId, `Smoke summary ${journalId}`]);
    await visibleText(page, journalId);
    await page.goto(base + "/daily");
    await page.getByRole("heading", { name: "Daily", exact: true }).waitFor();
    const repository = repositoryIds[0], ticket = fixtureMetadata.dailyDigestTickets[repository];
    const digestId = `C06_SMOKE_DIGEST_${randomUUID().slice(0, 8)}`;
    const digestAck = processJson("dotnet", [smokeDll, "--c06-digest-save", fixture, repository, ticket, digestId]);
    await visibleText(page, digestId);
    if (digestAck.journalSnapshotBefore !== digestAck.journalSnapshotAfter) throw new Error("Digest-only smoke changed the journal snapshot.");
    result.state = "smoke-passed";
    result.smoke = { documentWriteVisible: true, journalWriteVisible: true, digestOnlyWriteVisible: true, journalSnapshotStableDuringDigest: true };
    if (options.faultSmoke) result.faultCleanup = await runFaultSmoke();
    break runMain;
  }

  // Wait until the page is Live and quiet (no ordinary API request, connection event or status change for the settle period)
  // so the harness's own reader click or open is not counted against the strictly monitored window.
  async function settleLiveQuiet(targetPage) {
    const settleMs = Number(options.settleMs ?? 2000), timeoutMs = Number(options.settleTimeoutMs ?? 30000);
    const startedAtUtc = new Date().toISOString(), startMonotonic = performance.now();
    const apiCount = () => requests.reduce((n, row) => n + (row.path.startsWith("/api/") && !row.path.startsWith("/api/live") ? 1 : 0), 0);
    let lastApi = apiCount(), lastConnection = connectionEvents.length, lastSeq = null, lastActivity = performance.now(), polls = 0, activityResets = 0;
    while (true) {
      polls++;
      const now = performance.now();
      const state = await targetPage.evaluate(() => { window.__c06RecordLiveStatus?.(); return { seq: window.__c06StatusSeq, status: document.querySelector(".live-status")?.getAttribute("data-state") ?? null }; });
      const api = apiCount(), connection = connectionEvents.length;
      if (api !== lastApi || connection !== lastConnection || (lastSeq !== null && state.seq !== lastSeq) || state.status !== "live") { lastActivity = performance.now(); activityResets++; }
      lastApi = api; lastConnection = connection; lastSeq = state.seq;
      if (isSettleQuiet({ status: state.status, nowMs: performance.now(), lastActivityMs: lastActivity, settleMs })) break;
      if (performance.now() - startMonotonic >= timeoutMs) {
        const record = { settleMs, timeoutMs, startedAtUtc, endedAtUtc: new Date().toISOString(), durationMs: Math.round(performance.now() - startMonotonic), polls, activityResets, settled: false };
        throw Object.assign(new Error(`Live-and-quiet settle did not complete within ${timeoutMs}ms.`), { settle: record });
      }
      await delay(50);
    }
    return { settleMs, timeoutMs, startedAtUtc, endedAtUtc: new Date().toISOString(), durationMs: Math.round(performance.now() - startMonotonic), polls, activityResets, settled: true };
  }

  async function idle(name, readerOpen) {
    if (!readerOpen) await page.getByRole("button", { name: "Close document reader" }).click();
    await page.evaluate(() => {
      const target = window;
      if (target.__c06IdleMonitorInstalled) return;
      target.__c06IdleMonitorInstalled = true;
      target.__c06IdleMonitoring = false;
      target.__c06IdleActions = [];
      target.__c06IdleStatusChanges = [];
      for (const type of ["click", "keydown", "input", "change", "pointerdown", "wheel"]) {
        document.addEventListener(type, () => {
          if (target.__c06IdleMonitoring) target.__c06IdleActions.push({ type, monotonicMs: performance.now() });
        }, true);
      }
      target.__c06StatusSignature = null;
      target.__c06StatusSeq = 0;
      const recordStatus = () => {
        const states = Array.from(document.querySelectorAll(".live-status"), node => node.getAttribute("data-state"));
        const signature = states.join("|");
        if (signature !== target.__c06StatusSignature) { target.__c06StatusSignature = signature; target.__c06StatusSeq++; }
        if (!target.__c06IdleMonitoring) return;
        target.__c06IdleStatusChanges.push({ states, monotonicMs: performance.now() });
      };
      new MutationObserver(recordStatus).observe(document.documentElement, { subtree: true, childList: true, attributes: true, attributeFilter: ["data-state"] });
      target.__c06RecordLiveStatus = recordStatus;
    });
    const settle = await settleLiveQuiet(page);
    const startTransportCount = activeWebSockets.size + activeEventSources.size;
    const startStatus = await page.locator(".live-status").first().getAttribute("data-state").catch(() => null);
    if (startStatus !== "live" || startTransportCount < 1) {
      const startReasons = [];
      if (startStatus !== "live") startReasons.push("window did not start Live");
      if (startTransportCount < 1) startReasons.push("no established WebSocket or EventSource at window start");
      const workload = { name, durationMs: 0, readerOpen, settle, connectivityEligibility: { state: "ineligible", reasons: startReasons, baselineInterpretation: "not applicable: preserved baseline predates the live transport", initialStatus: startStatus, initialEstablishedTransportCount: startTransportCount, liveSamples: [], statusChanges: [], connectionEvents: [], userActions: [], localClockBoundaries: [], monitorResolutionMs: 250 } };
      result.workloads.push(workload);
      await writeFile(output, JSON.stringify(result, null, 2));
      throw new Error(`${name} could not start an eligible Live/transport window: ${startReasons.join("; ")}.`);
    }
    await page.evaluate(() => {
      window.__c06IdleActions = [];
      window.__c06IdleStatusChanges = [];
      window.__c06IdleMonitoring = true;
      window.__c06RecordLiveStatus?.();
    });
    const serverBefore = await serverMetrics();
    const startRequest = requests.length, startFrame = socketFrames.length, startConnectionEvent = connectionEvents.length;
    const before = processSnapshot(), begin = Date.now(), beginMonotonic = performance.now();
    const initialClockMinute = new Date().toLocaleString("en-CA", { timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone, year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hourCycle: "h23" });
    const clockBoundaries = [];
    const liveSamples = [];
    let previousMinute = initialClockMinute;
    const deadline = beginMonotonic + 300000;
    while (performance.now() < deadline) {
      const status = await page.locator(".live-status").first().getAttribute("data-state").catch(() => null);
      const transportCount = activeWebSockets.size + activeEventSources.size;
      const monotonicMs = performance.now();
      liveSamples.push({ atUtc: new Date().toISOString(), monotonicMs, status, establishedTransportCount: transportCount });
      const currentMinute = new Date().toLocaleString("en-CA", { timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone, year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hourCycle: "h23" });
      if (currentMinute !== previousMinute) {
        clockBoundaries.push({ fromLocalMinute: previousMinute, toLocalMinute: currentMinute, atUtc: new Date().toISOString(), monotonicMs });
        previousMinute = currentMinute;
      }
      await delay(Math.min(250, Math.max(1, deadline - performance.now())));
    }
    await page.evaluate(() => { window.__c06IdleMonitoring = false; });
    const endMonotonic = performance.now();
    const end = processSnapshot(), subset = requests.slice(startRequest), frames = socketFrames.slice(startFrame), serverAfter = await serverMetrics();
    const connectionWindow = connectionEvents.slice(startConnectionEvent);
    const idleUi = await page.evaluate(() => ({ actions: window.__c06IdleActions, statusChanges: window.__c06IdleStatusChanges }));
    const eligibility = classifyIdleEligibility({ startStatus, startTransportCount, liveSamples, statusChanges: idleUi.statusChanges, connectionEvents: connectionWindow, userActions: idleUi.actions });
    const api = subset.filter((row) => row.path.startsWith("/api/") && !row.path.startsWith("/api/live"));
    const workload = {
      name, durationMs: Math.round(endMonotonic - beginMonotonic), startedAtUtc: new Date(begin).toISOString(), endedAtUtc: new Date().toISOString(), readerOpen, settle, apiRequestCount: api.length,
      connectivityEligibility: { ...eligibility, baselineInterpretation: "not applicable: preserved baseline predates the live transport; these after-run observations require established transport and Live", initialStatus: startStatus, initialEstablishedTransportCount: startTransportCount, liveSamples, statusChanges: idleUi.statusChanges, connectionEvents: connectionWindow, userActions: idleUi.actions, localClockBoundaries: clockBoundaries, monitorResolutionMs: 250, monotonicStartMs: beginMonotonic, monotonicEndMs: endMonotonic },
      periodicCatalogDocumentSearchTimelineReads: api.filter((r) => ["/api/catalog", "/api/document", "/api/search", "/api/timeline"].includes(r.path)).length,
      requestsByPath: Object.fromEntries([...new Set(api.map(r => r.path))].map(path => [path, api.filter(r => r.path === path).length])),
      responseBytes: api.reduce((sum, row) => sum + (row.bytes ?? 0), 0),
      serverProcessCpuMs: before.cpuMs === null || end.cpuMs === null ? null : end.cpuMs - before.cpuMs,
      serverWorkingSetDeltaBytes: before.workingSetBytes === null || end.workingSetBytes === null ? null : end.workingSetBytes - before.workingSetBytes,
      serverBoundaryWork: counterDelta(serverBefore, serverAfter),
      transport: { requestCount: subset.filter(r => r.path.startsWith("/api/live")).length, websocketFrames: frames.length, websocketBytes: frames.reduce((sum, frame) => sum + frame.bytes, 0), frameKinds: Object.fromEntries([...new Set(frames.map(f => f.payloadKind))].map(kind => [kind, frames.filter(f => f.payloadKind === kind).length])), serverSentEvents: subset.filter(r => r.path.startsWith("/api/live") && r.resourceType === "eventsource").length, keepaliveOrCheckpointClassification: "SignalR transport absent in baseline; after-run raw frames are categorized heuristically and must be interpreted with instrumented server event counters." },
      rawRequests: api,
    };
    result.workloads.push(workload);
    await writeFile(output, JSON.stringify(result, null, 2));
    if (eligibility.reasons.length) throw new Error(`${name} was not an eligible five-minute connected-and-Live window: ${eligibility.reasons.join("; ")}.`);
  }
  await idle("idle-no-reader-5m", false);
  await page.getByRole("textbox", { name: "Search OpenSpec documents" }).fill("C06_TARGET_DOCUMENT");
  await page.locator(".search-result").first().waitFor(); await page.locator(".search-result").first().click();
  await page.getByRole("dialog", { name: "OpenSpec document reader" }).waitFor();
  await idle("idle-open-reader-5m", true);
  if (options.idleOnly) {
    result.state = "complete"; result.completedAtUtc = new Date().toISOString(); result.idleOnly = true;
    break runMain;
  }

  async function measure(name, write, waitVisible, targetFor = (id) => id) {
    const timings = [], raw = [], serverBefore = await serverMetrics();
    for (let index = 0; index < warmups + samples; index++) {
      const id = `C06_MEASURE_${name.toUpperCase().replaceAll("-", "_")}_${index}_${randomUUID().slice(0, 8)}`;
      const visibleToken = targetFor(id);
      if (await page.getByText(visibleToken, { exact: false }).count()) throw new Error(`Sample target already appears before commit for ${name}.`);
      const ack = await write(id);
      const committed = Date.parse(ack.committedAtUtc);
      await waitVisible(visibleToken, ack);
      const elapsed = Math.max(0, Date.now() - committed);
      if (index >= warmups) { timings.push(elapsed); raw.push({ sample: index - warmups + 1, committedAtUtc: ack.committedAtUtc, visibleAtUtc: new Date(committed + elapsed).toISOString(), latencyMs: elapsed, target: visibleToken }); }
    }
    const mean = timings.reduce((a,b)=>a+b,0)/timings.length, std = Math.sqrt(timings.reduce((a,x)=>a+(x-mean)**2,0)/timings.length);
    const serverAfter = await serverMetrics();
    result.workloads.push({ name, warmups, measuredSamples: timings.length, samplesMs: timings, raw, p50Ms: percentile(timings, .5), p95Ms: percentile(timings, .95), meanMs: mean, standardDeviationMs: std, coefficientOfVariation: mean ? std / mean : null, minMs: Math.min(...timings), maxMs: Math.max(...timings), serverBoundaryWork: counterDelta(serverBefore, serverAfter) });
    await writeFile(output, JSON.stringify(result, null, 2));
  }
  const largePath = join(repo, "openspec", "changes", "c06-active-1", "design.md");
  await measure("document-commit-to-dom", async (id) => {
    const text = `# C06 Active Design\n\nC06_TARGET_DOCUMENT\n${id}\n` + "x".repeat(100 * 1024 - Buffer.byteLength(`# C06 Active Design\n\nC06_TARGET_DOCUMENT\n${id}\n`));
    await writeFile(largePath, text, "utf8");
    return { committedAtUtc: new Date().toISOString() };
  }, (id) => waitForDomText(reader, id, 15000));

  await page.getByRole("button", { name: "Close document reader" }).click();
  await page.goto(base + "/history");
  await page.getByRole("heading", { name: "Work over time" }).waitFor({ timeout: 10000 });
  await measure("journal-write-to-history-ticket-visible", (id) => {
    const ticket = `C06-MEASURE-${id.slice(-8)}`;
    return processJson("dotnet", [smokeDll, "--c06-log", fixture, repositoryIds[0], "c06-active-1", ticket, id]);
  }, (ticket) => visibleText(page, ticket), (id) => `C06-MEASURE-${id.slice(-8)}`);

  await page.goto(base + "/daily");
  await page.getByRole("heading", { name: "Daily", exact: true }).waitFor();
  const digestRepository = repositoryIds[0], digestTicket = fixtureMetadata.dailyDigestTickets[digestRepository];
  let digestSnapshot;
  await measure("digest-save-to-daily-dom", async (id) => {
    const ack = processJson("dotnet", [smokeDll, "--c06-digest-save", fixture, digestRepository, digestTicket, id]);
    if (ack.journalSnapshotBefore !== ack.journalSnapshotAfter) throw new Error("Digest-only workload modified the journal.");
    if (digestSnapshot !== undefined && ack.journalSnapshotBefore !== digestSnapshot) throw new Error("Journal snapshot drifted between digest samples.");
    digestSnapshot = ack.journalSnapshotAfter;
    return ack;
  }, (id) => visibleText(page, id));

  // Run the preserved package as a separate MCP writer reference in its own disposable workspace.
  const mcpRoot = join(fixture, "mcp-reference");
  const mcpRepo = join(mcpRoot, "worklog"); await mkdir(join(mcpRepo, "openspec"), { recursive: true });
  await writeFile(join(mcpRepo, "openspec", "config.yaml"), "schema: spec-driven\n");
  const gitInit = spawnSync("git", ["init", "--initial-branch=main"], { cwd: mcpRepo, encoding: "utf8", windowsHide: true, timeout: 10000 });
  if (gitInit.status !== 0) throw new Error("Could not initialize the isolated MCP writer reference repository.");
  const gitAdd = spawnSync("git", ["add", "openspec/config.yaml"], { cwd: mcpRepo, encoding: "utf8", windowsHide: true, timeout: 10000 });
  const gitCommit = spawnSync("git", ["-c", "user.name=C06 Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-m", "C06 MCP writer reference"], { cwd: mcpRepo, encoding: "utf8", windowsHide: true, timeout: 10000 });
  if (gitAdd.status !== 0 || gitCommit.status !== 0) throw new Error("Could not commit the isolated MCP writer reference repository.");
  const mcp = runOwned("dotnet", [smokeDll, "--dashboard-mcp", preservedWriterExe, mcpRoot], 60000);
  if (mcp.status !== 0) throw new Error(`Preserved package MCP contract reference failed: ${mcp.stderr || mcp.stdout}`);
  const digestMcp = runOwned("dotnet", [smokeDll, "--digests-only", preservedWriterExe], 120000);
  if (digestMcp.status !== 0) throw new Error(`Preserved package digest MCP reference failed: ${digestMcp.stderr || digestMcp.stdout}`);
  result.mcpWriterReference = { executableSha256: sha(await readFile(preservedWriterExe)), client: "protocol-level MCP smoke fixture", loggingQuery: "passed", digestContract: "passed", nativeClaudeCodeOrCodexSessions: "not run" };
  result.state = "complete"; result.completedAtUtc = new Date().toISOString();
} catch (error) {
  result.state = "failed"; result.failure = String(error?.stack ?? error);
  throw error;
} finally {
  clearTimeout(deadline);
  process.off("SIGINT", onSigint); process.off("SIGTERM", onSigterm);
  if (browser) await browser.close().catch(() => {});
  let ownedWrappersStopped = true;
  for (const child of [...ownedProcesses]) if (!(await stopOwned(child, 10000))) ownedWrappersStopped = false;
  if (server && !exitSeen) await Promise.race([serverExited, new Promise((r) => setTimeout(r, 10000))]);
  const hostTermination = serverProcessId ? await waitForProcessGone(serverProcessId, serverProcessIdentity, 5000) : null;
  const portCheck = exitSeen && port ? await verifyPortReleased(port) : { released: false, error: null, attempts: 0, elapsedMs: 0 };
  const portReleased = portCheck.released;
  const hostProcessGone = hostTermination?.gone ?? null;
  result.cleanup = { ownedWrapperPid: server?.pid ?? null, actualHostPidFromHealth: serverProcessId, ownedServerExitObserved: exitSeen, ownedServerExit: serverExit, ownedWrappersStopped, hostProcessGone, hostTermination, cancellationReason, lastHealthCheck, ownedPort: port, portReleased, portReleaseError: portCheck.error, portReleaseAttempts: portCheck.attempts, portReleaseElapsedMs: portCheck.elapsedMs, fixtureRetained: true, fixture: relative(root, fixture) };
  result.transportFrames = socketFrames;
  await writeFile(output, JSON.stringify(result, null, 2));
  process.stdout.write(JSON.stringify({ state: result.state, phase, output: relative(root, output), fixture: relative(root, fixture), requestCount }) + "\n");
  if (!exitSeen || !ownedWrappersStopped || hostProcessGone === false || !portReleased) process.exitCode = 1;
}
}
