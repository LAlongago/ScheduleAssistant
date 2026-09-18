namespace ScheduleAssistant.Application.Abstractions.Settings;

/// <summary>
/// Loads and atomically saves the user's local settings.
/// </summary>
public interface IUserSettingsStore
{
    /// <summary>
    /// Loads settings without changing the settings file. Missing or invalid files produce a diagnostic result and safe settings.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the file read.</param>
    Task<UserSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves settings through a same-directory temporary file and atomic replacement.
    /// </summary>
    /// <param name="settings">The current user intent to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the file write.</param>
    Task<UserSettingsSaveResult> SaveAsync(UserSettings settings, CancellationToken cancellationToken = default);
}
