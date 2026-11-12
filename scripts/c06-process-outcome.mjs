export function processOutcome(childResult) {
  return {
    exitCode: childResult.status,
    signal: childResult.signal ?? null,
    timedOut: childResult.error?.code === "ETIMEDOUT",
  };
}
