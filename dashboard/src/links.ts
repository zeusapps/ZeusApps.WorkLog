import type { Ticket } from "./models";

/**
 * Pure builders of external links for recorded tickets, repositories, and branches. Nothing here contacts a service;
 * a link is produced only for a clearly recognized form and is otherwise null.
 */
const hostName = /^[a-z0-9.-]+\.[a-z]{2,}$/i;
const azureOrganization = /^[a-z0-9][a-z0-9_-]*$/i;
const visualStudioHost = /^[a-z0-9][a-z0-9-]*\.visualstudio\.com$/i;
const encodeSegment = (segment: string) => encodeURIComponent(segment);
const validSegment = (segment: string) => segment !== "" && segment !== "." && segment !== "..";

function absoluteWebUrl(value: string | null | undefined): string | null {
  const text = value?.trim();
  if (!text || !/^https?:\/\//i.test(text)) return null;
  try {
    return new URL(text).href;
  } catch {
    return null;
  }
}

/** The link of a ticket: its recorded URL, else a link built from a recognized Jira or Azure DevOps scope. */
export function ticketUrl(ticket: Ticket): string | null {
  const recorded = absoluteWebUrl(ticket.url);
  if (recorded) return recorded;
  const scope = ticket.scope.trim();
  const id = ticket.id.trim();
  if (!id) return null;
  if (ticket.provider.trim() === "jira") {
    return hostName.test(scope)
      ? "https://" + scope.toLowerCase() + "/browse/" + encodeSegment(id)
      : null;
  }
  if (ticket.provider.trim() === "azure-devops" && /^\d+$/.test(id)) {
    const parts = scope.split("/").map((part) => part.trim());
    if (parts.length > 2 || !parts.every(validSegment)) return null;
    const [first, project] = parts;
    const tail = "/_workitems/edit/" + id;
    if (visualStudioHost.test(first))
      return (
        "https://" + first.toLowerCase() +
        (project ? "/" + encodeSegment(project) : "") + tail
      );
    if (!azureOrganization.test(first)) return null;
    return (
      "https://dev.azure.com/" + encodeSegment(first) +
      (project ? "/" + encodeSegment(project) : "") + tail
    );
  }
  return null;
}

type Host = "github" | "gitlab" | "bitbucket" | "azure";
interface Recognized {
  host: Host;
  /** The repository web page. */
  url: string;
}

function recognize(identity: string): Recognized | null {
  const parts = identity.trim().split("/");
  if (parts.length < 3 || !parts.every(validSegment)) return null;
  const host = parts[0].toLowerCase();
  const encoded = (items: string[]) => items.map(encodeSegment).join("/");
  if (host === "github.com" || host === "bitbucket.org")
    return parts.length === 3
      ? {
          host: host === "github.com" ? "github" : "bitbucket",
          url: "https://" + host + "/" + encoded(parts.slice(1)),
        }
      : null;
  if (host === "gitlab.com")
    return { host: "gitlab", url: "https://gitlab.com/" + encoded(parts.slice(1)) };
  if (host === "ssh.dev.azure.com")
    return parts.length === 5 && parts[1] === "v3"
      ? {
          host: "azure",
          url: "https://dev.azure.com/" + encoded([parts[2], parts[3]]) + "/_git/" + encodeSegment(parts[4]),
        }
      : null;
  const git = parts.indexOf("_git");
  if (
    (host === "dev.azure.com" || visualStudioHost.test(host)) &&
    git >= 2 &&
    git === parts.length - 2
  )
    return { host: "azure", url: "https://" + host + "/" + encoded(parts.slice(1)) };
  return null;
}

/** The web page of a repository identity on a recognized host; null for any other identity, including `local:` and `path:`. */
export function repositoryUrl(identity: string): string | null {
  return recognize(identity)?.url ?? null;
}

/** The page of a branch on the repository's host; null when the identity is not recognized or the branch is blank. */
export function branchUrl(identity: string, branch: string): string | null {
  const name = branch.trim();
  const repository = recognize(identity);
  if (!repository || !name) return null;
  const path = name.split("/").map(encodeSegment).join("/");
  switch (repository.host) {
    case "github":
      return repository.url + "/tree/" + path;
    case "gitlab":
      return repository.url + "/-/tree/" + path;
    case "bitbucket":
      return repository.url + "/src/" + path;
    case "azure":
      return repository.url + "?version=GB" + encodeSegment(name);
  }
}

/** Qualified text of a ticket for a hover title. */
export function ticketTitle(ticket: Ticket): string {
  return ticket.provider + ":" + ticket.scope + ":" + ticket.id;
}
