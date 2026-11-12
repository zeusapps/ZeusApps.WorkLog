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
  /** The yyyy-MM-dd prefix of the archive directory; null for an active change or an undated archive. */
  archivedOn: string | null;
  repositories: string[];
  documents: { path: string; label: string; fingerprint: string }[];
  completedTasks: number | null;
  totalTasks: number | null;
  error: string | null;
}
/** First and latest recorded observation of a change name in one repository. */
export interface ChangeActivity {
  repository: string;
  change: string;
  firstAt: string;
  lastAt: string;
  lastStatus: string;
}
export type EditorPreset = "vscode" | "cursor" | "system";
export interface Catalog {
  repositories: Repository[];
  sources: Source[];
  changes: Change[];
  launchRepository: string | null;
  revision: string;
  notices: string[];
  journalSnapshot: number;
  digestRevision?: number;
}
export interface TimelineQuery {
  from: string;
  to: string;
  timezone: string;
  groupBy: string;
  repositories: string[];
  snapshot?: number;
  offset?: number;
  /** Optional History ticket filter; sent only when set. */
  ticket?: string;
}
/** A recorded issue reference; WorkLog never contacts the issue service. */
export interface Ticket {
  provider: string;
  scope: string;
  id: string;
  url?: string | null;
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
  tickets: Ticket[];
  observationCount: number;
  visibleCount: number;
  clippedStart: boolean;
}
export interface Insight {
  itemId: string;
  title: string;
  repository: string;
  at: string;
  tickets?: Ticket[];
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
  tickets: Ticket[];
  /** Ticket ID taken from the branch name but not qualified; shown without a link. */
  extractedTicketId?: string | null;
}
/** One work item of a daily digest section: its latest observation of the day and the rest, collapsed. */
export interface DailyItem {
  /** The ticket-grouped work identity, or `adhoc:<entry id>` for one unlinked observation. */
  id: string;
  title: string;
  repository: string;
  tickets: Ticket[];
  changes: string[];
  /** The latest recorded status as of the end of the digest's day (as of now for today's items). */
  status: string;
  primary: Entry;
  others: Entry[];
}
/**
 * A stored agent digest: the sorted IDs of the observations it covers, its readable flattened text, and, for a structured digest,
 * its Done / Next / Blockers bullets. The lists are omitted by the server when there are no valid parts (and by an older server).
 */
export interface DayDigest {
  covers: string[];
  text: string;
  done?: string[];
  next?: string[];
  blockers?: string[];
}
export interface DailyDigestIssue {
  category: string;
  message: string;
}
/** The last recorded day (or the selected one), its neighbours, and the current day's lines so far. `day` is null without earlier work. */
export interface DailyDigest {
  day: string | null;
  previousDay: string | null;
  nextDay: string | null;
  items: DailyItem[];
  today: DailyItem[];
  /** Stored agent digests of the listed items; absent from an older server. */
  digests?: DayDigest[];
  /** Optional private status when saved digest reads fail; absent from older servers and when no store is available. */
  digestIssue?: DailyDigestIssue | null;
}
export interface ObservationPage {
  entries: Entry[];
  total: number;
  nextOffset: number | null;
}
/** Observations with neither a ticket nor a change in a time window, newest first. `truncated` is set when more exist than were returned. */
export interface UnlinkedPage {
  snapshot: number;
  entries: Entry[];
  truncated: boolean;
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
    ticket?: string;
  };
  lastRoute?: string;
  documents?: Record<string, string>;
  positions?: Record<string, number>;
  editor?: EditorPreset;
  /** The dashboard-wide priority order of board cards, by card key. Absent until the user reorders. */
  board?: { order?: string[] };
  /** How Copy for daily formats the text; plain text without links until the user chooses. */
  daily?: { format?: "text" | "markdown"; links?: boolean };
}
