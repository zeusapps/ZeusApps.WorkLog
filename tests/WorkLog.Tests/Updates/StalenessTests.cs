using WorkLog.DevInstall;
using WorkLog.Installation;
using WorkLog.Repositories;
using Xunit;

namespace WorkLog.Tests.Updates;

/// <summary>The stale rule runs in a disposable Git repository under artifacts/, never in this checkout.</summary>
public sealed class StalenessTests
{
    private static readonly DateTime Old = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("delete", false)]
    [InlineData("rename", false)]
    [InlineData("copy", false)]
    [InlineData("delete", true)]
    [InlineData("rename", true)]
    [InlineData("copy", true)]
    public void DeletedRenamedAndOldTimestampCopiedFilesMakeThePackageStale(string change, bool dirtyPackage)
    {
        using var repository = new DisposableRepository();
        var marker = repository.Marker(dirtyPackage);
        Assert.Null(Staleness.Check(repository.Root, marker));

        switch (change)
        {
            case "delete":
                File.Delete(repository.Path("src/WorkLog/A.cs"));
                break;
            case "rename":
                File.Move(repository.Path("dashboard/src/app.ts"), repository.Path("dashboard/src/renamed.ts"));
                break;
            default:
                // File.Copy keeps the old modification time of the source.
                File.Copy(repository.Path("src/WorkLog/A.cs"), repository.Path("src/WorkLog/Copy.cs"));
                File.SetLastWriteTimeUtc(repository.Path("src/WorkLog/Copy.cs"), Old);
                break;
        }

        if (!dirtyPackage)
        {
            // Hide the directory time change so that only the clean-tree Git check can notice it.
            repository.AgeEverything();
            var message = Staleness.Check(repository.Root, marker);
            Assert.NotNull(message);
            Assert.Contains("uncommitted changes", message);
        }
        else
        {
            // A dirty package skips the Git check; the changed directory time notices it.
            var message = Staleness.Check(repository.Root, marker);
            Assert.NotNull(message);
            Assert.Contains("directory", message);
        }
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("dashboard/tsconfig.json")]
    public void ADeletedListedInputMakesACleanPackageStale(string input)
    {
        using var repository = new DisposableRepository();
        var marker = repository.Marker(false);
        Assert.True(File.Exists(repository.Path(input)));
        File.Delete(repository.Path(input));
        repository.AgeEverything();
        var message = Staleness.Check(repository.Root, marker);
        Assert.NotNull(message);
        Assert.Contains("uncommitted changes", message);
    }

    [Fact]
    public void ChangesOutsideTheStaleInputsAndSpecFilesDoNotMakeThePackageStale()
    {
        using var repository = new DisposableRepository();
        var marker = repository.Marker(false);
        File.WriteAllText(repository.Path("tests/NewTest.cs"), "// not a build input");
        File.WriteAllText(repository.Path("dashboard/src/app.spec.ts"), "// spec");
        Directory.CreateDirectory(repository.Path("src/WorkLog/obj"));
        File.WriteAllText(repository.Path("src/WorkLog/obj/generated.cs"), "// ignored");
        repository.AgeEverything();
        Assert.Null(Staleness.Check(repository.Root, marker));
    }

    [Fact]
    public void ADifferentCommitAndAnEditedFileAreStale()
    {
        using var repository = new DisposableRepository();
        Assert.Contains("built from commit", Staleness.Check(repository.Root,
            repository.Marker(false) with
            {
                Commit = "1234567890abcdef1234567890abcdef12345678"
            }));
        File.AppendAllText(repository.Path("src/WorkLog/A.cs"), "// edit");
        Assert.Contains("changed after the package", Staleness.Check(repository.Root, repository.Marker(true)));
    }

    private sealed class DisposableRepository : IDisposable
    {
        public string Root { get; } = System.IO.Path.Combine(FindRepository(), "artifacts", "staleness-tests", Guid.NewGuid().ToString("N"));

        public DisposableRepository()
        {
            Directory.CreateDirectory(Root);
            foreach (var file in new[]
            {
                "src/WorkLog/A.cs", "src/WorkLog/B.cs", "dashboard/src/app.ts", "dashboard/package.json", "dashboard/tsconfig.json",
                "Directory.Build.props", "README.md", "tests/T.cs"
            })
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path(file))!);
                File.WriteAllText(Path(file), file);
            }
            Git("init", "-q");
            Git("add", ".");
            Git("-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "fixture");
            AgeEverything();
        }

        public string Path(string relative) => System.IO.Path.Combine(Root, relative);

        public BuildMarker Marker(bool dirty) =>
            new("dev", Git("rev-parse", "HEAD").Trim(), dirty, "2021-01-01T00:00:00Z", "0.1.0");

        /// <summary>Sets every file and directory outside .git to an old time, before the marker's build time.</summary>
        public void AgeEverything()
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories)
                .Where(path => !System.IO.Path.GetRelativePath(Root, path).StartsWith(".git", StringComparison.Ordinal)))
            {
                if (Directory.Exists(entry))
                    Directory.SetLastWriteTimeUtc(entry, Old);
                else
                    File.SetLastWriteTimeUtc(entry, Old);
            }
        }

        private string Git(params string[] arguments) => GitRepository.Run(Root, arguments);

        public void Dispose()
        {
            var parent = System.IO.Path.Combine(FindRepository(), "artifacts", "staleness-tests");
            if (!DevTargets.Within(parent, Root))
                throw new InvalidOperationException("Invalid test cleanup path.");
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Root, true);
        }

        private static string FindRepository()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                if (File.Exists(System.IO.Path.Combine(directory.FullName, "ZeusApps.WorkLog.slnx")))
                    return directory.FullName;
            throw new InvalidOperationException("Repository root not found.");
        }
    }
}
