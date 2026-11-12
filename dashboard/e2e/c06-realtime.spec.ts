import { test, expect, Page } from "@playwright/test";
import { createHmac } from "node:crypto";
import { readFile } from "node:fs/promises";
import { join, resolve } from "node:path";

const base = `http://127.0.0.1:${process.env["WORKLOG_DASHBOARD_TEST_PORT"] ?? "47942"}`;

async function launchToken() {
  const fixture = JSON.parse(await readFile(resolve("test-results/fixture.json"), "utf8")) as { data: string };
  const health = await (await fetch(base + "/health")).json() as { instance: string };
  const secret = await readFile(join(fixture.data, "dashboard.secret"));
  const launch = await fetch(base + "/api/launch", {
    method: "POST",
    headers: {
      "X-WorkLog-Launch": createHmac("sha256", secret).update("launch:" + health.instance).digest("hex"),
    },
  });
  return (await launch.json() as { token: string }).token;
}

async function authorize(page: Page) {
  await page.goto(base + "/ongoing#connect=" + await launchToken());
  await expect(page.getByRole("heading", { name: "Ongoing board" })).toBeVisible({ timeout: 45_000 });
  await expect(page.locator(".live-indicator[data-state='live']")).toBeVisible({ timeout: 30_000 });
}

test.describe("C06 live transport acceptance", () => {
  test("establishes authenticated WebSockets and does not poll catalog or documents at idle checkpoints", async ({ page }) => {
    const websocket = new Promise<string>((resolveSocket) => {
      page.on("websocket", (socket) => {
        if (socket.url().includes("/api/live")) resolveSocket(socket.url());
      });
    });
    const periodicReads: string[] = [];
    page.on("request", (request) => {
      if (/\/api\/(catalog|document)(?:\/|\?|$)/.test(new URL(request.url()).pathname + new URL(request.url()).search))
        periodicReads.push(request.url());
    });

    await authorize(page);
    expect(await websocket).toContain("/api/live");
    periodicReads.length = 0;
    await page.waitForTimeout(5_500);
    expect(periodicReads).toEqual([]);
    // Live is shown by the header indicator alone; no page-level bar.
    await expect(page.locator(".live-indicator")).toHaveCount(1);
    await expect(page.locator(".live-status")).toHaveCount(0);
  });

  test("uses negotiated Server-Sent Events when WebSockets are unavailable", async ({ page, context }) => {
    let negotiationSeen = false;
    let eventStreamSeen = false;
    await context.route("**/api/live/negotiate*", async (route) => {
      const response = await route.fetch();
      const body = await response.json() as { availableTransports: Array<{ transport: string; transferFormats: string[] }> };
      negotiationSeen = true;
      await route.fulfill({
        response,
        json: { ...body, availableTransports: body.availableTransports.filter((item) => item.transport === "ServerSentEvents") },
      });
    });
    page.on("request", (request) => {
      if (request.method() === "GET" && new URL(request.url()).pathname === "/api/live" && request.headers()["accept"]?.includes("text/event-stream"))
        eventStreamSeen = true;
    });

    await authorize(page);
    await expect.poll(() => negotiationSeen).toBe(true);
    await expect.poll(() => eventStreamSeen).toBe(true);
    await expect(page.locator(".live-indicator[data-state='live']")).toBeVisible();
  });

  test("first launch fills the Ongoing board before the first live revision arrives (ORCH-023)", async ({ page, context }) => {
    // Hold the live connection back so the page is active, and must show its data, before any live revision exists.
    await context.route("**/api/live/negotiate*", async (route) => {
      await new Promise((done) => setTimeout(done, 6_000));
      await route.continue();
    });
    await page.goto(base + "/#connect=" + await launchToken());
    const recent = page.locator(".stat-card").filter({ hasText: "Recent work items" }).locator("strong");
    await expect(recent).not.toHaveText("0", { timeout: 30_000 });
    await expect(page.locator(".change-card").first()).toBeVisible();
    // A connection slower than the grace period is a neutral connecting state on the header dot, never a bar (DEV-045).
    await expect(page.locator(".live-indicator")).toHaveAttribute("data-state", "connecting");
    await page.waitForTimeout(2_500);
    await expect(page.locator(".live-status")).toHaveCount(0);
    await expect(page.locator(".live-indicator")).toHaveAttribute("title", /^Connecting/);
    await expect(page.locator(".live-indicator[data-state='live']")).toBeVisible({ timeout: 30_000 });
    await expect(page.locator(".live-status")).toHaveCount(0);
    await expect(recent).not.toHaveText("0");
  });

  test("a launch in a hidden tab shows no Updates paused bar and reconciles to Live once visible (DEV-044)", async ({ page }) => {
    await page.addInitScript(() => {
      let hidden = true;
      Object.defineProperty(document, "hidden", { get: () => hidden, configurable: true });
      (window as unknown as { __show: () => void }).__show = () => { hidden = false; document.dispatchEvent(new Event("visibilitychange")); };
    });
    await page.goto(base + "/#connect=" + await launchToken());
    const recent = page.locator(".stat-card").filter({ hasText: "Recent work items" }).locator("strong");
    await expect(recent).not.toHaveText("0", { timeout: 30_000 });
    await page.waitForTimeout(8_000);
    await expect(page.locator(".live-status")).toHaveCount(0);
    await expect(page.locator(".live-indicator")).not.toHaveAttribute("data-state", "paused");
    await expect(page.locator(".live-indicator")).not.toHaveAttribute("data-state", "live");
    await page.evaluate(() => (window as unknown as { __show: () => void }).__show());
    await expect(page.locator(".live-indicator[data-state='live']")).toBeVisible({ timeout: 30_000 });
    await expect(page.locator(".live-status")).toHaveCount(0);
  });

  for (const [name, width, height] of [["phone", 390, 800], ["tablet", 768, 900], ["desktop", 1440, 1000]] as const) {
    test(`shows Live on the header indicator alone at ${name} width in both themes`, async ({ page }) => {
      await page.setViewportSize({ width, height });
      await authorize(page);
      const indicator = page.locator(".live-indicator[data-state='live']");
      for (const theme of ["dark", "light"]) {
        await page.evaluate((value) => { document.documentElement.dataset["theme"] = value; }, theme);
        await expect(indicator).toBeVisible();
        await expect(page.locator(".live-status")).toHaveCount(0);
        const dot = await indicator.locator(".live-dot").evaluate((node) => {
          const box = node.getBoundingClientRect();
          return { width: box.width, height: box.height, color: getComputedStyle(node).backgroundColor };
        });
        expect(dot.width).toBeGreaterThan(0);
        expect(dot.color).not.toBe("rgba(0, 0, 0, 0)");
        const overflow = await page.evaluate(() => [...document.querySelectorAll<HTMLElement>("body *")]
          .filter((node) => node.getBoundingClientRect().right > window.innerWidth + 0.5 && getComputedStyle(node).display !== "none")
          .slice(0, 6).map((node) => node.tagName + "." + node.className + ":" + Math.round(node.getBoundingClientRect().right)));
        expect(overflow, "elements wider than the viewport").toEqual([]);
      }
      await expect(indicator).toHaveAttribute("title", /^Live · Last successful refresh/);
      await expect(indicator).toHaveAttribute("aria-label", /Live/);
      await indicator.focus();
      await expect(indicator).toBeFocused();
    });
  }

  test("raises the Reconnecting and Updates paused bars only when updates really are unavailable, and Retry recovers", async ({ page, context }) => {
    let refuse = true;
    await context.route("**/api/live/negotiate*", async (route) => {
      if (refuse) await route.fulfill({ status: 503, body: "unavailable" });
      else await route.continue();
    });
    await page.goto(base + "/#connect=" + await launchToken());
    await expect(page.getByRole("heading", { name: "Ongoing board" })).toBeVisible({ timeout: 45_000 });
    await expect(page.locator(".live-status[data-state='paused']")).toBeVisible({ timeout: 40_000 });
    await expect(page.locator(".live-status")).toContainText("Updates paused");
    await expect(page.locator("[data-testid='live-announcer']")).toHaveAttribute("aria-live", "polite");
    await expect(page.locator("[data-testid='live-announcer']")).toContainText("Updates paused");
    refuse = false;
    await page.getByRole("button", { name: "Retry updates" }).click();
    await expect(page.locator(".live-indicator[data-state='live']")).toBeVisible({ timeout: 30_000 });
    await expect(page.locator(".live-status")).toHaveCount(0);
    await expect(page.locator("[data-testid='live-announcer']")).toContainText("Live");
  });

  test("protects negotiate and rejects a foreign Origin", async ({ page, browser }) => {
    const unauthenticated = await browser.newContext();
    try {
      const negotiate = await unauthenticated.request.post(base + "/api/live/negotiate?negotiateVersion=1");
      expect(negotiate.status()).toBe(401);
    } finally {
      await unauthenticated.close();
    }

    await authorize(page);
    const foreignOrigin = await page.request.get(base + "/api/catalog", {
      headers: { Origin: "http://foreign.example" },
    });
    expect(foreignOrigin.status()).toBe(403);
  });
});
