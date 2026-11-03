import { describe, it, expect, vi } from "vitest";
import { diffTimeoutMilliseconds, readMarks, sourceSegments } from "./diff";
import { renderMarkdown } from "./markdown";

describe("a comparison that cannot finish", () => {
  // jsdiff answers undefined when its timeout aborts the comparison.
  const unfinished = vi.fn(() => undefined);

  it("is reported as incomplete without positions, and the document still renders unmarked", () => {
    const marks = readMarks("a\nb\n", "a\nc\n", unfinished);
    expect(marks).toEqual({ changed: [], removed: [], incomplete: true });
    expect(renderMarkdown("a\nc\n", false, marks).html).not.toContain("changed-since-read");
    expect(sourceSegments("a\nc\n", marks)).toEqual([{ text: "a\nc\n", changed: false }]);
  });

  it("is asked to finish within the diff timeout", () => {
    readMarks("a\nb\n", "a\nc\n", unfinished);
    expect(unfinished).toHaveBeenLastCalledWith("a\nb\n", "a\nc\n", { timeout: diffTimeoutMilliseconds });
    expect(diffTimeoutMilliseconds).toBe(3000);
  });
});
