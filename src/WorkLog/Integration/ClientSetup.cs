using WorkLog.Configuration;

namespace WorkLog.Integration;

internal sealed record ClientSetup(
    string Root,
    string RepositoryIdentity,
    string ClaudeConfigPath,
    string CodexUserConfigPath,
    ServerRegistration Server,
    IntegrationManifest? Previous,
    RepositoryContext Context);
