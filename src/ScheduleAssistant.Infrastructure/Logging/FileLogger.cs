using Microsoft.Extensions.Logging;

namespace ScheduleAssistant.Infrastructure.Logging;

internal sealed class FileLogger : ILogger
{
    private readonly FileLoggerProvider _provider;
    private readonly string _categoryName;

    public FileLogger(FileLoggerProvider provider, string categoryName)
    {
        _provider = provider;
        _categoryName = TechnicalLogFields.SanitizeCategory(categoryName);
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        // Scopes can contain task content and are intentionally not persisted by this provider.
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _provider.IsEnabled(logLevel);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var entry = _provider.CreateEntry(
            logLevel,
            eventId,
            _categoryName,
            TechnicalLogFields.Extract(state, exception));
        _provider.Enqueue(entry);
    }
}
