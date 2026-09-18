namespace ScheduleAssistant.Application.Abstractions.Settings;

/// <summary>
/// Theme preference stored as user intent. Applying the theme is a later presentation concern.
/// </summary>
public enum ThemePreference
{
    /// <summary>Follow the current Windows theme.</summary>
    System,

    /// <summary>Use a light theme.</summary>
    Light,

    /// <summary>Use a dark theme.</summary>
    Dark
}

/// <summary>
/// User intent when the main window close command is invoked.
/// </summary>
public enum CloseBehavior
{
    /// <summary>Hide the main window and keep the process in the tray.</summary>
    CloseToTray,

    /// <summary>Ask the user what to do.</summary>
    Ask,

    /// <summary>Exit the application.</summary>
    Exit
}

/// <summary>
/// Persisted user preferences. These values express intent only; Windows integrations are implemented by later tasks.
/// </summary>
public sealed record UserSettings
{
    /// <summary>The current serialized settings schema version.</summary>
    public const int CurrentConfigurationVersion = 1;

    /// <summary>Gets the serialized settings schema version.</summary>
    public int ConfigurationVersion { get; init; } = CurrentConfigurationVersion;

    /// <summary>Gets the requested application theme.</summary>
    public ThemePreference Theme { get; init; } = ThemePreference.System;

    /// <summary>Gets the requested main-window close behavior.</summary>
    public CloseBehavior CloseBehavior { get; init; } = CloseBehavior.CloseToTray;

    /// <summary>Gets a value indicating whether the user wants startup with Windows.</summary>
    public bool StartupEnabled { get; init; }

    /// <summary>Gets a value indicating whether new reminders are enabled by default.</summary>
    public bool DefaultRemindersEnabled { get; init; } = true;

    /// <summary>Gets the default reminder offset in minutes relative to a deadline.</summary>
    public int DefaultReminderOffsetMinutes { get; init; } = -1_440;

    /// <summary>Gets a value indicating whether desktop mode is requested.</summary>
    public bool DesktopModeEnabled { get; init; }

    /// <summary>Gets a new instance containing the product defaults.</summary>
    public static UserSettings Default => new();
}
