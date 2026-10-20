using WorkLog.Configuration;

namespace WorkLog.Repositories;

public sealed class RepositoryRegistry(LocalSettings settings)
{
    public RepositoryConfig Get(string path, bool persist = true)
    {
        var root = GitRepository.GetRoot(path);
        if (settings.Configuration.Repositories.TryGetValue(root, out var existing))
        {
            return existing;
        }

        var remote = GitRepository.Run(root, false, "config", "--get", "remote.origin.url").Trim();
        var repository = new RepositoryConfig(RepositoryIdentity.FromRemote(remote) ?? "local:" + Guid.NewGuid().ToString("N"));
        settings.Configuration.Repositories[root] = repository;

        if (persist)
        {
            settings.Save();
        }

        return repository;
    }
}
