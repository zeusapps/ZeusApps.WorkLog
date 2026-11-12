using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WorkLog.Configuration;
using WorkLog.Dashboard;
using WorkLog.Tests.Testing;
using Xunit;

namespace WorkLog.Tests.Dashboard;

/// <summary>ORCH-015: a live connection opened while the host is stopping must not delay graceful shutdown.</summary>
public sealed class DashboardShutdownTests
{
    private static readonly TimeSpan StopBudget = TimeSpan.FromSeconds(5);

    /// <summary>The window is a race (a browser reconnecting while the listener is closing), so several rounds make a regression reliable.</summary>
    [Fact]
    public async Task ReconnectingWebSocketClientDuringShutdownDoesNotDelayGracefulStop()
    {
        for (var round = 0; round < 6; round++)
            await ReconnectDuringShutdownAsync(round);
    }

    private static async Task ReconnectDuringShutdownAsync(int round)
    {
        using var box = new TestWorkspace();
        var authorization = new DashboardAuthorization(box.Data);
        var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "live-shutdown-ws-" + round, authorization,
            new DashboardTests.FixtureCommand(box.Repo));
        await app.StartAsync();
        var address = new Uri(DashboardHost.Address(app));
        var connections = app.Services.GetRequiredService<DashboardLiveConnections>();
        using var http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = address };
        (await http.PostAsJsonAsync("/api/bootstrap", new
        {
            token = authorization.Bootstrap()
        })).EnsureSuccessStatusCode();

        var opened = new List<ClientWebSocket>();
        try
        {
            var first = await OpenAsync(http, authorization, address);
            opened.Add(first);
            await WaitUntil(() => connections.ActiveConnectionCount == 1);

            var stopStarted = new TaskCompletionSource();
            var stopping = Task.Run(async () =>
            {
                var watch = Stopwatch.StartNew();
                stopStarted.SetResult();
                await app.StopAsync();
                return watch.Elapsed;
            });
            await stopStarted.Task;

            // Browser behavior: as soon as the old socket is gone, open a new live connection and keep it open.
            using var reconnect = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var reconnected = 0;
            while (!stopping.IsCompleted && !reconnect.IsCancellationRequested)
            {
                try
                {
                    var socket = await OpenAsync(http, authorization, address);
                    opened.Add(socket);
                    reconnected++;
                }
                catch (Exception e) when (e is HttpRequestException or WebSocketException or OperationCanceledException or IOException or InvalidOperationException) { }
                await Task.Delay(5);
            }
            var elapsed = await stopping.WaitAsync(TimeSpan.FromSeconds(40));
            Assert.True(elapsed < StopBudget, $"Round {round}: StopAsync took {elapsed.TotalMilliseconds:F0} ms with {reconnected} reconnects.");
            Assert.Equal(0, connections.ActiveConnectionCount);
        }
        finally
        {
            foreach (var socket in opened)
                socket.Dispose();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConnectionAcceptedAfterStoppingBeginsIsAbortedWithoutRegistryEntry()
    {
        using var box = new TestWorkspace();
        var authorization = new DashboardAuthorization(box.Data);
        var app = DashboardHost.Create(new LocalSettings(box.Data), box.Repo, 0, "live-shutdown-late", authorization,
            new DashboardTests.FixtureCommand(box.Repo));
        await app.StartAsync();
        var address = new Uri(DashboardHost.Address(app));
        var connections = app.Services.GetRequiredService<DashboardLiveConnections>();
        using var http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = address };
        (await http.PostAsJsonAsync("/api/bootstrap", new
        {
            token = authorization.Bootstrap()
        })).EnsureSuccessStatusCode();
        ClientWebSocket? late = null;
        try
        {
            // Complete the negotiate before shutdown so the upgrade is the only step that can land in the window.
            var negotiation = await http.PostAsync("/api/live/negotiate?negotiateVersion=1", new StringContent(""));
            negotiation.EnsureSuccessStatusCode();
            var token = (await negotiation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("connectionToken").GetString()!;

            var stopWatch = Stopwatch.StartNew();
            var stopping = Task.Run(() => app.StopAsync());
            // Wait until DashboardLiveConnections observed the stop (it aborts every slot), then upgrade.
            await WaitUntil(() => connections.StopRequested);
            late = new ClientWebSocket();
            late.Options.SetRequestHeader("Cookie", authorization.CookieName + "=" + authorization.Cookie);
            try
            {
                await late.ConnectAsync(new UriBuilder(address) { Scheme = "ws", Path = "/api/live", Query = "id=" + Uri.EscapeDataString(token) }.Uri, CancellationToken.None);
                await HandshakeAsync(late);
                _ = PumpAsync(late);
            }
            catch (Exception e) when (e is WebSocketException or HttpRequestException or IOException or OperationCanceledException) { }
            await stopping.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(stopWatch.Elapsed < StopBudget, $"StopAsync took {stopWatch.ElapsedMilliseconds} ms.");
            Assert.Equal(0, connections.ActiveConnectionCount);
        }
        finally
        {
            late?.Dispose();
            await app.DisposeAsync();
        }
    }

    private static async Task<ClientWebSocket> OpenAsync(HttpClient http, DashboardAuthorization authorization, Uri address)
    {
        using var negotiation = await http.PostAsync("/api/live/negotiate?negotiateVersion=1", new StringContent(""));
        negotiation.EnsureSuccessStatusCode();
        var token = (await negotiation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("connectionToken").GetString()!;
        var socket = new ClientWebSocket();
        try
        {
            socket.Options.SetRequestHeader("Cookie", authorization.CookieName + "=" + authorization.Cookie);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await socket.ConnectAsync(new UriBuilder(address) { Scheme = "ws", Path = "/api/live", Query = "id=" + Uri.EscapeDataString(token) }.Uri, timeout.Token);
            await HandshakeAsync(socket);
            _ = PumpAsync(socket);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>A browser answers a server close frame; without a reader the server waits out its close timeout.</summary>
    private static async Task PumpAsync(ClientWebSocket socket)
    {
        var buffer = new byte[1024];
        try
        {
            while (socket.State is WebSocketState.Open or WebSocketState.CloseSent)
                if ((await socket.ReceiveAsync(buffer, CancellationToken.None)).MessageType == WebSocketMessageType.Close)
                    break;
            if (socket.State == WebSocketState.CloseReceived)
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }
        catch (Exception e) when (e is WebSocketException or ObjectDisposedException or OperationCanceledException or InvalidOperationException) { }
    }

    private static async Task HandshakeAsync(ClientWebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, timeout.Token);
        var buffer = new byte[256];
        await socket.ReceiveAsync(buffer, timeout.Token);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            deadline.Token.ThrowIfCancellationRequested();
            await Task.Delay(5, deadline.Token);
        }
    }
}
