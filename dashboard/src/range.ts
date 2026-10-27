import { addDays, localDate } from "./api";

/** A remembered History range. Relative ranges roll forward; custom ranges keep their dates. */
export type HistoryRange =
  | { kind: "relative"; days: 7 | 30 | 90 }
  | { kind: "custom"; from: string; to: string };

export const rangePresets: readonly (7 | 30 | 90)[] = [7, 30, 90];
export const defaultRange: HistoryRange = { kind: "relative", days: 30 };

const datePattern = /^\d{4}-\d{2}-\d{2}$/;

export function validDate(value: unknown): value is string {
  return (
    typeof value === "string" &&
    datePattern.test(value) &&
    !Number.isNaN(Date.parse(value + "T00:00:00Z")) &&
    new Date(value + "T00:00:00Z").toISOString().startsWith(value)
  );
}

export function validTimezone(zone: unknown): zone is string {
  if (typeof zone !== "string" || !zone.trim()) return false;
  try {
    new Intl.DateTimeFormat("en", { timeZone: zone });
    return true;
  } catch {
    return false;
  }
}

/**
 * Reads a remembered range. Anything without a valid range kind, including the explicit
 * from/to dates saved by earlier versions, becomes the default relative range.
 */
export function normalizeRange(value: unknown): HistoryRange {
  const range = value as Record<string, unknown> | null | undefined;
  if (range?.["kind"] === "relative" && rangePresets.includes(range["days"] as 7))
    return { kind: "relative", days: range["days"] as 7 | 30 | 90 };
  if (
    range?.["kind"] === "custom" &&
    validDate(range["from"]) &&
    validDate(range["to"]) &&
    range["from"] <= range["to"]
  )
    return { kind: "custom", from: range["from"], to: range["to"] };
  return { ...defaultRange };
}

/** The inclusive dates a range covers when evaluated at `now` in `zone`. */
export function effectiveRange(
  range: HistoryRange,
  zone: string,
  now: Date,
): { from: string; to: string } {
  if (range.kind === "custom") return { from: range.from, to: range.to };
  const to = localDate(now, zone);
  return { from: addDays(to, 1 - range.days), to };
}

export function rangeLabel(range: HistoryRange): string {
  return range.kind === "relative"
    ? "last " + range.days + " days"
    : range.from + " to " + range.to;
}

/** Records a use of `key`, keeping only the `limit` most recently used entries (insertion order). */
export function touchRecent<T>(
  map: Record<string, T>,
  key: string,
  value: T,
  limit: number,
): void {
  delete map[key];
  map[key] = value;
  const keys = Object.keys(map);
  for (let i = 0; i < keys.length - limit; i++) delete map[keys[i]];
}
