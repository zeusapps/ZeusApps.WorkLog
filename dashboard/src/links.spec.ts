import { describe, it, expect } from "vitest";
import { branchUrl, repositoryUrl, ticketUrl } from "./links";

const jira = (scope: string, id = "DEP-12", url?: string) => ({ provider: "jira", scope, id, url });
const azure = (scope: string, id = "4211", url?: string) => ({ provider: "azure-devops", scope, id, url });

describe("ticketUrl", () => {
  it("prefers the recorded URL over any built link", () => {
    expect(ticketUrl(jira("company.atlassian.net", "DEP-12", "https://tracker.example.invalid/x"))).toBe("https://tracker.example.invalid/x");
    expect(ticketUrl(jira("not a host", "DEP-12", "http://tracker.example.invalid/x"))).toBe("http://tracker.example.invalid/x");
    expect(ticketUrl(azure("contoso", "1", "https://dev.example.invalid/wi/1"))).toBe("https://dev.example.invalid/wi/1");
  });
  it("normalizes a recorded URL so it contains no raw space", () => {
    expect(ticketUrl(jira("unknown", "DEP-12", "https://tracker.example.invalid/a b"))).toBe("https://tracker.example.invalid/a%20b");
  });
  it("ignores a recorded value that is not an absolute http(s) URL", () => {
    expect(ticketUrl(jira("company.atlassian.net", "DEP-12", "javascript:alert(1)"))).toBe("https://company.atlassian.net/browse/DEP-12");
    expect(ticketUrl(jira("unknown", "DEP-12", "/relative"))).toBeNull();
  });
  it("builds a Jira link only for a host-name scope", () => {
    expect(ticketUrl(jira("company.atlassian.net"))).toBe("https://company.atlassian.net/browse/DEP-12");
    expect(ticketUrl(jira("Company.Atlassian.NET"))).toBe("https://company.atlassian.net/browse/DEP-12");
    expect(ticketUrl(jira(" jira.example.org "))).toBe("https://jira.example.org/browse/DEP-12");
    expect(ticketUrl(jira("company"))).toBeNull();
    expect(ticketUrl(jira("company atlassian"))).toBeNull();
    expect(ticketUrl(jira("company.atlassian.net/extra"))).toBeNull();
    expect(ticketUrl(jira("company.atlassian.net", "A B/1"))).toBe("https://company.atlassian.net/browse/A%20B%2F1");
  });
  it("builds Azure DevOps links for organization, organization/project, and visualstudio.com scopes", () => {
    expect(ticketUrl(azure("contoso"))).toBe("https://dev.azure.com/contoso/_workitems/edit/4211");
    expect(ticketUrl(azure("contoso/Payments"))).toBe("https://dev.azure.com/contoso/Payments/_workitems/edit/4211");
    expect(ticketUrl(azure("contoso/My Project"))).toBe("https://dev.azure.com/contoso/My%20Project/_workitems/edit/4211");
    expect(ticketUrl(azure("contoso.visualstudio.com"))).toBe("https://contoso.visualstudio.com/_workitems/edit/4211");
    expect(ticketUrl(azure("contoso.visualstudio.com/Payments"))).toBe("https://contoso.visualstudio.com/Payments/_workitems/edit/4211");
  });
  it("shows unrecognized Azure DevOps scopes and non-numeric IDs without a link", () => {
    expect(ticketUrl(azure(""))).toBeNull();
    expect(ticketUrl(azure("a/b/c"))).toBeNull();
    expect(ticketUrl(azure("contoso/"))).toBeNull();
    expect(ticketUrl(azure("example.local"))).toBeNull();
    expect(ticketUrl(azure("contoso", "WI-1"))).toBeNull();
    expect(ticketUrl({ provider: "other", scope: "company.atlassian.net", id: "1" })).toBeNull();
  });
});

describe("repositoryUrl and branchUrl", () => {
  it("links GitHub, GitLab, Bitbucket, and Azure DevOps HTTPS identities", () => {
    expect(repositoryUrl("github.com/acme/api")).toBe("https://github.com/acme/api");
    expect(repositoryUrl("gitlab.com/acme/group/api")).toBe("https://gitlab.com/acme/group/api");
    expect(repositoryUrl("bitbucket.org/acme/api")).toBe("https://bitbucket.org/acme/api");
    expect(repositoryUrl("dev.azure.com/contoso/Payments/_git/api")).toBe("https://dev.azure.com/contoso/Payments/_git/api");
    expect(repositoryUrl("contoso.visualstudio.com/Payments/_git/api")).toBe("https://contoso.visualstudio.com/Payments/_git/api");
  });
  it("links Azure DevOps SSH identities to the web repository", () => {
    expect(repositoryUrl("ssh.dev.azure.com/v3/contoso/Payments/api")).toBe("https://dev.azure.com/contoso/Payments/_git/api");
  });
  it("gives local, path, and unrecognized identities no link", () => {
    for (const identity of ["local:5b0a", "path:C:/work/api", "example/worklog", "git.example.org/acme/api", "github.com/acme", "github.com/acme/api/extra", "", "dev.azure.com/contoso/Payments/api", "github.com//api"])
      expect(repositoryUrl(identity)).toBeNull();
    expect(branchUrl("local:5b0a", "main")).toBeNull();
    expect(branchUrl("path:C:/work/api", "main")).toBeNull();
  });
  it("links a branch on each recognized host", () => {
    expect(branchUrl("github.com/acme/api", "feature/DEP-12")).toBe("https://github.com/acme/api/tree/feature/DEP-12");
    expect(branchUrl("gitlab.com/acme/api", "feature/DEP-12")).toBe("https://gitlab.com/acme/api/-/tree/feature/DEP-12");
    expect(branchUrl("bitbucket.org/acme/api", "feature/DEP-12")).toBe("https://bitbucket.org/acme/api/src/feature/DEP-12");
    expect(branchUrl("dev.azure.com/contoso/Payments/_git/api", "main")).toBe("https://dev.azure.com/contoso/Payments/_git/api?version=GBmain");
    expect(branchUrl("ssh.dev.azure.com/v3/contoso/Payments/api", "feature/x")).toBe("https://dev.azure.com/contoso/Payments/_git/api?version=GBfeature%2Fx");
  });
  it("percent-encodes branch segments and keeps slashes", () => {
    expect(branchUrl("github.com/acme/api", "feature/my branch#1")).toBe("https://github.com/acme/api/tree/feature/my%20branch%231");
    expect(branchUrl("github.com/acme/api", "  ")).toBeNull();
  });
});
