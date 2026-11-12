import { TestBed } from "@angular/core/testing";
import { Router } from "@angular/router";
import { Subject } from "rxjs";
import { describe, it, expect, vi, afterEach } from "vitest";
import { App } from "./app";
import type { Catalog, Change, ChangeActivity, DailyDigest, DailyItem, Entry, Row, Ticket } from "./models";
import { primeLiveRevision, sendRevision } from "./app-live-test-helpers";

const repo = "github.com/acme/api";
const now = new Date("2026-10-28T12:00:00Z");
const todayStart = "2026-10-28T00:00:00.000Z";
const daysAgo = (days: number) => new Date(now.getTime() - days * 86400000).toISOString();
const hoursAgo = (hours: number) => new Date(now.getTime() - hours * 3600000).toISOString();
const dep1232: Ticket = { provider: "jira", scope: "acme.atlassian.net", id: "DEP-1232" };
const dep1240: Ticket = { provider: "jira", scope: "acme.atlassian.net", id: "DEP-1240" };

const change = (name: string, completed: number | null, total: number | null): Change => ({
  sourceId: "s", name, directoryName: name, variant: "v-" + name, archived: false, archivedOn: null, repositories: [repo],
  documents: [{ path: "design.md", label: "design", fingerprint: "1:1" }], completedTasks: completed, totalTasks: total, error: null,
});
const activity = (name: string, lastAt: string): ChangeActivity => ({ repository: repo, change: name, firstAt: daysAgo(30), lastAt, lastStatus: "in_progress" });
const row = (id: string, title: string, tickets: Ticket[], lastAt: string): Row => ({
  id, repository: repo, title, status: "in_progress", firstAt: daysAgo(3), startedAt: null, lastAt, elapsedSeconds: 0,
  activityDates: [], completionDates: [], resumedDates: [], changes: [], tickets, observationCount: 1, visibleCount: 1, clippedStart: false,
});
const entry = (id: string, occurredAt: string, summary: string | null, extra: Partial<Entry> = {}): Entry => ({
  id, repository: repo, change: null, status: "in_progress", summary, occurredAt, agent: "claude", branch: null, tickets: [], extractedTicketId: null, ...extra,
});
const item = (id: string, title: string, tickets: Ticket[], status: string, primary: Entry, extra: Partial<DailyItem> = {}): DailyItem => ({
  id, title, repository: repo, tickets, changes: [], status, primary, others: [], ...extra,
});

const yesterdayItems = [
  item("r", "DEP-1232", [dep1232], "done", entry("r1", "2026-10-22T15:00:00Z", "Retry policy for export jobs merged", { status: "done" }), {
    changes: ["retry-policy"],
    others: [entry("r0", "2026-10-22T10:00:00Z", "Started the retry work", { status: "started" })],
  }),
  item("w", "add-work-dashboard", [], "in_progress", entry("w1", "2026-10-22T12:00:00Z", "Review comments edit/delete"), { changes: ["add-work-dashboard"] }),
  item("adhoc:a", "Tried a spike", [], "in_progress", entry("a", "2026-10-22T09:00:00Z", "Tried a spike")),
];
const digest = (extra: Partial<DailyDigest> = {}): DailyDigest => ({
  day: "2026-10-22",
  previousDay: "2026-10-21",
  nextDay: null,
  items: yesterdayItems,
  today: [
    item("exp-identity", "DEP-1240", [dep1240], "in_progress", entry("x1", hoursAgo(2), "Export retries wired to the job scheduler; load test remains"), {
      others: [entry("x0", hoursAgo(5), "Wrote the scheduler hook")],
    }),
  ],
  ...extra,
});

interface Call {
  path: string;
  method: string;
  body: any;
}
function setup(options: { daily?: DailyDigest; query?: Record<string, string>; fail?: number } = {}) {
  const calls: Call[] = [];
  const events = new Subject<unknown>();
  const router = {
    events,
    url: "/daily",
    navigate: vi.fn().mockResolvedValue(true),
    parseUrl: () => ({ root: { children: { primary: { segments: [{ path: "daily" }] } } }, queryParams: options.query ?? {} }),
  };
  TestBed.configureTestingModule({ imports: [App], providers: [{ provide: Router, useValue: router }] });
  const fixture = TestBed.createComponent(App);
  const app = fixture.componentInstance;
  (app as unknown as { now: () => Date }).now = () => now;
  const catalog = {
    repositories: [{ id: repo, label: "api" }], sources: [{ id: "s", repositories: [repo], location: "project", error: null }],
    changes: [change("add-work-dashboard", 2, 9), change("quiet-one", 0, 9), change("idea", 0, 9), change("export-retries", 0, 9)],
    launchRepository: null, revision: "1", notices: [], journalSnapshot: 3,
  } as Catalog;
  const served = { catalog };
  app.catalog.set(catalog);
  app.ready.set(true);
  app["activityScope"].set("[]");
  app.activity.set([activity("add-work-dashboard", daysAgo(1)), activity("quiet-one", daysAgo(20))]);
  app.ongoing.set([row("exp-identity", "DEP-1240", [dep1240], hoursAgo(2))]);
  app["ongoingScope"].set("[]");
  app["dataLoaded"] = true;
  vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
    const path = String(input);
    calls.push({ path, method: init?.method ?? "GET", body: init?.body ? JSON.parse(init.body as string) : undefined });
    if (path === "/api/state") return new Response(null, { status: 204 });
    if (path === "/api/catalog") return Response.json(served.catalog);
    if (path === "/api/daily") {
      if (options.fail) return Response.json({ error: "day must be earlier than the current local date." }, { status: options.fail });
      return Response.json(options.daily ?? digest());
    }
    if (path === "/api/timeline") return Response.json({ snapshot: 7, rows: [], total: 0, nextOffset: null, insights: { longestSpans: [], completions: [], subsequentWork: [], days: [] } });
    if (path === "/api/ongoing/unlinked") return Response.json({ snapshot: 7, entries: [], truncated: false });
    throw new Error("Unexpected request: " + path);
  });
  return { app, fixture, calls, router, events, served, element: fixture.nativeElement as HTMLElement };
}
afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
  TestBed.resetTestingModule();
});

const saves = (calls: Call[]) => calls.filter((c) => c.method === "PUT" && c.path === "/api/state");
const lines = (element: HTMLElement, section: string) =>
  Array.from(element.querySelectorAll(`[data-testid="${section}"] .daily-line`)).map((l) => l.querySelector(".daily-title")!.textContent!.trim());
const flush = () => new Promise((resolve) => setTimeout(resolve));

describe("Daily view rendering and loading", () => {
  it("requests the digest with the start of today, the zone, the selection, and no day by default", async () => {
    const { app, calls, fixture } = setup();
    app.view.set("daily");
    await app.loadDaily();
    expect(calls.find((c) => c.path === "/api/daily")!.body).toEqual({ before: todayStart, timezone: "UTC", repositories: [] });
    fixture.destroy();
  });

  it("shows a loading state and does not copy until the Ongoing rows for the selection have loaded", async () => {
    const { app, fixture, element } = setup();
    app["ongoingScope"].set(null);
    app.view.set("daily");
    await app.loadDaily();
    fixture.detectChanges();
    expect(app.dailyTodayView()).toBeNull();
    expect(element.querySelector('[data-testid="daily-today"]')!.textContent).toContain("Reading today's work");
    expect(element.querySelector<HTMLButtonElement>('[data-testid="copy-daily"]')!.disabled).toBe(true);
    await app.loadOngoing();
    fixture.detectChanges();
    expect(app.dailyTodayView()).not.toBeNull();
    expect(element.querySelector<HTMLButtonElement>('[data-testid="copy-daily"]')!.disabled).toBe(false);
    // A different selection is not covered by what has loaded.
    app.selectedRepos.set([repo]);
    expect(app.dailyTodayView()).toBeNull();
    fixture.destroy();
  });

  it("sends the selected day and the repository selection", async () => {
    const { app, calls, fixture } = setup();
    app.view.set("daily");
    app.dailyDay = "2026-10-20";
    app.selectedRepos.set([repo]);
    await app.loadDaily();
    expect(calls.find((c) => c.path === "/api/daily")!.body).toEqual({ before: todayStart, timezone: "UTC", repositories: [repo], day: "2026-10-20" });
    fixture.destroy();
  });

  it("renders Yesterday in the server order, then Today from the board, then Blockers", async () => {
    const { app, fixture, element } = setup();
    app.view.set("daily");
    await app.loadDaily();
    fixture.detectChanges();
    expect(lines(element, "daily-yesterday")).toEqual(["DEP-1232", "Add work dashboard", "Tried a spike"]);
    // The In-progress change with ticked tasks and the ticket card both belong to Today; the quiet change and the proposal do not.
    expect(lines(element, "daily-today").sort()).toEqual(["Add work dashboard", "DEP-1240"]);
    expect(element.querySelector('[data-testid="daily-blockers"]')!.textContent).toContain("Nothing is recorded about blockers");
    const order = Array.from(element.querySelectorAll("[data-testid='daily-yesterday'], [data-testid='daily-today'], [data-testid='daily-blockers']")).map((s) => s.getAttribute("data-testid"));
    expect(order).toEqual(["daily-yesterday", "daily-today", "daily-blockers"]);
    expect(element.querySelector('[data-testid="daily-day"]')!.textContent).toContain("Thu 22 Oct");
    expect(element.querySelector('[data-testid="daily-day-kind"]')!.textContent).toContain("Last recorded day");
    expect(element.querySelector('[data-testid="daily-proposed"]')!.textContent).toContain("2 proposed changes");
    fixture.destroy();
  });

  it("shows today's status and summary on a Today line, the others collapsed, and only the title for a card with nothing today", async () => {
    const { app, fixture, element } = setup();
    app.view.set("daily");
    await app.loadDaily();
    fixture.detectChanges();
    const today = Array.from(element.querySelectorAll('[data-testid="daily-today-line"]'));
    const exportLine = today.find((l) => l.getAttribute("data-card-key")!.startsWith("ticket:"))!;
    expect(exportLine.querySelector('[data-testid="daily-today-summary"]')!.textContent).toContain("load test remains");
    expect(exportLine.textContent).toContain("In progress");
    expect(exportLine.querySelector("details summary")!.textContent).toContain("1 other observation today");
    const dashboardLine = today.find((l) => l.getAttribute("data-card-key")!.startsWith("change:"))!;
    expect(dashboardLine.querySelector('[data-testid="daily-today-summary"]')).toBeNull();
    expect(dashboardLine.querySelector(".daily-status")).toBeNull();
    fixture.destroy();
  });

  it("collapses other observations of a Yesterday line and shows an unlinked line by its summary only", async () => {
    const { app, fixture, element } = setup();
    app.view.set("daily");
    await app.loadDaily();
    fixture.detectChanges();
    const first = element.querySelector('[data-testid="daily-yesterday"] .daily-line')!;
    expect(first.querySelector('[data-testid="daily-summary"]')!.textContent).toContain("Retry policy");
    expect(first.querySelector("details summary")!.textContent).toContain("1 other observation");
    expect(first.querySelector("a.ticket-link")?.getAttribute("href")).toBe("https://acme.atlassian.net/browse/DEP-1232");
    const adhoc = element.querySelector('[data-item-id="adhoc:a"]')!;
    expect(adhoc.querySelector('[data-testid="daily-summary"]')).toBeNull();
    expect(adhoc.querySelector("details")).toBeNull();
    fixture.destroy();
  });

  it("states that no earlier work exists and still shows Today", async () => {
    const { app, fixture, element } = setup({ daily: digest({ day: null, previousDay: null, nextDay: null, items: [] }) });
    app.view.set("daily");
    await app.loadDaily();
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="daily-empty"]')!.textContent).toContain("No earlier recorded work exists");
    expect(lines(element, "daily-today")).not.toEqual([]);
    expect((element.querySelector('[data-testid="daily-previous"]') as HTMLButtonElement).disabled).toBe(true);
    fixture.destroy();
  });

  it("lists Today in the saved priority order", async () => {
    const { app, fixture, element } = setup();
    app.view.set("daily");
    await app.loadDaily();
    app.boardOrder.set(["change:" + repo + "/add-work-dashboard", "ticket:exp-identity"]);
    fixture.detectChanges();
    expect(lines(element, "daily-today")).toEqual(["Add work dashboard", "DEP-1240"]);
    app.boardOrder.set(["ticket:exp-identity", "change:" + repo + "/add-work-dashboard"]);
    fixture.detectChanges();
    expect(lines(element, "daily-today")).toEqual(["DEP-1240", "Add work dashboard"]);
    fixture.destroy();
  });

  it("identifies the repository of each line", async () => {
    const { app, fixture, element } = setup();
    app.view.set("daily");
    await app.loadDaily();
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="daily-yesterday"] .daily-line')!.textContent).toContain("api");
    fixture.destroy();
  });
});

describe("Daily view navigation", () => {
  it("steps to the previous and next recorded days through the URL", async () => {
    const { app, fixture, element, router } = setup({ daily: digest({ nextDay: "2026-10-23" }) });
    app.view.set("daily");
    await app.loadDaily();
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="daily-day-kind"]')!.textContent).toContain("Recorded day");
    (element.querySelector('[data-testid="daily-previous"]') as HTMLButtonElement).click();
    expect(router.navigate).toHaveBeenLastCalledWith(["/daily"], { queryParams: { day: "2026-10-21" } });
    (element.querySelector('[data-testid="daily-next"]') as HTMLButtonElement).click();
    expect(router.navigate).toHaveBeenLastCalledWith(["/daily"], { queryParams: { day: "2026-10-23" } });
    fixture.destroy();
  });

  it("disables stepping where no neighbouring recorded day exists", async () => {
    const { app, fixture, element } = setup({ daily: digest({ previousDay: null }) });
    app.view.set("daily");
    await app.loadDaily();
    fixture.detectChanges();
    expect((element.querySelector('[data-testid="daily-previous"]') as HTMLButtonElement).disabled).toBe(true);
    expect((element.querySelector('[data-testid="daily-next"]') as HTMLButtonElement).disabled).toBe(true);
    fixture.destroy();
  });

  it("reads the day from the URL when the route changes and loads when it changes", async () => {
    const { app, calls, fixture } = setup({ query: { day: "2026-10-20" } });
    await app["routeChanged"]();
    await flush();
    expect(app.view()).toBe("daily");
    expect(app.dailyDay).toBe("2026-10-20");
    expect(calls.filter((c) => c.path === "/api/daily")).toHaveLength(1);
    expect(calls.find((c) => c.path === "/api/daily")!.body.day).toBe("2026-10-20");
    await app["routeChanged"]();
    await flush();
    expect(calls.filter((c) => c.path === "/api/daily")).toHaveLength(1);
    fixture.destroy();
  });

  it("ignores an invalid day in the URL", async () => {
    const { app, calls, fixture } = setup({ query: { day: "yesterday" } });
    await app["routeChanged"]();
    await flush();
    expect(app.dailyDay).toBeNull();
    expect(calls.find((c) => c.path === "/api/daily")!.body.day).toBeUndefined();
    fixture.destroy();
  });

  it("falls back to the last recorded day when the server rejects the selected day", async () => {
    const { app, router, fixture } = setup({ fail: 400 });
    app.view.set("daily");
    app.dailyDay = "2026-10-28";
    const load = app.loadDaily();
    await load;
    expect(app.dailyDay).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(["/daily"], { replaceUrl: true });
    expect(app.notice()).toContain("Showing the last recorded day");
    fixture.destroy();
  });

  it("refreshes with the data refresh while the Daily view is open, and not otherwise", async () => {
    const { app, calls, fixture } = setup();
    app.view.set("ongoing");
    await app.refreshData();
    expect(calls.some((c) => c.path === "/api/daily")).toBe(false);
    app.view.set("daily");
    await app.refreshData();
    expect(calls.filter((c) => c.path === "/api/daily")).toHaveLength(1);
    fixture.destroy();
  });

  it("reloads Daily when only the digest revision moved, and not for an unchanged vector", async () => {
    const { app, calls, fixture, served } = setup();
    app.view.set("daily");
    app["evaluate"]();
    app["journalSnapshot"] = 3;
    app["digestRevision"] = 10;
    served.catalog = { ...served.catalog, digestRevision: 10 };
    primeLiveRevision(app, { journal: 3, digests: 10 });
    await app["receiveRevision"](app["revisionTracker"].desired()!);
    expect(calls.filter((c) => c.path === "/api/daily")).toHaveLength(0);
    served.catalog = { ...served.catalog, digestRevision: 11 };
    await sendRevision(app, { digests: 11, dirty: ["digests"] });
    expect(calls.filter((c) => c.path === "/api/daily")).toHaveLength(1);
    expect(calls.some((c) => c.path === "/api/timeline")).toBe(false);
    await app["receiveRevision"](app["revisionTracker"].desired()!);
    expect(calls.filter((c) => c.path === "/api/daily")).toHaveLength(1);
    fixture.destroy();
  });

  it("asks for the new day's digest after the date rolls over", async () => {
    const { app, calls, fixture } = setup();
    app.view.set("daily");
    await app.loadDaily();
    (app as unknown as { now: () => Date }).now = () => new Date("2026-10-29T08:00:00Z");
    await app.loadDaily();
    const bodies = calls.filter((c) => c.path === "/api/daily").map((c) => c.body.before);
    expect(bodies).toEqual([todayStart, "2026-10-29T00:00:00.000Z"]);
    fixture.destroy();
  });
});

describe("Daily view preferences", () => {
  it("performs no preference save when it renders, refreshes, or steps through days, and never saves the board order", async () => {
    vi.useFakeTimers();
    const { app, calls, fixture } = setup();
    app.view.set("daily");
    fixture.detectChanges();
    await app.loadDaily();
    fixture.detectChanges();
    await app.refreshData();
    await app.stepDaily("2026-10-21");
    await vi.advanceTimersByTimeAsync(5000);
    expect(saves(calls)).toEqual([]);
    expect(app.preferences.board).toBeUndefined();
    expect(app.preferences.daily).toBeUndefined();
    fixture.destroy();
  });

  it("remembers the copy format and the links choice, and never remembers the day", async () => {
    vi.useFakeTimers();
    const { app, calls, fixture, router } = setup();
    router.url = "/daily?day=2026-10-21";
    app.setDailyFormat("markdown");
    app.setDailyLinks(true);
    await vi.advanceTimersByTimeAsync(1000);
    const body = saves(calls).at(-1)!.body;
    expect(body.daily).toEqual({ format: "markdown", links: true });
    expect(body.lastRoute).toBe("/daily");
    expect(body).not.toHaveProperty("board");
    fixture.destroy();
  });

  it("ignores an unknown format and defaults to text without links", () => {
    const { app, fixture } = setup();
    expect(app.dailyFormat()).toBe("text");
    expect(app.dailyLinks()).toBe(false);
    app.setDailyFormat("html");
    expect(app.dailyFormat()).toBe("text");
    fixture.destroy();
  });
});

describe("Copy for daily", () => {
  const plain = [
    "Yesterday (Thu 22 Oct):",
    "- DEP-1232 — done — Retry policy for export jobs merged",
    "- add-work-dashboard — in progress — Review comments edit/delete",
    "- Tried a spike — in progress",
    "Today:",
    "- DEP-1240 — in progress — Export retries wired to the job scheduler; load test remains",
    "- add-work-dashboard",
    "Blockers:",
    "-",
  ].join("\n");

  async function copied(configure: (app: App) => void = () => {}) {
    const harness = setup();
    harness.app.view.set("daily");
    await harness.app.loadDaily();
    // Both Today cards carry the same priority here, so pin the order the fixture is written in.
    harness.app.boardOrder.set(["ticket:exp-identity", "change:" + repo + "/add-work-dashboard"]);
    configure(harness.app);
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
    await harness.app.copyDaily();
    return { ...harness, writeText };
  }

  it("copies the plain-text block without links by default", async () => {
    const { writeText, app, fixture } = await copied();
    expect(writeText).toHaveBeenCalledWith(plain);
    expect(app.copyToast()).toBe("Daily copied.");
    fixture.destroy();
  });

  it("copies Markdown with ticket links when chosen", async () => {
    const { writeText, fixture } = await copied((app) => {
      app.dailyFormat.set("markdown");
      app.dailyLinks.set(true);
    });
    expect(writeText).toHaveBeenCalledWith(
      [
        "**Yesterday (Thu 22 Oct)**",
        "",
        "- [DEP-1232](https://acme.atlassian.net/browse/DEP-1232) — done — Retry policy for export jobs merged",
        "- add-work-dashboard — in progress — Review comments edit/delete",
        "- Tried a spike — in progress",
        "",
        "**Today**",
        "",
        "- [DEP-1240](https://acme.atlassian.net/browse/DEP-1240) — in progress — Export retries wired to the job scheduler; load test remains",
        "- add-work-dashboard",
        "",
        "**Blockers**",
        "",
        "-",
      ].join("\n"),
    );
    fixture.destroy();
  });

  it("copies Markdown without links as plain IDs", async () => {
    const { writeText, fixture } = await copied((app) => app.dailyFormat.set("markdown"));
    const text = writeText.mock.calls[0][0] as string;
    expect(text).toContain("- DEP-1232 — done");
    expect(text).not.toContain("](");
    fixture.destroy();
  });

  it("offers the text for manual copying when the clipboard is unavailable", async () => {
    const harness = setup();
    harness.app.view.set("daily");
    await harness.app.loadDaily();
    harness.app.boardOrder.set(["ticket:exp-identity", "change:" + repo + "/add-work-dashboard"]);
    Object.defineProperty(navigator, "clipboard", { value: { writeText: vi.fn().mockRejectedValue(new Error("denied")) }, configurable: true });
    await harness.app.copyDaily();
    expect(harness.app.manualCopy()).toEqual({ title: "Copy this daily", text: plain, multiline: true });
    harness.fixture.destroy();
  });

  it("copies nothing before the digest and the board are loaded", async () => {
    const harness = setup();
    const writeText = vi.fn();
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
    await harness.app.copyDaily();
    expect(writeText).not.toHaveBeenCalled();
    harness.fixture.destroy();
  });
});

describe("Daily agent digests", () => {
  const withDigests = (digests: { covers: string[]; text: string }[], base: DailyDigest = digest()) => ({ ...base, digests });
  const shown = async (daily: DailyDigest) => {
    const harness = setup({ daily });
    harness.app.view.set("daily");
    await harness.app.loadDaily();
    harness.app.boardOrder.set(["ticket:exp-identity", "change:" + repo + "/add-work-dashboard"]);
    harness.fixture.detectChanges();
    return harness;
  };

  it("shows a matching digest with the mark on a Yesterday line, with status and collapsed observations unchanged", async () => {
    const { fixture, element } = await shown(withDigests([{ covers: ["r0", "r1"], text: "Merged the retry policy." }]));
    const first = element.querySelector('[data-testid="daily-yesterday"] .daily-line')!;
    const summary = first.querySelector('[data-testid="daily-summary"]')!;
    expect(summary.textContent).toContain("Merged the retry policy.");
    expect(summary.textContent).not.toContain("Retry policy for export jobs merged");
    expect(summary.querySelector('[data-testid="daily-digest-mark"]')!.textContent).toBe("agent digest");
    expect(first.querySelector(".daily-status")!.textContent).toBe("Recorded done");
    expect(first.querySelector("details summary")!.textContent).toContain("1 other observation");
    expect(first.querySelector("details li")!.textContent).toContain("Started the retry work");
    expect(element.querySelectorAll('[data-testid="daily-digest-mark"]')).toHaveLength(1);
    fixture.destroy();
  });

  it("keeps one digest issue across repeated failure and clears it after an absent or null successful response", async () => {
    let payload: DailyDigest = {
      ...digest(),
      digestIssue: { category: "unavailable", message: "Saved digests are temporarily unavailable." },
    };
    const harness = await shown(payload);
    const { app, fixture, element } = harness;
    const serve = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation(async (input, init) =>
      String(input) === "/api/daily" ? Response.json(payload) : serve(input, init));

    expect(element.querySelectorAll("[data-testid='daily-digest-issue']")).toHaveLength(1);
    expect(element.querySelector("[data-testid='daily-digest-issue']")!.textContent).toContain("temporarily unavailable");
    expect(element.querySelector("[data-testid='daily-summary']")!.textContent).toContain("Retry policy for export jobs merged");
    expect(element.querySelector("[data-testid='daily-today-summary']")!.textContent).toContain("Export retries wired to the job scheduler");
    await app.loadDaily();
    fixture.detectChanges();
    expect(element.querySelectorAll("[data-testid='daily-digest-issue']")).toHaveLength(1);
    expect(element.querySelector("[data-testid='daily-summary']")!.textContent).toContain("Retry policy for export jobs merged");

    payload = { ...digest(), digestIssue: null };
    await app.loadDaily();
    fixture.detectChanges();
    expect(element.querySelector("[data-testid='daily-digest-issue']")).toBeNull();
    expect(element.querySelector("[data-testid='daily-summary']")!.textContent).toContain("Retry policy for export jobs merged");
    expect(element.querySelector("[data-testid='daily-today-summary']")!.textContent).toContain("Export retries wired to the job scheduler");

    payload = digest();
    await app.loadDaily();
    fixture.detectChanges();
    expect(element.querySelector("[data-testid='daily-digest-issue']")).toBeNull();
    expect(element.querySelector("[data-testid='daily-summary']")!.textContent).toContain("Retry policy for export jobs merged");
    fixture.destroy();
  });

  it("shows a matching digest on a Today line", async () => {
    const { fixture, element } = await shown(withDigests([{ covers: ["x0", "x1"], text: "Scheduler hook is in." }]));
    const line = element.querySelector('[data-testid="daily-today-line"][data-card-key^="ticket:"]')!;
    expect(line.querySelector('[data-testid="daily-today-summary"]')!.textContent).toContain("Scheduler hook is in.");
    expect(line.querySelector('[data-testid="daily-digest-mark"]')).not.toBeNull();
    expect(line.querySelector("details summary")!.textContent).toContain("1 other observation today");
    fixture.destroy();
  });

  it("falls back to the latest summary when a later observation made the digest stale", async () => {
    const { fixture, element } = await shown(withDigests([{ covers: ["x0", "x1"], text: "Scheduler hook is in." }], {
      ...digest(),
      today: [item("exp-identity", "DEP-1240", [dep1240], "in_progress", entry("x2", hoursAgo(1), "Load test passed"), {
        others: [entry("x1", hoursAgo(2), "Export retries wired to the job scheduler; load test remains"), entry("x0", hoursAgo(5), "Wrote the scheduler hook")],
      })],
    }));
    const line = element.querySelector('[data-testid="daily-today-line"][data-card-key^="ticket:"]')!;
    expect(line.querySelector('[data-testid="daily-today-summary"]')!.textContent).toBe("Load test passed");
    expect(line.querySelector('[data-testid="daily-digest-mark"]')).toBeNull();
    fixture.destroy();
  });

  it("copies the digest text without the mark in both formats", async () => {
    const harness = await shown(withDigests([{ covers: ["r0", "r1"], text: "Merged the retry policy." }, { covers: ["x0", "x1"], text: "Scheduler hook is in." }]));
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
    await harness.app.copyDaily();
    expect(writeText).toHaveBeenLastCalledWith([
      "Yesterday (Thu 22 Oct):",
      "- DEP-1232 — done — Merged the retry policy.",
      "- add-work-dashboard — in progress — Review comments edit/delete",
      "- Tried a spike — in progress",
      "Today:",
      "- DEP-1240 — in progress — Scheduler hook is in.",
      "- add-work-dashboard",
      "Blockers:",
      "-",
    ].join("\n"));
    harness.app.dailyFormat.set("markdown");
    harness.app.dailyLinks.set(true);
    await harness.app.copyDaily();
    const markdown = writeText.mock.lastCall![0] as string;
    expect(markdown).toContain("- [DEP-1232](https://acme.atlassian.net/browse/DEP-1232) — done — Merged the retry policy.");
    expect(markdown).toContain("— in progress — Scheduler hook is in.");
    expect(markdown).not.toContain("agent digest");
    harness.fixture.destroy();
  });
});
