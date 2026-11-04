import { TestBed } from "@angular/core/testing";
import { Router } from "@angular/router";
import { Subject } from "rxjs";
import { describe, it, expect, vi, afterEach } from "vitest";
import { App } from "./app";
import type { BoardCard } from "./board";
import type { Catalog, Change, ChangeActivity, Entry, Row } from "./models";

const repo = "github.com/acme/api";
const now = new Date("2026-10-28T12:00:00Z");
const daysAgo = (days: number) => new Date(now.getTime() - days * 86400000).toISOString();
const change = (name: string, completed: number | null, total: number | null, extra: Partial<Change> = {}): Change => ({
  sourceId: "s", name, directoryName: name, variant: "v-" + name, archived: false, archivedOn: null, repositories: [repo],
  documents: [{ path: "design.md", label: "design", fingerprint: "1:1" }], completedTasks: completed, totalTasks: total, error: null, ...extra,
});
const activity = (name: string, days: number, status = "in_progress"): ChangeActivity => ({
  repository: repo, change: name, firstAt: daysAgo(days + 5), lastAt: daysAgo(days), lastStatus: status,
});
const row = (id: string, extra: Partial<Row> = {}): Row => ({
  id, repository: repo, title: id, status: "in_progress", firstAt: daysAgo(3), startedAt: null, lastAt: daysAgo(2), elapsedSeconds: 0,
  activityDates: [], completionDates: [], resumedDates: [], changes: [], tickets: [{ provider: "jira", scope: "company", id: "DEP-12" }],
  observationCount: 1, visibleCount: 1, clippedStart: false, ...extra,
});
const entry = (id: string, days: number, extra: Partial<Entry> = {}): Entry => ({
  id, repository: repo, change: null, status: "in_progress", summary: "Summary " + id, occurredAt: daysAgo(days), agent: "claude",
  branch: "main", tickets: [], extractedTicketId: null, ...extra,
});

interface Call {
  path: string;
  method: string;
  body: any;
}
function setup(changes: Change[] = [change("proposal", 0, 9), change("busy", 0, 9), change("finished", 9, 9), change("second", 0, 9)]) {
  const calls: Call[] = [];
  TestBed.configureTestingModule({
    imports: [App],
    providers: [{ provide: Router, useValue: { events: new Subject(), navigate: vi.fn().mockResolvedValue(true), parseUrl: () => ({ queryParams: {} }), url: "/ongoing" } }],
  });
  const fixture = TestBed.createComponent(App);
  const app = fixture.componentInstance;
  (app as unknown as { now: () => Date }).now = () => now;
  const catalog = {
    repositories: [{ id: repo, label: "api" }], sources: [{ id: "s", repositories: [repo], location: "project", error: null }], changes,
    launchRepository: null, revision: "1", notices: [], journalSnapshot: 3,
  } as Catalog;
  app.catalog.set(catalog);
  app.ready.set(true);
  app["activityScope"].set("[]");
  app.activity.set([activity("busy", 1), activity("second", 4)]);
  vi.spyOn(globalThis, "fetch").mockImplementation(async (input, options) => {
    const path = String(input);
    calls.push({ path, method: options?.method ?? "GET", body: options?.body ? JSON.parse(options.body as string) : undefined });
    if (path === "/api/state") return new Response(null, { status: 204 });
    if (path === "/api/timeline") return Response.json({ snapshot: 7, rows: [], total: 0, nextOffset: null, insights: { longestSpans: [], completions: [], subsequentWork: [], days: [] } });
    if (path === "/api/ongoing/unlinked") return Response.json({ snapshot: 7, entries: [], truncated: false });
    throw new Error("Unexpected request: " + path);
  });
  return { app, fixture, calls, element: fixture.nativeElement as HTMLElement };
}
afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
  TestBed.resetTestingModule();
});

const column = (element: HTMLElement, stage: string) => element.querySelector(`.board-column[data-stage="${stage}"]`) as HTMLElement;
const cardTitles = (element: HTMLElement, stage: string) =>
  Array.from(column(element, stage).querySelectorAll(".board-card")).map((card) => card.querySelector(".change-title")!.textContent!.trim());
const saves = (calls: Call[]) => calls.filter((c) => c.method === "PUT" && c.path === "/api/state");

describe("loading and saving", () => {
  it("loads a 7-day window and the unlinked observations under the timeline's snapshot", async () => {
    const { app, calls, fixture } = setup();
    vi.spyOn(app as unknown as { now: () => Date }, "now");
    await app.loadOngoing();
    const timeline = calls.find((c) => c.path === "/api/timeline")!.body;
    expect((Date.parse(timeline.to) - Date.parse(timeline.from)) / 86400000).toBeCloseTo(7, 3);
    const unlinked = calls.find((c) => c.path === "/api/ongoing/unlinked")!.body;
    expect(unlinked).toEqual({ repositories: [], from: timeline.from, to: timeline.to, snapshot: 7 });
    fixture.destroy();
  });

  it("performs no preference save when the board renders, refreshes, or the journal moves", async () => {
    vi.useFakeTimers();
    const { app, calls, fixture } = setup();
    fixture.detectChanges();
    await app.loadOngoing();
    app.ongoing.set([row("t1")]);
    app.unlinked.set([entry("a", 1)]);
    fixture.detectChanges();
    await app.loadOngoing();
    await vi.advanceTimersByTimeAsync(5000);
    expect(saves(calls)).toEqual([]);
    expect(app.preferences.board).toBeUndefined();
    fixture.destroy();
  });

  it("saves nothing about the board for preferences that never had one", async () => {
    const { app, calls, fixture } = setup();
    app.preferences = { schemaVersion: 1, theme: "light" };
    await app["persist"]();
    expect(saves(calls)).toHaveLength(1);
    expect(saves(calls)[0].body).not.toHaveProperty("board");
    expect(app.boardOrder()).toEqual([]);
    fixture.destroy();
  });

  it("drops the lowest ranks before a save that would exceed 64 KiB", async () => {
    const { app, calls, fixture } = setup();
    const repo2 = "github.com/some-organization/a-long-repository-name";
    const order = Array.from({ length: 200 }, (_, i) => "change:" + repo2 + "," + repo2 + "-two/a-fairly-long-change-name-" + i);
    app.preferences = { schemaVersion: 1, positions: { filler: 1 }, documents: { filler: "x".repeat(50000) }, board: { order } };
    app.boardOrder.set(order);
    await app["persist"]();
    const body = saves(calls)[0].body;
    expect(new TextEncoder().encode(JSON.stringify(body)).length).toBeLessThanOrEqual(65536);
    expect(body.board.order.length).toBeLessThan(200);
    expect(body.board.order).toEqual(order.slice(0, body.board.order.length));
    expect(app.boardOrder()).toEqual(body.board.order);
    fixture.destroy();
  });

  it("keeps the saved preferences within the server's byte count, which escapes some characters to 6 bytes", async () => {
    const { app, calls, fixture } = setup();
    const repo2 = "github.com/some-organization/a-long-repository-name";
    const order = Array.from({ length: 200 }, (_, i) => "change:" + repo2 + "," + repo2 + "-two/a-fairly-long-change-name-" + i);
    app.preferences = {
      schemaVersion: 1,
      lastRoute: "/ongoing?" + "a=1&".repeat(40),
      positions: { "Ünï/spec&plan+x.md": 1 },
      documents: { filler: "x".repeat(46000) },
      board: { order },
    };
    app.boardOrder.set(order);
    await app["persist"]();
    const body = JSON.stringify(saves(calls)[0].body);
    // The server's default encoder writes `&`, `+`, `<`, `>`, `'`, the backtick and every non-ASCII UTF-16 unit as \uXXXX (6 bytes).
    let serverBytes = 0;
    for (const char of body) serverBytes += /[&+<>'`]/.test(char) || char.charCodeAt(0) > 127 ? 6 * char.length : 1;
    expect(serverBytes).toBeLessThanOrEqual(65536);
    expect(saves(calls)[0].body.board.order.length).toBeLessThan(200);
    fixture.destroy();
  });

  it("offers only the copies that contain the requested document", () => {
    const doc = (path: string) => ({ path, label: "design", fingerprint: "1:1" });
    const a = change("dup", 0, 9, { sourceId: "a", variant: "va", documents: [doc("design.md")] });
    const b = change("dup", 0, 9, { sourceId: "b", variant: "vb", documents: [doc("design.md"), doc("tasks.md")] });
    const { app, fixture } = setup([a, b]);
    const dup = app.fullBoard().proposed[0];
    app.openCard(dup, "tasks.md");
    expect(app.copies()).toEqual([]);
    app.openCard(dup, "design.md");
    expect(app.copies().map((c) => c.sourceId)).toEqual(["a", "b"]);
    fixture.destroy();
  });

  it("loads a saved order and keeps it in later saves", async () => {
    const { app, calls, fixture } = setup();
    const order = ["change:github.com/acme/api/second"];
    app.preferences = { schemaVersion: 1, board: { order } };
    app.boardOrder.set(order);
    await app["persist"]();
    expect(saves(calls)[0].body.board).toEqual({ order });
    fixture.destroy();
  });
});

describe("board rendering", () => {
  it("lays out columns with counts, change cards in their stage, and the latest activity of each card", () => {
    const { app, fixture, element } = setup();
    fixture.detectChanges();
    expect(Array.from(element.querySelectorAll(".board-column-heading h3")).map((h) => h.textContent)).toEqual(["Proposed", "In progress", "Done"]);
    expect(cardTitles(element, "proposed")).toEqual(["Proposal"]);
    expect(cardTitles(element, "in_progress")).toEqual(["Busy", "Second"]);
    expect(cardTitles(element, "done")).toEqual(["Finished"]);
    expect(Array.from(element.querySelectorAll("[data-testid^=board-count]")).map((c) => c.textContent)).toEqual(["1", "2", "1"]);
    const busy = element.querySelector('[data-card-key="change:github.com/acme/api/busy"]')!;
    expect(busy.textContent).toContain("0 / 9");
    expect(busy.textContent).toContain("In progress");
    expect(busy.querySelector("[data-testid=card-date]")!.textContent).toContain("27 Oct 2026");
    expect(element.querySelector('[data-card-key="change:github.com/acme/api/proposal"] [data-testid=card-date]')!.textContent).toContain("No activity recorded");
    expect(element.textContent).not.toContain("Pick up where you left off");
    expect(app.boardTotal()).toBe(4);
    fixture.destroy();
  });

  it("shows both facts when task progress and recorded status disagree", () => {
    const { app, fixture, element } = setup([change("mixed", 3, 9)]);
    app.activity.set([activity("mixed", 1, "done")]);
    fixture.detectChanges();
    const card = element.querySelector('[data-card-key="change:github.com/acme/api/mixed"]')!;
    expect(card.closest(".board-column")!.getAttribute("data-stage")).toBe("done");
    expect(card.textContent).toContain("3 / 9");
    expect(card.textContent).toContain("Recorded done");
    fixture.destroy();
  });

  it("shows ticket, follow-up, and ad-hoc cards with their labels and dates", () => {
    const { app, fixture, element } = setup([change("shipped", 3, 3, { archived: true, archivedOn: "2026-10-01" })]);
    app.ongoing.set([row("DEP-12", { lastAt: daysAgo(2) }), row("follow", { changes: ["shipped"], lastAt: daysAgo(1), title: "DEP-30" })]);
    app.unlinked.set([entry("a", 6, { status: "done", summary: "Cleaned up flaky tests", extractedTicketId: "DEP-99" })]);
    fixture.detectChanges();
    const tickets = Array.from(element.querySelectorAll('[data-kind="ticket"]')) as HTMLElement[];
    expect(tickets.map((t) => t.querySelector(".badge")!.textContent!.trim())).toEqual(["Follow-up to Shipped", "Ticket · no OpenSpec change"]);
    expect(tickets[0].querySelector("[data-testid=card-date]")!.textContent).toContain("27 Oct 2026");
    expect(tickets[0].querySelector(".ticket-ref")).not.toBeNull();
    const adhoc = element.querySelector('[data-kind="adhoc"]')!;
    expect(column(element, "done").contains(adhoc)).toBe(true);
    expect(adhoc.querySelector(".change-title")!.textContent).toContain("Cleaned up flaky tests");
    expect(adhoc.textContent).toContain("DEP-99");
    expect(adhoc.querySelector(".adhoc-details")).toBeNull();
    fixture.destroy();
  });

  it("uses stable shared palette cues for sidebar and distinct repository labels across card kinds", () => {
    const shared = change("shared", 0, 9, { repositories: [repo, "github.com/acme/other", repo] });
    const { app, fixture, element } = setup([shared, change("single", 0, 9), change("unlinked", 0, 9, { repositories: [] })]);
    app.catalog.update((catalog) => ({
      ...catalog!,
      repositories: [
        { id: "github.com/acme/other", label: "other" },
        { id: repo, label: "api" },
        { id: "github.com/acme/added", label: "added later" },
      ],
    }));
    app.activity.set([activity("shared", 1), activity("single", 1)]);
    app.ongoing.set([row("DEP-12"), row("REC-5", { repository: "github.com/acme/other", tickets: [{ provider: "jira", scope: "company", id: "REC-5" }] })]);
    app.unlinked.set([entry("adhoc", 1)]);
    fixture.detectChanges();

    const sidebarColors = new Map(Array.from(element.querySelectorAll<HTMLButtonElement>(".repo-link"))
      .filter((link) => !link.querySelector(".all-dot"))
      .map((link) => [link.textContent!.trim(), link.querySelector(".repo-dot")!.getAttribute("data-repository-color")]));
    expect(sidebarColors.get("api")).toBe(app.repositoryColor(repo));
    expect(sidebarColors.get("other")).toBe(app.repositoryColor("github.com/acme/other"));
    expect(sidebarColors.get("added later")).toBe(app.repositoryColor("github.com/acme/added"));
    expect(element.querySelector(".all-dot")!.getAttribute("data-repository-color")).toBeNull();

    const sharedCard = Array.from(element.querySelectorAll<HTMLElement>('[data-kind="change"]'))
      .find((card) => card.querySelector(".change-title")?.textContent?.trim() === "Shared")!;
    expect(sharedCard.hasAttribute("data-repository-color")).toBe(false);
    expect(sharedCard.querySelectorAll(".card-repository-item")).toHaveLength(2);
    expect(Array.from(sharedCard.querySelectorAll(".card-repository-item .repo-dot")).map((dot) => dot.getAttribute("data-repository-color")))
      .toEqual([app.repositoryColor(repo), app.repositoryColor("github.com/acme/other")]);
    expect(Array.from(sharedCard.querySelectorAll(".repo-dot")).every((dot) => dot.getAttribute("aria-hidden") === "true")).toBe(true);

    const singleChange = Array.from(element.querySelectorAll<HTMLElement>('[data-kind="change"]'))
      .find((card) => card.querySelector(".change-title")?.textContent?.trim() === "Single")!;
    expect(singleChange.getAttribute("data-repository-color")).toBe(app.repositoryColor(repo));
    const unlinkedChange = Array.from(element.querySelectorAll<HTMLElement>('[data-kind="change"]'))
      .find((card) => card.querySelector(".change-title")?.textContent?.trim() === "Unlinked")!;
    expect(unlinkedChange.hasAttribute("data-repository-color")).toBe(false);
    const ticket = element.querySelector('[data-kind="ticket"]')!;
    const adhoc = element.querySelector('[data-kind="adhoc"]')!;
    expect(ticket.getAttribute("data-repository-color")).toBe(app.repositoryColor(repo));
    expect(adhoc.getAttribute("data-repository-color")).toBe(app.repositoryColor(repo));
    expect(ticket.querySelector(".card-repository-item .repo-dot")!.getAttribute("aria-hidden")).toBe("true");
    expect(adhoc.querySelector(".card-repository-item .repo-dot")!.getAttribute("aria-hidden")).toBe("true");
    expect(sharedCard.querySelector(".card-footer")!.getAttribute("data-repository-color")).toBeNull();

    app.selectedRepos.set([repo]);
    app.catalog.update((catalog) => ({ ...catalog!, repositories: [...catalog!.repositories].reverse() }));
    fixture.detectChanges();
    expect(element.querySelector('.repo-link[title="github.com/acme/api"] .repo-dot')!.getAttribute("data-repository-color")).toBe(app.repositoryColor(repo));
    fixture.destroy();
  });

  it("expands an ad-hoc card in place and opens the drill-down for a ticket card", () => {
    const { app, fixture, element } = setup([]);
    app.ongoing.set([row("DEP-12")]);
    app.unlinked.set([entry("a", 1, { summary: "A longer summary of the work", agent: "codex", branch: "feature/x" })]);
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('[data-kind="adhoc"] .change-title')!.click();
    fixture.detectChanges();
    const details = element.querySelector('[data-kind="adhoc"] .adhoc-details')!;
    expect(details.textContent).toContain("A longer summary of the work");
    expect(details.textContent).toContain("codex");
    expect(details.textContent).toContain("feature/x");
    const open = vi.spyOn(app, "openItem").mockResolvedValue();
    element.querySelector<HTMLButtonElement>('[data-kind="ticket"] .change-title')!.click();
    expect(open).toHaveBeenCalledWith(app.ongoing()[0], undefined, true);
    fixture.destroy();
  });

  it("shows the quiet indication only on In-progress change cards, and the no-activity note on Proposed ones", () => {
    const { app, fixture, element } = setup([change("quiet", 0, 9), change("finished", 9, 9), change("idea", 0, 9)]);
    app.activity.set([activity("quiet", 20), activity("finished", 20)]);
    fixture.detectChanges();
    const chips = (name: string) => Array.from(element.querySelector(`[data-card-key="change:github.com/acme/api/${name}"]`)!.querySelectorAll(".quiet-chip")).map((c) => c.textContent!.trim());
    expect(chips("quiet")).toHaveLength(1);
    expect(chips("quiet")[0]).toContain("No activity recorded in the last 14 days");
    expect(chips("finished")).toEqual([]);
    expect(chips("idea")).toEqual(["No recorded activity"]);
    fixture.destroy();
  });

  it("notes truncated unlinked work and hides the note otherwise", () => {
    const { app, fixture, element } = setup();
    fixture.detectChanges();
    expect(element.querySelector("[data-testid=board-truncated]")).toBeNull();
    app.unlinkedTruncated.set(true);
    fixture.detectChanges();
    expect(element.querySelector("[data-testid=board-truncated]")!.textContent).toContain("History");
    fixture.destroy();
  });

  it("shows the board's empty state without cards and a short line in an empty column", () => {
    const empty = setup([]);
    empty.fixture.detectChanges();
    expect(empty.element.querySelector(".board")).toBeNull();
    expect(empty.element.querySelector(".empty-state h2")!.textContent).toContain("A little space for your next idea");
    empty.fixture.destroy();
    TestBed.resetTestingModule();
    const { fixture, element } = setup([change("proposal", 0, 9)]);
    fixture.detectChanges();
    expect(column(element, "done").querySelector(".board-column-empty")!.textContent).toContain("Nothing done yet");
    fixture.destroy();
  });

  it("filters every column, shows that the filter is active, and clears it", () => {
    const { app, fixture, element } = setup();
    app.ongoing.set([row("DEP-12"), row("REC-5", { tickets: [{ provider: "jira", scope: "company", id: "REC-5" }] })]);
    fixture.detectChanges();
    expect(element.querySelector(".board-filter-active")).toBeNull();
    const input = element.querySelector<HTMLInputElement>(".board-filter-input")!;
    input.value = "dep-12";
    input.dispatchEvent(new Event("input"));
    fixture.detectChanges();
    expect(cardTitles(element, "in_progress")).toEqual(["DEP-12"]);
    expect(cardTitles(element, "proposed")).toEqual([]);
    expect(column(element, "proposed").textContent).toContain("No matching cards");
    expect(element.querySelector(".board-filter-active")!.textContent).toContain("1 of 6 cards");
    element.querySelector<HTMLButtonElement>(".board-filter-active button")!.click();
    fixture.detectChanges();
    expect(element.querySelector(".board-filter-active")).toBeNull();
    expect(app.preferences).not.toHaveProperty("board");
    fixture.destroy();
  });

  it("does not remember the filter in preferences", async () => {
    const { app, calls, fixture } = setup();
    app.boardFilter.set("busy");
    await app["persist"]();
    expect(JSON.stringify(saves(calls)[0].body)).not.toContain("busy");
    fixture.destroy();
  });

  it("shows no change cards until the activity of the selection is loaded", () => {
    const { app, fixture, element } = setup();
    app["activityScope"].set(JSON.stringify(["other"]));
    app.selectedRepos.set([repo]);
    app.ongoing.set([row("DEP-12")]);
    fixture.detectChanges();
    expect(element.querySelectorAll(".change-card")).toHaveLength(0);
    expect(element.querySelectorAll('[data-kind="ticket"]')).toHaveLength(1);
    fixture.destroy();
  });
});

describe("reordering", () => {
  const keyOf = (name: string) => "change:github.com/acme/api/" + name;
  const dragEvent = (clientY = 0) => {
    const target = document.createElement("div");
    target.getBoundingClientRect = () => ({ top: 0, height: 100 }) as DOMRect;
    return { preventDefault: vi.fn(), clientY, currentTarget: target, dataTransfer: { dropEffect: "", effectAllowed: "", setData: vi.fn(), setDragImage: vi.fn() } } as unknown as DragEvent;
  };
  const card = (app: App, key: string) => Object.values(app.fullBoard()).flat().find((c: BoardCard) => c.key === key)!;

  it("saves the whole column's order when a card moves with the keyboard actions", async () => {
    vi.useFakeTimers();
    const { app, calls, fixture, element } = setup();
    fixture.detectChanges();
    expect(cardTitles(element, "in_progress")).toEqual(["Busy", "Second"]);
    element.querySelector<HTMLButtonElement>(`[data-card-key="${keyOf("second")}"] [data-move="up"]`)!.click();
    fixture.detectChanges();
    expect(cardTitles(element, "in_progress")).toEqual(["Second", "Busy"]);
    expect(saves(calls)).toEqual([]);
    await vi.advanceTimersByTimeAsync(500);
    expect(saves(calls)).toHaveLength(1);
    expect(saves(calls)[0].body.board.order).toEqual([keyOf("second"), keyOf("busy")]);
    app.moveCard(card(app, keyOf("second")), "down");
    await vi.advanceTimersByTimeAsync(500);
    expect(saves(calls).at(-1)!.body.board.order).toEqual([keyOf("busy"), keyOf("second")]);
    app.moveCard(card(app, keyOf("second")), "top");
    expect(app.boardOrder()).toEqual([keyOf("second"), keyOf("busy")]);
    fixture.destroy();
  });

  it("keeps the order across a reload of the saved preferences", async () => {
    const first = setup();
    first.app.moveCard(card(first.app, keyOf("second")), "up");
    const saved = first.app.preferences.board!.order!;
    first.fixture.destroy();
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
    const second = setup();
    second.app.boardOrder.set(saved);
    second.fixture.detectChanges();
    expect(cardTitles(second.element, "in_progress")).toEqual(["Second", "Busy"]);
    second.fixture.destroy();
  });

  it("keeps focus on the moved card's action and announces the new position", async () => {
    vi.useFakeTimers();
    const { app, fixture, element } = setup();
    fixture.detectChanges();
    const button = () => element.querySelector<HTMLButtonElement>(`[data-card-key="${keyOf("second")}"] [data-move="up"]`)!;
    button().focus();
    button().click();
    fixture.detectChanges();
    await vi.advanceTimersByTimeAsync(10);
    expect(document.activeElement).toBe(button());
    expect(app.boardAnnouncement()).toBe("Second moved to position 1 of 2 in In progress.");
    fixture.detectChanges();
    expect(element.querySelector("[data-testid=board-announcement]")!.textContent).toContain("Second moved to position 1 of 2");
    button().click();
    expect(app.boardAnnouncement()).toBe("Second is already first in In progress.");
    fixture.destroy();
  });

  it("moves past cards hidden by the filter and ranks every card of the column", () => {
    const { app, fixture } = setup([change("one-a", 0, 9), change("two-b", 0, 9), change("three-a", 0, 9)]);
    app.activity.set([activity("one-a", 1), activity("two-b", 2), activity("three-a", 3)]);
    app.boardFilter.set("-a");
    app.moveCard(card(app, keyOf("three-a")), "up");
    expect(app.boardOrder()).toEqual([keyOf("three-a"), keyOf("one-a"), keyOf("two-b")]);
    fixture.destroy();
  });

  it("rejects a drop from another column and accepts one inside the column", () => {
    const { app, fixture } = setup();
    const done = card(app, keyOf("finished"));
    const second = card(app, keyOf("second"));
    const busy = card(app, keyOf("busy"));
    app.dragStart(done, dragEvent());
    const over = dragEvent();
    app.dragOver(busy, over);
    expect(over.preventDefault).not.toHaveBeenCalled();
    expect(app.dropMark()).toBeNull();
    const rejected = dragEvent();
    expect(app.dropOn(busy, rejected)).toBe(false);
    expect(rejected.preventDefault).not.toHaveBeenCalled();
    expect(app.boardOrder()).toEqual([]);
    expect(app.dragged()).toBeNull();

    app.dragStart(second, dragEvent());
    const inside = dragEvent(10);
    app.dragOver(busy, inside);
    expect(inside.preventDefault).toHaveBeenCalled();
    expect(app.dropMark()).toEqual({ key: keyOf("busy"), after: false });
    expect(app.dropOn(busy, dragEvent())).toBe(true);
    expect(app.boardOrder()).toEqual([keyOf("second"), keyOf("busy")]);
    expect(app.dropMark()).toBeNull();
    fixture.destroy();
  });

  it("drops after a card when the pointer is in its lower half and ignores a drop onto itself", () => {
    const { app, fixture } = setup();
    const second = card(app, keyOf("second"));
    const busy = card(app, keyOf("busy"));
    app.dragStart(busy, dragEvent());
    app.dragOver(second, dragEvent(90));
    expect(app.dropOn(second, dragEvent())).toBe(true);
    expect(app.boardOrder()).toEqual([keyOf("second"), keyOf("busy")]);
    app.dragStart(busy, dragEvent());
    app.dragOver(busy, dragEvent());
    expect(app.dropOn(busy, dragEvent())).toBe(false);
    fixture.destroy();
  });

  it("drops ranks of archived changes and expired cards on the next save", () => {
    const { app, fixture } = setup();
    app.boardOrder.set([keyOf("gone"), "adhoc:old", keyOf("busy")]);
    app.moveCard(card(app, keyOf("second")), "down");
    expect(app.boardOrder()).toEqual([keyOf("busy"), keyOf("second")]);
    fixture.destroy();
  });

  it("keeps the rank of an active change that the repository selection hides, also in the saved preferences", async () => {
    const hidden = change("hidden", 0, 9, { repositories: ["github.com/acme/web"] });
    const { app, calls, fixture } = setup([change("busy", 0, 9), change("second", 0, 9), hidden]);
    app.selectedRepos.set([repo]);
    app["activityScope"].set("[]");
    app.activity.set([activity("busy", 1), activity("second", 4)]);
    const hiddenKey = "change:github.com/acme/web/hidden";
    app.boardOrder.set([hiddenKey]);
    app.moveCard(card(app, keyOf("second")), "up");
    expect(app.boardOrder()).toContain(hiddenKey);
    await app["persist"]();
    expect(saves(calls).at(-1)!.body.board.order).toContain(hiddenKey);
    fixture.destroy();
  });
});
