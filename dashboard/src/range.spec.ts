import { describe, it, expect } from "vitest";
import { boundary, localDate } from "./api";
import {
  effectiveRange,
  normalizeRange,
  rangeLabel,
  touchRecent,
  validDate,
  validTimezone,
} from "./range";

describe("history range model", () => {
  it("evaluates a relative range against the date it is loaded on", () => {
    const range = normalizeRange({ kind: "relative", days: 30 });
    const first = effectiveRange(range, "UTC", new Date("2026-10-24T12:00:00Z"));
    const later = effectiveRange(range, "UTC", new Date("2026-10-27T12:00:00Z"));
    expect(first).toEqual({ from: "2026-09-25", to: "2026-10-24" });
    expect(later).toEqual({ from: "2026-09-28", to: "2026-10-27" });
    expect(later.from).not.toBe(first.from);
    expect(effectiveRange({ kind: "relative", days: 7 }, "UTC", new Date("2026-10-27T12:00:00Z")))
      .toEqual({ from: "2026-10-21", to: "2026-10-27" });
  });

  it("uses the date in the selected timezone, not in the browser's", () => {
    const instant = new Date("2026-10-27T23:30:00Z");
    expect(effectiveRange({ kind: "relative", days: 7 }, "Pacific/Auckland", instant).to).toBe("2026-10-28");
    expect(effectiveRange({ kind: "relative", days: 7 }, "UTC", instant).to).toBe("2026-10-27");
  });

  it("keeps the dates of a custom range regardless of the current date", () => {
    const range = normalizeRange({ kind: "custom", from: "2026-01-02", to: "2026-01-20" });
    const dates = { from: "2026-01-02", to: "2026-01-20" };
    expect(effectiveRange(range, "UTC", new Date("2026-02-01T00:00:00Z"))).toEqual(dates);
    expect(effectiveRange(range, "UTC", new Date("2026-10-01T00:00:00Z"))).toEqual(dates);
    expect(rangeLabel(range)).toBe("2026-01-02 to 2026-01-20");
  });

  it("treats a saved range without a kind as the default relative 30 days", () => {
    const legacy = { from: "2026-01-01", to: "2026-01-31" };
    expect(normalizeRange(undefined)).toEqual({ kind: "relative", days: 30 });
    expect(normalizeRange(legacy)).toEqual({ kind: "relative", days: 30 });
    expect(normalizeRange({ kind: "relative", days: 12 })).toEqual({ kind: "relative", days: 30 });
    expect(normalizeRange({ kind: "custom", from: "2026-02-30", to: "2026-03-01" })).toEqual({ kind: "relative", days: 30 });
    expect(normalizeRange({ kind: "custom", from: "2026-03-02", to: "2026-03-01" })).toEqual({ kind: "relative", days: 30 });
  });

  it("validates dates and timezones", () => {
    expect(validDate("2026-02-28")).toBe(true);
    expect(validDate("2026-02-30")).toBe(false);
    expect(validDate("")).toBe(false);
    expect(validTimezone("Europe/Warsaw")).toBe(true);
    expect(validTimezone("Mars/Olympus")).toBe(false);
    expect(validTimezone("")).toBe(false);
  });

  it("keeps only the most recently used entries", () => {
    const map: Record<string, number> = {};
    for (let i = 0; i < 5; i++) touchRecent(map, "k" + i, i, 3);
    expect(Object.keys(map)).toEqual(["k2", "k3", "k4"]);
    touchRecent(map, "k2", 20, 3);
    touchRecent(map, "k5", 5, 3);
    expect(Object.keys(map)).toEqual(["k4", "k2", "k5"]);
    expect(map["k2"]).toBe(20);
  });
});

describe("day boundaries", () => {
  it("returns the first instant of the date in zones that skip midnight", () => {
    // Cairo and Beirut start daylight time at 00:00, so local 00:00 does not exist on these dates.
    expect(boundary("2026-04-24", "Africa/Cairo")).toBe("2026-04-23T22:00:00.000Z");
    expect(boundary("2026-03-29", "Asia/Beirut")).toBe("2026-03-28T22:00:00.000Z");
    for (const [date, zone] of [["2026-04-24", "Africa/Cairo"], ["2026-03-29", "Asia/Beirut"]]) {
      const start = new Date(boundary(date, zone));
      expect(localDate(start, zone)).toBe(date);
      expect(localDate(new Date(start.getTime() - 1000), zone) < date).toBe(true);
    }
  });

  it("keeps midnight boundaries in zones without a skipped midnight", () => {
    expect(boundary("2026-04-24", "UTC")).toBe("2026-04-24T00:00:00.000Z");
    expect(boundary("2026-04-24", "Europe/Warsaw")).toBe("2026-04-23T22:00:00.000Z");
    expect(boundary("2026-03-29", "Europe/Warsaw")).toBe("2026-03-28T23:00:00.000Z");
    expect(boundary("2026-10-25", "Europe/Warsaw")).toBe("2026-10-24T22:00:00.000Z");
    expect(boundary("2026-01-15", "America/New_York")).toBe("2026-01-15T05:00:00.000Z");
  });
});
