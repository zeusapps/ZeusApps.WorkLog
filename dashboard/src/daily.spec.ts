import { describe, it, expect } from "vitest";
import { BoardInput, buildBoard } from "./board";
import { dailyBlockers, dailyText, dailyToday, dayLabel, digestFor, digestIndex, linePresentation, noBlockers, TodayEntry } from "./daily";
import type { Change, ChangeActivity, DailyItem, Entry, Row, Ticket } from "./models";

const now = new Date("2026-10-28T12:00:00Z");
const todayStart = "2026-10-28T00:00:00.000Z";
const zone = "UTC";
const hoursAgo = (hours: number) => new Date(now.getTime() - hours * 3600000).toISOString();
const daysAgo = (days: number) => hoursAgo(days * 24);
const repo = "github.com/acme/api";
const dep12: Ticket = { provider: "jira", scope: "company", id: "DEP-12" };
const dep13: Ticket = { provider: "jira", scope: "company", id: "DEP-13", url: "https://tracker.example/DEP-13" };
const dep1232: Ticket = { provider: "jira", scope: "acme.atlassian.net", id: "DEP-1232" };
const dep1240: Ticket = { provider: "jira", scope: "acme.atlassian.net", id: "DEP-1240" };

const change = (name: string, completed: number | null = 0, total: number | null = 9, extra: Partial<Change> = {}): Change => ({
  sourceId: "s", name, directoryName: name, variant: "v-" + name, archived: false, archivedOn: null, repositories: [repo],
  documents: [], completedTasks: completed, totalTasks: total, error: null, ...extra,
});
const activity = (name: string, lastAt: string, status = "in_progress"): ChangeActivity => ({
  repository: repo, change: name, firstAt: daysAgo(30), lastAt, lastStatus: status,
});
const row = (id: string, extra: Partial<Row> = {}): Row => ({
  id, repository: repo, title: id, status: "in_progress", firstAt: daysAgo(3), startedAt: null, lastAt: daysAgo(2), elapsedSeconds: 0,
  activityDates: [], completionDates: [], resumedDates: [], changes: [], tickets: [dep12], observationCount: 1, visibleCount: 1, clippedStart: false, ...extra,
});
const entry = (id: string, occurredAt: string, extra: Partial<Entry> = {}): Entry => ({
  id, repository: repo, change: null, status: "in_progress", summary: "Summary " + id, occurredAt, agent: "claude",
  branch: null, tickets: [], extractedTicketId: null, ...extra,
});
const item = (id: string, occurredAt: string, extra: Partial<DailyItem> = {}): DailyItem => ({
  id, title: id, repository: repo, tickets: [dep12], changes: [], status: "in_progress", primary: entry("e-" + id, occurredAt), others: [], ...extra,
});
const input = (extra: Partial<BoardInput> = {}): BoardInput => ({ changes: [], activity: [], rows: [], unlinked: [], order: [], ...extra });
const today = (board: ReturnType<typeof buildBoard>, lines: DailyItem[] = [], order: string[] = []) => dailyToday(board, todayStart, lines, { zone, order });
const keys = (result: ReturnType<typeof dailyToday>) => result.items.map((i) => i.card.key);
const titles = (result: ReturnType<typeof dailyToday>) => result.items.map((i) => i.card.title);

describe("dailyToday", () => {
  it("lists an In-progress card", () => {
    const board = buildBoard(input({ changes: [change("busy")], activity: [activity("busy", daysAgo(2))] }), now);
    expect(titles(today(board))).toEqual(["busy"]);
  });

  it("leaves out a quiet In-progress change", () => {
    const board = buildBoard(input({ changes: [change("idle"), change("busy")], activity: [activity("idle", daysAgo(20)), activity("busy", daysAgo(1))] }), now);
    expect(titles(today(board))).toEqual(["busy"]);
  });

  it("lists a Done card with an observation today but not one finished yesterday", () => {
    const board = buildBoard(input({
      rows: [row("finished-today", { status: "done", lastAt: hoursAgo(3) }), row("finished-yesterday", { status: "done", lastAt: hoursAgo(20) })],
    }), now);
    expect(board.done).toHaveLength(2);
    expect(titles(today(board))).toEqual(["finished-today"]);
  });

  it("leaves out a change with all tasks ticked unless it has an observation today", () => {
    const quiet = buildBoard(input({ changes: [change("complete", 9, 9)], activity: [activity("complete", daysAgo(1))] }), now);
    expect(today(quiet).items).toEqual([]);
    const touched = buildBoard(input({ changes: [change("complete", 9, 9)], activity: [activity("complete", hoursAgo(2))] }), now);
    expect(titles(today(touched))).toEqual(["complete"]);
  });

  it("counts the Proposed cards without listing them", () => {
    const board = buildBoard(input({ changes: [change("idea-1"), change("idea-2"), change("busy")], activity: [activity("busy", daysAgo(1))] }), now);
    const result = today(board);
    expect(result.proposed).toBe(2);
    expect(titles(result)).toEqual(["busy"]);
  });

  it("lists each card once even when it qualifies twice", () => {
    const board = buildBoard(input({ changes: [change("busy")], activity: [activity("busy", hoursAgo(1))] }), now);
    expect(keys(today(board))).toHaveLength(1);
  });

  it("follows the board priority order across columns", () => {
    const board = buildBoard(input({
      changes: [change("first"), change("second")],
      activity: [activity("first", daysAgo(3)), activity("second", daysAgo(1))],
      rows: [row("t-done", { status: "done", lastAt: hoursAgo(1) })],
    }), now);
    const unranked = titles(today(board));
    expect(unranked).toEqual(["t-done", "second", "first"]);
    const ranked = ["change:" + repo + "/first", "ticket:t-done", "change:" + repo + "/second"];
    expect(titles(today(board, [], ranked))).toEqual(["first", "t-done", "second"]);
    const reordered = buildBoard(input({
      changes: [change("first"), change("second")],
      activity: [activity("first", daysAgo(3)), activity("second", daysAgo(1))],
      order: ["change:" + repo + "/second", "change:" + repo + "/first"],
    }), now);
    expect(titles(today(reordered, [], ["change:" + repo + "/second", "change:" + repo + "/first"]))).toEqual(["second", "first"]);
  });

  it("matches a ticket card to its today line by row id", () => {
    const board = buildBoard(input({ rows: [row("abcdef0123456789zzzz", { title: "DEP-12" })] }), now);
    const mine = item("abcdef0123456789zzzz", hoursAgo(2), { title: "DEP-12" });
    const other = item("another-identity", hoursAgo(1));
    const result = today(board, [other, mine]);
    expect(result.items[0].line).toBe(mine);
  });

  it("builds a change card's line from today's entries of that change, the latest winning", () => {
    const board = buildBoard(input({ changes: [change("export-retries")], activity: [activity("export-retries", hoursAgo(1))] }), now);
    const mine = (id: string, hours: number, extra: Partial<Entry> = {}) => entry(id, hoursAgo(hours), { change: "export-retries", ...extra });
    const older = item("a", hoursAgo(5), { primary: mine("e-a", 5) });
    const latest = item("b", hoursAgo(1), { changes: ["export-retries", "other"], status: "done", primary: mine("e-b", 1, { status: "done" }) });
    const elsewhere = item("c", hoursAgo(0.5), { repository: "github.com/acme/other", primary: mine("e-c", 0.5, { repository: "github.com/acme/other" }) });
    const unrelated = item("d", hoursAgo(0.2), { primary: entry("e-d", hoursAgo(0.2), { change: "export" }) });
    const line = today(board, [older, latest, elsewhere, unrelated]).items[0].line;
    expect(line?.primary.id).toBe("e-b");
    expect(line?.status).toBe("done");
    expect(line?.others.map((e) => e.id)).toEqual(["e-a"]);
  });

  it("gives a change card no line when its item spans the change only through history", () => {
    const board = buildBoard(input({ changes: [change("alpha")], activity: [activity("alpha", hoursAgo(1))] }), now);
    const spanning = item("dep-7", hoursAgo(1), {
      changes: ["alpha", "beta"],
      primary: entry("e1", hoursAgo(1), { change: "beta" }),
      others: [entry("e2", hoursAgo(3), { change: "beta" })],
    });
    expect(today(board, [spanning]).items[0].line).toBeNull();
  });

  it("joins today's entries of one change split across a ticket item and a change-only item", () => {
    const board = buildBoard(input({ changes: [change("alpha")], activity: [activity("alpha", hoursAgo(1))] }), now);
    const ticketItem = item("dep-7", hoursAgo(1), {
      tickets: [dep12],
      primary: entry("t1", hoursAgo(1), { change: "alpha", status: "done", tickets: [dep12] }),
      others: [entry("t0", hoursAgo(4), { change: "alpha", tickets: [dep12] })],
    });
    const changeOnly = item("change-alpha", hoursAgo(2), { tickets: [], primary: entry("c1", hoursAgo(2), { change: "alpha" }) });
    const line = today(board, [changeOnly, ticketItem]).items[0].line;
    expect(line?.primary.id).toBe("t1");
    expect(line?.status).toBe("done");
    expect(line?.others.map((e) => e.id)).toEqual(["c1", "t0"]);
    expect(line?.tickets).toEqual([dep12]);
  });

  it("gives a change card the entries logged today without a change for its own ticket", () => {
    const dep7: Ticket = { provider: "jira", scope: "company", id: "DEP-7" };
    const board = buildBoard(input({
      changes: [change("alpha")], activity: [activity("alpha", daysAgo(2))],
      rows: [row("dep-7", { title: "DEP-7", tickets: [dep7], changes: ["alpha"], lastAt: daysAgo(2) })],
    }), now);
    const fix = item("dep-7", hoursAgo(1), {
      title: "DEP-7", tickets: [dep7],
      primary: entry("f1", hoursAgo(1), { change: null, tickets: [dep7], summary: "Fixed DEP-7 regression" }),
    });
    const result = today(board, [fix]);
    expect(titles(result)).toEqual(["alpha"]);
    const line = result.items[0].line;
    expect(line?.primary.id).toBe("f1");
    expect(line?.status).toBe("in_progress");
    expect(dailyText({ day: null, yesterday: [], today: result.items, format: "text", links: false })).toContain("Fixed DEP-7 regression");
  });

  it("leaves out a change-less entry whose ticket differs by provider or scope, and one of another change", () => {
    const dep7: Ticket = { provider: "jira", scope: "company", id: "DEP-7" };
    const board = buildBoard(input({
      changes: [change("alpha")], activity: [activity("alpha", hoursAgo(1))],
      rows: [row("dep-7", { title: "DEP-7", tickets: [dep7], changes: ["alpha"] })],
    }), now);
    const other = (id: string, ticket: Ticket, change: string | null = null) =>
      item(id, hoursAgo(1), { tickets: [ticket], primary: entry(id, hoursAgo(1), { change, tickets: [ticket] }) });
    const line = today(board, [
      other("p", { ...dep7, provider: "github" }),
      other("s", { ...dep7, scope: "elsewhere" }),
      other("c", dep7, "beta"),
    ]).items[0].line;
    expect(line).toBeNull();
  });

  it("gives an ad-hoc card no line", () => {
    const loose = entry("loose", hoursAgo(1), { summary: "Tried a spike" });
    const board = buildBoard(input({ unlinked: [loose] }), now);
    const result = today(board, [item("adhoc:loose", hoursAgo(1), { tickets: [], primary: loose })]);
    expect(keys(result)).toEqual(["adhoc:loose"]);
    expect(result.items[0].line).toBeNull();
  });

  it("gives an In-progress card with nothing recorded today a null line", () => {
    const board = buildBoard(input({ changes: [change("busy")], activity: [activity("busy", daysAgo(2))] }), now);
    expect(today(board, [item("x", hoursAgo(1), { changes: ["busy"], repository: "elsewhere" })]).items[0].line).toBeNull();
  });
});

describe("dayLabel", () => {
  it("names the weekday, day and month", () => {
    expect(dayLabel("2026-10-22")).toBe("Thu 22 Oct");
    expect(dayLabel("2026-01-05")).toBe("Mon 5 Jan");
  });
});

describe("dailyText", () => {
  const retry = item("r", "2026-10-22T15:00:00Z", {
    title: "DEP-1232", tickets: [dep1232], changes: ["retry-policy"], status: "done",
    primary: entry("r1", "2026-10-22T15:00:00Z", { summary: "Retry policy for export jobs merged" }),
  });
  const dashboard = item("w", "2026-10-22T12:00:00Z", {
    title: "add-work-dashboard", tickets: [], changes: ["add-work-dashboard"],
    primary: entry("w1", "2026-10-22T12:00:00Z", { summary: "Review comments edit/delete" }),
  });
  const exportCard = (): TodayEntry => {
    const board = buildBoard(input({ rows: [row("exp-identity", { title: "DEP-1240", tickets: [dep1240], lastAt: hoursAgo(2) })] }), now);
    return {
      card: board.in_progress[0],
      line: item("exp-identity", hoursAgo(2), {
        title: "DEP-1240", tickets: [dep1240],
        primary: entry("x1", hoursAgo(2), { summary: "Export retries wired to the job scheduler;\n  load test remains" }),
      }),
    };
  };
  const dashboardCard = (): TodayEntry => {
    const board = buildBoard(input({ changes: [change("add-work-dashboard")], activity: [activity("add-work-dashboard", daysAgo(1))] }), now);
    return { card: board.in_progress[0], line: null };
  };
  const copyInput = { day: "2026-10-22", yesterday: [retry, dashboard], today: [exportCard(), dashboardCard()] };

  it("copies the plain-text block without links", () => {
    expect(dailyText({ ...copyInput, format: "text", links: false })).toBe(
      [
        "Yesterday (Thu 22 Oct):",
        "- DEP-1232 — done — Retry policy for export jobs merged",
        "- add-work-dashboard — in progress — Review comments edit/delete",
        "Today:",
        "- DEP-1240 — in progress — Export retries wired to the job scheduler; load test remains",
        "- add-work-dashboard",
        "Blockers:",
        "- No blockers recorded",
      ].join("\n"),
    );
  });

  it("copies Markdown with ticket IDs as links when links are on", () => {
    const text = dailyText({ ...copyInput, format: "markdown", links: true });
    expect(text).toBe(
      [
        "**Yesterday (Thu 22 Oct)**",
        "",
        "- [DEP-1232](https://acme.atlassian.net/browse/DEP-1232) — done — Retry policy for export jobs merged",
        "- add-work-dashboard — in progress — Review comments edit/delete",
        "",
        "**Today**",
        "",
        "- [DEP-1240](https://acme.atlassian.net/browse/DEP-1240) — in progress — Export retries wired to the job scheduler; load test remains",
        "- add-work-dashboard",
        "",
        "**Blockers**",
        "",
        "- No blockers recorded",
      ].join("\n"),
    );
  });

  it("copies Markdown without links as plain IDs", () => {
    const text = dailyText({ ...copyInput, format: "markdown", links: false });
    expect(text).toContain("- DEP-1232 — done");
    expect(text).not.toContain("](");
  });

  it("appends the URL in plain text only when links are on, and leaves unknown tickets plain", () => {
    const withUrl = item("l", "2026-10-22T15:00:00Z", { title: "DEP-12 + DEP-13", tickets: [dep13, dep12], status: "started", primary: entry("l1", "2026-10-22T15:00:00Z", { summary: null }) });
    const text = dailyText({ day: "2026-10-22", yesterday: [withUrl], today: [], format: "text", links: true });
    expect(text.split("\n")[1]).toBe("- DEP-12 + DEP-13 (https://tracker.example/DEP-13) — started");
    expect(dailyText({ day: "2026-10-22", yesterday: [withUrl], today: [], format: "text", links: false })).not.toContain("http");
  });

  it("titles an unlinked line by its summary without repeating it, or states that none was recorded", () => {
    const loose = item("adhoc:a", "2026-10-22T10:00:00Z", { title: "Tried a spike", tickets: [], primary: entry("a", "2026-10-22T10:00:00Z", { summary: "Tried a spike" }) });
    const bare = item("adhoc:b", "2026-10-22T09:00:00Z", { title: "", tickets: [], status: "started", primary: entry("b", "2026-10-22T09:00:00Z", { summary: null }) });
    const lines = dailyText({ day: "2026-10-22", yesterday: [loose, bare], today: [], format: "text", links: false }).split("\n");
    expect(lines.slice(1, 3)).toEqual(["- Tried a spike — in progress", "- No summary recorded — started"]);
  });

  it("states what was not recorded without inventing work", () => {
    expect(dailyText({ day: null, yesterday: [], today: [], format: "text", links: false })).toBe(
      "Yesterday:\n- Nothing recorded\nToday:\n- Nothing recorded\nBlockers:\n- No blockers recorded",
    );
  });
});

describe("agent digests", () => {
  const two = item("r", "2026-10-22T15:00:00Z", {
    title: "DEP-1232", tickets: [dep1232], status: "done",
    primary: entry("r2", "2026-10-22T15:00:00Z", { summary: "Latest summary", status: "done" }),
    others: [entry("r1", "2026-10-22T10:00:00Z", { summary: "Older summary" })],
  });
  const legacy = { covers: ["r1", "r2"], text: "Shipped the retry policy.\nTwo steps." };
  const structured = {
    covers: ["r1", "r2"], text: "Done: a; b. Next: c.",
    done: ["Retry policy merged", "Backoff tuned"], next: ["Roll out to staging"], blockers: [] as string[],
  };

  it("matches a line whose primary and others are exactly the covered observations, in any order", () => {
    expect(digestFor(digestIndex([legacy]), two)?.text).toBe("Shipped the retry policy.\nTwo steps.");
    expect(digestFor(digestIndex([{ covers: ["r2", "r1"], text: "x" }]), two)?.text).toBe("x");
  });

  it("does not match a stale digest after a later entry, or a subset or superset", () => {
    const later = { ...two, primary: entry("r3", "2026-10-22T16:00:00Z"), others: [two.primary, ...two.others] };
    expect(digestFor(digestIndex([legacy]), later)).toBeNull();
    expect(digestFor(digestIndex([{ covers: ["r1"], text: "x" }]), two)).toBeNull();
    expect(digestFor(digestIndex([legacy]), null)).toBeNull();
    expect(digestFor(digestIndex(undefined), two)).toBeNull();
  });

  it("matches an assembled change-card line by the entries it was built from", () => {
    const board = buildBoard(input({ changes: [change("alpha")], activity: [activity("alpha", hoursAgo(1))] }), now);
    const ticketItem = item("dep-7", hoursAgo(1), {
      tickets: [dep12], primary: entry("t1", hoursAgo(1), { change: "alpha", tickets: [dep12] }),
      others: [entry("t0", hoursAgo(4), { change: "alpha", tickets: [dep12] })],
    });
    const changeOnly = item("change-alpha", hoursAgo(2), { tickets: [], primary: entry("c1", hoursAgo(2), { change: "alpha" }) });
    const line = today(board, [changeOnly, ticketItem]).items[0].line;
    const index = digestIndex([{ covers: ["c1", "t0", "t1"], text: "Alpha moved forward." }]);
    expect(digestFor(index, line)?.text).toBe("Alpha moved forward.");
    expect(digestFor(digestIndex([{ covers: ["t0", "t1"], text: "partial" }]), line)).toBeNull();
  });

  describe("line presentation", () => {
    it("uses the lists of a matched structured digest, keeping empty lists empty", () => {
      expect(linePresentation(structured, "Latest summary")).toEqual({
        done: ["Retry policy merged", "Backoff tuned"], next: ["Roll out to staging"], blockers: [], agent: true,
      });
    });

    it("shows an earlier single-text digest, with or without lists, as one Done bullet", () => {
      const expected = { done: ["Shipped the retry policy. Two steps."], next: [], blockers: [], agent: true };
      expect(linePresentation(legacy, "Latest summary")).toEqual(expected);
      expect(linePresentation({ ...legacy, done: ["Shipped the retry policy. Two steps."], next: [], blockers: [] }, "Latest summary")).toEqual(expected);
    });

    it("falls back to the latest summary as one Done bullet, or the no-summary label, without the agent mark", () => {
      expect(linePresentation(null, "Latest summary")).toEqual({ done: ["Latest summary"], next: [], blockers: [], agent: false });
      expect(linePresentation(null, null)).toEqual({ done: ["No summary recorded"], next: [], blockers: [], agent: false });
      expect(linePresentation(null, "  ")).toEqual({ done: ["No summary recorded"], next: [], blockers: [], agent: false });
    });

    it("falls back when a later observation made the digest stale, or a different timezone splits the day differently", () => {
      const later = { ...two, primary: entry("r3", "2026-10-22T16:00:00Z", { summary: "Third step" }), others: [two.primary, ...two.others] };
      expect(linePresentation(digestFor(digestIndex([structured]), later), later.primary.summary ?? null).done).toEqual(["Third step"]);
      const split = { ...two, others: [] };
      expect(linePresentation(digestFor(digestIndex([structured]), split), "Latest summary")).toMatchObject({ done: ["Latest summary"], agent: false });
    });
  });

  describe("blockers", () => {
    const dep9: Ticket = { provider: "jira", scope: "company", id: "DEP-9" };
    const blocked = { covers: ["x1"], text: "t", done: ["Hook added"], next: [], blockers: ["API access from the platform team"] };
    const todayEntries = (lines: Record<string, DailyItem>): TodayEntry[] => {
      const board = buildBoard(input({
        rows: [
          row("dep9", { title: "DEP-9", tickets: [dep9], lastAt: hoursAgo(1) }),
          row("dep12", { title: "DEP-12", tickets: [dep12], lastAt: hoursAgo(2) }),
        ],
      }), now);
      return board.in_progress.map((card) => ({ card, line: lines[card.row!.id] ?? null }));
    };
    const lineOf = (id: string, entryId: string, tickets: Ticket[]) =>
      item(id, hoursAgo(1), { title: id, tickets, primary: entry(entryId, hoursAgo(1)) });

    it("lists a Today line's blockers with its ticket", () => {
      const groups = dailyBlockers(todayEntries({ dep9: lineOf("dep9", "x1", [dep9]) }), digestIndex([blocked]));
      expect(groups).toHaveLength(1);
      expect(groups[0].tickets.map((t) => t.id)).toEqual(["DEP-9"]);
      expect(groups[0].bullets).toEqual(["API access from the platform team"]);
    });

    it("finds no blockers when no Today line shows blocker bullets", () => {
      const lines = todayEntries({ dep9: lineOf("dep9", "x1", [dep9]) });
      expect(dailyBlockers(lines, digestIndex([{ ...blocked, blockers: [] }]))).toEqual([]);
      expect(dailyBlockers(lines, digestIndex([{ covers: ["x1"], text: "legacy" }]))).toEqual([]);
      expect(dailyBlockers(lines, digestIndex(undefined))).toEqual([]);
      expect(noBlockers).toBe("No blockers recorded");
    });

    it("follows Today order, and ignores a Yesterday digest the Today line does not show", () => {
      const lines = todayEntries({ dep9: lineOf("dep9", "x1", [dep9]), dep12: lineOf("dep12", "x2", [dep12]) });
      const both = digestIndex([blocked, { covers: ["x2"], text: "t", done: ["d"], blockers: ["Waiting on review"] }]);
      const expected = lines.map((e) => (e.line!.primary.id === "x1" ? "API access from the platform team" : "Waiting on review"));
      expect(dailyBlockers(lines, both).map((g) => g.bullets[0])).toEqual(expected);
      const yesterdayOnly = digestIndex([
        { covers: ["y1"], text: "t", done: ["d"], blockers: ["Old blocker"] },
        { covers: ["x1"], text: "t", done: ["d"], blockers: [] },
      ]);
      expect(dailyBlockers(lines, yesterdayOnly)).toEqual([]);
    });
  });

  describe("copy", () => {
    const loose = item("w", "2026-10-22T12:00:00Z", { title: "add-work-dashboard", tickets: [], primary: entry("w1", "2026-10-22T12:00:00Z", { summary: "Review comments" }) });
    const full = { ...structured, blockers: ["Waiting on API access"] };
    const base = { day: "2026-10-22", yesterday: [two, loose], today: [] as TodayEntry[], digests: [full] };

    it("copies a structured digest as labelled bullet lists in plain text, omitting empty lists, and the summary inline where none matches", () => {
      expect(dailyText({ ...base, digests: [structured], format: "text", links: false })).toBe([
        "Yesterday (Thu 22 Oct):",
        "- DEP-1232 — done",
        "  Done:",
        "  - Retry policy merged",
        "  - Backoff tuned",
        "  Next:",
        "  - Roll out to staging",
        "- add-work-dashboard — in progress — Review comments",
        "Today:", "- Nothing recorded", "Blockers:", "- No blockers recorded",
      ].join("\n"));
    });

    it("copies Markdown with links as nested - lists", () => {
      const withLink = { ...base, yesterday: [{ ...two, tickets: [dep13] }], digests: [full] };
      expect(dailyText({ ...withLink, format: "markdown", links: true })).toBe([
        "**Yesterday (Thu 22 Oct)**", "",
        "- [DEP-13](https://tracker.example/DEP-13) — done",
        "  - Done:",
        "    - Retry policy merged",
        "    - Backoff tuned",
        "  - Next:",
        "    - Roll out to staging",
        "  - Blockers:",
        "    - Waiting on API access",
        "",
        "**Today**", "", "- Nothing recorded", "", "**Blockers**", "", "- No blockers recorded",
      ].join("\n"));
    });

    it("copies an earlier single-text digest under Done without the mark", () => {
      const text = dailyText({ ...base, digests: [legacy], format: "text", links: false });
      expect(text).toContain("- DEP-1232 — done — Shipped the retry policy. Two steps.\n- add-work-dashboard");
      expect(text).not.toContain("agent digest");
    });

    it.each([
      ["no digest", [] as (typeof structured)[], "Latest summary"],
      ["a text-only digest", [legacy], "Shipped the retry policy. Two steps."],
      ["a structured digest with one Done bullet", [{ covers: ["r1", "r2"], text: "t", done: ["One bullet"], next: [], blockers: [] }], "One bullet"],
    ])("copies one Done bullet inline in both formats (%s)", (_name, digests, text) => {
      const one = { day: "2026-10-22", yesterday: [two], today: [] as TodayEntry[], digests };
      expect(dailyText({ ...one, format: "text", links: false })).toContain("- DEP-1232 — done — " + text + "\nToday:");
      expect(dailyText({ ...one, format: "markdown", links: false })).toContain("- DEP-1232 — done — " + text + "\n\n**Today**");
    });

    it("copies the derived blockers with the line's tickets", () => {
      const board = buildBoard(input({ rows: [row("dep9", { title: "DEP-9", tickets: [dep13], lastAt: hoursAgo(1) })] }), now);
      const todayEntry: TodayEntry = { card: board.in_progress[0], line: item("dep9", hoursAgo(1), { tickets: [dep13], primary: entry("x1", hoursAgo(1)) }) };
      const digests = [{ covers: ["x1"], text: "t", done: ["Hook added"], blockers: ["API access from the platform team"] }];
      const text = dailyText({ day: null, yesterday: [], today: [todayEntry], digests, format: "text", links: false });
      expect(text).toContain("Blockers:\n- DEP-13 — API access from the platform team");
      expect(text).not.toContain("No blockers recorded");
      const md = dailyText({ day: null, yesterday: [], today: [todayEntry], digests, format: "markdown", links: true });
      expect(md).toContain("**Blockers**\n\n- [DEP-13](https://tracker.example/DEP-13) — API access from the platform team");
    });
  });
});
