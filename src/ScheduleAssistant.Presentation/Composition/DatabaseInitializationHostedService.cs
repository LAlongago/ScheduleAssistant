using Microsoft.Extensions.Hosting;
namespace ScheduleAssistant.Presentation.Composition;

/// <summary>
/// Runs database migrations once during host startup, before the main window and its page graph are resolved.
/// </summary>
public sealed class DatabaseInitializationHostedService : IHostedService
{
    private readonly IDatabaseInitialization _databaseInitialization;

    /// <summary>Initializes the startup migration service.</summary>
    public DatabaseInitializationHostedService(IDatabaseInitialization databaseInitialization)
    {
        _databaseInitialization = databaseInitialization
            ?? throw new ArgumentNullException(nameof(databaseInitialization));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _databaseInitialization.EnsureInitializedAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
