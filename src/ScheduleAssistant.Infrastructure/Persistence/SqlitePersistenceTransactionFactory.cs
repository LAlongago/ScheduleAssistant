using Microsoft.Data.Sqlite;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Infrastructure.Persistence.Repositories;

namespace ScheduleAssistant.Infrastructure.Persistence;

/// <summary>Begins SQLite transactions while exposing only the application transaction port.</summary>
public sealed class SqlitePersistenceTransactionFactory : IPersistenceTransactionFactory
{
    private readonly SqliteConnectionFactory _connectionFactory;

    /// <summary>Initializes a transaction factory.</summary>
    public SqlitePersistenceTransactionFactory(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<IPersistenceTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        return await BeginSqliteAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task<SqlitePersistenceTransaction> BeginSqliteAsync(CancellationToken cancellationToken)
    {
        var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            return new SqlitePersistenceTransaction(connection, transaction);
        }
        catch (Exception exception)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            if (PersistenceExceptionMapper.IsProviderFailure(exception))
            {
                throw PersistenceExceptionMapper.MapProviderFailure("Transaction.Begin", exception);
            }

            throw;
        }
    }
}
