import { describe, expect, it } from "vitest";
import { RefreshQueue, RevisionSnapshot, RevisionTracker, revisionToken } from "./live-updates";

const snapshot = (patch: Partial<RevisionSnapshot> = {}): RevisionSnapshot => ({
  instance: "one",
  catalog: 1,
  documents: 1,
  journal: 1,
  digests: 1,
  reads: 1,
  dirty: [],
  ...patch,
});

describe("live revision tracking", () => {
  it("starts with every domain pending and resets ordering for a new server instance", () => {
    const tracker = new RevisionTracker();
    expect(tracker.accept(snapshot()).domains).toEqual(["catalog", "documents", "journal", "digests", "reads"]);
    tracker.markApplied("one", snapshot(), ["catalog", "documents", "journal", "digests", "reads"]);
    expect(tracker.pending()).toEqual([]);

    const restarted = snapshot({ instance: "two", catalog: 0, documents: 0, journal: 0, digests: 0, reads: 0 });
    expect(tracker.accept(restarted)).toMatchObject({ instanceChanged: true, domains: ["catalog", "documents", "journal", "digests", "reads"] });
    expect(tracker.pending()).toEqual(["catalog", "documents", "journal", "digests", "reads"]);
  });

  it("ignores duplicate and out-of-order vectors while advancing only newer domains", () => {
    const tracker = new RevisionTracker();
    tracker.accept(snapshot());
    tracker.markApplied("one", snapshot(), ["catalog", "documents", "journal", "digests", "reads"]);
    expect(tracker.accept(snapshot({ dirty: ["documents", "journal"] }))).toMatchObject({ changed: false, domains: [] });
    expect(tracker.accept(snapshot({ catalog: 0, documents: 0, journal: 3 }))).toMatchObject({ changed: true, domains: ["journal"] });
    expect(tracker.desired()?.journal).toBe(3);
    expect(tracker.desired()?.documents).toBe(1);
    expect(tracker.pending()).toEqual(["journal"]);
  });

  it("does not acknowledge a superseded target when the current instance advances during reconciliation", () => {
    const tracker = new RevisionTracker();
    const first = snapshot();
    tracker.accept(first);
    const next = snapshot({ journal: 2 });
    tracker.accept(next);
    tracker.markApplied("one", first, ["journal"]);
    expect(tracker.pending()).toEqual(["catalog", "documents", "journal", "digests", "reads"]);
    tracker.markApplied("one", next, ["catalog", "documents", "journal", "digests", "reads"]);
    expect(tracker.pending()).toEqual([]);
    tracker.accept(snapshot({ instance: "two", journal: 0 }));
    tracker.markApplied("one", next, ["journal"]);
    expect(tracker.pending()).toEqual(["catalog", "documents", "journal", "digests", "reads"]);
  });
});

describe("bounded refresh queue", () => {
  it("coalesces updates during an in-flight read into one latest follow-up", async () => {
    const queue = new RefreshQueue();
    const calls: string[] = [];
    let finish!: (value: boolean) => void;
    const first = queue.enqueue("timeline", "one", () => {
      calls.push("one");
      return new Promise<boolean>((resolve) => { finish = resolve; });
    });
    const duplicate = queue.enqueue("timeline", "two", async () => { calls.push("two"); return true; });
    queue.enqueue("timeline", "three", async () => { calls.push("three"); return true; });
    finish(true);
    await Promise.all([first, duplicate]);
    expect(calls).toEqual(["one", "three"]);
    expect(queue.dirty("timeline")).toBe(false);
    expect(await queue.enqueue("timeline", "three", async () => { calls.push("duplicate"); return true; })).toBe(true);
    expect(calls).toEqual(["one", "three"]);
  });

  it("retains a failed job until explicit retry and retries only the latest token", async () => {
    const queue = new RefreshQueue();
    let calls = 0;
    expect(await queue.enqueue("reader", "r1", async () => { calls++; return false; })).toBe(false);
    expect(queue.dirty("reader")).toBe(true);
    expect(await queue.enqueue("reader", "r1", async () => { calls++; return true; })).toBe(false);
    expect(await queue.retry("reader")).toBe(false);
    expect(calls).toBe(2);
    expect(queue.dirty("reader")).toBe(true);

    const result = await queue.enqueue("reader", "r2", async () => { calls++; return true; });
    expect(result).toBe(true);
    expect(calls).toBe(3);
    expect(queue.dirty("reader")).toBe(false);
  });

  it("cancels a queued follow-up when the queue is cleared during an in-flight read", async () => {
    const queue = new RefreshQueue();
    let finishFirst!: (value: boolean) => void;
    let followUps = 0;
    const first = queue.enqueue("reader", "first", () => new Promise<boolean>((resolve) => { finishFirst = resolve; }));
    queue.enqueue("reader", "second", async () => { followUps++; return true; });
    queue.clear();
    finishFirst(true);
    expect(await first).toBe(false);
    expect(followUps).toBe(0);
    expect(queue.dirty("reader")).toBe(false);
    expect(await queue.enqueue("reader", "fresh", async () => true)).toBe(true);
  });

  it("builds stable tokens only from the domains a resource depends on", () => {
    expect(revisionToken(snapshot(), ["journal", "digests"])).toBe("one:1:1");
    expect(revisionToken(snapshot({ documents: 5 }), ["journal", "digests"])).toBe("one:1:1");
  });
});
