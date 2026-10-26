export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}
export async function api<T>(
  path: string,
  body?: unknown,
  method?: string,
  signal?: AbortSignal,
): Promise<T> {
  const response = await fetch("/api/" + path, {
    method: method ?? (body === undefined ? "GET" : "POST"),
    credentials: "same-origin",
    cache: "no-store",
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
  });
  if (!response.ok) {
    const value = await response
      .json()
      .catch(() => ({ error: response.statusText }));
    throw new ApiError(value.error ?? "Request failed", response.status);
  }
  return response.status === 204 ||
    response.headers.get("content-length") === "0"
    ? (undefined as T)
    : response.json();
}
export function localDate(date: Date, zone: string): string {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: zone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(date);
}
export function addDays(date: string, days: number): string {
  const value = new Date(date + "T12:00:00Z");
  value.setUTCDate(value.getUTCDate() + days);
  return value.toISOString().slice(0, 10);
}
export function boundary(date: string, zone: string): string {
  const wanted = Date.parse(date + "T00:00:00Z");
  let candidate = wanted;
  const formatter = new Intl.DateTimeFormat("en-CA", {
    timeZone: zone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hourCycle: "h23",
  });
  for (let i = 0; i < 3; i++) {
    const parts = Object.fromEntries(
      formatter
        .formatToParts(new Date(candidate))
        .map((p) => [p.type, p.value]),
    );
    const represented = Date.UTC(
      +parts["year"],
      +parts["month"] - 1,
      +parts["day"],
      +parts["hour"],
      +parts["minute"],
      +parts["second"],
    );
    candidate += wanted - represented;
  }
  return new Date(candidate).toISOString();
}
export function humanize(value: string): string {
  if (/^[A-Z][A-Z0-9]*-\d+(?:\s+\+\s+[A-Z][A-Z0-9]*-\d+)*$/.test(value))
    return value;
  const result = value.replace(/[-_]/g, " ");
  return result.charAt(0).toUpperCase() + result.slice(1);
}
export function elapsed(seconds: number): string {
  if (seconds < 60) return "Under a minute";
  if (seconds < 3600) return Math.floor(seconds / 60) + "m";
  if (seconds < 86400)
    return (
      Math.floor(seconds / 3600) +
      "h " +
      Math.floor((seconds % 3600) / 60) +
      "m"
    );
  return (
    Math.floor(seconds / 86400) +
    "d " +
    Math.floor((seconds % 86400) / 3600) +
    "h"
  );
}
