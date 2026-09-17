using Dapper;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

/// <summary>SQLite repository for recurrence series definitions.</summary>
public sealed class SqliteRecurrenceSeriesRepository : SqliteRepositoryBase, IRecurrenceSeriesRepository
{
    private const string Columns = """
        id AS Id,
        title AS Title,
        category_id AS CategoryId,
        priority AS Priority,
        frequency AS Frequency,
        effective_date AS EffectiveDate,
        end_date AS EndDate,
        time_zone_id AS TimeZoneId,
        recurrence_interval AS RecurrenceInterval,
        weekdays AS Weekdays,
        month_day AS MonthDay,
        year_month AS YearMonth,
        year_day AS YearDay,
        planned_start_time AS PlannedStartTime,
        planned_end_time AS PlannedEndTime,
        location AS Location,
        description AS Description,
        materials AS Materials,
        notes AS Notes,
        is_enabled AS IsEnabled,
        created_at_utc AS CreatedAtUtc,
        updated_at_utc AS UpdatedAtUtc,
        version AS Version
        """;

    private readonly SqliteConnectionFactory _connectionFactory;

    /// <summary>Initializes a recurrence series repository.</summary>
    public SqliteRecurrenceSeriesRepository(
        SqliteConnectionFactory connectionFactory,
        SqlitePersistenceTransactionFactory transactionFactory)
        : base(transactionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<RecurrenceSeries?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<SeriesRow>(new CommandDefinition(
            $"SELECT {Columns} FROM recurrence_series WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecurrenceSeries>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<SeriesRow>(new CommandDefinition(
            $"SELECT {Columns} FROM recurrence_series ORDER BY id;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public Task<PersistenceCommitResult<RecurrenceSeries>> AddAsync(
        RecurrenceSeries series,
        CancellationToken cancellationToken = default)
    {
        return InTransactionAsync(transaction => AddCoreAsync(series, transaction, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task<PersistenceCommitResult<RecurrenceSeries>> AddAsync(
        RecurrenceSeries series,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return AddCoreAsync(series, RequireTransaction(transaction), cancellationToken);
    }

    /// <inheritdoc />
    public Task<PersistenceCommitResult<RecurrenceSeries>> UpdateAsync(
        RecurrenceSeries series,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        return InTransactionAsync(
            transaction => UpdateCoreAsync(series, expectedVersion, transaction, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<PersistenceCommitResult<RecurrenceSeries>> UpdateAsync(
        RecurrenceSeries series,
        long expectedVersion,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return UpdateCoreAsync(series, expectedVersion, RequireTransaction(transaction), cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return InTransactionAsync(transaction => DeleteCoreAsync(id, transaction, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        return DeleteCoreAsync(id, RequireTransaction(transaction), cancellationToken);
    }

    private static async Task<PersistenceCommitResult<RecurrenceSeries>> AddCoreAsync(
        RecurrenceSeries series,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        await transaction.Connection.ExecuteAsync(Command(
            """
            INSERT INTO recurrence_series (
                id, title, category_id, priority, frequency, effective_date, end_date,
                time_zone_id, recurrence_interval, weekdays, month_day, year_month, year_day,
                planned_start_time, planned_end_time, location, description, materials, notes,
                is_enabled, created_at_utc, updated_at_utc, version)
            VALUES (
                @Id, @Title, @CategoryId, @Priority, @Frequency, @EffectiveDate, @EndDate,
                @TimeZoneId, @RecurrenceInterval, @Weekdays, @MonthDay, @YearMonth, @YearDay,
                @PlannedStartTime, @PlannedEndTime, @Location, @Description, @Materials, @Notes,
                @IsEnabled, @CreatedAtUtc, @UpdatedAtUtc, @Version);
            """,
            Parameters(series),
            transaction,
            cancellationToken)).ConfigureAwait(false);
        var row = await GetRowAsync(series.Id, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The inserted recurrence series could not be reloaded.");
        return new PersistenceCommitResult<RecurrenceSeries>(Map(row), row.Version);
    }

    private static async Task<PersistenceCommitResult<RecurrenceSeries>> UpdateCoreAsync(
        RecurrenceSeries series,
        long expectedVersion,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series.Version != expectedVersion)
        {
            throw Conflict(nameof(RecurrenceSeries), series.Id, expectedVersion, series.Version);
        }

        var newVersion = checked(expectedVersion + 1);
        var affected = await transaction.Connection.ExecuteAsync(Command(
            """
            UPDATE recurrence_series SET
                title = @Title,
                category_id = @CategoryId,
                priority = @Priority,
                frequency = @Frequency,
                effective_date = @EffectiveDate,
                end_date = @EndDate,
                time_zone_id = @TimeZoneId,
                recurrence_interval = @RecurrenceInterval,
                weekdays = @Weekdays,
                month_day = @MonthDay,
                year_month = @YearMonth,
                year_day = @YearDay,
                planned_start_time = @PlannedStartTime,
                planned_end_time = @PlannedEndTime,
                location = @Location,
                description = @Description,
                materials = @Materials,
                notes = @Notes,
                is_enabled = @IsEnabled,
                updated_at_utc = @UpdatedAtUtc,
                version = @NewVersion
            WHERE id = @Id AND version = @ExpectedVersion;
            """,
            Parameters(series, expectedVersion, newVersion),
            transaction,
            cancellationToken)).ConfigureAwait(false);
        if (affected != 1)
        {
            var actualVersion = await GetVersionAsync(series.Id, transaction, cancellationToken).ConfigureAwait(false);
            throw Conflict(nameof(RecurrenceSeries), series.Id, expectedVersion, actualVersion);
        }

        var row = await GetRowAsync(series.Id, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The updated recurrence series could not be reloaded.");
        return new PersistenceCommitResult<RecurrenceSeries>(Map(row), row.Version);
    }

    private static async Task DeleteCoreAsync(
        Guid id,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.Connection.ExecuteAsync(Command(
            "DELETE FROM recurrence_series WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<SeriesRow?> GetRowAsync(
        Guid id,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        return await transaction.Connection.QuerySingleOrDefaultAsync<SeriesRow>(Command(
            $"SELECT {Columns} FROM recurrence_series WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<long?> GetVersionAsync(
        Guid id,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        return await transaction.Connection.QuerySingleOrDefaultAsync<long?>(Command(
            "SELECT version FROM recurrence_series WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private static object Parameters(RecurrenceSeries series, long? expectedVersion = null, long? newVersion = null)
    {
        var rule = series.Rule;
        return new
        {
            Id = SqliteValueConverter.ToGuid(series.Id),
            series.Title,
            CategoryId = SqliteValueConverter.ToGuid(series.CategoryId),
            Priority = (int)series.Priority,
            Frequency = (int)rule.Frequency,
            EffectiveDate = SqliteValueConverter.ToDate(rule.EffectiveDate),
            EndDate = rule.EndDate.HasValue ? SqliteValueConverter.ToDate(rule.EndDate.Value) : null,
            rule.TimeZoneId,
            RecurrenceInterval = rule.Interval,
            Weekdays = (int)rule.Weekdays,
            rule.MonthDay,
            rule.YearMonth,
            rule.YearDay,
            PlannedStartTime = series.PlannedStart.HasValue ? SqliteValueConverter.ToTime(series.PlannedStart.Value) : null,
            PlannedEndTime = series.PlannedEnd.HasValue ? SqliteValueConverter.ToTime(series.PlannedEnd.Value) : null,
            series.Location,
            series.Description,
            series.Materials,
            series.Notes,
            IsEnabled = series.IsEnabled ? 1 : 0,
            CreatedAtUtc = SqliteValueConverter.ToUtc(series.CreatedAtUtc),
            UpdatedAtUtc = SqliteValueConverter.ToUtc(series.UpdatedAtUtc),
            Version = series.Version,
            ExpectedVersion = expectedVersion,
            NewVersion = newVersion
        };
    }

    private static RecurrenceSeries Map(SeriesRow row)
    {
        var rule = new RecurrenceRule(
            (RecurrenceFrequency)row.Frequency,
            SqliteValueConverter.ToDate(row.EffectiveDate),
            row.TimeZoneId,
            row.EndDate is null ? null : SqliteValueConverter.ToDate(row.EndDate),
            (RecurrenceWeekdayMask)row.Weekdays,
            row.MonthDay,
            row.YearMonth,
            row.YearDay,
            row.RecurrenceInterval);
        return RecurrenceSeries.Rehydrate(
            SqliteValueConverter.ToGuid(row.Id),
            row.Title,
            SqliteValueConverter.ToGuid(row.CategoryId),
            (TaskPriority)row.Priority,
            rule,
            row.PlannedStartTime is null ? null : SqliteValueConverter.ToTime(row.PlannedStartTime),
            row.PlannedEndTime is null ? null : SqliteValueConverter.ToTime(row.PlannedEndTime),
            row.Location,
            row.Description,
            row.Materials,
            row.Notes,
            row.IsEnabled != 0,
            SqliteValueConverter.ToUtc(row.CreatedAtUtc),
            SqliteValueConverter.ToUtc(row.UpdatedAtUtc),
            row.Version);
    }

    private sealed class SeriesRow
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string CategoryId { get; init; } = string.Empty;
        public int Priority { get; init; }
        public int Frequency { get; init; }
        public string EffectiveDate { get; init; } = string.Empty;
        public string? EndDate { get; init; }
        public string TimeZoneId { get; init; } = string.Empty;
        public int RecurrenceInterval { get; init; }
        public int Weekdays { get; init; }
        public int? MonthDay { get; init; }
        public int? YearMonth { get; init; }
        public int? YearDay { get; init; }
        public string? PlannedStartTime { get; init; }
        public string? PlannedEndTime { get; init; }
        public string? Location { get; init; }
        public string? Description { get; init; }
        public string? Materials { get; init; }
        public string? Notes { get; init; }
        public int IsEnabled { get; init; }
        public string CreatedAtUtc { get; init; } = string.Empty;
        public string UpdatedAtUtc { get; init; } = string.Empty;
        public long Version { get; init; }
    }
}
