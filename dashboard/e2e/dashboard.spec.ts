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
    page.getByRole("heading", { name: "Pick up where you left off" }),
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
  await expect(page.locator(".change-card")).toHaveCount(4);
  if (process.env["WORKLOG_DASHBOARD_NO_NODE"] === "true")
    await expect(page.locator(".card-footer").first()).toContainText(
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
  await expect(page.locator(".timeline-row")).toHaveCount(61);
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
    page.getByRole("heading", { name: "Pick up where you left off" }),
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
