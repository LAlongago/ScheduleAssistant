using Dapper;
using ScheduleAssistant.Application.Abstractions.Persistence;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

public abstract class SqliteRepositoryBase
{
    private readonly SqlitePersistenceTransactionFactory _transactionFactory;

    protected SqliteRepositoryBase(SqlitePersistenceTransactionFactory transactionFactory)
    {
        _transactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
    }

    protected Task<T> InTransactionAsync<T>(
        Func<SqlitePersistenceTransaction, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return InTransactionCoreAsync(operation, cancellationToken);
    }

    protected Task InTransactionAsync(
        Func<SqlitePersistenceTransaction, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return InTransactionCoreAsync(operation, cancellationToken);
    }

    protected static SqlitePersistenceTransaction RequireTransaction(
        IPersistenceTransaction transaction,
        string parameterName = "transaction")
    {
        ArgumentNullException.ThrowIfNull(transaction, parameterName);
        return transaction as SqlitePersistenceTransaction
            ?? throw new ArgumentException("The transaction was not created by this Infrastructure persistence boundary.", parameterName);
    }

    protected static CommandDefinition Command(
        string sql,
        object? parameters,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        transaction.EnsureActive();
        return new CommandDefinition(sql, parameters, transaction.Transaction, cancellationToken: cancellationToken);
    }

    protected static void EnsureDateRange(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new ArgumentException("The end date must not be earlier than the start date.", nameof(end));
        }
    }

    protected static PersistenceConflictException Conflict(
        string entityName,
        Guid entityId,
        long expectedVersion,
        long? actualVersion)
    {
        return new PersistenceConflictException(entityName, entityId, expectedVersion, actualVersion);
    }

    private async Task<T> InTransactionCoreAsync<T>(
        Func<SqlitePersistenceTransaction, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _transactionFactory.BeginSqliteAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await operation(transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await TryRollbackAsync(transaction).ConfigureAwait(false);
            throw;
        }
    }

    private async Task InTransactionCoreAsync(
        Func<SqlitePersistenceTransaction, Task> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _transactionFactory.BeginSqliteAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation(transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await TryRollbackAsync(transaction).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task TryRollbackAsync(SqlitePersistenceTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Preserve the original database error; disposal still closes the connection.
        }
    }
}
