using Microsoft.Extensions.Logging;

namespace ScheduleAssistant.Infrastructure.Logging;

/// <summary>
/// Bounds and rolling policy for the built-in file logger.
/// </summary>
public sealed class FileLoggerOptions
{
    /// <summary>Maximum number of queued log entries before older entries may be discarded.</summary>
    public int QueueCapacity { get; init; } = 1_024;

    /// <summary>Maximum size of one log file before a new file is opened.</summary>
    public long MaxFileBytes { get; init; } = 5 * 1024 * 1024;

    /// <summary>Maximum number of files owned by this provider and retained in the log directory.</summary>
    public int MaxRetainedFiles { get; init; } = 14;

    /// <summary>Minimum level accepted by this provider.</summary>
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;

    /// <summary>Maximum time spent draining the queue when the provider is disposed.</summary>
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Prefix used for files owned by this provider.</summary>
    public string FileNamePrefix { get; init; } = "scheduleassistant-";

    internal FileLoggerOptions Validate()
    {
        if (QueueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity));
        }

        if (MaxFileBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFileBytes));
        }

        if (MaxRetainedFiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRetainedFiles));
        }

        if (MinimumLevel is < LogLevel.Trace or > LogLevel.None)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumLevel));
        }

        if (ShutdownTimeout <= TimeSpan.Zero || ShutdownTimeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(ShutdownTimeout));
        }

        if (string.IsNullOrWhiteSpace(FileNamePrefix)
            || !string.Equals(Path.GetFileName(FileNamePrefix), FileNamePrefix, StringComparison.Ordinal)
            || FileNamePrefix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The log file prefix must be a valid file-name prefix.", nameof(FileNamePrefix));
        }

        return this;
    }
}
