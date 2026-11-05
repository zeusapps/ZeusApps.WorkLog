import { diffLines, type Change } from "diff";

/** The longest the line diff may run before the changes are reported as not computable. */
export const diffTimeoutMilliseconds = 50;
/** Bound the edit distance so very different documents cannot consume unbounded synchronous work. */
export const diffMaxEditLength = 2000;

/** The line comparison readMarks runs. A seam so a test can supply one that cannot finish; jsdiff returns undefined on timeout. */
export type LineComparer = (before: string, after: string, options: { timeout: number; maxEditLength: number }) => Change[] | undefined;

/** A passage that was in the text last read but is gone: its lines and the current line it sat before. */
export interface RemovedHunk {
  /** Zero-based line of the current text the removed lines preceded. */
  line: number;
  text: string;
}

/**
 * Where the current text differs from the text last read. Lines are zero-based, the same coordinate as `data-start` and
 * `data-end`, and ranges are half-open.
 */
export interface ReadMarks {
  changed: [start: number, end: number][];
  removed: RemovedHunk[];
  /** The diff did not finish in time, so no positions are known. */
  incomplete?: boolean;
}

/** Line endings are unified and the text ends with a newline, so every line is a whole token. */
export function normalizeText(text: string): string {
  const unified = text.replace(/\r\n?/g, "\n");
  return unified && !unified.endsWith("\n") ? unified + "\n" : unified;
}

function lineCount(value: string): number {
  let count = 0;
  for (let i = value.indexOf("\n"); i >= 0; i = value.indexOf("\n", i + 1)) count++;
  return count;
}

/**
 * Compares the text last read with the current text line by line. Returns null when they hold the same lines.
 * Added and rewritten lines become `changed` ranges of the current text; deleted lines become `removed` hunks.
 */
export function readMarks(baseline: string, current: string, compare: LineComparer = diffLines): ReadMarks | null {
  const before = normalizeText(baseline);
  const after = normalizeText(current);
  if (before === after) return null;
  const parts = compare(before, after, { timeout: diffTimeoutMilliseconds, maxEditLength: diffMaxEditLength });
  if (!parts) return { changed: [], removed: [], incomplete: true };
  const marks: ReadMarks = { changed: [], removed: [] };
  let line = 0;
  for (const part of parts) {
    const count = lineCount(part.value);
    if (part.removed) marks.removed.push({ line, text: part.value });
    else if (part.added) {
      const last = marks.changed.at(-1);
      if (last && last[1] === line) last[1] += count;
      else marks.changed.push([line, line + count]);
      line += count;
    } else line += count;
  }
  return marks;
}

/** True when the half-open block `[start, end)` shares a line with a changed range. */
export function intersects(ranges: readonly [number, number][], start: number, end: number): boolean {
  return ranges.some(([from, to]) => from < end && to > start);
}

export interface SourceSegment {
  text: string;
  changed: boolean;
}

/** Splits source text into runs of unchanged and changed lines, so the source view needs one element per run, not per line. */
export function sourceSegments(text: string, marks: ReadMarks | null): SourceSegment[] {
  if (!marks || marks.changed.length === 0) return [{ text, changed: false }];
  const lines = text.split(/(?<=\n)/);
  const segments: SourceSegment[] = [];
  let at = 0;
  const take = (to: number, changed: boolean) => {
    if (to > at) segments.push({ text: lines.slice(at, to).join(""), changed });
    at = Math.max(at, to);
  };
  for (const [start, end] of marks.changed) {
    take(start, false);
    take(Math.min(end, lines.length), true);
  }
  take(lines.length, false);
  return segments;
}

const stopSelector = ".changed-since-read, .removed-since-read";

/**
 * The places Previous and Next change visit, in document order. Neighbouring changed blocks are one stop, so stepping
 * through a rewritten section does not take a keypress per paragraph.
 */
export function changeStops(root: ParentNode): HTMLElement[] {
  const all = Array.from(root.querySelectorAll<HTMLElement>(stopSelector));
  const members = new Set(all);
  return all.filter((element) => {
    const previous = element.previousElementSibling;
    return !(previous instanceof HTMLElement && members.has(previous));
  });
}
