namespace WorkLog.Integration;

public sealed record IntegrationManifest(
    string Version,
    string Repository,
    string[] Clients,
    string Command,
    string[] Arguments);
