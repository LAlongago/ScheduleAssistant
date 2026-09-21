using Dapper;
using ScheduleAssistant.Application.Abstractions.Persistence;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

/// <summary>SQLite repository for recurrence occurrence exclusions.</summary>
public sealed class SqliteRecurrenceExclusionRepository : SqliteRepositoryBase, IRecurrenceExclusionRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    /// <summary>Initializes an exclusion repository.</summary>
    public SqliteRecurrenceExclusionRepository(
        SqliteConnectionFactory connectionFactory,
        SqlitePersistenceTransactionFactory transactionFactory)
        : base(transactionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        Guid seriesId,
        DateOnly occurrenceDate,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var value = await connection.QuerySingleAsync<long>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM recurrence_exclusions WHERE series_id = @SeriesId AND occurrence_date = @OccurrenceDate);",
            new
            {
                SeriesId = SqliteValueConverter.ToGuid(seriesId),
                OccurrenceDate = SqliteValueConverter.ToDate(occurrenceDate)
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return value != 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecurrenceExclusion>> GetBySeriesAndRangeAsync(
        Guid seriesId,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        CancellationToken cancellationToken = default)
    {
        EnsureDateRange(rangeStart, rangeEnd);
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ExclusionRow>(new CommandDefinition(
            """
            SELECT series_id AS SeriesId, occurrence_date AS OccurrenceDate
            FROM recurrence_exclusions
            WHERE series_id = @SeriesId
              AND occurrence_date BETWEEN @StartDate AND @EndDate
            ORDER BY occurrence_date;
            """,
            new
            {
                SeriesId = SqliteValueConverter.ToGuid(seriesId),
                StartDate = SqliteValueConverter.ToDate(rangeStart),
                EndDate = SqliteValueConverter.ToDate(rangeEnd)
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows
            .Select(row => new RecurrenceExclusion(
                SqliteValueConverter.ToGuid(row.SeriesId),
                SqliteValueConverter.ToDate(row.OccurrenceDate)))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecurrenceExclusion>> GetBySeriesAndRangeAsync(
        Guid seriesId,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        EnsureDateRange(rangeStart, rangeEnd);
        var sqliteTransaction = RequireTransaction(transaction);
        var rows = await sqliteTransaction.Connection.QueryAsync<ExclusionRow>(Command(
            """
            SELECT series_id AS SeriesId, occurrence_date AS OccurrenceDate
            FROM recurrence_exclusions
            WHERE series_id = @SeriesId
              AND occurrence_date BETWEEN @StartDate AND @EndDate
            ORDER BY occurrence_date;
            """,
            new
            {
                SeriesId = SqliteValueConverter.ToGuid(seriesId),
                StartDate = SqliteValueConverter.ToDate(rangeStart),
                EndDate = SqliteValueConverter.ToDate(rangeEnd)
            },
            sqliteTransaction,
            cancellationToken)).ConfigureAwait(false);
        return rows
            .Select(row => new RecurrenceExclusion(
                SqliteValueConverter.ToGuid(row.SeriesId),
                SqliteValueConverter.ToDate(row.OccurrenceDate)))
            .ToArray();
    }

    /// <inheritdoc />
    public Task AddAsync(RecurrenceExclusion exclusion, CancellationToken cancellationToken = default)
    {
        return InTransactionAsync(transaction => AddCoreAsync(exclusion, transaction, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task AddAsync(
        RecurrenceExclusion exclusion,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return AddCoreAsync(exclusion, RequireTransaction(transaction), cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> AddIfAbsentAsync(
        RecurrenceExclusion exclusion,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return AddIfAbsentCoreAsync(exclusion, RequireTransaction(transaction), cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(
        Guid seriesId,
        DateOnly occurrenceDate,
        CancellationToken cancellationToken = default)
    {
        return InTransactionAsync(
            transaction => DeleteCoreAsync(seriesId, occurrenceDate, transaction, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(
        Guid seriesId,
        DateOnly occurrenceDate,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return DeleteCoreAsync(seriesId, occurrenceDate, RequireTransaction(transaction), cancellationToken);
    }

    private static async Task AddCoreAsync(
        RecurrenceExclusion exclusion,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exclusion);
        await transaction.Connection.ExecuteAsync(Command(
            "INSERT INTO recurrence_exclusions (series_id, occurrence_date) VALUES (@SeriesId, @OccurrenceDate);",
            new
            {
                SeriesId = SqliteValueConverter.ToGuid(exclusion.SeriesId),
                OccurrenceDate = SqliteValueConverter.ToDate(exclusion.OccurrenceDate)
            },
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<bool> AddIfAbsentCoreAsync(
        RecurrenceExclusion exclusion,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exclusion);
        var affected = await transaction.Connection.ExecuteAsync(Command(
            """
            INSERT INTO recurrence_exclusions (series_id, occurrence_date)
            VALUES (@SeriesId, @OccurrenceDate)
            ON CONFLICT(series_id, occurrence_date) DO NOTHING;
            """,
            new
            {
                SeriesId = SqliteValueConverter.ToGuid(exclusion.SeriesId),
                OccurrenceDate = SqliteValueConverter.ToDate(exclusion.OccurrenceDate)
            },
            transaction,
            cancellationToken)).ConfigureAwait(false);
        return affected == 1;
    }

    private static async Task DeleteCoreAsync(
        Guid seriesId,
        DateOnly occurrenceDate,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.Connection.ExecuteAsync(Command(
            "DELETE FROM recurrence_exclusions WHERE series_id = @SeriesId AND occurrence_date = @OccurrenceDate;",
            new
            {
                SeriesId = SqliteValueConverter.ToGuid(seriesId),
                OccurrenceDate = SqliteValueConverter.ToDate(occurrenceDate)
            },
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private sealed class ExclusionRow
    {
        public string SeriesId { get; init; } = string.Empty;

        public string OccurrenceDate { get; init; } = string.Empty;
    }
}
