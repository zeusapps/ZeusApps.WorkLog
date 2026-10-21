using WorkLog.Infrastructure;

namespace WorkLog.Configuration;

public sealed class LocalSettings
{
    public string DataDirectory
    {
        get;
    }
    public string ConfigPath => Path.Combine(DataDirectory, "config.json");
    public WorkLogConfiguration Configuration
    {
        get; private set;
    }

    public LocalSettings(string? dataDirectory = null)
    {
        DataDirectory = Path.GetFullPath(dataDirectory
            ?? Environment.GetEnvironmentVariable("WORKLOG_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZeusApps.WorkLog"));

        Configuration = File.Exists(ConfigPath)
            ? Json.Decode<WorkLogConfiguration>(File.ReadAllText(ConfigPath))
            : new WorkLogConfiguration();
        Configuration.Normalize();
    }

    private LocalSettings(string dataDirectory, WorkLogConfiguration configuration)
    {
        DataDirectory = dataDirectory;
        Configuration = configuration;
    }

    internal LocalSettings Copy() => new(DataDirectory, Json.Decode<WorkLogConfiguration>(Json.Encode(Configuration)));

    internal void Adopt(LocalSettings planned) => Configuration = planned.Configuration;

    public void Save()
    {
        AtomicFile.Write(ConfigPath, Json.Encode(Configuration));
    }
}
