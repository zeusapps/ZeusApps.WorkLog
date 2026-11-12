import { test, expect, Page, BrowserContext, Route, Request, TestInfo, Locator } from "@playwright/test";
import { readFile, writeFile, mkdir, rename, mkdtemp, rm } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, resolve, join } from "node:path";
import { createHmac, randomUUID } from "node:crypto";
import { spawn, spawnSync } from "node:child_process";
import { createServer } from "node:net";
const testPort = Number(process.env["WORKLOG_DASHBOARD_TEST_PORT"] ?? "47942");
if (!Number.isInteger(testPort) || testPort < 1 || testPort > 65535 || testPort === 47831)
  throw new Error("WORKLOG_DASHBOARD_TEST_PORT must be a valid test port other than 47831.");
const base = `http://127.0.0.1:${testPort}`;
const evidencePrefix = process.env["WORKLOG_REPOSITORY_COLOR_EVIDENCE_PREFIX"] ?? "dev-002";
if (!/^[a-z0-9][a-z0-9-]{0,31}$/i.test(evidencePrefix))
  throw new Error("WORKLOG_REPOSITORY_COLOR_EVIDENCE_PREFIX must be a short filename prefix.");
const fixture = async () =>
  JSON.parse(await readFile(resolve("test-results/fixture.json"), "utf8")) as {
    root: string;
    data: string;
    repo: string;
    otherRepo: string;
    worktree: string;
    executable: string;
    launchLog: string;
  };
async function authorize(page: Page, serverBase = base, serverInfo?: Awaited<ReturnType<typeof fixture>>) {
  const selectedFixture = serverInfo ?? await fixture();
  const health = (await (await fetch(serverBase + "/health")).json()) as {
    instance: string;
  };
  const secret = await readFile(join(selectedFixture.data, "dashboard.secret"));
  const response = await fetch(serverBase + "/api/launch", {
    method: "POST",
    headers: {
      "X-WorkLog-Launch": createHmac("sha256", secret)
        .update("launch:" + health.instance)
        .digest("hex"),
    },
  });
  const { token } = (await response.json()) as { token: string };
  await page.goto(serverBase + "/ongoing#connect=" + token);
  await expect(
    page.getByRole("heading", { name: "Ongoing board" }),
  ).toBeVisible({ timeout: 45_000 });
}
async function unusedPort() {
  const probe = createServer();
  await new Promise<void>((done, fail) => probe.once("error", fail).listen(0, "127.0.0.1", done));
  const address = probe.address();
  if (!address || typeof address === "string") throw new Error("Could not reserve an isolated dashboard test port.");
  const port = address.port;
  await new Promise<void>((done, fail) => probe.close((error) => error ? fail(error) : done()));
  return port;
}
async function stopIsolatedServer(child: ReturnType<typeof spawn>, port: number) {
  let exitObserved = child.exitCode !== null;
  const exited = new Promise<void>((done) => child.once("exit", () => { exitObserved = true; done(); }));
  if (child.exitCode === null) {
    child.kill();
    let timeout: NodeJS.Timeout | undefined;
    try {
      await Promise.race([exited, new Promise<void>((_, fail) => { timeout = setTimeout(() => fail(new Error("The isolated Daily dashboard did not stop during teardown.")), 5000); })]);
    } finally {
      if (timeout) clearTimeout(timeout);
    }
  }
  if (!exitObserved) throw new Error("The isolated Daily dashboard process is still running after teardown.");
  const released = createServer();
  await new Promise<void>((done, fail) => released.once("error", fail).listen(port, "127.0.0.1", done));
  await new Promise<void>((done, fail) => released.close((error) => error ? fail(error) : done()));
}
async function isolatedDailyServer() {
  const fixtureParent = resolve("../artifacts/dashboard-e2e/daily-real-write-" + randomUUID());
  const fixtureRoot = join(fixtureParent, "fixture");
  await mkdir(fixtureParent, { recursive: true });
  const smoke = resolve("../tools/WorkLog.Smoke/bin/Release/net10.0/WorkLog.Smoke.dll");
  const generated = spawnSync("dotnet", [smoke, "--dashboard-fixture", fixtureRoot], { encoding: "utf8", windowsHide: true });
  if (generated.status !== 0) {
    await rm(fixtureParent, { recursive: true, force: true });
    throw new Error("Could not create the isolated Daily acceptance fixture.");
  }
  const info = JSON.parse(await readFile(join(fixtureRoot, "fixture.json"), "utf8")) as Awaited<ReturnType<typeof fixture>>;
  const executable = process.env["WORKLOG_DASHBOARD_EXECUTABLE"] ?? resolve("../src/WorkLog/bin/Release/net10.0/worklog.dll");
  const port = await unusedPort();
  const serverBase = `http://127.0.0.1:${port}`;
  const launchLog = join(fixtureParent, "editor-launches.log");
  const isDll = executable.toLowerCase().endsWith(".dll");
  const environment: NodeJS.ProcessEnv = { ...process.env, WORKLOG_DASHBOARD_TEST_LAUNCH_LOG: launchLog };
  if (process.env["WORKLOG_DASHBOARD_NO_NODE"] === "true") {
    environment.PATH = (process.env.PATH ?? "").split(process.platform === "win32" ? ";" : ":").filter((directory) =>
      !["node.exe", "node", "openspec.cmd", "openspec"].some((name) => existsSync(join(directory, name))),
    ).join(process.platform === "win32" ? ";" : ":");
    if (!isDll) {
      environment.DOTNET_ROOT = join(fixtureParent, "no-installed-dotnet");
      environment.DOTNET_ROOT_X64 = environment.DOTNET_ROOT;
      environment.DOTNET_MULTILEVEL_LOOKUP = "0";
      environment.DOTNET_BUNDLE_EXTRACT_BASE_DIR = join(fixtureParent, "bundle-extraction");
      await mkdir(environment.DOTNET_BUNDLE_EXTRACT_BASE_DIR, { recursive: true });
    }
  }
  const args = ["dashboard", "--no-open", "--port", String(port), "--data-dir", info.data, "--repository", info.repo];
  const child = spawn(isDll ? "dotnet" : executable, isDll ? [executable, ...args] : args, {
    cwd: resolve(".."), env: environment, windowsHide: true, stdio: "ignore",
  });
  try {
    let ready = false;
    for (let attempt = 0; attempt < 150; attempt++) {
      if (child.exitCode !== null) break;
      try {
        if ((await fetch(serverBase + "/health")).ok) { ready = true; break; }
      } catch { /* server is still starting */ }
      await new Promise((done) => setTimeout(done, 100));
    }
    if (!ready) throw new Error("The isolated Daily acceptance dashboard did not become ready.");
    return {
      info: { ...info, executable, launchLog, baseUrl: serverBase },
      baseUrl: serverBase,
      close: async () => {
        try {
          await stopIsolatedServer(child, port);
        } catch {
          throw new Error(`Could not stop the isolated Daily dashboard on port ${port}; its fixture was retained at ${fixtureParent} to protect possible live journal data.`);
        }
        await rm(fixtureParent, { recursive: true, force: true });
      },
    };
  } catch (error) {
    try {
      await stopIsolatedServer(child, port);
    } catch {
      throw new Error(`Could not stop the isolated Daily dashboard on port ${port}; its fixture was retained at ${fixtureParent} to protect possible live journal data.`);
    }
    await rm(fixtureParent, { recursive: true, force: true });
    throw error;
  }
}
// Writes the preferences document out of band. A loaded page holds an older copy and saves it back 350 ms after its last change,
// which would overwrite this write, so the page is left first (dropping that pending timer) and the write is read back.
async function putState(page: Page, data: object) {
  await page.goto("about:blank");
  const response = await page.request.put("/api/state", { data });
  expect(response.status()).toBe(204);
  const stored = await (await page.request.get("/api/state")).json();
  expect(stored).toMatchObject(JSON.parse(JSON.stringify(data)));
}
async function openTimeline(page: Page, useDefaultDocument = false) {
  const card = page.locator(".change-card").filter({
    has: page.getByRole("button", { name: "Activity timeline", exact: true }),
  }).first();
  await card.getByRole("button", {
    name: useDefaultDocument ? "Activity timeline" : /^Design/,
    exact: useDefaultDocument,
  }).click();
  // The change exists in the checkout and its worktree, so its one card opens the copy chooser first.
  await expect(page.getByRole("region", { name: "Choose document source" })).toBeVisible();
  await page.locator(".copy-choice .work-list-item").filter({ hasNotText: /worktree/ }).first().click();
  await expect(
    page.getByRole("dialog", { name: "OpenSpec document reader" }),
  ).toBeVisible();
  await expect(page.locator(".markdown h1")).toHaveText("Activity timeline");
}
function trackTimelineReads(page: Page) {
  const requiredPaths = new Set(["/api/document", "/api/reviews"]);
  const started = new Map<string, number>();
  const completed = new Map<string, number>();
  const pending = new Set<string>();
  const requestIds = new Map<Request, string>();
  const failures: string[] = [];
  const pathFor = (request: Request) => {
    const path = new URL(request.url()).pathname;
    return request.method() === "POST" && requiredPaths.has(path) ? path : null;
  };
  const onRequest = (request: Request) => {
    const path = pathFor(request);
    if (!path) return;
    const id = `${path}:${started.get(path) ?? 0}`;
    started.set(path, (started.get(path) ?? 0) + 1);
    pending.add(id);
    requestIds.set(request, id);
  };
  const onFinished = (request: Request) => {
    const id = requestIds.get(request);
    if (!id) return;
    pending.delete(id);
    const path = pathFor(request);
    if (path) completed.set(path, (completed.get(path) ?? 0) + 1);
  };
  const onFailed = (request: Request) => {
    const id = requestIds.get(request);
    if (!id) return;
    pending.delete(id);
    failures.push(`${pathFor(request) ?? "relevant read"}: ${request.failure()?.errorText ?? "request failed"}`);
  };
  page.on("request", onRequest);
  page.on("requestfinished", onFinished);
  page.on("requestfailed", onFailed);
  return {
    async waitForReadsAndLive() {
      await expect.poll(async () => {
        if (failures.length) throw new Error(`Timeline read failed before archive: ${failures.join("; ")}`);
        const readsFinished = [...requiredPaths].every(path => (started.get(path) ?? 0) > 0 &&
          (completed.get(path) ?? 0) === started.get(path));
        if (!readsFinished || pending.size > 0) return false;
        return page.locator(".live-indicator").evaluateAll(nodes => nodes.length > 0 && nodes.every(node => {
          const box = node.getBoundingClientRect();
          return node.getAttribute("data-state") === "live" && box.width > 0 && box.height > 0 &&
            getComputedStyle(node).visibility !== "hidden";
        }));
      }, { timeout: 15_000, message: "document and review reads must finish before the page is reconciled Live" }).toBe(true);
      if (failures.length || pending.size > 0) throw new Error("Timeline API reads changed after the Live readiness check.");
    },
    dispose() {
      page.off("request", onRequest);
      page.off("requestfinished", onFinished);
      page.off("requestfailed", onFailed);
    },
  };
}
async function captureArchiveLockProbe(info: Awaited<ReturnType<typeof fixture>>, testInfo: TestInfo) {
  const configuredPath = process.env["WORKLOG_C06_ARCHIVE_LOCK_PROBE"];
  if (!configuredPath) return;
  const expectedPaths = [
    resolve("artifacts/realtime-dashboard-updates/archive-lock-probe.ps1"),
    resolve("../artifacts/realtime-dashboard-updates/archive-lock-probe.ps1"),
  ];
  const candidates = [resolve(configuredPath), resolve("..", configuredPath)];
  const probePath = candidates.find(candidate => expectedPaths.includes(candidate) && existsSync(candidate));
  const evidencePath = testInfo.outputPath("archive-lock-probe.json");
  const evidence: Record<string, unknown> = { probe: "archive-lock-probe.ps1", state: "not-run" };
  try {
    if (!probePath) throw new Error("WORKLOG_C06_ARCHIVE_LOCK_PROBE must identify the ignored archive-lock-probe.ps1 script.");
    const healthResponse = await fetch(base + "/health");
    if (!healthResponse.ok) throw new Error("Dashboard health returned " + healthResponse.status + " during the archive failure probe.");
    const health = await healthResponse.json() as { pid?: number };
    if (!Number.isInteger(health.pid) || health.pid! < 1)
      throw new Error("Dashboard health did not provide a valid pid for the lock probe.");
    const targetDirectory = join(info.repo, "openspec/changes/activity-timeline");
    const probe = spawnSync("powershell.exe", [
      "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", probePath,
      "-WorkspaceRoot", info.repo, "-TargetDirectory", targetDirectory, "-DashboardPid", String(health.pid),
    ], { encoding: "utf8", windowsHide: true, timeout: 10_000 });
    evidence.state = probe.error ? "probe-process-error" : probe.status === 0 ? "completed" : "probe-failed";
    evidence.exitCode = probe.status;
    evidence.errorCode = (probe.error as NodeJS.ErrnoException | undefined)?.code ?? null;
    evidence.stdout = probe.stdout?.trim() ?? "";
    evidence.stderr = probe.stderr?.trim() ?? "";
    if (probe.status === 0) {
      try { evidence.result = JSON.parse(probe.stdout); } catch { /* Preserve non-JSON output as raw ignored evidence. */ }
    }
  } catch (error) {
    evidence.state = "probe-invocation-error";
    evidence.error = error instanceof Error ? error.message : String(error);
  }
  try {
    await mkdir(dirname(evidencePath), { recursive: true });
    await writeFile(evidencePath, JSON.stringify(evidence, null, 2) + "\n");
  } catch (error) {
    console.error("Could not persist archive lock probe evidence:", error);
  }
}
async function renderPaletteAndMeasure(page: Page, theme: "dark" | "light") {
  return page.evaluate((activeTheme) => {
    const keys = ["teal", "blue", "sand", "green", "rose", "violet", "aqua", "amber"];
    const board = document.querySelector<HTMLElement>(".board")!;
    document.querySelector(".repository-palette-evidence")?.remove();
    document.documentElement.dataset.theme = activeTheme;
    const source = board.querySelector<HTMLElement>(".board-card")!;
    const evidence = document.createElement("section");
    evidence.className = "repository-palette-evidence";
    evidence.setAttribute("aria-hidden", "true");
    evidence.innerHTML = "<h3>Repository palette rendering evidence</h3>";
    const grid = document.createElement("div");
    grid.className = "repository-palette-grid";
    for (const key of keys) {
      const card = source.cloneNode(true) as HTMLElement;
      card.setAttribute("data-repository-color", key);
      card.dataset.paletteCase = key;
      card.querySelectorAll<HTMLElement>(".repo-dot").forEach((dot) => dot.setAttribute("data-repository-color", key));
      const title = card.querySelector<HTMLElement>(".change-title");
      if (title) title.textContent = "Repository · " + key;
      grid.append(card);
    }
    const shared = source.cloneNode(true) as HTMLElement;
    shared.removeAttribute("data-repository-color");
    shared.dataset.paletteCase = "shared";
    const sharedRepositories = shared.querySelector<HTMLElement>(".card-repository")!;
    sharedRepositories.replaceChildren();
    for (const [index, label] of ["repository one", "repository two"].entries()) {
      const item = document.createElement("span");
      item.className = "card-repository-item";
      const dot = document.createElement("span");
      dot.className = "repo-dot";
      dot.dataset.repositoryColor = keys[index];
      dot.setAttribute("aria-hidden", "true");
      item.append(dot, document.createTextNode(label));
      sharedRepositories.append(item);
    }
    const sharedTitle = shared.querySelector<HTMLElement>(".change-title");
    if (sharedTitle) sharedTitle.textContent = "Shared repositories · neutral footer";
    grid.append(shared);
    const missing = source.cloneNode(true) as HTMLElement;
    missing.removeAttribute("data-repository-color");
    missing.dataset.paletteCase = "missing";
    missing.querySelector<HTMLElement>(".card-repository")!.textContent = "Repository identity unavailable";
    const missingTitle = missing.querySelector<HTMLElement>(".change-title");
    if (missingTitle) missingTitle.textContent = "Missing repository identity · neutral footer";
    grid.append(missing);
    evidence.append(grid);
    board.after(evidence);
    const style = document.createElement("style");
    style.textContent = ".repository-palette-evidence{margin:24px 0;padding:16px;border:1px dashed var(--border);border-radius:8px}.repository-palette-evidence h3{margin:0 0 12px;font-size:13px}.repository-palette-grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px}.repository-palette-grid>.board-card{min-width:0}@media(max-width:760px){.repository-palette-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}";
    style.dataset.paletteEvidence = "true";
    evidence.append(style);
    const rgb = (value: string) => value.match(/[\d.]+/g)!.slice(0, 3).map(Number);
    const luminance = (value: string) => {
      const channels = rgb(value).map((channel) => {
        const normalized = channel / 255;
        return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
      });
      return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
    };
    const measureFooter = (footer: HTMLElement) => {
      const background = getComputedStyle(footer).backgroundColor;
      const ratios = Array.from(footer.querySelectorAll<HTMLElement>("*"))
        .filter((text) => (text.textContent ?? "").trim().length > 0)
        .map((text) => {
          const foreground = getComputedStyle(text).color;
          const light = Math.max(luminance(foreground), luminance(background));
          const dark = Math.min(luminance(foreground), luminance(background));
          return (light + 0.05) / (dark + 0.05);
        });
      return { background, minimum: Math.min(...ratios) };
    };
    const paletteMinimum = Math.min(...keys.map((key) => measureFooter(grid.querySelector<HTMLElement>(`[data-palette-case="${key}"] .card-footer`)!).minimum));
    const sharedFooter = measureFooter(grid.querySelector<HTMLElement>('[data-palette-case="shared"] .card-footer')!);
    const missingFooter = measureFooter(grid.querySelector<HTMLElement>('[data-palette-case="missing"] .card-footer')!);
    const neutralReference = document.createElement("div");
    neutralReference.className = "card-footer";
    document.body.append(neutralReference);
    const expectedNeutralBackground = getComputedStyle(neutralReference).backgroundColor;
    neutralReference.remove();
    return {
      keys,
      minimum: paletteMinimum,
      sharedMinimum: sharedFooter.minimum,
      missingMinimum: missingFooter.minimum,
      sharedBackground: sharedFooter.background,
      missingBackground: missingFooter.background,
      expectedNeutralBackground,
    };
  }, theme);
}
test.describe.configure({ mode: "serial" });

test("opens design first, searches across scope, and renders without remote resources", async ({
  page,
}, testInfo) => {
  const remote: string[] = [];
  const errors: string[] = [];
  page.on("request", (r) => {
    if (!r.url().startsWith(base) && !r.url().startsWith("data:"))
      remote.push(r.url());
  });
  page.on("pageerror", (e) => errors.push(e.message));
  await authorize(page);
  await expect(page.locator(".change-card")).toHaveCount(3);
  // The footer names the running version: the plain version, or the development label whose first token is the bare version.
  await expect(page.locator(".page-footer .version-label")).toHaveText(/^\d+\.\d+\.\d+( dev .+)?$/);
  if (process.env["WORKLOG_DASHBOARD_NO_NODE"] === "true")
    await expect(page.locator(".change-card .card-footer").first()).toContainText(
      "Task progress unavailable",
    );
  await page.screenshot({
    path: testInfo.outputPath("ongoing.png"),
    fullPage: true,
  });
  await openTimeline(page, true);
  await expect(page.locator(".document-current strong")).toHaveText("design.md");
  await expect(page.locator(".markdown table")).toBeVisible();
  await expect(page.locator(".markdown img")).toHaveCount(0);
  await page.screenshot({ path: testInfo.outputPath("design.png") });
  await page.getByRole("button", { name: "Close document reader" }).click();
  const search = page.getByRole("textbox", {
    name: "Search OpenSpec documents",
  });
  await search.fill("explicit period");
  await expect(page.locator(".search-result")).toHaveCount(0);
  await page
    .getByRole("button", { name: "All repositories", exact: true })
    .click();
  await expect(page.locator(".search-result")).toHaveCount(1);
  await page.locator(".search-result").click();
  await expect(page.locator(".markdown h1")).toHaveText("Shipping reports");
  expect(remote).toEqual([]);
  expect(errors).toEqual([]);
  await page.goBack();
  await expect(
    page.getByRole("dialog", { name: "OpenSpec document reader" }),
  ).toHaveCount(0);
  await page.goForward();
  await expect(page.locator(".markdown h1")).toHaveText("Shipping reports");
});

test("defaults to dark, serves a favicon, and remembers the light theme", async ({ page }, testInfo) => {
  await authorize(page);
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  expect(await page.locator("html").evaluate(el => getComputedStyle(el).colorScheme)).toBe("dark");
  const icon = await page.request.get("/favicon.svg");
  expect(icon.status()).toBe(200);
  expect(icon.headers()["content-type"]).toContain("image/svg+xml");
  await page.screenshot({ path: testInfo.outputPath("dark-ongoing.png"), fullPage: true });
  await openTimeline(page);
  expect(await page.locator(".reader").evaluate(el => getComputedStyle(el).backgroundColor)).toBe("rgb(21, 34, 45)");
  await page.screenshot({ path: testInfo.outputPath("dark-reader.png") });
  await page.getByRole("button", { name: "Close document reader" }).click();
  let saved = page.waitForResponse(r => r.url().endsWith("/api/state") && r.request().method() === "PUT" && r.request().postDataJSON().theme === "light");
  await page.getByRole("button", { name: "Switch to light theme" }).click();
  expect((await saved).ok()).toBe(true);
  await page.reload();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "light");
  expect(await page.locator("html").evaluate(el => getComputedStyle(el).colorScheme)).toBe("light");
  await page.screenshot({ path: testInfo.outputPath("light-ongoing.png"), fullPage: true });
  await openTimeline(page);
  expect(await page.locator(".reader").evaluate(el => getComputedStyle(el).backgroundColor)).toBe("rgb(255, 255, 255)");
  await page.screenshot({ path: testInfo.outputPath("light-reader.png") });
  await page.getByRole("button", { name: "Close document reader" }).click();
  saved = page.waitForResponse(r => r.url().endsWith("/api/state") && r.request().method() === "PUT" && r.request().postDataJSON().theme === "dark");
  await page.getByRole("button", { name: "Switch to dark theme" }).click();
  await saved;
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
});

test("shows the connection status inside the open reader without overflow at phone, tablet and desktop widths", async ({ page }) => {
  await authorize(page);
  for (const theme of ["dark", "light"]) {
    await page.evaluate(value => document.documentElement.setAttribute("data-theme", value), theme);
    for (const width of [390, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      if (!(await readerDialog(page).count())) await openTimeline(page);
      const status = page.getByTestId("reader-live-status");
      await expect(status).toBeVisible();
      await expect(status).toContainText(/Live|Reconnecting|Updates paused/);
      const overflow = await page.locator(".reader-meta").evaluate(el => el.scrollWidth - el.clientWidth);
      expect(overflow, `${theme} ${width}px reader-meta overflow`).toBeLessThanOrEqual(0);
      await expect(page.locator("[role=status][data-state]")).toHaveCount(1);
    }
    await closeReader(page);
  }
});

test("switches documents during a live catalog refresh and ignores slower earlier selections", async ({ page }, testInfo) => {
  await authorize(page);
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await openTimeline(page);
  const info = await fixture();
  const externalFile = join(info.repo, "openspec/changes/activity-timeline/tasks.md");
  const externalOriginal = await readFile(externalFile, "utf8");
  let releaseCatalog!: () => void, catalogStarted!: () => void;
  const catalogHeld = new Promise<void>(resolve => releaseCatalog = resolve);
  const catalogReady = new Promise<void>(resolve => catalogStarted = resolve);
  await page.route("**/api/catalog", async route => {
    const response = await route.fetch();
    catalogStarted();
    await catalogHeld;
    await route.fulfill({ response });
  }, { times: 1 });
  let releaseDocument!: () => void, documentStarted!: () => void;
  let heldPath = "proposal.md";
  let documentHeld = new Promise<void>(resolve => releaseDocument = resolve);
  let documentReady = new Promise<void>(resolve => documentStarted = resolve);
  await page.route("**/api/document", async route => {
    if (route.request().postDataJSON().path !== heldPath) return route.continue();
    heldPath = "";
    const release = documentHeld;
    const response = await route.fetch();
    documentStarted();
    await release;
    await route.fulfill({ response });
  });
  try {
    // An external file change makes the host publish a revision, and the open page refreshes its catalog without polling.
    await writeFile(externalFile, externalOriginal + "\nExternal live-refresh note.\n");
    await catalogReady;
    await page.getByRole("button", { name: "proposal.md", exact: true }).click();
    await documentReady;
    const catalogResponse = page.waitForResponse(r => r.url().endsWith("/api/catalog"));
    releaseCatalog();
    await catalogResponse;
    await page.evaluate(() => new Promise(requestAnimationFrame));
    releaseDocument();
    await expect(page.locator(".document-current strong")).toHaveText("proposal.md");
    await expect(page.locator(".markdown h1")).toHaveText("Proposal");
    heldPath = "tasks.md";
    documentHeld = new Promise<void>(resolve => releaseDocument = resolve);
    documentReady = new Promise<void>(resolve => documentStarted = resolve);
    await page.getByRole("button", { name: "tasks.md", exact: true }).click();
    await documentReady;
    await page.getByRole("button", { name: "specs/work-dashboard/spec.md", exact: true }).click();
    await expect(page.locator(".markdown h1")).toHaveText("work-dashboard specification");
    releaseDocument();
    for (const documentPath of ["specs/openspec-browser/spec.md", "tasks.md", "proposal.md", "design.md"]) {
      await page.getByRole("button", { name: documentPath, exact: true }).click();
      await expect(page.locator(".document-current strong")).toHaveText(documentPath);
    }
    await expect(page.locator(".markdown h1")).toHaveText("Activity timeline");
    await expect(page.getByText("Reading document...", { exact: true })).toHaveCount(0);
    await page.screenshot({ path: testInfo.outputPath("dark-reader.png") });
  } finally {
    releaseCatalog();
    releaseDocument();
    await page.unroute("**/api/document");
    await writeFile(externalFile, externalOriginal);
  }
});

test("retains review text across refresh, rejects stale blocks and writes review markers", async ({
  page,
}) => {
  await authorize(page);
  await page
    .getByRole("button", { name: "All repositories", exact: true })
    .click();
  await openTimeline(page);
  const paragraph = page
    .locator(".markdown p")
    .filter({ hasText: "A timeline connects recorded moments" });
  await paragraph.click();
  const comment = page.getByRole("textbox", { name: "Review comment" });
  await comment.fill("Keep the elapsed span explicit --> please.");
  const info = await fixture();
  const file = join(info.repo, "openspec/changes/activity-timeline/design.md");
  let text = await readFile(file, "utf8");
  await writeFile(
    file,
    text.replace(
      "A timeline connects recorded moments",
      "A timeline now connects recorded moments",
    ),
  );
  await expect(
    page.locator(".markdown p").filter({ hasText: "A timeline now connects" }),
  ).toBeVisible();
  await expect(comment).toHaveValue(
    "Keep the elapsed span explicit --> please.",
  );
  await page.getByRole("button", { name: /^Save comment/ }).click();
  await expect(page.locator(".comment-status")).toContainText(
    /changed|updated/,
  );
  expect(await readFile(file, "utf8")).not.toContain("Comment:\nKeep");
  await page
    .locator(".markdown p")
    .filter({ hasText: "A timeline now connects" })
    .click();
  await page.getByRole("button", { name: /^Save comment/ }).click();
  await expect(page.locator(".review-note")).toContainText(
    "Keep the elapsed span explicit --> please.",
  );
  expect(await readFile(file, "utf8")).toContain(
    "Comment:\nKeep the elapsed span explicit -&#45;> please.\n-->",
  );
});

test("edits referenced notes in both views and deletes only after confirmation", async ({ page }, testInfo) => {
  await authorize(page);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await openTimeline(page);
  const info = await fixture();
  const file = join(info.repo, "openspec/changes/activity-timeline/design.md");
  const original = await readFile(file, "utf8");
  const id = original.match(/<!-- REVIEW: id=([a-f0-9]{32})/)![1];
  let note = page.locator(".review-note").filter({ hasText: "Keep the elapsed span explicit" });
  await expect(note.locator(".review-reference")).toContainText("A timeline now connects recorded moments");
  await note.getByRole("button", { name: "Edit review comment", exact: true }).click();
  const comment = page.getByRole("textbox", { name: "Review comment" });
  await expect(comment).toHaveValue("Keep the elapsed span explicit --> please.");
  await comment.fill("Draft retained between documents");
  await page.getByRole("button", { name: "tasks.md", exact: true }).click();
  await expect(page.locator(".document-current strong")).toHaveText("tasks.md");
  await page.getByRole("button", { name: "design.md", exact: true }).click();
  await expect(comment).toHaveValue("Draft retained between documents");
  await page.getByRole("button", { name: "Cancel edit", exact: true }).click();
  expect(await readFile(file, "utf8")).toBe(original);
  await note.getByRole("button", { name: "Edit review comment", exact: true }).focus();
  await page.keyboard.press("Enter");
  await comment.fill("Revised feedback --> with a clear reference.");
  await page.getByRole("button", { name: /^Save changes/ }).click();
  note = page.locator(".review-note").filter({ hasText: "Revised feedback" });
  await expect(note).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath("referenced-review.png") });
  expect(await readFile(file, "utf8")).toContain("<!-- REVIEW: id=" + id);
  expect(await readFile(file, "utf8")).toContain("Target: preceding passage\nQuote: A timeline now connects recorded moments");
  await page.getByRole("button", { name: "Source", exact: true }).click();
  await note.getByRole("button", { name: "Edit review comment", exact: true }).click();
  await comment.fill("Edited from source view");
  await page.getByRole("button", { name: /^Save changes/ }).click();
  await expect(page.locator(".source-text")).toContainText("Edited from source view");
  note = page.locator(".review-note").filter({ hasText: "Edited from source view" });
  await note.getByRole("button", { name: "Delete review comment", exact: true }).click();
  await expect(page.getByRole("alertdialog", { name: "Delete review comment?" })).toBeVisible();
  await page.getByRole("button", { name: "Keep comment", exact: true }).click();
  expect(await readFile(file, "utf8")).toContain("Edited from source view");
  await note.getByRole("button", { name: "Delete review comment", exact: true }).click();
  await page.getByRole("button", { name: "Delete comment", exact: true }).click();
  await expect(note).toHaveCount(0);
  const deleted = await readFile(file, "utf8");
  expect(deleted).not.toContain(id);
  expect(deleted).toContain("A timeline now connects recorded moments");
  await page.getByRole("button", { name: "Read", exact: true }).click();
});

test("edits legacy notes and retains drafts when a marker changes or disappears", async ({ page }) => {
  await authorize(page);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await page.getByRole("button", { name: "Inline review comments", exact: true }).click();
  const info = await fixture();
  const file = join(info.repo, "openspec/changes/inline-review-comments/design.md");
  const legacy = "<!-- REVIEW: Keep comments beside their source passage. -->";
  let note = page.locator(".review-note").filter({ hasText: "Keep comments beside" });
  await note.getByRole("button", { name: "Edit review comment", exact: true }).click();
  const comment = page.getByRole("textbox", { name: "Review comment" });
  await comment.fill("My unsaved revision");
  await writeFile(file, (await readFile(file, "utf8")).replace(legacy, "<!-- REVIEW: Agent updated the comment -->"));
  await expect(page.locator(".review-note p")).toHaveText("Agent updated the comment");
  await page.getByRole("button", { name: /^Save changes/ }).click();
  await expect(page.locator(".comment-status")).toContainText("changed");
  await expect(comment).toHaveValue("My unsaved revision");
  expect(await readFile(file, "utf8")).not.toContain("My unsaved revision");
  await page.getByRole("button", { name: "Cancel edit", exact: true }).click();
  note = page.locator(".review-note").filter({ hasText: "Agent updated the comment" });
  await note.getByRole("button", { name: "Edit review comment", exact: true }).click();
  await comment.fill("Updated legacy feedback");
  await page.getByRole("button", { name: /^Save changes/ }).click();
  note = page.locator(".review-note").filter({ hasText: "Updated legacy feedback" });
  await expect(note.locator(".review-reference")).toContainText("Write feedback where the agent already reads the design.");
  const saved = await readFile(file, "utf8");
  expect(saved).toContain("<!-- REVIEW: id=");
  await note.getByRole("button", { name: "Delete review comment", exact: true }).click();
  const without = saved.replace(/<!-- REVIEW: id=[\s\S]*?-->/, "");
  await writeFile(file, without);
  await page.getByRole("button", { name: "Delete comment", exact: true }).click();
  await expect(page.locator(".comment-status")).toContainText(/changed|removed/);
  expect(await readFile(file, "utf8")).toBe(without);
});

test("shows factual history, continuation and observation details", async ({
  page,
}, testInfo) => {
  await authorize(page);
  await page
    .getByRole("button", { name: "All repositories", exact: true })
    .click();
  await page.getByRole("navigation", { name: "Main navigation" }).getByRole("button", { name: "Work history" })
    .click();
  await expect(
    page.getByRole("heading", { name: "Work over time" }),
  ).toBeVisible();
  await expect(page.locator(".timeline-row")).toHaveCount(50);
  await page.getByRole("button", { name: "Load more work items" }).click();
  await expect(page.locator(".timeline-row")).toHaveCount(74);
  await page.screenshot({
    path: testInfo.outputPath("history.png"),
    fullPage: true,
  });
  await page
    .locator(".timeline-row .row-label")
    .filter({ hasText: "WL-190" })
    .click();
  await expect(
    page.getByRole("dialog", { name: "Recorded work detail" }),
  ).toBeVisible();
  await expect(page.locator(".observation")).toHaveCount(100);
  await page.getByRole("button", { name: "Load more observations" }).click();
  await expect(page.locator(".observation")).toHaveCount(125);
  await expect(page.locator(".detail-footnote")).toContainText("125 of 125");
});

test("keeps links working after archiving and honors repeat no-open launches", async ({
  page,
}, testInfo) => {
  await authorize(page);
  await page
    .getByRole("button", { name: "All repositories", exact: true })
    .click();
  const info = await fixture();
  const designFile = join(info.repo, "openspec/changes/activity-timeline/design.md");
  await writeFile(designFile, (await readFile(designFile, "utf8")) + "\n<!-- REVIEW: Archive-safe note -->\n");
  const timelineReads = trackTimelineReads(page);
  try {
    await openTimeline(page);
    await expect(page.getByRole("button", { name: "Edit review comment", exact: true })).toHaveCount(1);
    await timelineReads.waitForReadsAndLive();
  } finally {
    timelineReads.dispose();
  }
  const original = page.url();
  const archive = join(info.repo, "openspec/changes/archive");
  await mkdir(archive, { recursive: true });
  try {
    await rename(
      join(info.repo, "openspec/changes/activity-timeline"),
      join(archive, "2026-10-26-activity-timeline"),
    );
  } catch (renameError) {
    await captureArchiveLockProbe(info, testInfo);
    throw renameError;
  }
  await expect(page.locator(".reader-provenance .badge")).toHaveText(
    "Archived",
  );
  await expect(page.getByRole("button", { name: "Save comment" })).toHaveCount(
    0,
  );
  await expect(page.getByRole("button", { name: "Edit review comment", exact: true })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Delete review comment", exact: true })).toHaveCount(0);
  await page.goto(original);
  await expect(page.locator(".markdown h1")).toHaveText("Activity timeline");
  const priorInstance = (await (await fetch(base + "/health")).json()).instance;
  await writeFile(join(info.root, "restart.request"), String(Date.now()));
  await expect
    .poll(
      async () => {
        try {
          return (await (await fetch(base + "/health")).json()).instance;
        } catch {
          return priorInstance;
        }
      },
      { timeout: 30_000 },
    )
    .not.toBe(priorInstance);
  await page.reload();
  await expect(page.locator(".markdown h1")).toHaveText("Activity timeline");
  await expect(page.locator(".reader-provenance .badge")).toHaveText(
    "Archived",
  );
  const args = [
    "dashboard",
    "--no-open",
    "--port",
    String(testPort),
    "--data-dir",
    info.data,
  ];
  const isDll = info.executable.endsWith(".dll");
  const repeat = spawnSync(
    isDll ? "dotnet" : info.executable,
    isDll ? [info.executable, ...args] : args,
    { encoding: "utf8", windowsHide: true, timeout: 20_000 },
  );
  expect(repeat.status, repeat.stderr).toBe(0);
  expect(repeat.stdout).toContain(base);
  expect((await (await fetch(base + "/health")).json()).port).toBe(testPort);
});

test("remains usable while a separate MCP process logs and queries", async ({
  page,
}) => {
  await authorize(page);
  const info = await fixture();
  const smoke = resolve(
    "../tools/WorkLog.Smoke/bin/Release/net10.0/WorkLog.Smoke.dll",
  );
  const child = spawn(
    "dotnet",
    [smoke, "--dashboard-mcp", info.executable, info.root],
    { windowsHide: true },
  );
  let output = "";
  child.stdout.on("data", (d) => (output += d));
  child.stderr.on("data", (d) => (output += d));
  const finished = new Promise<number | null>((done, reject) => {
    child.on("error", reject);
    child.on("close", done);
  });
  await expect
    .poll(
      () =>
        readFile(join(info.root, "dashboard-mcp.started"), "utf8").catch(
          () => "",
        ),
      { timeout: 20_000 },
    )
    .toBe("ready");
  await page.getByRole("button", { name: "Refresh dashboard" }).click();
  await page
    .getByRole("button", { name: "Inline review comments", exact: true })
    .click();
  await page
    .locator(".markdown p")
    .filter({ hasText: "Write feedback where the agent already reads" })
    .click();
  await page
    .getByRole("textbox", { name: "Review comment" })
    .fill("Review saved while MCP remains connected.");
  expect(child.exitCode).toBeNull();
  await page.getByRole("button", { name: /^Save comment/ }).click();
  await expect(page.locator(".review-note").filter({hasText:"Review saved while MCP remains connected."})).toBeVisible();
  expect(await finished, output).toBe(0);
  expect(output).toContain("DASHBOARD MCP PASSED");
  await page.keyboard.press("Escape");
  await expect(
    page.getByRole("heading", { name: "Ongoing board" }),
  ).toBeVisible();
});

test("copies section and task links with useful missing-target fallbacks", async ({
  page,
  context,
}) => {
  await authorize(page);
  await context.grantPermissions(["clipboard-read", "clipboard-write"]);
  await page
    .getByRole("button", { name: "Inline review comments", exact: true })
    .click();
  await openDocumentOutline(page);
  await page
    .getByRole("button", {
      name: "Copy link to Review after editing",
      exact: true,
    })
    .click();
  const section = await page.evaluate(() => navigator.clipboard.readText());
  expect(section).toContain("anchor=review-after-editing");
  expect(section).not.toContain("connect=");
  await page.goto(section);
  await expect(page.locator(".markdown #review-after-editing")).toBeFocused();
  await page.getByRole("button", { name: "tasks.md", exact: true }).click();
  await openDocumentOutline(page);
  await page
    .getByRole("button", { name: "Copy task link 1.2", exact: true })
    .click();
  const task = await page.evaluate(() => navigator.clipboard.readText());
  const info = await fixture();
  const path = join(
    info.repo,
    "openspec/changes/inline-review-comments/tasks.md",
  );
  const original = await readFile(path, "utf8");
  await writeFile(
    path,
    original.replace("Implement the change", "Implement the revised change"),
  );
  await page.goto(task);
  await expect(page.locator('[id="task-1.2"]')).toContainText("revised change");
  await writeFile(
    path,
    (await readFile(path, "utf8")).replace(
      "- [ ] 1.2 Implement the revised change\n",
      "",
    ),
  );
  await page.goto(task);
  await expect(page.locator(".reader-toast")).toContainText(
    "unavailable or ambiguous",
  );
  await page.keyboard.press("Escape");
  await expect(
    page.getByRole("dialog", { name: "OpenSpec document reader" }),
  ).toHaveCount(0);
});

const savedState = (page: Page) =>
  page.request.get("/api/state").then((r) => r.json() as Promise<any>);
const dayCount = (from: string, through: string) =>
  Math.round((Date.parse(through) - Date.parse(from)) / 86_400_000);

test("Escape leaves the comment field first and closes the reader second", async ({ page }) => {
  await authorize(page);
  await page.getByRole("button", { name: "Repository filters", exact: true }).click();
  const reader = page.getByRole("dialog", { name: "OpenSpec document reader" });
  await expect(reader).toBeVisible();
  await page.locator(".markdown p").filter({ hasText: "Choose one project" }).click();
  const comment = page.getByRole("textbox", { name: "Review comment" });
  await comment.fill("Keep this draft while I check something");
  await comment.press("Escape");
  await expect(reader).toBeVisible();
  await expect(comment).toHaveValue("Keep this draft while I check something");
  await expect(page.locator(".reader")).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(reader).toHaveCount(0);
});

test("a focused history day is temporary and a reload restores the remembered range", async ({ page }) => {
  await authorize(page);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await page.getByRole("navigation", { name: "Main navigation" }).getByRole("button", { name: "Work history" }).click();
  await expect(page.getByRole("heading", { name: "Work over time" })).toBeVisible();
  const saved = page.waitForResponse(
    (r) => r.url().endsWith("/api/state") && r.request().method() === "PUT" && r.request().postDataJSON().history?.range?.days === 7,
  );
  await page.getByRole("button", { name: "Last 7 days", exact: true }).click();
  await saved;
  const from = page.getByLabel("From", { exact: true });
  const through = page.getByLabel("Through", { exact: true });
  await expect.poll(async () => dayCount(await from.inputValue(), await through.inputValue())).toBe(6);
  await page.locator(".daily-item").first().click();
  await expect(page.getByRole("button", { name: "Back to last 7 days" })).toBeVisible();
  await expect.poll(async () => dayCount(await from.inputValue(), await through.inputValue())).toBe(0);
  const focused = await from.inputValue();
  expect(JSON.stringify(await savedState(page))).not.toContain(focused);
  await page.reload();
  await expect(page.getByRole("heading", { name: "Work over time" })).toBeVisible();
  await expect(page.getByRole("button", { name: /^Back to/ })).toHaveCount(0);
  await expect.poll(async () => dayCount(await from.inputValue(), await through.inputValue())).toBe(6);
  const state = await savedState(page);
  expect(state.history.range).toEqual({ kind: "relative", days: 7 });
  expect(state.history).not.toHaveProperty("from");
  expect(state.history).not.toHaveProperty("to");
  const restored = page.waitForResponse(
    (r) => r.url().endsWith("/api/state") && r.request().method() === "PUT" && r.request().postDataJSON().history?.range?.days === 30,
  );
  await page.getByRole("button", { name: "Last 30 days", exact: true }).click();
  await restored;
  await expect.poll(async () => dayCount(await from.inputValue(), await through.inputValue())).toBe(29);
});

test("a relative history range rolls forward when the dashboard stays open for three days", async ({ page }) => {
  await page.clock.install({ time: new Date() });
  const queries: { from: string; to: string }[] = [];
  page.on("request", (r) => {
    // The History query spans weeks; the Ongoing query spans 7 days.
    if (r.url().endsWith("/api/timeline") && Date.parse(r.postDataJSON().to) - Date.parse(r.postDataJSON().from) > 20 * 86_400_000)
      queries.push(r.postDataJSON());
  });
  await authorize(page);
  await page.getByRole("navigation", { name: "Main navigation" }).getByRole("button", { name: "Work history" }).click();
  await expect(page.getByRole("heading", { name: "Work over time" })).toBeVisible();
  const from = page.getByLabel("From", { exact: true });
  const through = page.getByLabel("Through", { exact: true });
  await expect(through).not.toHaveValue("");
  const [startFrom, startThrough] = [await from.inputValue(), await through.inputValue()];
  expect(dayCount(startFrom, startThrough)).toBe(29);
  const shift = (date: string, days: number) => new Date(Date.parse(date + "T12:00:00Z") + days * 86_400_000).toISOString().slice(0, 10);
  queries.length = 0;
  await page.clock.fastForward(3 * 86_400_000);
  await expect(through).toHaveValue(shift(startThrough, 3));
  await expect(from).toHaveValue(shift(startFrom, 3));
  await expect.poll(() => queries.length).toBeGreaterThan(0);
  const last = queries.at(-1)!;
  expect(Math.abs(Date.parse(last.to) - Date.parse(shift(startThrough, 4) + "T00:00:00Z"))).toBeLessThan(86_400_000);
  expect((await savedState(page)).history.range).toEqual({ kind: "relative", days: 30 });
});

test("reading positions stay bounded however many documents were read", async ({ page }) => {
  await authorize(page);
  const seeded: Record<string, number> = {};
  for (let i = 0; i < 150; i++) seeded[`seed-${String(i).padStart(3, "0")}:old:design.md:`] = i;
  await putState(page, { schemaVersion: 1, repositories: null, positions: seeded });
  await page.goto("/ongoing");
  await expect(page.getByRole("heading", { name: "Ongoing board" })).toBeVisible();
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await page.getByRole("button", { name: "Shipping reports", exact: true }).click();
  await expect(page.getByRole("dialog", { name: "OpenSpec document reader" })).toBeVisible();
  const saved = page.waitForResponse(
    (r) => r.url().endsWith("/api/state") && r.request().method() === "PUT"
      && Object.keys(r.request().postDataJSON().positions ?? {}).some((k) => k.includes(":shipping-reports:")),
  );
  await page.locator(".document-scroll").evaluate((el) => el.dispatchEvent(new Event("scroll")));
  const body = (await saved).request().postDataJSON();
  const keys = Object.keys(body.positions);
  expect(keys).toHaveLength(100);
  expect(keys.at(-1)).toContain(":shipping-reports:");
  expect(keys).not.toContain("seed-000:old:design.md:");
  expect(keys).toContain("seed-149:old:design.md:");
  expect((await saved).ok()).toBe(true);
});

test("search reports unavailable sources only for the selected repositories", async ({ page }) => {
  await authorize(page);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await page.getByRole("button", { name: "example/worklog", exact: true }).click();
  await expect(page.getByRole("button", { name: /example\/worklog/ }).first()).toBeVisible();
  const search = page.getByRole("textbox", { name: "Search OpenSpec documents" });
  await search.fill("timeline");
  await expect(page.locator(".search-result").first()).toBeVisible();
  // Only the selected repository is searched, so another repository's unavailable store is not reported.
  await expect(page.locator(".search-panel details.source-notices")).toHaveCount(0);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await expect(page.locator(".search-result").first()).toBeVisible();
  await search.fill("");
});

test("keeps history available when a declared store cannot be resolved",async({page})=>{
  await authorize(page);
  await page.getByRole('button',{name:'All repositories',exact:true}).click();
  await expect(page.getByRole('button',{name:'Local shadow',exact:true})).toHaveCount(0);
  const sources=page.locator('details.source-notices').last();
  await sources.locator('summary').click();
  await expect(sources).toContainText('store-member');
  await expect(sources).toContainText(/store.*(resolved|registered|unavailable)|not registered/i);
  if (process.env["WORKLOG_DASHBOARD_NO_NODE"] !== "true")
    await expect(page.locator('.change-card').filter({hasText:'Real root precedence'})).toHaveCount(1);
  await page.getByRole("navigation", { name: "Main navigation" }).getByRole("button", { name: "Work history" }).click();
  await expect(page.locator('.timeline-row').first()).toBeVisible();
});

/** Writes (or rewrites) the Markdown documents of a change in the isolated fixture. */
async function writeChange(root: string, name: string, files: Record<string, string>) {
  const directory = join(root, "openspec", "changes", name);
  await mkdir(directory, { recursive: true });
  await writeFile(join(directory, ".openspec.yaml"), "schema: spec-driven\ncreated: 2026-10-27\n");
  for (const [file, text] of Object.entries(files)) {
    const target = join(directory, file);
    await mkdir(dirname(target), { recursive: true });
    await writeFile(target, text);
  }
}
const structuredMarker = (id: string, quote: string, comment: string) =>
  `<!-- REVIEW: id=${id}\nTarget: preceding passage\nQuote: ${quote}\nComment:\n${comment}\n-->`;
const panel = (page: Page) => page.locator(".review-inbox");
const cardFor = (page: Page, title: string) =>
  page.locator(".change-card").filter({ has: page.getByRole("button", { name: title, exact: true }) });
const closeReader = (page: Page) => page.getByRole("button", { name: "Close document reader" }).click();
async function openDocumentOutline(page: Page) {
  const outline = page.locator(".document-outline");
  await expect(outline).toBeVisible();
  if (!(await outline.evaluate((element) => (element as HTMLDetailsElement).open)))
    await outline.locator("summary").click();
  await expect.poll(() => outline.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(true);
}

test("lists open comments across two changes, opens one at its note, and follows an agent removing a marker", async ({ page }, testInfo) => {
  const info = await fixture();
  await writeChange(info.repo, "review-inbox-alpha", {
    "design.md": `# Inbox alpha\n\nAlpha passage one.\n\n<!-- REVIEW: E2E alpha first comment -->\n\nAlpha passage two.\n\n${structuredMarker("a1".repeat(16), "Alpha passage two.", "E2E alpha second comment")}\n\n\`\`\`md\n<!-- REVIEW: E2E syntax only, inside a code example -->\n\`\`\`\n`,
  });
  await writeChange(info.otherRepo, "review-inbox-beta", {
    "design.md": `# Inbox beta\n\nBeta passage.\n\n${structuredMarker("b2".repeat(16), "Beta passage.", "E2E beta comment")}\n`,
  });
  await authorize(page);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  const inbox = panel(page);
  await expect(inbox.getByRole("heading", { name: "Open review comments" })).toBeVisible();
  const mine = inbox.locator(".review-entry").filter({ hasText: "E2E " });
  await expect(mine).toHaveCount(3);
  await expect(inbox).not.toContainText("syntax only");
  const alpha = inbox.locator(".review-group").filter({ hasText: "Review inbox alpha" });
  await expect(alpha.locator(".comments-chip")).toHaveText("2 comments");
  await expect(alpha).toContainText("design.md");
  await expect(alpha).toContainText("example/worklog");
  await expect(inbox.locator(".review-group").filter({ hasText: "Review inbox beta" }).locator(".comments-chip")).toHaveText("1 comment");
  await expect(cardFor(page, "Review inbox alpha").locator(".comments-chip")).toHaveText("2 comments");
  await expect(cardFor(page, "Review inbox beta").locator(".comments-chip")).toHaveText("1 comment");
  await expect(cardFor(page, "Review inbox beta")).toContainText("example/bell-tower");
  await page.screenshot({ path: testInfo.outputPath("review-inbox.png"), fullPage: true });

  await inbox.locator(".review-entry").filter({ hasText: "E2E alpha second comment" }).click();
  await expect(page.getByRole("dialog", { name: "OpenSpec document reader" })).toBeVisible();
  const note = page.locator(".review-note").filter({ hasText: "E2E alpha second comment" });
  await expect(note).toBeInViewport();
  await expect(page.locator(".comment-menu summary")).toContainText("2 open comments in this change");
  await expect(page.locator(".comment-menu summary")).toContainText("2 in this document");
  await closeReader(page);

  // The agent addresses the first comment by deleting its marker; the overview and the card follow without a reload.
  const file = join(info.repo, "openspec/changes/review-inbox-alpha/design.md");
  await writeFile(file, (await readFile(file, "utf8")).replace("<!-- REVIEW: E2E alpha first comment -->\n", ""));
  await expect(mine).toHaveCount(2);
  await expect(inbox.locator(".review-entry").filter({ hasText: "E2E alpha first comment" })).toHaveCount(0);
  await expect(alpha.locator(".comments-chip")).toHaveText("1 comment");
  await expect(cardFor(page, "Review inbox alpha").locator(".comments-chip")).toHaveText("1 comment");
});

test("highlights an agent's edit of a read design, lists the addressed comment, and Mark as read clears both", async ({ page }, testInfo) => {
  const info = await fixture();
  const id = "c3".repeat(16);
  const quote = "Keep the reviewer in control of when a change counts as read.";
  await writeChange(info.repo, "review-loop-demo", {
    "design.md": `# Review loop demo\n\n## Context\n\nThe agent drafts this design and the reviewer reads it.\n\n## Decision\n\n${quote}\n\n${structuredMarker(id, quote, "E2E please explain how an unread edit is shown.")}\n\n## Consequences\n\nNothing else changes.\n`,
  });
  await authorize(page);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  const card = cardFor(page, "Review loop demo");
  await expect(card.locator(".comments-chip")).toHaveText("1 comment");
  await card.getByRole("button", { name: /^Design/ }).click();
  await expect(page.locator(".markdown h1")).toHaveText("Review loop demo");
  // First open: the current content is the baseline, so nothing is highlighted.
  await expect(page.locator(".read-banner")).toHaveCount(0);
  await expect(page.locator(".changed-since-read")).toHaveCount(0);
  await closeReader(page);

  const file = join(info.repo, "openspec/changes/review-loop-demo/design.md");
  const edited =
    (await readFile(file, "utf8"))
      .replace(quote, "The reviewer decides when a change counts as read; an edit stays marked until then.")
      .replace(/<!-- REVIEW: id=[\s\S]*?-->\n\n/, "") + "\n## Follow-up\n\nA new section added by the agent.\n";
  await writeFile(file, edited);
  await expect(card.locator(".updated-chip")).toHaveText("Updated since read");
  await expect(card.locator(".comments-chip")).toHaveCount(0);
  await card.getByRole("button", { name: /^Design/ }).click();

  const banner = page.locator(".read-banner");
  await expect(banner).toContainText("Changed since last read");
  const changed = page.locator(".markdown .changed-since-read");
  await expect(changed.filter({ hasText: "The reviewer decides when a change counts as read" })).toHaveCount(1);
  await expect(changed.filter({ hasText: "Follow-up" })).toHaveCount(1);
  await expect(changed.filter({ hasText: "A new section added by the agent." })).toHaveCount(1);
  await expect(changed.filter({ hasText: "Nothing else changes." })).toHaveCount(0);
  await expect(changed.filter({ hasText: "The agent drafts this design" })).toHaveCount(0);
  await expect(page.locator(".markdown .removed-since-read").filter({ hasText: "(1 line)" })).toContainText("Keep the reviewer in control");
  await expect(page.locator(".removed-comments")).toContainText("Removed since last read");
  await page.locator(".removed-comments summary").click();
  await expect(page.locator(".removed-comments")).toContainText("E2E please explain how an unread edit is shown.");
  await expect(page.locator(".removed-comments .review-reference")).toContainText(quote);
  await page.screenshot({ path: testInfo.outputPath("changed-since-read.png") });

  // Keyboard: ] and [ step through the changes.
  await page.locator(".reader").focus();
  await page.keyboard.press("]");
  await expect(banner).toContainText("Change 1 of 2");
  await expect(page.locator(".removed-since-read summary:focus, .changed-since-read:focus")).toHaveCount(1);
  await page.getByRole("button", { name: "Next change", exact: true }).click();
  await expect(banner).toContainText("Change 2 of 2");
  await expect(page.locator("h2.changed-since-read:focus")).toHaveText("Follow-up");
  await page.keyboard.press("[");
  await expect(banner).toContainText("Change 1 of 2");
  await page.getByRole("button", { name: "Source", exact: true }).click();
  await expect(page.locator(".source-text .changed-since-read").first()).toBeVisible();
  await expect(page.locator(".source-text .changed-since-read").filter({ hasText: "A new section added by the agent." })).toHaveCount(1);
  await page.getByRole("button", { name: "Read", exact: true }).click();

  await page.getByRole("button", { name: "Mark as read", exact: true }).click();
  await expect(banner).toHaveCount(0);
  await expect(page.locator(".changed-since-read")).toHaveCount(0);
  await expect(page.locator(".removed-since-read")).toHaveCount(0);
  await expect(page.locator(".removed-comments")).toHaveCount(0);
  await closeReader(page);
  await expect(card.locator(".updated-chip")).toHaveCount(0);
  // The baseline is stored by the server, so a reload keeps it.
  await page.reload();
  await expect(page.getByRole("heading", { name: "Ongoing board" })).toBeVisible();
  await card.getByRole("button", { name: /^Design/ }).click();
  await expect(page.locator(".markdown h1")).toHaveText("Review loop demo");
  await expect(page.locator(".read-banner")).toHaveCount(0);
  await expect(card.locator(".updated-chip")).toHaveCount(0);

  // The user's own comment, saved right after reading, is not a change either.
  await page.locator(".markdown p").filter({ hasText: "Nothing else changes." }).click();
  await page.getByRole("textbox", { name: "Review comment" }).fill("E2E my own note after reading.");
  await page.getByRole("button", { name: /^Save comment/ }).click();
  await expect(page.locator(".review-note").filter({ hasText: "E2E my own note after reading." })).toBeVisible();
  await expect(page.locator(".read-banner")).toHaveCount(0);
  await expect(page.locator(".changed-since-read")).toHaveCount(0);
  await expect(page.locator(".removed-comments")).toHaveCount(0);
  await closeReader(page);
  await expect(card.locator(".updated-chip")).toHaveCount(0);
  await expect(card.locator(".comments-chip")).toHaveText("1 comment");
  await card.getByRole("button", { name: /^Design/ }).click();
  await expect(page.locator(".read-banner")).toHaveCount(0);
  await closeReader(page);
});

test("shows and navigates an honest document-level fallback when a large comparison is incomplete", async ({ page }) => {
  const info = await fixture();
  const lines = (prefix: string) => Array.from({ length: 2100 }, (_, index) => `${prefix} unique line ${index}`).join("\n") + "\n";
  const largeName = "zz-e2e-large-comparison";
  const emptyName = "zz-e2e-empty-comparison";
  await writeChange(info.repo, largeName, { "design.md": `# Large comparison\n\n${lines("baseline")}` });
  await writeChange(info.repo, emptyName, { "design.md": lines("empty baseline") });
  await authorize(page);

  const largeCard = cardFor(page, "Zz e2e large comparison");
  await largeCard.getByRole("button", { name: /^Design/ }).click();
  await expect(page.locator(".markdown h1")).toHaveText("Large comparison");
  await writeFile(join(info.repo, "openspec/changes", largeName, "design.md"), `# Large comparison updated\n\n${lines("replacement")}`);
  await page.getByRole("button", { name: "Refresh document" }).click();
  const banner = page.locator(".read-banner");
  await expect(banner).toContainText("Comparison is incomplete");
  await expect(banner).toContainText("Use Next change");
  const renderedFallback = page.locator(".document-scroll > .markdown.changed-since-read");
  await expect(renderedFallback).toBeVisible();
  await expect(page.getByRole("button", { name: "Next change", exact: true })).toBeEnabled();
  await page.getByRole("button", { name: "Next change", exact: true }).click();
  await expect(renderedFallback).toBeFocused();

  await page.getByRole("button", { name: "Source", exact: true }).click();
  const sourceFallback = page.locator(".document-scroll > .source-text.changed-since-read");
  await expect(sourceFallback).toBeVisible();
  await expect(sourceFallback).toHaveAttribute("tabindex", "-1");
  await page.getByRole("button", { name: "Next change", exact: true }).click();
  await expect(sourceFallback).toBeFocused();
  await closeReader(page);

  const emptyCard = cardFor(page, "Zz e2e empty comparison");
  await emptyCard.getByRole("button", { name: /^Design/ }).click();
  // View mode is reader state and deliberately carries across documents.
  await page.getByRole("button", { name: "Read", exact: true }).click();
  await expect(page.locator(".markdown")).toBeVisible();
  await writeFile(join(info.repo, "openspec/changes", emptyName, "design.md"), "");
  await page.getByRole("button", { name: "Refresh document" }).click();
  await expect(banner).toContainText("Comparison is incomplete");
  const emptyFallback = page.locator(".document-scroll > .markdown.changed-since-read");
  await expect(emptyFallback).toBeVisible();
  await expect(emptyFallback).toHaveText("");
  await expect(page.getByRole("button", { name: "Next change", exact: true })).toBeEnabled();
  await page.getByRole("button", { name: "Next change", exact: true }).click();
  await expect(emptyFallback).toBeFocused();
});

test("lists a comment left behind in an archived change as read-only", async ({ page }, testInfo) => {
  const info = await fixture();
  await writeChange(info.repo, "archive/2026-10-02-review-leftover", {
    "design.md": "# Leftover\n\nAn archived passage.\n\n<!-- REVIEW: E2E leftover from before the archive -->\n",
  });
  await writeChange(info.repo, "review-active-control", {
    "design.md": "# Active control\n\nAn active passage.\n\n<!-- REVIEW: E2E active control comment -->\n",
  });
  await authorize(page);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  const inbox = panel(page);
  await expect(inbox.getByRole("heading", { name: "Open review comments" })).toBeVisible();
  await expect(inbox.locator(".review-entry").filter({ hasText: "E2E active control comment" })).toBeVisible();
  // Active scope is the default, so the archived comment is not listed.
  await expect(inbox).not.toContainText("E2E leftover");
  await inbox.getByRole("button", { name: "Archived", exact: true }).click();
  const entry = inbox.locator(".review-entry").filter({ hasText: "E2E leftover from before the archive" });
  await expect(entry).toBeVisible();
  const group = inbox.locator(".review-group").filter({ hasText: "E2E leftover from before the archive" });
  await expect(group.locator(".badge")).toContainText("Archived");
  await expect(inbox).not.toContainText("E2E active control");
  await page.screenshot({ path: testInfo.outputPath("review-inbox-archived.png"), fullPage: true });
  await entry.click();
  await expect(page.getByRole("dialog", { name: "OpenSpec document reader" })).toBeVisible();
  await expect(page.locator(".reader-provenance .badge")).toHaveText("Archived");
  const note = page.locator(".review-note").filter({ hasText: "E2E leftover from before the archive" });
  await expect(note).toBeVisible();
  await expect(note).toBeInViewport();
  await expect(page.getByRole("button", { name: "Edit review comment", exact: true })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Delete review comment", exact: true })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Save comment" })).toHaveCount(0);
  await closeReader(page);
  await inbox.getByRole("button", { name: "All", exact: true }).click();
  await expect(inbox.locator(".review-entry").filter({ hasText: "E2E leftover" })).toHaveCount(1);
  await expect(inbox.locator(".review-entry").filter({ hasText: "E2E active control" })).toHaveCount(1);
});

const launches = async () =>
  (await readFile((await fixture()).launchLog, "utf8").catch(() => ""))
    .split("\n")
    .filter(Boolean);
const lastLaunch = async () => (await launches()).at(-1);
const archiveNav = (page: Page) =>
  page.getByRole("navigation", { name: "Main navigation" }).getByRole("button", { name: /Archive/ });
const readerDialog = (page: Page) => page.getByRole("dialog", { name: "OpenSpec document reader" });

test("the Archive view lists archives newest first, filters by name, shows activity and leftover comments, and opens read-only", async ({ page }, testInfo) => {
  const info = await fixture();
  await writeChange(info.repo, "archive/2026-10-03-zz-e2e-newest", {
    "design.md": "# Newest\n\nAn archived passage.\n\n<!-- REVIEW: E2E archive leftover -->\n",
  });
  await writeChange(info.repo, "archive/2026-09-15-zz-e2e-older", { "design.md": "# Older\n\nAn older archived passage.\n" });
  await writeChange(info.repo, "archive/zz-e2e-undated", { "design.md": "# Undated\n\nNo date prefix.\n" });
  await authorize(page);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await archiveNav(page).click();
  await expect(page).toHaveURL(/\/archive/);
  const filter = page.getByRole("searchbox", { name: "Filter archived changes" });
  await filter.fill("ZZ-E2E");
  const entries = page.getByTestId("archive-entry");
  await expect(entries).toHaveCount(3);
  await expect(entries.getByTestId("archive-name")).toHaveText(["zz-e2e-newest", "zz-e2e-older", "zz-e2e-undated"]);
  await expect(entries.getByTestId("archive-date")).toHaveText(["Archived 2026-10-03", "Archived 2026-09-15", "Archive date unknown"]);
  await expect(entries.nth(0).getByTestId("archive-comments")).toHaveText("1 comment");
  await expect(entries.nth(1).getByTestId("archive-comments")).toHaveCount(0);
  await expect(entries.nth(0).getByTestId("archive-activity")).toHaveText("No activity recorded");
  await filter.fill("zz-e2e-old");
  await expect(entries).toHaveCount(1);
  await expect(entries.getByTestId("archive-name")).toHaveText("zz-e2e-older");
  await filter.fill("no-such-archive-name");
  await expect(entries).toHaveCount(0);
  await expect(page.locator(".archive-panel .compact-empty")).toContainText("No archived change names contain");
  // Recorded activity is separate from the archive date: the fixture journal records done for update-rollout.
  await filter.fill("update-rollout");
  await expect(entries).toHaveCount(1);
  await expect(entries.getByTestId("archive-date")).toHaveText("Archived 2026-10-01");
  await expect(entries.getByTestId("archive-activity")).toContainText("Recorded done");
  await page.screenshot({ path: testInfo.outputPath("archive-activity.png"), fullPage: true });
  await filter.fill("zz-e2e-newest");
  await entries.getByTestId("archive-name").click();
  await expect(readerDialog(page)).toBeVisible();
  await expect(page.locator(".reader-provenance .badge")).toHaveText("Archived");
  await expect(page.locator(".markdown h1")).toHaveText("Newest");
  await expect(page.getByRole("button", { name: "Save comment" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Edit review comment", exact: true })).toHaveCount(0);
  await closeReader(page);
  await expect(page).toHaveURL(/\/archive$/);
  await expect(filter).toHaveValue("zz-e2e-newest");
});

test("Open in editor from the source view uses the first visible source line, not the scroll distance", async ({ page }) => {
  const info = await fixture();
  const wrapped = "wrapped ".repeat(500).trim();
  await writeChange(info.repo, "zz-e2e-wrapped", {
    "design.md": ["# Wrapped target", "", wrapped, "", ...Array.from({ length: 60 }, (_, i) => "Tail line " + i), ""].join("\n"),
  });
  await authorize(page);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await cardFor(page, "Zz e2e wrapped").getByRole("button", { name: /^Design/ }).click();
  await expect(page.locator(".markdown h1")).toHaveText("Wrapped target");
  await readerDialog(page).getByLabel("Editor for Open in editor").selectOption("vscode");
  await page.getByRole("button", { name: "Source", exact: true }).click();
  await expect(page.locator(".source-text")).toContainText("Tail line 59");
  const before = (await launches()).length;
  const target = "/openspec/changes/zz-e2e-wrapped/design.md";
  // Several wrapped rows into the long paragraph (source line 3): a scroll distance divided by the row height would be far larger.
  const rows = await page.evaluate(() => {
    const scroller = document.querySelector<HTMLElement>(".reader .document-scroll")!;
    const text = scroller.querySelector<HTMLElement>(".source-text")!;
    const row = parseFloat(getComputedStyle(text).lineHeight);
    scroller.scrollTo({ top: text.offsetTop + row * 8, behavior: "instant" });
    return Math.floor(scroller.scrollTop / row);
  });
  expect(rows).toBeGreaterThan(3);
  await page.getByTestId("open-in-editor").click();
  await expect.poll(async () => (await launches()).length).toBe(before + 1);
  expect((await lastLaunch())!.endsWith(target + ":3:1")).toBe(true);
  // Past the paragraph the top edge is on a later source line.
  await page.evaluate(() => {
    const scroller = document.querySelector<HTMLElement>(".reader .document-scroll")!;
    scroller.scrollTo({ top: scroller.scrollHeight, behavior: "instant" });
  });
  await page.getByTestId("open-in-editor").click();
  await expect.poll(async () => (await launches()).length).toBe(before + 2);
  const line = Number((await lastLaunch())!.match(/:(\d+):1$/)![1]);
  expect(line).toBeGreaterThan(5);
  expect(line).toBeLessThanOrEqual(65);
});

test("Open in editor records the expected document and line for a heading, the reader, a search result, and an archived document", async ({ page }, testInfo) => {
  const info = await fixture();
  await writeChange(info.repo, "zz-e2e-editor", {
    "design.md": "# Editor target\n\nFirst paragraph.\n\n## Second section\n\nUnique zzeditor sentence.\n",
  });
  await writeChange(info.repo, "archive/2026-10-04-zz-e2e-editor-old", { "design.md": "# Old editor target\n\nArchived body.\n" });
  await authorize(page);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await cardFor(page, "Zz e2e editor").getByRole("button", { name: /^Design/ }).click();
  await expect(page.locator(".markdown h1")).toHaveText("Editor target");
  await openDocumentOutline(page);
  const before = (await launches()).length;
  const target = "/openspec/changes/zz-e2e-editor/design.md";
  // A heading opens at its own line (zero-based 4, one-based 5).
  await page.getByRole("button", { name: "Open Second section in editor" }).click();
  await expect.poll(async () => (await launches()).length).toBe(before + 1);
  const heading = (await lastLaunch())!;
  expect(heading).toMatch(/^vscode:\/\/file\//);
  expect(heading.endsWith(target + ":5:1")).toBe(true);
  // At the top of the document the first visible block is the title on line 1.
  await page.getByTestId("open-in-editor").click();
  await expect.poll(async () => (await launches()).length).toBe(before + 2);
  expect((await lastLaunch())!.endsWith(target + ":1:1")).toBe(true);
  // The preset selects the scheme.
  await readerDialog(page).getByLabel("Editor for Open in editor").selectOption("cursor");
  await page.getByTestId("open-in-editor").click();
  await expect.poll(async () => (await launches()).length).toBe(before + 3);
  expect((await lastLaunch())!).toMatch(/^cursor:\/\/file\/.*zz-e2e-editor\/design\.md:1:1$/);
  await page.screenshot({ path: testInfo.outputPath("open-in-editor.png") });
  await closeReader(page);
  // A search result opens at the matching line (zero-based 6, one-based 7).
  await page.getByRole("textbox", { name: "Search OpenSpec documents" }).fill("zzeditor");
  await page.getByRole("button", { name: "Open search result in editor" }).first().click();
  await expect.poll(async () => (await launches()).length).toBe(before + 4);
  expect((await lastLaunch())!).toMatch(/^cursor:\/\/file\/.*zz-e2e-editor\/design\.md:7:1$/);
  await page.getByRole("button", { name: "Clear search" }).click();
  // An archived document may be opened too.
  await archiveNav(page).click();
  await page.getByRole("searchbox", { name: "Filter archived changes" }).fill("zz-e2e-editor-old");
  await page.getByTestId("archive-name").click();
  await expect(page.locator(".markdown h1")).toHaveText("Old editor target");
  await readerDialog(page).getByLabel("Editor for Open in editor").selectOption("vscode");
  await page.getByTestId("open-in-editor").click();
  await expect.poll(async () => (await launches()).length).toBe(before + 5);
  expect((await lastLaunch())!).toMatch(/^vscode:\/\/file\/.*2026-10-04-zz-e2e-editor-old\/design\.md:1:1$/);
  // The preset is saved with the other preferences.
  await expect
    .poll(async () => page.evaluate(async () => (await (await fetch("/api/state")).json()).editor), { timeout: 20_000 })
    .toBe("vscode");
  // The server accepts no client path or command: such a request is rejected and nothing more is recorded.
  const count = (await launches()).length;
  const rejected = await page.evaluate(async () =>
    (await fetch("/api/open", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ document: { sourceId: "x", change: "zz-e2e-editor", path: "../../../outside.md" }, line: 0, editor: "powershell" }),
    })).status);
  expect(rejected).toBe(400);
  expect((await launches()).length).toBe(count);
});

async function openHistory(page: Page) {
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await page.getByRole("navigation", { name: "Main navigation" }).getByRole("button", { name: "Work history" }).click();
  await expect(page.getByRole("heading", { name: "Work over time" })).toBeVisible();
  // A filter remembered by an earlier scenario must not leak into this one.
  if (await page.getByRole("textbox", { name: "Ticket filter" }).inputValue()) await page.getByRole("button", { name: "Clear ticket filter" }).click();
  await expect(page.locator(".ticket-filter-active")).toHaveCount(0);
}
const ticketFilter = (page: Page) => page.getByRole("textbox", { name: "Ticket filter" });

test("filters and selects full document paths at reader scale across themes and widths", async ({ page }, testInfo) => {
  const info = await fixture();
  for (const count of [10, 30, 50]) {
    const name = `zz-e2e-reader-navigation-${count}`;
    const files: Record<string, string> = {
      "proposal.md": "# Navigation proposal\n\nReader acceptance fixture.\n",
      "design.md": "# Navigation design\n\nReader acceptance fixture.\n",
      "tasks.md": "# Tasks\n\n- [ ] 1.1 Reader fixture\n",
    };
    const specs = Math.max(2, Math.floor((count - 3) / 2));
    for (let index = 0; index < specs; index++)
      files[`specs/capability-${index}/spec.md`] = `# Capability ${index}\n\nNested specification ${index}.\n`;
    for (let index = 0; Object.keys(files).length < count; index++)
      files[`notes/area-${index}/readme.md`] = `# Note ${index}\n\nOther document ${index}.\n`;
    await writeChange(info.repo, name, files);
  }
  await authorize(page);
  for (const count of [10, 30, 50]) {
    const name = `zz-e2e-reader-navigation-${count}`;
    await page.getByRole("button", { name: `Zz e2e reader navigation ${count}`, exact: true }).click();
    await expect(page.locator(".reader")).toBeVisible();
    await expect(page.locator(".document-picker-desktop .document-link")).toHaveCount(count);
    const target = `specs/capability-${Math.max(2, Math.floor((count - 3) / 2)) - 1}/spec.md`;
    await page.getByRole("textbox", { name: "Filter documents" }).first().fill(target);
    await page.locator(".document-picker-desktop .document-link").filter({ hasText: target }).click();
    await expect(page.locator(".document-current strong")).toHaveText(target);
    await expect(page.locator(".markdown h1")).toHaveText(new RegExp(`Capability ${Math.max(2, Math.floor((count - 3) / 2)) - 1}`));
    await closeReader(page);
  }

  const name = "zz-e2e-reader-navigation-50";
  await page.getByRole("button", { name: "Zz e2e reader navigation 50", exact: true }).click();
  await expect(page.locator(".reader")).toBeVisible();
  for (const width of [390, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    for (const theme of ["dark", "light"] as const) {
      await closeReader(page);
      const toggle = page.getByRole("button", { name: theme === "dark" ? "Switch to dark theme" : "Switch to light theme" });
      if (await toggle.count()) await toggle.click();
      await page.getByRole("button", { name: "Zz e2e reader navigation 50", exact: true }).click();
      await expect(page.locator(".reader")).toBeVisible();
      const target = theme === "dark" ? "specs/capability-2/spec.md" : "notes/area-1/readme.md";
      if (width <= 850) {
        const selector = page.getByRole("button", { name: /Documents ·/ });
        if (!(await page.locator("#document-filter-mobile").isVisible().catch(() => false))) await selector.click();
        await page.locator("#document-filter-mobile").fill(target);
        await page.locator(".document-picker-mobile .document-link").filter({ hasText: target }).click();
      } else {
        await page.locator("#document-filter-desktop").fill(target);
        await page.locator(".document-picker-desktop .document-link").filter({ hasText: target }).click();
      }
      await expect(page.locator(".document-current strong")).toHaveText(target);
      if (width === 1440 && theme === "dark") {
        const filter = page.locator("#document-filter-desktop");
        await filter.fill("");
        const rail = page.locator(".document-navigation");
        await expect(rail.locator(".document-picker-desktop .document-link")).toHaveCount(50);
        await page.locator(".reader-body").screenshot({ path: testInfo.outputPath("reader-nav-1440-dark-all-documents.png") });
        const content = page.locator(".document-scroll");
        const contentPosition = await content.evaluate((element) => element.scrollTop);
        const windowPosition = await page.evaluate(() => window.scrollY);
        await rail.hover();
        await page.mouse.wheel(0, 400);
        await expect.poll(() => rail.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
        expect(await content.evaluate((element) => element.scrollTop)).toBe(contentPosition);
        expect(await page.evaluate(() => window.scrollY)).toBe(windowPosition);
        await rail.evaluate((element) => { element.scrollTop = 0; });
        await filter.fill(target);
        await rail.locator(".document-picker-desktop .document-link").filter({ hasText: target }).click();
        await expect(page.locator(".document-current strong")).toHaveText(target);
      }
      expect(await page.locator(".reader").evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
      await page.locator(".reader-body").screenshot({ path: testInfo.outputPath(`reader-nav-${width}-${theme}.png`) });
      if (width <= 850) {
        await page.getByRole("button", { name: /Documents ·/ }).click();
        const filter = page.locator("#document-filter-mobile");
        await expect(filter).toBeVisible();
        await filter.focus();
        await filter.press("Escape");
        expect(await filter.evaluate((element) => document.activeElement !== element)).toBe(true);
        await expect(page.locator(".reader")).toBeFocused();
        await page.keyboard.press("Escape");
        await expect(page.getByRole("button", { name: /Documents ·/ })).toHaveAttribute("aria-expanded", "false");
        await expect(page.getByRole("button", { name: /Documents ·/ })).toBeFocused();
        await expect(page.locator(".reader")).toBeVisible();
      }
    }
  }
  await closeReader(page);
});

test("keyboard document rail navigation preserves an unsaved draft and the reading position", async ({ page }) => {
  const info = await fixture();
  const name = "zz-e2e-reader-navigation-state";
  const paragraphs = Array.from({ length: 90 }, (_, index) => `Reading passage ${index + 1}. This paragraph gives the design enough height to retain a real reader position.`).join("\n\n");
  await writeChange(info.repo, name, {
    "design.md": `# Reader state design\n\nThis is a selectable passage for a navigation draft.\n\n${paragraphs}\n`,
    "tasks.md": "# Reader state tasks\n\nA separate document to exercise rail navigation.\n",
  });
  await authorize(page);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  const card = cardFor(page, "Zz e2e reader navigation state");
  await expect(card).toBeVisible({ timeout: 20_000 });
  await card.getByRole("button", { name: /^Design/ }).click();
  const reader = page.getByRole("dialog", { name: "OpenSpec document reader" });
  await expect(reader).toBeVisible();
  await page.locator(".markdown p").filter({ hasText: "This is a selectable passage" }).click();
  const comment = page.getByRole("textbox", { name: "Review comment" });
  await comment.fill("Keep this draft while I read the tasks.");

  const savedPosition = page.waitForResponse((response) => {
    if (!response.url().endsWith("/api/state") || response.request().method() !== "PUT") return false;
    const positions = response.request().postDataJSON().positions ?? {};
    return Object.keys(positions).some((key) => key.includes(`:${name}:design.md:`));
  });
  await page.locator(".document-scroll").evaluate((element) => {
    element.scrollTop = 700;
    element.dispatchEvent(new Event("scroll"));
  });
  await savedPosition;
  const originalPosition = await page.locator(".document-scroll").evaluate((element) => element.scrollTop);
  expect(originalPosition).toBeGreaterThan(400);

  const tasksLink = page.locator(".document-picker-desktop .document-link").filter({ hasText: "tasks.md" });
  await tasksLink.focus();
  await tasksLink.press("Enter");
  await expect(page.locator(".markdown h1")).toHaveText("Reader state tasks");
  await expect(tasksLink).toBeFocused();
  const designLink = page.locator(".document-picker-desktop .document-link").filter({ hasText: "design.md" });
  await designLink.focus();
  await designLink.press("Enter");
  await expect(page.locator(".markdown h1")).toHaveText("Reader state design");
  await expect(designLink).toBeFocused();
  await expect(comment).toHaveValue("Keep this draft while I read the tasks.");
  await expect.poll(() => page.locator(".document-scroll").evaluate((element) => element.scrollTop)).toBeGreaterThan(400);
  await closeReader(page);
});

test("links tickets, repositories and branches from recorded facts without navigating", async ({ page, context }) => {
  const external: string[] = [];
  page.on("request", (r) => {
    if (!r.url().startsWith(base) && !r.url().startsWith("data:")) external.push(r.url());
  });
  await authorize(page);
  await openHistory(page);
  await ticketFilter(page).fill("DEP-12");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row")).toHaveCount(2);
  const hrefs = await page.locator(".timeline-row .ticket-link").evaluateAll((anchors) => anchors.map((a) => a.getAttribute("href")).sort());
  expect(hrefs).toEqual(["https://company.atlassian.net/browse/DEP-12", "https://other.atlassian.net/browse/DEP-12"]);
  // Azure DevOps organization/project, a recorded URL, an unrecognized scope, and a local-only repository.
  await ticketFilter(page).fill("4211");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row .ticket-link[href='https://dev.azure.com/contoso/Payments/_workitems/edit/4211']")).toBeVisible();
  await expect(page.locator(".timeline-row .ticket-link")).toHaveCount(1);
  await ticketFilter(page).fill("REC-5");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row .ticket-link[href='https://tracker.example.invalid/browse/REC-5']")).toBeVisible();
  await expect(page.locator(".timeline-row .ticket-link")).toHaveCount(1);
  await ticketFilter(page).fill("UNK-7");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row .ticket-id")).toHaveText("UNK-7");
  await expect(page.locator(".timeline-row")).toHaveCount(1);
  await expect(page.locator(".timeline-row a")).toHaveCount(0);
  // Copy ID works without a link.
  await context.grantPermissions(["clipboard-read", "clipboard-write"], { origin: base });
  await page.locator(".timeline-row").getByRole("button", { name: "Copy ticket ID UNK-7" }).click();
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe("UNK-7");
  // A link opens only after an explicit click, through window.open without the opener.
  await ticketFilter(page).fill("jira:company.atlassian.net:DEP-12");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row .ticket-link[href='https://company.atlassian.net/browse/DEP-12']")).toBeVisible();
  await expect(page.locator(".timeline-row")).toHaveCount(1);
  await page.evaluate(() => {
    (window as unknown as { opened: unknown[] }).opened = [];
    window.open = ((...args: unknown[]) => ((window as unknown as { opened: unknown[] }).opened.push(args), null)) as typeof window.open;
  });
  const startUrl = page.url();
  await page.locator(".timeline-row .ticket-link").click();
  expect(await page.evaluate(() => (window as unknown as { opened: unknown[] }).opened)).toEqual([
    ["https://company.atlassian.net/browse/DEP-12", "_blank", "noopener,noreferrer"],
  ]);
  expect(page.url()).toBe(startUrl);
  // Drill-down: repository and branch links on a recognized host.
  await page.locator(".timeline-row .row-label").click();
  const detail = page.getByRole("dialog", { name: "Recorded work detail" });
  await expect(detail.locator(".repository-web-link")).toHaveAttribute("href", "https://github.com/example/links-demo");
  await expect(detail.locator(".branch-link").first()).toHaveAttribute("href", "https://github.com/example/links-demo/tree/feature/DEP-12");
  await page.getByRole("button", { name: "Close activity detail" }).click();
  // No repository or branch link for a local-only identity.
  await ticketFilter(page).fill("DEP-30");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row .ticket-link[href='https://company.atlassian.net/browse/DEP-30']")).toBeVisible();
  await expect(page.locator(".timeline-row")).toHaveCount(1);
  await expect(page.locator(".timeline-row .ticket-link")).toHaveAttribute("href", "https://company.atlassian.net/browse/DEP-30");
  await page.locator(".timeline-row .row-label").click();
  await expect(detail.locator(".observation")).toHaveCount(1);
  await expect(detail.locator(".repository-web-link")).toHaveCount(0);
  await expect(detail.locator(".branch-link")).toHaveCount(0);
  await expect(detail.locator(".branch-line")).toContainText("feature/DEP-30");
  await page.getByRole("button", { name: "Close activity detail" }).click();
  await page.getByRole("button", { name: "Clear ticket filter" }).click();
  expect(external).toEqual([]);
});

test("filters History by ticket, keeps the full lifecycle, and remembers the filter", async ({ page }) => {
  await authorize(page);
  await openHistory(page);
  await expect(page.locator(".timeline-row")).toHaveCount(50);
  await ticketFilter(page).fill("  jira:other.atlassian.net:DEP-12  ");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row .ticket-link[href='https://other.atlassian.net/browse/DEP-12']")).toBeVisible();
  await expect(page.locator(".timeline-row")).toHaveCount(1);
  await expect(page.locator(".ticket-filter-active")).toContainText("jira:other.atlassian.net:DEP-12");
  await ticketFilter(page).fill("jira:company.atlassian.net:DEP-12");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row .ticket-link[href='https://company.atlassian.net/browse/DEP-12']")).toBeVisible();
  await expect(page.locator(".timeline-row")).toHaveCount(1);
  // The company ticket's two observations are 4 days apart: the filter keeps its whole recorded span.
  await expect(page.locator(".timeline-row .row-label small")).toContainText("2 activity dates");
  await ticketFilter(page).fill("DEP-12");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row")).toHaveCount(2);
  await expect(page.locator(".timeline-row .ticket-link[href='https://company.atlassian.net/browse/DEP-12']")).toBeVisible();
  await expect(page.locator(".timeline-row .ticket-link[href='https://other.atlassian.net/browse/DEP-12']")).toBeVisible();
  await expect.poll(async () => page.evaluate(async () => (await (await fetch("/api/state")).json()).history?.ticket), { timeout: 20_000 }).toBe("DEP-12");
  await page.reload();
  await expect(page.getByRole("heading", { name: "Work over time" })).toBeVisible({ timeout: 45_000 });
  await expect(ticketFilter(page)).toHaveValue("DEP-12");
  await expect(page.locator(".timeline-row")).toHaveCount(2);
  await page.getByRole("button", { name: "Clear ticket filter" }).click();
  await expect(page.locator(".ticket-filter-active")).toHaveCount(0);
  await expect(page.locator(".timeline-row")).toHaveCount(50);
  await expect(ticketFilter(page)).toHaveValue("");
  await expect.poll(async () => page.evaluate(async () => (await (await fetch("/api/state")).json()).history?.ticket ?? null), { timeout: 20_000 }).toBeNull();
});

test("copies a work item as Markdown, loading every observation page", async ({ page, context }) => {
  await context.grantPermissions(["clipboard-read", "clipboard-write"], { origin: base });
  await authorize(page);
  await openHistory(page);
  await ticketFilter(page).fill("jira:company.atlassian.net:DEP-12");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row .ticket-link[href='https://company.atlassian.net/browse/DEP-12']")).toBeVisible();
  await expect(page.locator(".timeline-row")).toHaveCount(1);
  await page.locator(".timeline-row .row-label").click();
  await expect(page.locator(".observation")).toHaveCount(2);
  await page.getByRole("button", { name: "Copy as Markdown" }).click();
  await expect(page.locator(".copy-toast")).toHaveText("Markdown copied.");
  // The Windows clipboard returns CRLF line breaks.
  const text = (await page.evaluate(() => navigator.clipboard.readText())).replace(/\r\n/g, "\n");
  const lines = text.split("\n");
  expect(lines[0]).toBe("### DEP-12");
  expect(lines[1]).toContain("Tickets: [DEP-12](https://company.atlassian.net/browse/DEP-12)");
  expect(lines[1]).toContain("(https://github.com/example/links-demo)");
  expect(lines[2]).toMatch(/^Recorded: .+ → .+ · Latest status: In progress · Times in .+$/);
  const items = lines.filter((l) => l.startsWith("- "));
  expect(items).toHaveLength(2);
  expect(items[0]).toContain("Recorded start — Started the linked Jira work.");
  expect(items[1]).toContain("In progress — Continued the linked Jira work.");
  expect(text).not.toContain("truncated");
  await page.getByRole("button", { name: "Close activity detail" }).click();
  // A work item with more observations than one page: the second page is loaded before copying.
  await page.getByRole("button", { name: "Clear ticket filter" }).click();
  await page.locator(".timeline-row .row-label").filter({ hasText: "WL-190" }).click();
  await expect(page.locator(".observation")).toHaveCount(100);
  await page.getByRole("button", { name: "Copy as Markdown" }).click();
  await expect(page.locator(".copy-toast")).toHaveText("Markdown copied.");
  const large = (await page.evaluate(() => navigator.clipboard.readText())).replace(/\r\n/g, "\n");
  expect(large.split("\n").filter((l) => l.startsWith("- "))).toHaveLength(125);
  expect(large).not.toContain("truncated");
});

test("marks quiet active changes and changes without recorded activity", async ({ page }, testInfo) => {
  await authorize(page);
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await page.getByRole("button", { name: /links-demo/ }).first().click();
  const card = (name: string) => page.locator(".change-card").filter({ has: page.getByRole("button", { name, exact: true }) });
  await expect(card("Quiet change")).toBeVisible();
  await expect(card("Quiet change").locator(".quiet-chip")).toContainText(/No activity recorded in the last 14 days . latest \d{1,2} \w{3} \d{4}/);
  await expect(card("Never logged").locator(".quiet-chip")).toHaveText("No recorded activity");
  await expect(page.locator(".change-card")).toHaveCount(2);
  // Ongoing items show ticket links.
  await expect(page.locator(".board .ticket-link[href='https://dev.azure.com/contoso/Payments/_workitems/edit/4211']")).toBeVisible();
  await expect(page.locator(".board .ticket-id", { hasText: "UNK-7" })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath("quiet.png"), fullPage: true });
  // A change with recent activity gets no chip.
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await page.getByRole("button", { name: /example\/worklog/ }).first().click();
  await expect(card("Activity timeline")).not.toHaveCount(0);
  await expect(card("Activity timeline").locator(".quiet-chip")).toHaveCount(0);
});

test.describe("Ongoing board", () => {
  const stateWith = (page: Page, repositories: string[] | null, extra: object = {}) =>
    putState(page, { schemaVersion: 1, repositories, ...extra });
  const column = (page: Page, stage: string) => page.locator(`.board-column[data-stage="${stage}"]`);
  const titles = async (page: Page, stage: string) =>
    (await column(page, stage).locator(".board-card .change-title").allTextContents()).map((t) => t.trim());
  const open = async (page: Page, extra: object = {}) => {
    await authorize(page);
    await stateWith(page, ["example/board-demo"], extra);
    await page.goto("/ongoing");
    await expect(page.getByRole("heading", { name: "Ongoing board" })).toBeVisible();
    await settled(page);
  };
  // Change cards render first; ticket and ad-hoc cards follow once the Ongoing reads finish.
  const settled = async (page: Page) => {
    await expect(column(page, "in_progress").locator(".board-card")).toHaveCount(4);
    await expect(column(page, "done").locator('[data-kind="adhoc"]')).toHaveCount(1);
  };
  const reset = async (page: Page) => stateWith(page, null);
  // Without the OpenSpec CLI (the packaged run) task totals are unavailable, and stages follow recorded facts alone.
  const tasks = (ticked: number) => (process.env["WORKLOG_DASHBOARD_NO_NODE"] === "true" ? "Task progress unavailable" : `${ticked} / 3 tasks`);
  const cardNamed = (page: Page, stage: string, name: string) =>
    column(page, stage).locator(".board-card").filter({ has: page.getByRole("button", { name, exact: true }) });

  test("derives columns from recorded facts and expires short-lived cards", async ({ page }) => {
    await open(page);
    try {
      await expect(column(page, "proposed").locator(".board-card")).toHaveCount(1);
      await expect(cardNamed(page, "proposed", "Board proposal")).toContainText("No activity recorded");
      await expect(cardNamed(page, "proposed", "Board proposal")).toContainText(tasks(0));
      await expect(cardNamed(page, "in_progress", "Board started")).toContainText(tasks(0));
      await expect(cardNamed(page, "in_progress", "Board started")).toContainText("In progress");
      await expect(cardNamed(page, "done", "Board recorded done")).toContainText(tasks(1));
      await expect(cardNamed(page, "done", "Board recorded done")).toContainText("Recorded done");
      // Ticket work without a change, and a follow-up to the archived change.
      await expect(column(page, "in_progress").locator('[data-kind="ticket"]').filter({ hasText: "Ticket · no OpenSpec change" })).toContainText("BRD-71");
      await expect(column(page, "in_progress").locator('[data-kind="ticket"]').filter({ hasText: "Follow-up to Board shipped" })).toContainText("BRD-70");
      // Ad-hoc cards: 6 days old stays (in Done), 8 days old is gone.
      const adhoc = column(page, "done").locator('[data-kind="adhoc"]');
      await expect(adhoc).toHaveCount(1);
      await expect(adhoc.locator(".change-title")).toContainText("Adhoc six days old");
      await expect(adhoc).toContainText("BRD-99");
      await expect(page.getByText("Adhoc eight days old")).toHaveCount(0);
      await adhoc.locator(".change-title").click();
      await expect(adhoc.locator(".adhoc-details")).toContainText("fixture");
      await expect(page.locator("[data-testid=board-truncated]")).toHaveCount(0);
      await expect(column(page, "proposed").locator(".quiet-chip")).toHaveText("No recorded activity");
    } finally {
      await reset(page);
    }
  });

  test("card footers span the full card width for every card kind", async ({ page }) => {
    await open(page);
    try {
      const edges = async (selector: string) =>
        page.locator(`.board-card${selector}`).first().evaluate((card) => {
          const footer = card.querySelector(".card-footer")!.getBoundingClientRect();
          const box = card.getBoundingClientRect();
          const style = getComputedStyle(card);
          return {
            left: footer.left - (box.left + parseFloat(style.borderLeftWidth)),
            right: box.right - parseFloat(style.borderRightWidth) - footer.right,
            padding: style.paddingLeft + " " + style.paddingRight,
          };
        });
      const change = await edges('[data-kind="change"]');
      const ticket = await edges('[data-kind="ticket"]');
      for (const e of [change, ticket]) {
        expect(Math.abs(e.left)).toBeLessThanOrEqual(1);
        expect(Math.abs(e.right)).toBeLessThanOrEqual(1);
      }
      expect(change.padding).toBe(ticket.padding);
    } finally {
      await reset(page);
    }
  });

  test("repository colors stay stable and every themed footer keeps readable contrast", async ({ page }, testInfo) => {
    await open(page);
    const activityRoutePattern = "**/api/activity/changes*";
    const movementActivityRoute = async (route: Route) => {
      const response = await route.fetch();
      const activities = (await response.json()) as Array<{
        repository: string;
        change: string;
        firstAt: string;
        lastAt: string;
        lastStatus: string;
      }>;
      const observedAt = new Date().toISOString();
      const updatedActivities = activities.filter(
        (activity) => !(activity.repository === "example/board-demo" && activity.change === "board-proposal"),
      );
      updatedActivities.push({
        repository: "example/board-demo",
        change: "board-proposal",
        firstAt: observedAt,
        lastAt: observedAt,
        lastStatus: "in_progress",
      });
      await route.fulfill({ response, json: updatedActivities });
    };
    let movementRouteInstalled = false;
    try {
      const repoLink = page.locator('.repo-link[title="example/board-demo"]');
      const initialSidebarColor = await repoLink.locator(".repo-dot").getAttribute("data-repository-color");
      const initialCardColor = await cardNamed(page, "in_progress", "Board started").getAttribute("data-repository-color");
      expect(initialCardColor).toBe(initialSidebarColor);
      const proposedColor = await cardNamed(page, "proposed", "Board proposal").getAttribute("data-repository-color");
      expect(proposedColor).toBe(initialSidebarColor);

      await page.getByRole("button", { name: "All repositories", exact: true }).click();
      await expect(repoLink.locator(".repo-dot")).toHaveAttribute("data-repository-color", initialSidebarColor!);
      await repoLink.click();
      await settled(page);
      await expect(cardNamed(page, "in_progress", "Board started")).toHaveAttribute("data-repository-color", initialCardColor!);

      const filter = page.getByRole("searchbox", { name: "Filter board cards" });
      await filter.fill("board-started");
      await expect(page.locator(".board-card")).toHaveCount(1);
      await filter.fill("");
      await page.reload();
      await expect(page.getByRole("heading", { name: "Ongoing board" })).toBeVisible();
      await settled(page);
      await expect(repoLink.locator(".repo-dot")).toHaveAttribute("data-repository-color", initialSidebarColor!);
      await expect(cardNamed(page, "in_progress", "Board started")).toHaveAttribute("data-repository-color", initialCardColor!);

      // Supply one synthetic activity response so stage derivation moves the card without changing fixture files or journal data.
      await page.route(activityRoutePattern, movementActivityRoute);
      movementRouteInstalled = true;
      await page.reload();
      await expect(cardNamed(page, "in_progress", "Board proposal")).toHaveAttribute("data-repository-color", proposedColor!);
      await expect(repoLink.locator(".repo-dot")).toHaveAttribute("data-repository-color", proposedColor!);

      const evidenceDirectory = resolve("test-results/repository-card-colors");
      await mkdir(evidenceDirectory, { recursive: true });
      const darkContrast = await renderPaletteAndMeasure(page, "dark");
      expect(darkContrast.keys).toEqual(["teal", "blue", "sand", "green", "rose", "violet", "aqua", "amber"]);
      expect(darkContrast.minimum).toBeGreaterThanOrEqual(4.5);
      expect(darkContrast.sharedMinimum).toBeGreaterThanOrEqual(4.5);
      expect(darkContrast.missingMinimum).toBeGreaterThanOrEqual(4.5);
      expect(darkContrast.sharedBackground).toBe(darkContrast.expectedNeutralBackground);
      expect(darkContrast.missingBackground).toBe(darkContrast.expectedNeutralBackground);
      await page.screenshot({ path: resolve(evidenceDirectory, `${evidencePrefix}-dark.png`), fullPage: true });

      const lightContrast = await renderPaletteAndMeasure(page, "light");
      expect(lightContrast.keys).toEqual(darkContrast.keys);
      expect(lightContrast.minimum).toBeGreaterThanOrEqual(4.5);
      expect(lightContrast.sharedMinimum).toBeGreaterThanOrEqual(4.5);
      expect(lightContrast.missingMinimum).toBeGreaterThanOrEqual(4.5);
      expect(lightContrast.sharedBackground).toBe(lightContrast.expectedNeutralBackground);
      expect(lightContrast.missingBackground).toBe(lightContrast.expectedNeutralBackground);
      await page.screenshot({ path: resolve(evidenceDirectory, `${evidencePrefix}-light.png`), fullPage: true });

      await page.locator(".repository-palette-evidence").evaluate((evidence) => evidence.remove());
      await page.setViewportSize({ width: 390, height: 844 });
      const narrow = await page.evaluate(() => {
        const cards = Array.from(document.querySelectorAll<HTMLElement>(".board-card"));
        const fits = (element: Element, parent: Element) => {
          const box = element.getBoundingClientRect();
          const bounds = parent.getBoundingClientRect();
          return box.left >= bounds.left - 1 && box.right <= bounds.right + 1;
        };
        const boardHeading = document.querySelector(".board-section > .section-heading.spaced")!;
        const boardFilter = document.querySelector(".board-section .board-filter")!;
        const boardFilterInput = boardFilter.querySelector(".board-filter-input")!;
        const inboxHeading = document.querySelector(".review-inbox > .section-heading")!;
        const inboxControls = inboxHeading.querySelector(".inbox-controls")!;
        return {
          overflow: document.documentElement.scrollWidth > window.innerWidth,
          boardHeadingFits: fits(boardHeading, document.querySelector(".board-section")!),
          boardFilterFits: fits(boardFilter, boardHeading),
          boardFilterInputFits: fits(boardFilterInput, boardFilter),
          inboxControlsFit: fits(inboxControls, inboxHeading),
          clippedFooters: cards.filter((card) => {
            const footer = card.querySelector(".card-footer")!.getBoundingClientRect();
            const cardBox = card.getBoundingClientRect();
            return footer.left < cardBox.left || footer.right > cardBox.right || Array.from(card.querySelectorAll(".card-footer > span"))
              .some((span) => span.getBoundingClientRect().right > footer.right + 1);
          }).length,
        };
      });
      expect(narrow.overflow).toBe(false);
      expect(narrow.boardHeadingFits).toBe(true);
      expect(narrow.boardFilterFits).toBe(true);
      expect(narrow.boardFilterInputFits).toBe(true);
      expect(narrow.inboxControlsFit).toBe(true);
      expect(narrow.clippedFooters).toBe(0);
      await page.screenshot({ path: resolve(evidenceDirectory, `${evidencePrefix}-narrow.png`), fullPage: true });
    } finally {
      try {
        if (movementRouteInstalled)
          await page.unroute(activityRoutePattern, movementActivityRoute);
      } finally {
        await reset(page);
      }
    }
  });

  test("filters every column by ticket and change name", async ({ page }) => {
    await open(page);
    try {
      const filter = page.getByRole("searchbox", { name: "Filter board cards" });
      const total = await page.locator(".board-card").count();
      expect(total).toBeGreaterThan(4);
      await filter.fill("brd-70");
      await expect(page.locator(".board-card")).toHaveCount(1);
      await expect(page.locator(".board-filter-active")).toContainText(`1 of ${total} cards`);
      await filter.fill("board-second");
      await expect(page.locator(".board-card")).toHaveCount(1);
      await filter.fill("brd-99");
      await expect(page.locator(".board-card")).toHaveCount(1);
      await filter.fill("");
      await expect(page.locator(".board-filter-active")).toHaveCount(0);
      await expect(page.locator(".board-card")).toHaveCount(total);
      // The filter is not remembered.
      await filter.fill("brd-70");
      await page.reload();
      await expect(page.getByRole("searchbox", { name: "Filter board cards" })).toHaveValue("");
    } finally {
      await reset(page);
    }
  });

  test("reorders with the keyboard, and the order is restored after a reload", async ({ page }) => {
    await open(page);
    try {
      await expect(column(page, "in_progress").locator(".board-card")).toHaveCount(4);
      const before = await titles(page, "in_progress");
      const last = before.at(-1)!;
      const beforeLast = before.at(-2)!;
      const doneBefore = await titles(page, "done");
      const saved = page.waitForResponse((r) => r.url().endsWith("/api/state") && r.request().method() === "PUT" && Array.isArray(r.request().postDataJSON().board?.order));
      const card = cardNamed(page, "in_progress", last);
      await card.locator("summary").focus();
      await page.keyboard.press("Enter");
      const up = card.getByRole("button", { name: /^Move up/ });
      await up.focus();
      await page.keyboard.press("Enter");
      await expect.poll(() => titles(page, "in_progress")).toEqual([...before.slice(0, -2), last, beforeLast]);
      await expect(up).toBeFocused();
      await expect(page.locator("[data-testid=board-announcement]")).toContainText(`moved to position ${before.length - 1} of ${before.length} in In progress.`);
      expect((await saved).ok()).toBe(true);
      // Move the card now at the end to the top.
      const end = cardNamed(page, "in_progress", beforeLast);
      await end.locator("summary").click();
      await end.getByRole("button", { name: /^Move to top/ }).click();
      const expected = [beforeLast, ...before.slice(0, -2), last];
      await expect.poll(() => titles(page, "in_progress")).toEqual(expected);
      const topKey = await end.getAttribute("data-card-key");
      await expect.poll(async () => (await (await page.request.get("/api/state")).json()).board?.order).toHaveLength(before.length);
      await expect.poll(async () => (await (await page.request.get("/api/state")).json()).board?.order?.[0]).toBe(topKey);
      await page.reload();
      await settled(page);
      expect(await titles(page, "in_progress")).toEqual(expected);
      expect(await titles(page, "done")).toEqual(doneBefore);
    } finally {
      await reset(page);
    }
  });

  test("restores a saved priority order for a card in its column", async ({ page }) => {
    await open(page);
    try {
      await expect(column(page, "in_progress").locator(".board-card")).toHaveCount(4);
      const before = await titles(page, "in_progress");
      const lastKey = await column(page, "in_progress").locator(".board-card").last().getAttribute("data-card-key");
      await stateWith(page, ["example/board-demo"], { board: { order: [lastKey] } });
      await page.goto("/ongoing");
      await settled(page);
      // Unranked cards come first; the ranked one follows them.
      expect(await titles(page, "in_progress")).toEqual(before);
      const first = await column(page, "in_progress").locator(".board-card").first().getAttribute("data-card-key");
      await stateWith(page, ["example/board-demo"], { board: { order: [first] } });
      await page.goto("/ongoing");
      await settled(page);
      expect((await titles(page, "in_progress")).at(-1)).toBe(before[0]);
    } finally {
      await reset(page);
    }
  });

  test("renders in both themes without horizontal overflow", async ({ page }, testInfo) => {
    await open(page);
    try {
      for (const theme of ["dark", "light"]) {
        if ((await page.locator("html").getAttribute("data-theme")) !== theme)
          await page.getByRole("button", { name: `Switch to ${theme} theme` }).click();
        await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
        expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
        // WORKLOG_BOARD_SCREENSHOT_DIR keeps the screenshots for review instead of the per-run output folder.
        const folder = process.env["WORKLOG_BOARD_SCREENSHOT_DIR"];
        await page.screenshot({ path: folder ? join(folder, `dev-a1-board-${theme}.png`) : testInfo.outputPath(`board-${theme}.png`), fullPage: true });
      }
      await page.setViewportSize({ width: 700, height: 900 });
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
      await expect(column(page, "proposed")).toBeVisible();
    } finally {
      await reset(page);
    }
  });
});

test.describe("Daily view", () => {
  // The fixture records its Daily repository on fixed past dates: work on Tue 6 Jan and Fri 9 Jan 2026. The browser clock is Monday 12 January 2026.
  const monday = new Date("2026-01-12T12:30:00Z");
  const stateWith = async (page: Page, repositories: string[] | null, extra: object = {}, serverBase = base) => {
    await page.goto("about:blank");
    const response = await page.request.put(serverBase + "/api/state", { data: { schemaVersion: 1, repositories, ...extra } });
    expect(response.status()).toBe(204);
    const stored = await (await page.request.get(serverBase + "/api/state")).json();
    expect(stored).toMatchObject({ repositories, ...extra });
  };
  const open = async (page: Page, context: BrowserContext, repository = "example/daily-demo", serverBase = base, serverInfo?: Awaited<ReturnType<typeof fixture>>) => {
    await context.grantPermissions(["clipboard-read", "clipboard-write"], { origin: serverBase });
    await page.clock.install({ time: monday });
    await authorize(page, serverBase, serverInfo);
    await stateWith(page, [repository], {}, serverBase);
    await page.goto(serverBase + "/daily");
    await expect(page.locator("#daily-title")).toBeVisible();
    await expect(page.getByTestId("daily-day")).toBeVisible();
    // Copy for daily is enabled only once Today is built from the loaded Ongoing rows, so this is the settled state.
    await expect(page.getByTestId("copy-daily")).toBeEnabled();
    await expect(page.getByTestId("daily-today-line").first()).toBeVisible();
  };
  const reset = async (page: Page, serverBase = base) => stateWith(page, null, {}, serverBase);
  const copied = async (page: Page) => {
    await page.getByTestId("copy-daily").click();
    await expect(page.locator(".copy-toast")).toHaveText("Daily copied.");
    // The Windows clipboard returns CRLF line breaks.
    return (await page.evaluate(() => navigator.clipboard.readText())).replace(/\r\n/g, "\n");
  };
  const todayTitles = async (page: Page) =>
    (await page.getByTestId("daily-today").locator(".daily-title").allTextContents()).map((t) => t.trim());

  test("Monday shows Friday, and stepping back skips the days without observations", async ({ page, context }) => {
    await open(page, context);
    try {
      await expect(page.getByTestId("daily-day")).toHaveText("Fri 9 Jan");
      await expect(page.getByTestId("daily-day-kind")).toHaveText("Last recorded day");
      await expect(page.getByTestId("daily-yesterday").getByTestId("daily-line")).toHaveCount(2);
      await expect(page.getByTestId("daily-next")).toBeDisabled();
      // Done first, with the earlier observation of the ticket collapsed.
      const first = page.getByTestId("daily-yesterday").getByTestId("daily-line").first();
      await expect(first).toContainText("DEMO-1");
      await expect(first.getByTestId("daily-part-done").locator("li")).toHaveText("Retry policy for export jobs merged.");
      await expect(first.locator("details summary")).toHaveText("1 other observation");
      await page.getByTestId("daily-previous").click();
      await expect(page).toHaveURL(/\/daily\?day=2026-01-06$/);
      await expect(page.getByTestId("daily-day")).toHaveText("Tue 6 Jan");
      await expect(page.getByTestId("daily-day-kind")).toHaveText("Recorded day");
      await expect(page.getByTestId("daily-yesterday").getByTestId("daily-line")).toHaveCount(1);
      await expect(page.getByTestId("daily-previous")).toBeDisabled();
      // The selected day is in the URL for back and forward, and is never remembered.
      await expect.poll(async () => (await (await page.request.get("/api/state")).json()).lastRoute).toBe("/daily");
      await page.goBack();
      await expect(page.getByTestId("daily-day")).toHaveText("Fri 9 Jan");
      await page.getByTestId("daily-previous").click();
      await page.getByTestId("daily-next").click();
      await expect(page.getByTestId("daily-day")).toHaveText("Fri 9 Jan");
    } finally {
      await reset(page);
    }
  });

  test("copies the plain-text block, and the Markdown format with links is remembered", async ({ page, context }) => {
    await open(page, context);
    try {
      expect(await copied(page)).toBe(
        [
          "Yesterday (Fri 9 Jan):",
          "- DEMO-1 — done — Retry policy for export jobs merged.",
          "- DEMO-2 — in progress — Wired the scheduler hook; load test remains.",
          "Today:",
          "- DEMO-2",
          "- DEMO-3",
          "Blockers:",
          "- No blockers recorded",
        ].join("\n"),
      );
      await page.getByLabel("Copy format").selectOption("markdown");
      await page.getByLabel("Include ticket links").check();
      const text = await copied(page);
      expect(text.split("\n")[0]).toBe("**Yesterday (Fri 9 Jan)**");
      expect(text).toContain("- [DEMO-1](https://example.local/browse/DEMO-1) — done — Retry policy for export jobs merged.");
      expect(text).toContain("**Blockers**");
      await expect.poll(async () => (await (await page.request.get("/api/state")).json()).daily).toEqual({ format: "markdown", links: true });
      await page.reload();
      await expect(page.getByLabel("Copy format")).toHaveValue("markdown");
      await expect(page.getByLabel("Include ticket links")).toBeChecked();
      expect((await (await page.request.get("/api/state")).json()).board).toBeUndefined();
    } finally {
      await reset(page);
    }
  });

  test("a new observation for today appears under Today with its summary, and a ticket worked on both days shows each day's summary", async ({ page, context }) => {
    const isolated = await isolatedDailyServer();
    try {
      await open(page, context, "example/daily-demo", isolated.baseUrl, isolated.info);
      const info = isolated.info;
      const smoke = resolve("../tools/WorkLog.Smoke/bin/Release/net10.0/WorkLog.Smoke.dll");
      const logged = spawnSync(
        "dotnet",
        [smoke, "--dashboard-log", info.root, "example/daily-demo", "in_progress", "daily-flow", "DEMO-2", "2026-01-12T12:20:00.000Z", "Export retries wired to the job scheduler; load test remains."],
        { encoding: "utf8", windowsHide: true },
      );
      expect(logged.status, logged.stdout + logged.stderr).toBe(0);
      await page.getByRole("button", { name: "Refresh dashboard" }).click();
      const line = page.getByTestId("daily-today-line").filter({ hasText: "DEMO-2" });
      await expect(line.getByTestId("daily-part-done").locator("li")).toHaveText("Export retries wired to the job scheduler; load test remains.");
      await expect(line).toContainText("In progress");
      // Yesterday keeps Friday's summary for the same ticket.
      const yesterday = page.getByTestId("daily-yesterday").getByTestId("daily-line").filter({ hasText: "DEMO-2" });
      await expect(yesterday.getByTestId("daily-part-done").locator("li")).toHaveText("Wired the scheduler hook; load test remains.");
      // A card with nothing recorded today shows its title and ticket only.
      const quiet = page.getByTestId("daily-today-line").filter({ hasText: "DEMO-3" });
      await expect(quiet).toHaveCount(1);
      await expect(quiet.getByTestId("daily-today-summary")).toHaveCount(0);
      expect(await copied(page)).toContain("Today:\n- DEMO-2 — in progress — Export retries wired to the job scheduler; load test remains.\n- DEMO-3\nBlockers:");
    } finally {
      await isolated.close();
    }
  });

  test("a stored digest shows on its Yesterday and Today lines with the mark and is copied in place of the summary", async ({ page, context }) => {
    await open(page, context, "example/digest-demo");
    try {
      const yesterday = page.getByTestId("daily-yesterday").getByTestId("daily-line").filter({ hasText: "DGT-1" });
      await expect(yesterday.getByTestId("daily-summary")).toContainText("Built and merged the audit export.");
      await expect(yesterday.getByTestId("daily-digest-mark")).toHaveText("agent digest");
      await expect(yesterday).toContainText("Recorded done");
      await expect(yesterday.locator("details summary")).toHaveText("1 other observation");
      const today = page.getByTestId("daily-today-line").filter({ hasText: "DGT-2" });
      await expect(today.getByTestId("daily-today-summary")).toContainText("Retention sweep runs in batches.");
      await expect(today.getByTestId("daily-digest-mark")).toHaveText("agent digest");
      await expect(today).toContainText("In progress");
      expect(await copied(page)).toBe(
        [
          "Yesterday (Fri 9 Jan):",
          "- DGT-1 — done — Built and merged the audit export.",
          "Today:",
          "- DGT-2 — in progress — Retention sweep runs in batches.",
          "Blockers:",
          "- No blockers recorded",
        ].join("\n"),
      );
      await page.getByLabel("Copy format").selectOption("markdown");
      const markdown = await copied(page);
      expect(markdown).toContain("- DGT-1 — done — Built and merged the audit export.");
      expect(markdown).toContain("- DGT-2 — in progress — Retention sweep runs in batches.");
      expect(markdown).not.toContain("agent digest");
    } finally {
      await page.getByLabel("Copy format").selectOption("text");
      await reset(page);
    }
  });

  test("a further observation makes the digest stale, and the line falls back to the new summary after a refresh", async ({ page, context }) => {
    const isolated = await isolatedDailyServer();
    try {
      await open(page, context, "example/digest-demo", isolated.baseUrl, isolated.info);
      const today = page.getByTestId("daily-today-line").filter({ hasText: "DGT-2" });
      await expect(today.getByTestId("daily-digest-mark")).toHaveCount(1);
      const smoke = resolve("../tools/WorkLog.Smoke/bin/Release/net10.0/WorkLog.Smoke.dll");
      const logged = spawnSync(
        "dotnet",
        [smoke, "--dashboard-log", isolated.info.root, "example/digest-demo", "in_progress", "-", "DGT-2", "2026-01-12T12:20:00.000Z", "Added the retention dry-run flag."],
        { encoding: "utf8", windowsHide: true },
      );
      expect(logged.status, logged.stdout + logged.stderr).toBe(0);
      await page.getByRole("button", { name: "Refresh dashboard" }).click();
      await expect(today.getByTestId("daily-part-done").locator("li")).toHaveText("Added the retention dry-run flag.");
      await expect(today.getByTestId("daily-digest-mark")).toHaveCount(0);
      await expect(today.locator("details summary")).toHaveText("2 other observations today");
      // Friday's digest still matches its own, unchanged observations.
      await expect(page.getByTestId("daily-yesterday").getByTestId("daily-digest-mark")).toHaveCount(1);
      const text = await copied(page);
      expect(text).toContain("- DGT-2 — in progress — Added the retention dry-run flag.");
      expect(text).not.toContain("Retention sweep runs in batches.");
      expect(text).toContain("- DGT-1 — done — Built and merged the audit export.");
    } finally {
      try {
        await reset(page, isolated.baseUrl);
      } finally {
        await isolated.close();
      }
    }
  });

  test("a digest saved while Daily is open appears and is copied within a poll, without Refresh", async ({ page, context }) => {
    const isolated = await isolatedDailyServer();
    try {
      await open(page, context, "example/digest-demo", isolated.baseUrl, isolated.info);
      const smoke = resolve("../tools/WorkLog.Smoke/bin/Release/net10.0/WorkLog.Smoke.dll");
      const run = (args: string[]) => {
        const done = spawnSync("dotnet", [smoke, ...args], { encoding: "utf8", windowsHide: true });
        expect(done.status, done.stdout + done.stderr).toBe(0);
      };
      run(["--dashboard-log", isolated.info.root, "example/digest-demo", "started", "-", "DGT-3", "2026-01-12T12:05:00.000Z", "Started the export audit trail."]);
      run(["--dashboard-log", isolated.info.root, "example/digest-demo", "in_progress", "-", "DGT-3", "2026-01-12T12:10:00.000Z", "Trail records every export."]);
      await page.getByRole("button", { name: "Refresh dashboard" }).click();
      const line = page.getByTestId("daily-today-line").filter({ hasText: "DGT-3" });
      await expect(line.getByTestId("daily-part-done").locator("li")).toHaveText("Trail records every export.");
      await expect(line.getByTestId("daily-digest-mark")).toHaveCount(0);
      run(["--dashboard-digest", isolated.info.root, "example/digest-demo", "DGT-3", "Export audit trail records every export."]);
      await expect(line.getByTestId("daily-today-summary")).toContainText("Export audit trail records every export.", { timeout: 10_000 });
      await expect(line.getByTestId("daily-digest-mark")).toHaveText("agent digest");
      expect(await copied(page)).toContain("- DGT-3 — in progress — Export audit trail records every export.");
    } finally {
      try {
        await reset(page, isolated.baseUrl);
      } finally {
        await isolated.close();
      }
    }
  });

  test("a structured digest with a blocker shows its Done, Next and Blockers lists, lists the item under Blockers and is copied with labelled bullets", async ({ page, context }) => {
    const isolated = await isolatedDailyServer();
    try {
      await open(page, context, "example/digest-demo", isolated.baseUrl, isolated.info);
      const smoke = resolve("../tools/WorkLog.Smoke/bin/Release/net10.0/WorkLog.Smoke.dll");
      const run = (args: string[]) => {
        const done = spawnSync("dotnet", [smoke, ...args], { encoding: "utf8", windowsHide: true });
        expect(done.status, done.stdout + done.stderr).toBe(0);
      };
      run(["--dashboard-log", isolated.info.root, "example/digest-demo", "started", "-", "DGT-4", "2026-01-12T12:05:00.000Z", "Started the billing export."]);
      run(["--dashboard-log", isolated.info.root, "example/digest-demo", "in_progress", "-", "DGT-4", "2026-01-12T12:10:00.000Z", "Export covers invoices."]);
      run([
        "--dashboard-digest-structured", isolated.info.root, "example/digest-demo", "DGT-4",
        "Invoices export to CSV|Credit notes included", "Add the PDF layout", "Waiting for the finance sign-off",
      ]);
      const line = page.getByTestId("daily-today-line").filter({ hasText: "DGT-4" });
      await expect(line.getByTestId("daily-digest-mark")).toHaveText("agent digest", { timeout: 10_000 });
      await expect(line.getByTestId("daily-part-done").locator("li")).toHaveText(["Invoices export to CSV", "Credit notes included"]);
      await expect(line.getByTestId("daily-part-next").locator("li")).toHaveText("Add the PDF layout");
      await expect(line.getByTestId("daily-part-blockers").locator("li")).toHaveText("Waiting for the finance sign-off");
      const blocker = page.getByTestId("daily-blockers").getByTestId("daily-blocker");
      await expect(blocker).toHaveCount(1);
      await expect(blocker).toContainText("DGT-4");
      await expect(blocker).toContainText("Waiting for the finance sign-off");
      const text = await copied(page);
      expect(text).toContain("- DGT-4 — in progress\n  Done:\n  - Invoices export to CSV\n  - Credit notes included\n  Next:\n  - Add the PDF layout\n  Blockers:\n  - Waiting for the finance sign-off");
      expect(text).toMatch(/\nBlockers:\n- DGT-4 — Waiting for the finance sign-off$/);
    } finally {
      try {
        await reset(page, isolated.baseUrl);
      } finally {
        await isolated.close();
      }
    }
  });

  test("reordering two In-progress cards on the board changes their order under Today", async ({ page, context }) => {
    await open(page, context);
    try {
      const before = await todayTitles(page);
      expect(before).toHaveLength(2);
      await page.getByRole("button", { name: /Ongoing work/ }).click();
      await expect(page.getByRole("heading", { name: "Ongoing board" })).toBeVisible();
      const column = page.locator('.board-column[data-stage="in_progress"]');
      await expect(column.locator(".board-card")).toHaveCount(2);
      const last = column.locator(".board-card").last();
      const saved = page.waitForResponse((r) => r.url().endsWith("/api/state") && r.request().method() === "PUT" && Array.isArray(r.request().postDataJSON().board?.order));
      await last.locator("summary").click();
      await last.getByRole("button", { name: /^Move to top/ }).click();
      expect((await saved).ok()).toBe(true);
      await page.getByRole("button", { name: /Daily/ }).first().click();
      await expect(page.getByTestId("daily-today-line")).toHaveCount(2);
      await expect.poll(() => todayTitles(page)).toEqual([...before].reverse());
      // Visiting Daily saved nothing about the order beyond what the board saved.
      const order = (await (await page.request.get("/api/state")).json()).board.order;
      expect(order).toHaveLength(2);
    } finally {
      await reset(page);
    }
  });

  const box = async (locator: Locator) => {
    const found = await locator.boundingBox();
    expect(found).not.toBeNull();
    return found!;
  };
  const noHorizontalScroll = (page: Page) =>
    page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth);

  test("at 1440px, in both themes, Today and Yesterday sit side by side, Today first and visible without scrolling, with Blockers below spanning both", async ({ page, context }) => {
    await open(page, context);
    try {
      await page.setViewportSize({ width: 1440, height: 1000 });
      for (const theme of ["dark", "light"]) {
        await page.evaluate((value) => document.documentElement.setAttribute("data-theme", value), theme);
        const today = box(page.getByTestId("daily-today"));
        const yesterday = box(page.getByTestId("daily-yesterday"));
        const [t, y, b] = [await today, await yesterday, await box(page.getByTestId("daily-blockers"))];
        expect(t.x).toBeLessThan(y.x);
        expect(Math.abs(t.y - y.y)).toBeLessThan(4);
        expect(t.x + t.width).toBeLessThanOrEqual(y.x + 1);
        // Each column keeps a readable line length (about 70ch).
        expect(t.width).toBeLessThan(760);
        expect(y.width).toBeLessThan(760);
        expect(b.y).toBeGreaterThanOrEqual(Math.max(t.y + t.height, y.y + y.height) - 1);
        expect(b.width).toBeGreaterThan(t.width + y.width - 1);
        await expect(page.getByTestId("daily-today-line").first()).toBeInViewport({ ratio: 1 });
        expect(await noHorizontalScroll(page)).toBe(true);
        // Day navigation and the copy controls stay in the header, above both columns.
        expect((await box(page.getByTestId("daily-previous"))).y).toBeLessThan(t.y);
        expect((await box(page.getByTestId("copy-daily"))).y).toBeLessThan(t.y);
      }
    } finally {
      await page.evaluate(() => document.documentElement.setAttribute("data-theme", "dark"));
      await reset(page);
    }
  });

  test("at 390px, in both themes, there is no horizontal scroll and Today is above Yesterday in one column", async ({ page, context }) => {
    await open(page, context);
    try {
      await page.setViewportSize({ width: 390, height: 800 });
      for (const theme of ["dark", "light"]) {
        await page.evaluate((value) => document.documentElement.setAttribute("data-theme", value), theme);
        await expect(page.getByTestId("daily-today")).toBeVisible();
        expect(await noHorizontalScroll(page)).toBe(true);
        const t = await box(page.getByTestId("daily-today"));
        const y = await box(page.getByTestId("daily-yesterday"));
        const b = await box(page.getByTestId("daily-blockers"));
        expect(t.y + t.height).toBeLessThanOrEqual(y.y + 1);
        expect(y.y + y.height).toBeLessThanOrEqual(b.y + 1);
        expect(Math.abs(t.x - y.x)).toBeLessThan(2);
      }
    } finally {
      await page.evaluate(() => document.documentElement.setAttribute("data-theme", "dark"));
      await reset(page);
    }
  });

  test("at 768px the columns are stacked with Today first, in both themes, and text stays legible", async ({ page, context }) => {
    await open(page, context);
    try {
      await page.setViewportSize({ width: 768, height: 900 });
      for (const theme of ["dark", "light"]) {
        await page.evaluate((value) => document.documentElement.setAttribute("data-theme", value), theme);
        const t = await box(page.getByTestId("daily-today"));
        const y = await box(page.getByTestId("daily-yesterday"));
        expect(t.y + t.height, `${theme}: Today above Yesterday`).toBeLessThanOrEqual(y.y + 1);
        expect(await noHorizontalScroll(page), `${theme}: no horizontal scroll`).toBe(true);
        const contrast = await page.getByTestId("daily-line").first().locator(".daily-bullets li").evaluate((el) => {
          const channels = (value: string) => (value.match(/[\d.]+/g) ?? []).slice(0, 4).map(Number);
          const luminance = ([r, g, b]: number[]) => {
            const lin = (c: number) => ((c /= 255) <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4));
            return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
          };
          let background = [0, 0, 0, 0];
          for (let node: Element | null = el; node && background[3] === 0; node = node.parentElement) {
            const next = channels(getComputedStyle(node).backgroundColor);
            if (next.length === 3) background = [...next, 1];
            else if (next.length === 4 && next[3] > 0) background = next;
          }
          const [a, b] = [luminance(channels(getComputedStyle(el).color)), luminance(background)];
          return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
        });
        expect(contrast, `${theme}: bullet text contrast`).toBeGreaterThanOrEqual(4.5);
      }
    } finally {
      await page.evaluate(() => document.documentElement.setAttribute("data-theme", "dark"));
      await reset(page);
    }
  });

  test("the day navigation and each row's collapsed observations are reachable and operable with the keyboard", async ({ page, context }) => {
    await open(page, context);
    try {
      const active = () => page.evaluate(() => {
        const el = document.activeElement as HTMLElement | null;
        return { testid: el?.getAttribute("data-testid") ?? "", tag: el?.tagName ?? "", inYesterday: !!el?.closest('[data-testid="daily-yesterday"]') };
      });
      const tabUntil = async (key: string, matches: (state: Awaited<ReturnType<typeof active>>) => boolean) => {
        for (let i = 0; i < 60; i++) {
          await page.keyboard.press(key);
          if (matches(await active())) return;
        }
        throw new Error("The keyboard never reached the expected control.");
      };
      await page.locator("#daily-title").click();
      await tabUntil("Tab", (s) => s.testid === "daily-previous");
      await tabUntil("Tab", (s) => s.tag === "SUMMARY" && s.inYesterday);
      const details = page.getByTestId("daily-yesterday").locator("details").first();
      await expect(details).not.toHaveAttribute("open", "");
      await page.keyboard.press("Enter");
      await expect(details).toHaveAttribute("open", "");
      await tabUntil("Shift+Tab", (s) => s.testid === "daily-previous");
      await page.keyboard.press("Enter");
      await expect(page).toHaveURL(/\/daily\?day=2026-01-06$/);
    } finally {
      await reset(page);
    }
  });

  test("renders at phone width without horizontal overflow", async ({ page, context }, testInfo) => {
    await open(page, context);
    try {
      await page.setViewportSize({ width: 390, height: 800 });
      await expect(page.getByTestId("daily-yesterday")).toBeVisible();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
      const folder = process.env["WORKLOG_DAILY_SCREENSHOT_DIR"];
      await page.screenshot({ path: folder ? join(folder, "dev-a1-daily-phone.png") : testInfo.outputPath("daily-phone.png"), fullPage: true });
    } finally {
      await reset(page);
    }
  });
});
