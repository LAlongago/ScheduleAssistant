namespace ScheduleAssistant.Application.Abstractions.Configuration;

/// <summary>
/// Resolves the directories and files owned by one ScheduleAssistant installation.
/// </summary>
public interface IAppPaths
{
    /// <summary>Gets the application data root.</summary>
    string RootDirectory { get; }

    /// <summary>Gets the directory containing the SQLite database.</summary>
    string DataDirectory { get; }

    /// <summary>Gets the SQLite database file path.</summary>
    string DatabaseFilePath { get; }

    /// <summary>Gets the directory containing managed attachments.</summary>
    string AttachmentsDirectory { get; }

    /// <summary>Gets the directory containing automatic backups.</summary>
    string BackupsDirectory { get; }

    /// <summary>Gets the directory containing application log files.</summary>
    string LogsDirectory { get; }

    /// <summary>Gets the directory containing user settings.</summary>
    string SettingsDirectory { get; }

    /// <summary>Gets the JSON file containing user settings.</summary>
    string UserSettingsFilePath { get; }
}
