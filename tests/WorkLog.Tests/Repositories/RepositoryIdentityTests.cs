using WorkLog.Repositories;
using WorkLog.Configuration;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Repositories;

public sealed class RepositoryIdentityTests
{
    [Theory]
    [InlineData("https://user:secret@github.com/org/repo.git", "github.com/org/repo")]
    [InlineData("git@github.com:org/repo.git", "github.com/org/repo")]
    [InlineData("https://github.com/org/repo.git?token=secret", "github.com/org/repo")]
    public void RepositoryIdentityRemovesCredentials(string remote, string expected) => Assert.Equal(expected, RepositoryIdentity.FromRemote(remote));

    [Fact]
    public void AddingOriginPreservesSavedLocalIdentity()
    {
        using var box = new TestWorkspace();
        box.Git("init");
        var first = new RepositoryRegistry(new LocalSettings(box.Data)).Get(box.Repo);
        Assert.StartsWith("local:", first.Identity);
        box.Git("remote", "add", "origin", "https://github.com/zeusapps/ZeusApps.WorkLog.git");
        var afterPublication = new RepositoryRegistry(new LocalSettings(box.Data)).Get(box.Repo);
        Assert.Equal(first.Identity, afterPublication.Identity);
    }
}
