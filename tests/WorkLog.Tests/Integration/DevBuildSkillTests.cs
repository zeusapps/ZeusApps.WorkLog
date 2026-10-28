using Xunit;

namespace WorkLog.Tests.Integration;

public sealed class DevBuildSkillTests
{
    private const string Claude = ".claude/skills/install-dev-build/SKILL.md", Codex = ".agents/skills/install-dev-build/SKILL.md";

    private static string Repository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ZeusApps.WorkLog.slnx")))
                return directory.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Repository(), relative));

    [Fact]
    public void ClaudeAndCodexCopiesOfTheInstallDevBuildSkillAreIdentical() =>
        Assert.True(File.ReadAllBytes(Path.Combine(Repository(), Claude)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(Repository(), Codex))),
            $"{Claude} and {Codex} must hold identical text.");

    [Fact]
    public void SkillRequiresAuthorizationPerOperationAndForbidsStoppingUserProcesses()
    {
        var text = Read(Claude);
        Assert.StartsWith("---\nname: install-dev-build\n", text.ReplaceLineEndings("\n"));
        foreach (var action in new[] { "Switch", "Rollback", "Retire" })
            Assert.Contains($"`{action}`", text);
        Assert.Contains("explicit authorization", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Earlier approval for a different build, version or action does not carry over", text);
        Assert.Contains("Never stop or restart user processes", text);
        Assert.Contains("scripts/install-dev.ps1", text);
    }

    [Fact]
    public void SkillStopsOnARefusalAndNeedsAuthorizationForEachRecoveryStep()
    {
        var text = Read(Claude).ReplaceLineEndings("\n");
        Assert.Contains("**Stop on a refusal or failure.**", text);
        Assert.Contains("When Rollback or Retire refuses or fails, stop and report", text);
        Assert.Contains("unless the user explicitly authorizes that specific step", text);
        Assert.Contains("`worklog update --rollback`", text);
        // Rules 3 and 4 name the same exception instead of contradicting the fallback.
        Assert.Contains("The one exception is `worklog update --rollback` as a recovery step, only with the user's explicit authorization for that specific step (rule 6).", text);
        Assert.Contains("unless the user explicitly authorizes that specific recovery step (rule 6)", text);
        Assert.Contains("`-Workspace` must always be under `artifacts\\`", text);
        Assert.DoesNotContain("-Workspace`, `-SkipProbe`) against the real installation", text);
        Assert.Contains("one further selection change to another payload", text);
        Assert.Contains("only with the user's explicit authorization for that specific step", Read("docs/releases.md"));
    }
}
