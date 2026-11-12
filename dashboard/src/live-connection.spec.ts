import { HttpError } from "@microsoft/signalr";
import { describe, expect, it, vi } from "vitest";
import { HubConnectionLike, LiveConnectionController, LiveConnectionStatus } from "./live-connection";
import { RevisionSnapshot } from "./live-updates";

const vector: RevisionSnapshot = {
  instance: "server",
  catalog: 1,
  documents: 2,
  journal: 3,
  digests: 4,
  reads: 5,
  dirty: ["documents"],
};

class FakeConnection implements HubConnectionLike {
  readonly order: string[] = [];
  readonly calls: string[] = [];
  status: "stopped" | "started" = "stopped";
  starts = 0;
  stops = 0;
  snapshot: RevisionSnapshot = vector;
  startResult: () => Promise<void> = async () => {};
  invokeResult: () => Promise<RevisionSnapshot> = async () => this.snapshot;
  private revisionHandler?: (value: RevisionSnapshot) => void;
  private closeHandler?: (error?: Error) => void;

  on(method: string, handler: (value: RevisionSnapshot) => void) {
    this.order.push("on:" + method);
    if (method === "Revision") this.revisionHandler = handler;
  }
  onclose(handler: (error?: Error) => void) { this.closeHandler = handler; }
  async start() {
    this.order.push("start");
    this.starts++;
    await this.startResult();
    this.status = "started";
  }
  async stop() { this.stops++; this.status = "stopped"; }
  invoke<T>(method: string): Promise<T> {
    this.calls.push(method);
    return this.invokeResult() as Promise<T>;
  }
  publish(value: RevisionSnapshot) { this.revisionHandler?.(value); }
  close(error?: Error) { this.status = "stopped"; this.closeHandler?.(error); }
}

function controller(connection: FakeConnection, options: Partial<ConstructorParameters<typeof LiveConnectionController>[0]> = {}) {
  const statuses: LiveConnectionStatus[] = [];
  const revisions: RevisionSnapshot[] = [];
  const snapshots: RevisionSnapshot[] = [];
  const unauthorized = vi.fn();
  const authorized = vi.fn();
  const value = new LiveConnectionController({
    create: () => connection,
    onRevision: (revision) => { revisions.push(revision); },
    onSnapshot: async (snapshot) => { snapshots.push(snapshot); return true; },
    onStatus: (status) => statuses.push(status),
    onUnauthorized: unauthorized,
    onAuthorized: authorized,
    delay: async () => {},
    random: () => 0,
    ...options,
  });
  return { value, statuses, revisions, snapshots, unauthorized, authorized };
}

describe("live SignalR lifecycle", () => {
  it("registers Revision before start and reports Live only after Snapshot reconciliation", async () => {
    const connection = new FakeConnection();
    const { value, statuses, snapshots } = controller(connection);
    expect(await value.start()).toBe(true);
    expect(connection.order).toEqual(["on:Revision", "start"]);
    expect(connection.calls).toEqual(["Snapshot"]);
    expect(snapshots).toEqual([vector]);
    expect(statuses.at(-1)).toBe("live");
    connection.publish(vector);
    expect(connection.order).toContain("on:Revision");
    await value.stop();
  });

  it("bounds initial connection attempts and stops after one authorization classification probe", async () => {
    const connection = new FakeConnection();
    connection.startResult = async () => { throw new Error("network unavailable"); };
    const probe = vi.fn(async () => true);
    const { value, statuses } = controller(connection, { proveAuthorization: probe });
    expect(await value.start()).toBe(false);
    expect(connection.starts).toBe(5);
    expect(probe).toHaveBeenCalledTimes(1);
    expect(statuses.at(-1)).toBe("paused");
    await value.stop();
  });

  it("halts automatic attempts on confirmed 401 and resumes only after explicit authorization proof", async () => {
    const connection = new FakeConnection();
    let authorized = false;
    connection.startResult = async () => {
      if (!authorized) throw new HttpError("Unauthorized", 401);
    };
    const probe = vi.fn(async () => authorized);
    const { value, unauthorized, authorized: authorizedCallback } = controller(connection, { proveAuthorization: probe });
    expect(await value.start()).toBe(false);
    expect(connection.starts).toBe(1);
    expect(unauthorized).toHaveBeenCalledTimes(1);

    expect(await value.retry()).toBe(false);
    expect(connection.starts).toBe(1);
    expect(probe).toHaveBeenCalledTimes(1);

    authorized = true;
    expect(await value.retry()).toBe(true);
    expect(connection.starts).toBe(2);
    expect(authorizedCallback).toHaveBeenCalledTimes(1);
    await value.stop();
  });

  it("keeps a failed data reconciliation paused and makes simultaneous Retry single-flight", async () => {
    const connection = new FakeConnection();
    let reconcile = async () => false;
    const { value, statuses } = controller(connection, { onSnapshot: async () => reconcile() });
    expect(await value.start()).toBe(false);
    expect(statuses.at(-1)).toBe("paused");
    reconcile = () => new Promise<boolean>((resolve) => setTimeout(() => resolve(true), 0));
    const [first, second] = await Promise.all([value.retry(), value.retry()]);
    expect(first).toBe(true);
    expect(second).toBe(true);
    expect(connection.calls).toEqual(["Snapshot", "Snapshot"]);
    expect(statuses.at(-1)).toBe("live");
    await value.stop();
  });

  it("recovers a transport close that arrives while Snapshot data is being reconciled", async () => {
    const connection = new FakeConnection();
    let finishFirst!: (value: boolean) => void;
    let reconciliations = 0;
    const { value, statuses } = controller(connection, {
      onSnapshot: async () => ++reconciliations === 1
        ? new Promise<boolean>((resolve) => { finishFirst = resolve; })
        : true,
    });
    const starting = value.start();
    await vi.waitFor(() => expect(reconciliations).toBe(1));
    connection.close(new Error("transport closed during snapshot"));
    finishFirst(true);
    await starting;
    await vi.waitFor(() => expect(connection.starts).toBe(2));
    await vi.waitFor(() => expect(statuses.at(-1)).toBe("live"));
    expect(reconciliations).toBe(2);
    await value.stop();
  });

  it("does not resume a retry loop after authorization is lost during backoff", async () => {
    const connection = new FakeConnection();
    let releaseDelay!: () => void;
    connection.startResult = async () => { throw new Error("offline"); };
    const { value, statuses, unauthorized } = controller(connection, {
      delay: () => new Promise<void>((resolve) => { releaseDelay = resolve; }),
    });
    const starting = value.start();
    await vi.waitFor(() => expect(releaseDelay).toBeTypeOf("function"));
    value.authorizationLost();
    releaseDelay();
    expect(await starting).toBe(false);
    expect(connection.starts).toBe(1);
    expect(unauthorized).toHaveBeenCalledTimes(1);
    expect(statuses.at(-1)).toBe("paused");
    await value.stop();
  });

  it("does not publish Live when authorization is lost during data reconciliation", async () => {
    const connection = new FakeConnection();
    let finishSnapshot!: (value: boolean) => void;
    const { value, statuses, unauthorized } = controller(connection, {
      onSnapshot: () => new Promise<boolean>((resolve) => { finishSnapshot = resolve; }),
    });
    const starting = value.start();
    await vi.waitFor(() => expect(finishSnapshot).toBeTypeOf("function"));
    value.authorizationLost();
    finishSnapshot(true);
    expect(await starting).toBe(false);
    expect(connection.starts).toBe(1);
    expect(statuses.at(-1)).toBe("paused");
    expect(unauthorized).toHaveBeenCalledTimes(1);
    await value.stop();
  });

  it("does not start the authorization classification probe after loss during failed-start cleanup", async () => {
    const connection = new FakeConnection();
    connection.startResult = async () => { throw new Error("offline"); };
    let releaseStop!: () => void;
    const originalStop = connection.stop.bind(connection);
    connection.stop = async () => {
      if (connection.stops === 0) {
        connection.stops++;
        await new Promise<void>((resolve) => { releaseStop = resolve; });
        connection.status = "stopped";
        return;
      }
      await originalStop();
    };
    const probe = vi.fn(async () => true);
    const { value, statuses } = controller(connection, { proveAuthorization: probe });
    const starting = value.start();
    await vi.waitFor(() => expect(releaseStop).toBeTypeOf("function"));
    value.authorizationLost();
    releaseStop();

    expect(await starting).toBe(false);
    expect(connection.starts).toBe(1);
    expect(probe).not.toHaveBeenCalled();
    expect(statuses.at(-1)).toBe("paused");
    await value.stop();
  });

  it("shares the five-start budget across closes before reconciliation and resets it on Retry", async () => {
    const connection = new FakeConnection();
    let closesBeforeRecovery = 10;
    let snapshots = 0;
    connection.invokeResult = async () => connection.snapshot;
    const { value, statuses } = controller(connection, {
      onSnapshot: async () => {
        if (++snapshots <= closesBeforeRecovery) connection.close(new Error("still recovering"));
        return true;
      },
    });

    expect(await value.start()).toBe(false);
    await vi.waitFor(() => expect((value as any).operation).toBeNull());
    expect(connection.starts).toBe(5);
    expect(statuses.at(-1)).toBe("paused");

    closesBeforeRecovery = 5;
    expect(await value.retry()).toBe(true);
    expect(connection.starts).toBe(6);
    expect(statuses.at(-1)).toBe("live");
    await value.stop();
  });
});
