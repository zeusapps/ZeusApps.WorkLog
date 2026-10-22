using System.Diagnostics;
using System.Text.Json;

namespace WorkLog.Smoke;

internal sealed class PackagedServer : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task<string> errors;
    private readonly SemaphoreSlim gate = new(1);
    private int sequence;
    private readonly string log;
    public string Version { get; private set; } = "";
    public int LauncherPid => process.Id;
    private PackagedServer(string executable, string data, string logPath, string? repository)
    {
        log = logPath;
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "serve", "--data-dir", data })
            start.ArgumentList.Add(argument);
        if (repository is not null)
        {
            start.ArgumentList.Add("--repository");
            start.ArgumentList.Add(repository);
        }
        process = Process.Start(start)!;
        errors = process.StandardError.ReadToEndAsync();
    }
    public static async Task<PackagedServer> StartAsync(string executable, string data, string log, string? repository = null)
    {
        var server = new PackagedServer(executable, data, log, repository);
        try
        {
            var initialized = await server.RequestAsync("initialize", new
            {
                protocolVersion = "2025-11-25",
                capabilities = new
                {
                },
                clientInfo = new
                {
                    name = "versioned-smoke",
                    version = "1.0"
                }
            });
            server.Version = initialized.GetProperty("result").GetProperty("serverInfo").GetProperty("version").GetString()!;
            await server.process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
            var tools = (await server.RequestAsync("tools/list", new
            {
            })).GetProperty("result").GetProperty("tools");
            if (tools.GetArrayLength() != 2)
                throw new InvalidOperationException("Expected both journal tools.");
            return server;
        }
        catch { await server.DisposeAsync(); throw; }
    }
    public async Task<JsonElement> ToolAsync(string name, object arguments, bool expectError = false)
    {
        var reply = await RequestAsync("tools/call", new
        {
            name,
            arguments
        });
        var result = reply.GetProperty("result");
        var isError = result.TryGetProperty("isError", out var value) && value.GetBoolean();
        if (isError != expectError)
            throw new InvalidOperationException("Unexpected tool outcome: " + result.GetRawText());
        return result;
    }
    private async Task<JsonElement> RequestAsync(string method, object parameters)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await gate.WaitAsync(timeout.Token);
        try
        {
            var id = ++sequence;
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                method,
                @params = parameters
            }));
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
                if (line is null)
                    throw new IOException("Server exited: " + (errors.IsCompleted ? await errors : "stdout closed"));
                if (line.Length > 256 * 1024)
                    throw new IOException("Protocol reply exceeds fixture bound.");
                File.AppendAllText(log, line + "\n");
                using var json = JsonDocument.Parse(line);
                var reply = json.RootElement;
                if (reply.TryGetProperty("id", out var responseId) && responseId.GetInt32() == id)
                {
                    if (reply.TryGetProperty("error", out _))
                        throw new IOException("Protocol failure: " + line);
                    return reply.Clone();
                }
            }
        }
        finally { gate.Release(); }
    }
    public async Task KillLauncherAsync()
    {
        if (!process.HasExited)
            process.Kill(); // Only the fixture-owned wrapper; its job owns the payload.
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }
    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited)
        {
            process.StandardInput.Close();
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException) { await KillLauncherAsync(); }
        }
        File.AppendAllText(log, "\nSTDERR\n" + await errors.WaitAsync(TimeSpan.FromSeconds(10)));
        process.Dispose();
        gate.Dispose();
    }
}
