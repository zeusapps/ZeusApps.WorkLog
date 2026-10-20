using System.Net.Http.Headers;
using System.Text.Json;
using WorkLog.Cli;

namespace WorkLog.Updates;

public sealed class ReleaseClient(HttpClient client)
{
    public static HttpClient CreateHttpClient() => new(new HttpClientHandler { MaxResponseHeadersLength = 32 })
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    public async Task<ReleaseCheck> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            using var request = Request(ReleaseInfo.Endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                DateTimeOffset? retry = response.Headers.RetryAfter?.Date;
                if (response.Headers.RetryAfter?.Delta is { } delta) retry = DateTimeOffset.UtcNow + delta;
                if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values)
                    && long.TryParse(values.FirstOrDefault(), out var seconds))
                    retry = DateTimeOffset.FromUnixTimeSeconds(seconds);
                return new(null, $"GitHub release check failed (HTTP {(int)response.StatusCode}).", retry);
            }

            using var content = new MemoryStream();
            await CopyBoundedAsync(response.Content, content, 256 * 1024, timeout.Token);
            using var document = JsonDocument.Parse(content.ToArray());
            var root = document.RootElement;
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return new(null);
            var tag = root.GetProperty("tag_name").GetString();
            if (tag is null || !tag.StartsWith('v') || ReleaseInfo.ParseVersion(tag[1..]) is null) return new(null);
            var version = tag[1..];
            var package = ReleaseInfo.PackageName(version);
            var assets = root.GetProperty("assets").EnumerateArray().ToArray();
            string? Asset(string name) => assets.FirstOrDefault(asset => asset.GetProperty("name").GetString() == name)
                is { ValueKind: JsonValueKind.Object } item ? item.GetProperty("browser_download_url").GetString() : null;
            var release = new ReleaseInfo(version, root.GetProperty("html_url").GetString() ?? "", Asset(package) ?? "", Asset(package + ".sha256") ?? "");
            return new(release.IsValid() ? release : null);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException or JsonException
            or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return new(null, "Release check failed: " + exception.Message);
        }
    }

    public async Task DownloadAsync(string url, string destination, long maximumBytes, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var request = Request(url);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await CopyBoundedAsync(response.Content, output, maximumBytes, timeout.Token);
    }

    private static HttpRequestMessage Request(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("WorkLog/" + ApplicationInfo.Version);
        return request;
    }

    private static async Task CopyBoundedAsync(HttpContent content, Stream output, long maximumBytes, CancellationToken token)
    {
        if (content.Headers.ContentLength > maximumBytes) throw new IOException("Release response exceeds the size limit.");
        await using var source = await content.ReadAsStreamAsync(token);
        var buffer = new byte[64 * 1024];
        long total = 0;
        int count;
        while ((count = await source.ReadAsync(buffer, token)) != 0)
        {
            total += count;
            if (total > maximumBytes) throw new IOException("Release response exceeds the size limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
    }
}
