import { defineConfig } from "@playwright/test";
const testPort = Number(process.env["WORKLOG_DASHBOARD_TEST_PORT"] ?? "47942");
if (!Number.isInteger(testPort) || testPort < 1 || testPort > 65535 || testPort === 47831)
  throw new Error("WORKLOG_DASHBOARD_TEST_PORT must be a valid test port other than 47831.");
export default defineConfig({
  testDir: "./e2e",
  testMatch: "**/*.spec.ts",
  fullyParallel: false,
  workers: 1,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  outputDir: "test-results/results",
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: `http://127.0.0.1:${testPort}`,
    channel: process.env["WORKLOG_BROWSER_CHANNEL"] ?? "msedge",
    headless: true,
    viewport: { width: 1440, height: 1000 },
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  webServer: {
    command: "node e2e/server.mjs",
    url: `http://127.0.0.1:${testPort}/health`,
    reuseExistingServer: false,
    timeout: 120_000,
    gracefulShutdown: { signal: "SIGTERM", timeout: 5000 },
  },
});
