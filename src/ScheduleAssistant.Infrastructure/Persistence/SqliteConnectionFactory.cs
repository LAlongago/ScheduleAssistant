using Microsoft.Data.Sqlite;

namespace ScheduleAssistant.Infrastructure.Persistence;

/// <summary>
/// Creates short-lived SQLite connections with the required connection pragmas.
/// </summary>
public sealed class SqliteConnectionFactory
{
    private const int BusyTimeoutMilliseconds = 5_000;
    private readonly AppPaths _paths;

    /// <summary>Initializes a connection factory rooted at the supplied paths.</summary>
    public SqliteConnectionFactory(AppPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    /// <summary>
    /// Opens a new connection and enables WAL, foreign keys, a busy timeout, and normal synchronous mode.
    /// </summary>
    public async Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _paths.DatabaseFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecutePragmaAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);
            await ExecutePragmaAsync(connection, $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds};", cancellationToken).ConfigureAwait(false);
            await ExecutePragmaAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);
            await ExecutePragmaAsync(connection, "PRAGMA synchronous = NORMAL;", cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task ExecutePragmaAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
