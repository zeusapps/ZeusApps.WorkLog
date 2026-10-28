import { describe, it, expect } from "vitest";
import { activityFor, archiveEntries, editorLabel, firstVisibleLine, sourceLineAfter, validEditor } from "./archive";
import type { Change, ChangeActivity } from "./models";

function change(name: string, archivedOn: string | null, extra: Partial<Change> = {}): Change {
  return {
    sourceId: "s",
    name,
    directoryName: archivedOn ? archivedOn + "-" + name : name,
    variant: "v-" + name + (archivedOn ?? ""),
    archived: true,
    archivedOn,
    repositories: ["r"],
    documents: [],
    completedTasks: null,
    totalTasks: null,
    error: null,
    ...extra,
  };
}
function activity(change: string, lastAt: string, extra: Partial<ChangeActivity> = {}): ChangeActivity {
  return { repository: "r", change, firstAt: "2026-09-01T00:00:00Z", lastAt, lastStatus: "done", ...extra };
}
const everything = () => true;

describe("archive list", () => {
  it("orders newest first, then undated entries, with ties by name", () => {
    const entries = archiveEntries(
      [
        change("zeta", "2026-10-02"),
        change("undated", null),
        change("alpha", "2026-10-02"),
        change("newest", "2026-10-20"),
        change("oldest", "2026-01-05"),
        change("active", null, { archived: false }),
      ],
      everything,
      "",
      [],
    );
    expect(entries.map((e) => e.change.name)).toEqual(["newest", "alpha", "zeta", "oldest", "undated"]);
  });

  it("filters by name ignoring case and by repository scope", () => {
    const changes = [
      change("add-Archive-browser", "2026-10-02"),
      change("fix-reliability", "2026-10-01"),
      change("archive-other", "2026-09-01", { repositories: ["other"] }),
    ];
    expect(archiveEntries(changes, (r) => r.includes("r"), "ARCHIVE", []).map((e) => e.change.name)).toEqual(["add-Archive-browser"]);
    expect(archiveEntries(changes, everything, "  archive ", []).map((e) => e.change.name)).toEqual(["add-Archive-browser", "archive-other"]);
    expect(archiveEntries(changes, everything, "missing", [])).toEqual([]);
  });

  it("matches activity by logical name in any of the change's repositories, newest last activity winning", () => {
    const shared = change("demo", "2026-10-02", { repositories: ["r", "worktree"] });
    const records = [
      activity("demo", "2026-10-03T10:00:00Z"),
      activity("demo", "2026-10-05T10:00:00Z", { repository: "worktree", lastStatus: "in_progress" }),
      activity("demo", "2026-11-01T10:00:00Z", { repository: "unrelated" }),
      activity("other", "2026-12-01T10:00:00Z"),
    ];
    expect(activityFor(shared, records)).toMatchObject({ repository: "worktree", lastStatus: "in_progress" });
    expect(archiveEntries([shared], everything, "", records)[0].activity?.lastAt).toBe("2026-10-05T10:00:00Z");
    expect(activityFor(change("quiet", null), records)).toBeNull();
    expect(archiveEntries([change("quiet", null)], everything, "", records)[0].activity).toBeNull();
  });
});

describe("editor presets", () => {
  it("accepts only the fixed presets and defaults to VS Code", () => {
    expect(validEditor("cursor")).toBe("cursor");
    expect(validEditor("system")).toBe("system");
    expect(validEditor("notepad")).toBe("vscode");
    expect(validEditor(undefined)).toBe("vscode");
    expect(editorLabel("system")).toBe("System default app");
  });
});

describe("reading position", () => {
  const blocks = [
    { start: 0, top: -400 },
    { start: 4, top: -120 },
    { start: 9, top: 20 },
    { start: 15, top: 300 },
  ];
  it("is the first block whose top is at or below the reader's top edge", () => {
    expect(firstVisibleLine(blocks, 0)).toBe(9);
    expect(firstVisibleLine(blocks, 20)).toBe(9);
    expect(firstVisibleLine(blocks, 21)).toBe(15);
    expect(firstVisibleLine(blocks, -1000)).toBe(0);
  });
  it("falls back to the last block when all start above the edge, and to the first line without blocks", () => {
    expect(firstVisibleLine(blocks, 1000)).toBe(15);
    expect(firstVisibleLine([], 0)).toBe(0);
  });
  it("counts the line breaks before the character at the top edge of the source view", () => {
    expect(sourceLineAfter("")).toBe(0);
    expect(sourceLineAfter("# Title\n\nA long wrapped paragraph that")).toBe(2);
    expect(sourceLineAfter("a\r\nb\r\n")).toBe(2);
    expect(sourceLineAfter("\n\n\n")).toBe(3);
  });
});
