import type { Change, ReviewItem, ReadSummary } from "./models";
import { parseReviews, type Review } from "./markdown";

export type ReviewScope = "active" | "archived" | "all";

export interface ReviewGroup {
  /** Identifies the change copy: its source and variant. */
  key: string;
  sourceId: string;
  variant: string;
  change: string;
  archived: boolean;
  repositories: string[];
  count: number;
  documents: { path: string; items: ReviewItem[] }[];
}

/** One change copy, the same identity the catalog and document requests use. */
export function changeKey(sourceId: string, variant: string): string {
  return sourceId + "/" + variant;
}
export function documentKey(sourceId: string, variant: string, path: string): string {
  return changeKey(sourceId, variant) + "/" + path;
}

export function inScope(item: { archived: boolean }, scope: ReviewScope): boolean {
  return scope === "all" || item.archived === (scope === "archived");
}

/** Groups listed comments by change, then by document, keeping the order the server listed them in. */
export function groupReviews(items: readonly ReviewItem[], scope: ReviewScope): ReviewGroup[] {
  const groups = new Map<string, ReviewGroup>();
  for (const item of items) {
    if (!inScope(item, scope)) continue;
    const key = changeKey(item.sourceId, item.variant);
    let group = groups.get(key);
    if (!group) {
      group = {
        key,
        sourceId: item.sourceId,
        variant: item.variant,
        change: item.change,
        archived: item.archived,
        repositories: item.repositories,
        count: 0,
        documents: [],
      };
      groups.set(key, group);
    }
    let document = group.documents.find((d) => d.path === item.path);
    if (!document) group.documents.push((document = { path: item.path, items: [] }));
    document.items.push(item);
    group.count++;
  }
  return [...groups.values()];
}

/** Open comments per change copy. */
export function countByChange(items: readonly ReviewItem[]): Map<string, number> {
  const counts = new Map<string, number>();
  for (const item of items) {
    const key = changeKey(item.sourceId, item.variant);
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  return counts;
}

/** Open comments per document of a change copy. */
export function countByDocument(items: readonly ReviewItem[]): Map<string, number> {
  const counts = new Map<string, number>();
  for (const item of items) {
    const key = documentKey(item.sourceId, item.variant, item.path);
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  return counts;
}

/** The key under which the server stores a document's read baseline. The variant is left out so an archive move keeps it. */
export function baselineKey(sourceId: string, change: string, path: string): string {
  return sourceId + ":" + change + ":" + path;
}

/** The documents of a change whose fingerprint differs from the one stored when they were last read. */
export function updatedDocuments(change: Change, summary: ReadSummary["entries"]): string[] {
  return change.documents
    .filter((document) => {
      const stored = summary[baselineKey(change.sourceId, change.name, document.path)];
      return stored !== undefined && stored !== document.fingerprint;
    })
    .map((document) => document.path);
}

/**
 * The review comments that were in the text last read and are gone from the current text. A comment with an id is the
 * same comment while the id is present; a legacy comment without one is the same only while its marker text is identical.
 */
export function removedReviews(baselineText: string, current: readonly Review[]): Review[] {
  const ids = new Set(current.map((review) => review.id).filter((id): id is string => !!id));
  const markers = new Set(current.map((review) => review.marker));
  return parseReviews(baselineText).filter((review) => (review.id ? !ids.has(review.id) : !markers.has(review.marker)));
}
