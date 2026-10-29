import { describe, it, expect } from "vitest";
import { documentLimit, positionLimit } from "./app";
import {
  BoardCard,
  BoardInput,
  buildBoard,
  byPriority,
  changeKey,
  dropWithin,
  filterBoard,
  fitOrder,
  isQuiet,
  matchesFilter,
  moveWithin,
  orderLimit,
  pruneOrder,
  rankColumn,
} from "./board";
import type { Change, ChangeActivity, Entry, Row, Ticket } from "./models";

const now = new Date("2026-10-28T12:00:00Z");
const daysAgo = (days: number) => new Date(now.getTime() - days * 86400000).toISOString();
const repo = "github.com/acme/api";
const dep12: Ticket = { provider: "jira", scope: "company", id: "DEP-12" };

const change = (name: string, completed: number | null = 0, total: number | null = 9, extra: Partial<Change> = {}): Change => ({
  sourceId: "s", name, directoryName: name, variant: "v-" + name, archived: false, archivedOn: null, repositories: [repo],
  documents: [], completedTasks: completed, totalTasks: total, error: null, ...extra,
});
const archived = (name: string) => change(name, 3, 3, { archived: true, archivedOn: "2026-10-01" });
const activity = (name: string, days: number, status = "in_progress", repository = repo): ChangeActivity => ({
  repository, change: name, firstAt: daysAgo(days + 5), lastAt: daysAgo(days), lastStatus: status,
});
const row = (id: string, extra: Partial<Row> = {}): Row => ({
  id, repository: repo, title: id, status: "in_progress", firstAt: daysAgo(3), startedAt: null, lastAt: daysAgo(2), elapsedSeconds: 0,
  activityDates: [], completionDates: [], resumedDates: [], changes: [], tickets: [dep12], observationCount: 1, visibleCount: 1, clippedStart: false, ...extra,
});
const entry = (id: string, days: number, extra: Partial<Entry> = {}): Entry => ({
  id, repository: repo, change: null, status: "in_progress", summary: "Summary " + id, occurredAt: daysAgo(days), agent: "claude",
  branch: null, tickets: [], extractedTicketId: null, ...extra,
});
const input = (extra: Partial<BoardInput> = {}): BoardInput => ({ changes: [], activity: [], rows: [], unlinked: [], order: [], ...extra });
const titles = (cards: BoardCard[]) => cards.map((c) => c.title);

describe("change card columns", () => {
  it("puts an untouched proposal in Proposed with no recorded activity", () => {
    const board = buildBoard(input({ changes: [change("idea")] }), now);
    expect(titles(board.proposed)).toEqual(["idea"]);
    expect(board.proposed[0].lastAt).toBeNull();
    expect(board.proposed[0].status).toBeNull();
  });

  it("puts a change with a recorded observation and no ticked task in In progress with both facts", () => {
    const board = buildBoard(input({ changes: [change("work", 0, 9)], activity: [activity("work", 1)] }), now);
    const card = board.in_progress[0];
    expect([card.completedTasks, card.totalTasks, card.status]).toEqual([0, 9, "in_progress"]);
  });

  it("puts a change with a ticked task but no observation in In progress", () => {
    expect(titles(buildBoard(input({ changes: [change("ticked", 1, 9)] }), now).in_progress)).toEqual(["ticked"]);
  });

  it("puts a recorded-done change with unticked tasks in Done and keeps both facts", () => {
    const card = buildBoard(input({ changes: [change("done", 3, 9)], activity: [activity("done", 1, "done")] }), now).done[0];
    expect([card.completedTasks, card.totalTasks, card.status]).toEqual([3, 9, "done"]);
  });

  it("keeps a change with every task ticked in Done indefinitely, until it is archived", () => {
    const quiet = buildBoard(input({ changes: [change("finished", 4, 4)], activity: [activity("finished", 30)] }), now);
    expect(titles(quiet.done)).toEqual(["finished"]);
    expect(titles(buildBoard(input({ changes: [archived("finished")], activity: [activity("finished", 30)] }), now).done)).toEqual([]);
  });

  it("does not treat zero tasks as complete", () => {
    expect(titles(buildBoard(input({ changes: [change("empty", 0, 0)] }), now).proposed)).toEqual(["empty"]);
  });

  it("falls back to the recorded status alone when task progress is unavailable", () => {
    const board = buildBoard(
      input({ changes: [change("a", null, null), change("b", null, null), change("c", null, null)], activity: [activity("a", 1, "done"), activity("b", 1)] }),
      now,
    );
    expect([titles(board.done), titles(board.in_progress), titles(board.proposed)]).toEqual([["a"], ["b"], ["c"]]);
  });

  it("matches activity in any repository of the change and takes the newest", () => {
    const shared = change("shared", 0, 9, { repositories: [repo, "github.com/acme/web"] });
    const board = buildBoard(
      input({ changes: [shared], activity: [activity("shared", 5, "in_progress", repo), activity("shared", 1, "done", "github.com/acme/web"), activity("shared", 0, "done", "elsewhere")] }),
      now,
    );
    expect(board.done[0].lastAt).toBe(daysAgo(1));
    expect(board.done[0].key).toBe("change:github.com/acme/api,github.com/acme/web/shared");
  });

  it("leaves change cards out while activity is not loaded and never shows archived changes", () => {
    const board = buildBoard(input({ changes: [change("x"), archived("old")], activity: null }), now);
    expect(titles([...board.proposed, ...board.in_progress, ...board.done])).toEqual([]);
    expect(titles(buildBoard(input({ changes: [archived("old")] }), now).proposed)).toEqual([]);
  });

  it("shows the tickets of an active change's rows on the change card and not as separate cards", () => {
    const board = buildBoard(
      input({ changes: [change("work")], activity: [activity("work", 1)], rows: [row("r1", { changes: ["work"] }), row("r2", { changes: ["work"], tickets: [dep12, { ...dep12, id: "DEP-13" }] })] }),
      now,
    );
    expect(board.in_progress).toHaveLength(1);
    expect(board.in_progress[0].tickets.map((t) => t.id)).toEqual(["DEP-12", "DEP-13"]);
  });
});

describe("ticket and ad-hoc cards", () => {
  it("makes a ticket card in In progress or Done by its recorded status", () => {
    const board = buildBoard(input({ rows: [row("open"), row("closed", { status: "done" })] }), now);
    expect(titles(board.in_progress)).toEqual(["open"]);
    expect(titles(board.done)).toEqual(["closed"]);
    expect(board.in_progress[0].followUpTo).toBeNull();
    expect(board.in_progress[0].key).toBe("ticket:open");
  });

  it("labels a ticket whose only change is archived as a follow-up to it", () => {
    const board = buildBoard(input({ changes: [archived("shipped")], rows: [row("followup", { changes: ["shipped"] })] }), now);
    expect(board.in_progress[0].followUpTo).toBe("shipped");
  });

  it("turns a change-grouped row of an archived change into a follow-up card, and drops unknown changes and the nameless bucket", () => {
    const board = buildBoard(
      input({
        changes: [archived("shipped")],
        rows: [row("byChange", { changes: ["shipped"], tickets: [] }), row("unknown", { changes: ["gone"], tickets: [] }), row("bucket", { tickets: [], changes: [] })],
      }),
      now,
    );
    expect(titles(board.in_progress)).toEqual(["byChange"]);
    expect(board.in_progress[0].followUpTo).toBe("shipped");
  });

  it("does not call a ticket a follow-up when one of its changes is not archived or the archive is in another repository", () => {
    const other = { ...archived("shipped"), repositories: ["github.com/acme/web"] };
    const board = buildBoard(input({ changes: [other, archived("kept")], rows: [row("a", { changes: ["shipped"] }), row("b", { changes: ["kept", "gone"] })] }), now);
    expect(board.in_progress.map((c) => c.followUpTo)).toEqual([null, null]);
  });

  it("makes each unlinked observation its own card titled with its summary, in the column of its status", () => {
    const board = buildBoard(input({ unlinked: [entry("a", 1), entry("b", 2, { status: "done", summary: "Fixed the flaky build" }), entry("c", 1, { summary: null })] }), now);
    expect(board.in_progress.map((c) => c.title).sort()).toEqual(["No summary recorded", "Summary a"]);
    expect(titles(board.done)).toEqual(["Fixed the flaky build"]);
    expect(board.done[0].key).toBe("adhoc:b");
  });

  it("keeps an ad-hoc card at 6 days and drops it at 8, and the same for ticket cards", () => {
    const board = buildBoard(
      input({ unlinked: [entry("six", 6, { status: "done" }), entry("eight", 8, { status: "done" })], rows: [row("t6", { lastAt: daysAgo(6) }), row("t8", { lastAt: daysAgo(8) })] }),
      now,
    );
    expect(titles(board.done)).toEqual(["Summary six"]);
    expect(titles(board.in_progress)).toEqual(["t6"]);
    expect(titles(buildBoard(input({ unlinked: [entry("edge", 7)] }), now).in_progress)).toEqual([]);
  });

  it("shows the extracted ticket ID of an ad-hoc card and finds it by the filter", () => {
    const board = buildBoard(input({ unlinked: [entry("a", 1, { extractedTicketId: "DEP-99", summary: null })] }), now);
    expect(board.in_progress[0].extractedTicketId).toBe("DEP-99");
    expect(titles(filterBoard(board, "dep-99").in_progress)).toEqual(["No summary recorded"]);
  });
});

describe("filter", () => {
  const board = buildBoard(
    input({
      changes: [change("add-ticket-links"), change("other")],
      activity: [activity("add-ticket-links", 1)],
      rows: [row("t", { title: "DEP-12", tickets: [dep12] }), row("u", { title: "REC-5", tickets: [{ ...dep12, id: "REC-5" }] })],
      unlinked: [entry("a", 1, { summary: "Looked at dep-12 logs" })],
    }),
    now,
  );
  const all = [...board.proposed, ...board.in_progress, ...board.done];

  it("matches change names, ticket IDs, and titles without regard to case, in every column", () => {
    const names = (text: string) => titles(all.filter((c) => matchesFilter(c, text))).sort();
    expect(names("DEP-12")).toEqual(["DEP-12", "Looked at dep-12 logs"]);
    expect(names("TICKET-LINKS")).toEqual(["add-ticket-links"]);
    expect(names("  ")).toHaveLength(all.length);
    expect(names("nothing")).toEqual([]);
    expect(filterBoard(board, "rec").in_progress.map((c) => c.title)).toEqual(["REC-5"]);
  });

  it("also matches the ticket IDs carried by a change card", () => {
    const withTicket = buildBoard(input({ changes: [change("work")], activity: [activity("work", 1)], rows: [row("r", { changes: ["work"] })] }), now);
    expect(titles(filterBoard(withTicket, "DEP-12").in_progress)).toEqual(["work"]);
  });
});

describe("priority order", () => {
  const cards = buildBoard(
    input({
      changes: [change("never", 0, 9), change("new", 0, 9), change("old", 0, 9), change("ranked-a", 0, 9), change("ranked-b", 0, 9)],
      activity: [activity("new", 1), activity("old", 5), activity("ranked-a", 2), activity("ranked-b", 3)],
      order: [changeKey(change("ranked-b")), changeKey(change("ranked-a"))],
    }),
    now,
  ).in_progress;

  it("shows unranked cards first by newest activity, then ranked cards in priority order", () => {
    expect(titles(cards)).toEqual(["new", "old", "ranked-b", "ranked-a"]);
  });

  it("orders unranked cards without activity by name after those with activity", () => {
    const board = buildBoard(input({ changes: [change("b"), change("a"), change("c")], order: [] }), now);
    expect(titles(board.proposed)).toEqual(["a", "b", "c"]);
    const mixed = [...cards, ...board.proposed].sort(byPriority([]));
    expect(titles(mixed)).toEqual(["new", "ranked-a", "ranked-b", "old", "a", "b", "c"]);
  });

  it("keeps a rank when the card changes column", () => {
    const order = [changeKey(change("idea"))];
    const before = buildBoard(input({ changes: [change("idea"), change("peer", 0, 9)], activity: [activity("peer", 1)], order }), now);
    expect(titles(before.proposed)).toEqual(["idea"]);
    const after = buildBoard(input({ changes: [change("idea"), change("peer", 0, 9)], activity: [activity("peer", 1), activity("idea", 0)], order }), now);
    expect(titles(after.in_progress)).toEqual(["peer", "idea"]);
    const ranked = buildBoard(input({ changes: [change("idea"), change("peer", 0, 9)], activity: [activity("peer", 1), activity("idea", 0)], order: [changeKey(change("peer")), ...order] }), now);
    expect(titles(ranked.in_progress)).toEqual(["peer", "idea"]);
    const above = buildBoard(input({ changes: [change("idea"), change("peer", 0, 9)], activity: [activity("peer", 1), activity("idea", 0)], order: [changeKey(change("idea")), changeKey(change("peer"))] }), now);
    expect(titles(above.in_progress)).toEqual(["idea", "peer"]);
  });

  it("treats a new unranked change as first in its column, above ranked ones", () => {
    const board = buildBoard(input({ changes: [change("fresh"), change("kept")], order: [changeKey(change("kept"))] }), now);
    expect(titles(board.proposed)).toEqual(["fresh", "kept"]);
  });

  it("uses the same comparator for any list of cards", () => {
    expect(titles([...cards].sort(byPriority([changeKey(change("old"))])))).toEqual(["new", "ranked-a", "ranked-b", "old"]);
  });
});

describe("moving cards", () => {
  const column = ["a", "b", "c", "d"];
  const visible = new Set(column);

  it("moves one position up or down, or to the top", () => {
    expect(moveWithin(column, visible, "c", "up")).toEqual(["a", "c", "b", "d"]);
    expect(moveWithin(column, visible, "b", "down")).toEqual(["a", "c", "b", "d"]);
    expect(moveWithin(column, visible, "d", "top")).toEqual(["d", "a", "b", "c"]);
  });

  it("refuses moves past the ends", () => {
    expect(moveWithin(column, visible, "a", "up")).toBeNull();
    expect(moveWithin(column, visible, "a", "top")).toBeNull();
    expect(moveWithin(column, visible, "d", "down")).toBeNull();
    expect(moveWithin(column, visible, "missing", "up")).toBeNull();
  });

  it("passes cards hidden by the filter", () => {
    expect(moveWithin(column, new Set(["a", "c"]), "c", "up")).toEqual(["c", "a", "b", "d"]);
    expect(moveWithin(column, new Set(["a", "c"]), "a", "down")).toEqual(["b", "c", "a", "d"]);
  });

  it("drops a card before or after another and ignores a no-op", () => {
    expect(dropWithin(column, "a", "c", false)).toEqual(["b", "a", "c", "d"]);
    expect(dropWithin(column, "a", "c", true)).toEqual(["b", "c", "a", "d"]);
    expect(dropWithin(column, "d", "a", false)).toEqual(["d", "a", "b", "c"]);
    expect(dropWithin(column, "b", "a", true)).toBeNull();
    expect(dropWithin(column, "a", "a", true)).toBeNull();
    expect(dropWithin(column, "a", "x", true)).toBeNull();
  });

  it("ranks the whole column in its new order without reordering other columns", () => {
    // Other columns: x1 before x2 before x3. The column being ranked was partly ranked: c before a.
    const order = ["x1", "c", "x2", "a", "x3"];
    const next = rankColumn(order, ["d", "a", "b", "c"]);
    expect(next).toEqual(["x1", "d", "a", "b", "c", "x2", "x3"]);
    expect(next.filter((k) => k.startsWith("x"))).toEqual(["x1", "x2", "x3"]);
  });

  it("starts the order with the column when none of it was ranked", () => {
    expect(rankColumn(["x1", "x2"], ["a", "b"])).toEqual(["a", "b", "x1", "x2"]);
    expect(rankColumn([], ["a", "b"])).toEqual(["a", "b"]);
  });

  it("keeps previously unranked cards where they were shown after a move", () => {
    const cards = buildBoard(input({ changes: [change("a", 0, 9), change("b", 0, 9), change("c", 0, 9)], activity: [activity("a", 1), activity("b", 2), activity("c", 3)] }), now).in_progress;
    expect(titles(cards)).toEqual(["a", "b", "c"]);
    const moved = moveWithin(cards.map((c) => c.key), new Set(cards.map((c) => c.key)), cards[2].key, "up")!;
    const order = rankColumn([], moved);
    const again = buildBoard(input({ changes: [change("a", 0, 9), change("b", 0, 9), change("c", 0, 9)], activity: [activity("a", 1), activity("b", 2), activity("c", 3)], order }), now).in_progress;
    expect(titles(again)).toEqual(["a", "c", "b"]);
  });
});

describe("pruning the saved order", () => {
  it("drops keys that are not on the board and duplicates, keeping order", () => {
    expect(pruneOrder(["a", "gone", "b", "a", "c"], new Set(["a", "b", "c"]))).toEqual(["a", "b", "c"]);
  });

  it("drops an archived change's key", () => {
    const keyOf = changeKey(change("done-and-archived"));
    const board = buildBoard(input({ changes: [archived("done-and-archived"), change("kept")], order: [keyOf] }), now);
    const live = new Set([...board.proposed, ...board.in_progress, ...board.done].map((c) => c.key));
    expect(pruneOrder([keyOf, changeKey(change("kept"))], live)).toEqual([changeKey(change("kept"))]);
  });

  it("keeps at most 200 entries, dropping the lowest ranked", () => {
    const keys = Array.from({ length: orderLimit + 25 }, (_, i) => "k" + i);
    const pruned = pruneOrder(keys, new Set(keys));
    expect(pruned).toHaveLength(orderLimit);
    expect(pruned[0]).toBe("k0");
    expect(pruned.at(-1)).toBe("k" + (orderLimit - 1));
  });
});

describe("a change present in several sources", () => {
  const copy = (sourceId: string) => change("shared", 0, 9, { sourceId, variant: "v-" + sourceId });
  const build = () =>
    buildBoard(input({ changes: [copy("main"), copy("tree"), change("other", 0, 9)], activity: [activity("shared", 1), activity("other", 2)] }), now);

  it("is one card with unique keys and keeps the copies for the copy chooser", () => {
    const cards = build().in_progress;
    expect(new Set(cards.map((c) => c.key)).size).toBe(cards.length);
    expect(titles(cards)).toEqual(["shared", "other"]);
    expect(cards[0].copies.map((c) => c.sourceId)).toEqual(["main", "tree"]);
    expect(cards[1].copies).toHaveLength(1);
  });

  it("keeps the column length when the card moves", () => {
    const column = build().in_progress.map((c) => c.key);
    const visible = new Set(column);
    expect(moveWithin(column, visible, column[0], "down")).toHaveLength(column.length);
    expect(dropWithin(column, column[1], column[0], false)).toHaveLength(column.length);
  });
});

describe("quiet indication", () => {
  const zone = "UTC";
  const card = (stage: "proposed" | "in_progress" | "done", days: number | null, kind: "change" | "ticket" = "change") => {
    const changes = stage === "proposed" ? [change("c", 0, 9)] : stage === "done" ? [change("c", 9, 9)] : [change("c", 0, 9)];
    const board = buildBoard(input({ changes, activity: days === null ? [] : [activity("c", days)], rows: kind === "ticket" ? [row("t", { lastAt: daysAgo(2) })] : [] }), now);
    return [...board.proposed, ...board.in_progress, ...board.done].find((c) => c.kind === kind)!;
  };

  it("applies only to In-progress change cards", () => {
    expect(isQuiet(card("in_progress", 20), now, zone)).toBe(true);
    expect(isQuiet(card("done", 20), now, zone)).toBe(false);
    expect(isQuiet(card("proposed", null), now, zone)).toBe(false);
    expect(isQuiet(card("in_progress", 20, "ticket"), now, zone)).toBe(false);
  });

  it("uses the 14-day rule: 14 days is recent, 15 is quiet", () => {
    expect(isQuiet(card("in_progress", 14), now, zone)).toBe(false);
    expect(isQuiet(card("in_progress", 15), now, zone)).toBe(true);
    expect(isQuiet(card("in_progress", 1), now, zone)).toBe(false);
  });
});

describe("preference size", () => {
  const hex = (seed: number) => seed.toString(16).padStart(8, "0").repeat(8);

  it("stays within 64 KiB with positions, documents and a 200-entry order at their limits", () => {
    const positions = Object.fromEntries(
      Array.from({ length: positionLimit }, (_, i) => [hex(i) + ":" + "a-fairly-long-change-name-" + i + ":specs/work-dashboard/spec.md:" + hex(i + 1000), 123456]),
    );
    const documents = Object.fromEntries(Array.from({ length: documentLimit }, (_, i) => [hex(i) + ":a-fairly-long-change-name-" + i, "specs/work-dashboard/spec.md"]));
    const longRepo = "github.com/some-organization/a-long-repository-name";
    const order = Array.from({ length: orderLimit }, (_, i) =>
      i % 2 ? "ticket:" + hex(i).slice(0, 16) : changeKey({ repositories: [longRepo, longRepo + "-two"], name: "a-fairly-long-change-name-" + i }),
    );
    const kept = pruneOrder([...order, "extra"], new Set([...order, "extra"]));
    expect(kept).toHaveLength(orderLimit);
    const body = JSON.stringify({ schemaVersion: 1, theme: "light", repositories: [longRepo, longRepo + "-two"], positions, documents, board: { order: kept } });
    expect(new TextEncoder().encode(body).length).toBeLessThanOrEqual(65536);
  });

  it("keys a ticket card by a 16-character prefix of its identity hash", () => {
    const id = "ab".repeat(32);
    const card = buildBoard(input({ rows: [row(id)] }), now).in_progress[0];
    expect(card.key).toBe("ticket:" + id.slice(0, 16));
  });
});

describe("merged change card", () => {
  const doc = (path: string) => ({ path, label: "design", fingerprint: "1:1" });
  const a = change("shared", 1, 9, { sourceId: "a", variant: "va", documents: [doc("design.md")] });
  const b = change("shared", 6, 9, { sourceId: "b", variant: "vb", documents: [doc("design.md"), doc("tasks.md")] });
  const tie = change("shared", 6, 9, { sourceId: "0", variant: "v0", documents: [doc("spec.md")] });

  it("does not depend on the order of the copies", () => {
    for (const changes of [[a, b, tie], [tie, b, a], [b, a, tie]]) {
      const board = buildBoard(input({ changes }), now);
      const card = board.in_progress[0];
      expect(card.stage).toBe("in_progress");
      expect(card.completedTasks).toBe(6);
      expect(card.change!.sourceId).toBe("0");
      expect(card.copies.map((c) => c.sourceId)).toEqual(["0", "b", "a"]);
      expect(card.documents.map((d) => d.path)).toEqual(["spec.md", "design.md", "tasks.md"]);
    }
  });

  it("is Done when the leading copy has every task ticked", () => {
    const done = change("shared", 9, 9, { sourceId: "z", variant: "vz" });
    expect(buildBoard(input({ changes: [a, done] }), now).done[0].change!.sourceId).toBe("z");
    expect(buildBoard(input({ changes: [done, a] }), now).done).toHaveLength(1);
  });
});

describe("fitting the saved order", () => {
  it("drops the lowest ranks until the whole document fits", () => {
    const repo = "github.com/some-organization/a-long-repository-name";
    const order = Array.from({ length: orderLimit }, (_, i) => changeKey({ repositories: [repo, repo + "-two"], name: "a-fairly-long-change-name-" + i }));
    const size = (o: readonly string[]) => new TextEncoder().encode(JSON.stringify({ filler: "x".repeat(40000), board: { order: o } })).length;
    expect(size(order)).toBeGreaterThan(65536);
    const fitted = fitOrder(order, size, 65536);
    expect(size(fitted)).toBeLessThanOrEqual(65536);
    expect(fitted.length).toBeLessThan(order.length);
    expect(fitted).toEqual(order.slice(0, fitted.length));
    expect(size([...fitted, order[fitted.length]])).toBeGreaterThan(65536);
  });
});
