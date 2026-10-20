using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using WorkLog.Cli;
using WorkLog.Configuration;
using WorkLog.Journaling;
using WorkLog.Repositories;

namespace WorkLog.Mcp;

internal static class McpServerHost
{
    public static async Task RunAsync(LocalSettings settings, string? repositoryPath, CancellationToken cancellationToken = default)
    {
        var repository = repositoryPath is null ? null : new RepositoryRegistry(settings).Get(repositoryPath);
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton(new Journal(settings, repository));
        builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation { Name = "worklog", Version = ApplicationInfo.Version };
        }).WithStdioServerTransport().WithTools<JournalTools>(McpJson.Options)
            .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, token) =>
            {
                var unknown = UnknownFieldValidation.Find(context.Params);
                if (unknown is not null)
                {
                    return new CallToolResult
                    {
                        IsError = true,
                        Content = [new TextContentBlock { Text = $"Unknown field '{unknown}'. Check the tool's input schema for supported fields." }]
                    };
                }
                return await next(context, token);
            }));

        using var host = builder.Build();
        await host.RunAsync(cancellationToken);
    }
}
