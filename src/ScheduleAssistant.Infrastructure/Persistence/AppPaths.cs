using ScheduleAssistant.Application.Abstractions.Configuration;

namespace ScheduleAssistant.Infrastructure.Persistence;

/// <summary>
/// Resolves all application data locations from one injectable root directory.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    /// <summary>Initializes paths under the supplied application root.</summary>
    public AppPaths(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    /// <summary>Creates paths under the normal per-user local application directory.</summary>
    public static AppPaths CreateDefault()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("The local application data directory is unavailable.");
        }

        return new AppPaths(Path.Combine(localApplicationData, "ScheduleAssistant"));
    }

    /// <summary>Gets the configured application root.</summary>
    public string RootDirectory { get; }

    /// <summary>Gets the database directory.</summary>
    public string DataDirectory => Path.Combine(RootDirectory, "data");

    /// <summary>Gets the SQLite database file path.</summary>
    public string DatabaseFilePath => Path.Combine(DataDirectory, "schedule.db");

    /// <summary>Gets the managed attachment directory.</summary>
    public string AttachmentsDirectory => Path.Combine(RootDirectory, "attachments");

    /// <summary>Gets the automatic backup directory.</summary>
    public string BackupsDirectory => Path.Combine(RootDirectory, "backups");

    /// <summary>Gets the log directory.</summary>
    public string LogsDirectory => Path.Combine(RootDirectory, "logs");

    /// <summary>Gets the settings directory.</summary>
    public string SettingsDirectory => Path.Combine(RootDirectory, "settings");

    /// <summary>Gets the user settings JSON file path.</summary>
    public string UserSettingsFilePath => Path.Combine(SettingsDirectory, "user-settings.json");

    /// <summary>Creates the application directories needed by persistence and later adapters.</summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(AttachmentsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(SettingsDirectory);
    }
}
