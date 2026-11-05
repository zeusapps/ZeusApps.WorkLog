#!/usr/bin/env node
import { createHash } from "node:crypto";
import { readFile, mkdir, rename, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { createRequire } from "node:module";
import os from "node:os";
import { performance } from "node:perf_hooks";
import { dirname, isAbsolute, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const dashboardRoot = resolve(repositoryRoot, "dashboard");
const helperPath = resolve(dashboardRoot, "src", "diff.ts");
const outputRoot = resolve(repositoryRoot, "artifacts", "dashboard-reading-reliability", "c04");
const dashboardRequire = createRequire(resolve(dashboardRoot, "package.json"));

function argumentsFrom(argv) {
  const result = { run: false, samples: 30, warmups: 3, output: undefined };
  for (const argument of argv) {
    if (argument === "--run") result.run = true;
    else if (argument.startsWith("--samples=")) result.samples = Number(argument.slice("--samples=".length));
    else if (argument.startsWith("--warmups=")) result.warmups = Number(argument.slice("--warmups=".length));
    else if (argument.startsWith("--out=")) result.output = argument.slice("--out=".length);
    else throw new Error(`Unknown option: ${argument}`);
  }
  if (!result.run) throw new Error("This benchmark is opt-in. Pass --run to measure the isolated comparison helper.");
  if (!Number.isInteger(result.samples) || result.samples < 30) throw new Error("--samples must be an integer of at least 30.");
  if (!Number.isInteger(result.warmups) || result.warmups < 1) throw new Error("--warmups must be a positive integer.");
  return result;
}

function sha256(value) {
  return createHash("sha256").update(value).digest("hex");
}

function requireInsideOutputRoot(value) {
  const target = resolve(repositoryRoot, value);
  const relativeTarget = relative(outputRoot, target);
  if (relativeTarget === "" || relativeTarget === ".." || relativeTarget.startsWith(`..${sep}`) || isAbsolute(relativeTarget))
    throw new Error("--out must name a file beneath artifacts/dashboard-reading-reliability/c04/.");
  return target;
}

function loadComparisonHelper(source) {
  const typescript = dashboardRequire("typescript");
  const compiled = typescript.transpileModule(source, {
    compilerOptions: {
      module: typescript.ModuleKind.CommonJS,
      target: typescript.ScriptTarget.ES2022,
      isolatedModules: true,
    },
    reportDiagnostics: true,
  });
  const errors = (compiled.diagnostics ?? []).filter((diagnostic) => diagnostic.category === typescript.DiagnosticCategory.Error);
  if (errors.length) {
    const format = typescript.formatDiagnosticsWithColorAndContext(errors, {
      getCanonicalFileName: (path) => path,
      getCurrentDirectory: () => dashboardRoot,
      getNewLine: () => "\n",
    });
    throw new Error(`Could not compile the comparison helper:\n${format}`);
  }
  const commonJsModule = { exports: {} };
  const execute = new Function("require", "module", "exports", compiled.outputText);
  execute(createRequire(helperPath), commonJsModule, commonJsModule.exports);
  if (typeof commonJsModule.exports.readMarks !== "function") throw new Error("The source comparison helper does not export readMarks().");
  return { readMarks: commonJsModule.exports.readMarks, timeoutMilliseconds: commonJsModule.exports.diffTimeoutMilliseconds, typescript };
}

const lineBytes = 128;
function markdownLines(sizeKiB) {
  const lineCount = (sizeKiB * 1024) / lineBytes;
  if (!Number.isInteger(lineCount)) throw new Error(`The ${sizeKiB} KiB fixture does not align to the fixed line size.`);
  return Array.from({ length: lineCount }, (_, index) => {
    const prefix = index % 32 === 0
      ? `## Section ${String(Math.floor(index / 32)).padStart(4, "0")} | `
      : `- Entry ${String(index).padStart(6, "0")} | `;
    return `${(prefix + "BASELINE detail").padEnd(lineBytes - 1, "x").slice(0, lineBytes - 1)}\n`;
  });
}

function workloadPair(sizeKiB, kind) {
  const beforeLines = markdownLines(sizeKiB);
  const afterLines = [...beforeLines];
  if (kind === "small-edit") {
    const index = Math.floor(afterLines.length / 2) + 1;
    afterLines[index] = afterLines[index].replace("BASELINE", "REVISED!");
  } else {
    for (let index = 0; index < afterLines.length; index++)
      afterLines[index] = afterLines[index].replace("BASELINE", "REWRITE!");
  }
  const before = beforeLines.join("");
  const after = afterLines.join("");
  const expectedBytes = sizeKiB * 1024;
  if (Buffer.byteLength(before, "ascii") !== expectedBytes || Buffer.byteLength(after, "ascii") !== expectedBytes)
    throw new Error(`The generated ${sizeKiB} KiB ${kind} pair has an unexpected byte length.`);
  if (!/^[\x00-\x7f]*$/.test(before + after)) throw new Error("The generated benchmark text must stay ASCII.");
  return { before, after };
}

function percentile(values, quantile) {
  const sorted = [...values].sort((left, right) => left - right);
  return sorted[Math.max(0, Math.ceil(quantile * sorted.length) - 1)];
}

async function saveSnapshot(target, value) {
  const temporary = `${target}.partial`;
  await writeFile(temporary, `${JSON.stringify(value, null, 2)}\n`, { encoding: "utf8" });
  await rename(temporary, target);
}

const options = argumentsFrom(process.argv.slice(2));
const timestamp = new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
const outputPath = options.output
  ? requireInsideOutputRoot(options.output)
  : resolve(outputRoot, `comparison-${timestamp}.json`);
if (existsSync(outputPath)) throw new Error(`Output already exists; choose a new ignored artifact path.`);
await mkdir(dirname(outputPath), { recursive: true });

const helperSource = await readFile(helperPath, "utf8");
const { readMarks, timeoutMilliseconds, typescript } = loadComparisonHelper(helperSource);
const diffPackage = JSON.parse(await readFile(resolve(dashboardRoot, "node_modules", "diff", "package.json"), "utf8"));
const gitHead = spawnSync("git", ["rev-parse", "HEAD"], { cwd: repositoryRoot, encoding: "utf8", windowsHide: true });
if (gitHead.status !== 0) throw new Error("Could not read the current source revision from Git.");

const result = {
  schemaVersion: 1,
  status: "running",
  scope: "Synchronous readMarks() comparison helper only: normalization, jsdiff line comparison, and result processing. Markdown rendering, sanitization, and browser work are excluded.",
  recordedAtUtc: new Date().toISOString(),
  source: {
    gitHead: gitHead.stdout.trim(),
    helperPath: "dashboard/src/diff.ts",
    helperSha256: sha256(helperSource),
    harnessPath: "scripts/benchmark-review-comparison.mjs",
    harnessSha256: sha256(await readFile(fileURLToPath(import.meta.url))),
    transpilation: "TypeScript single-file transpileModule, CommonJS, isolatedModules",
  },
  dependencies: { diff: diffPackage.version, typescript: typescript.version },
  environment: {
    platform: process.platform,
    architecture: process.arch,
    osRelease: os.release(),
    nodeVersion: process.version,
    cpuCount: os.cpus().length,
  },
  settings: {
    samplesPerWorkload: options.samples,
    warmupsPerWorkload: options.warmups,
    helperTimeoutMilliseconds: timeoutMilliseconds,
    sizesKiB: [50, 200, 400, 800],
    workloadKinds: ["small-edit", "full-rewrite"],
    lineBytes: lineBytes,
  },
  workloads: [],
};

console.log(JSON.stringify({ event: "benchmark-start", output: relative(repositoryRoot, outputPath), samples: options.samples, warmups: options.warmups }));
await saveSnapshot(outputPath, result);
for (const sizeKiB of result.settings.sizesKiB) {
  for (const kind of result.settings.workloadKinds) {
    const { before, after } = workloadPair(sizeKiB, kind);
    const warmupsMs = [];
    for (let index = 0; index < options.warmups; index++) {
      const start = performance.now();
      readMarks(before, after);
      warmupsMs.push(performance.now() - start);
    }
    const samplesMs = [];
    const incompleteSamples = [];
    let changedRangeCount = 0;
    let removedHunkCount = 0;
    for (let index = 0; index < options.samples; index++) {
      const start = performance.now();
      const marks = readMarks(before, after);
      const elapsedMs = performance.now() - start;
      samplesMs.push(elapsedMs);
      incompleteSamples.push(Boolean(marks?.incomplete));
      changedRangeCount = marks?.changed.length ?? 0;
      removedHunkCount = marks?.removed.length ?? 0;
      if ((index + 1) % 5 === 0 || index + 1 === options.samples)
        console.log(JSON.stringify({ event: "sample-progress", sizeKiB, kind, completed: index + 1, total: options.samples }));
    }
    result.workloads.push({
      sizeKiB,
      kind,
      inputBytesBefore: Buffer.byteLength(before, "ascii"),
      inputBytesAfter: Buffer.byteLength(after, "ascii"),
      lineCount: before.split("\n").length - 1,
      warmupsMs,
      samplesMs,
      incompleteCount: incompleteSamples.filter(Boolean).length,
      incompleteSamples,
      changedRangeCount,
      removedHunkCount,
      p50Ms: percentile(samplesMs, 0.5),
      p95Ms: percentile(samplesMs, 0.95),
    });
    await saveSnapshot(outputPath, result);
    console.log(JSON.stringify({ event: "workload-complete", sizeKiB, kind, p50Ms: result.workloads.at(-1).p50Ms, p95Ms: result.workloads.at(-1).p95Ms, incompleteCount: result.workloads.at(-1).incompleteCount }));
  }
}
result.status = "complete";
result.completedAtUtc = new Date().toISOString();
await saveSnapshot(outputPath, result);
console.log(JSON.stringify({ event: "benchmark-complete", output: relative(repositoryRoot, outputPath), workloads: result.workloads.length }));
