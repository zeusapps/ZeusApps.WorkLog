import type { Change, ChangeActivity, EditorPreset } from "./models";

export interface ArchiveEntry {
  change: Change;
  /** The latest activity recorded for the change name in any of the change's repositories; null when none was recorded. */
  activity: ChangeActivity | null;
}

export const editorPresets: { value: EditorPreset; label: string }[] = [
  { value: "vscode", label: "VS Code" },
  { value: "cursor", label: "Cursor" },
  { value: "system", label: "System default app" },
];

export function editorLabel(editor: EditorPreset): string {
  return editorPresets.find((p) => p.value === editor)?.label ?? editor;
}

/** A remembered editor, or the default when the stored value is not one of the fixed presets. */
export function validEditor(value: unknown): EditorPreset {
  return editorPresets.some((p) => p.value === value) ? (value as EditorPreset) : "vscode";
}

/** The newest activity recorded for the change's logical name in any of its repositories. */
export function activityFor(change: Change, activity: readonly ChangeActivity[]): ChangeActivity | null {
  let latest: ChangeActivity | null = null;
  for (const item of activity)
    if (
      item.change === change.name &&
      change.repositories.includes(item.repository) &&
      (!latest || Date.parse(item.lastAt) > Date.parse(latest.lastAt))
    )
      latest = item;
  return latest;
}

/**
 * Archived changes in the selected repositories whose names contain the filter text (ignoring case), newest archive date first.
 * Entries without a date prefix follow the dated ones; equal dates are ordered by name, then by directory.
 */
export function archiveEntries(
  changes: readonly Change[],
  inScope: (repositories: string[]) => boolean,
  filter: string,
  activity: readonly ChangeActivity[],
): ArchiveEntry[] {
  const needle = filter.trim().toLowerCase();
  const compare = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0);
  return changes
    .filter((c) => c.archived && inScope(c.repositories) && (!needle || c.name.toLowerCase().includes(needle)))
    .sort(
      (a, b) =>
        (a.archivedOn === null ? 1 : 0) - (b.archivedOn === null ? 1 : 0) ||
        compare(b.archivedOn ?? "", a.archivedOn ?? "") ||
        compare(a.name, b.name) ||
        compare(a.directoryName, b.directoryName),
    )
    .map((change) => ({ change, activity: activityFor(change, activity) }));
}

/** The zero-based source line of the character that follows `preceding`: the number of line breaks in front of it. */
export function sourceLineAfter(preceding: string): number {
  let count = 0;
  for (let i = preceding.indexOf("\n"); i !== -1; i = preceding.indexOf("\n", i + 1)) count++;
  return count;
}

/** A rendered block: its first source line (zero-based) and its distance from the top of the viewport. */
export interface BlockPosition {
  start: number;
  top: number;
}

/**
 * The line to open in an editor for the reader's position: the first rendered block whose top is at or below the reader's
 * top edge. When every block starts above it (the reader is scrolled past the last block's start), the last block.
 */
export function firstVisibleLine(blocks: readonly BlockPosition[], readerTop: number): number {
  const sorted = blocks.filter((b) => Number.isFinite(b.start));
  if (sorted.length === 0) return 0;
  return (sorted.find((b) => b.top >= readerTop) ?? sorted[sorted.length - 1]).start;
}
