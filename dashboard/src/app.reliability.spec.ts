import { TestBed } from "@angular/core/testing";
import { Router } from "@angular/router";
import { Subject } from "rxjs";
import { describe, it, expect, vi, afterEach } from "vitest";
import { App, documentLimit, positionLimit } from "./app";
import { localDate } from "./api";
import { validTimezone } from "./range";
import { Catalog, Change, DailyDigest, DocumentContent } from "./models";
import { primeLiveRevision, sendRevision } from "./app-live-test-helpers";

const change: Change = {
  sourceId: "s",
  name: "demo",
  directoryName: "demo",
  variant: "v",
  archived: false,
  archivedOn: null,
  repositories: ["r"],
  documents: [{ path: "design.md", label: "design", fingerprint: "1:1" }],
  completedTasks: 2,
  totalTasks: 7,
  error: null,
};
const timeline = {
  snapshot: 1,
  rows: [],
  total: 0,
  nextOffset: null,
  insights: { longestSpans: [], completions: [], subsequentWork: [], days: [] },
};
const design: DocumentContent = {
  sourceId: "s",
  change: "demo",
  path: "design.md",
  location: "project",
  revision: "r1",
  text: "# Design\n\nBody",
  archived: false,
  canComment: true,
  tasks: [],
};

interface Call {
  path: string;
  method: string;
  body: any;
}
/** A fake server that records every request the app makes. */
function setup() {
  const calls: Call[] = [];
  const server = {
    calls,
    catalog: {
      repositories: [{ id: "r", label: "Project" }],
      sources: [],
      changes: [change],
      launchRepository: "r",
      revision: "1",
      notices: [],
      journalSnapshot: 5,
    } as Catalog,
    document: design,
    state: { schemaVersion: 1 } as any,
    stateStatus: 204,
    info: { timezone: "UTC" },
    daily: { day: null, previousDay: null, nextDay: null, items: [], today: [] } as DailyDigest,
    /** Zones the server answers with 400 "Unknown reporting timezone." although the browser knows them. */
    rejectedZones: [] as string[],
    /** When set, History answers with this status and message instead of a timeline. */
    timelineFailure: undefined as { status: number; error: string } | undefined,
    count: (path: string, method?: string) =>
      calls.filter((c) => c.path === path && (!method || c.method === method)).length,
  };
  vi.spyOn(globalThis, "fetch").mockImplementation(async (input, options) => {
    const path = String(input);
    const method = options?.method ?? "GET";
    const body = options?.body ? JSON.parse(options.body as string) : undefined;
    calls.push({ path, method, body });
    if (path === "/api/catalog") return Response.json(server.catalog);
    if (path === "/api/state" && method === "GET") return Response.json(server.state);
    if (path === "/api/state")
      return server.stateStatus === 204
        ? new Response(null, { status: 204 })
        : Response.json({ error: "Dashboard preferences exceed 64 KiB." }, { status: server.stateStatus });
    if (path === "/api/info") return Response.json(server.info);
    if (path === "/api/timeline" && server.timelineFailure)
      return Response.json({ error: server.timelineFailure.error }, { status: server.timelineFailure.status });
    if (path === "/api/timeline")
      return server.rejectedZones.includes(body.timezone)
        ? Response.json({ error: "Unknown reporting timezone." }, { status: 400 })
        : Response.json(timeline);
    if (path === "/api/daily")
      return Response.json(server.daily ?? { day: null, previousDay: null, nextDay: null, items: [], today: [] });
    if (path === "/api/search") return Response.json({ matches: [], limitReached: false, unavailable: [] });
    if (path === "/api/reviews") return Response.json({ items: [], unavailable: [] });
    if (path === "/api/reads/summary") return Response.json({ entries: {} });
    if (path === "/api/reads/get") return Response.json(null);
    if (path === "/api/reads/mark") return Response.json({ revision: body.revision, fingerprint: "1:1" });
    if (path === "/api/document")
      return Response.json(
        body.knownRevision === server.document.revision
          ? { revision: server.document.revision, unchanged: true }
          : { ...server.document, path: body.path, change: body.change },
      );
    throw new Error("Unexpected request: " + path);
  });
  // Zoneless change detection would start the app on its own; each test drives it explicitly.
  vi.spyOn(App.prototype, "ngAfterViewInit").mockImplementation(() => {});
  const events = new Subject();
  TestBed.configureTestingModule({
    imports: [App],
    providers: [
      {
        provide: Router,
        useValue: {
          events,
          navigate: vi.fn().mockResolvedValue(true),
          parseUrl: () => ({ queryParams: {}, root: { children: {} } }),
          url: "/ongoing",
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(App);
  const app = fixture.componentInstance;
  app.catalog.set(server.catalog);
  const hidden = vi.spyOn(document, "hidden", "get").mockReturnValue(false);
  return { app, fixture, server, events, hidden };
}
afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
  TestBed.resetTestingModule();
});
const puts = (server: ReturnType<typeof setup>["server"]) =>
  server.calls.filter((c) => c.path === "/api/state" && c.method === "PUT");

describe("history range", () => {
  it("does not steal focus from a connected reader input used while a document navigation is pending", async () => {
    const { app, fixture } = setup();
    const selected = {
      ...change,
      documents: [
        { path: "design.md", label: "design", fingerprint: "1:1" },
        { path: "tasks.md", label: "tasks", fingerprint: "1:2" },
      ],
    };
    app.selectedChange.set(selected);
    app.document.set(design);
    app["renderDocument"](design);
    fixture.detectChanges();
    const navigationControl = fixture.nativeElement.querySelector(".document-link") as HTMLButtonElement;
    const filter = fixture.nativeElement.querySelector("#document-filter-desktop") as HTMLInputElement;
    expect(navigationControl).not.toBeNull();
    expect(filter).not.toBeNull();
    navigationControl.focus();

    let finishDocument!: (response: Response) => void;
    const pendingDocument = new Promise<Response>((resolve) => { finishDocument = resolve; });
    const serve = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) =>
      String(input) === "/api/document" ? pendingDocument : serve(input, options),
    );
    const navigation = app["loadDocument"]({ sourceId: "s", change: "demo", path: "tasks.md", variant: "v" });
    filter.focus();
    finishDocument(Response.json({ ...design, path: "tasks.md", revision: "r2" }));
    await navigation;
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();

    expect(document.activeElement).toBe(filter);
    fixture.destroy();
  });

  it("uses the default relative range for a saved range without a kind and rolls it forward", async () => {
    const { app, server, fixture } = setup();
    server.state = {
      schemaVersion: 1,
      history: { from: "2026-01-01", to: "2026-01-31", timezone: "UTC", groupBy: "ticket", scale: "day" },
    };
    app["now"] = () => new Date("2026-10-24T12:00:00Z");
    await app["start"]();
    expect(app.range).toEqual({ kind: "relative", days: 30 });
    expect([app.from, app.to]).toEqual(["2026-09-25", "2026-10-24"]);
    app["now"] = () => new Date("2026-10-27T12:00:00Z");
    server.calls.length = 0;
    await app.loadHistory();
    expect([app.from, app.to]).toEqual(["2026-09-25", "2026-10-24"]);
    app["evaluate"]();
    await app.loadHistory();
    expect([app.from, app.to]).toEqual(["2026-09-28", "2026-10-27"]);
    expect(app.historyQuery?.from).toBe("2026-09-28T00:00:00.000Z");
    expect(app.historyQuery?.to).toBe("2026-10-28T00:00:00.000Z");
    fixture.destroy();
  });

  it("shows the server's version label in the footer", async () => {
    const { app, server, fixture } = setup();
    server.info = { timezone: "UTC", version: "0.3.9001 dev 50df7b8 (2026-10-28 09:12 UTC)" } as typeof server.info;
    await app["start"]();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector(".page-footer .version-label")?.textContent).toBe(
      "0.3.9001 dev 50df7b8 (2026-10-28 09:12 UTC)",
    );
    fixture.destroy();
  });

  it("shows no version label when the server sends none", async () => {
    const { app, fixture } = setup();
    await app["start"]();
    fixture.detectChanges();
    expect(app.versionLabel()).toBe("");
    expect(fixture.nativeElement.querySelector(".version-label")).toBeNull();
    fixture.destroy();
  });

  it("accepts an existing protected session after a spent launch token", async () => {
    const { app, server, fixture } = setup();
    window.history.replaceState(null, "", "/ongoing#connect=already-used");
    const serve = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation(async (input, options) =>
      String(input) === "/api/bootstrap"
        ? Response.json({ error: "Launch token expired." }, { status: 401 })
        : serve(input, options));

    await app["start"]();

    expect(server.calls.filter((call) => call.path === "/api/info")).toHaveLength(1);
    expect(app.ready()).toBe(true);
    expect(app.unauthorized()).toBe(false);
    expect(window.location.hash).toBe("");
    fixture.destroy();
  });

  it("does not enter the workspace when a spent launch token cannot prove the existing session", async () => {
    const { app, fixture } = setup();
    window.history.replaceState(null, "", "/ongoing#connect=already-used");
    const requests: string[] = [];
    vi.mocked(globalThis.fetch).mockImplementation(async (input) => {
      const path = String(input);
      requests.push(path);
      return Response.json({ error: "Unauthorized." }, { status: 401 });
    });

    await app["start"]();

    expect(requests).toEqual(["/api/bootstrap", "/api/info"]);
    expect(app.ready()).toBe(false);
    expect(app.unauthorized()).toBe(true);
    expect(window.location.hash).toBe("");
    fixture.destroy();
  });

  it("shows a retry after startup failure and keeps startup retries single-flight", async () => {
    const { app, fixture } = setup();
    const serve = vi.mocked(globalThis.fetch).getMockImplementation()!;
    let catalogCalls = 0;
    vi.mocked(globalThis.fetch).mockImplementation(async (input, options) => {
      if (String(input) === "/api/catalog" && catalogCalls++ === 0)
        return Response.json({ error: "Temporary catalog failure." }, { status: 503 });
      return serve(input, options);
    });

    await Promise.all([app["start"](), app["start"]()]);

    expect(catalogCalls).toBe(1);
    expect(app.ready()).toBe(false);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain("Could not open your workspace");
    expect(fixture.nativeElement.textContent).toContain("Retry");
    await app.retryStartup();
    expect(app.ready()).toBe(true);
    fixture.destroy();
  });

  it("retries a network-failed launch token from memory without starting a browser interval", async () => {
    const { app, fixture, events } = setup();
    window.history.replaceState(null, "", "/ongoing#connect=retryable-token");
    const serve = vi.mocked(globalThis.fetch).getMockImplementation()!;
    const tokens: string[] = [];
    let bootstrapCalls = 0;
    let finishBootstrap!: (response: Response) => void;
    const delayedBootstrap = new Promise<Response>((resolve) => { finishBootstrap = resolve; });
    vi.mocked(globalThis.fetch).mockImplementation(async (input, options) => {
      if (String(input) === "/api/bootstrap") {
        bootstrapCalls++;
        tokens.push(JSON.parse(options!.body as string).token);
        if (bootstrapCalls === 1) throw new TypeError("Failed to fetch");
        return delayedBootstrap;
      }
      return serve(input, options);
    });
    const interval = vi.spyOn(globalThis, "setInterval");

    await app["start"]();
    expect(app.ready()).toBe(false);
    expect(app.error()).toContain("Failed to fetch");
    expect(window.location.hash).toBe("");

    const retry = app.retryStartup();
    const duplicateRetry = app.retryStartup();
    expect(bootstrapCalls).toBe(2);
    expect(tokens).toEqual(["retryable-token", "retryable-token"]);
    finishBootstrap(Response.json({}));
    await Promise.all([retry, duplicateRetry]);

    expect(app.ready()).toBe(true);
    expect(interval.mock.calls).toHaveLength(0);
    expect(events.observers).toHaveLength(1);
    fixture.destroy();
  });

  it("does not write dates when History loads and saves the timezone only after the user chose it", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    app["evaluate"]();
    await app.loadHistory();
    await app.loadHistory();
    await new Promise((resolve) => setTimeout(resolve, 450));
    expect(puts(server)).toHaveLength(0);
    await app["persist"]();
    expect(puts(server)).toHaveLength(1);
    const saved = puts(server)[0].body;
    expect(saved.history.range).toEqual({ kind: "relative", days: 30 });
    expect(saved.history).not.toHaveProperty("from");
    expect(saved.history).not.toHaveProperty("to");
    expect(saved.history).not.toHaveProperty("timezone");
    app.timezoneInput = "Europe/Warsaw";
    await app.timezoneChanged();
    await app["persist"]();
    expect(puts(server).at(-1)!.body.history.timezone).toBe("Europe/Warsaw");
    fixture.destroy();
  });

  it("rejects an invalid timezone inline without querying or remembering it", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    app["evaluate"]();
    await app.loadHistory();
    server.calls.length = 0;
    app.timezoneInput = "Mars/Olympus";
    await app.timezoneChanged();
    expect(app.timezoneError()).toContain("Mars/Olympus");
    expect(app.timezone).toBe("UTC");
    expect(server.calls).toHaveLength(0);
    await app["persist"]();
    expect(puts(server)[0].body.history).not.toHaveProperty("timezone");
    fixture.destroy();
  });

  it("keeps the previous zone, results and saved preferences when the server rejects a zone the browser accepts", async () => {
    const { app, server, fixture } = setup();
    server.rejectedZones = ["+02:00"];
    expect(validTimezone("+02:00")).toBe(true);
    app.ready.set(true);
    app["evaluate"]();
    await app.loadHistory();
    const before = app.timeline();
    const historyQuery = app.historyQuery;
    server.calls.length = 0;
    app.timezoneInput = "+02:00";
    await app.timezoneChanged();
    expect(app.timezoneError()).toContain("+02:00");
    expect(app.timezone).toBe("UTC");
    expect(app.timeline()).toBe(before);
    expect(app.historyQuery).toBe(historyQuery);
    expect(app.error()).toBe("");
    expect(server.calls.map((c) => c.path)).toEqual(["/api/timeline"]);
    await app["persist"]();
    expect(puts(server)[0].body.history).not.toHaveProperty("timezone");
    // A zone both sides accept is still remembered, and it clears the message.
    app.timezoneInput = "Europe/Warsaw";
    await app.timezoneChanged();
    expect(app.timezoneError()).toBe("");
    await app["persist"]();
    expect(puts(server).at(-1)!.body.history.timezone).toBe("Europe/Warsaw");
    fixture.destroy();
  });

  it("ignores a saved zone the server rejects and starts in the server zone", async () => {
    const { app, server, fixture } = setup();
    server.rejectedZones = ["America/Ciudad_Juarez"];
    server.state = { schemaVersion: 1, history: { timezone: "America/Ciudad_Juarez" } };
    await app["start"]();
    expect(app.timezone).toBe("UTC");
    await app["persist"]();
    expect(puts(server)[0].body.history).not.toHaveProperty("timezone");
    fixture.destroy();
  });

  it("keeps a saved zone and starts normally when the start-up probe fails for another reason", async () => {
    const { app, server, fixture } = setup();
    server.timelineFailure = { status: 503, error: "Journal unavailable." };
    server.state = { schemaVersion: 1, history: { timezone: "Europe/Warsaw" } };
    await app["start"]();
    expect(app.ready()).toBe(true);
    expect(app["clockTimer"]).toBeDefined();
    expect(app.timezone).toBe("Europe/Warsaw");
    await app["persist"]();
    expect(puts(server)[0].body.history.timezone).toBe("Europe/Warsaw");
    fixture.destroy();
  });

  it("does not treat a 400 with another message as a rejected zone", async () => {
    const { app, server, fixture } = setup();
    server.timelineFailure = { status: 400, error: "Something else is wrong." };
    server.state = { schemaVersion: 1, history: { timezone: "Europe/Warsaw" } };
    await app["start"]();
    expect(app.ready()).toBe(true);
    expect(app.timezone).toBe("Europe/Warsaw");
    app.timezoneInput = "Asia/Tokyo";
    await app.timezoneChanged();
    expect(app.timezoneError()).toBe("");
    expect(app.timezone).toBe("Europe/Warsaw");
    fixture.destroy();
  });

  it("rejects a range whose start follows its end before querying", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    app["evaluate"]();
    server.calls.length = 0;
    const before = app.range;
    app.fromInput = "2026-10-20";
    app.toInput = "2026-10-10";
    await app.datesChanged();
    expect(app.rangeError()).toContain("From must not be after Through");
    expect(server.calls).toHaveLength(0);
    expect(app.range).toBe(before);
    app.fromInput = "2026-10-01";
    await app.datesChanged();
    expect(app.rangeError()).toBe("");
    expect(app.range).toEqual({ kind: "custom", from: "2026-10-01", to: "2026-10-10" });
    expect(server.count("/api/timeline")).toBe(1);
    fixture.destroy();
  });

  it("keeps a focused day transient so the remembered range survives", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    app["now"] = () => new Date("2026-10-27T12:00:00Z");
    app["evaluate"]();
    await app.selectPreset(90);
    await app.focusDay("2026-10-20");
    expect([app.from, app.to]).toEqual(["2026-10-20", "2026-10-20"]);
    await app["persist"]();
    expect(puts(server).at(-1)!.body.history.range).toEqual({ kind: "relative", days: 90 });
    expect(JSON.stringify(puts(server).at(-1)!.body)).not.toContain("2026-10-20");
    await app.clearFocus();
    expect(app.focusedDay()).toBeNull();
    expect([app.from, app.to]).toEqual(["2026-07-30", "2026-10-27"]);
    fixture.destroy();
  });
});

describe("bounded preferences", () => {
  it("keeps the most recent 100 reading positions and 200 remembered documents", async () => {
    const { app, server, fixture } = setup();
    for (let i = 0; i < 150; i++) {
      app.document.set({ ...design, change: "c" + i });
      app.onScroll({ target: { scrollTop: i } } as unknown as Event);
    }
    const positions = Object.keys(app.preferences.positions!);
    expect(positions).toHaveLength(positionLimit);
    expect(positions.at(-1)).toBe("s:c149:design.md:");
    expect(app.preferences.positions!["s:c149:design.md:"]).toBe(149);
    expect(app.preferences.positions!["s:c0:design.md:"]).toBeUndefined();
    for (let i = 0; i < 250; i++) await app["loadDocument"]({ sourceId: "s", change: "d" + i, path: "design.md" });
    const documents = Object.keys(app.preferences.documents!);
    expect(documents).toHaveLength(documentLimit);
    expect(documents.at(-1)).toBe("s:d249");
    expect(documents).not.toContain("s:d0");
    expect(server.count("/api/document")).toBe(250);
    fixture.destroy();
  });

  it("reports repeated save failures once, without the global error", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    server.stateStatus = 413;
    await app["persist"]();
    expect(app.preferencesNotice()).toContain("could not be saved");
    const first = app.preferencesNotice();
    for (let i = 0; i < 3; i++) {
      app.preferences.theme = i % 2 ? "light" : "dark";
      await app["persist"]();
    }
    expect(puts(server)).toHaveLength(4);
    expect(app.preferencesNotice()).toBe(first);
    expect(app.error()).toBe("");
    app.preferencesNotice.set("");
    app.preferences.theme = "light";
    await app["persist"]();
    expect(app.preferencesNotice()).toBe("");
    expect(app.error()).toBe("");
    expect(app.preferences.theme).toBe("light");
    fixture.destroy();
  });
});

describe("revision-driven updates", () => {
  async function open(app: App, server: ReturnType<typeof setup>["server"], clock: { now: Date }) {
    app.ready.set(true);
    app["now"] = () => clock.now;
    app["evaluate"]();
    app["journalSnapshot"] = server.catalog.journalSnapshot;
    app.searchText = "needle";
    await app.search();
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md" });
    primeLiveRevision(app);
    server.calls.length = 0;
  }

  it("counts a search superseded by a newer search as complete so the live refresh is not paused", async () => {
    const { app, fixture } = setup();
    app.searchText = "needle";
    const earlier = app.search();
    const newer = app.search();
    expect(await earlier).toBe(true);
    expect(await newer).toBe(true);
    app.searchText = "other";
    const superseded = app.search();
    app.searchText = "";
    app["searchEpoch"]++;
    expect(await superseded).toBe(true);
    fixture.destroy();
  });

  describe("a load superseded by a foreground call of the same loader (ANALYSIS-702)", () => {
    const stubLive = (app: App) => {
      app["liveConnection"] = {
        stop: async () => {},
        markReconciling: () => app.liveStatus.set("reconnecting"),
        markReconciled: () => app.liveStatus.set("live"),
        markPaused: () => app.liveStatus.set("paused"),
        authorizationLost: () => {},
      } as any;
    };
    /** The first request of `path` waits for `release`; later requests are served by `later` (default: the fake server). */
    const holdFirst = (path: string, later?: () => Response) => {
      const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
      let release!: (response: Response) => void;
      let seen = 0;
      vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
        const url = String(input);
        if (url === "/api/activity/changes") return Promise.resolve(Response.json([]));
        if (url === "/api/ongoing/unlinked") return Promise.resolve(Response.json({ snapshot: 7, entries: [], truncated: false }));
        if (url !== path) return realFetch(input, options);
        if (++seen === 1) return new Promise<Response>((resolve) => { release = resolve; });
        return later ? Promise.resolve(later()) : realFetch(input, options);
      });
      return { release: (response: Response) => release(response), requests: () => seen };
    };

    it("ends Live with no new revision when a scope change reloads the reviews during a live reconcile", async () => {
      const { app, fixture } = setup();
      app.ready.set(true);
      primeLiveRevision(app);
      stubLive(app);
      const held = holdFirst("/api/reviews");

      const refresh = sendRevision(app, { documents: 2, dirty: ["documents"] });
      await vi.waitFor(() => expect(held.requests()).toBe(1));
      app.selectAll();
      await vi.waitFor(() => expect(held.requests()).toBe(2));
      held.release(Response.json({ items: [], unavailable: [] }));
      await refresh;

      expect(app["revisionTracker"].pending()).toEqual([]);
      expect(app.liveStatus()).toBe("live");
      expect(held.requests()).toBe(2);
      fixture.destroy();
    });

    it("still fails the reconcile when the newer call genuinely fails", async () => {
      const { app, fixture } = setup();
      app.ready.set(true);
      primeLiveRevision(app);
      stubLive(app);
      const held = holdFirst("/api/reviews", () => Response.json({ error: "Reviews are unavailable." }, { status: 500 }));

      const refresh = sendRevision(app, { documents: 2, dirty: ["documents"] });
      await vi.waitFor(() => expect(held.requests()).toBe(1));
      app.selectAll();
      await vi.waitFor(() => expect(held.requests()).toBe(2));
      held.release(Response.json({ items: [], unavailable: [] }));
      await refresh;

      expect(app.liveStatus()).toBe("paused");
      expect(app["revisionTracker"].pending().length).toBeGreaterThan(0);
      fixture.destroy();
    });

    it("reports unauthorized for a superseded search when the newer search is refused", async () => {
      const { app, fixture } = setup();
      app.ready.set(true);
      stubLive(app);
      app.searchText = "needle";
      const held = holdFirst("/api/search", () => Response.json({ error: "Unauthorized." }, { status: 401 }));

      const superseded = app.search();
      await vi.waitFor(() => expect(held.requests()).toBe(1));
      const newer = app.search();
      held.release(Response.json({ matches: [], limitReached: false, unavailable: [] }));

      expect(await newer).toBe(false);
      expect(await superseded).toBe(false);
      expect(app.unauthorized()).toBe(true);
      fixture.destroy();
    });

    it("reports unauthorized for a search superseded by a cleared query (ANALYSIS-741)", async () => {
      const { app, fixture } = setup();
      app.ready.set(true);
      stubLive(app);
      app.searchText = "needle";
      const held = holdFirst("/api/search");

      const superseded = app.search();
      await vi.waitFor(() => expect(held.requests()).toBe(1));
      app.searchText = "";
      app["searchEpoch"]++;
      held.release(Response.json({ error: "Unauthorized." }, { status: 401 }));

      expect(await superseded).toBe(false);
      expect(app.unauthorized()).toBe(true);
      fixture.destroy();
    });

    it("keeps the journal revision pending when a live History reload is superseded by Load more (ANALYSIS-740)", async () => {
      const { app, fixture } = setup();
      const clock = { now: new Date("2026-10-27T10:00:00Z") };
      app.view.set("history");
      app.ready.set(true);
      app["now"] = () => clock.now;
      app["evaluate"]();
      await app.loadHistory();
      primeLiveRevision(app);
      stubLive(app);
      app.timeline.set({ ...timeline, nextOffset: 10 });
      const held = holdFirst("/api/timeline");

      const refresh = sendRevision(app, { journal: 2, dirty: ["journal"] });
      await vi.waitFor(() => expect(held.requests()).toBe(1));
      const more = app.loadHistory(true);
      held.release(Response.json({ ...timeline, nextOffset: null }));
      await more;
      await refresh;

      expect(app["revisionTracker"].pending()).toContain("journal");
      expect(app.liveStatus()).toBe("paused");
      fixture.destroy();
    });
  });

  it("attaches task anchors again after the rendered document is replaced by a read-baseline refresh", async () => {
    const { app, fixture } = setup();
    const host = document.createElement("div");
    host.className = "markdown";
    host.innerHTML = '<ul><li data-start="3">1.2 Implement</li></ul>';
    document.body.appendChild(host);
    try {
      const tasks = { ...design, tasks: [{ number: "1.2", text: "Implement", line: 3 }] };
      app.document.set(tasks);
      app["renderDocument"](tasks);
      host.querySelector("li")!.removeAttribute("id");
      app["renderDocument"](tasks);
      await new Promise((done) => setTimeout(done, 5));
      expect(host.querySelector("li")!.id).toBe("task-1.2");
    } finally {
      host.remove();
      fixture.destroy();
    }
  });

  it("does no REST work for identical vectors and has no periodic catalog poll", async () => {
    const { app, server, fixture } = setup();
    const clock = { now: new Date("2026-10-27T10:00:00Z") };
    await open(app, server, clock);
    const rendered = app.html();
    const current = app.document();
    const same = app["revisionTracker"].desired()!;
    await app["receiveRevision"](same);
    await app["receiveRevision"]({ ...same, dirty: ["documents", "journal"] });
    expect(server.calls).toEqual([]);
    expect(puts(server)).toHaveLength(0);
    expect(app.html()).toBe(rendered);
    expect(app.document()).toBe(current);
    expect("timer" in app).toBe(false);
    fixture.destroy();
  });

  it("settles the startup Snapshot and keeps duplicate checkpoints Live", async () => {
    const { app, server, fixture } = setup();
    const clock = { now: new Date("2026-10-27T10:00:00Z") };
    app.ready.set(true);
    app["now"] = () => clock.now;
    app.view.set("history");
    app["evaluate"]();
    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
    } as any;
    const initial = {
      instance: "startup-instance",
      catalog: 1,
      documents: 1,
      journal: 5,
      digests: 1,
      reads: 1,
      dirty: [],
    };

    await app["receiveRevision"](initial);

    expect(app["revisionTracker"].pending()).toEqual([]);
    expect(app.lastSuccessfulRefresh()).toBe(clock.now.toISOString());
    expect(app.liveStatus()).toBe("live");
    const requestsAfterSnapshot = [...server.calls];
    await app["receiveRevision"](initial);
    expect(server.calls).toEqual(requestsAfterSnapshot);
    expect(app.liveStatus()).toBe("live");
    fixture.destroy();
  });

  it("uses one Daily read for the initial Snapshot, overlapping activation, and journal or digest revisions", async () => {
    const { app, server, fixture } = setup();
    const clock = { now: new Date("2026-10-27T10:00:00Z") };
    app.ready.set(true);
    app["now"] = () => clock.now;
    app.view.set("daily");
    app["evaluate"]();
    vi.spyOn(app, "loadOngoing").mockResolvedValue(true);
    vi.spyOn(app, "loadActivity").mockResolvedValue(true);
    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
    } as any;
    let finishDaily!: (response: Response) => void;
    const originalFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) === "/api/daily") {
        server.calls.push({
          path: "/api/daily",
          method: options?.method ?? "GET",
          body: options?.body ? JSON.parse(options.body as string) : undefined,
        });
        return new Promise<Response>((resolve) => { finishDaily = resolve; });
      }
      return originalFetch(input, options);
    });
    const daily = { day: null, previousDay: null, nextDay: null, items: [], today: [] };
    const initial = {
      instance: "daily-startup-instance",
      catalog: 1,
      documents: 1,
      journal: 5,
      digests: 3,
      reads: 1,
      dirty: [],
    };

    const snapshot = app["receiveRevision"](initial);
    await vi.waitFor(() => expect(server.count("/api/daily")).toBe(1));
    const activation = app["activateLiveScope"]();
    await Promise.resolve();
    expect(server.count("/api/daily")).toBe(1);
    finishDaily(Response.json(daily));
    await Promise.all([snapshot, activation]);

    expect(server.count("/api/daily")).toBe(1);
    expect(app.dailyData()?.digest).toEqual(daily);
    expect(app["revisionTracker"].pending()).toEqual([]);
    expect(app.liveStatus()).toBe("live");

    vi.mocked(globalThis.fetch).mockImplementation(originalFetch);
    await app["receiveRevision"]({ ...initial, journal: 6 });
    expect(server.count("/api/daily")).toBe(2);
    await app["receiveRevision"]({ ...initial, journal: 6, digests: 4 });
    expect(server.count("/api/daily")).toBe(3);
    await app["receiveRevision"]({ ...initial, journal: 7, digests: 5 });
    expect(server.count("/api/daily")).toBe(4);
    expect(app["revisionTracker"].pending()).toEqual([]);
    expect(app.liveStatus()).toBe("live");
    fixture.destroy();
  });

  it("keeps Daily journal and digest revisions pending when the single Daily read fails", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    app.view.set("daily");
    app["evaluate"]();
    vi.spyOn(app, "loadOngoing").mockResolvedValue(true);
    vi.spyOn(app, "loadActivity").mockResolvedValue(true);
    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
    } as any;
    const originalFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/daily") return originalFetch(input, options);
      server.calls.push({ path: "/api/daily", method: options?.method ?? "GET", body: undefined });
      return Promise.resolve(Response.json({ error: "Daily unavailable." }, { status: 503 }));
    });
    const initial = {
      instance: "daily-failure-instance",
      catalog: 1,
      documents: 1,
      journal: 5,
      digests: 3,
      reads: 1,
      dirty: [],
    };

    await app["receiveRevision"](initial);

    expect(server.count("/api/daily")).toBe(1);
    expect(app["revisionTracker"].pending()).toContain("journal");
    expect(app["revisionTracker"].pending()).toContain("digests");
    expect(app.liveStatus()).toBe("paused");
    expect(app.dailyData()).toBeNull();
    fixture.destroy();
  });

  it("does not publish a superseded Daily response while the latest selected day is pending", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    app.view.set("daily");
    app.dailyDay = "2026-01-01";
    app["evaluate"]();
    vi.spyOn(app, "loadOngoing").mockResolvedValue(true);
    vi.spyOn(app, "loadActivity").mockResolvedValue(true);
    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
    } as any;
    const responses: Array<(response: Response) => void> = [];
    const originalFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/daily") return originalFetch(input, options);
      server.calls.push({ path: "/api/daily", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
      return new Promise<Response>((resolve) => responses.push(resolve));
    });
    const initial = {
      instance: "daily-scope-instance",
      catalog: 1,
      documents: 1,
      journal: 5,
      digests: 3,
      reads: 1,
      dirty: [],
    };
    const snapshot = app["receiveRevision"](initial);
    await vi.waitFor(() => expect(server.count("/api/daily")).toBe(1));

    app.dailyDay = "2025-12-31";
    app["selectionEpoch"]++;
    const activation = app["activateLiveScope"]();
    responses[0](Response.json({ day: "2026-01-01", previousDay: null, nextDay: null, items: [], today: [] }));
    await vi.waitFor(() => expect(server.count("/api/daily")).toBe(2));

    expect(app.dailyDay).toBe("2025-12-31");
    expect(app.dailyData()).toBeNull();
    expect(app.dailyLoading()).toBe(true);
    responses[1](Response.json({ day: "2025-12-31", previousDay: null, nextDay: null, items: [], today: [] }));
    await Promise.all([snapshot, activation]);

    expect(app.dailyData()?.digest.day).toBe("2025-12-31");
    expect(app["revisionTracker"].pending()).toEqual([]);
    expect(app.liveStatus()).toBe("live");
    fixture.destroy();
  });

  it.each(["timezone", "repositories", "local-day"] as const)(
    "keeps reconciliation pending until the latest Daily %s scope succeeds",
    async (scopeChange) => {
      const { app, server, fixture } = setup();
      const clock = { now: new Date("2026-03-28T23:59:59.000Z") };
      app.ready.set(true);
      app["now"] = () => clock.now;
      app.view.set("daily");
      app.dailyDay = "2026-03-25";
      app["evaluate"]();
      vi.spyOn(app, "loadHistory").mockResolvedValue(true);
      vi.spyOn(app, "loadOngoing").mockResolvedValue(true);
      vi.spyOn(app, "loadActivity").mockResolvedValue(true);
      app["liveConnection"] = {
        stop: async () => {},
        markReconciling: () => app.liveStatus.set("reconnecting"),
        markReconciled: () => app.liveStatus.set("live"),
        markPaused: () => app.liveStatus.set("paused"),
      } as any;
      const responses: Array<(response: Response) => void> = [];
      const originalFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
      vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
        if (String(input) !== "/api/daily") return originalFetch(input, options);
        server.calls.push({ path: "/api/daily", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
        return new Promise<Response>((resolve) => responses.push(resolve));
      });
      const initial = {
        instance: "daily-context-instance",
        catalog: 1,
        documents: 1,
        journal: 5,
        digests: 3,
        reads: 1,
        dirty: [],
      };
      const selectionEpoch = app["selectionEpoch"];
      const snapshot = app["receiveRevision"](initial);
      await vi.waitFor(() => expect(server.count("/api/daily")).toBe(1));

      let contextRefresh: Promise<unknown> = Promise.resolve();
      if (scopeChange === "timezone") {
        app.timezoneInput = "Europe/Warsaw";
        contextRefresh = app.timezoneChanged();
      } else if (scopeChange === "repositories") {
        app.selectedRepos.set(["r"]);
        contextRefresh = app.refreshData();
      } else {
        clock.now = new Date("2026-03-29T00:00:01.000Z");
        app["timeChanged"]();
      }
      expect(app["selectionEpoch"]).toBe(selectionEpoch);
      if (scopeChange === "timezone") await vi.waitFor(() => expect(app.timezone).toBe("Europe/Warsaw"));
      expect(server.count("/api/daily")).toBe(1);
      responses[0](Response.json({ day: "2026-03-25", previousDay: null, nextDay: null, items: [], today: [] }));
      await vi.waitFor(() => expect(server.count("/api/daily")).toBe(2));

      expect(app.dailyData()).toBeNull();
      expect(app.dailyLoading()).toBe(true);
      expect(app.liveStatus()).toBe("reconnecting");
      expect(app["revisionTracker"].pending()).not.toEqual([]);
      if (scopeChange === "timezone") expect(server.calls.filter((call) => call.path === "/api/daily")[1].body.timezone).toBe("Europe/Warsaw");
      if (scopeChange === "repositories") expect(server.calls.filter((call) => call.path === "/api/daily")[1].body.repositories).toEqual(["r"]);

      responses[1](Response.json({ day: "2026-03-25", previousDay: null, nextDay: null, items: [], today: [] }));
      await Promise.all([snapshot, contextRefresh]);
      expect(app.dailyData()?.digest.day).toBe("2026-03-25");
      expect(app.dailyLoading()).toBe(false);
      expect(app["revisionTracker"].pending()).toEqual([]);
      expect(app.liveStatus()).toBe("live");
      fixture.destroy();
    },
  );

  it("drains the latest Daily scope after a stale error and keeps a current authorization failure real", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    app.view.set("daily");
    app.dailyDay = "2026-01-01";
    app["evaluate"]();
    vi.spyOn(app, "loadOngoing").mockResolvedValue(true);
    vi.spyOn(app, "loadActivity").mockResolvedValue(true);
    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
      authorizationLost: vi.fn(),
    } as any;
    let failOld!: (response: Response) => void;
    const originalFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/daily") return originalFetch(input, options);
      server.calls.push({ path: "/api/daily", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
      if (server.count("/api/daily") === 1)
        return new Promise<Response>((resolve) => { failOld = resolve; });
      return Promise.resolve(Response.json({ error: "Latest authorization expired." }, { status: 401 }));
    });
    const initial = {
      instance: "daily-scope-error-instance",
      catalog: 1,
      documents: 1,
      journal: 5,
      digests: 3,
      reads: 1,
      dirty: [],
    };
    const snapshot = app["receiveRevision"](initial);
    await vi.waitFor(() => expect(server.count("/api/daily")).toBe(1));
    app.dailyDay = "2025-12-31";
    app["selectionEpoch"]++;
    const activation = app["activateLiveScope"]();
    failOld(Response.json({ error: "Old day unavailable." }, { status: 503 }));
    await vi.waitFor(() => expect(server.count("/api/daily")).toBe(2));
    await Promise.all([snapshot, activation]);

    expect(app.dailyData()).toBeNull();
    expect(app.error()).toContain("Latest authorization expired.");
    expect(app.error()).not.toContain("Old day unavailable.");
    expect(app.unauthorized()).toBe(true);
    expect(app["revisionTracker"].pending()).toContain("journal");
    expect(app["revisionTracker"].pending()).toContain("digests");
    expect(app.liveStatus()).toBe("paused");
    fixture.destroy();
  });

  it("loads Daily once when the active view changes after journal and digest updates", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    app.view.set("history");
    app["evaluate"]();
    vi.spyOn(app, "loadHistory").mockResolvedValue(true);
    vi.spyOn(app, "loadOngoing").mockResolvedValue(true);
    vi.spyOn(app, "loadActivity").mockResolvedValue(true);
    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
    } as any;
    const initial = primeLiveRevision(app);
    await app["receiveRevision"]({ ...initial, journal: 2, digests: 2 });
    expect(server.count("/api/daily")).toBe(0);
    expect(app["revisionTracker"].pending()).toEqual([]);

    app.view.set("daily");
    await app["activateLiveScope"]();

    expect(server.count("/api/daily")).toBe(1);
    expect(app["revisionTracker"].pending()).toEqual([]);
    expect(app.liveStatus()).toBe("live");
    fixture.destroy();
  });

  it("refreshes document-scoped data once on a document revision and leaves journal views alone", async () => {
    const { app, server, fixture } = setup();
    const clock = { now: new Date("2026-10-27T10:00:00Z") };
    await open(app, server, clock);
    server.catalog = { ...server.catalog, revision: "2" };
    server.document = { ...server.document, revision: "r2", text: "# Design\n\nUpdated" };
    await sendRevision(app, { documents: 2, dirty: ["documents"] });
    expect(server.count("/api/search")).toBe(1);
    expect(server.count("/api/timeline")).toBe(0);
    expect(server.count("/api/reviews")).toBe(1);
    expect(server.count("/api/reads/summary")).toBe(1);
    expect(server.count("/api/catalog")).toBe(1);
    expect(server.count("/api/document")).toBe(1);
    expect(app.document()?.text).toContain("Updated");
    expect(app.lastSuccessfulRefresh()).not.toBeNull();
    fixture.destroy();
  });

  it("does not lose the reads revision when a same-document baseline response overlaps a document refresh", async () => {
    const { app, server, fixture } = setup();
    const clock = { now: new Date("2026-10-27T10:00:00Z") };
    app.ready.set(true);
    app["now"] = () => clock.now;
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md", variant: "v" });
    expect(app.document()?.revision).toBe(server.document.revision);
    primeLiveRevision(app);
    server.calls.length = 0;
    server.catalog = { ...server.catalog, revision: "2" };
    const stored = { revision: server.document.revision, text: server.document.text };
    server.document = { ...server.document, revision: "r-next", text: "# Design\n\nLatest text" };

    let finishBaseline!: (response: Response) => void;
    let baselineReads = 0;
    const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/reads/get") return realFetch(input, options);
      server.calls.push({ path: "/api/reads/get", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
      return ++baselineReads === 1
        ? new Promise<Response>((resolve) => { finishBaseline = resolve; })
        : Promise.resolve(Response.json(stored));
    });

    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
    } as any;
    const refresh = sendRevision(app, { documents: 2, reads: 2, dirty: ["documents", "reads"] });
    await vi.waitFor(() => expect(server.count("/api/reads/get")).toBe(1));
    await vi.waitFor(() => expect(app.document()?.revision).toBe("r-next"));
    finishBaseline(Response.json({ revision: stored.revision, text: stored.text }));

    await refresh;
    expect(app.document()?.text).toContain("Latest text");
    expect(app.baseline()?.revision).toBe(stored.revision);
    expect(app["revisionTracker"].pending()).toEqual([]);
    expect(app.liveStatus()).toBe("live");
    expect(server.count("/api/reads/get")).toBe(2);
    fixture.destroy();
  });

  it("keeps a comment draft and pending revisions when the baseline retry fails", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md", variant: "v" });
    app.comment.set("Unsaved comment");
    primeLiveRevision(app);
    server.calls.length = 0;
    server.catalog = { ...server.catalog, revision: "2" };
    server.document = { ...server.document, revision: "r-next", text: "# Design\n\nLatest text" };
    const stored = { revision: "r1", text: "# Design\n\nBody" };

    let finishBaseline!: (response: Response) => void;
    let baselineReads = 0;
    const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/reads/get") return realFetch(input, options);
      server.calls.push({ path: "/api/reads/get", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
      return ++baselineReads === 1
        ? new Promise<Response>((resolve) => { finishBaseline = resolve; })
        : Promise.resolve(Response.json({ error: "Read baseline is unavailable." }, { status: 503 }));
    });
    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
    } as any;

    const refresh = sendRevision(app, { documents: 2, reads: 2, dirty: ["documents", "reads"] });
    await vi.waitFor(() => expect(server.count("/api/reads/get")).toBe(1));
    await vi.waitFor(() => expect(app.document()?.revision).toBe("r-next"));
    finishBaseline(Response.json(stored));
    await refresh;

    expect(app.comment()).toBe("Unsaved comment");
    expect(app.document()?.text).toContain("Latest text");
    expect(app["revisionTracker"].pending()).toEqual(["documents", "reads"]);
    expect(app.liveStatus()).toBe("paused");
    fixture.destroy();
  });

  it("rejects an old selection baseline and follows up for the newer logical document", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md", variant: "v" });
    primeLiveRevision(app);
    server.calls.length = 0;
    let finishBaseline!: (response: Response) => void;
    let reads = 0;
    const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/reads/get") return realFetch(input, options);
      server.calls.push({ path: "/api/reads/get", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
      if (++reads === 1) return new Promise<Response>((resolve) => { finishBaseline = resolve; });
      return Promise.resolve(Response.json({ revision: "current-baseline", text: "# Current selection" }));
    });
    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
    } as any;

    const refresh = sendRevision(app, { reads: 2, dirty: ["reads"] });
    await vi.waitFor(() => expect(server.count("/api/reads/get")).toBe(1));
    const newer = { ...server.document, path: "tasks.md", revision: "new-selection" };
    app["selectionEpoch"]++;
    app.baseline.set({ revision: "selection-baseline", text: "new selection baseline" });
    app.document.set(newer);
    finishBaseline(Response.json({ revision: "stale", text: "old selection baseline" }));
    await refresh;

    expect(app.document()?.path).toBe("tasks.md");
    expect(app.baseline()?.revision).toBe("current-baseline");
    expect(server.count("/api/reads/get")).toBe(2);
    expect(app["revisionTracker"].pending()).toEqual([]);
    expect(app.liveStatus()).toBe("live");
    fixture.destroy();
  });

  it("reconciles a newer selected document after an overlapped baseline is superseded", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md", variant: "v" });
    primeLiveRevision(app);
    server.calls.length = 0;
    let finishOldBaseline!: (response: Response) => void;
    const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    let baselineReads = 0;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/reads/get") return realFetch(input, options);
      server.calls.push({ path: "/api/reads/get", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
      if (++baselineReads === 1) return new Promise<Response>((resolve) => { finishOldBaseline = resolve; });
      return Promise.resolve(Response.json({ revision: "tasks-baseline", text: "# Tasks\n\nBody" }));
    });
    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
    } as any;

    const refresh = sendRevision(app, { reads: 2, dirty: ["reads"] });
    await vi.waitFor(() => expect(server.count("/api/reads/get")).toBe(1));
    app["selectionEpoch"]++;
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "tasks.md", variant: "v" });
    finishOldBaseline(Response.json({ revision: "stale", text: "# Old design" }));
    await refresh;

    expect(app.document()?.path).toBe("tasks.md");
    expect(app.baseline()?.revision).toBe("tasks-baseline");
    expect(server.count("/api/reads/get")).toBe(3);
    expect(app["revisionTracker"].pending()).toEqual([]);
    expect(app.liveStatus()).toBe("live");
    fixture.destroy();
  });

  it("reconciles a documents-only revision after navigation and an identical checkpoint", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md", variant: "v" });
    primeLiveRevision(app);
    server.calls.length = 0;
    let finishFirstCatalog!: (response: Response) => void;
    let catalogLoads = 0;
    const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/catalog") return realFetch(input, options);
      server.calls.push({ path: "/api/catalog", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
      return ++catalogLoads === 1
        ? new Promise<Response>((resolve) => { finishFirstCatalog = resolve; })
        : realFetch(input, options);
    });
    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
    } as any;

    const documentsCheckpoint = { catalog: 1, documents: 2, dirty: ["documents"] };
    const refresh = sendRevision(app, documentsCheckpoint);
    await vi.waitFor(() => expect(server.count("/api/catalog")).toBe(1));
    app["selectionEpoch"]++;
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "tasks.md", variant: "v" });
    finishFirstCatalog(Response.json(server.catalog));
    await refresh;

    const catalogCount = server.count("/api/catalog");
    await sendRevision(app, documentsCheckpoint);
    expect(app.document()?.path).toBe("tasks.md");
    expect(app["revisionTracker"].pending()).toEqual([]);
    expect(app.liveStatus()).toBe("live");
    expect(catalogCount).toBeGreaterThanOrEqual(2);
    expect(server.count("/api/catalog")).toBe(catalogCount);
    fixture.destroy();
  });

  it("does not steal focus or scroll changed while a read baseline is loading", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md", variant: "v" });
    app.comment.set("Draft");
    app.selection.set({ start: 0, end: 5, text: "Design" });
    fixture.detectChanges();
    const pane = fixture.nativeElement.querySelector(".document-scroll") as HTMLElement;
    const oldPassage = pane.querySelector<HTMLElement>("[data-start]")!;
    const textarea = fixture.nativeElement.querySelector(".review-composer textarea") as HTMLTextAreaElement;
    oldPassage.focus();
    pane.scrollTop = 5;
    server.calls.length = 0;
    let finishBaseline!: (response: Response) => void;
    const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/reads/get") return realFetch(input, options);
      server.calls.push({ path: "/api/reads/get", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
      return new Promise<Response>((resolve) => { finishBaseline = resolve; });
    });

    const refresh = app["refreshOpenBaseline"](app["selectionEpoch"]);
    await vi.waitFor(() => expect(server.count("/api/reads/get")).toBe(1));
    textarea.focus();
    app.comment.set("Draft newer input");
    fixture.detectChanges();
    pane.scrollTop = 41;
    finishBaseline(Response.json(null));
    expect(await refresh).toBe(true);
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(document.activeElement).toBe(textarea);
    expect(textarea.value).toBe("Draft newer input");
    expect(pane.scrollTop).toBe(41);
    fixture.destroy();
  });

  it("keeps a newer connected input focused when the delayed reader-focus restore runs", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md", variant: "v" });
    app.selection.set({ start: 0, end: 5, text: "Design" });
    fixture.detectChanges();
    const pane = fixture.nativeElement.querySelector(".document-scroll") as HTMLElement;
    const oldPassage = pane.querySelector<HTMLElement>("[data-start]")!;
    const textarea = fixture.nativeElement.querySelector(".review-composer textarea") as HTMLTextAreaElement;
    oldPassage.focus();
    server.calls.length = 0;
    let finishBaseline!: (response: Response) => void;
    const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/reads/get") return realFetch(input, options);
      server.calls.push({ path: "/api/reads/get", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
      return new Promise<Response>((resolve) => { finishBaseline = resolve; });
    });

    const refresh = app["refreshOpenBaseline"](app["selectionEpoch"]);
    await vi.waitFor(() => expect(server.count("/api/reads/get")).toBe(1));
    finishBaseline(Response.json(null));
    await refresh;
    textarea.focus();
    app.comment.set("Keep focus here");
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(document.activeElement).toBe(textarea);
    expect(textarea.value).toBe("Keep focus here");
    fixture.destroy();
  });

  it("releases foreground revision waiters and ignores a catalog response after destruction", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    app.saving.set(true);
    let finishCatalog!: (response: Response) => void;
    const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => String(input) === "/api/catalog"
      ? new Promise<Response>((resolve) => { finishCatalog = resolve; })
      : realFetch(input, options));
    const refresh = sendRevision(app, { catalog: 2, dirty: ["catalog"] });
    await vi.waitFor(() => expect(finishCatalog).toBeTypeOf("function"));
    expect(app["foregroundWaiters"].length).toBeGreaterThan(0);

    app.ngOnDestroy();
    finishCatalog(Response.json({ ...server.catalog, revision: "late" }));
    await refresh;

    expect(app["foregroundWaiters"]).toHaveLength(0);
    expect(app.catalog()?.revision).toBe(server.catalog.revision);
    expect(server.count("/api/reads/get")).toBe(0);
    fixture.destroy();
  });

  it("does not request an Ongoing next page or unlinked entries after destruction during page one", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    server.calls.length = 0;
    let finishPage!: (response: Response) => void;
    const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/timeline") return realFetch(input, options);
      server.calls.push({ path: "/api/timeline", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
      return new Promise<Response>((resolve) => { finishPage = resolve; });
    });
    const loading = app.loadOngoing();
    await vi.waitFor(() => expect(server.count("/api/timeline")).toBe(1));

    app.ngOnDestroy();
    finishPage(Response.json({ ...timeline, nextOffset: 10 }));
    expect(await loading).toBe(false);

    expect(server.count("/api/timeline")).toBe(1);
    expect(server.count("/api/ongoing/unlinked")).toBe(0);
    fixture.destroy();
  });

  it("does not paginate or read unlinked entries after destruction during a later Ongoing page", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    server.calls.length = 0;
    let finishSecondPage!: (response: Response) => void;
    const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/timeline") return realFetch(input, options);
      const count = server.count("/api/timeline") + 1;
      server.calls.push({ path: "/api/timeline", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
      return count === 1
        ? Promise.resolve(Response.json({ ...timeline, nextOffset: 10 }))
        : new Promise<Response>((resolve) => { finishSecondPage = resolve; });
    });
    const loading = app.loadOngoing();
    await vi.waitFor(() => expect(server.count("/api/timeline")).toBe(2));

    app.ngOnDestroy();
    finishSecondPage(Response.json({ ...timeline, nextOffset: 20 }));
    expect(await loading).toBe(false);

    expect(server.count("/api/timeline")).toBe(2);
    expect(server.count("/api/ongoing/unlinked")).toBe(0);
    fixture.destroy();
  });

  it("stops revision follow-up after a protected reconciliation request returns 401", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md", variant: "v" });
    primeLiveRevision(app);
    server.calls.length = 0;
    const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => String(input) === "/api/reads/get"
      ? (server.calls.push({ path: "/api/reads/get", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined }), Promise.resolve(Response.json({ error: "Unauthorized" }, { status: 401 })))
      : realFetch(input, options));
    app["liveConnection"] = {
      stop: async () => {},
      authorizationLost: () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
    } as any;

    await sendRevision(app, { reads: 2, dirty: ["reads"] });
    await Promise.resolve();

    expect(app.unauthorized()).toBe(true);
    expect(app.liveStatus()).toBe("paused");
    expect(server.count("/api/reads/get")).toBe(1);
    expect(app["revisionTracker"].pending()).toEqual(["reads"]);
    fixture.destroy();
  });

  it("refreshes only the active History snapshot on journal changes", async () => {
    const { app, server, fixture } = setup();
    const clock = { now: new Date("2026-10-27T10:00:00Z") };
    app.view.set("history");
    app.ready.set(true);
    app["now"] = () => clock.now;
    app["evaluate"]();
    await app.loadHistory();
    primeLiveRevision(app);
    server.calls.length = 0;
    await sendRevision(app, { journal: 2, dirty: ["journal"] });
    expect(server.count("/api/timeline")).toBe(1);
    expect(server.count("/api/ongoing/unlinked")).toBe(0);
    expect(server.count("/api/reviews")).toBe(0);
    fixture.destroy();
  });

  it("reloads full document metadata after a documents-only archive change without retransmitting unchanged text", async () => {
    const { app, server, fixture } = setup();
    const clock = { now: new Date("2026-10-27T10:00:00Z") };
    await open(app, server, clock);
    server.catalog = {
      ...server.catalog,
      changes: [{ ...change, directoryName: "2026-10-27-demo", archived: true }],
    };
    server.document = { ...design, archived: true, canComment: false };
    await sendRevision(app, { documents: 2, dirty: ["documents"] });
    const read = server.calls.find((c) => c.path === "/api/document");
    expect(read).toBeDefined();
    expect(read!.body).not.toHaveProperty("knownRevision");
    expect(app.document()?.archived).toBe(true);
    expect(app.document()?.canComment).toBe(false);
    fixture.destroy();
  });

  it("keeps unchanged document validation conditional when its archive metadata is stable", async () => {
    const { app, server, fixture } = setup();
    const clock = { now: new Date("2026-10-27T10:00:00Z") };
    await open(app, server, clock);

    server.calls.length = 0;
    await sendRevision(app, { documents: 2, dirty: ["documents"] });

    const read = server.calls.find((c) => c.path === "/api/document");
    expect(read).toBeDefined();
    expect(read!.body.knownRevision).toBe(design.revision);
    expect(app.document()?.archived).toBe(false);
    expect(app.document()?.canComment).toBe(true);
    fixture.destroy();
  });

  it("retains document invalidation during comment saving and reads it after the save settles", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md" });
    primeLiveRevision(app);
    server.document = { ...server.document, revision: "r2", text: "# Design\n\nSaved after external edit" };
    app.saving.set(true);
    server.calls.length = 0;
    const refresh = sendRevision(app, { documents: 2, dirty: ["documents"] });
    await vi.waitFor(() => expect(server.count("/api/catalog")).toBe(1));
    expect(server.count("/api/document")).toBe(0);
    expect(app.liveStatus()).toBe("reconnecting");
    app.saving.set(false);
    app["releaseForegroundWaiters"]();
    await refresh;
    expect(server.count("/api/document")).toBe(1);
    expect(app.document()?.text).toContain("Saved after external edit");
    expect(app["revisionTracker"].pending()).toEqual([]);
    expect(app.lastSuccessfulRefresh()).not.toBeNull();
    fixture.destroy();
  });

  it("shows an accessible paused status and preserves the last successful refresh", () => {
    const { app, fixture } = setup();
    app.ready.set(true);
    app.liveStatus.set("paused");
    app.lastSuccessfulRefresh.set("2026-10-27T10:00:00.000Z");
    fixture.detectChanges();
    const status = fixture.nativeElement.querySelector(".live-status") as HTMLElement;
    expect(status.getAttribute("role")).toBe("status");
    expect(status.getAttribute("aria-live")).toBe("polite");
    expect(status.dataset["state"]).toBe("paused");
    expect(status.textContent).toContain("Updates paused");
    expect(status.textContent).toContain("Last successful refresh");
    expect(status.querySelector("button")?.textContent).toContain("Retry updates");
    fixture.destroy();
  });

  describe("connection status inside the open reader", () => {
    async function openReader() {
      const ctx = setup();
      ctx.app.ready.set(true);
      await ctx.app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md" });
      ctx.fixture.detectChanges();
      return ctx;
    }
    const reader = (fixture: { nativeElement: HTMLElement }) => fixture.nativeElement.querySelector(".reader-live") as HTMLElement;

    for (const [state, label] of [["live", "Live"], ["reconnecting", "Reconnecting"], ["paused", "Updates paused"]] as const) {
      it(`shows ${state} truthfully, with one live region and a state-bound dot`, async () => {
        const { app, fixture } = await openReader();
        app.liveStatus.set(state);
        app.lastSuccessfulRefresh.set("2026-10-27T10:00:00.000Z");
        fixture.detectChanges();
        const el = reader(fixture);
        expect(el.dataset["state"]).toBe(state);
        expect(el.textContent).toContain(label);
        expect(el.textContent).toContain("Last successful refresh");
        expect(!!el.querySelector("button")).toBe(state === "paused");
        const dot = fixture.nativeElement.querySelector(".reader-meta .live-dot") as HTMLElement;
        expect(dot.dataset["state"]).toBe(state);
        const header = fixture.nativeElement.querySelector(".live-status") as HTMLElement;
        expect(header.getAttribute("role")).toBeNull();
        expect(fixture.nativeElement.querySelectorAll("[role=status][data-state]").length).toBe(1);
        fixture.destroy();
      });
    }

    it("retries once from the reader without closing it or touching the document", async () => {
      const { app, fixture } = await openReader();
      const retry = vi.fn(async () => {});
      const stop = vi.fn();
      app["liveConnection"] = { retry, stop } as never;
      app.liveStatus.set("paused");
      fixture.detectChanges();
      const before = app.document();
      const button = reader(fixture).querySelector("button") as HTMLButtonElement;
      expect(button.type).toBe("button");
      // A pointer press must not steal focus from an active review draft.
      const press = new MouseEvent("mousedown", { bubbles: true, cancelable: true });
      button.dispatchEvent(press);
      expect(press.defaultPrevented).toBe(true);
      button.click();
      await vi.waitFor(() => expect(retry).toHaveBeenCalledTimes(1));
      await fixture.whenStable();
      expect(app.document()).toBe(before);
      expect(fixture.nativeElement.querySelector(".reader")).not.toBeNull();
      fixture.destroy();
    });

    it("restores the header status live region when the reader closes", async () => {
      const { app, fixture } = await openReader();
      app.closeDocument();
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector(".reader-live")).toBeNull();
      expect((fixture.nativeElement.querySelector(".live-status") as HTMLElement).getAttribute("role")).toBe("status");
      fixture.destroy();
    });
  });

  it("expires an Ongoing card exactly seven days after its last recorded instant without REST reads", async () => {
    vi.useFakeTimers();
    const start = new Date("2026-10-07T23:59:59.999Z");
    vi.setSystemTime(start);
    const { app, server, fixture } = setup();
    app["now"] = () => new Date();
    app.ready.set(true);
    app.unlinked.set([{
      id: "entry",
      repository: "r",
      change: null,
      status: "in_progress",
      summary: "Small task",
      occurredAt: "2026-10-01T00:00:00.000Z",
      agent: null,
      branch: null,
      tickets: [],
    }]);
    app["ongoingScope"].set("[]");
    app["evaluate"]();
    expect(app.fullBoard().in_progress.some((card) => card.kind === "adhoc")).toBe(true);
    app["armClockTimer"]();
    await vi.advanceTimersByTimeAsync(1);
    expect(app.fullBoard().in_progress.some((card) => card.kind === "adhoc")).toBe(false);
    expect(server.calls).toEqual([]);
    fixture.destroy();
  });

  it("schedules quiet state by selected-zone calendar day and keeps explicit History dates unchanged", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-11-06T22:59:59.999Z"));
    const { app, server, fixture } = setup();
    app["now"] = () => new Date();
    app.timezone = "Europe/Warsaw";
    app.ready.set(true);
    app.activity.set([{ repository: "r", change: "demo", firstAt: "2026-10-23T12:00:00.000Z", lastAt: "2026-10-23T12:00:00.000Z", lastStatus: "in_progress" }]);
    app["activityScope"].set("[]");
    app["evaluate"]();
    const card = app.fullBoard().in_progress.find((item) => item.kind === "change")!;
    expect(app.quiet(card)).toBe(false);
    app["armClockTimer"]();
    await vi.advanceTimersByTimeAsync(1);
    expect(app.quiet(card)).toBe(true);
    expect(server.calls).toEqual([]);

    app.view.set("history");
    app.range = { kind: "custom", from: "2026-10-01", to: "2026-10-03" };
    app["evaluate"]();
    const loadHistory = vi.spyOn(app, "loadHistory").mockResolvedValue(true);
    const fixed = [app.from, app.to];
    vi.setSystemTime(new Date("2026-11-07T00:10:00Z"));
    app["timeChanged"]();
    expect([app.from, app.to]).toEqual(fixed);
    expect(loadHistory).not.toHaveBeenCalled();
    fixture.destroy();
  });

  it("reevaluates relative History at Warsaw midnight after the 23-hour DST day without unrelated reads or writes", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-03-28T23:00:00.000Z"));
    const { app, server, fixture } = setup();
    app["now"] = () => new Date();
    app.timezone = "Europe/Warsaw";
    app.view.set("history");
    app.range = { kind: "relative", days: 7 };
    app.ready.set(true);
    app["evaluate"]();
    expect([app.from, app.to]).toEqual(["2026-03-23", "2026-03-29"]);
    expect(app["temporalBoundaries"]()[0] - Date.now()).toBe(23 * 60 * 60 * 1000);
    await app.loadHistory();
    app["armClockTimer"]();
    server.calls.length = 0;

    vi.setSystemTime(new Date("2026-03-29T22:00:00.000Z"));
    await vi.advanceTimersByTimeAsync(60_000);
    await vi.waitFor(() => expect(server.count("/api/timeline")).toBe(1));
    expect([app.from, app.to]).toEqual(["2026-03-24", "2026-03-30"]);
    expect(server.calls.map((call) => call.path)).toEqual(["/api/timeline"]);
    expect(server.count("/api/state", "PUT")).toBe(0);
    expect(app.error()).toBe("");

    app.range = { kind: "custom", from: "2026-03-01", to: "2026-03-10" };
    app["evaluate"]();
    app["armClockTimer"]();
    server.calls.length = 0;
    vi.setSystemTime(new Date("2026-03-30T22:00:00.000Z"));
    await vi.advanceTimersByTimeAsync(60_000);
    await Promise.resolve();
    expect([app.from, app.to]).toEqual(["2026-03-01", "2026-03-10"]);
    expect(server.calls).toEqual([]);
    fixture.destroy();
  });

  it("loads Daily for the new Warsaw day while retaining an explicitly selected past day", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-03-29T21:59:59.999Z"));
    const { app, server, fixture } = setup();
    app["now"] = () => new Date();
    app.timezone = "Europe/Warsaw";
    app.view.set("daily");
    app.dailyDay = "2026-03-25";
    app.ready.set(true);
    app["evaluate"]();
    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.liveStatus.set("reconnecting"),
      markReconciled: () => app.liveStatus.set("live"),
      markPaused: () => app.liveStatus.set("paused"),
    } as any;
    primeLiveRevision(app);
    const dailyLine = (id: string, summary: string) => ({
      id,
      title: "Current work",
      repository: "r",
      tickets: [],
      changes: [],
      status: "in_progress",
      primary: {
        id: id + "-entry",
        repository: "r",
        change: null,
        status: "in_progress",
        summary,
        occurredAt: new Date().toISOString(),
        agent: null,
        branch: null,
        tickets: [],
      },
      others: [],
    });
    server.daily = {
      day: "2026-03-25",
      previousDay: "2026-03-24",
      nextDay: "2026-03-26",
      items: [],
      today: [dailyLine("today-before", "Before rollover")],
    };
    expect(await app["refreshDaily"]()).toBe(true);
    expect(app.dailyData()?.before).toBe("2026-03-28T23:00:00.000Z");
    expect(app.dailyData()?.digest.today[0]?.primary.summary).toBe("Before rollover");
    app["armClockTimer"]();
    server.calls.length = 0;

    server.daily = {
      ...server.daily,
      today: [dailyLine("today-after", "After rollover")],
    };
    vi.setSystemTime(new Date("2026-03-29T22:00:00.000Z"));
    await vi.advanceTimersByTimeAsync(60_000);
    await vi.waitFor(() => expect(app.dailyData()?.digest.today[0]?.id).toBe("today-after"));
    expect(server.count("/api/daily")).toBe(1);
    expect(app.dailyLoading()).toBe(false);
    expect(app.error()).toBe("");
    expect(app.dailyData()?.before).toBe("2026-03-29T22:00:00.000Z");
    expect(app.dailyDay).toBe("2026-03-25");
    expect(server.calls.find((call) => call.path === "/api/daily")?.body).toEqual({
      before: "2026-03-29T22:00:00.000Z",
      timezone: "Europe/Warsaw",
      repositories: [],
      day: "2026-03-25",
    });
    expect(server.count("/api/catalog")).toBe(0);
    expect(server.count("/api/state", "PUT")).toBe(0);
    expect(server.calls.some((call) => call.path.includes("observations"))).toBe(false);
    fixture.destroy();
  });

  it("reevaluates a long-hidden Ongoing view before one authoritative resume while preserving its route and draft", async () => {
    const { app, server, fixture, hidden } = setup();
    app.ready.set(true);
    app.view.set("ongoing");
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md", variant: "v" });
    app.comment.set("Keep this draft");
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-03-28T23:00:00.000Z"));
    app["now"] = () => new Date();
    app.timezone = "Europe/Warsaw";
    app.activity.set([{
      repository: "r",
      change: "demo",
      firstAt: "2026-03-15T10:00:00.000Z",
      lastAt: "2026-03-15T10:00:00.000Z",
      lastStatus: "in_progress",
    }]);
    app.unlinked.set([{
      id: "stale-entry",
      repository: "r",
      change: null,
      status: "in_progress",
      summary: "Old task",
      occurredAt: "2026-03-22T00:00:00.000Z",
      agent: null,
      branch: null,
      tickets: [],
    }]);
    app["ongoingScope"].set("[]");
    app["activityScope"].set("[]");
    app["activityKey"] = "1|[]";
    app["evaluate"]();
    const changeCard = app.fullBoard().in_progress.find((card) => card.kind === "change")!;
    expect(app.quiet(changeCard)).toBe(false);
    expect(app.fullBoard().in_progress.some((card) => card.kind === "adhoc")).toBe(true);
    primeLiveRevision(app);

    const realFetch = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation((input, options) => {
      if (String(input) !== "/api/ongoing/unlinked") return realFetch(input, options);
      server.calls.push({ path: "/api/ongoing/unlinked", method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
      return Promise.resolve(Response.json({ entries: app.unlinked(), truncated: false }));
    });
    const liveStatesWereReconciled = [] as boolean[];
    const live = {
      resume: vi.fn(async () => app["applyRevisionSnapshot"]({
        instance: "fixture-instance",
        catalog: 1,
        documents: 2,
        journal: 1,
        digests: 1,
        reads: 1,
        dirty: ["documents"],
      }, true)),
      markReconciled: vi.fn(() => {
        liveStatesWereReconciled.push(
          app.lastSuccessfulRefresh() !== null
          && app["evaluatedDate"] === "2026-04-06"
          && app.quiet(app.fullBoard().in_progress.find((card) => card.kind === "change")!)
          && !app.fullBoard().in_progress.some((card) => card.kind === "adhoc"),
        );
        app.liveStatus.set("live");
      }),
      markPaused: vi.fn(() => app.liveStatus.set("paused")),
      markReconciling: vi.fn(() => app.liveStatus.set("reconnecting")),
      stop: async () => {},
    };
    app["liveConnection"] = live as any;
    server.calls.length = 0;
    app["armClockTimer"]();
    hidden.mockReturnValue(true);
    await app.visibilityChanged();
    expect(live.resume).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);

    vi.setSystemTime(new Date("2026-04-06T12:00:00.000Z"));
    hidden.mockReturnValue(false);
    await app.visibilityChanged();

    expect(live.resume).toHaveBeenCalledTimes(1);
    expect(liveStatesWereReconciled.length).toBeGreaterThan(0);
    expect(liveStatesWereReconciled.every(Boolean)).toBe(true);
    expect(app.liveStatus()).toBe("live");
    expect(app.lastSuccessfulRefresh()).toBe(new Date().toISOString());
    expect(app.view()).toBe("ongoing");
    expect(app.document()?.path).toBe("design.md");
    expect(app.comment()).toBe("Keep this draft");
    expect(app.quiet(app.fullBoard().in_progress.find((card) => card.kind === "change")!)).toBe(true);
    expect(app.fullBoard().in_progress.some((card) => card.kind === "adhoc")).toBe(false);
    expect(server.count("/api/catalog")).toBe(1);
    expect(server.count("/api/state", "PUT")).toBe(0);
    expect(server.calls.some((call) => call.path.includes("observations"))).toBe(false);
    expect(app.observations()).toEqual([]);
    fixture.destroy();
  });
});
