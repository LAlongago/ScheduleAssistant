using Microsoft.Extensions.Logging;
using ScheduleAssistant.Application.Abstractions.Settings;
using ScheduleAssistant.Infrastructure.Logging;
using ScheduleAssistant.Infrastructure.Persistence;
using ScheduleAssistant.Infrastructure.Settings;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class UserSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_WhenFileIsMissing_ShouldReturnDefaultsWithoutCreatingFiles()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppPaths(temporaryDirectory.Path);
        var store = new JsonUserSettingsStore(paths);

        var result = await store.LoadAsync();

        Assert.Equal(UserSettingsLoadStatus.Missing, result.Status);
        Assert.True(result.IsUsable);
        Assert.Equal(UserSettings.Default, result.Settings);
        Assert.False(File.Exists(paths.UserSettingsFilePath));
        Assert.False(Directory.Exists(paths.SettingsDirectory));
    }

    [Fact]
    public async Task SaveAsync_WhenSettingsAreSaved_ShouldReloadAllValuesFromTheSharedSettingsRoot()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppPaths(temporaryDirectory.Path);
        var settings = new UserSettings
        {
            Theme = ThemePreference.Dark,
            CloseBehavior = CloseBehavior.Ask,
            StartupEnabled = true,
            DefaultRemindersEnabled = false,
            DefaultReminderOffsetMinutes = -30,
            DesktopModeEnabled = true
        };

        var saveResult = await new JsonUserSettingsStore(paths).SaveAsync(settings);
        var loadResult = await new JsonUserSettingsStore(paths).LoadAsync();

        Assert.True(saveResult.IsSuccess);
        Assert.Equal(UserSettingsLoadStatus.Loaded, loadResult.Status);
        Assert.Equal(settings, loadResult.Settings);
        Assert.Equal(
            Path.Combine(temporaryDirectory.Path, "settings", "user-settings.json"),
            paths.UserSettingsFilePath);
        Assert.Contains("\"configurationVersion\": 1", File.ReadAllText(paths.UserSettingsFilePath));
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsCorrupted_ShouldPreserveFileAndReturnDiagnosticStatus()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppPaths(temporaryDirectory.Path);
        Directory.CreateDirectory(paths.SettingsDirectory);
        File.WriteAllText(paths.UserSettingsFilePath, "{ not valid json");
        var original = File.ReadAllText(paths.UserSettingsFilePath);

        var result = await new JsonUserSettingsStore(paths).LoadAsync();

        Assert.Equal(UserSettingsLoadStatus.Corrupted, result.Status);
        Assert.False(result.IsUsable);
        Assert.Equal("JsonInvalid", result.DiagnosticCode);
        Assert.Equal(UserSettings.Default, result.Settings);
        Assert.Equal(original, File.ReadAllText(paths.UserSettingsFilePath));
    }

    [Fact]
    public async Task LoadAsync_WhenConfigurationVersionIsUnsupported_ShouldPreserveFileAndReturnDiagnosticStatus()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppPaths(temporaryDirectory.Path);
        Directory.CreateDirectory(paths.SettingsDirectory);
        File.WriteAllText(
            paths.UserSettingsFilePath,
            "{\"configurationVersion\":99,\"theme\":\"dark\",\"startupEnabled\":true}");
        var original = File.ReadAllText(paths.UserSettingsFilePath);

        var result = await new JsonUserSettingsStore(paths).LoadAsync();

        Assert.Equal(UserSettingsLoadStatus.UnsupportedVersion, result.Status);
        Assert.False(result.IsUsable);
        Assert.Equal("ConfigurationVersionUnsupported:99", result.DiagnosticCode);
        Assert.Equal(UserSettings.Default, result.Settings);
        Assert.Equal(original, File.ReadAllText(paths.UserSettingsFilePath));
    }

    [Fact]
    public async Task SaveAsync_WhenExistingFileCannotBeReplaced_ShouldPreserveOldSettingsAndCache()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppPaths(temporaryDirectory.Path);
        var store = new JsonUserSettingsStore(paths);
        var originalSettings = new UserSettings { Theme = ThemePreference.Light };
        var replacementSettings = new UserSettings { Theme = ThemePreference.Dark };
        Assert.True((await store.SaveAsync(originalSettings)).IsSuccess);
        var originalFile = File.ReadAllText(paths.UserSettingsFilePath);

        using (new FileStream(paths.UserSettingsFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = await store.SaveAsync(replacementSettings);

            Assert.Equal(UserSettingsSaveStatus.Failed, result.Status);
            Assert.False(result.IsSuccess);
        }

        Assert.Equal(originalFile, File.ReadAllText(paths.UserSettingsFilePath));
        var loadResult = await store.LoadAsync();
        Assert.Equal(UserSettingsLoadStatus.Loaded, loadResult.Status);
        Assert.Equal(originalSettings, loadResult.Settings);
    }
}

public sealed class FileLoggerProviderTests
{
    [Fact]
    public async Task FileLogger_WhenLogContainsBusinessText_ShouldPersistOnlyTechnicalFields()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppPaths(temporaryDirectory.Path);
        var provider = new FileLoggerProvider(
            paths,
            new FileLoggerOptions
            {
                MinimumLevel = LogLevel.Trace,
                FileNamePrefix = "dev021-privacy-"
            });
        var logger = provider.CreateLogger("ScheduleAssistant.Infrastructure.Tests.TechnicalComponent");
        var state = new[]
        {
            new KeyValuePair<string, object?>("TaskId", Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new KeyValuePair<string, object?>("Title", "secret task title"),
            new KeyValuePair<string, object?>("Description", "private task body"),
            new KeyValuePair<string, object?>("AttachmentPath", "C:\\private\\attachment.pdf"),
            new KeyValuePair<string, object?>("ErrorCode", "SETTINGS_READ_FAILED")
        };
        var scope = logger.BeginScope(new[]
        {
            new KeyValuePair<string, object?>("Notes", "private scope content")
        });

        logger.Log(
            LogLevel.Error,
            new EventId(421, "SettingsRead"),
            state,
            new InvalidOperationException("unfiltered private exception message"),
            static (_, _) => "unfiltered formatter text");
        scope?.Dispose();
        await provider.FlushAsync();
        await provider.DisposeAsync();

        var logFiles = Directory.GetFiles(paths.LogsDirectory, "dev021-privacy-*.log");
        var content = string.Join(Environment.NewLine, logFiles.Select(File.ReadAllText));

        Assert.Contains("\"eventId\":421", content);
        Assert.Contains("\"TaskId\":\"11111111-1111-1111-1111-111111111111\"", content);
        Assert.Contains("SETTINGS_READ_FAILED", content);
        Assert.Contains("InvalidOperationException", content);
        Assert.DoesNotContain("secret task title", content);
        Assert.DoesNotContain("private task body", content);
        Assert.DoesNotContain("C:\\private\\attachment.pdf", content);
        Assert.DoesNotContain("private scope content", content);
        Assert.DoesNotContain("unfiltered private exception message", content);
        Assert.DoesNotContain("unfiltered formatter text", content);
    }

    [Fact]
    public async Task FileLogger_WhenFilesRoll_ShouldRetainOwnLogsAndFlushBeforeClose()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppPaths(temporaryDirectory.Path);
        var provider = new FileLoggerProvider(
            paths,
            new FileLoggerOptions
            {
                QueueCapacity = 128,
                MaxFileBytes = 256,
                MaxRetainedFiles = 2,
                MinimumLevel = LogLevel.Trace,
                FileNamePrefix = "dev021-roll-",
                ShutdownTimeout = TimeSpan.FromSeconds(2)
            });
        var logger = provider.CreateLogger("ScheduleAssistant.Infrastructure.Tests.RollingComponent");

        if (logger.IsEnabled(LogLevel.Information))
        {
            for (var index = 0; index < 30; index++)
            {
                logger.Log(
                    LogLevel.Information,
                    new EventId(500 + index),
                    new[]
                    {
                        new KeyValuePair<string, object?>("Operation", "Write"),
                        new KeyValuePair<string, object?>("Count", index)
                    },
                    exception: null,
                    static (_, _) => string.Empty);
            }
        }

        Directory.CreateDirectory(paths.LogsDirectory);
        var unrelatedFile = Path.Combine(paths.LogsDirectory, "keep-me.txt");
        File.WriteAllText(unrelatedFile, "not owned by the provider");
        var similarlyPrefixedFile = Path.Combine(paths.LogsDirectory, "dev021-roll-external.log");
        File.WriteAllText(similarlyPrefixedFile, "also not owned by the provider");
        await provider.FlushAsync();
        await provider.DisposeAsync();

        var logFiles = Directory.GetFiles(paths.LogsDirectory, "dev021-roll-????????-*.log");
        Assert.InRange(logFiles.Length, 1, 2);
        Assert.Contains(logFiles, file => File.ReadAllText(file).Contains("\"eventId\":529", StringComparison.Ordinal));
        Assert.True(File.Exists(unrelatedFile));
        Assert.Equal("not owned by the provider", File.ReadAllText(unrelatedFile));
        Assert.True(File.Exists(similarlyPrefixedFile));
        Assert.Equal("also not owned by the provider", File.ReadAllText(similarlyPrefixedFile));
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ScheduleAssistant-DEV021-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
