import { TestBed } from "@angular/core/testing";
import { Router } from "@angular/router";
import { Subject } from "rxjs";
import { describe, it, expect, vi, afterEach } from "vitest";
import { App } from "./app";
import type { Catalog, Change, DocumentContent } from "./models";

function archived(name: string, archivedOn: string | null): Change {
  return {
    sourceId: "s",
    name,
    directoryName: archivedOn ? archivedOn + "-" + name : name,
    variant: "v-" + name,
    archived: true,
    archivedOn,
    repositories: ["r"],
    documents: [{ path: "design.md", label: "design", fingerprint: "1:1" }],
    completedTasks: null,
    totalTasks: null,
    error: null,
  };
}
const document_: DocumentContent = {
  sourceId: "s", change: "old", path: "design.md", variant: "v-old", location: "project",
  revision: "r1", text: "# Design\n\nBody", archived: true, canComment: false, tasks: [],
};

function setup() {
  const navigate = vi.fn().mockResolvedValue(true);
  TestBed.configureTestingModule({
    imports: [App],
    providers: [{ provide: Router, useValue: { events: new Subject(), navigate, parseUrl: () => ({ queryParams: {} }), url: "/archive" } }],
  });
  const fixture = TestBed.createComponent(App);
  const app = fixture.componentInstance;
  app.catalog.set({
    repositories: [{ id: "r", label: "Project" }, { id: "x", label: "Other" }],
    sources: [],
    changes: [archived("old", "2026-10-01"), archived("newer", "2026-10-09"), archived("undated", null),
      { ...archived("elsewhere", "2026-10-20"), repositories: ["x"] }],
    launchRepository: "r",
    revision: "1",
    notices: [],
    journalSnapshot: 0,
  } as Catalog);
  return { app, fixture, navigate };
}
afterEach(() => {
  vi.restoreAllMocks();
  document.body.querySelectorAll(".reader").forEach((e) => e.remove());
  TestBed.resetTestingModule();
});

describe("Archive view and Open in editor", () => {
  it("lists archived changes of the selected repositories newest first and applies the name filter", () => {
    const { app, fixture } = setup();
    app.selectedRepos.set(["r"]);
    expect(app.archiveList().map((e) => e.change.name)).toEqual(["newer", "old", "undated"]);
    app.archiveFilter.set("OL");
    expect(app.archiveList().map((e) => e.change.name)).toEqual(["old"]);
    app.selectedRepos.set(null);
    app.archiveFilter.set("");
    expect(app.archiveList().map((e) => e.change.name)).toEqual(["elsewhere", "newer", "old", "undated"]);
    fixture.destroy();
  });

  it("posts only the document identity, line and preset, and reports a launch failure", async () => {
    const { app, fixture } = setup();
    const calls: { path: string; body: unknown }[] = [];
    vi.spyOn(globalThis, "fetch").mockImplementation(async (path, options) => {
      calls.push({ path: String(path), body: JSON.parse(options!.body as string) });
      return calls.length === 1 ? new Response(null, { status: 204 }) : Response.json({ error: "Could not open Cursor. Choose another editor." }, { status: 503 });
    });
    await app.openInEditor({ sourceId: "s", change: "old", path: "design.md", variant: "v-old" }, 11);
    expect(calls[0]).toEqual({
      path: "/api/open",
      body: { document: { sourceId: "s", change: "old", path: "design.md", variant: "v-old" }, line: 11, editor: "vscode" },
    });
    expect(app.notice()).toBe("");
    app.editor.set("cursor");
    await app.openInEditor({ sourceId: "s", change: "old", path: "design.md" }, 0);
    expect((calls[1].body as { editor: string }).editor).toBe("cursor");
    expect(app.notice()).toBe("Could not open Cursor. Choose another editor.");
    fixture.destroy();
  });

  it("remembers the editor preset in the preferences and ignores unknown values", () => {
    const { app, fixture } = setup();
    app.setEditor("system");
    expect(app.editor()).toBe("system");
    expect(app.preferences.editor).toBe("system");
    app.setEditor("notepad");
    expect(app.editor()).toBe("vscode");
    fixture.destroy();
  });

  it("opens the reader's document at the first block at or below the top edge, in read and source views", async () => {
    const { app, fixture } = setup();
    app.document.set(document_);
    const reader = window.document.createElement("section");
    reader.className = "reader";
    reader.innerHTML = '<div class="document-scroll"><article class="markdown"><h1 data-start="0">A</h1><p data-start="2">B</p><p data-start="6">C</p></article><pre class="source-text">x</pre></div>';
    window.document.body.append(reader);
    const scroller = reader.querySelector<HTMLElement>(".document-scroll")!;
    scroller.getBoundingClientRect = () => ({ top: 100 }) as DOMRect;
    const tops = [-50, 90, 140];
    reader.querySelectorAll<HTMLElement>("[data-start]").forEach((e, i) => (e.getBoundingClientRect = () => ({ top: tops[i] }) as DOMRect));
    expect(app.readerLine()).toBe(6);
    tops[1] = 100;
    expect(app.readerLine()).toBe(2);
    const bodies: { document: { variant: string }; line: number }[] = [];
    vi.spyOn(globalThis, "fetch").mockImplementation(async (_path, options) => {
      bodies.push(JSON.parse(options!.body as string));
      return new Response(null, { status: 204 });
    });
    await app.openCurrentInEditor();
    await app.openCurrentInEditor(4);
    expect(bodies.map((b) => [b.document.variant, b.line])).toEqual([["v-old", 2], ["v-old", 4]]);
    app.sourceView.set(true);
    scroller.scrollTop = 0;
    expect(app.readerLine()).toBe(0);
    // The text starts above the top edge and wraps: the line is the one holding the character at the top edge.
    const pre = reader.querySelector<HTMLElement>(".source-text")!;
    pre.textContent = "# A\n\nvery long wrapped paragraph\nnext";
    pre.getBoundingClientRect = () => ({ top: -300, left: 0 }) as DOMRect;
    scroller.getBoundingClientRect = () => ({ top: 100, left: 0, right: 500 }) as DOMRect;
    scroller.scrollTop = 0;
    const node = pre.firstChild!;
    const caret = vi.fn(() => ({ startContainer: node, startOffset: 20 }) as unknown as Range);
    Object.defineProperty(window.document, "caretRangeFromPoint", { value: caret, configurable: true });
    Object.defineProperty(window.document, "caretPositionFromPoint", { value: undefined, configurable: true });
    expect(app.readerLine()).toBe(2);
    expect(caret).toHaveBeenCalled();
    // Text still below the top edge: the document's first line.
    pre.getBoundingClientRect = () => ({ top: 150, left: 0 }) as DOMRect;
    expect(app.readerLine()).toBe(0);
    fixture.destroy();
  });
});
