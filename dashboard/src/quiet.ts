import { localDate } from "./api";

/** An active change is quiet when its latest recorded observation is more than this many days old. */
export const quietDays = 14;

export type Activity =
  | { kind: "none" }
  | { kind: "recent" }
  | { kind: "quiet"; lastAt: string };

function dayNumber(date: string): number {
  return Math.floor(Date.parse(date + "T00:00:00Z") / 86400000);
}

/**
 * Classifies a change by its latest recorded observation in calendar days of `zone`: no observation, recent
 * (up to 14 days ago), or quiet (older). A quiet result carries the latest recorded instant for display.
 */
export function activityOf(lastAt: string | null | undefined, now: Date, zone: string): Activity {
  if (!lastAt) return { kind: "none" };
  const last = new Date(lastAt);
  if (Number.isNaN(last.getTime())) return { kind: "none" };
  const age = dayNumber(localDate(now, zone)) - dayNumber(localDate(last, zone));
  return age > quietDays ? { kind: "quiet", lastAt } : { kind: "recent" };
}

