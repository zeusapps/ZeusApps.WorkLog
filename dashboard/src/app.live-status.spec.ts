import { TestBed } from "@angular/core/testing";
import { Router } from "@angular/router";
import { Subject } from "rxjs";
import { describe, it, expect, vi, afterEach } from "vitest";
import { App, reconnectingGraceMs } from "./app";
import { Catalog } from "./models";

const catalog = {
  repositories: [{ id: "r", label: "Project" }],
  sources: [],
  changes: [],
  launchRepository: "r",
  revision: "1",
  notices: [],
  journalSnapshot: 5,
} as Catalog;

function setup() {
  vi.spyOn(App.prototype, "ngAfterViewInit").mockImplementation(() => {});
  vi.spyOn(document, "hidden", "get").mockReturnValue(false);
  TestBed.configureTestingModule({
    imports: [App],
    providers: [
      {
        provide: Router,
        useValue: {
          events: new Subject(),
          navigate: vi.fn().mockResolvedValue(true),
          parseUrl: () => ({ queryParams: {}, root: { children: {} } }),
          url: "/ongoing",
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(App);
  const app = fixture.componentInstance;
  app.catalog.set(catalog);
  app.ready.set(true);
  return { app, fixture };
}
function stubLoaders(app: App) {
  vi.spyOn(app, "loadOngoing").mockResolvedValue(true);
  vi.spyOn(app, "loadActivity").mockResolvedValue(true);
  vi.spyOn(app, "loadHistory").mockResolvedValue(true);
  vi.spyOn(app, "loadReviews").mockResolvedValue(true as never);
  vi.spyOn(app, "loadReadSummary").mockResolvedValue(true as never);
  vi.spyOn(app, "search").mockResolvedValue(true as never);
  vi.spyOn(app as any, "refreshCatalog").mockResolvedValue(true);
}
function fakeLive(app: App) {
  return {
    stop: async () => {},
    markReconciling: () => app.setLiveStatus("reconnecting"),
    markReconciled: () => app.setLiveStatus("live"),
    markDeferred: () => app.setLiveStatus("connecting"),
    markPaused: () => app.setLiveStatus("paused"),
  } as never;
}
const readerDocument = {
  sourceId: "s", change: "demo", path: "design.md", location: "project", revision: "r1", text: "# Design", archived: false, canComment: true, tasks: [],
} as never;
const el = (fixture: { nativeElement: HTMLElement }, selector: string) =>
  fixture.nativeElement.querySelector(selector) as HTMLElement | null;

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
  TestBed.resetTestingModule();
});

describe("first launch before the first live revision (ORCH-023)", () => {
  const loaders = {
    ongoing: ["loadOngoing", "loadActivity"],
    history: ["loadHistory"],
    archive: ["loadActivity"],
    daily: ["loadOngoing", "loadActivity", "loadDaily"],
  } as const;
  for (const view of ["ongoing", "history", "archive", "daily"] as const) {
    it(`loads the ${view} view on activation without waiting for a live revision`, async () => {
      const { app, fixture } = setup();
      app.view.set(view);
      app["dataLoaded"] = true;
      const markReconciled = vi.fn();
      app["liveConnection"] = { stop: async () => {}, markReconciled, markPaused: vi.fn() } as never;
      const spies = (["loadOngoing", "loadActivity", "loadHistory", "loadDaily"] as const).map(
        (name) => [name, vi.spyOn(app, name).mockResolvedValue(true)] as const,
      );
      expect(app["revisionTracker"].desired()).toBeNull();

      await app["activateLiveScope"]();

      for (const [name, spy] of spies)
        expect(spy.mock.calls.length > 0, `${name} for ${view}`).toBe(
          (loaders[view] as readonly string[]).includes(name),
        );
      // Live is only claimed by a reconciled live revision, never by this direct load.
      expect(markReconciled).not.toHaveBeenCalled();
      expect(app.liveStatus()).toBe("connecting");
      fixture.destroy();
    });
  }

  it("shows Live only after the first revision has reconciled the visible data", async () => {
    const { app, fixture } = setup();
    app["dataLoaded"] = true;
    app["liveConnection"] = {
      stop: async () => {},
      markReconciling: () => app.setLiveStatus("reconnecting"),
      markReconciled: () => app.setLiveStatus("live"),
      markPaused: () => app.setLiveStatus("paused"),
    } as never;
    let finishOngoing!: (value: boolean) => void;
    const ongoing = vi
      .spyOn(app, "loadOngoing")
      .mockImplementation(() => new Promise<boolean>((resolve) => { finishOngoing = resolve; }));
    vi.spyOn(app, "loadActivity").mockResolvedValue(true);
    vi.spyOn(app, "loadReviews").mockResolvedValue(true as never);
    vi.spyOn(app, "loadReadSummary").mockResolvedValue(true as never);
    vi.spyOn(app, "search").mockResolvedValue(true as never);
    vi.spyOn(app as any, "refreshCatalog").mockResolvedValue(true);

    const activation = app["activateLiveScope"]();
    await vi.waitFor(() => expect(ongoing).toHaveBeenCalledTimes(1));
    expect(app.liveStatus()).toBe("connecting");
    finishOngoing(true);
    await activation;
    expect(app.liveStatus()).toBe("connecting");

    const revision = app["receiveRevision"]({
      instance: "i", catalog: 1, documents: 1, journal: 5, digests: 1, reads: 1, dirty: [],
    });
    await vi.waitFor(() => expect(ongoing).toHaveBeenCalledTimes(2));
    expect(app.liveStatus()).toBe("reconnecting");
    finishOngoing(true);
    await revision;
    expect(app.liveStatus()).toBe("live");
    fixture.destroy();
  });
});

describe("connecting and hidden-tab states (DEV-044, DEV-045)", () => {
  it("starts in a neutral connecting state with no bar, even after the grace period", async () => {
    vi.useFakeTimers();
    const { app, fixture } = setup();
    expect(app.liveStatus()).toBe("connecting");
    fixture.detectChanges();
    await vi.advanceTimersByTimeAsync(reconnectingGraceMs * 3);
    fixture.detectChanges();
    expect(el(fixture, ".live-status")).toBeNull();
    const indicator = el(fixture, ".live-indicator")!;
    expect(indicator.dataset["state"]).toBe("connecting");
    expect(indicator.getAttribute("title")).toContain("Connecting");
    expect(el(fixture, "[data-testid=live-announcer]")!.textContent!.trim()).toBe("");
    fixture.destroy();
  });

  it("keeps a first snapshot that arrives while the tab is hidden pending without pausing, then reconciles to Live", async () => {
    const { app, fixture } = setup();
    vi.spyOn(document, "hidden", "get").mockReturnValue(true);
    const live = {
      stop: async () => {},
      markReconciling: vi.fn(),
      markReconciled: () => app.setLiveStatus("live"),
      markDeferred: vi.fn(),
      markPaused: vi.fn(() => app.setLiveStatus("paused")),
    };
    app["liveConnection"] = live as never;
    const snapshot = { instance: "i", catalog: 1, documents: 1, journal: 5, digests: 1, reads: 1, dirty: [] };
    const success = await app["applyRevisionSnapshot"](snapshot, false);
    expect(success).toBe(false);
    expect(app["deferredWhileHidden"]).toBe(true);
    expect(app["revisionTracker"].pending().length).toBeGreaterThan(0);
    // The same revision again (a checkpoint) while still hidden is also only deferred.
    expect(await app["applyRevisionSnapshot"](snapshot, false)).toBe(false);
    expect(app["deferredWhileHidden"]).toBe(true);
    await app["receiveRevision"]({ ...snapshot, journal: 6 });
    expect(live.markPaused).not.toHaveBeenCalled();
    expect(live.markReconciling).not.toHaveBeenCalled();
    expect(app.liveStatus()).toBe("connecting");

    // The tab becomes visible: the pending work reconciles and Live is shown.
    vi.spyOn(document, "hidden", "get").mockReturnValue(false);
    stubLoaders(app);
    expect(await app["applyRevisionSnapshot"]({ ...snapshot, journal: 6 }, false)).toBe(true);
    expect(app.liveStatus()).toBe("live");
    fixture.destroy();
  });

  it("is not Live while a newer revision waits for a hidden tab, and reconciles after it becomes visible (ANALYSIS-781)", async () => {
    const { app, fixture } = setup();
    stubLoaders(app);
    app["liveConnection"] = fakeLive(app);
    const first = { instance: "i", catalog: 1, documents: 1, journal: 5, digests: 1, reads: 1, dirty: [] };
    await app["receiveRevision"](first);
    expect(app.liveStatus()).toBe("live");

    const hidden = vi.spyOn(document, "hidden", "get").mockReturnValue(true);
    await app["receiveRevision"]({ ...first, journal: 6 });
    expect(app.liveStatus()).toBe("connecting");
    expect(app["revisionTracker"].pending()).toContain("journal");

    hidden.mockReturnValue(false);
    await app["receiveRevision"]({ ...first, journal: 6 });
    expect(app.liveStatus()).toBe("live");
    expect(app["revisionTracker"].pending()).toEqual([]);
    fixture.destroy();
  });

  it("does not pause when window focus resumes before the visibility change (ANALYSIS-781)", async () => {
    const { app, fixture } = setup();
    stubLoaders(app);
    app["liveConnection"] = fakeLive(app);
    const snapshot = { instance: "i", catalog: 1, documents: 1, journal: 5, digests: 1, reads: 1, dirty: [] };
    const hidden = vi.spyOn(document, "hidden", "get").mockReturnValue(true);
    expect(await app["applyRevisionSnapshot"](snapshot, false)).toBe(false);
    hidden.mockReturnValue(false);
    // Focus arrives first: the apply is not marked forced by visibilitychange yet.
    expect(await app["applyRevisionSnapshot"](snapshot, false)).toBe(true);
    expect(app.liveStatus()).toBe("live");
    fixture.destroy();
  });
});

describe("reader status follows the grace period (ANALYSIS-782, QA-650)", () => {
  it("never shows or announces Reconnecting in the open reader for a reconcile shorter than the grace period", async () => {
    vi.useFakeTimers();
    const { app, fixture } = setup();
    app.document.set(readerDocument);
    app.setLiveStatus("live");
    fixture.detectChanges();
    const strip = () => el(fixture, "[data-testid=reader-live-status]")!;
    const dot = () => el(fixture, ".reader-meta .live-dot")!;
    expect(strip().dataset["state"]).toBe("live");
    app.setLiveStatus("reconnecting");
    fixture.detectChanges();
    await vi.advanceTimersByTimeAsync(reconnectingGraceMs - 200);
    fixture.detectChanges();
    expect(strip().dataset["state"]).toBe("live");
    expect(strip().textContent).not.toContain("Reconnecting");
    expect(dot().dataset["state"]).toBe("live");
    app.setLiveStatus("live");
    fixture.detectChanges();
    expect(strip().textContent).not.toContain("Reconnecting");
    // Paused stays immediate, with Retry.
    app.setLiveStatus("paused");
    fixture.detectChanges();
    expect(strip().dataset["state"]).toBe("paused");
    expect(strip().querySelector("button")).not.toBeNull();
    // A Reconnecting that outlasts the grace period is shown.
    app.setLiveStatus("live");
    fixture.detectChanges();
    app.setLiveStatus("reconnecting");
    fixture.detectChanges();
    await vi.advanceTimersByTimeAsync(reconnectingGraceMs + 50);
    fixture.detectChanges();
    expect(strip().dataset["state"]).toBe("reconnecting");
    fixture.destroy();
  });

  it("shows the connecting state on the reader dot and strip, not Live", () => {
    const { app, fixture } = setup();
    app.document.set(readerDocument);
    fixture.detectChanges();
    expect(el(fixture, ".reader-meta .live-dot")!.dataset["state"]).toBe("connecting");
    expect(el(fixture, "[data-testid=reader-live-status]")!.textContent).toContain("Connecting");
    fixture.destroy();
  });
});

describe("presented status details", () => {
  it("shows Reconnecting on the header only after the grace period", async () => {
    vi.useFakeTimers();
    const { app, fixture } = setup();
    app.setLiveStatus("live");
    fixture.detectChanges();
    app.setLiveStatus("reconnecting");
    fixture.detectChanges();
    await vi.advanceTimersByTimeAsync(reconnectingGraceMs + 50);
    fixture.detectChanges();
    expect(el(fixture, ".live-indicator")!.dataset["state"]).toBe("reconnecting");
    expect(el(fixture, ".live-indicator")!.getAttribute("title")).toMatch(/^Reconnecting/);
    fixture.destroy();
  });

  it("shows paused, not Live, when paused and reconnecting happen with no rendering between (ANALYSIS-800)", () => {
    const { app, fixture } = setup();
    app.document.set(readerDocument);
    app.setLiveStatus("live");
    fixture.detectChanges();
    app.setLiveStatus("paused");
    app.setLiveStatus("reconnecting");
    fixture.detectChanges();
    expect(el(fixture, "[data-testid=reader-live-status]")!.dataset["state"]).toBe("paused");
    expect(el(fixture, ".reader-meta .live-dot")!.dataset["state"]).toBe("paused");
    fixture.destroy();
  });

  it("retries a failed refresh on the first visible apply after a deferral, whichever event comes first", async () => {
    const { app, fixture } = setup();
    stubLoaders(app);
    app["liveConnection"] = fakeLive(app);
    let fail = true;
    vi.spyOn(app, "loadOngoing").mockImplementation(async () => !fail);
    const snapshot = { instance: "i", catalog: 1, documents: 1, journal: 5, digests: 1, reads: 1, dirty: [] };
    expect(await app["applyRevisionSnapshot"](snapshot, false)).toBe(false);
    const hidden = vi.spyOn(document, "hidden", "get").mockReturnValue(true);
    expect(await app["applyRevisionSnapshot"](snapshot, false)).toBe(false);
    expect(app["deferredWhileHidden"]).toBe(true);
    hidden.mockReturnValue(false);
    fail = false;
    expect(await app["applyRevisionSnapshot"](snapshot, false)).toBe(true);
    expect(app["revisionTracker"].pending()).toEqual([]);
    fixture.destroy();
  });
});

describe("live status presentation", () => {
  it("shows no page-level bar while Live and exposes the state on the header indicator", () => {
    const { app, fixture } = setup();
    app.setLiveStatus("live");
    app.lastSuccessfulRefresh.set("2026-10-27T10:00:00.000Z");
    fixture.detectChanges();
    expect(el(fixture, ".live-status")).toBeNull();
    const indicator = el(fixture, ".live-indicator")!;
    expect(indicator.dataset["state"]).toBe("live");
    expect(indicator.getAttribute("title")).toContain("Live");
    expect(indicator.getAttribute("title")).toContain("Last successful refresh");
    expect(indicator.getAttribute("aria-label")).toContain("Live");
    expect(indicator.getAttribute("aria-label")).toContain("Last successful refresh");
    expect(indicator.tabIndex).toBe(0);
    fixture.destroy();
  });

  it("does not show a Reconnecting bar for a reconciliation shorter than the grace period", async () => {
    vi.useFakeTimers();
    const { app, fixture } = setup();
    app.setLiveStatus("live");
    fixture.detectChanges();
    app.setLiveStatus("reconnecting");
    fixture.detectChanges();
    await vi.advanceTimersByTimeAsync(reconnectingGraceMs - 100);
    fixture.detectChanges();
    expect(el(fixture, ".live-status")).toBeNull();
    // The header holds Live too: Reconnecting shows only after the grace period (ANALYSIS-801).
    expect(el(fixture, ".live-indicator")!.dataset["state"]).toBe("live");
    expect(el(fixture, ".live-indicator")!.getAttribute("title")).toMatch(/^Live/);
    expect(el(fixture, ".live-indicator")!.getAttribute("aria-label")).toContain("Live");
    app.setLiveStatus("live");
    fixture.detectChanges();
    await vi.advanceTimersByTimeAsync(reconnectingGraceMs * 2);
    fixture.detectChanges();
    expect(el(fixture, ".live-status")).toBeNull();
    expect(el(fixture, ".live-indicator")!.dataset["state"]).toBe("live");
    expect(el(fixture, "[data-testid=live-announcer]")!.textContent!.trim()).toBe("");
    fixture.destroy();
  });

  it("shows the Reconnecting bar once the grace period has passed and removes it when Live returns", async () => {
    vi.useFakeTimers();
    const { app, fixture } = setup();
    app.setLiveStatus("reconnecting");
    fixture.detectChanges();
    await vi.advanceTimersByTimeAsync(reconnectingGraceMs + 50);
    fixture.detectChanges();
    const bar = el(fixture, ".live-status")!;
    expect(bar.dataset["state"]).toBe("reconnecting");
    expect(bar.textContent).toContain("Reconnecting");
    expect(bar.querySelector("button")).toBeNull();
    expect(el(fixture, "[data-testid=live-announcer]")!.textContent).toContain("Reconnecting");
    app.setLiveStatus("live");
    fixture.detectChanges();
    expect(el(fixture, ".live-status")).toBeNull();
    expect(el(fixture, "[data-testid=live-announcer]")!.textContent).toContain("Live");
    fixture.destroy();
  });

  it("shows the paused bar with Retry immediately and announces it politely", () => {
    const { app, fixture } = setup();
    app.setLiveStatus("paused");
    app.lastSuccessfulRefresh.set("2026-10-27T10:00:00.000Z");
    fixture.detectChanges();
    const bar = el(fixture, ".live-status")!;
    expect(bar.dataset["state"]).toBe("paused");
    expect(bar.textContent).toContain("Updates paused");
    expect(bar.textContent).toContain("Last successful refresh");
    expect(bar.querySelector("button")?.textContent).toContain("Retry updates");
    const announcer = el(fixture, "[data-testid=live-announcer]")!;
    expect(announcer.getAttribute("role")).toBe("status");
    expect(announcer.getAttribute("aria-live")).toBe("polite");
    expect(announcer.textContent).toContain("Updates paused");
    expect(el(fixture, ".live-indicator")!.dataset["state"]).toBe("paused");
    fixture.destroy();
  });
});

describe("keyboard focus during Retry updates (ANALYSIS-705)", () => {
  afterEach(() => {
    document.body.replaceChildren();
  });
  function paused(withReader: boolean) {
    const { app, fixture } = setup();
    document.body.appendChild(fixture.nativeElement);
    app["liveConnection"] = { retry: () => new Promise<boolean>(() => {}), stop: async () => {}, markPaused: () => {} } as never;
    if (withReader) app.document.set(readerDocument);
    app.setLiveStatus("paused");
    fixture.detectChanges();
    return { app, fixture };
  }
  const retryButton = (fixture: { nativeElement: HTMLElement }, scope: string) =>
    fixture.nativeElement.querySelector(`${scope} button`) as HTMLButtonElement;

  it("keeps focus in the reader when the reader Retry is activated while focused", () => {
    const { app, fixture } = paused(true);
    const button = retryButton(fixture, "[data-testid=reader-live-status]");
    button.focus();
    expect(document.activeElement).toBe(button);
    button.click();
    // Synchronous: before any recovery is awaited, and before the button is disabled or removed.
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector(".reader"));
    fixture.detectChanges();
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector(".reader"));
    expect(app.liveRetrying()).toBe(true);
    fixture.destroy();
  });

  it("moves focus to a focusable page element when the page Retry is activated while focused", () => {
    const { app, fixture } = paused(false);
    const button = retryButton(fixture, ".live-status");
    button.focus();
    expect(document.activeElement).toBe(button);
    button.click();
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector("[data-testid=live-indicator]"));
    fixture.detectChanges();
    expect(button.disabled).toBe(true);
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector("[data-testid=live-indicator]"));
    expect(app.liveRetrying()).toBe(true);
    fixture.destroy();
  });

  for (const withReader of [true, false]) {
    it(`leaves focus in a draft when Retry is triggered by pointer (${withReader ? "reader" : "page"})`, () => {
      const { fixture } = paused(withReader);
      const draft = document.createElement("textarea");
      fixture.nativeElement.appendChild(draft);
      draft.focus();
      retryButton(fixture, withReader ? "[data-testid=reader-live-status]" : ".live-status").click();
      expect(document.activeElement).toBe(draft);
      fixture.detectChanges();
      expect(document.activeElement).toBe(draft);
      fixture.destroy();
    });
  }
});
