import { test, expect, Page } from "@playwright/test";
import { createHmac } from "node:crypto";
import { readFile } from "node:fs/promises";
import { join, resolve } from "node:path";

const base = `http://127.0.0.1:${process.env["WORKLOG_DASHBOARD_TEST_PORT"] ?? "47942"}`;

async function authorize(page: Page) {
  const fixture = JSON.parse(await readFile(resolve("test-results/fixture.json"), "utf8")) as { data: string };
  const health = await (await fetch(base + "/health")).json() as { instance: string };
  const secret = await readFile(join(fixture.data, "dashboard.secret"));
  const launch = await fetch(base + "/api/launch", {
    method: "POST",
    headers: {
      "X-WorkLog-Launch": createHmac("sha256", secret).update("launch:" + health.instance).digest("hex"),
    },
  });
  const { token } = await launch.json() as { token: string };
  await page.goto(base + "/ongoing#connect=" + token);
  await expect(page.getByRole("heading", { name: "Ongoing board" })).toBeVisible({ timeout: 45_000 });
  await expect(page.locator(".live-status[data-state='live']")).toBeVisible({ timeout: 30_000 });
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
    await expect(page.locator(".live-status")).toHaveCount(1);
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
    await expect(page.locator(".live-status[data-state='live']")).toBeVisible();
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
