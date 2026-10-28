import { describe, it, expect } from "vitest";
import { activityOf } from "./quiet";

const now = new Date("2026-10-28T12:00:00Z");
const ago = (days: number) => new Date(now.getTime() - days * 86400000).toISOString();

describe("activityOf", () => {
  it("reports no activity without an observation", () => {
    expect(activityOf(undefined, now, "UTC")).toEqual({ kind: "none" });
    expect(activityOf(null, now, "UTC")).toEqual({ kind: "none" });
  });
  it("is recent at 13 days and quiet at 15 days", () => {
    expect(activityOf(ago(13), now, "UTC")).toEqual({ kind: "recent" });
    expect(activityOf(ago(14), now, "UTC")).toEqual({ kind: "recent" });
    expect(activityOf(ago(15), now, "UTC")).toEqual({ kind: "quiet", lastAt: ago(15) });
    expect(activityOf(ago(20), now, "UTC")).toEqual({ kind: "quiet", lastAt: ago(20) });
  });
  it("counts days in the selected timezone", () => {
    const last = "2026-10-14T05:00:00Z";
    expect(activityOf(last, now, "UTC").kind).toBe("recent");
    expect(activityOf(last, now, "Pacific/Auckland").kind).toBe("quiet");
  });
});
