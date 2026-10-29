import { TestBed } from "@angular/core/testing";
import { Router } from "@angular/router";
import { Subject } from "rxjs";
import { describe, it, expect, vi, afterEach } from "vitest";
import { App } from "./app";
import type { Catalog, Change, ChangeActivity, Entry, Row, Ticket } from "./models";

const linked: Ticket = { provider: "jira", scope: "company.atlassian.net", id: "DEP-12" };
const unlinked: Ticket = { provider: "jira", scope: "Some Site", id: "UNK-7" };
const change = (name: string): Change => ({
  sourceId: "s", name, directoryName: name, variant: "v-" + name, archived: false, archivedOn: null, repositories: ["github.com/acme/api"],
  documents: [{ path: "design.md", label: "design", fingerprint: "1:1" }], completedTasks: null, totalTasks: null, error: null,
});
const row = (id: string, tickets: Ticket[], extra: Partial<Row> = {}): Row => ({
  id, repository: "github.com/acme/api", title: tickets[0]?.id ?? id, status: "in_progress", firstAt: "2026-10-27T10:00:00Z", startedAt: null,
  lastAt: "2026-10-27T10:00:00Z", elapsedSeconds: 0, activityDates: ["2026-10-27"], completionDates: [], resumedDates: [], changes: [], tickets,
  observationCount: 1, visibleCount: 1, clippedStart: false, ...extra,
});

function setup(url = "/ongoing") {
  TestBed.configureTestingModule({
    imports: [App],
    providers: [{ provide: Router, useValue: { events: new Subject(), navigate: vi.fn().mockResolvedValue(true), parseUrl: () => ({ queryParams: {} }), url } }],
  });
  const fixture = TestBed.createComponent(App);
  const app = fixture.componentInstance;
  (app as unknown as { now: () => Date }).now = () => new Date("2026-10-28T12:00:00Z");
  app.catalog.set({
    repositories: [{ id: "github.com/acme/api", label: "api" }], sources: [], changes: [change("recent"), change("old"), change("never")],
    launchRepository: null, revision: "1", notices: [], journalSnapshot: 0,
  } as Catalog);
  app.ready.set(true);
  return { app, fixture, element: fixture.nativeElement as HTMLElement };
}
afterEach(() => {
  vi.restoreAllMocks();
  TestBed.resetTestingModule();
});

describe("ticket and repository links", () => {
  it("renders a link for a linkable ticket and no anchor for an unlinkable one, with a copy action for both", () => {
    const { app, fixture, element } = setup();
    app.ongoing.set([row("a", [linked, unlinked])]);
    fixture.detectChanges();
    const refs = Array.from(element.querySelectorAll(".board .ticket-ref"));
    expect(refs).toHaveLength(2);
    expect(refs[0].querySelector("a")?.getAttribute("href")).toBe("https://company.atlassian.net/browse/DEP-12");
    expect(refs[1].querySelector("a")).toBeNull();
    expect(refs[1].querySelector(".ticket-id")?.textContent).toBe("UNK-7");
    expect(element.querySelectorAll(".board .ticket-copy")).toHaveLength(2);
    fixture.destroy();
  });

  it("opens a ticket link only through the explicit external navigation handler", () => {
    const { app, fixture, element } = setup();
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    app.ongoing.set([row("a", [linked])]);
    fixture.detectChanges();
    expect(open).not.toHaveBeenCalled();
    const click = new MouseEvent("click", { bubbles: true, cancelable: true });
    element.querySelector<HTMLAnchorElement>(".ticket-link")!.dispatchEvent(click);
    expect(click.defaultPrevented).toBe(true);
    expect(open).toHaveBeenCalledExactlyOnceWith("https://company.atlassian.net/browse/DEP-12", "_blank", "noopener,noreferrer");
    fixture.destroy();
  });

  it("shows repository and branch links in the drill-down, and none for a local repository", () => {
    const { app, fixture, element } = setup();
    const entry: Entry = {
      id: "e1", repository: "github.com/acme/api", change: null, status: "in_progress", summary: "Did work", occurredAt: "2026-10-27T10:00:00Z",
      agent: null, branch: "feature/DEP-12", tickets: [linked], extractedTicketId: "EXT-9",
    };
    app.detail.set({ id: "a", title: "DEP-12", repository: "github.com/acme/api", tickets: [linked] });
    app.observations.set([entry]);
    fixture.detectChanges();
    expect(element.querySelector(".activity-detail .repository-web-link")?.getAttribute("href")).toBe("https://github.com/acme/api");
    expect(element.querySelector(".activity-detail .branch-link")?.getAttribute("href")).toBe("https://github.com/acme/api/tree/feature/DEP-12");
    expect(element.querySelector(".observation .ticket-link")?.getAttribute("href")).toBe("https://company.atlassian.net/browse/DEP-12");
    expect(element.querySelector(".observation .ticket-id")?.textContent).toBe("EXT-9");
    app.detail.set({ id: "b", title: "DEP-12", repository: "local:abc" });
    app.observations.set([{ ...entry, repository: "local:abc" }]);
    fixture.detectChanges();
    expect(element.querySelector(".activity-detail .repository-web-link")).toBeNull();
    expect(element.querySelector(".activity-detail .branch-link")).toBeNull();
    expect(element.querySelector(".branch-line code")?.textContent).toBe("feature/DEP-12");
    fixture.destroy();
  });

  it("copies a ticket ID and falls back to a manual copy field when the clipboard is unavailable", async () => {
    const { app, fixture } = setup();
    const write = vi.fn().mockResolvedValueOnce(undefined).mockRejectedValueOnce(new Error("denied"));
    Object.defineProperty(navigator, "clipboard", { value: { writeText: write }, configurable: true });
    await app.copyTicket("DEP-12");
    expect(write).toHaveBeenCalledWith("DEP-12");
    expect(app.copyToast()).toBe("Ticket ID copied.");
    expect(app.manualCopy()).toBeNull();
    await app.copyTicket("DEP-13");
    expect(app.manualCopy()).toEqual({ title: "Copy this ticket ID", text: "DEP-13", multiline: false });
    fixture.destroy();
  });
});

describe("History ticket filter", () => {
  it("sends the trimmed filter only when set, shows an indicator, and clears it", async () => {
    const { app, fixture, element } = setup("/history");
    app.view.set("history");
    app.from = "2026-10-01";
    app.to = "2026-10-28";
    const bodies: Record<string, unknown>[] = [];
    vi.spyOn(globalThis, "fetch").mockImplementation(async (_path, options) => {
      bodies.push(JSON.parse(options!.body as string));
      return Response.json({ snapshot: 1, rows: [], total: 0, nextOffset: null, insights: { longestSpans: [], completions: [], subsequentWork: [], days: [] } });
    });
    await app.loadHistory();
    expect("ticket" in bodies[0]).toBe(false);
    app.ticketInput = "  jira:company.atlassian.net:DEP-12 ";
    await app.ticketChanged();
    expect(bodies[1]["ticket"]).toBe("jira:company.atlassian.net:DEP-12");
    fixture.detectChanges();
    expect(element.querySelector(".ticket-filter-active")?.textContent).toContain("jira:company.atlassian.net:DEP-12");
    expect(app["historyQuery"]?.ticket).toBe("jira:company.atlassian.net:DEP-12");
    await app.clearTicket();
    expect("ticket" in bodies[2]).toBe(false);
    expect(app.ticketInput).toBe("");
    fixture.detectChanges();
    expect(element.querySelector(".ticket-filter-active")).toBeNull();
    fixture.destroy();
  });

  it("remembers the filter with the other History controls", async () => {
    const { app, fixture } = setup("/history");
    vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(null, { status: 204 }));
    app.ticket = "DEP-12";
    await app["persist"]();
    expect(app.preferences.history?.ticket).toBe("DEP-12");
    app.ticket = "";
    await app["persist"]();
    expect(app.preferences.history?.ticket).toBeUndefined();
    fixture.destroy();
  });
});

describe("quiet active changes", () => {
  const activity = (change: string, days: number): ChangeActivity => ({
    repository: "github.com/acme/api", change, firstAt: "2026-09-01T10:00:00Z", lastAt: new Date(Date.parse("2026-10-28T12:00:00Z") - days * 86400000).toISOString(), lastStatus: "in_progress",
  });
  const chips = (element: HTMLElement) =>
    Object.fromEntries(Array.from(element.querySelectorAll(".change-card")).map((card) => [card.querySelector(".change-title")!.textContent!.trim(), Array.from(card.querySelectorAll(".quiet-chip")).map((c) => c.textContent!.trim())]));

  it("marks a 15-day change quiet with its latest date, leaves a 13-day change alone, and flags a change without activity", () => {
    const { app, fixture, element } = setup();
    app["activityScope"].set("[]");
    app.activity.set([activity("recent", 13), activity("old", 15)]);
    fixture.detectChanges();
    const result = chips(element);
    expect(result["Recent"]).toEqual([]);
    expect(result["Old"]).toHaveLength(1);
    expect(result["Old"][0]).toContain("No activity recorded in the last 14 days");
    expect(result["Old"][0]).toContain("13 Oct 2026");
    expect(result["Never"]).toEqual(["No recorded activity"]);
    fixture.destroy();
  });

  it("claims nothing until the activity of the current selection is loaded", () => {
    const { app, fixture, element } = setup();
    fixture.detectChanges();
    expect(Object.values(chips(element)).flat()).toEqual([]);
    app["activityScope"].set(JSON.stringify(["other"]));
    app.selectedRepos.set(["github.com/acme/api"]);
    fixture.detectChanges();
    expect(Object.values(chips(element)).flat()).toEqual([]);
    fixture.destroy();
  });

  it("loads the activity on Ongoing work and not on History, and reloads when the journal snapshot moves", async () => {
    const { app, fixture } = setup();
    const calls: string[] = [];
    vi.spyOn(globalThis, "fetch").mockImplementation(async (path) => {
      calls.push(String(path));
      return Response.json([]);
    });
    await app.loadActivity();
    await app.loadActivity();
    expect(calls).toEqual(["/api/activity/changes"]);
    app["journalSnapshot"] = 5;
    await app.loadActivity();
    expect(calls).toHaveLength(2);
    app.view.set("history");
    app["journalSnapshot"] = 6;
    await app.loadActivity();
    expect(calls).toHaveLength(2);
    fixture.destroy();
  });
});
