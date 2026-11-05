import { describe, it, expect, vi } from "vitest";
import { diffMaxEditLength, diffTimeoutMilliseconds, readMarks, sourceSegments } from "./diff";
import { renderMarkdown } from "./markdown";

describe("a comparison that cannot finish", () => {
  // jsdiff answers undefined when its timeout aborts the comparison.
  const unfinished = vi.fn(() => undefined);

  it("is reported as incomplete without inventing passage positions", () => {
    const marks = readMarks("a\nb\n", "a\nc\n", unfinished);
    expect(marks).toEqual({ changed: [], removed: [], incomplete: true });
    expect(renderMarkdown("a\nc\n", false, marks).html).not.toContain("changed-since-read");
    expect(sourceSegments("a\nc\n", marks)).toEqual([{ text: "a\nc\n", changed: false }]);
  });

  it("uses both the synchronous timeout and edit-distance bound", () => {
    readMarks("a\nb\n", "a\nc\n", unfinished);
    expect(unfinished).toHaveBeenLastCalledWith("a\nb\n", "a\nc\n", {
      timeout: diffTimeoutMilliseconds,
      maxEditLength: diffMaxEditLength,
    });
    expect(diffTimeoutMilliseconds).toBe(50);
    expect(diffMaxEditLength).toBe(2000);
  });
});
