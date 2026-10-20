using System.IO.Compression;
using WorkLog.Tests.Testing;
using WorkLog.Updates;
using Xunit;
using WorkJson = WorkLog.Infrastructure.Json;

namespace WorkLog.Tests.Updates;

public sealed class PackageTests
{
    [Fact]
    public void ChecksumMismatchAndInvalidContentsLeaveInstallUntouched()
    {
        using var box = new TestWorkspace();
        var archive = MakeArchive(box.Root);
        var checksum = archive + ".sha256";
        File.WriteAllText(checksum, new string('0', 64) + "  " + Path.GetFileName(archive));
        Assert.Throws<InvalidOperationException>(() => ReleasePackage.VerifyChecksum(archive, checksum, Path.GetFileName(archive)));
        File.WriteAllText(checksum, ReleasePackage.Hash(archive) + "  " + Path.GetFileName(archive));
        Assert.Equal(ReleasePackage.Hash(archive), ReleasePackage.VerifyChecksum(archive, checksum, Path.GetFileName(archive)));
        ReleasePackage.Extract(archive, Path.Combine(box.Root, "valid"), "0.2.0");
        Assert.Throws<InvalidOperationException>(() => ReleasePackage.Extract(archive, Path.Combine(box.Root, "wrong-version"), "0.3.0"));
    }

    [Theory]
    [InlineData("../escaped.txt", 0)]
    [InlineData("sub/../../escaped.txt", 0)]
    [InlineData("C:/escaped.txt", 0)]
    [InlineData("config.json", 0)]
    [InlineData("journal.db", 0)]
    [InlineData("worklog.exe", -1610612736)]
    [InlineData("README.md", 1024)]
    public void UnsafeEntriesAreRejected(string name, int attributes)
    {
        using var box = new TestWorkspace();
        var archive = MakeArchive(box.Root);
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        {
            zip.GetEntry(name)?.Delete();
            var entry = zip.CreateEntry(name);
            entry.ExternalAttributes = attributes;
            using var writer = new StreamWriter(entry.Open());
            writer.Write("unsafe");
        }
        Assert.Throws<IOException>(() => ReleasePackage.Extract(archive, Path.Combine(box.Root, "payload"), "0.2.0"));
        Assert.False(File.Exists(Path.Combine(box.Root, "escaped.txt")));
    }

    [Fact]
    public void DataAndInstallationOverlapAndConcurrentUpdatesAreRejected()
    {
        using var box = new TestWorkspace();
        var target = Path.Combine(box.Root, "install");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "worklog.exe"), "original");
        File.WriteAllText(Path.Combine(target, "release.json"), WorkJson.Encode(new ReleaseManifest("0.1.0", "win-x64", true)));
        Assert.Throws<InvalidOperationException>(() => UpdateInstallation.ValidateTarget(target, target));
        Assert.Throws<InvalidOperationException>(() => UpdateInstallation.ValidateTarget(target, Path.Combine(target, "data")));
        UpdateInstallation.ValidateTarget(target, box.Data);
        using var lease = UpdateInstallation.Acquire(target);
        Assert.Throws<IOException>(() => UpdateInstallation.Acquire(target));
    }

    [Fact]
    public async Task MalformedHelperPlanCannotModifyInstallation()
    {
        using var box = new TestWorkspace();
        var path = Path.Combine(box.Root, "plan.json");
        File.WriteAllText(path, "{}");
        Assert.Equal(1, await UpdateHelper.ApplyAsync(path, new string('a', 64)));
    }

    private static string MakeArchive(string root)
    {
        var path = Path.Combine(root, "worklog-0.2.0-win-x64.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var name in ReleasePackage.ManagedFiles.Take(4))
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(name == "release.json" ? WorkJson.Encode(new ReleaseManifest("0.2.0", "win-x64", true)) : "fixture");
        }
        return path;
    }
}
