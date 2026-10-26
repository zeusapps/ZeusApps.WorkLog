using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkLog.Configuration;
using WorkLog.Infrastructure;

namespace WorkLog.Dashboard;

internal sealed record DashboardLock(int Pid, int Port, string Instance);
internal sealed record ObservationRequest(TimelineQuery Query, string Id, int Offset = 0, string? Day = null);
internal sealed record LinkRequest(DocumentRequest Document, string Target);
internal sealed record BootstrapRequest(string Token);

internal static class DashboardHost
{
    public static async Task RunAsync(LocalSettings settings, string? repository, int port, bool noOpen, CancellationToken token)
    {
        Directory.CreateDirectory(settings.DataDirectory);
        var lockPath = Path.Combine(settings.DataDirectory, "dashboard.lock");
        var authorization = new DashboardAuthorization(settings.DataDirectory);
        FileStream lease;
        try { lease = new FileStream(lockPath + ".lease", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read); }
        catch (IOException)
        {
            await ReuseAsync(lockPath, authorization, noOpen, token);
            return;
        }
        using (lease)
        {
            var instance = Guid.NewGuid().ToString("N");
            await using var app = Create(settings, repository, port, instance, authorization);
            try
            {
                try { await app.StartAsync(token); }
                catch (IOException e) { throw new IOException($"Dashboard port {port} is unavailable. Choose another with --port. No process was adopted.", e); }
                var address = Address(app);
                var actualPort = new Uri(address).Port;
                AtomicFile.Write(lockPath, Json.Encode(new DashboardLock(Environment.ProcessId, actualPort, instance)));
                OpenOrPrint(address + "/#connect=" + authorization.Bootstrap(), noOpen);
                await app.WaitForShutdownAsync(token);
            }
            finally
            {
                await app.StopAsync(CancellationToken.None);
                try { if (File.Exists(lockPath) && Json.Decode<DashboardLock>(File.ReadAllText(lockPath)).Instance == instance) File.Delete(lockPath); }
                catch (Exception e) when (e is IOException or JsonException) { }
            }
        }
    }

    private static async Task ReuseAsync(string lockPath, DashboardAuthorization authorization, bool noOpen, CancellationToken token)
    {
        try
        {
            var saved = Json.Decode<DashboardLock>(await File.ReadAllTextAsync(lockPath, token));
            using var process = Process.GetProcessById(saved.Pid);
            if (process.HasExited || saved.Port is < 1 or > 65535) throw new IOException("Dashboard lock is stale.");
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{saved.Port}"), Timeout = TimeSpan.FromSeconds(5) };
            var health = await client.GetFromJsonAsync<DashboardLock>("/health", token);
            if (health?.Instance != saved.Instance || health.Pid != saved.Pid) throw new IOException("The port does not belong to the recorded dashboard.");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/launch");
            request.Headers.Add("X-WorkLog-Launch", authorization.Sign("launch:" + saved.Instance));
            using var response = await client.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            var value = await response.Content.ReadFromJsonAsync<BootstrapRequest>(token) ?? throw new IOException("Invalid dashboard launch response.");
            OpenOrPrint(client.BaseAddress.ToString().TrimEnd('/') + "/#connect=" + value.Token, noOpen);
        }
        catch (Exception e) when (e is IOException or HttpRequestException or JsonException or ArgumentException or InvalidOperationException)
        { throw new IOException("Cannot reuse the running dashboard. Check its process or choose another data directory. " + e.Message, e); }
    }

    internal static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

    internal static WebApplication Create(LocalSettings settings, string? repository, int port, string instance, DashboardAuthorization authorization, LocalCommand? commands = null)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [], ApplicationName = typeof(DashboardHost).Assembly.FullName });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, port);
            options.Limits.MaxRequestBodySize = DocumentService.MaximumBytes + 131_072;
        });
        var app = builder.Build();
        var journal = new DashboardJournal(settings.DataDirectory);
        var timeline = new TimelineService(journal);
        var catalog = new SourceCatalog(settings, journal, commands ?? new LocalCommand(), repository ?? Environment.CurrentDirectory);
        var documents = new DocumentService(catalog);
        var state = new DashboardState(settings.DataDirectory);
        app.Use(async (context, next) =>
        {
            var host = context.Request.Host;
            var origin = context.Request.Headers.Origin.ToString();
            var site = context.Request.Headers["Sec-Fetch-Site"].ToString();
            var expectedOrigin = $"http://127.0.0.1:{context.Connection.LocalPort}";
            if (host.Host != "127.0.0.1" || host.Port != context.Connection.LocalPort
                || (origin.Length > 0 && origin != expectedOrigin) || (site.Length > 0 && site is not ("same-origin" or "none")))
            { context.Response.StatusCode = 403; return; }
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            var path = context.Request.Path.Value ?? "/";
            if (path.StartsWith("/api/", StringComparison.Ordinal) && path is not ("/api/bootstrap" or "/api/launch")
                && !authorization.Valid(context.Request.Cookies[authorization.CookieName]))
            { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { error = "Launch worklog dashboard to authorize this browser." }, context.RequestAborted); return; }
            try { await next(context); }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (Exception e) when (e is ArgumentException or FileNotFoundException or DashboardConflictException or IOException or UnauthorizedAccessException or JsonException or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
            {
                if (context.Response.HasStarted) throw;
                context.Response.StatusCode = e switch { DashboardConflictException => 409, FileNotFoundException => 404, UnauthorizedAccessException => 403,
                    IOException or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException => 503, _ => 400 };
                await context.Response.WriteAsJsonAsync(new { error = e.Message }, context.RequestAborted);
            }
        });
        app.MapGet("/health", (HttpContext context) => new DashboardLock(Environment.ProcessId, context.Connection.LocalPort, instance));
        app.MapPost("/api/launch", (HttpContext context) => authorization.ValidLaunch(context.Request.Headers["X-WorkLog-Launch"], instance)
            ? Results.Json(new BootstrapRequest(authorization.Bootstrap())) : Results.StatusCode(403));
        app.MapPost("/api/bootstrap", (BootstrapRequest request, HttpContext context) =>
        {
            if (!authorization.Exchange(request.Token)) return Results.StatusCode(401);
            context.Response.Cookies.Append(authorization.CookieName, authorization.Cookie, new CookieOptions
            { HttpOnly = true, SameSite = SameSiteMode.Strict, IsEssential = true, Path = "/", MaxAge = TimeSpan.FromDays(365) });
            return Results.Ok();
        });
        app.MapGet("/api/info", () => new { timezone = TimeZoneInfo.TryConvertWindowsIdToIanaId(TimeZoneInfo.Local.Id, out var iana) ? iana : TimeZoneInfo.Local.Id });
        app.MapGet("/api/catalog", (CancellationToken ct) => catalog.GetAsync(ct));
        app.MapGet("/api/state", () => state.Read());
        app.MapPut("/api/state", (JsonObject value) => { state.Write(value); return Results.NoContent(); });
        app.MapPost("/api/timeline", (TimelineQuery query, CancellationToken ct) => timeline.Query(query, ct));
        app.MapPost("/api/observations", (ObservationRequest request, CancellationToken ct) => timeline.Observations(request.Query, request.Id, request.Offset, ct, request.Day));
        app.MapPost("/api/document", (DocumentRequest request, CancellationToken ct) => documents.GetAsync(request, ct));
        app.MapPost("/api/comments", (CommentRequest request, CancellationToken ct) => documents.CommentAsync(request, ct));
        app.MapPost("/api/comments/edit", (ReviewMutationRequest request, CancellationToken ct) => documents.MutateCommentAsync(request, delete: false, ct));
        app.MapPost("/api/comments/delete", (ReviewMutationRequest request, CancellationToken ct) => documents.MutateCommentAsync(request, delete: true, ct));
        app.MapPost("/api/search", (SearchRequest request, CancellationToken ct) => documents.SearchAsync(request, ct));
        app.MapPost("/api/link", (LinkRequest request, CancellationToken ct) => documents.LinkAsync(request.Document, request.Target, ct));
        app.MapFallback("/{**path}", (HttpContext context) => Asset(context.Request.Path.Value ?? "/"));
        return app;
    }

    private static IResult Asset(string path)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = path.TrimStart('/');
        if (name.Length == 0 || !Path.HasExtension(name)) name = "index.html";
        if (name.Contains("..", StringComparison.Ordinal) || name.Contains('\\')) return Results.NotFound();
        var stream = assembly.GetManifestResourceStream("WorkLog.Dashboard.Assets." + name);
        if (stream is null) return Results.NotFound();
        var type = Path.GetExtension(name) switch { ".html" => "text/html; charset=utf-8", ".js" => "text/javascript; charset=utf-8", ".css" => "text/css; charset=utf-8", ".svg" => "image/svg+xml", ".txt" => "text/plain; charset=utf-8", _ => "application/octet-stream" };
        return Results.Stream(stream, type);
    }

    private static void OpenOrPrint(string url, bool noOpen)
    {
        Console.WriteLine(url);
        if (noOpen) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        { Console.Error.WriteLine("Could not open the browser. Open the printed local URL. " + e.Message); }
    }
}
