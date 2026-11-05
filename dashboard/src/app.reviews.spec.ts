import { TestBed } from "@angular/core/testing";
import { Router } from "@angular/router";
import { Subject } from "rxjs";
import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { App } from "./app";
import { baselineKey } from "./reviews";
import { Catalog, Change, DocumentContent, ReviewItem } from "./models";

const change: Change = {
  sourceId: "s",
  name: "demo",
  directoryName: "demo",
  variant: "v",
  archived: false,
  archivedOn: null,
  repositories: ["r"],
  documents: [
    { path: "design.md", label: "design", fingerprint: "100:5000" },
    { path: "tasks.md", label: "tasks", fingerprint: "40:6000" },
  ],
  completedTasks: 1,
  totalTasks: 3,
  error: null,
};
const other: Change = {
  ...change,
  name: "other",
  directoryName: "other",
  variant: "v2",
  documents: [{ path: "design.md", label: "design", fingerprint: "7:70" }],
};
const archived: Change = {
  ...change,
  name: "old",
  directoryName: "2026-10-01-old",
  variant: "v3",
  archived: true,
  archivedOn: "2026-10-01",
  documents: [{ path: "design.md", label: "design", fingerprint: "8:80" }],
};
const idA = "a".repeat(32);
const timeline = { snapshot: 1, rows: [], total: 0, nextOffset: null, insights: { longestSpans: [], completions: [], subsequentWork: [], days: [] } };
const marker = (id: string, quote: string, comment: string) =>
  `<!-- REVIEW: id=${id}\nTarget: preceding passage\nQuote: ${quote}\nComment:\n${comment}\n-->`;
const item = (c: Change, path: string, line: number, comment: string): ReviewItem => ({
  sourceId: c.sourceId, change: c.name, variant: c.variant, path, archived: c.archived, repositories: c.repositories,
  line, id: null, quote: "A passage", comment, truncated: false,
});

interface Call { path: string; method: string; body: any }
interface Stored { revision: string; text: string }

/** A fake server: documents, a baseline store that behaves like the real one (409 for a stale revision), and the overview. */
function setup() {
  const calls: Call[] = [];
  let revisionNumber = 0;
  const nextRevision = () => "r" + ++revisionNumber;
  const server = {
    calls,
    catalog: {
      repositories: [{ id: "r", label: "Project" }], sources: [{ id: "s", repositories: ["r"], location: "project", error: null }],
      changes: [change, other, archived], launchRepository: "r", revision: "1", notices: [], journalSnapshot: 5,
    } as Catalog,
    docs: new Map<string, Stored>(),
    baselines: new Map<string, Stored>(),
    summary: {} as Record<string, string>,
    reviews: { items: [] as ReviewItem[], unavailable: [] as string[] },
    count: (path: string) => calls.filter((c) => c.path === path).length,
    marks: () => calls.filter((c) => c.path === "/api/reads/mark").map((c) => [c.body.document.path, c.body.revision]),
    /** The agent rewrites a document; its revision changes. */
    edit(path: string, text: string) {
      const stored = { revision: nextRevision(), text };
      server.docs.set(path, stored);
      return stored;
    },
    seed(path: string, text: string) {
      return server.edit(path, text);
    },
  };
  const content = (path: string): DocumentContent => ({
    sourceId: "s", change: "demo", path, location: "project", archived: false, canComment: true, tasks: [], ...server.docs.get(path)!,
  });
  const key = (body: any) => baselineKey(body.document.sourceId, body.document.change, body.document.path);
  vi.spyOn(globalThis, "fetch").mockImplementation(async (input, options) => {
    const path = String(input);
    const method = options?.method ?? "GET";
    const body = options?.body ? JSON.parse(options.body as string) : undefined;
    calls.push({ path, method, body });
    switch (path) {
      case "/api/catalog": return Response.json(server.catalog);
      case "/api/state": return method === "GET" ? Response.json({ schemaVersion: 1 }) : new Response(null, { status: 204 });
      case "/api/info": return Response.json({ timezone: "UTC" });
      case "/api/timeline": return Response.json(timeline);
      case "/api/search": return Response.json({ matches: [], limitReached: false, unavailable: [] });
      case "/api/reviews": return Response.json(server.reviews);
      case "/api/reads/summary": return Response.json({ entries: server.summary });
      case "/api/document":
        return body.knownRevision && body.knownRevision === server.docs.get(body.path)?.revision
          ? Response.json({ revision: body.knownRevision, unchanged: true })
          : Response.json(content(body.path));
      case "/api/reads/get": return Response.json(server.baselines.get(key(body)) ?? null);
      case "/api/reads/mark": {
        const current = server.docs.get(body.document.path)!;
        if (current.revision !== body.revision) return Response.json({ error: "The document changed after it was displayed." }, { status: 409 });
        server.baselines.set(key(body), { ...current });
        return Response.json({ revision: current.revision, fingerprint: "100:5000" });
      }
      case "/api/comments": {
        const current = server.docs.get(body.path)!;
        server.edit(body.path, current.text.replace(/\n*$/, "\n\n") + marker(idA, body.blockText, body.comment) + "\n");
        return Response.json(content(body.path));
      }
      case "/api/comments/delete": {
        const current = server.docs.get(body.path)!;
        server.edit(body.path, current.text.replace(body.markerText, "").replace(/\n{3,}/g, "\n\n"));
        return Response.json(content(body.path));
      }
    }
    throw new Error("Unexpected request: " + path);
  });
  vi.spyOn(App.prototype, "ngAfterViewInit").mockImplementation(() => {});
  const events = new Subject();
  TestBed.configureTestingModule({
    imports: [App],
    providers: [{ provide: Router, useValue: { events, navigate: vi.fn().mockResolvedValue(true), parseUrl: () => ({ queryParams: {}, root: { children: {} } }), url: "/ongoing" } }],
  });
  const fixture = TestBed.createComponent(App);
  const app = fixture.componentInstance;
  app.catalog.set(server.catalog);
  vi.spyOn(document, "hidden", "get").mockReturnValue(false);
  const navigate = TestBed.inject(Router).navigate as unknown as ReturnType<typeof vi.fn>;
  const open = async (path: string) => {
    await app["loadDocument"]({ sourceId: "s", change: "demo", path, variant: "v" });
    app.selectedChange.set(change);
  };
  return { app, fixture, server, open, navigate, content };
}
const changedStarts = (html: string) =>
  Array.from(new DOMParser().parseFromString(html, "text/html").querySelectorAll<HTMLElement>(".changed-since-read")).map((e) => e.dataset["start"]);
const html = (app: App) => String((app.html() as any).changingThisBreaksApplicationSecurity ?? "");

beforeEach(() => {
  Element.prototype.scrollIntoView = vi.fn();
});
afterEach(() => {
  vi.restoreAllMocks();
  TestBed.resetTestingModule();
});

const design = "# Design\n\nIntro paragraph.\n\nSecond paragraph.\n\nThird paragraph.\n";

describe("read baselines in the reader", () => {
  it("records the displayed revision on the first open and highlights nothing", async () => {
    const { app, server, open, fixture } = setup();
    const stored = server.seed("design.md", design);
    await open("design.md");
    expect(server.marks()).toEqual([["design.md", stored.revision]]);
    expect(app.baseline()).toEqual(stored);
    expect(app.readMarks()).toBeNull();
    expect(changedStarts(html(app))).toEqual([]);
    fixture.destroy();
  });

  it("highlights what an agent changed after the last read, and Mark as read clears it", async () => {
    const { app, server, open, fixture } = setup();
    server.seed("design.md", design);
    await open("design.md");
    const agent = server.edit("design.md", design.replace("Second paragraph.", "Second paragraph, rewritten.") + "\nFourth paragraph, added.\n");
    // A later visit opens the document again; the baseline is the earlier content.
    app.document.set(null);
    await open("design.md");
    expect(app.readMarks()).not.toBeNull();
    expect(changedStarts(html(app))).toEqual(["4", "8"]);
    expect(app.baseline()!.revision).not.toBe(agent.revision);
    server.calls.length = 0;
    await app.markRead();
    expect(server.marks()).toEqual([["design.md", agent.revision]]);
    expect(app.readMarks()).toBeNull();
    expect(changedStarts(html(app))).toEqual([]);
    expect(app.baseline()).toEqual(agent);
    // The stored length and write time now match what the catalog reports, so the card hint clears too.
    expect(app.readSummary()[baselineKey("s", "demo", "design.md")]).toBe("100:5000");
    fixture.destroy();
  });

  it("shows a live agent edit of the open document against the last read without moving the baseline", async () => {
    const { app, server, open, fixture } = setup();
    const first = server.seed("design.md", design);
    await open("design.md");
    server.edit("design.md", design.replace("Third paragraph.", "Third paragraph, edited live."));
    server.calls.length = 0;
    await app["poll"]();
    await app["poll"]();
    expect(changedStarts(html(app))).toEqual(["6"]);
    expect(server.marks()).toEqual([]);
    expect(app.baseline()).toEqual(first);
    fixture.destroy();
  });

  it("does not highlight the user's own comment when the document was read just before saving it", async () => {
    const { app, server, open, fixture } = setup();
    server.seed("design.md", design);
    await open("design.md");
    server.calls.length = 0;
    app.selection.set({ start: 2, end: 3, text: "Intro paragraph." });
    app.comment.set("Please expand this.");
    await app.saveComment();
    const saved = server.docs.get("design.md")!;
    expect(server.marks()).toEqual([["design.md", saved.revision]]);
    expect(app.baseline()).toEqual(saved);
    expect(app.readMarks()).toBeNull();
    expect(changedStarts(html(app))).toEqual([]);
    expect(app.reviews()).toHaveLength(1);
    expect(app.removedComments()).toEqual([]);
    // Deleting the user's own comment is not a change either.
    server.calls.length = 0;
    const review = app.reviews()[0];
    app.requestDeleteReview(review);
    await app.deleteReview();
    expect(server.marks()).toEqual([["design.md", server.docs.get("design.md")!.revision]]);
    expect(app.readMarks()).toBeNull();
    expect(app.removedComments()).toEqual([]);
    fixture.destroy();
  });

  it("keeps an unread agent edit highlighted when the user saves a comment after it", async () => {
    const { app, server, open, fixture } = setup();
    const first = server.seed("design.md", design);
    await open("design.md");
    server.edit("design.md", design.replace("Second paragraph.", "Second paragraph, rewritten by the agent."));
    await app["poll"]();
    expect(changedStarts(html(app))).toEqual(["4"]);
    server.calls.length = 0;
    app.selection.set({ start: 2, end: 3, text: "Intro paragraph." });
    app.comment.set("A note after the agent's edit.");
    await app.saveComment();
    // The baseline did not move, so the agent's edit stays highlighted; no mark was requested.
    expect(server.marks()).toEqual([]);
    expect(app.baseline()).toEqual(first);
    expect(changedStarts(html(app))).toContain("4");
    expect(app.reviews()).toHaveLength(1);
    fixture.destroy();
  });

  it("treats a stale mark as a conflict and keeps the baseline", async () => {
    const { app, server, open, fixture } = setup();
    const first = server.seed("design.md", design);
    await open("design.md");
    const shown = app.document()!;
    server.edit("design.md", design + "\nAgent addition.\n");
    await app.markRead();
    expect(server.marks().at(-1)).toEqual(["design.md", shown.revision]);
    expect(app.baseline()).toEqual(first);
    expect(app["notice"]()).toContain("changed while you were reading");
    fixture.destroy();
  });

  it("marks every document of the open change read", async () => {
    const { app, server, open, fixture } = setup();
    const a = server.seed("design.md", design);
    const b = server.seed("tasks.md", "# Tasks\n\n- [ ] 1.1 One\n");
    await open("design.md");
    server.edit("design.md", design + "\nMore.\n");
    server.edit("tasks.md", "# Tasks\n\n- [ ] 1.1 One\n- [ ] 1.2 Two\n");
    app.document.set(null);
    await open("design.md");
    expect(app.readMarks()).not.toBeNull();
    server.calls.length = 0;
    await app.markChangeRead();
    expect(server.marks()).toEqual([
      ["design.md", server.docs.get("design.md")!.revision],
      ["tasks.md", server.docs.get("tasks.md")!.revision],
    ]);
    expect(server.docs.get("design.md")!.revision).not.toBe(a.revision);
    expect(server.docs.get("tasks.md")!.revision).not.toBe(b.revision);
    expect(app.readMarks()).toBeNull();
    expect(app["notice"]()).toContain("marked as read");
    expect(server.baselines.get(baselineKey("s", "demo", "tasks.md"))!.text).toContain("1.2 Two");
    fixture.destroy();
  });

  it("reports a deleted document without claiming the dashboard connection failed", async () => {
    const { app, server, open, fixture } = setup();
    server.seed("design.md", design);
    server.seed("tasks.md", "# Tasks\n\n- [ ] 1.1 One\n");
    await open("design.md");
    app.selection.set({ start: 1, end: 2, text: "Intro paragraph." });
    app.comment.set("Keep this draft when a document disappears.");
    const serve = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation(async (input, options) => {
      const body = options?.body ? JSON.parse(options.body as string) : undefined;
      if (String(input) === "/api/document" && body.path === "tasks.md")
        return Response.json({ error: "Document unavailable." }, { status: 404 });
      return serve(input, options);
    });

    await app.markChangeRead();

    expect(app.readSummary()[baselineKey("s", "demo", "design.md")]).toBe("100:5000");
    expect(app.notice()).toContain("1 document was marked as read");
    expect(app.notice()).toContain("1 document could not be marked");
    expect(app.notice()).toContain("refresh the change, confirm its documents and read state are available, then retry");
    expect(app.notice()).not.toContain("connection");
    expect(app.notice()).not.toContain("Document unavailable");
    expect(app.selection()).toEqual({ start: 1, end: 2, text: "Intro paragraph." });
    expect(app.comment()).toBe("Keep this draft when a document disappears.");
    fixture.destroy();
  });

  it("reports a read-state storage failure with one aggregate retry hint", async () => {
    const { app, server, open, fixture } = setup();
    server.seed("design.md", design);
    server.seed("tasks.md", "# Tasks\n\n- [ ] 1.1 One\n");
    await open("design.md");
    app.selection.set({ start: 1, end: 2, text: "Intro paragraph." });
    app.comment.set("Keep this draft when read state cannot be saved.");
    const serve = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation(async (input, options) => {
      const body = options?.body ? JSON.parse(options.body as string) : undefined;
      if (String(input) === "/api/reads/mark" && body.document.path === "tasks.md")
        return Response.json({ error: "Read storage unavailable." }, { status: 503 });
      return serve(input, options);
    });

    await app.markChangeRead();

    expect(app.readSummary()[baselineKey("s", "demo", "design.md")]).toBe("100:5000");
    expect(app.notice()).toContain("1 document was marked as read");
    expect(app.notice()).toContain("1 document could not be marked");
    expect(app.notice()).toContain("refresh the change, confirm its documents and read state are available, then retry");
    expect(app.notice().match(/could not be marked/g)).toHaveLength(1);
    expect(app.notice()).not.toContain("connection");
    expect(app.notice()).not.toContain("Read storage unavailable");
    expect(app.selection()).toEqual({ start: 1, end: 2, text: "Intro paragraph." });
    expect(app.comment()).toBe("Keep this draft when read state cannot be saved.");
    fixture.destroy();
  });

  it("summarizes mixed mark outcomes while retaining successful fingerprints", async () => {
    const { app, server, open, fixture } = setup();
    server.seed("design.md", design);
    server.seed("tasks.md", "# Tasks\n\n- [ ] 1.1 One\n");
    server.seed("proposal.md", "# Proposal\n\nOne more document.\n");
    await open("design.md");
    app.selectedChange.set({ ...change, documents: [...change.documents, { path: "proposal.md", label: "proposal", fingerprint: "20:7000" }] });
    app.selection.set({ start: 1, end: 2, text: "Intro paragraph." });
    app.comment.set("Keep this draft across a partial mark failure.");
    const serve = vi.mocked(globalThis.fetch).getMockImplementation()!;
    let marks = 0;
    vi.mocked(globalThis.fetch).mockImplementation(async (input, options) => {
      if (String(input) === "/api/reads/mark") {
        marks++;
        if (marks === 2) return Response.json({ error: "Changed during mark." }, { status: 409 });
        if (marks === 3) throw new TypeError("Failed to fetch");
      }
      return serve(input, options);
    });

    await app.markChangeRead();

    expect(marks).toBe(3);
    expect(app.readSummary()[baselineKey("s", "demo", "design.md")]).toBe("100:5000");
    expect(app.readSummary()[baselineKey("s", "demo", "tasks.md")]).toBeUndefined();
    expect(app.readSummary()[baselineKey("s", "demo", "proposal.md")]).toBeUndefined();
    expect(app.notice()).toContain("1 document was marked as read");
    expect(app.notice()).toContain("1 document changed while being marked");
    expect(app.notice()).toContain("1 document could not be marked");
    expect(app.notice()).toContain("refresh the change, confirm its documents and read state are available, then retry");
    expect(app.selection()).toEqual({ start: 1, end: 2, text: "Intro paragraph." });
    expect(app.comment()).toBe("Keep this draft across a partial mark failure.");
    fixture.destroy();
  });

  it("stops a change-wide mark on authorization loss and reports other failures separately", async () => {
    const { app, server, open, fixture } = setup();
    server.seed("design.md", design);
    server.seed("tasks.md", "# Tasks\n\n- [ ] 1.1 One\n");
    server.seed("proposal.md", "# Proposal\n\nOne more document.\n");
    await open("design.md");
    app.selectedChange.set({ ...change, documents: [...change.documents, { path: "proposal.md", label: "proposal", fingerprint: "20:7000" }] });
    app.selection.set({ start: 1, end: 2, text: "Intro paragraph." });
    app.comment.set("Keep this draft after authorization loss.");
    const serve = vi.mocked(globalThis.fetch).getMockImplementation()!;
    let marks = 0;
    vi.mocked(globalThis.fetch).mockImplementation(async (input, options) => {
      if (String(input) === "/api/reads/mark") {
        marks++;
        return Response.json({ error: "Unavailable." }, { status: marks === 1 ? 503 : 401 });
      }
      return serve(input, options);
    });

    await app.markChangeRead();

    expect(marks).toBe(2);
    expect(app.notice()).toContain("1 document could not be marked");
    expect(app.notice()).toContain("refresh the change, confirm its documents and read state are available, then retry");
    expect(app.notice()).toContain("Authorization was lost");
    expect(app.notice()).not.toContain("changed while being marked");
    expect(app.selection()).toEqual({ start: 1, end: 2, text: "Intro paragraph." });
    expect(app.comment()).toBe("Keep this draft after authorization loss.");
    fixture.destroy();
  });

  it("gives a fresh dashboard launch action when marking one document loses authorization", async () => {
    const { app, server, open, fixture } = setup();
    server.seed("design.md", design);
    await open("design.md");
    app.comment.set("Keep this single-document draft.");
    const serve = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation(async (input, options) =>
      String(input) === "/api/reads/mark"
        ? Response.json({ error: "Unauthorized." }, { status: 401 })
        : serve(input, options));

    await app.markRead();

    expect(app.unauthorized()).toBe(true);
    expect(app.notice()).toContain("run `worklog dashboard` again");
    expect(app.comment()).toBe("Keep this single-document draft.");
    fixture.destroy();
  });

  it("shows nothing to compare when the baseline cannot be read, and still renders the document", async () => {
    const { app, server, open, fixture } = setup();
    server.seed("design.md", design);
    const serve = vi.mocked(globalThis.fetch).getMockImplementation()!;
    vi.mocked(globalThis.fetch).mockImplementation(async (input, options) =>
      String(input).startsWith("/api/reads/") ? Response.json({ error: "Unavailable" }, { status: 503 }) : serve(input, options));
    await open("design.md");
    expect(app.document()!.path).toBe("design.md");
    expect(app.baseline()).toBeNull();
    expect(app.readMarks()).toBeNull();
    expect(app.error()).toBe("");
    // Opening again and refreshing the hints repeats the failure without a notice, and no document is shown as updated.
    await open("design.md");
    await app.loadReadSummary();
    await app.loadReadSummary();
    expect(app.error()).toBe("");
    expect(app["notice"]()).toBe("");
    expect(app.readSummary()).toEqual({});
    fixture.destroy();
  });
});

describe("removed since last read", () => {
  it("lists the comments that were in the last read and are gone, with their quote", async () => {
    const { app, server, open, fixture } = setup();
    const withComment = design + "\n" + marker(idA, "Third paragraph.", "Say more about the third.") + "\n";
    server.seed("design.md", withComment);
    server.seed("tasks.md", "# Tasks\n");
    await open("design.md");
    expect(app.removedComments()).toEqual([]);
    // The agent addresses the comment: the marker is removed and the paragraph rewritten.
    server.edit("design.md", design.replace("Third paragraph.", "Third paragraph, now much longer."));
    await app["poll"]();
    expect(app.removedComments().map((r) => [r.id, r.quote, r.text])).toEqual([[idA, "Third paragraph.", "Say more about the third."]]);
    expect(changedStarts(html(app))).toEqual(["6"]);
    const cached = app["removedReviewsMemo"];
    app["renderDocument"](app.document()!);
    expect(app["removedReviewsMemo"]).toBe(cached);
    server.edit("design.md", server.docs.get("design.md")!.text + "\nAn additional current paragraph.\n");
    await app["poll"]();
    expect(app["removedReviewsMemo"]).not.toBe(cached);
    await open("tasks.md");
    expect(app["removedReviewsMemo"]).toBeNull();
    fixture.destroy();
  });

  it("keeps a comment that is still present, even when its text was edited", async () => {
    const { app, server, open, fixture } = setup();
    server.seed("design.md", design + "\n" + marker(idA, "Third paragraph.", "Original") + "\n");
    await open("design.md");
    server.edit("design.md", design + "\n" + marker(idA, "Third paragraph.", "Reworded") + "\n");
    await app["poll"]();
    expect(app.readMarks()).not.toBeNull();
    expect(app.removedComments()).toEqual([]);
    fixture.destroy();
  });
});

describe("updated since read", () => {
  it("compares stored fingerprints with the catalog and clears the hint when the opened content is unchanged", async () => {
    const { app, server, open, fixture } = setup();
    const stored = server.seed("design.md", design);
    server.baselines.set(baselineKey("s", "demo", "design.md"), stored);
    // The file was touched: the card sees a different write time, though the content is the same.
    server.summary = { [baselineKey("s", "demo", "design.md")]: "100:4999", [baselineKey("s", "demo", "tasks.md")]: "40:6000" };
    await app.loadReadSummary();
    expect(app.updatedDocs(change)).toEqual(["design.md"]);
    expect(app.updatedDocs(other)).toEqual([]);
    server.calls.length = 0;
    await open("design.md");
    expect(server.marks()).toEqual([["design.md", stored.revision]]);
    expect(app.updatedDocs(change)).toEqual([]);
    expect(app.readMarks()).toBeNull();
    fixture.destroy();
  });

  it("keeps the hint, and shows the differences, when the content really changed", async () => {
    const { app, server, open, fixture } = setup();
    const before = server.seed("design.md", design);
    server.baselines.set(baselineKey("s", "demo", "design.md"), before);
    server.edit("design.md", design.replace("Intro paragraph.", "Intro paragraph, rewritten."));
    server.summary = { [baselineKey("s", "demo", "design.md")]: "100:4999" };
    await app.loadReadSummary();
    server.calls.length = 0;
    await open("design.md");
    expect(server.marks()).toEqual([]);
    expect(app.updatedDocs(change)).toEqual(["design.md"]);
    expect(changedStarts(html(app))).toEqual(["2"]);
    fixture.destroy();
  });

  it("shows the chips on change cards", async () => {
    const { app, server, fixture } = setup();
    server.reviews = { items: [item(change, "design.md", 4, "One"), item(change, "tasks.md", 2, "Two"), item(archived, "design.md", 3, "Old")], unavailable: [] };
    server.summary = { [baselineKey("s", "demo", "design.md")]: "100:4999" };
    app.ready.set(true);
    app["activityScope"].set("[]");
    await app.refreshOverview();
    fixture.detectChanges();
    const cards = Array.from(fixture.nativeElement.querySelectorAll(".change-card")) as HTMLElement[];
    const demo = cards.find((c) => c.textContent!.includes("Demo"))!;
    expect(demo.querySelector(".comments-chip")!.textContent!.trim()).toBe("2 comments");
    expect(demo.querySelector(".updated-chip")!.textContent!.trim()).toBe("Updated since read");
    const second = cards.find((c) => c.textContent!.includes("Other"))!;
    expect(second.querySelector(".comments-chip")).toBeNull();
    expect(second.querySelector(".updated-chip")).toBeNull();
    fixture.destroy();
  });
});

describe("open review comments panel", () => {
  it("requests every scope for the selected repositories and groups the active ones by change", async () => {
    const { app, server, fixture } = setup();
    server.reviews = {
      items: [item(change, "design.md", 4, "First"), item(change, "design.md", 9, "Second"), item(other, "design.md", 2, "Third"), item(archived, "design.md", 3, "Left behind")],
      unavailable: ["demo/large.md: Document exceeds the 2 MiB read limit."],
    };
    app.selectedRepos.set(["r"]);
    app.ready.set(true);
    await app.loadReviews();
    expect(server.calls.find((c) => c.path === "/api/reviews")!.body).toEqual({ repositories: ["r"], scope: "all" });
    expect(app.reviewTotal()).toBe(3);
    expect(app.reviewGroups().map((g) => [g.change, g.count])).toEqual([["demo", 2], ["other", 1]]);
    expect(app.commentCount(change)).toBe(2);
    expect(app.documentCommentCount(change, "design.md")).toBe(2);
    expect(app.documentCommentCount(change, "tasks.md")).toBe(0);
    fixture.detectChanges();
    const panel = fixture.nativeElement.querySelector(".review-inbox") as HTMLElement;
    expect(panel.querySelector(".review-total")!.textContent).toContain("3 open");
    expect(panel.querySelectorAll(".review-group")).toHaveLength(2);
    expect(panel.querySelectorAll(".review-entry")).toHaveLength(3);
    expect(panel.querySelector(".source-notices")!.textContent).toContain("could not be read");
    // The switch is local: archived comments were already loaded and are listed read-only.
    app.inboxScope.set("archived");
    fixture.detectChanges();
    expect(panel.querySelectorAll(".review-group")).toHaveLength(1);
    expect(panel.querySelector(".review-group .badge")!.textContent).toContain("Archived");
    expect(panel.querySelector(".review-group")!.textContent).toContain("Left behind");
    expect(server.count("/api/reviews")).toBe(1);
    app.inboxScope.set("all");
    fixture.detectChanges();
    expect(panel.querySelectorAll(".review-group")).toHaveLength(3);
    fixture.destroy();
  });

  it("opens a listed comment at its note", async () => {
    const { app, server, navigate, fixture } = setup();
    server.reviews = { items: [item(change, "design.md", 9, "Second")], unavailable: [] };
    await app.loadReviews();
    await app.openReview(app.inbox()!.items[0]);
    expect(navigate.mock.calls.at(-1)![1].queryParams).toMatchObject({ source: "s", change: "demo", doc: "design.md", anchor: "note:9" });
    fixture.destroy();
  });

  it("scrolls the reader to the note of the listed comment", async () => {
    const { app, server, open, fixture } = setup();
    server.seed("design.md", design + "\n" + marker(idA, "Third paragraph.", "Say more.") + "\n");
    await open("design.md");
    fixture.detectChanges();
    const note = document.createElement("aside");
    note.dataset["reviewLine"] = "8";
    const root = document.createElement("article");
    root.className = "markdown";
    root.append(note);
    document.body.append(root);
    const scroll = vi.spyOn(note, "scrollIntoView");
    app.scrollTo("note:8");
    expect(scroll).toHaveBeenCalled();
    root.remove();
    fixture.destroy();
  });

  it("still resolves a heading whose slug starts with review-", () => {
    const { app, fixture } = setup();
    const root = document.createElement("article");
    root.className = "markdown";
    const heading = document.createElement("h2");
    heading.id = "review-after-editing";
    root.append(heading);
    document.body.append(root);
    const scroll = vi.spyOn(heading, "scrollIntoView");
    app.scrollTo("review-after-editing");
    expect(scroll).toHaveBeenCalled();
    expect(app["notice"]()).toBe("");
    root.remove();
    fixture.destroy();
  });

  it("refreshes the overview only when the catalog revision moves", async () => {
    const { app, server, fixture } = setup();
    app.ready.set(true);
    app["overviewRevision"] = server.catalog.revision;
    app["journalSnapshot"] = server.catalog.journalSnapshot;
    for (let i = 0; i < 5; i++) await app["poll"]();
    expect(server.count("/api/reviews")).toBe(0);
    expect(server.count("/api/reads/summary")).toBe(0);
    server.catalog = { ...server.catalog, revision: "2" };
    await app["poll"]();
    await vi.waitFor(() => expect(server.count("/api/reviews")).toBe(1));
    await app["poll"]();
    expect(server.count("/api/reviews")).toBe(1);
    expect(server.count("/api/reads/summary")).toBe(1);
    server.reviews = { items: [], unavailable: [] };
    fixture.destroy();
  });

  it("drops the comment from the overview after the agent removes its marker", async () => {
    const { app, server, fixture } = setup();
    server.reviews = { items: [item(change, "design.md", 4, "One"), item(change, "design.md", 9, "Two")], unavailable: [] };
    app.ready.set(true);
    app["overviewRevision"] = server.catalog.revision;
    app["journalSnapshot"] = server.catalog.journalSnapshot;
    await app.loadReviews();
    expect(app.commentCount(change)).toBe(2);
    server.reviews = { items: [item(change, "design.md", 9, "Two")], unavailable: [] };
    server.catalog = { ...server.catalog, revision: "2" };
    await app["poll"]();
    await vi.waitFor(() => expect(app.commentCount(change)).toBe(1));
    expect(app.reviewTotal()).toBe(1);
    fixture.destroy();
  });

  it("shows the open comments of the change in the reader", async () => {
    const { app, server, open, fixture } = setup();
    server.seed("design.md", design);
    server.reviews = { items: [item(change, "design.md", 4, "One"), item(change, "tasks.md", 2, "Two"), item(other, "design.md", 1, "Elsewhere")], unavailable: [] };
    await app.loadReviews();
    await open("design.md");
    expect(app.readerComments().map((i) => i.comment)).toEqual(["One", "Two"]);
    fixture.detectChanges();
    const menu = fixture.nativeElement.querySelector(".comment-menu") as HTMLElement;
    expect(menu.querySelector("summary")!.textContent!.replace(/\s+/g, " ")).toContain("2 open comments in this change · 1 in this document");
    fixture.destroy();
  });
});

describe("navigating between changes", () => {
  async function changed() {
    const context = setup();
    context.server.seed("design.md", design);
    await context.open("design.md");
    context.server.edit(
      "design.md",
      design.replace("Intro paragraph.", "Intro, rewritten.").replace("Third paragraph.", "Third, rewritten.") + "\nFourth, added.\n",
    );
    context.app.document.set(null);
    await context.open("design.md");
    context.fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 5));
    context.fixture.detectChanges();
    return context;
  }

  it("steps through the changes with the buttons and wraps around", async () => {
    const { app, fixture } = await changed();
    // The rewritten intro (with the passage it replaced) is one stop; the rewritten third paragraph and the added fourth form the other.
    const stops = app["stops"]();
    expect(stops).toHaveLength(2);
    expect(app.changeCount()).toBe(stops.length);
    expect(fixture.nativeElement.querySelector(".read-banner")!.textContent).toContain("Changed since last read");
    app.navigateChange(1);
    expect(app.changePosition()).toBe(1);
    app.navigateChange(1);
    expect(app.changePosition()).toBe(2);
    for (let i = 0; i < stops.length - 2; i++) app.navigateChange(1);
    expect(app.changePosition()).toBe(stops.length);
    app.navigateChange(1);
    expect(app.changePosition()).toBe(1);
    app.navigateChange(-1);
    expect(app.changePosition()).toBe(stops.length);
    fixture.destroy();
  });

  it("answers [ and ] unless the user is typing", async () => {
    const { app, fixture } = await changed();
    const press = (key: string, target: Element = document.body, init: KeyboardEventInit = {}) => {
      const event = new KeyboardEvent("keydown", { key, bubbles: true, cancelable: true, ...init });
      target.dispatchEvent(event);
      return event;
    };
    expect(press("]").defaultPrevented).toBe(true);
    expect(app.changePosition()).toBe(1);
    expect(press("]").defaultPrevented).toBe(true);
    expect(app.changePosition()).toBe(2);
    expect(press("[").defaultPrevented).toBe(true);
    expect(app.changePosition()).toBe(1);
    const field = document.createElement("textarea");
    document.body.append(field);
    expect(press("]", field).defaultPrevented).toBe(false);
    expect(app.changePosition()).toBe(1);
    expect(press("]", document.body, { ctrlKey: true }).defaultPrevented).toBe(false);
    expect(app.changePosition()).toBe(1);
    field.remove();
    fixture.destroy();
  });

  it("highlights the changed lines of the source view and offers the same stops", async () => {
    const { app, fixture } = await changed();
    app.setSourceView(true);
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 5));
    const marked = Array.from(fixture.nativeElement.querySelectorAll(".source-text .changed-since-read")) as HTMLElement[];
    expect(marked.length).toBeGreaterThanOrEqual(2);
    expect(marked.some((e) => e.textContent!.includes("Intro, rewritten."))).toBe(true);
    expect(fixture.nativeElement.querySelector(".source-text")!.textContent).toBe(app.document()!.text);
    expect(app.changeCount()).toBe(marked.length);
    app.navigateChange(1);
    expect(app.changePosition()).toBe(1);
    fixture.destroy();
  });

  it("uses a focusable document-level stop when a large comparison is incomplete, including an empty current document", async () => {
    const { app, server, open, fixture } = setup();
    const before = Array.from({ length: 2100 }, (_, index) => `baseline unique line ${index}`).join("\n") + "\n";
    const after = Array.from({ length: 2100 }, (_, index) => `replacement unique line ${index}`).join("\n") + "\n";
    server.seed("design.md", before);
    await open("design.md");

    server.edit("design.md", after);
    await app["poll"]();
    fixture.detectChanges();
    app["syncStops"]();
    expect(app.readMarks()?.incomplete).toBe(true);
    expect((fixture.nativeElement.querySelector(".markdown") as HTMLElement).classList.contains("changed-since-read")).toBe(true);
    expect(app.changeCount()).toBe(1);
    expect(app.changeStatus(app.readMarks()!)).toContain("Use Next change");
    app.navigateChange(1);
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector(".markdown"));

    app.setSourceView(true);
    fixture.detectChanges();
    app["syncStops"]();
    const source = fixture.nativeElement.querySelector(".source-text");
    expect(source.classList.contains("changed-since-read")).toBe(true);
    expect(source.getAttribute("tabindex")).toBe("-1");
    app.navigateChange(1);
    expect(document.activeElement).toBe(source);

    server.edit("design.md", "");
    await app["poll"]();
    app.setSourceView(false);
    fixture.detectChanges();
    app["syncStops"]();
    const empty = fixture.nativeElement.querySelector(".markdown");
    expect(app.readMarks()).toMatchObject({ changed: [], removed: [], incomplete: true });
    expect(empty.classList.contains("changed-since-read")).toBe(true);
    expect(empty.textContent).toBe("");
    expect(app.changeCount()).toBe(1);
    app.navigateChange(1);
    expect(document.activeElement).toBe(empty);
    fixture.destroy();
  });
});
