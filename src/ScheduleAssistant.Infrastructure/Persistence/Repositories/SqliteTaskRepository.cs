using Dapper;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

/// <summary>SQLite repository for tasks and materialized recurrence instances.</summary>
public sealed partial class SqliteTaskRepository : SqliteRepositoryBase, ITaskRepository
{
    private const string Columns = """
        id AS Id,
        title AS Title,
        category_id AS CategoryId,
        priority AS Priority,
        workflow_status AS WorkflowStatus,
        planned_date AS PlannedDate,
        planned_start_time AS PlannedStartTime,
        planned_end_time AS PlannedEndTime,
        deadline_local AS DeadlineLocal,
        deadline_time_zone_id AS DeadlineTimeZoneId,
        deadline_utc AS DeadlineUtc,
        location AS Location,
        description AS Description,
        materials AS Materials,
        notes AS Notes,
        series_id AS SeriesId,
        occurrence_date AS OccurrenceDate,
        is_occurrence_override AS IsOccurrenceOverride,
        created_at_utc AS CreatedAtUtc,
        updated_at_utc AS UpdatedAtUtc,
        completed_at_utc AS CompletedAtUtc,
        version AS Version
        """;

    private readonly SqliteConnectionFactory _connectionFactory;

    /// <summary>Initializes a task repository.</summary>
    public SqliteTaskRepository(
        SqliteConnectionFactory connectionFactory,
        SqlitePersistenceTransactionFactory transactionFactory)
        : base(transactionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<TaskRow>(new CommandDefinition(
            $"SELECT {Columns} FROM tasks WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    /// <inheritdoc />
    public async Task<TaskItem?> GetByIdAsync(
        Guid id,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var sqliteTransaction = RequireTransaction(transaction);
        var row = await sqliteTransaction.Connection.QuerySingleOrDefaultAsync<TaskRow>(Command(
            $"SELECT {Columns} FROM tasks WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            sqliteTransaction,
            cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskItem>> GetPlannedByDateAsync(
        DateOnly plannedOn,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<TaskRow>(new CommandDefinition(
            $"""
            SELECT {Columns}
            FROM tasks
            WHERE planned_date = @Date
            ORDER BY CASE WHEN planned_start_time IS NULL THEN 0 ELSE 1 END,
                     planned_start_time,
                     priority DESC,
                     created_at_utc,
                     id;
            """,
            new { Date = SqliteValueConverter.ToDate(plannedOn) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskItem>> GetByRangeAsync(
        DateOnly rangeStart,
        DateOnly rangeEnd,
        CancellationToken cancellationToken = default)
    {
        EnsureDateRange(rangeStart, rangeEnd);
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<TaskRow>(new CommandDefinition(
            $"""
            SELECT {Columns}
            FROM tasks
            WHERE planned_date BETWEEN @StartDate AND @EndDate
               OR (deadline_local >= @StartLocal AND deadline_local < @EndLocal)
            ORDER BY COALESCE(planned_date, substr(deadline_local, 1, 10)),
                     CASE WHEN planned_start_time IS NULL THEN 0 ELSE 1 END,
                     planned_start_time,
                     deadline_utc,
                     created_at_utc,
                     id;
            """,
            new
            {
                StartDate = SqliteValueConverter.ToDate(rangeStart),
                EndDate = SqliteValueConverter.ToDate(rangeEnd),
                StartLocal = SqliteValueConverter.ToDate(rangeStart) + "T00:00:00.0000000",
                EndLocal = SqliteValueConverter.ToDate(rangeEnd.AddDays(1)) + "T00:00:00.0000000"
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskItem>> GetUpcomingDeadlinesAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset? untilUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<TaskRow>(new CommandDefinition(
            $"""
            SELECT {Columns}
            FROM tasks
            WHERE workflow_status <> @Completed
              AND deadline_utc >= @NowUtc
              AND (@UntilUtc IS NULL OR deadline_utc <= @UntilUtc)
            ORDER BY deadline_utc, priority DESC, created_at_utc, id;
            """,
            new
            {
                Completed = (int)WorkflowStatus.Completed,
                NowUtc = SqliteValueConverter.ToUtc(nowUtc),
                UntilUtc = untilUtc.HasValue ? SqliteValueConverter.ToUtc(untilUtc.Value) : null
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public Task<PersistenceCommitResult<TaskItem>> AddAsync(
        TaskItem item,
        CancellationToken cancellationToken = default)
    {
        return InCommittedTransactionAsync(transaction => AddCoreAsync(item, transaction, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task<PersistenceCommitResult<TaskItem>> AddAsync(
        TaskItem item,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return AddCoreAsync(item, RequireTransaction(transaction), cancellationToken);
    }

    /// <inheritdoc />
    public Task<PersistenceCommitResult<TaskItem>> UpdateAsync(
        TaskItem item,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        return InCommittedTransactionAsync(
            transaction => UpdateCoreAsync(item, expectedVersion, transaction, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<PersistenceCommitResult<TaskItem>> UpdateAsync(
        TaskItem item,
        long expectedVersion,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return UpdateCoreAsync(item, expectedVersion, RequireTransaction(transaction), cancellationToken);
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

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default)
    {
        return InTransactionAsync(
            transaction => DeleteConditionalCoreAsync(id, expectedVersion, transaction, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(
        Guid id,
        long expectedVersion,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return DeleteConditionalCoreAsync(id, expectedVersion, RequireTransaction(transaction), cancellationToken);
    }

    private static async Task<PersistenceCommitResult<TaskItem>> AddCoreAsync(
        TaskItem item,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        await transaction.Connection.ExecuteAsync(Command(
            """
            INSERT INTO tasks (
                id, title, category_id, priority, workflow_status, planned_date,
                planned_start_time, planned_end_time, deadline_local, deadline_time_zone_id,
                deadline_utc, location, description, materials, notes, series_id,
                occurrence_date, is_occurrence_override, created_at_utc, updated_at_utc,
                completed_at_utc, version)
            VALUES (
                @Id, @Title, @CategoryId, @Priority, @WorkflowStatus, @PlannedDate,
                @PlannedStartTime, @PlannedEndTime, @DeadlineLocal, @DeadlineTimeZoneId,
                @DeadlineUtc, @Location, @Description, @Materials, @Notes, @SeriesId,
                @OccurrenceDate, @IsOccurrenceOverride, @CreatedAtUtc, @UpdatedAtUtc,
                @CompletedAtUtc, @Version);
            """,
            Parameters(item),
            transaction,
            cancellationToken)).ConfigureAwait(false);
        var row = await GetRowAsync(item.Id, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The inserted task could not be reloaded.");
        return new PersistenceCommitResult<TaskItem>(Map(row), row.Version);
    }

    private static async Task<PersistenceCommitResult<TaskItem>> UpdateCoreAsync(
        TaskItem item,
        long expectedVersion,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Version != expectedVersion)
        {
            throw Conflict(nameof(TaskItem), item.Id, expectedVersion, item.Version);
        }

        var newVersion = checked(expectedVersion + 1);
        var affected = await transaction.Connection.ExecuteAsync(Command(
            """
            UPDATE tasks SET
                title = @Title,
                category_id = @CategoryId,
                priority = @Priority,
                workflow_status = @WorkflowStatus,
                planned_date = @PlannedDate,
                planned_start_time = @PlannedStartTime,
                planned_end_time = @PlannedEndTime,
                deadline_local = @DeadlineLocal,
                deadline_time_zone_id = @DeadlineTimeZoneId,
                deadline_utc = @DeadlineUtc,
                location = @Location,
                description = @Description,
                materials = @Materials,
                notes = @Notes,
                is_occurrence_override = @IsOccurrenceOverride,
                updated_at_utc = @UpdatedAtUtc,
                completed_at_utc = @CompletedAtUtc,
                version = @NewVersion
            WHERE id = @Id AND version = @ExpectedVersion;
            """,
            Parameters(item, expectedVersion, newVersion),
            transaction,
            cancellationToken)).ConfigureAwait(false);
        if (affected != 1)
        {
            var actualVersion = await GetVersionAsync(item.Id, transaction, cancellationToken).ConfigureAwait(false);
            throw Conflict(nameof(TaskItem), item.Id, expectedVersion, actualVersion);
        }

        var row = await GetRowAsync(item.Id, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The updated task could not be reloaded.");
        return new PersistenceCommitResult<TaskItem>(Map(row), row.Version);
    }

    private static async Task DeleteCoreAsync(
        Guid id,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.Connection.ExecuteAsync(Command(
            "DELETE FROM tasks WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<bool> DeleteConditionalCoreAsync(
        Guid id,
        long expectedVersion,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        var affected = await transaction.Connection.ExecuteAsync(Command(
            "DELETE FROM tasks WHERE id = @Id AND version = @ExpectedVersion;",
            new
            {
                Id = SqliteValueConverter.ToGuid(id),
                ExpectedVersion = expectedVersion
            },
            transaction,
            cancellationToken)).ConfigureAwait(false);
        if (affected == 1)
        {
            return true;
        }

        var actualVersion = await GetVersionAsync(id, transaction, cancellationToken).ConfigureAwait(false);
        if (!actualVersion.HasValue)
        {
            throw new PersistenceNotFoundException(nameof(TaskItem), id);
        }

        throw Conflict(nameof(TaskItem), id, expectedVersion, actualVersion);
    }

    private static async Task<TaskRow?> GetRowAsync(
        Guid id,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        return await transaction.Connection.QuerySingleOrDefaultAsync<TaskRow>(Command(
            $"SELECT {Columns} FROM tasks WHERE id = @Id;",
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
            "SELECT version FROM tasks WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private static object Parameters(TaskItem item, long? expectedVersion = null, long? newVersion = null)
    {
        return new
        {
            Id = SqliteValueConverter.ToGuid(item.Id),
            item.Title,
            CategoryId = SqliteValueConverter.ToGuid(item.CategoryId),
            Priority = (int)item.Priority,
            WorkflowStatus = (int)item.WorkflowStatus,
            PlannedDate = item.PlannedDate.HasValue ? SqliteValueConverter.ToDate(item.PlannedDate.Value) : null,
            PlannedStartTime = item.PlannedStart.HasValue ? SqliteValueConverter.ToTime(item.PlannedStart.Value) : null,
            PlannedEndTime = item.PlannedEnd.HasValue ? SqliteValueConverter.ToTime(item.PlannedEnd.Value) : null,
            DeadlineLocal = item.Deadline is null ? null : SqliteValueConverter.ToDeadlineLocal(item.Deadline),
            DeadlineTimeZoneId = item.Deadline?.TimeZoneId,
            DeadlineUtc = item.Deadline is null ? null : SqliteValueConverter.ToUtc(item.Deadline.Utc),
            item.Location,
            item.Description,
            item.Materials,
            item.Notes,
            SeriesId = item.SeriesId.HasValue ? SqliteValueConverter.ToGuid(item.SeriesId.Value) : null,
            OccurrenceDate = item.OccurrenceDate is null ? null : SqliteValueConverter.ToDate(item.OccurrenceDate.Date),
            IsOccurrenceOverride = item.IsOccurrenceOverride ? 1 : 0,
            CreatedAtUtc = SqliteValueConverter.ToUtc(item.CreatedAtUtc),
            UpdatedAtUtc = SqliteValueConverter.ToUtc(item.UpdatedAtUtc),
            CompletedAtUtc = item.CompletedAtUtc.HasValue ? SqliteValueConverter.ToUtc(item.CompletedAtUtc.Value) : null,
            Version = item.Version,
            ExpectedVersion = expectedVersion,
            NewVersion = newVersion
        };
    }

    private static TaskItem Map(TaskRow row)
    {
        var deadline = row.DeadlineLocal is null
            ? null
            : CreateDeadline(row);
        return TaskItem.Rehydrate(
            SqliteValueConverter.ToGuid(row.Id),
            row.Title,
            SqliteValueConverter.ToGuid(row.CategoryId),
            (TaskPriority)row.Priority,
            (WorkflowStatus)row.WorkflowStatus,
            SqliteValueConverter.ToUtc(row.CreatedAtUtc),
            SqliteValueConverter.ToUtc(row.UpdatedAtUtc),
            row.Version,
            row.PlannedDate is null ? null : SqliteValueConverter.ToDate(row.PlannedDate),
            row.PlannedStartTime is null ? null : SqliteValueConverter.ToTime(row.PlannedStartTime),
            row.PlannedEndTime is null ? null : SqliteValueConverter.ToTime(row.PlannedEndTime),
            deadline,
            row.Location,
            row.Description,
            row.Materials,
            row.Notes,
            row.SeriesId is null ? null : SqliteValueConverter.ToGuid(row.SeriesId),
            row.OccurrenceDate is null ? null : new OccurrenceDate(SqliteValueConverter.ToDate(row.OccurrenceDate)),
            row.IsOccurrenceOverride != 0,
            row.CompletedAtUtc is null ? null : SqliteValueConverter.ToUtc(row.CompletedAtUtc));
    }

    private static ZonedDeadline CreateDeadline(TaskRow row)
    {
        var local = SqliteValueConverter.ToDeadlineLocal(row.DeadlineLocal!);
        return ZonedDeadline.CreateResolvedUtc(
            local.Date,
            local.Time,
            row.DeadlineTimeZoneId!,
            SqliteValueConverter.ToUtc(row.DeadlineUtc!));
    }

    private sealed class TaskRow
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string CategoryId { get; init; } = string.Empty;
        public int Priority { get; init; }
        public int WorkflowStatus { get; init; }
        public string? PlannedDate { get; init; }
        public string? PlannedStartTime { get; init; }
        public string? PlannedEndTime { get; init; }
        public string? DeadlineLocal { get; init; }
        public string? DeadlineTimeZoneId { get; init; }
        public string? DeadlineUtc { get; init; }
        public string? Location { get; init; }
        public string? Description { get; init; }
        public string? Materials { get; init; }
        public string? Notes { get; init; }
        public string? SeriesId { get; init; }
        public string? OccurrenceDate { get; init; }
        public int IsOccurrenceOverride { get; init; }
        public string CreatedAtUtc { get; init; } = string.Empty;
        public string UpdatedAtUtc { get; init; } = string.Empty;
        public string? CompletedAtUtc { get; init; }
        public long Version { get; init; }
    }
}
