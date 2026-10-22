using WorkLog.Configuration;

namespace WorkLog.Repositories;

public sealed class RepositoryRegistry(LocalSettings settings)
{
    public RepositoryConfig Get(string path, bool persist = true)
        => GetContext(path, persist).Repository;

    public RepositoryContext GetContext(string path, bool persist = true)
    {
        if (!persist)
            return ResolveContext(path);
        using var lease = settings.AcquireWrite();
        var latest = settings.FreshCopy();
        _ = new RepositoryRegistry(latest).ResolveContext(path);
        if (latest.SerializePreserving() != settings.SerializePreserving() || !File.Exists(settings.ConfigPath))
            latest.Save();
        settings.Adopt(latest);
        return settings.Configuration.RepositoryContexts.First(pair =>
            GitRepository.PathComparer.Equals(pair.Key, GitRepository.GetCommonDirectory(path))).Value;
    }

    private RepositoryContext ResolveContext(string path)
    {
        var root = GitRepository.GetRoot(path);
        var common = GitRepository.GetCommonDirectory(root);
        var contexts = settings.Configuration.RepositoryContexts;
        var existing = contexts.FirstOrDefault(pair => GitRepository.PathComparer.Equals(pair.Key, common)).Value;
        if (existing is not null)
        {
            return existing;
        }

        var legacy = settings.Configuration.Repositories.Where(pair =>
            GitRepository.PathComparer.Equals(GitRepository.TryGetCommonDirectory(pair.Key), common)).ToArray();
        string? Merge(string field, Func<RepositoryConfig, string?> select, bool trim = true)
        {
            var values = legacy.Select(pair => trim ? select(pair.Value)?.Trim() : select(pair.Value)).Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal).ToArray();
            if (values.Length > 1)
                throw new ArgumentException($"Conflicting repository {field} in legacy settings for {string.Join(", ", legacy.Select(pair => pair.Key))}. "
                    + "Reconcile these config.json records explicitly, preserving the intended identity and ticket context, then retry.");
            return values.SingleOrDefault();
        }

        var identity = Merge("identity", config => config.Identity);
        var provider = Merge("provider", config => config.Provider);
        var scope = Merge("scope", config => config.Scope);
        var pattern = Merge("ticketPattern", config => config.TicketPattern, trim: false);
        if (identity is null)
        {
            var remote = GitRepository.Run(root, false, "config", "--get", "remote.origin.url").Trim();
            identity = RepositoryIdentity.FromRemote(remote) ?? "local:" + Guid.NewGuid().ToString("N");
        }
        var repository = new RepositoryConfig(identity, provider, scope, pattern ?? new RepositoryConfig(identity).TicketPattern);
        var context = new RepositoryContext(repository) { LegacyPaths = legacy.Select(pair => pair.Key).ToList() };
        contexts[common] = context;

        return context;
    }
}
