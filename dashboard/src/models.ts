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
  documents: { path: string; label: string }[];
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
    from: string;
    to: string;
    timezone: string;
    groupBy: string;
    scale: string;
  };
  lastRoute?: string;
  documents?: Record<string, string>;
  positions?: Record<string, number>;
}
