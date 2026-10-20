namespace WorkLog.Integration;

internal sealed record ClientSetup(
    string Root,
    string RepositoryIdentity,
    string ClaudeConfigPath,
    string CodexUserConfigPath,
    ServerRegistration Server,
    IntegrationManifest? Previous);
