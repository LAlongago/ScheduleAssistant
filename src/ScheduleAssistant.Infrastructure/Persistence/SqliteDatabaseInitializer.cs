using Dapper;
using Microsoft.Data.Sqlite;
using ScheduleAssistant.Infrastructure.Persistence.Migrations;

namespace ScheduleAssistant.Infrastructure.Persistence;

/// <summary>
/// Explicitly creates the database directory and applies immutable embedded migrations.
/// </summary>
public sealed class SqliteDatabaseInitializer
{
    private const string BootstrapMigrationTableSql = """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            version         INTEGER PRIMARY KEY,
            name            TEXT NOT NULL UNIQUE,
            checksum        TEXT NOT NULL,
            applied_at_utc  TEXT NOT NULL
        );
        """;

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a database initializer.</summary>
    public SqliteDatabaseInitializer(SqliteConnectionFactory connectionFactory, TimeProvider? timeProvider = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Applies all pending migrations in one transaction and verifies checksums of applied migrations.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                BootstrapMigrationTableSql,
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            var appliedRows = (await connection.QueryAsync<AppliedMigrationRow>(new CommandDefinition(
                "SELECT version AS Version, name AS Name, checksum AS Checksum FROM schema_migrations ORDER BY version;",
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToDictionary(row => row.Version);
            var migrations = MigrationCatalog.Load(typeof(SqliteDatabaseInitializer).Assembly);
            var knownVersions = migrations.Select(migration => migration.Version).ToHashSet();

            if (appliedRows.Keys.Any(version => !knownVersions.Contains(version)))
            {
                throw new InvalidOperationException("The database contains a migration newer than this application.");
            }

            foreach (var migration in migrations)
            {
                if (appliedRows.TryGetValue(migration.Version, out var applied))
                {
                    if (!string.Equals(applied.Name, migration.Name, StringComparison.Ordinal)
                        || !string.Equals(applied.Checksum, migration.Checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Migration {migration.Version:000} does not match its recorded name or checksum.");
                    }

                    continue;
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    migration.Sql,
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO schema_migrations (version, name, checksum, applied_at_utc)
                    VALUES (@Version, @Name, @Checksum, @AppliedAtUtc);
                    """,
                    new
                    {
                        migration.Version,
                        migration.Name,
                        migration.Checksum,
                        AppliedAtUtc = SqliteValueConverter.ToUtc(_timeProvider.GetUtcNow())
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await TryRollbackAsync(transaction).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task TryRollbackAsync(SqliteTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Preserve the migration error; disposing the transaction still closes the connection.
        }
    }

    private sealed class AppliedMigrationRow
    {
        public int Version { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Checksum { get; init; } = string.Empty;
    }
}
