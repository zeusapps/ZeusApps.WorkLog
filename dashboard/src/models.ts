import type { HistoryRange } from "./range";

export interface Repository {
  id: string;
  label: string;
}
export interface Source {
  id: string;
  repositories: string[];
  location: string;
  error: string | null;
}
export interface Change {
  sourceId: string;
  name: string;
  directoryName: string;
  variant: string;
  archived: boolean;
  repositories: string[];
  documents: { path: string; label: string; fingerprint: string }[];
  completedTasks: number | null;
  totalTasks: number | null;
  error: string | null;
}
export interface Catalog {
  repositories: Repository[];
  sources: Source[];
  changes: Change[];
  launchRepository: string | null;
  revision: string;
  notices: string[];
  journalSnapshot: number;
}
export interface TimelineQuery {
  from: string;
  to: string;
  timezone: string;
  groupBy: string;
  repositories: string[];
  snapshot?: number;
  offset?: number;
}
export interface Row {
  id: string;
  repository: string;
  title: string;
  status: string;
  firstAt: string;
  startedAt: string | null;
  lastAt: string;
  elapsedSeconds: number;
  activityDates: string[];
  completionDates: string[];
  resumedDates: string[];
  changes: string[];
  observationCount: number;
  visibleCount: number;
  clippedStart: boolean;
}
export interface Insight {
  itemId: string;
  title: string;
  repository: string;
  at: string;
}
export interface Timeline {
  snapshot: number;
  rows: Row[];
  total: number;
  nextOffset: number | null;
  insights: {
    longestSpans: Row[];
    completions: Insight[];
    subsequentWork: Insight[];
    days: { date: string; items: number }[];
  };
}
export interface Entry {
  id: string;
  repository: string;
  change: string | null;
  status: string;
  summary: string | null;
  occurredAt: string;
  agent: string | null;
  branch: string | null;
  tickets: { id: string; provider: string; scope: string; url?: string }[];
}
export interface ObservationPage {
  entries: Entry[];
  total: number;
  nextOffset: number | null;
}
export interface DocumentRequest {
  sourceId: string;
  change: string;
  path: string;
  variant?: string;
  knownRevision?: string;
}
/** Answer to a document request whose knownRevision is still current: no text is sent. */
export interface DocumentUnchanged {
  revision: string;
  unchanged: true;
}
export interface DocumentContent extends DocumentRequest {
  location: string;
  revision: string;
  text: string;
  archived: boolean;
  canComment: boolean;
  tasks: { number: string | null; text: string; line: number }[];
}
export interface Match {
  sourceId: string;
  change: string;
  path: string;
  repositories: string[];
  archived: boolean;
  line: number;
  section: string | null;
  excerpt: string;
  variant: string;
}
export interface SearchResults {
  matches: Match[];
  limitReached: boolean;
  unavailable: string[];
}
/** An open review comment as the overview lists it. */
export interface ReviewItem {
  sourceId: string;
  change: string;
  variant: string;
  path: string;
  archived: boolean;
  repositories: string[];
  line: number;
  id: string | null;
  quote: string;
  comment: string;
  truncated: boolean;
}
export interface ReviewList {
  items: ReviewItem[];
  unavailable: string[];
}
/** The document text as the user last read it. */
export interface ReadBaseline {
  revision: string;
  text: string;
}
/** A fingerprint is `length:lastWriteTicks` as text: ticks are too large for a JavaScript number to hold exactly. */
export interface ReadMarkResult {
  revision: string;
  fingerprint: string;
}
export interface ReadSummary {
  /** The fingerprint stored when each remembered document was last read, by baseline key. */
  entries: Record<string, string>;
}
export interface Selection {
  start: number;
  end: number;
  text: string;
}
export interface Preferences {
  schemaVersion: number;
  theme?: "dark" | "light";
  repositories?: string[] | null;
  history?: {
    range?: HistoryRange;
    /** Written by earlier versions; never written now. */
    from?: string;
    to?: string;
    timezone?: string;
    groupBy?: string;
    scale?: string;
  };
  lastRoute?: string;
  documents?: Record<string, string>;
  positions?: Record<string, number>;
}
