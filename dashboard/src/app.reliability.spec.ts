import { TestBed } from "@angular/core/testing";
import { Router } from "@angular/router";
import { Subject } from "rxjs";
import { describe, it, expect, vi, afterEach } from "vitest";
import { App, documentLimit, positionLimit } from "./app";
import { localDate } from "./api";
import { validTimezone } from "./range";
import { Catalog, Change, DocumentContent } from "./models";

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
  vi.spyOn(document, "hidden", "get").mockReturnValue(false);
  return { app, fixture, server };
}
afterEach(() => {
  vi.restoreAllMocks();
  TestBed.resetTestingModule();
});
const puts = (server: ReturnType<typeof setup>["server"]) =>
  server.calls.filter((c) => c.path === "/api/state" && c.method === "PUT");

describe("history range", () => {
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
    expect(app["timer"]).toBeDefined();
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

describe("idle polling", () => {
  async function open(app: App, server: ReturnType<typeof setup>["server"], clock: { now: Date }) {
    app.ready.set(true);
    app["now"] = () => clock.now;
    app["evaluate"]();
    app["journalSnapshot"] = server.catalog.journalSnapshot;
    app["overviewRevision"] = server.catalog.revision;
    app.searchText = "needle";
    await app.search();
    await app["loadDocument"]({ sourceId: "s", change: "demo", path: "design.md" });
    server.calls.length = 0;
  }

  it("repeats no search, timeline query, document transfer, or preference save while nothing changes", async () => {
    const { app, server, fixture } = setup();
    const clock = { now: new Date("2026-10-27T10:00:00Z") };
    await open(app, server, clock);
    const rendered = app.html();
    const current = app.document();
    for (let i = 0; i < 12; i++) await app["poll"]();
    expect(server.count("/api/catalog")).toBe(12);
    expect(server.count("/api/document")).toBe(12);
    expect(server.calls.filter((c) => c.path === "/api/document").every((c) => c.body.knownRevision === "r1")).toBe(true);
    expect(server.count("/api/search")).toBe(0);
    expect(server.count("/api/timeline")).toBe(0);
    // The overview and the read hints are not requested again while the catalog revision stays the same.
    expect(server.calls.filter((c) => c.path === "/api/reviews" || c.path.startsWith("/api/reads/"))).toEqual([]);
    expect(puts(server)).toHaveLength(0);
    // The unchanged answer keeps the rendered document; nothing was parsed again.
    expect(app.html()).toBe(rendered);
    expect(app.document()).toBe(current);
    fixture.destroy();
  });

  it("refreshes search on a revision change and History on a journal snapshot change or a new day", async () => {
    const { app, server, fixture } = setup();
    const clock = { now: new Date("2026-10-27T10:00:00Z") };
    await open(app, server, clock);
    server.catalog = { ...server.catalog, revision: "2" };
    await app["poll"]();
    await app["poll"]();
    expect(server.count("/api/search")).toBe(1);
    expect(server.count("/api/timeline")).toBe(0);
    await vi.waitFor(() => expect(server.count("/api/reviews")).toBe(1));
    expect(server.count("/api/reads/summary")).toBe(1);
    server.catalog = { ...server.catalog, journalSnapshot: 6 };
    await app["poll"]();
    const afterJournal = server.count("/api/timeline");
    expect(afterJournal).toBeGreaterThan(0);
    await app["poll"]();
    expect(server.count("/api/timeline")).toBe(afterJournal);
    expect(server.count("/api/search")).toBe(1);
    // A journal change moves History, not the overview.
    expect(server.count("/api/reviews")).toBe(1);
    expect([app.from, app.to]).toEqual(["2026-09-28", "2026-10-27"]);
    clock.now = new Date("2026-10-28T00:30:00Z");
    await app["poll"]();
    expect(server.count("/api/timeline")).toBeGreaterThan(afterJournal);
    expect([app.from, app.to]).toEqual(["2026-09-29", "2026-10-28"]);
    expect(localDate(clock.now, "UTC")).toBe(app.to);
    expect(puts(server)).toHaveLength(0);
    fixture.destroy();
  });

  it("reloads the full document when the text changed or the change was archived", async () => {
    const { app, server, fixture } = setup();
    const clock = { now: new Date("2026-10-27T10:00:00Z") };
    await open(app, server, clock);
    server.document = { ...design, revision: "r2", text: "# Design\n\nEdited by an agent" };
    await app["poll"]();
    expect(app.document()?.revision).toBe("r2");
    expect(app.document()?.text).toContain("Edited by an agent");
    server.calls.length = 0;
    server.catalog = { ...server.catalog, changes: [{ ...change, archived: true }] };
    await app["poll"]();
    expect(server.calls.find((c) => c.path === "/api/document")!.body).not.toHaveProperty("knownRevision");
    fixture.destroy();
  });
});
