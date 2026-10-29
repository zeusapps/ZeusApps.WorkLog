import { activityFor } from "./archive";
import { activityOf } from "./quiet";
import type { Change, ChangeActivity, Entry, Row, Ticket } from "./models";

/** Ticket and ad-hoc cards leave the board this many days after their latest observation. */
export const boardWindowDays = 7;
/** The most entries the saved priority order keeps. */
export const orderLimit = 200;
/** Ticket rank keys use this many hex characters of the work identity hash, which keeps the saved order small. */
export const ticketKeyLength = 16;

export type Stage = "proposed" | "in_progress" | "done";
export type CardKind = "change" | "ticket" | "adhoc";
export const stages: readonly Stage[] = ["proposed", "in_progress", "done"];

export interface BoardCard {
  kind: CardKind;
  /** The rank key: stable across refreshes and column changes. */
  key: string;
  stage: Stage;
  title: string;
  repositories: string[];
  /** The latest recorded observation; null when no activity is recorded. */
  lastAt: string | null;
  /** The latest recorded status; null when no activity is recorded. */
  status: string | null;
  completedTasks: number | null;
  totalTasks: number | null;
  /** The archived change a ticket card follows up; null otherwise. */
  followUpTo: string | null;
  tickets: Ticket[];
  /** A ticket ID taken from a branch name without a qualified provider; shown without a link. */
  extractedTicketId: string | null;
  /** The source objects, for the template. Exactly the one matching `kind` is set. */
  change: Change | null;
  /** Every catalog copy of the change (for example a checkout and its worktree); `change` is the first. Empty for other kinds. */
  copies: Change[];
  /** The documents of every copy, one entry per path, in the order of the copies. Empty for other kinds. */
  documents: Change["documents"];
  row: Row | null;
  entry: Entry | null;
}

export type Board = Record<Stage, BoardCard[]>;

export interface BoardInput {
  /** The catalog changes of the selected repositories, active and archived. Archived ones identify follow-ups. */
  changes: readonly Change[];
  /** Recorded activity per change name; null while it is not loaded for the selection, which leaves change cards out. */
  activity: readonly ChangeActivity[] | null;
  /** Ticket-grouped timeline rows of the last 7 days. */
  rows: readonly Row[];
  /** Observations of the last 7 days with neither a ticket nor a change. */
  unlinked: readonly Entry[];
  /** The saved priority order. */
  order: readonly string[];
}

const dayMilliseconds = 86400000;
const noSummary = "No summary recorded";

export function changeKey(change: Pick<Change, "repositories" | "name">): string {
  return "change:" + [...change.repositories].sort().join(",") + "/" + change.name;
}

function expired(lastAt: string, now: Date): boolean {
  return now.getTime() - Date.parse(lastAt) >= boardWindowDays * dayMilliseconds;
}

function ticketKey(ticket: Ticket): string {
  return ticket.provider + "|" + ticket.scope + "|" + ticket.id;
}

/** The column of a change card: done by ticked tasks or recorded status, otherwise in progress by any observation or ticked task. */
export function changeStage(
  completedTasks: number | null,
  totalTasks: number | null,
  activity: Pick<ChangeActivity, "lastStatus"> | null,
): Stage {
  const done = (totalTasks !== null && totalTasks > 0 && completedTasks === totalTasks) || activity?.lastStatus === "done";
  if (done) return "done";
  return activity !== null || (completedTasks ?? 0) > 0 ? "in_progress" : "proposed";
}

function emptyBoard(): Board {
  return { proposed: [], in_progress: [], done: [] };
}

function changeCards(input: BoardInput, activity: readonly ChangeActivity[]): BoardCard[] {
  // A change present in several sources is one card; its copies stay available for the copy chooser.
  const groups = new Map<string, Change[]>();
  for (const change of input.changes.filter((c) => !c.archived)) {
    const key = changeKey(change);
    groups.set(key, [...(groups.get(key) ?? []), change]);
  }
  return [...groups.entries()].map(([key, unordered]) => {
    // The copy with the most ticked tasks leads (ties by source id), so the card does not depend on catalog order.
    const copies = [...unordered].sort(
      (a, b) => (b.completedTasks ?? 0) - (a.completedTasks ?? 0) || (a.sourceId < b.sourceId ? -1 : a.sourceId > b.sourceId ? 1 : 0),
    );
    const change = copies[0];
    const documents = new Map<string, Change["documents"][number]>();
    for (const copy of copies) for (const document of copy.documents) if (!documents.has(document.path)) documents.set(document.path, document);
    const latest = activityFor(change, activity);
    const tickets = new Map<string, Ticket>();
    for (const row of input.rows)
      if (change.repositories.includes(row.repository) && row.changes.includes(change.name))
        for (const ticket of row.tickets) tickets.set(ticketKey(ticket), ticket);
    return {
      kind: "change" as const,
      key,
      stage: changeStage(change.completedTasks, change.totalTasks, latest),
      title: change.name,
      repositories: change.repositories,
      lastAt: latest?.lastAt ?? null,
      status: latest?.lastStatus ?? null,
      completedTasks: change.completedTasks,
      totalTasks: change.totalTasks,
      followUpTo: null,
      tickets: [...tickets.values()],
      extractedTicketId: null,
      change,
      copies,
      documents: [...documents.values()],
      row: null,
      entry: null,
    };
  });
}

/**
 * Ticket cards: ticket-grouped rows without an active change, and change-grouped rows whose changes are all archived.
 * Rows of an active change are not cards; their tickets show on that change's card.
 */
function ticketCards(input: BoardInput, now: Date): BoardCard[] {
  const cards: BoardCard[] = [];
  const archivedIn = (row: Row, name: string) =>
    input.changes.some((c) => c.archived && c.name === name && c.repositories.includes(row.repository));
  for (const row of input.rows) {
    const active = input.changes.some(
      (c) => !c.archived && c.repositories.includes(row.repository) && row.changes.includes(c.name),
    );
    if (active || expired(row.lastAt, now)) continue;
    const followUp = row.changes.length > 0 && row.changes.every((name) => archivedIn(row, name));
    // Without tickets only an archived change's own entries qualify; the unnamed bucket and unknown changes are not cards.
    if (row.tickets.length === 0 && !followUp) continue;
    cards.push({
      kind: "ticket",
      key: "ticket:" + row.id.slice(0, ticketKeyLength),
      stage: row.status === "done" ? "done" : "in_progress",
      title: row.title,
      repositories: [row.repository],
      lastAt: row.lastAt,
      status: row.status,
      completedTasks: null,
      totalTasks: null,
      followUpTo: followUp ? row.changes.join(", ") : null,
      tickets: row.tickets,
      extractedTicketId: null,
      change: null,
      copies: [],
      documents: [],
      row,
      entry: null,
    });
  }
  return cards;
}

function adhocCards(input: BoardInput, now: Date): BoardCard[] {
  return input.unlinked
    .filter((entry) => entry.change === null && entry.tickets.length === 0 && !expired(entry.occurredAt, now))
    .map((entry) => ({
      kind: "adhoc" as const,
      key: "adhoc:" + entry.id,
      stage: entry.status === "done" ? ("done" as const) : ("in_progress" as const),
      title: entry.summary?.trim() || noSummary,
      repositories: [entry.repository],
      lastAt: entry.occurredAt,
      status: entry.status,
      completedTasks: null,
      totalTasks: null,
      followUpTo: null,
      tickets: [],
      extractedTicketId: entry.extractedTicketId ?? null,
      change: null,
      copies: [],
      documents: [],
      row: null,
      entry,
    }));
}

/**
 * Orders cards: unranked first (newest activity first, then cards without activity by name), then ranked cards in priority order.
 * Used by the Ongoing board and by any view that applies the same dashboard-wide order.
 */
export function byPriority(order: readonly string[]): (a: BoardCard, b: BoardCard) => number {
  const rank = new Map<string, number>();
  order.forEach((key, index) => {
    if (!rank.has(key)) rank.set(key, index);
  });
  const group = (card: BoardCard) => (rank.has(card.key) ? 2 : card.lastAt !== null ? 0 : 1);
  return (a, b) => {
    const left = group(a);
    const right = group(b);
    if (left !== right) return left - right;
    if (left === 2) return rank.get(a.key)! - rank.get(b.key)!;
    if (left === 0) {
      const delta = Date.parse(b.lastAt!) - Date.parse(a.lastAt!);
      if (delta !== 0) return delta;
    }
    return a.title < b.title ? -1 : a.title > b.title ? 1 : a.key < b.key ? -1 : a.key > b.key ? 1 : 0;
  };
}

/** Builds the three columns, each ordered by priority. Pure: the same input and clock give the same board. */
export function buildBoard(input: BoardInput, now: Date): Board {
  const cards = [
    ...(input.activity ? changeCards(input, input.activity) : []),
    ...ticketCards(input, now),
    ...adhocCards(input, now),
  ];
  const compare = byPriority(input.order);
  const board = emptyBoard();
  for (const card of cards) board[card.stage].push(card);
  for (const stage of stages) board[stage].sort(compare);
  return board;
}

/** Whether a card's change name, ticket IDs, or title contain the text, ignoring case. An empty filter matches everything. */
export function matchesFilter(card: BoardCard, text: string): boolean {
  const needle = text.trim().toLowerCase();
  if (!needle) return true;
  const haystack = [
    card.title,
    card.change?.name,
    card.followUpTo,
    card.extractedTicketId,
    ...card.tickets.map((t) => t.id),
  ];
  return haystack.some((value) => value?.toLowerCase().includes(needle));
}

export function filterBoard(board: Board, text: string): Board {
  const result = emptyBoard();
  for (const stage of stages) result[stage] = board[stage].filter((card) => matchesFilter(card, text));
  return result;
}

export type MoveAction = "up" | "down" | "top";

/**
 * Where a card lands when it moves within the column's displayed keys. Moves skip cards hidden by the filter:
 * `visible` lists the keys that are shown, and up and down pass the neighbouring visible card.
 * Returns the new key sequence of the whole column, or null when the card cannot move that way.
 */
export function moveWithin(column: readonly string[], visible: ReadonlySet<string>, key: string, action: MoveAction): string[] | null {
  const from = column.indexOf(key);
  if (from < 0) return null;
  let to = -1;
  if (action === "top") {
    to = 0;
  } else if (action === "up") {
    for (let i = from - 1; i >= 0; i--)
      if (visible.has(column[i])) {
        to = i;
        break;
      }
  } else {
    for (let i = from + 1; i < column.length; i++)
      if (visible.has(column[i])) {
        to = i;
        break;
      }
  }
  if (to < 0 || to === from) return null;
  const result = column.filter((k) => k !== key);
  result.splice(to, 0, key);
  return result;
}

/** The column's keys with `key` placed before (or, with `after`, behind) `target`. Null when nothing changes. */
export function dropWithin(column: readonly string[], key: string, target: string, after: boolean): string[] | null {
  if (key === target || !column.includes(key) || !column.includes(target)) return null;
  const result = column.filter((k) => k !== key);
  result.splice(result.indexOf(target) + (after ? 1 : 0), 0, key);
  return result.every((k, i) => k === column[i]) ? null : result;
}

/**
 * Ranks every card of a column in the given sequence. The column's keys leave the order and re-enter as one run at the position of the
 * first of them that was ranked (or at the start), so other columns keep their relative order.
 */
export function rankColumn(order: readonly string[], column: readonly string[]): string[] {
  const moved = new Set(column);
  const at = order.findIndex((key) => moved.has(key));
  const rest = order.filter((key) => !moved.has(key));
  rest.splice(at < 0 ? 0 : at, 0, ...column);
  return rest;
}

/** Drops keys that are not live, then the lowest-ranked entries beyond the limit. Also removes duplicates. */
export function pruneOrder(order: readonly string[], live: ReadonlySet<string>, limit = orderLimit): string[] {
  return [...new Set(order.filter((key) => live.has(key)))].slice(0, limit);
}

/**
 * Whether a card shows the quiet indication: only an In-progress change card whose latest recorded observation is more than
 * 14 days old in the calendar days of `zone`.
 */
export function isQuiet(card: BoardCard, now: Date, zone: string): boolean {
  return card.kind === "change" && card.stage === "in_progress" && activityOf(card.lastAt, now, zone).kind === "quiet";
}

/** Drops the lowest-ranked keys from the end of the order until `size(order)` is within `limit`. */
export function fitOrder(order: readonly string[], size: (order: readonly string[]) => number, limit: number): string[] {
  const kept = [...order];
  while (kept.length > 0 && size(kept) > limit) kept.pop();
  return kept;
}
