using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WorkLog.Dashboard;

/// <summary>Opens a target the dashboard built itself: an editor URI, or the canonical path of a contained document.</summary>
internal interface IEditorLauncher
{
    void Launch(string target);
}

/// <summary>Hands the target to the operating system shell, which resolves the registered URL handler or file association.</summary>
internal sealed class ShellEditorLauncher : IEditorLauncher
{
    public void Launch(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
}

/// <summary>
/// Test-only launcher: appends each target to a file instead of starting a process. It exists only when
/// <see cref="Variable"/> names a file, so every other run launches editors normally.
/// </summary>
internal sealed class RecordingEditorLauncher(string path) : IEditorLauncher
{
    public const string Variable = "WORKLOG_DASHBOARD_TEST_LAUNCH_LOG";
    private readonly object gate = new();

    public static IEditorLauncher FromEnvironment()
        => Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } log ? new RecordingEditorLauncher(log) : new ShellEditorLauncher();

    public void Launch(string target)
    {
        lock (gate)
            File.AppendAllText(path, target + "\n");
    }
}

internal sealed record OpenRequest(DocumentRequest Document, int Line, string Editor);

internal sealed partial class EditorOpener(DocumentService documents, IEditorLauncher launcher)
{
    public async Task OpenAsync(OpenRequest request, CancellationToken token)
    {
        if (request.Editor is not ("vscode" or "cursor" or "system"))
            throw new ArgumentException("editor must be vscode, cursor, or system.");
        if (request.Line is < 0 or > 10_000_000)
            throw new ArgumentException("line must be a non-negative line number.");
        var file = await documents.ResolveFileAsync(request.Document, token);
        // Containment follows links to the canonical target, so the requested name's extension is not enough.
        if (!file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only Markdown documents can be opened.");
        var target = request.Editor == "system" ? file : Uri(request.Editor, file, request.Line);
        try
        {
            launcher.Launch(target);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            throw new IOException($"Could not open {Label(request.Editor)}. Choose another editor.");
        }
    }

    private static string Label(string editor) => editor switch { "vscode" => "VS Code", "cursor" => "Cursor", _ => "the system default app" };

    /// <summary>
    /// <c>scheme://file/&lt;path&gt;:&lt;line+1&gt;:1</c> with forward slashes and every segment percent-encoded as UTF-8,
    /// except a leading drive designator such as <c>C:</c>.
    /// </summary>
    internal static string Uri(string scheme, string file, int zeroBasedLine)
    {
        var segments = file.Replace('\\', '/').Split('/');
        var encoded = segments.Select((segment, index) => index == 0 && Drive().IsMatch(segment) ? segment : System.Uri.EscapeDataString(segment));
        var path = string.Join('/', encoded);
        return $"{scheme}://file{(path.StartsWith('/') ? "" : "/")}{path}:{zeroBasedLine + 1}:1";
    }

    [GeneratedRegex("^[A-Za-z]:$")] private static partial Regex Drive();
}
