using Microsoft.Data.Sqlite;
using ScheduleAssistant.Application.Abstractions.Persistence;

namespace ScheduleAssistant.Infrastructure.Persistence;

/// <summary>Infrastructure-owned implementation of the application transaction port.</summary>
public sealed class SqlitePersistenceTransaction : IPersistenceTransaction
{
    private bool _completed;

    internal SqlitePersistenceTransaction(SqliteConnection connection, SqliteTransaction transaction)
    {
        Connection = connection;
        Transaction = transaction;
    }

    internal SqliteConnection Connection { get; }

    internal SqliteTransaction Transaction { get; }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsureActive();
        await Transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_completed)
        {
            return;
        }

        await Transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_completed)
        {
            await RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await Transaction.DisposeAsync().ConfigureAwait(false);
        await Connection.DisposeAsync().ConfigureAwait(false);
    }

    internal void EnsureActive()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The persistence transaction has already completed.");
        }
    }
}
