import { describe, it, expect } from "vitest";
import { Change, ReviewItem } from "./models";
import { parseReviews } from "./markdown";
import {
  baselineKey,
  changeKey,
  countByChange,
  countByDocument,
  documentKey,
  groupReviews,
  removedReviews,
  updatedDocuments,
} from "./reviews";

function item(change: string, path: string, line: number, extra: Partial<ReviewItem> = {}): ReviewItem {
  return {
    sourceId: "s",
    change,
    variant: "v-" + change,
    path,
    archived: false,
    repositories: ["r"],
    line,
    id: null,
    quote: "Quote " + line,
    comment: "Comment " + line,
    truncated: false,
    ...extra,
  };
}
const items = [
  item("alpha", "design.md", 4),
  item("alpha", "design.md", 12),
  item("alpha", "tasks.md", 3),
  item("beta", "design.md", 6),
  item("old", "design.md", 8, { archived: true }),
];

describe("review comment overview", () => {
  it("groups comments by change, then document, in the order the server listed them", () => {
    const groups = groupReviews(items, "active");
    expect(groups.map((g) => [g.change, g.count])).toEqual([
      ["alpha", 3],
      ["beta", 1],
    ]);
    expect(groups[0].documents.map((d) => [d.path, d.items.map((i) => i.line)])).toEqual([
      ["design.md", [4, 12]],
      ["tasks.md", [3]],
    ]);
  });

  it("filters by archive state with the scope switch and keeps archived entries marked as archived", () => {
    expect(groupReviews(items, "archived").map((g) => [g.change, g.archived, g.count])).toEqual([["old", true, 1]]);
    expect(groupReviews(items, "all").map((g) => g.change)).toEqual(["alpha", "beta", "old"]);
    expect(groupReviews([], "all")).toEqual([]);
  });

  it("keeps two copies of one change apart", () => {
    const copies = [item("alpha", "design.md", 1, { variant: "one" }), item("alpha", "design.md", 2, { variant: "two" })];
    expect(groupReviews(copies, "all").map((g) => [g.variant, g.count])).toEqual([
      ["one", 1],
      ["two", 1],
    ]);
  });

  it("counts open comments per change and per document", () => {
    const perChange = countByChange(items);
    expect(perChange.get(changeKey("s", "v-alpha"))).toBe(3);
    expect(perChange.get(changeKey("s", "v-beta"))).toBe(1);
    expect(perChange.get(changeKey("s", "v-none"))).toBeUndefined();
    const perDocument = countByDocument(items);
    expect(perDocument.get(documentKey("s", "v-alpha", "design.md"))).toBe(2);
    expect(perDocument.get(documentKey("s", "v-alpha", "tasks.md"))).toBe(1);
  });
});

const change: Change = {
  sourceId: "s",
  name: "demo",
  directoryName: "demo",
  variant: "v",
  archived: false,
  repositories: ["r"],
  documents: [
    { path: "design.md", label: "design", fingerprint: "100:5000" },
    { path: "tasks.md", label: "tasks", fingerprint: "40:6000" },
    { path: "proposal.md", label: "proposal", fingerprint: "10:7000" },
  ],
  completedTasks: null,
  totalTasks: null,
  error: null,
};

describe("updated since read", () => {
  it("compares fingerprints as text, so write times beyond what a number holds exactly still match", () => {
    const exact = "639000000000000001";
    const big = { ...change, documents: [{ path: "design.md", label: "design", fingerprint: "100:" + exact }] };
    expect(updatedDocuments(big, { [baselineKey("s", "demo", "design.md")]: "100:" + exact })).toEqual([]);
    expect(updatedDocuments(big, { [baselineKey("s", "demo", "design.md")]: "100:639000000000000002" })).toEqual(["design.md"]);
  });

  it("lists the documents whose stored fingerprint differs from the catalog", () => {
    const summary = {
      [baselineKey("s", "demo", "design.md")]: "100:4999",
      [baselineKey("s", "demo", "tasks.md")]: "40:6000",
    };
    // proposal.md was never read, so it has nothing to differ from.
    expect(updatedDocuments(change, summary)).toEqual(["design.md"]);
    expect(updatedDocuments(change, {})).toEqual([]);
  });

  it("uses the logical change name, so an archive move keeps the entry", () => {
    const archived = { ...change, archived: true, directoryName: "2026-10-27-demo" };
    const summary = { [baselineKey("s", "demo", "tasks.md")]: "41:6000" };
    expect(updatedDocuments(archived, summary)).toEqual(["tasks.md"]);
  });
});

function marker(id: string | null, quote: string, comment: string) {
  return id
    ? `<!-- REVIEW: id=${id}\nTarget: preceding passage\nQuote: ${quote}\nComment:\n${comment}\n-->\n`
    : `<!-- REVIEW: ${comment} -->\n`;
}
const idA = "a".repeat(32);
const idB = "b".repeat(32);

describe("removed since last read", () => {
  it("lists a structured comment whose id is gone and keeps one that is still there, even when edited", () => {
    const baseline = "# Doc\n\nOne.\n\n" + marker(idA, "One.", "Fix one") + "\nTwo.\n\n" + marker(idB, "Two.", "Fix two");
    const current = "# Doc\n\nOne.\n\n" + marker(idA, "One.", "Fix one, reworded") + "\nTwo, fixed.\n";
    const removed = removedReviews(baseline, parseReviews(current));
    expect(removed.map((r) => [r.id, r.quote, r.text])).toEqual([[idB, "Two.", "Fix two"]]);
  });

  it("compares legacy comments without an id by their exact marker text", () => {
    const baseline = "A.\n\n" + marker(null, "", "Legacy kept") + "\nB.\n\n" + marker(null, "", "Legacy gone") + "\nC.\n\n" + marker(null, "", "Legacy reworded");
    const current = "A.\n\n" + marker(null, "", "Legacy kept") + "\nB fixed.\n\nC.\n\n" + marker(null, "", "Legacy reworded by agent");
    expect(removedReviews(baseline, parseReviews(current)).map((r) => r.text)).toEqual(["Legacy gone", "Legacy reworded"]);
  });

  it("ignores marker syntax inside code examples and reports nothing when nothing was removed", () => {
    const fenced = "```md\n" + marker(idA, "x", "Just syntax") + "```\n";
    expect(removedReviews(fenced, [])).toEqual([]);
    const text = "P.\n\n" + marker(idA, "P.", "Keep");
    expect(removedReviews(text, parseReviews(text))).toEqual([]);
    expect(removedReviews("No comments here.\n", parseReviews(text))).toEqual([]);
  });

  it("matches CRLF documents against LF documents", () => {
    const baseline = ("P.\n\n" + marker(idA, "P.", "Keep")).replace(/\n/g, "\r\n");
    const current = "P.\n\n" + marker(idA, "P.", "Keep");
    expect(removedReviews(baseline, parseReviews(current))).toEqual([]);
  });
});
