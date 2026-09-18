using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ScheduleAssistant.Application.Abstractions.Configuration;

namespace ScheduleAssistant.Infrastructure.Logging;

/// <summary>
/// Writes privacy-filtered structured log lines through an asynchronous bounded queue.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);

    private readonly IAppPaths _paths;
    private readonly FileLoggerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Channel<FileLogWorkItem> _queue;
    private readonly CancellationTokenSource _shutdownCancellation = new();
    private readonly object _lifecycleGate = new();
    private Task? _writerTask;
    private int _disposed;

    /// <summary>Initializes a provider without touching the file system.</summary>
    public FileLoggerProvider(
        IAppPaths paths,
        FileLoggerOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _options = (options ?? new FileLoggerOptions()).Validate();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _queue = Channel.CreateBounded<FileLogWorkItem>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            AllowSynchronousContinuations = false
        });
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);
        return new FileLogger(this, categoryName);
    }

    /// <summary>Flushes entries currently accepted by the provider within the caller's cancellation policy.</summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var writerTask = Volatile.Read(ref _writerTask);
        if (writerTask is null || writerTask.IsCompleted)
        {
            return;
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_queue.Writer.TryWrite(FileLogWorkItem.CreateFlush(completion)))
        {
            return;
        }

        await Task.WhenAny(completion.Task, writerTask).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Disposes the provider after a finite best-effort queue drain.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _queue.Writer.TryComplete();
        }

        var writerTask = Volatile.Read(ref _writerTask);
        if (writerTask is null)
        {
            return;
        }

        try
        {
            await writerTask.WaitAsync(_options.ShutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _shutdownCancellation.Cancel();
        }
        catch
        {
            // File-system failures are intentionally isolated from application shutdown.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // A logger provider must never prevent the host from shutting down.
        }
    }

    internal bool IsEnabled(LogLevel logLevel)
    {
        return Volatile.Read(ref _disposed) == 0
            && logLevel != LogLevel.None
            && logLevel >= _options.MinimumLevel;
    }

    internal FileLogEntry CreateEntry(
        LogLevel logLevel,
        EventId eventId,
        string categoryName,
        IReadOnlyDictionary<string, string> fields)
    {
        return new FileLogEntry(
            _timeProvider.GetUtcNow().ToUniversalTime(),
            logLevel,
            eventId,
            categoryName,
            fields);
    }

    internal void Enqueue(FileLogEntry entry)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        EnsureWriterStarted();
        if (Volatile.Read(ref _disposed) == 0)
        {
            _queue.Writer.TryWrite(FileLogWorkItem.CreateEntry(entry));
        }
    }

    private void EnsureWriterStarted()
    {
        if (Volatile.Read(ref _writerTask) is not null)
        {
            return;
        }

        lock (_lifecycleGate)
        {
            if (_writerTask is null && Volatile.Read(ref _disposed) == 0)
            {
                _writerTask = ProcessQueueAsync();
            }
        }
    }

    private async Task ProcessQueueAsync()
    {
        FileWriterState writerState = new(_paths, _options);
        try
        {
            await foreach (var workItem in _queue.Reader.ReadAllAsync(_shutdownCancellation.Token).ConfigureAwait(false))
            {
                if (workItem.FlushCompletion is not null)
                {
                    await writerState.FlushAsync().ConfigureAwait(false);
                    workItem.FlushCompletion.TrySetResult(true);
                    continue;
                }

                if (workItem.Entry is not null)
                {
                    await writerState.WriteAsync(workItem.Entry).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (_shutdownCancellation.IsCancellationRequested)
        {
            // A bounded shutdown timeout may abandon queued entries.
        }
        catch
        {
            // The provider is deliberately fail-closed and never reports through ILogger itself.
        }
        finally
        {
            await writerState.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed record FileLogWorkItem(
        FileLogEntry? Entry,
        TaskCompletionSource<bool>? FlushCompletion)
    {
        public static FileLogWorkItem CreateEntry(FileLogEntry entry) => new(entry, null);

        public static FileLogWorkItem CreateFlush(TaskCompletionSource<bool> completion) => new(null, completion);
    }

    internal sealed record FileLogEntry(
        DateTimeOffset TimestampUtc,
        LogLevel Level,
        EventId EventId,
        string CategoryName,
        IReadOnlyDictionary<string, string> Fields);

    private sealed class FileWriterState : IAsyncDisposable
    {
        private readonly IAppPaths _paths;
        private readonly FileLoggerOptions _options;
        private StreamWriter? _writer;
        private DateOnly? _currentDate;
        private long _currentBytes;
        private string? _currentFilePath;
        private bool _disabled;

        public FileWriterState(IAppPaths paths, FileLoggerOptions options)
        {
            _paths = paths;
            _options = options;
        }

        public async Task WriteAsync(FileLogEntry entry)
        {
            if (_disabled)
            {
                return;
            }

            try
            {
                var line = Serialize(entry);
                var lineBytes = Encoding.UTF8.GetByteCount(line) + 1;
                EnsureWriter(entry.TimestampUtc, lineBytes);
                if (_writer is null)
                {
                    return;
                }

                await _writer.WriteLineAsync(line).ConfigureAwait(false);
                await _writer.FlushAsync().ConfigureAwait(false);
                _currentBytes += lineBytes;
            }
            catch
            {
                Disable();
            }
        }

        public async Task FlushAsync()
        {
            if (_writer is null)
            {
                return;
            }

            try
            {
                await _writer.FlushAsync().ConfigureAwait(false);
            }
            catch
            {
                Disable();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_writer is null)
            {
                return;
            }

            try
            {
                await _writer.FlushAsync().ConfigureAwait(false);
                await _writer.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // There is no safe logging path for a provider disposal failure.
            }
            finally
            {
                _writer = null;
            }
        }

        private void EnsureWriter(DateTimeOffset timestampUtc, int lineBytes)
        {
            var date = DateOnly.FromDateTime(timestampUtc.UtcDateTime);
            if (_writer is not null
                && _currentDate == date
                && (_currentBytes == 0 || _currentBytes + lineBytes <= _options.MaxFileBytes))
            {
                return;
            }

            CloseWriter();
            Directory.CreateDirectory(_paths.LogsDirectory);

            var candidate = FindAppendCandidate(date, lineBytes);
            var filePath = candidate?.Path
                ?? Path.Combine(
                    _paths.LogsDirectory,
                    $"{_options.FileNamePrefix}{date:yyyyMMdd}-{(candidate?.Sequence ?? FindNextSequence(date)):000}.log");

            var fileStream = new FileStream(
                filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4_096,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            _writer = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            _currentDate = date;
            _currentFilePath = filePath;
            _currentBytes = fileStream.Length;
            PruneProviderFiles();
        }

        private Candidate? FindAppendCandidate(DateOnly date, int lineBytes)
        {
            var candidates = GetCandidates(date).OrderBy(candidate => candidate.Sequence).ToArray();
            return candidates.LastOrDefault(candidate => candidate.Length + lineBytes <= _options.MaxFileBytes);
        }

        private int FindNextSequence(DateOnly date)
        {
            var candidates = GetCandidates(date).ToArray();
            return candidates.Length is 0 ? 0 : candidates.Max(candidate => candidate.Sequence) + 1;
        }

        private IEnumerable<Candidate> GetCandidates(DateOnly date)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(_paths.LogsDirectory);
            }
            catch
            {
                yield break;
            }

            var prefix = $"{_options.FileNamePrefix}{date:yyyyMMdd}-";
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (!name.StartsWith(prefix, StringComparison.Ordinal)
                    || !name.EndsWith(".log", StringComparison.Ordinal)
                    || !int.TryParse(
                        name[prefix.Length..^4],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var sequence))
                {
                    continue;
                }

                long length;
                try
                {
                    length = new FileInfo(file).Length;
                }
                catch
                {
                    continue;
                }

                yield return new Candidate(file, sequence, length);
            }
        }

        private void PruneProviderFiles()
        {
            try
            {
                var files = Directory.EnumerateFiles(_paths.LogsDirectory)
                    .Where(file =>
                    {
                        var name = Path.GetFileName(file);
                        return IsProviderLogFileName(name)
                            && !string.Equals(file, _currentFilePath, StringComparison.OrdinalIgnoreCase);
                    })
                    .Select(file => new FileInfo(file))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ThenByDescending(file => file.Name, StringComparer.Ordinal)
                    .ToArray();

                foreach (var file in files.Skip(Math.Max(0, _options.MaxRetainedFiles - 1)))
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        // A stale log that cannot be deleted must not affect current logging.
                    }
                }
            }
            catch
            {
                // Retention is best effort and is restricted to the provider's own file pattern.
            }
        }

        private bool IsProviderLogFileName(string name)
        {
            if (!name.StartsWith(_options.FileNamePrefix, StringComparison.Ordinal)
                || !name.EndsWith(".log", StringComparison.Ordinal))
            {
                return false;
            }

            var stem = name[_options.FileNamePrefix.Length..^4];
            var separatorIndex = stem.IndexOf('-');
            if (separatorIndex != 8 || stem.LastIndexOf('-') != separatorIndex)
            {
                return false;
            }

            return DateOnly.TryParseExact(
                       stem[..separatorIndex],
                       "yyyyMMdd",
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.None,
                       out _)
                && int.TryParse(
                    stem[(separatorIndex + 1)..],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out _);
        }

        private void CloseWriter()
        {
            if (_writer is null)
            {
                return;
            }

            try
            {
                _writer.Dispose();
            }
            catch
            {
                // Continue with a fresh file or disable logging below.
            }
            finally
            {
                _writer = null;
                _currentDate = null;
                _currentFilePath = null;
                _currentBytes = 0;
            }
        }

        private void Disable()
        {
            _disabled = true;
            CloseWriter();
        }

        private static string Serialize(FileLogEntry entry)
        {
            var payload = new
            {
                timestampUtc = entry.TimestampUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                level = entry.Level.ToString(),
                eventId = entry.EventId.Id,
                category = entry.CategoryName,
                fields = entry.Fields
            };
            return JsonSerializer.Serialize(payload, SerializerOptions);
        }

        private sealed record Candidate(string Path, int Sequence, long Length);
    }
}
