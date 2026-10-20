using WorkLog.Repositories;
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
}
