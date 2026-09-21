using ScheduleAssistant.Infrastructure.Persistence;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>
/// Provides one process-wide database initialization task to page ViewModels.
/// </summary>
public interface IDatabaseInitialization
{
    /// <summary>Ensures migrations have completed before a page reads application data.</summary>
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs the explicit database initializer at most once and keeps page queries asynchronous.
/// </summary>
public sealed class DatabaseInitialization : IDatabaseInitialization
{
    private readonly Lazy<Task> _initialization;

    /// <summary>Initializes the one-shot database initialization gate.</summary>
    public DatabaseInitialization(SqliteDatabaseInitializer initializer)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        _initialization = new Lazy<Task>(
            () => Task.Run(
                () => initializer.InitializeAsync(CancellationToken.None),
                CancellationToken.None),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        return _initialization.Value.WaitAsync(cancellationToken);
    }
}
