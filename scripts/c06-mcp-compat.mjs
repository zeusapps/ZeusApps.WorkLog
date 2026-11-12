import { spawnSync } from "node:child_process";
import { createHash, randomUUID } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { processOutcome } from "./c06-process-outcome.mjs";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const artifacts = join(root, "artifacts", "realtime-dashboard-updates");
const smokeDll = join(root, "tools", "WorkLog.Smoke", "bin", "Release", "net10.0", "WorkLog.Smoke.dll");
const args = parseArgs(process.argv.slice(2));
const baseline = resolve(String(args.baseline ?? join(artifacts, "baseline", "package-retry", "worklog-0.4.1-win-x64", "worklog.exe")));
const current = resolve(String(args.current ?? join(root, "src", "WorkLog", "bin", "Release", "net10.0", "worklog.exe")));
const outputDirectory = join(artifacts, "mcp-compat");
await mkdir(outputDirectory, { recursive: true });
const output = resolve(String(args.output ?? join(outputDirectory, `${new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-")}-${randomUUID().slice(0, 8)}.json`)));
const deadlineMs = Number(args.deadlineMinutes ?? 10) * 60_000;
if (!Number.isFinite(deadlineMs) || deadlineMs < 60_000 || deadlineMs > 20 * 60_000) throw new Error("--deadlineMinutes must be from 1 through 20.");
if (!existsSync(smokeDll)) throw new Error("The Release Smoke DLL is missing.");
if (!existsSync(baseline)) throw new Error("The preserved baseline package executable is missing.");
if (!existsSync(current)) throw new Error("The current Release executable is missing; build it before running compatibility acceptance.");

const expectedBaselineSha256 = "cf11ef5faae749ca76259ee05745ed8f4e8aa42590bd3a0f4f8c951e5d5e7f46";
const fixtureLabels = ["Claude Code protocol fixture", "Codex protocol fixture"];
const result = {
  schemaVersion: 1,
  state: "running",
  startedAtUtc: new Date().toISOString(),
  scope: "isolated stdio MCP protocol fixtures; no authenticated native client sessions",
  references: { nativeClaudeCodeSessions: "not run", nativeCodexSessions: "not run", fixtureLabels },
  executables: {
    preservedBaseline: { path: relative(root, baseline), sha256: await sha256(baseline) },
    currentRelease: { path: relative(root, current), sha256: await sha256(current) },
    smokeDllSha256: await sha256(smokeDll),
  },
  expectedBaselineSha256,
  cases: [],
  cleanup: { ownedWrapperExitCodes: [], fixturesRetained: true },
};
await saveResult();

let cancelled = false;
const hardDeadline = setTimeout(() => { cancelled = true; }, deadlineMs);
hardDeadline.unref();

function parseArgs(values) {
  const parsed = {};
  for (let index = 0; index < values.length; index++) {
    const value = values[index].replace(/^--/, "");
    const equals = value.indexOf("=");
    if (equals >= 0) parsed[value.slice(0, equals)] = value.slice(equals + 1);
    else if (values[index + 1] && !values[index + 1].startsWith("--")) parsed[value] = values[++index];
    else parsed[value] = true;
  }
  return parsed;
}

async function sha256(path) {
  const content = await readFile(path);
  return createHash("sha256").update(content).digest("hex");
}

async function saveResult() {
  await writeFile(output, JSON.stringify(result, null, 2));
}

function runOwned(label, executable, parameters, timeoutMs) {
  if (cancelled) throw new Error("C06 MCP compatibility deadline expired.");
  const environment = {
    ...process.env,
    DOTNET_BUNDLE_EXTRACT_BASE_DIR: join(artifacts, "mcp-compat", "bundle", label.replaceAll(" ", "-").toLowerCase()),
  };
  const startedAtUtc = new Date().toISOString();
  const start = performance.now();
  const childResult = spawnSync("dotnet", [smokeDll, "--c06-owned-process", executable, ...parameters], {
    cwd: root,
    env: environment,
    encoding: "utf8",
    windowsHide: true,
    timeout: timeoutMs,
  });
  const stdout = sanitize(childResult.stdout ?? "").trim();
  const stderr = sanitize(childResult.stderr ?? "").trim();
  const processStatus = processOutcome(childResult);
  const exitCode = processStatus.exitCode;
  result.cleanup.ownedWrapperExitCodes.push({ label, ...processStatus });
  return {
    command: `dotnet ${relative(root, smokeDll)} --c06-owned-process <executable> ${parameters.slice(1).map(() => "<isolated-argument>").join(" ")}`,
    startedAtUtc,
    elapsedMs: Math.round(performance.now() - start),
    ...processStatus,
    stdoutSummary: summarize(stdout),
    stderrPresent: !!stderr,
    stderrSummary: stderr ? summarize(stderr) : null,
  };
}

function sanitize(value) {
  return String(value)
    .replaceAll(root, "%WORKSPACE%")
    .replace(/#connect=[a-f0-9]+/gi, "#connect=[redacted]")
    .replace(/(dashboard\.secret|Cookie|Set-Cookie)\s*[:=]\s*[^\s]+/gi, "$1=[redacted]");
}

function summarize(value) {
  if (value.includes("DASHBOARD MCP PASSED")) return "DASHBOARD MCP PASSED";
  if (value.includes("Packaged digests:")) return value.split(/\r?\n/).find(line => line.includes("Packaged digests:"))?.slice(0, 500) ?? "Packaged digest acceptance completed.";
  return value.slice(-700);
}

function requireSuccess(label, outcome, expectedText) {
  if (outcome.exitCode !== 0 || outcome.timedOut || !outcome.stdoutSummary.includes(expectedText))
    throw new Error(`${label} failed (exit ${outcome.exitCode ?? "unknown"}, timed out ${outcome.timedOut}).`);
}

async function runClientFixture(role, clientLabel, executable) {
  const fixture = join(artifacts, "fixtures", `mcp-${role}-${randomUUID()}`);
  const setup = runOwned(`${role}-${clientLabel}-fixture-setup`, "dotnet", [smokeDll, "--dashboard-fixture", fixture], 120_000);
  requireSuccess(`${role} ${clientLabel} fixture setup`, setup, "");
  const dashboardMcp = runOwned(`${role}-${clientLabel}-mcp`, "dotnet", [smokeDll, "--dashboard-mcp", executable, fixture], 90_000);
  requireSuccess(`${role} ${clientLabel} MCP`, dashboardMcp, "DASHBOARD MCP PASSED");
  const digestMcp = runOwned(`${role}-${clientLabel}-digests`, "dotnet", [smokeDll, "--digests-only", executable], 90_000);
  requireSuccess(`${role} ${clientLabel} digest MCP`, digestMcp, "Packaged digests:");
  return { role, clientFixtureLabel: clientLabel, fixture: relative(root, fixture), dashboardMcp, digestMcp };
}

try {
  if (result.executables.preservedBaseline.sha256 !== expectedBaselineSha256)
    throw new Error("The preserved baseline executable hash differs from the immutable reference.");
  for (const clientLabel of fixtureLabels) {
    result.cases.push(await runClientFixture("preserved-baseline", clientLabel, baseline));
    await saveResult();
    result.cases.push(await runClientFixture("current-release", clientLabel, current));
    await saveResult();
  }
  result.finalExecutableHashes = {
    preservedBaseline: await sha256(baseline),
    currentRelease: await sha256(current),
  };
  if (result.finalExecutableHashes.preservedBaseline !== result.executables.preservedBaseline.sha256 || result.finalExecutableHashes.currentRelease !== result.executables.currentRelease.sha256)
    throw new Error("An MCP fixture changed an executable during compatibility acceptance.");
  result.state = "passed";
  result.completedAtUtc = new Date().toISOString();
} catch (error) {
  result.state = "failed";
  result.failure = sanitize(`${error?.name ?? "Error"}: ${error?.message ?? "compatibility failure"}`).slice(0, 1200);
} finally {
  clearTimeout(hardDeadline);
  await saveResult();
  process.stdout.write(JSON.stringify({ state: result.state, cases: result.cases.length, output: relative(root, output) }) + "\n");
  if (result.state !== "passed") process.exitCode = 1;
}
