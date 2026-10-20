using WorkLog.Tests.Testing;
using WorkLog.Updates;
using Xunit;

namespace WorkLog.Tests.Updates;

public sealed class ReplacementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementPreservesUnownedFilesAndRestoresOnFailedVerification(bool failVerification)
    {
        using var box = new TestWorkspace();
        var (plan, payload) = Fixture(box);
        var replacement = new ApplicationReplacement((_, _, _) => failVerification
            ? Task.FromException(new IOException("injected post-install verification failure")) : Task.CompletedTask);
        if (failVerification)
        {
            var failure = await Assert.ThrowsAsync<IOException>(() => replacement.ApplyAsync(plan, payload));
            Assert.Contains("Prior application files restored", failure.Message);
            Assert.Equal("old executable", File.ReadAllText(Path.Combine(plan.TargetDirectory, "worklog.exe")));
            Assert.False(File.Exists(Path.Combine(plan.TargetDirectory, "INSTALL.md")));
        }
        else
        {
            await replacement.ApplyAsync(plan, payload);
            Assert.Equal("new executable", File.ReadAllText(Path.Combine(plan.TargetDirectory, "worklog.exe")));
        }
        Assert.Equal("private setting", File.ReadAllText(Path.Combine(plan.TargetDirectory, "keep.txt")));
        Assert.Equal("journal sentinel", File.ReadAllText(Path.Combine(box.Data, "journal.db")));
        Assert.Equal("config sentinel", File.ReadAllText(Path.Combine(box.Data, "config.json")));
        Assert.Equal("old executable", File.ReadAllText(Path.Combine(plan.BackupDirectory, "worklog.exe")));
    }

    [Fact]
    public async Task LockedTargetIsRejectedBeforeAnyReplacement()
    {
        using var box = new TestWorkspace();
        var (plan, payload) = Fixture(box);
        using var locked = new FileStream(Path.Combine(plan.TargetDirectory, "worklog.exe"), FileMode.Open, FileAccess.Read, FileShare.None);
        await Assert.ThrowsAsync<IOException>(() => new ApplicationReplacement((_, _, _) => Task.CompletedTask).ApplyAsync(plan, payload));
        Assert.Equal("old manifest", File.ReadAllText(Path.Combine(plan.TargetDirectory, "release.json")));
        Assert.False(Directory.Exists(plan.BackupDirectory));
    }

    private static (UpdatePlan Plan, string Payload) Fixture(TestWorkspace box)
    {
        var target = Path.Combine(box.Root, "install");
        var payload = Path.Combine(box.Root, "payload");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(payload);
        Directory.CreateDirectory(box.Data);
        File.WriteAllText(Path.Combine(target, "worklog.exe"), "old executable");
        File.WriteAllText(Path.Combine(target, "release.json"), "old manifest");
        File.WriteAllText(Path.Combine(target, "keep.txt"), "private setting");
        File.WriteAllText(Path.Combine(payload, "worklog.exe"), "new executable");
        File.WriteAllText(Path.Combine(payload, "release.json"), "new manifest");
        File.WriteAllText(Path.Combine(payload, "INSTALL.md"), "new instructions");
        File.WriteAllText(Path.Combine(box.Data, "journal.db"), "journal sentinel");
        File.WriteAllText(Path.Combine(box.Data, "config.json"), "config sentinel");
        return (new(Guid.NewGuid().ToString("N"), new string('a', 64), target, box.Data,
            "0.1.0", "0.2.0", "hash", "original hash", 1, DateTime.UtcNow, DateTimeOffset.UtcNow), payload);
    }
}
