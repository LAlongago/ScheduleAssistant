namespace ScheduleAssistant.Application.Abstractions.Settings;

/// <summary>Describes the outcome of a user-settings load.</summary>
public enum UserSettingsLoadStatus
{
    /// <summary>A valid current-version file was loaded.</summary>
    Loaded,

    /// <summary>No settings file exists; defaults are being used.</summary>
    Missing,

    /// <summary>The file exists but is not valid settings JSON.</summary>
    Corrupted,

    /// <summary>The file uses a configuration version this application does not support.</summary>
    UnsupportedVersion,

    /// <summary>The file could not be read.</summary>
    ReadFailed
}

/// <summary>Result returned by a settings load, including safe fallback values.</summary>
public sealed record UserSettingsLoadResult(
    UserSettings Settings,
    UserSettingsLoadStatus Status,
    string? DiagnosticCode = null)
{
    /// <summary>Gets a value indicating whether the result can be used without user intervention.</summary>
    public bool IsUsable => Status is UserSettingsLoadStatus.Loaded or UserSettingsLoadStatus.Missing;
}

/// <summary>Describes the outcome of a user-settings save.</summary>
public enum UserSettingsSaveStatus
{
    /// <summary>The replacement completed successfully.</summary>
    Saved,

    /// <summary>The requested settings could not be saved.</summary>
    Failed
}

/// <summary>Result returned by a settings save.</summary>
public sealed record UserSettingsSaveResult(
    UserSettingsSaveStatus Status,
    string? DiagnosticCode = null)
{
    /// <summary>Gets a value indicating whether the settings file was replaced.</summary>
    public bool IsSuccess => Status == UserSettingsSaveStatus.Saved;
}
