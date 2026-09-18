using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScheduleAssistant.Application.Abstractions.Configuration;
using ScheduleAssistant.Application.Abstractions.Settings;

namespace ScheduleAssistant.Infrastructure.Settings;

/// <summary>
/// Stores the small user-settings document below the application root.
/// </summary>
public sealed class JsonUserSettingsStore : IUserSettingsStore
{
    private readonly IAppPaths _paths;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly object _cacheGate = new();
    private UserSettings _cachedSettings = UserSettings.Default;

    /// <summary>Initializes a settings store without reading or creating any files.</summary>
    public JsonUserSettingsStore(IAppPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _serializerOptions = CreateSerializerOptions();
    }

    /// <inheritdoc />
    public async Task<UserSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var stream = new FileStream(
                _paths.UserSettingsFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4_096,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Fallback(UserSettingsLoadStatus.Corrupted, "JsonRootInvalid");
            }

            if (!TryReadConfigurationVersion(document.RootElement, out var configurationVersion))
            {
                return Fallback(UserSettingsLoadStatus.Corrupted, "ConfigurationVersionMissingOrInvalid");
            }

            if (configurationVersion != UserSettings.CurrentConfigurationVersion)
            {
                return Fallback(
                    UserSettingsLoadStatus.UnsupportedVersion,
                    $"ConfigurationVersionUnsupported:{configurationVersion}");
            }

            var settings = JsonSerializer.Deserialize<UserSettings>(json, _serializerOptions);
            return settings is null
                ? Fallback(UserSettingsLoadStatus.Corrupted, "JsonValueMissing")
                : new UserSettingsLoadResult(settings, UserSettingsLoadStatus.Loaded);
        }
        catch (FileNotFoundException)
        {
            return new UserSettingsLoadResult(GetCachedSettings(), UserSettingsLoadStatus.Missing);
        }
        catch (DirectoryNotFoundException)
        {
            return new UserSettingsLoadResult(GetCachedSettings(), UserSettingsLoadStatus.Missing);
        }
        catch (JsonException)
        {
            return Fallback(UserSettingsLoadStatus.Corrupted, "JsonInvalid");
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            return Fallback(UserSettingsLoadStatus.ReadFailed, $"ReadFailed:{exception.GetType().Name}");
        }
    }

    /// <inheritdoc />
    public async Task<UserSettingsSaveResult> SaveAsync(
        UserSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.ConfigurationVersion != UserSettings.CurrentConfigurationVersion)
        {
            return new UserSettingsSaveResult(
                UserSettingsSaveStatus.Failed,
                $"ConfigurationVersionUnsupported:{settings.ConfigurationVersion}");
        }

        var settingsFilePath = _paths.UserSettingsFilePath;
        var settingsDirectory = Path.GetDirectoryName(settingsFilePath);
        if (string.IsNullOrWhiteSpace(settingsDirectory))
        {
            return new UserSettingsSaveResult(UserSettingsSaveStatus.Failed, "SettingsDirectoryInvalid");
        }

        var temporaryFilePath = Path.Combine(
            settingsDirectory,
            $".user-settings.{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(settingsDirectory);
            var json = JsonSerializer.SerializeToUtf8Bytes(settings, _serializerOptions);
            await using (var stream = new FileStream(
                temporaryFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4_096,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await stream.WriteAsync(json, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            ReplaceAtomically(temporaryFilePath, settingsFilePath);

            lock (_cacheGate)
            {
                _cachedSettings = settings;
            }

            return new UserSettingsSaveResult(UserSettingsSaveStatus.Saved);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsWriteFailure(exception))
        {
            return new UserSettingsSaveResult(UserSettingsSaveStatus.Failed, $"SaveFailed:{exception.GetType().Name}");
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryFilePath);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private UserSettingsLoadResult Fallback(UserSettingsLoadStatus status, string diagnosticCode)
    {
        return new UserSettingsLoadResult(GetCachedSettings(), status, diagnosticCode);
    }

    private UserSettings GetCachedSettings()
    {
        lock (_cacheGate)
        {
            return _cachedSettings;
        }
    }

    private static bool TryReadConfigurationVersion(JsonElement root, out int version)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(
                    property.Name,
                    nameof(UserSettings.ConfigurationVersion),
                    StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.TryGetInt32(out version);
            }
        }

        version = default;
        return false;
    }

    private static void ReplaceAtomically(string temporaryFilePath, string settingsFilePath)
    {
        if (!File.Exists(settingsFilePath))
        {
            File.Move(temporaryFilePath, settingsFilePath);
            return;
        }

        try
        {
            File.Replace(temporaryFilePath, settingsFilePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (PlatformNotSupportedException)
        {
            File.Move(temporaryFilePath, settingsFilePath, overwrite: true);
        }
        catch (NotSupportedException)
        {
            File.Move(temporaryFilePath, settingsFilePath, overwrite: true);
        }
    }

    private static void TryDeleteTemporaryFile(string temporaryFilePath)
    {
        try
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
        catch
        {
            // A failed cleanup must not hide the original settings save result.
        }
    }

    private static bool IsReadFailure(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or SecurityException
            or InvalidDataException
            or NotSupportedException;
    }

    private static bool IsWriteFailure(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or SecurityException
            or InvalidDataException
            or NotSupportedException
            or ArgumentException;
    }
}
