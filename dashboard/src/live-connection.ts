import { HttpError } from "@microsoft/signalr";
import { ApiError, api } from "./api";
import { RevisionSnapshot } from "./live-updates";

export type LiveConnectionStatus = "live" | "reconnecting" | "paused";

export interface HubConnectionLike {
  on(method: string, handler: (value: RevisionSnapshot) => void): void;
  onclose(handler: (error?: Error) => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke<T>(method: string): Promise<T>;
}

export interface LiveConnectionOptions {
  create: () => HubConnectionLike;
  onRevision: (snapshot: RevisionSnapshot) => void | Promise<void>;
  onSnapshot: (snapshot: RevisionSnapshot) => Promise<boolean>;
  onStatus: (status: LiveConnectionStatus) => void;
  onUnauthorized: () => void;
  onAuthorized?: () => void;
  proveAuthorization?: () => Promise<boolean>;
  attemptTimeoutMs?: number;
  random?: () => number;
  delay?: (milliseconds: number) => Promise<void>;
}

/** One owner coordinates initial starts, reconnects, visibility recovery, and Retry. */
export class LiveConnectionController {
  private connection?: HubConnectionLike;
  private operation: Promise<boolean> | null = null;
  private connected = false;
  private intentionallyStopping = false;
  private disposed = false;
  private unauthorized = false;
  private authorizationProbeUsed = false;
  private authorizationEpoch = 0;
  private lifecycleEpoch = 0;
  private outageAttempts = 0;
  private recoveryPending = false;
  private recoveryError?: Error;
  private status: LiveConnectionStatus = "reconnecting";
  private readonly attemptTimeoutMs: number;
  private readonly random: () => number;
  private readonly delay: (milliseconds: number) => Promise<void>;

  constructor(private readonly options: LiveConnectionOptions) {
    this.attemptTimeoutMs = options.attemptTimeoutMs ?? 8_000;
    this.random = options.random ?? Math.random;
    this.delay = options.delay ?? ((milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds)));
  }

  start(): Promise<boolean> {
    return this.runSingleFlight(() => this.connectWithRetries());
  }

  retry(): Promise<boolean> {
    return this.runSingleFlight(async () => {
      this.outageAttempts = 0;
      if (this.unauthorized) {
        const authorized = await this.proveAuthorization();
        if (!authorized) {
          this.setStatus("paused");
          return false;
        }
        this.unauthorized = false;
        this.options.onAuthorized?.();
      }
      if (this.connected) return this.snapshotAndApply();
      return this.connectWithRetries();
    });
  }

  resume(): Promise<boolean> {
    if (this.unauthorized) return Promise.resolve(false);
    return this.runSingleFlight(async () => {
      if (!this.connected) this.outageAttempts = 0;
      return this.connected ? this.snapshotAndApply() : this.connectWithRetries();
    });
  }

  async stop() {
    this.lifecycleEpoch++;
    this.recoveryPending = false;
    this.disposed = true;
    this.intentionallyStopping = true;
    this.connected = false;
    try {
      await this.connection?.stop();
    } catch {
      // Disposal does not depend on transport shutdown completing successfully.
    } finally {
      this.intentionallyStopping = false;
      this.connection = undefined;
    }
  }

  markReconciled() {
    if (this.connected && !this.unauthorized) {
      this.outageAttempts = 0;
      this.setStatus("live");
    }
  }

  markReconciling() {
    if (this.connected && !this.unauthorized && !this.disposed) this.setStatus("reconnecting");
  }

  markPaused() {
    if (!this.disposed) this.setStatus("paused");
  }

  authorizationLost() {
    if (this.disposed || this.unauthorized) return;
    this.lifecycleEpoch++;
    this.recoveryPending = false;
    this.setUnauthorized();
  }

  private async connectWithRetries(): Promise<boolean> {
    if (this.disposed || this.unauthorized) return false;
    this.setStatus("reconnecting");
    const connection = this.ensureConnection();
    while (this.outageAttempts < 5 && !this.disposed) {
      const attempt = this.outageAttempts + 1;
      if (attempt > 1) {
        const delayEpoch = this.lifecycleEpoch;
        await this.delay(this.retryDelay(attempt - 1));
        if (!this.isCurrent(connection, delayEpoch, false)) return false;
      }
      if (this.disposed || this.unauthorized) return false;
      const attemptEpoch = this.lifecycleEpoch;
      const authorizationEpoch = this.authorizationEpoch;
      this.outageAttempts++;
      try {
        await this.withDeadline(connection.start());
        if (!this.isCurrent(connection, attemptEpoch, false)) return false;
        this.connected = true;
        const success = await this.snapshotAndApply();
        if (!success) {
          this.setStatus("paused");
          return false;
        }
        return true;
      } catch (error) {
        this.connected = false;
        await this.stopConnectionForRetry(connection);
        if (this.disposed || this.unauthorized || authorizationEpoch !== this.authorizationEpoch) return false;
        if (await this.confirmUnauthorized(error)) {
          if (this.disposed || this.unauthorized || authorizationEpoch !== this.authorizationEpoch) return false;
          this.setUnauthorized();
          return false;
        }
        if (this.disposed || this.unauthorized || authorizationEpoch !== this.authorizationEpoch) return false;
      }
    }
    if (!this.disposed) this.setStatus("paused");
    return false;
  }

  private ensureConnection(): HubConnectionLike {
    if (this.connection) return this.connection;
    const connection = this.options.create();
    // Install the invalidation handler before start so a revision cannot fall between startup and Snapshot.
    connection.on("Revision", (snapshot) => {
      void Promise.resolve(this.options.onRevision(snapshot)).catch(() => this.setStatus("paused"));
    });
    connection.onclose((error) => {
      this.lifecycleEpoch++;
      this.connected = false;
      if (!this.disposed && !this.intentionallyStopping) {
        this.setStatus("reconnecting");
        this.recoveryPending = true;
        this.recoveryError = error;
        this.drainRecovery();
      }
    });
    this.connection = connection;
    return connection;
  }

  private async recoverAfterClose(error?: Error): Promise<boolean> {
    if (this.disposed || this.unauthorized) return false;
    if (await this.confirmUnauthorized(error)) {
      this.setUnauthorized();
      return false;
    }
    if (this.disposed || this.unauthorized) return false;
    return this.connectWithRetries();
  }

  private async snapshotAndApply(): Promise<boolean> {
    const connection = this.connection;
    if (!connection || !this.connected || this.disposed) return false;
    const epoch = this.lifecycleEpoch;
    try {
      const snapshot = await this.withDeadline(connection.invoke<RevisionSnapshot>("Snapshot"));
      if (!this.isCurrent(connection, epoch)) return false;
      const success = await this.options.onSnapshot(snapshot);
      if (!this.isCurrent(connection, epoch)) return false;
      if (success) {
        this.authorizationProbeUsed = false;
        this.outageAttempts = 0;
        this.setStatus("live");
      }
      else this.setStatus("paused");
      return success;
    } catch (error) {
      if (!this.isCurrent(connection, epoch)) return false;
      if (await this.confirmUnauthorized(error)) this.setUnauthorized();
      else this.setStatus("paused");
      return false;
    }
  }

  private async stopConnectionForRetry(connection: HubConnectionLike) {
    this.intentionallyStopping = true;
    try {
      await connection.stop();
    } catch {
      // A failed start is already disconnected; the next bounded start remains authoritative.
    } finally {
      this.intentionallyStopping = false;
    }
  }

  private async confirmUnauthorized(error: unknown): Promise<boolean> {
    if ((error instanceof ApiError && error.status === 401) || (error instanceof HttpError && error.statusCode === 401)) return true;
    if (this.authorizationProbeUsed) return false;
    this.authorizationProbeUsed = true;
    try {
      return !(await this.withDeadline((this.options.proveAuthorization ?? this.defaultAuthorizationProbe)()));
    } catch {
      return false;
    }
  }

  private defaultAuthorizationProbe = async () => {
    try {
      await api("info");
      return true;
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) return false;
      throw error;
    }
  };

  private async proveAuthorization(): Promise<boolean> {
    try {
      return await this.withDeadline((this.options.proveAuthorization ?? this.defaultAuthorizationProbe)());
    } catch {
      return false;
    }
  }

  private setUnauthorized() {
    if (this.unauthorized || this.disposed) return;
    this.unauthorized = true;
    this.authorizationEpoch++;
    this.lifecycleEpoch++;
    this.recoveryPending = false;
    this.connected = false;
    this.setStatus("paused");
    this.options.onUnauthorized();
    if (this.connection) void this.stopConnectionForRetry(this.connection).catch(() => {});
  }

  private retryDelay(attempt: number): number {
    const base = Math.min(15_000, 500 * 2 ** (attempt - 1));
    return Math.round(base * (0.8 + this.random() * 0.4));
  }

  private async withDeadline<T>(promise: Promise<T>): Promise<T> {
    let timer: ReturnType<typeof setTimeout> | undefined;
    try {
      return await Promise.race([
        promise,
        new Promise<T>((_, reject) => {
          timer = setTimeout(() => reject(new Error("Live connection attempt timed out.")), this.attemptTimeoutMs);
        }),
      ]);
    } finally {
      if (timer) clearTimeout(timer);
    }
  }

  private setStatus(value: LiveConnectionStatus) {
    if (this.status === value) return;
    this.status = value;
    this.options.onStatus(value);
  }

  private runSingleFlight(action: () => Promise<boolean>): Promise<boolean> {
    if (this.operation) return this.operation;
    const operation = action().finally(() => {
      if (this.operation === operation) {
        this.operation = null;
        this.drainRecovery();
      }
    });
    this.operation = operation;
    return operation;
  }

  private drainRecovery() {
    if (!this.recoveryPending || this.operation || this.disposed || this.unauthorized) return;
    const error = this.recoveryError;
    this.recoveryPending = false;
    this.recoveryError = undefined;
    void this.runSingleFlight(() => this.recoverAfterClose(error));
  }

  private isCurrent(connection: HubConnectionLike, epoch: number, requireConnected = true): boolean {
    return !this.disposed && !this.unauthorized && this.connection === connection
      && (!requireConnected || this.connected) && this.lifecycleEpoch === epoch;
  }
}
