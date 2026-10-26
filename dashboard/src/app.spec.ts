import { TestBed } from "@angular/core/testing";
import { Router } from "@angular/router";
import { Subject } from "rxjs";
import { describe, it, expect, vi, afterEach } from "vitest";
import { App } from "./app";
import { Catalog, Change } from "./models";
import { DocumentContent } from "./models";
import { renderMarkdown } from "./markdown";
const change: Change = {
  sourceId: "s",
  name: "demo",
  directoryName: "demo",
  variant: "v",
  archived: false,
  repositories: ["r"],
  documents: [
    { path: "design.md", label: "design" },
    { path: "tasks.md", label: "tasks" },
  ],
  completedTasks: 2,
  totalTasks: 7,
  error: null,
};
function setup() {
  const navigate = vi.fn().mockResolvedValue(true);
  const events = new Subject();
  TestBed.configureTestingModule({
    imports: [App],
    providers: [
      {
        provide: Router,
        useValue: {
          events,
          navigate,
          parseUrl: () => ({ queryParams: {} }),
          url: "/ongoing",
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(App);
  const app = fixture.componentInstance;
  app.catalog.set({
    repositories: [{ id: "r", label: "Project" }],
    sources: [],
    changes: [change],
    launchRepository: "r",
    revision: "1",
    notices: [],
  } as Catalog);
  return { app, navigate, fixture };
}
afterEach(() => {
  vi.restoreAllMocks();
  TestBed.resetTestingModule();
});
describe("Angular workspace behavior", () => {
  it("retains an existing-note edit across document navigation and a conflict reload", async () => {
    const { app, fixture } = setup();
    const text = 'Passage\n\n<!-- REVIEW: id=123456781234123412341234567890ab\nTarget: preceding passage\nQuote: Passage\nComment:\nOriginal note\n-->';
    const design: DocumentContent = {
      sourceId: "s", change: "demo", path: "design.md", location: "project",
      revision: "before", text, archived: false, canComment: true, tasks: [],
    };
    app.document.set(design);
    const review = renderMarkdown(text, true).reviews[0];
    app.editReview(review);
    app.comment.set("Keep this unsaved edit");
    const tasks = { ...design, path: "tasks.md", text: "# Tasks" };
    const changed = { ...design, revision: "after", text: text.replace("Original note", "Agent edited note") };
    vi.spyOn(globalThis, "fetch").mockImplementation(async (path, options) => {
      if (path === "/api/comments/edit") {
        const request = JSON.parse(options!.body as string);
        expect(request.markerText).toBe(review.marker);
        expect(request.comment).toBe("Keep this unsaved edit");
        return Response.json({ error: "This review comment changed." }, { status: 409 });
      }
      if (path === "/api/document") return Response.json(JSON.parse(options!.body as string).path === "tasks.md" ? tasks : changed);
      throw new Error("Unexpected request: " + path);
    });
    await app["loadDocument"](tasks);
    await app["loadDocument"](design);
    expect(app.comment()).toBe("Keep this unsaved edit");
    expect(app.editingReview()?.marker).toBe(review.marker);
    await app.saveComment();
    expect(app.comment()).toBe("Keep this unsaved edit");
    expect(app.commentStatus()).toContain("changed");
    expect(app.editingReview()?.marker).toBe(review.marker);
    app.cancelReviewEdit();
    expect(app.comment()).toBe("");
    expect(app.editingReview()).toBeNull();
    fixture.destroy();
  });
  it("keeps a document switch when an earlier catalog poll finishes during loading", async () => {
    const { app, fixture } = setup();
    const design: DocumentContent = {
      sourceId: "s", change: "demo", path: "design.md", location: "project",
      revision: "design", text: "# Design", archived: false, canComment: true, tasks: [],
    };
    const proposal = { ...design, path: "proposal.md", revision: "proposal", text: "# Proposal" };
    app.document.set(design);
    vi.spyOn(document, "hidden", "get").mockReturnValue(false);
    let finishCatalog!: (value: Response) => void;
    let finishProposal!: (value: Response) => void;
    const catalog = new Promise<Response>((resolve) => finishCatalog = resolve);
    const documentRequest = new Promise<Response>((resolve) => finishProposal = resolve);
    vi.spyOn(globalThis, "fetch").mockImplementation((path, options) => {
      if (path === "/api/catalog") return catalog;
      if (path === "/api/document")
        return JSON.parse(options!.body as string).path === "proposal.md"
          ? documentRequest : Promise.resolve(Response.json(design));
      throw new Error("Unexpected request: " + path);
    });
    const poll = app["poll"]();
    const navigation = app["loadDocument"](proposal);
    finishCatalog(Response.json(app.catalog()!));
    await poll;
    finishProposal(Response.json(proposal));
    await navigation;
    expect(app.document()?.path).toBe("proposal.md");
    expect(app.documentLoading()).toBe(false);
    fixture.destroy();
  });
  it("defaults to design and honors the remembered task document", async () => {
    const { app, navigate, fixture } = setup();
    await app.openChange(change);
    expect(navigate.mock.calls[0][1].queryParams.doc).toBe("design.md");
    app.preferences.documents = { "s:demo": "tasks.md" };
    await app.openChange(change);
    expect(navigate.mock.calls[1][1].queryParams.doc).toBe("tasks.md");
    fixture.destroy();
  });
  it("scopes active changes without manufacturing journal activity", () => {
    const { app, fixture } = setup();
    expect(app.activeChanges()).toHaveLength(1);
    expect(app.changeRow(change)).toBeUndefined();
    app.selectedRepos.set(["other"]);
    expect(app.activeChanges()).toHaveLength(0);
    fixture.destroy();
  });
});
