import { test, expect, Page } from "@playwright/test";
import { readFile, writeFile, mkdir, rename } from "node:fs/promises";
import { resolve, join } from "node:path";
import { createHmac } from "node:crypto";
import { spawn, spawnSync } from "node:child_process";
const base = "http://127.0.0.1:47942";
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
async function authorize(page: Page) {
  const info = await fixture();
  const health = (await (await fetch(base + "/health")).json()) as {
    instance: string;
  };
  const secret = await readFile(join(info.data, "dashboard.secret"));
  const response = await fetch(base + "/api/launch", {
    method: "POST",
    headers: {
      "X-WorkLog-Launch": createHmac("sha256", secret)
        .update("launch:" + health.instance)
        .digest("hex"),
    },
  });
  const { token } = (await response.json()) as { token: string };
  await page.goto("/ongoing#connect=" + token);
  await expect(
    page.getByRole("heading", { name: "Ongoing board" }),
  ).toBeVisible({ timeout: 45_000 });
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
  await expect(page.locator(".document-tabs .current")).toHaveText("design");
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

test("switches documents during polling and ignores slower earlier selections", async ({ page }, testInfo) => {
  await authorize(page);
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.getByRole("button", { name: "All repositories", exact: true }).click();
  await openTimeline(page);
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
    await catalogReady;
    await page.getByRole("button", { name: "proposal", exact: true }).click();
    await documentReady;
    const catalogResponse = page.waitForResponse(r => r.url().endsWith("/api/catalog"));
    releaseCatalog();
    await catalogResponse;
    await page.evaluate(() => new Promise(requestAnimationFrame));
    releaseDocument();
    await expect(page.locator(".document-tabs .current")).toHaveText("proposal");
    await expect(page.locator(".markdown h1")).toHaveText("Proposal");
    heldPath = "tasks.md";
    documentHeld = new Promise<void>(resolve => releaseDocument = resolve);
    documentReady = new Promise<void>(resolve => documentStarted = resolve);
    await page.getByRole("button", { name: "tasks", exact: true }).click();
    await documentReady;
    await page.getByRole("button", { name: "specs/work-dashboard/spec", exact: true }).click();
    await expect(page.locator(".markdown h1")).toHaveText("work-dashboard specification");
    releaseDocument();
    for (const label of ["specs/openspec-browser/spec", "tasks", "proposal", "design"]) {
      await page.getByRole("button", { name: label, exact: true }).click();
      await expect(page.locator(".document-tabs .current")).toHaveText(label);
    }
    await expect(page.locator(".markdown h1")).toHaveText("Activity timeline");
    await expect(page.getByText("Reading document...", { exact: true })).toHaveCount(0);
    await page.screenshot({ path: testInfo.outputPath("dark-reader.png") });
  } finally {
    releaseCatalog();
    releaseDocument();
    await page.unroute("**/api/document");
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
  await page.getByRole("button", { name: "tasks", exact: true }).click();
  await expect(page.locator(".document-tabs .current")).toHaveText("tasks");
  await page.getByRole("button", { name: "design", exact: true }).click();
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
  await page
    .getByRole("button", { name: "Work history", exact: false })
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
}) => {
  await authorize(page);
  await page
    .getByRole("button", { name: "All repositories", exact: true })
    .click();
  const info = await fixture();
  const designFile = join(info.repo, "openspec/changes/activity-timeline/design.md");
  await writeFile(designFile, (await readFile(designFile, "utf8")) + "\n<!-- REVIEW: Archive-safe note -->\n");
  await openTimeline(page);
  await expect(page.getByRole("button", { name: "Edit review comment", exact: true })).toHaveCount(1);
  const original = page.url();
  const archive = join(info.repo, "openspec/changes/archive");
  await mkdir(archive, { recursive: true });
  await rename(
    join(info.repo, "openspec/changes/activity-timeline"),
    join(archive, "2026-10-26-activity-timeline"),
  );
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
    "47942",
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
  expect((await (await fetch(base + "/health")).json()).port).toBe(47942);
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
  await page.getByRole("button", { name: "tasks", exact: true }).click();
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
  await page.getByRole("button", { name: "Work history", exact: false }).click();
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
  await page.getByRole("button", { name: "Work history", exact: false }).click();
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
  const seed = await page.request.put("/api/state", { data: { schemaVersion: 1, repositories: null, positions: seeded } });
  expect(seed.status()).toBe(204);
  await page.reload();
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
  await expect(page.getByRole('button',{name:'Must not fallback',exact:true})).toHaveCount(0);
  const sources=page.locator('details.source-notices').last();
  await sources.locator('summary').click();
  await expect(sources).toContainText('store-member');
  await expect(sources).toContainText(/store.*(resolved|registered|unavailable)|not registered/i);
  await page.getByRole('button',{name:'Work history',exact:false}).click();
  await expect(page.locator('.timeline-row').first()).toBeVisible();
});

/** Writes (or rewrites) the Markdown documents of a change in the isolated fixture. */
async function writeChange(root: string, name: string, files: Record<string, string>) {
  const directory = join(root, "openspec", "changes", name);
  await mkdir(directory, { recursive: true });
  await writeFile(join(directory, ".openspec.yaml"), "schema: spec-driven\ncreated: 2026-10-27\n");
  for (const [file, text] of Object.entries(files)) await writeFile(join(directory, file), text);
}
const structuredMarker = (id: string, quote: string, comment: string) =>
  `<!-- REVIEW: id=${id}\nTarget: preceding passage\nQuote: ${quote}\nComment:\n${comment}\n-->`;
const panel = (page: Page) => page.locator(".review-inbox");
const cardFor = (page: Page, title: string) =>
  page.locator(".change-card").filter({ has: page.getByRole("button", { name: title, exact: true }) });
const closeReader = (page: Page) => page.getByRole("button", { name: "Close document reader" }).click();

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
  await page.getByRole("button", { name: "Work history", exact: false }).click();
  await expect(page.getByRole("heading", { name: "Work over time" })).toBeVisible();
  // A filter remembered by an earlier scenario must not leak into this one.
  if (await page.getByRole("textbox", { name: "Ticket filter" }).inputValue()) await page.getByRole("button", { name: "Clear ticket filter" }).click();
  await expect(page.locator(".ticket-filter-active")).toHaveCount(0);
}
const ticketFilter = (page: Page) => page.getByRole("textbox", { name: "Ticket filter" });

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
  await expect(page.locator(".timeline-row .ticket-link")).toHaveAttribute("href", "https://dev.azure.com/contoso/Payments/_workitems/edit/4211");
  await ticketFilter(page).fill("REC-5");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row .ticket-link")).toHaveAttribute("href", "https://tracker.example.invalid/browse/REC-5");
  await ticketFilter(page).fill("UNK-7");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row")).toHaveCount(1);
  await expect(page.locator(".timeline-row .ticket-id")).toHaveText("UNK-7");
  await expect(page.locator(".timeline-row a")).toHaveCount(0);
  // Copy ID works without a link.
  await context.grantPermissions(["clipboard-read", "clipboard-write"], { origin: base });
  await page.locator(".timeline-row").getByRole("button", { name: "Copy ticket ID UNK-7" }).click();
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe("UNK-7");
  // A link opens only after an explicit click, through window.open without the opener.
  await ticketFilter(page).fill("jira:company.atlassian.net:DEP-12");
  await ticketFilter(page).press("Enter");
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
  await expect(page.locator(".timeline-row")).toHaveCount(1);
  await expect(page.locator(".ticket-filter-active")).toContainText("jira:other.atlassian.net:DEP-12");
  await ticketFilter(page).fill("jira:company.atlassian.net:DEP-12");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row")).toHaveCount(1);
  // The company ticket's two observations are 4 days apart: the filter keeps its whole recorded span.
  await expect(page.locator(".timeline-row .row-label small")).toContainText("2 activity dates");
  await ticketFilter(page).fill("DEP-12");
  await ticketFilter(page).press("Enter");
  await expect(page.locator(".timeline-row")).toHaveCount(2);
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
    page.request.put("/api/state", { data: { schemaVersion: 1, repositories, ...extra } });
  const column = (page: Page, stage: string) => page.locator(`.board-column[data-stage="${stage}"]`);
  const titles = async (page: Page, stage: string) =>
    (await column(page, stage).locator(".board-card .change-title").allTextContents()).map((t) => t.trim());
  const open = async (page: Page, extra: object = {}) => {
    await authorize(page);
    expect((await stateWith(page, ["example/board-demo"], extra)).status()).toBe(204);
    await page.reload();
    await expect(page.getByRole("heading", { name: "Ongoing board" })).toBeVisible();
    await settled(page);
  };
  // Change cards render first; ticket and ad-hoc cards follow once the Ongoing reads finish.
  const settled = async (page: Page) => {
    await expect(column(page, "in_progress").locator(".board-card")).toHaveCount(4);
    await expect(column(page, "done").locator('[data-kind="adhoc"]')).toHaveCount(1);
  };
  const reset = async (page: Page) => expect((await stateWith(page, null)).status()).toBe(204);
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
      await page.reload();
      await settled(page);
      // Unranked cards come first; the ranked one follows them.
      expect(await titles(page, "in_progress")).toEqual(before);
      const first = await column(page, "in_progress").locator(".board-card").first().getAttribute("data-card-key");
      await stateWith(page, ["example/board-demo"], { board: { order: [first] } });
      await page.reload();
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
