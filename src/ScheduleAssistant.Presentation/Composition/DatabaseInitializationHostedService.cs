using Microsoft.Extensions.Hosting;
using ScheduleAssistant.Infrastructure.Persistence;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>
/// Runs database migrations once during host startup, before the main window and its page graph are resolved.
/// </summary>
public sealed class DatabaseInitializationHostedService : IHostedService
{
    private readonly SqliteDatabaseInitializer _initializer;

    /// <summary>Initializes the startup migration service.</summary>
    public DatabaseInitializationHostedService(SqliteDatabaseInitializer initializer)
    {
        _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _initializer.InitializeAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
