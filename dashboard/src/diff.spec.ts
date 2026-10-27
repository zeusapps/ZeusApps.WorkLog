import { describe, it, expect } from "vitest";
import { changeStops, intersects, normalizeText, readMarks, sourceSegments } from "./diff";
import { renderMarkdown } from "./markdown";

const before = [
  "# Title", //                      0
  "", //                             1
  "First paragraph.", //             2
  "", //                             3
  "- item one", //                   4
  "- item two", //                   5
  "- item three", //                 6
  "", //                             7
  "| a | b |", //                   8
  "| - | - |", //                   9
  "| 1 | 2 |", //                  10
  "| 3 | 4 |", //                  11
  "", //                            12
  "```js", //                       13
  "code();", //                     14
  "```", //                         15
  "", //                            16
  "Last paragraph.", //             17
  "",
].join("\n");

const after = [
  "# Title", //                      0
  "", //                             1
  "First paragraph, rewritten.", //  2
  "", //                             3
  "- item one", //                   4
  "- item 2 edited", //              5
  "", //                             6
  "| a | b |", //                   7
  "| - | - |", //                   8
  "| 1 | 2 |", //                   9
  "| 3 | 5 |", //                  10
  "", //                            11
  "```js", //                       12
  "code(1);", //                    13
  "```", //                         14
  "", //                            15
  "Last paragraph.", //             16
  "", //                            17
  "Added paragraph.", //            18
  "",
].join("\n");

function blocks(html: string, selector = ".changed-since-read") {
  const parsed = new DOMParser().parseFromString(html, "text/html");
  return Array.from(parsed.querySelectorAll<HTMLElement>(selector));
}

describe("line differences", () => {
  it("is null for identical text however its line endings are written", () => {
    expect(readMarks("a\nb\n", "a\r\nb\r\n")).toBeNull();
    expect(readMarks("a\nb", "a\nb\n")).toBeNull();
    expect(readMarks("", "")).toBeNull();
  });

  it("reports added and rewritten lines as ranges of the current text and deleted lines as hunks", () => {
    const marks = readMarks(before, after)!;
    expect(marks.changed).toEqual([[2, 3], [5, 6], [10, 11], [13, 14], [17, 19]]);
    expect(marks.removed.map((r) => [r.line, r.text])).toEqual([
      [2, "First paragraph.\n"],
      [5, "- item two\n- item three\n"],
      [10, "| 3 | 4 |\n"],
      [13, "code();\n"],
    ]);
  });

  it("merges neighbouring additions and handles a document that appears from nothing or is emptied", () => {
    expect(readMarks("", "a\nb\nc\n")).toEqual({ changed: [[0, 3]], removed: [] });
    expect(readMarks("a\nb\n", "")).toEqual({ changed: [], removed: [{ line: 0, text: "a\nb\n" }] });
    expect(readMarks("a\nb\n", "a\nx\ny\nb\n")!.changed).toEqual([[1, 3]]);
  });

  it("tests whether a block shares a line with a range", () => {
    expect(intersects([[2, 4]], 0, 2)).toBe(false);
    expect(intersects([[2, 4]], 3, 5)).toBe(true);
    expect(intersects([[2, 4]], 4, 6)).toBe(false);
    expect(intersects([], 0, 100)).toBe(false);
  });
});

describe("diff to block mapping", () => {
  const html = renderMarkdown(after, false, readMarks(before, after)).html;

  it("marks the innermost blocks that share a line with a change, by their data-start", () => {
    expect(blocks(html).map((e) => [e.tagName.toLowerCase(), e.dataset["start"]])).toEqual([
      ["p", "2"],
      ["li", "5"],
      ["tr", "10"],
      ["div", "12"],
      ["p", "18"],
    ]);
  });

  it("leaves unchanged blocks and their containers unmarked and labels the marked ones", () => {
    const parsed = new DOMParser().parseFromString(html, "text/html");
    for (const selector of ["h1", "ul", "table", "li[data-start='4']", "tr[data-start='9']", "p[data-start='16']"])
      expect(parsed.querySelector(selector)!.classList.contains("changed-since-read")).toBe(false);
    for (const element of blocks(html)) expect(element.dataset["readState"]).toBe("changed");
  });

  it("places each removed passage collapsed before the top-level block that contains or follows it", () => {
    const parsed = new DOMParser().parseFromString(html, "text/html");
    const removed = Array.from(parsed.querySelectorAll<HTMLDetailsElement>("details.removed-since-read"));
    expect(removed.map((d) => d.dataset["removedLine"])).toEqual(["2", "5", "10", "13"]);
    expect(removed.every((d) => !d.open)).toBe(true);
    // The first sits right before the paragraph that replaced it; the others before the list, table and fence that contain the change.
    expect(removed.map((d) => d.nextElementSibling!.tagName.toLowerCase())).toEqual(["p", "ul", "table", "div"]);
    expect(removed[1].querySelector("pre")!.textContent).toBe("- item two\n- item three");
    expect(removed[1].querySelector("summary")!.textContent).toContain("2 lines");
    expect(removed[0].querySelector("summary")!.textContent).toContain("1 line)");
  });

  it("shows removed text as escaped plain text, never as markup", () => {
    const rendered = renderMarkdown("kept\n", false, readMarks("kept\n\n<script>alert(1)</script>\n<b>bold</b> & more\n", "kept\n")).html;
    const parsed = new DOMParser().parseFromString(rendered, "text/html");
    expect(parsed.querySelector("script")).toBeNull();
    expect(parsed.querySelector("b")).toBeNull();
    expect(parsed.querySelector("details pre")!.textContent).toContain("<script>alert(1)</script>");
    expect(parsed.querySelector("details pre")!.textContent).toContain("<b>bold</b> & more");
  });

  it("puts a passage removed at the end of the document after the last block", () => {
    const rendered = renderMarkdown("# Title\n\nBody\n", false, readMarks("# Title\n\nBody\n\nGone\n", "# Title\n\nBody\n")).html;
    const parsed = new DOMParser().parseFromString(rendered, "text/html");
    const last = parsed.body.lastElementChild!;
    expect(last.matches("details.removed-since-read")).toBe(true);
    expect(last.previousElementSibling!.tagName.toLowerCase()).toBe("p");
  });

  it("marks nothing when no marks are given", () => {
    expect(blocks(renderMarkdown(after).html)).toHaveLength(0);
    expect(blocks(renderMarkdown(after, false, null).html, "details")).toHaveLength(0);
  });

  it("marks the review note when a comment appears between reads", () => {
    const text = "Passage.\n\n<!-- REVIEW: id=0123456789abcdef0123456789abcdef\nTarget: preceding passage\nQuote: Passage.\nComment:\nFix it\n-->\n";
    const rendered = renderMarkdown(text, false, readMarks("Passage.\n", text)).html;
    const notes = blocks(rendered, "aside.review-note");
    expect(notes).toHaveLength(1);
    expect(notes[0].classList.contains("changed-since-read")).toBe(true);
    expect(notes[0].dataset["readState"]).toBe("changed");
    expect(blocks(rendered, "p.changed-since-read")).toHaveLength(0);
  });

  it("groups neighbouring marked blocks into one stop and counts removed passages as stops", () => {
    const old = "# T\n\nOne\n\nTwo\n\nThree\n\nFour\n";
    const rewritten = "# T\n\nOne edited\n\nTwo edited\n\nThree\n\nFour edited\n";
    const parsed = new DOMParser().parseFromString(renderMarkdown(rewritten, false, readMarks(old, rewritten)).html, "text/html");
    const stops = changeStops(parsed);
    // One stop for the first two paragraphs (plus the removed passage before them), one for the last.
    expect(stops.map((e) => e.dataset["start"] ?? e.dataset["removedLine"])).toEqual(["2", "8"]);
    const lonely = new DOMParser().parseFromString(renderMarkdown("a\n", false, readMarks("a\n\nb\n", "a\n")).html, "text/html");
    expect(changeStops(lonely)).toHaveLength(1);
  });
});

describe("source view highlighting", () => {
  it("splits the text into runs of unchanged and changed lines that rejoin to the original", () => {
    const marks = readMarks(before, after)!;
    const segments = sourceSegments(after, marks);
    expect(segments.map((s) => s.text).join("")).toBe(after);
    const lines = after.split("\n");
    const changed = segments.filter((s) => s.changed).map((s) => s.text);
    expect(changed).toEqual([
      lines[2] + "\n",
      lines[5] + "\n",
      lines[10] + "\n",
      lines[13] + "\n",
      lines[17] + "\n" + lines[18] + "\n",
    ]);
    expect(segments.some((s) => !s.changed && s.text.startsWith("# Title"))).toBe(true);
  });

  it("is one unchanged run without marks or without changed lines, and keeps CRLF text intact", () => {
    expect(sourceSegments("a\nb", null)).toEqual([{ text: "a\nb", changed: false }]);
    expect(sourceSegments("a", { changed: [], removed: [{ line: 0, text: "x\n" }] })).toEqual([{ text: "a", changed: false }]);
    const crlf = "a\r\nb\r\nc";
    const segments = sourceSegments(crlf, { changed: [[1, 2]], removed: [] });
    expect(segments).toEqual([
      { text: "a\r\n", changed: false },
      { text: "b\r\n", changed: true },
      { text: "c", changed: false },
    ]);
  });

  it("normalizes line endings and the final newline", () => {
    expect(normalizeText("a\r\nb\rc")).toBe("a\nb\nc\n");
    expect(normalizeText("")).toBe("");
  });
});
