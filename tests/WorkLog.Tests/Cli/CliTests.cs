using System.Diagnostics;
using WorkLog.Cli;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Cli;

public sealed class CliTests
{
    [Theory]
    [InlineData("init", "--clients", "--dry-run")]
    [InlineData("init", "--clients", "unknown")]
    [InlineData("init", "--repository")]
    [InlineData("serve", "--data-dir")]
    [InlineData("serve", "--dry-run")]
    [InlineData("serve", "--clients", "codex")]
    [InlineData("serve", "--unknown")]
    [InlineData("update", "--check", "--status")]
    [InlineData("update", "--data-dir")]
    [InlineData("update", "--unknown")]
    public async Task InvalidArgumentsFailBeforeExecutingCommands(params string[] arguments)
    {
        using var workspace = new TestWorkspace();
        var result = await RunAsync(workspace, arguments);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.False(Directory.Exists(workspace.Data));
        Assert.False(Directory.Exists(Path.Combine(workspace.Repo, ".worklog")));
    }

    [Theory]
    [InlineData("init", "--clients", "--dry-run")]
    [InlineData("serve", "--repository", "--data-dir")]
    [InlineData("update", "--check", "--status")]
    public async Task CommandHelpDescribesOnlyItsOwnOptions(string command, string firstOption, string secondOption)
    {
        using var workspace = new TestWorkspace();
        var result = await RunAsync(workspace, command, "--help", "--data-dir", workspace.Data);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(firstOption, result.Output);
        Assert.Contains(secondOption, result.Output);
        Assert.Empty(result.Error);
        Assert.False(Directory.Exists(workspace.Data));
        if (command == "serve")
        {
            Assert.DoesNotContain("--clients", result.Output);
            Assert.DoesNotContain("--dry-run", result.Output);
        }
    }

    [Fact]
    public async Task RootHelpAndVersionRemainAvailable()
    {
        using var workspace = new TestWorkspace();
        foreach (var arguments in new[] { Array.Empty<string>(), ["--help"], ["help"] })
        {
            var help = await RunAsync(workspace, arguments);
            Assert.Equal(0, help.ExitCode);
            Assert.Contains("init", help.Output);
            Assert.Contains("serve", help.Output);
            Assert.Contains("update", help.Output);
        }

        var version = await RunAsync(workspace, "--version");
        Assert.Equal(0, version.ExitCode);
        Assert.Equal(ApplicationInfo.Version, version.Output.Trim());
    }

    [Fact]
    public async Task UpdateRefusesSourceInstallationAndStatusNeedsNoNetwork()
    {
        using var workspace = new TestWorkspace();
        var update = await RunAsync(workspace, "update", "--data-dir", workspace.Data);
        Assert.NotEqual(0, update.ExitCode);
        Assert.Contains("self-contained Windows", update.Error);
        Assert.False(Directory.Exists(workspace.Data));
        var status = await RunAsync(workspace, "update", "--status", "--data-dir", workspace.Data);
        Assert.Equal(0, status.ExitCode);
        Assert.Contains("No update outcome", status.Output);
        Assert.False(Directory.Exists(workspace.Data));
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(TestWorkspace workspace, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workspace.Repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return (process.ExitCode, await output, await error);
    }
}
