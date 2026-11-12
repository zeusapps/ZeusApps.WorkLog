/** Track the only wrapper and port owned by one fixture-host startup attempt. */
export async function startTrackedHost({ spawn, port, base, watcherMode, onOwned, waitForHealth, waitForRoots }) {
  const child = spawn();
  const host = { child, wrapperPid: child.pid ?? null, port, base, health: null, ready: null, watcherMode, startupFailure: null };
  onOwned(host);
  try {
    host.health = await waitForHealth(host);
    host.ready = await waitForRoots(host);
    return host;
  } catch (error) {
    host.startupFailure = String(error?.message ?? error ?? "host startup failed");
    throw error;
  }
}

export async function stopTrackedHost(currentHost, { childHasExited, sendStop, killChild, waitChildExit, readReady, readFinal, portReleased, sanitize = String }) {
  if (!currentHost) return null;
  let ready = currentHost.ready;
  if (!ready) {
    try { ready = await readReady(); }
    catch { /* Startup may have failed before the host wrote its ready record. */ }
  }
  let gracefulStopAcknowledged = false, gracefulStopError = null;
  if (!childHasExited(currentHost.child) && ready?.port === currentHost.port) {
    try { await sendStop(); gracefulStopAcknowledged = true; }
    catch (error) { gracefulStopError = sanitize(error?.message ?? "owned host stop command failed"); }
  }
  const gracefulExitObserved = gracefulStopAcknowledged
    && (childHasExited(currentHost.child) || await waitChildExit(currentHost.child, 10_000));
  const forcedTermination = !childHasExited(currentHost.child);
  if (forcedTermination) killChild(currentHost.child);
  const exited = childHasExited(currentHost.child) || await waitChildExit(currentHost.child, 10_000);
  let final = null;
  try { final = await readFinal(); } catch { /* abrupt termination is reported in the parent result */ }
  const released = await portReleased(currentHost.port);
  return {
    startupFailure: currentHost.startupFailure,
    wrapperPid: currentHost.wrapperPid,
    wrapperExitObserved: exited,
    wrapperExitCode: currentHost.child.exitCode,
    gracefulStopAcknowledged,
    gracefulStopError,
    gracefulExitObserved,
    forcedTermination,
    hostPid: currentHost.health?.pid ?? ready?.pid ?? null,
    hostInstance: currentHost.health?.instance ?? ready?.instance ?? null,
    watcherMode: currentHost.watcherMode,
    final,
    port: currentHost.port,
    portReleased: released,
  };
}

export function hostCleanupComplete(result) {
  return result?.wrapperExitObserved === true && result?.portReleased?.released === true;
}
