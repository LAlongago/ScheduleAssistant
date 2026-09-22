using Dapper;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

public sealed partial class SqliteTaskRepository
{
    /// <inheritdoc />
    public async Task<PersistenceCommitResult<TaskItem>?> AddIfAbsentAsync(
        TaskItem item,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.SeriesId is null || item.OccurrenceDate is null)
        {
            throw new ArgumentException("Only recurrence instances can use the recurrence idempotency operation.", nameof(item));
        }

        var sqliteTransaction = RequireTransaction(transaction);
        var affected = await sqliteTransaction.Connection.ExecuteAsync(Command(
            $"""
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
                @CompletedAtUtc, @Version)
            ON CONFLICT(series_id, occurrence_date) WHERE series_id IS NOT NULL DO NOTHING;
            """,
            Parameters(item),
            sqliteTransaction,
            cancellationToken)).ConfigureAwait(false);
        if (affected == 0)
        {
            return null;
        }

        var row = await GetRowAsync(item.Id, sqliteTransaction, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The inserted recurrence task could not be reloaded.");
        return new PersistenceCommitResult<TaskItem>(Map(row), row.Version);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskItem>> DeleteUncompletedNonOverrideBySeriesFromDateAsync(
        Guid seriesId,
        DateOnly fromDate,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var sqliteTransaction = RequireTransaction(transaction);
        var rows = await sqliteTransaction.Connection.QueryAsync<TaskRow>(Command(
            $"""
            SELECT {Columns}
            FROM tasks
            WHERE series_id = @SeriesId
              AND occurrence_date >= @FromDate
              AND workflow_status <> @Completed
              AND is_occurrence_override = 0
            ORDER BY occurrence_date, id;
            """,
            new
            {
                SeriesId = SqliteValueConverter.ToGuid(seriesId),
                FromDate = SqliteValueConverter.ToDate(fromDate),
                Completed = (int)WorkflowStatus.Completed
            },
            sqliteTransaction,
            cancellationToken)).ConfigureAwait(false);
        var tasks = rows.Select(Map).ToArray();
        if (tasks.Length == 0)
        {
            return tasks;
        }

        await sqliteTransaction.Connection.ExecuteAsync(Command(
            """
            DELETE FROM tasks
            WHERE series_id = @SeriesId
              AND occurrence_date >= @FromDate
              AND workflow_status <> @Completed
              AND is_occurrence_override = 0;
            """,
            new
            {
                SeriesId = SqliteValueConverter.ToGuid(seriesId),
                FromDate = SqliteValueConverter.ToDate(fromDate),
                Completed = (int)WorkflowStatus.Completed
            },
            sqliteTransaction,
            cancellationToken)).ConfigureAwait(false);
        return tasks;
    }
}
