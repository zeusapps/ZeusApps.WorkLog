import { spawn, spawnSync } from "node:child_process";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { resolve, dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { randomUUID } from "node:crypto";
import { watch } from "node:fs";
import { existsSync } from "node:fs";
import { delimiter } from "node:path";
const workspace = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const testPort = Number(process.env.WORKLOG_DASHBOARD_TEST_PORT ?? "47942");
if (!Number.isInteger(testPort) || testPort < 1 || testPort > 65535 || testPort === 47831)
  throw new Error("WORKLOG_DASHBOARD_TEST_PORT must be a valid test port other than 47831.");
const root = join(workspace, "artifacts", "dashboard-e2e", randomUUID());
const smoke = join(
  workspace,
  "tools/WorkLog.Smoke/bin/Release/net10.0/WorkLog.Smoke.dll",
);
const fixture = spawnSync("dotnet", [smoke, "--dashboard-fixture", root], {
  cwd: workspace,
  encoding: "utf8",
  windowsHide: true,
});
if (fixture.status !== 0) throw new Error(fixture.stdout + fixture.stderr);
const info = JSON.parse(await readFile(join(root, "fixture.json"), "utf8"));
const executable =
  process.env.WORKLOG_DASHBOARD_EXECUTABLE ??
  join(workspace, "src/WorkLog/bin/Release/net10.0/worklog.dll");
// Test-only recording launcher: with this variable set the dashboard appends editor targets to this file instead of launching an editor.
const launchLog = join(root, "editor-launches.log");
await mkdir(join(workspace, "dashboard/test-results"), { recursive: true });
await writeFile(
  join(workspace, "dashboard/test-results/fixture.json"),
  JSON.stringify({ ...info, executable, launchLog }),
);
const args = [
  "dashboard",
  "--no-open",
  "--port",
  String(testPort),
  "--data-dir",
  info.data,
  "--repository",
  info.repo,
];
const isDll = executable.endsWith(".dll");
let restarting = false;
const runtimeEnvironment = {
  ...process.env,
  WORKLOG_DASHBOARD_TEST_LAUNCH_LOG: launchLog,
};
if (process.env.WORKLOG_DASHBOARD_NO_NODE === "true")
  runtimeEnvironment.PATH = (process.env.PATH ?? "")
    .split(delimiter)
    .filter(
      (directory) =>
        !["node.exe", "node", "openspec.cmd", "openspec"].some((name) =>
          existsSync(join(directory, name)),
        ),
    )
    .join(delimiter);
if (process.env.WORKLOG_DASHBOARD_NO_NODE === "true" && !isDll) {
  runtimeEnvironment.DOTNET_ROOT = join(root, "no-installed-dotnet");
  runtimeEnvironment.DOTNET_ROOT_X64 = runtimeEnvironment.DOTNET_ROOT;
  runtimeEnvironment.DOTNET_MULTILEVEL_LOOKUP = "0";
}
function start() {
  const processChild = spawn(
    isDll ? "dotnet" : executable,
    isDll ? [executable, ...args] : args,
    {
      cwd: workspace,
      env: runtimeEnvironment,
      windowsHide: true,
      stdio: ["ignore", "pipe", "pipe"],
    },
  );
  processChild.stdout.on("data", (data) =>
    process.stdout.write(
      data
        .toString()
        .replace(/#connect=[a-f0-9]+/g, "#connect=[local-session]"),
    ),
  );
  processChild.stderr.pipe(process.stderr);
  processChild.on("exit", (code) => {
    if (!restarting) process.exit(code ?? 0);
  });
  return processChild;
}
let child = start(),
  lastRestart = "";
watch(root, async (_event, name) => {
  if (name !== "restart.request" || restarting) return;
  const request = await readFile(join(root, "restart.request"), "utf8").catch(
    () => "",
  );
  if (!request || request === lastRestart) return;
  lastRestart = request;
  restarting = true;
  await new Promise((done) => {
    child.once("exit", done);
    child.kill();
  });
  child = start();
  restarting = false;
});
for (const signal of ["SIGTERM", "SIGINT"])
  process.on(signal, () => {
    child.kill();
    setTimeout(() => process.exit(0), 1000);
  });
