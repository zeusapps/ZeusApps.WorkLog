export const liveDomains = ["catalog", "documents", "journal", "digests", "reads"] as const;
export type LiveDomain = (typeof liveDomains)[number];

export interface RevisionSnapshot {
  instance: string;
  catalog: number;
  documents: number;
  journal: number;
  digests: number;
  reads: number;
  dirty: string[];
}

export type RevisionVector = Pick<RevisionSnapshot, LiveDomain>;

export interface RevisionChange {
  changed: boolean;
  instanceChanged: boolean;
  domains: LiveDomain[];
  target: RevisionSnapshot;
}

const initialVector = (): RevisionVector => ({ catalog: -1, documents: -1, journal: -1, digests: -1, reads: -1 });

/** Tracks monotonic per-domain targets without relying on notification order or delivery. */
export class RevisionTracker {
  private currentInstance: string | null = null;
  private desiredVector = initialVector();
  private appliedVector = initialVector();
  private currentTarget: RevisionSnapshot | null = null;

  accept(snapshot: RevisionSnapshot): RevisionChange {
    const valid = this.valid(snapshot);
    if (this.currentInstance === null || snapshot.instance !== this.currentInstance) {
      this.currentInstance = snapshot.instance;
      this.desiredVector = this.vector(valid);
      this.appliedVector = initialVector();
      this.currentTarget = valid;
      return { changed: true, instanceChanged: true, domains: [...liveDomains], target: valid };
    }

    const changed: LiveDomain[] = [];
    const merged = { ...this.desiredVector };
    for (const domain of liveDomains) {
      if (valid[domain] > merged[domain]) {
        merged[domain] = valid[domain];
        changed.push(domain);
      }
    }
    if (changed.length === 0) {
      return { changed: false, instanceChanged: false, domains: [], target: this.currentTarget ?? valid };
    }
    this.desiredVector = merged;
    this.currentTarget = { ...valid, ...merged, dirty: [...valid.dirty] };
    return { changed: true, instanceChanged: false, domains: changed, target: this.currentTarget };
  }

  pending(): LiveDomain[] {
    return liveDomains.filter((domain) => this.appliedVector[domain] < this.desiredVector[domain]);
  }

  desired(): RevisionSnapshot | null {
    return this.currentTarget;
  }

  applied(): RevisionVector {
    return { ...this.appliedVector };
  }

  markApplied(instance: string, target: RevisionSnapshot, domains: readonly LiveDomain[]) {
    if (this.currentInstance !== instance || target.instance !== instance) return;
    for (const domain of domains) {
      if (target[domain] > this.appliedVector[domain])
        this.appliedVector[domain] = Math.min(target[domain], this.desiredVector[domain]);
    }
  }

  reset() {
    this.currentInstance = null;
    this.desiredVector = initialVector();
    this.appliedVector = initialVector();
    this.currentTarget = null;
  }

  private vector(snapshot: RevisionSnapshot): RevisionVector {
    return {
      catalog: snapshot.catalog,
      documents: snapshot.documents,
      journal: snapshot.journal,
      digests: snapshot.digests,
      reads: snapshot.reads,
    };
  }

  private valid(snapshot: RevisionSnapshot): RevisionSnapshot {
    if (!snapshot || typeof snapshot.instance !== "string" || !snapshot.instance)
      throw new Error("Invalid live revision instance.");
    for (const domain of liveDomains)
      if (!Number.isSafeInteger(snapshot[domain]) || snapshot[domain] < 0)
        throw new Error("Invalid live revision.");
    if (!Array.isArray(snapshot.dirty) || snapshot.dirty.some((value) => typeof value !== "string"))
      throw new Error("Invalid live dirty hints.");
    return { ...snapshot, dirty: [...snapshot.dirty] };
  }
}

interface RefreshJob {
  token: string;
  run: () => Promise<boolean>;
}

interface RefreshState {
  applied: string | null;
  desired: RefreshJob | null;
  running: Promise<boolean> | null;
  inFlightToken: string | null;
  failed: boolean;
  cancelled: boolean;
}

/** Coalesces one resource to one in-flight read and one latest follow-up. Failed work stays dirty until retried. */
export class RefreshQueue {
  private readonly states = new Map<string, RefreshState>();

  enqueue(resource: string, token: string, run: () => Promise<boolean>, retryFailed = false): Promise<boolean> {
    const state = this.state(resource);
    if (state.applied === token && !state.failed) return Promise.resolve(true);
    if (state.running) {
      if (state.inFlightToken === token || state.desired?.token === token) return state.running;
      state.desired = { token, run };
      return state.running;
    }
    if (state.failed && !retryFailed && state.desired?.token === token)
      return Promise.resolve(false);
    state.desired = { token, run };
    state.failed = false;
    const task = this.drain(resource, state);
    state.running = task;
    return task;
  }

  dirty(resource: string): boolean {
    const state = this.states.get(resource);
    return !!state && (state.failed || state.desired !== null);
  }

  retry(resource: string): Promise<boolean> {
    const state = this.states.get(resource);
    if (!state?.desired) return Promise.resolve(true);
    if (state.running) return state.running;
    const job = state.desired;
    return this.enqueue(resource, job.token, job.run, true);
  }

  clear() {
    for (const state of this.states.values()) {
      state.cancelled = true;
      state.desired = null;
    }
    this.states.clear();
  }

  private async drain(resource: string, state: RefreshState): Promise<boolean> {
    let result = false;
    try {
      while (state.desired) {
        if (state.cancelled) return false;
        const job = state.desired;
        state.desired = null;
        state.inFlightToken = job.token;
        try {
          result = await job.run();
        } catch {
          result = false;
        }
        if (state.cancelled) return false;
        if (!result) {
          state.failed = true;
          state.desired ??= job;
          state.inFlightToken = null;
          break;
        }
        state.applied = job.token;
        state.inFlightToken = null;
      }
      return result && !state.failed;
    } finally {
      state.running = null;
      state.inFlightToken = null;
      // A newer job can be added after the loop observes an empty queue but before it settles.
      if (!state.cancelled && state.desired && !state.failed) {
        const next = this.drain(resource, state);
        state.running = next;
        await next;
      }
    }
  }

  private state(resource: string): RefreshState {
    let value = this.states.get(resource);
    if (!value) {
      value = { applied: null, desired: null, running: null, inFlightToken: null, failed: false, cancelled: false };
      this.states.set(resource, value);
    }
    return value;
  }
}

export function revisionToken(snapshot: RevisionSnapshot, domains: readonly LiveDomain[]): string {
  return snapshot.instance + ":" + domains.map((domain) => snapshot[domain]).join(":");
}
