import type { App } from "./app";
import { LiveDomain, RevisionSnapshot, RevisionTracker } from "./live-updates";

const allDomains: LiveDomain[] = ["catalog", "documents", "journal", "digests", "reads"];

export function primeLiveRevision(app: App, patch: Partial<RevisionSnapshot> = {}) {
  const tracker = app["revisionTracker"] as RevisionTracker;
  const value: RevisionSnapshot = {
    instance: "fixture-instance",
    catalog: 1,
    documents: 1,
    journal: 1,
    digests: 1,
    reads: 1,
    dirty: [],
    ...patch,
  };
  tracker.accept(value);
  tracker.markApplied(value.instance, value, allDomains);
  app["journalSnapshot"] = value.journal;
  app["digestRevision"] = value.digests;
  return value;
}

export function revisionFrom(app: App, patch: Partial<RevisionSnapshot> = {}): RevisionSnapshot {
  const current = (app["revisionTracker"] as RevisionTracker).desired();
  return {
    instance: current?.instance ?? "fixture-instance",
    catalog: current?.catalog ?? 1,
    documents: current?.documents ?? 1,
    journal: current?.journal ?? 1,
    digests: current?.digests ?? 1,
    reads: current?.reads ?? 1,
    dirty: [],
    ...patch,
  };
}

export function sendRevision(app: App, patch: Partial<RevisionSnapshot> = {}) {
  return app["receiveRevision"](revisionFrom(app, patch));
}
