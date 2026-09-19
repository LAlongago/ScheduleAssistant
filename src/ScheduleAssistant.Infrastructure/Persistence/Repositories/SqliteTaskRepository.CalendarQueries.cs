using Dapper;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

public sealed partial class SqliteTaskRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskItem>> GetTodayPendingAsync(
        DateOnly todayLocal,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var tomorrowLocal = todayLocal.AddDays(1);
        await using var connection = await _connectionFactory
            .CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        var rows = await connection.QueryAsync<TaskRow>(new CommandDefinition(
            $"""
            SELECT {Columns}
            FROM tasks
            WHERE workflow_status <> @Completed
              AND (
                    (planned_date IS NOT NULL AND planned_date <= @Today)
                 OR (deadline_utc IS NOT NULL AND deadline_utc < @NowUtc)
                 OR (deadline_local >= @TodayStart AND deadline_local < @TomorrowStart)
              )
            ORDER BY
                CASE
                    WHEN deadline_utc IS NOT NULL AND deadline_utc < @NowUtc THEN 0
                    WHEN planned_date < @Today THEN 1
                    ELSE 2
                END,
                CASE WHEN planned_start_time IS NULL THEN 0 ELSE 1 END,
                planned_start_time,
                priority DESC,
                deadline_utc,
                created_at_utc,
                id;
            """,
            new
            {
                Completed = (int)WorkflowStatus.Completed,
                Today = SqliteValueConverter.ToDate(todayLocal),
                TodayStart = SqliteValueConverter.ToDate(todayLocal) + "T00:00:00.0000000",
                TomorrowStart = SqliteValueConverter.ToDate(tomorrowLocal) + "T00:00:00.0000000",
                NowUtc = SqliteValueConverter.ToUtc(nowUtc)
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskItem>> GetDeadlinesAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset? untilUtc,
        bool includeOverdue,
        CancellationToken cancellationToken = default)
    {
        if (untilUtc.HasValue && untilUtc.Value.ToUniversalTime() < nowUtc.ToUniversalTime())
        {
            throw new ArgumentException(
                "The deadline range end must not be earlier than now.",
                nameof(untilUtc));
        }

        await using var connection = await _connectionFactory
            .CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        var rows = await connection.QueryAsync<TaskRow>(new CommandDefinition(
            $"""
            SELECT {Columns}
            FROM tasks
            WHERE workflow_status <> @Completed
              AND deadline_utc IS NOT NULL
              AND (
                    (@IncludeOverdue = 1 AND deadline_utc < @NowUtc)
                 OR (deadline_utc >= @NowUtc AND (@UntilUtc IS NULL OR deadline_utc <= @UntilUtc))
              )
            ORDER BY
                CASE WHEN deadline_utc < @NowUtc THEN 0 ELSE 1 END,
                deadline_utc,
                priority DESC,
                created_at_utc,
                id;
            """,
            new
            {
                Completed = (int)WorkflowStatus.Completed,
                IncludeOverdue = includeOverdue ? 1 : 0,
                NowUtc = SqliteValueConverter.ToUtc(nowUtc),
                UntilUtc = untilUtc.HasValue ? SqliteValueConverter.ToUtc(untilUtc.Value) : null
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }
}
