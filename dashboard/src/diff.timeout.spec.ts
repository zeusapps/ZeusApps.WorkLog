import { describe, it, expect, vi } from "vitest";
import { readMarks, sourceSegments } from "./diff";
import { renderMarkdown } from "./markdown";

// jsdiff answers undefined when its timeout aborts the comparison.
vi.mock("diff", () => ({ diffLines: vi.fn(() => undefined) }));

describe("a comparison that cannot finish", () => {
  it("is reported as incomplete without positions, and the document still renders unmarked", () => {
    const marks = readMarks("a\nb\n", "a\nc\n");
    expect(marks).toEqual({ changed: [], removed: [], incomplete: true });
    expect(renderMarkdown("a\nc\n", false, marks).html).not.toContain("changed-since-read");
    expect(sourceSegments("a\nc\n", marks)).toEqual([{ text: "a\nc\n", changed: false }]);
  });
});
