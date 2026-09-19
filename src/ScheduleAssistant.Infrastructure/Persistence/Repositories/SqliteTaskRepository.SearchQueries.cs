using Dapper;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

public sealed partial class SqliteTaskRepository
{
    private const string SearchWhere = """
        WHERE (@Keyword IS NULL OR
               title LIKE @Keyword ESCAPE '\' OR
               location LIKE @Keyword ESCAPE '\' OR
               description LIKE @Keyword ESCAPE '\' OR
               materials LIKE @Keyword ESCAPE '\' OR
               notes LIKE @Keyword ESCAPE '\')
          AND (@CategoryId IS NULL OR category_id = @CategoryId)
          AND (@Priority IS NULL OR priority = @Priority)
          AND (@WorkflowStatus IS NULL OR workflow_status = @WorkflowStatus)
          AND (
                @IsOverdue IS NULL
             OR (@IsOverdue = 1
                 AND workflow_status <> @Completed
                 AND deadline_utc IS NOT NULL
                 AND deadline_utc < @NowUtc)
             OR (@IsOverdue = 0
                 AND NOT (
                       workflow_status <> @Completed
                   AND deadline_utc IS NOT NULL
                   AND deadline_utc < @NowUtc))
          )
        """;

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskItem>> SearchAsync(
        TaskSearchFilter filter,
        long offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        EnsureSearchPage(offset, limit);
        await using var connection = await _connectionFactory
            .CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        var rows = await connection.QueryAsync<TaskRow>(new CommandDefinition(
            $"""
            SELECT {Columns}
            FROM tasks
            {SearchWhere}
            ORDER BY
                CASE WHEN planned_date IS NULL THEN 1 ELSE 0 END,
                planned_date,
                CASE WHEN planned_start_time IS NULL THEN 0 ELSE 1 END,
                planned_start_time,
                CASE WHEN deadline_utc IS NULL THEN 1 ELSE 0 END,
                deadline_utc,
                priority DESC,
                created_at_utc,
                id
            LIMIT @Limit OFFSET @Offset;
            """,
            SearchParameters(filter, offset, limit),
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public async Task<long> CountSearchAsync(
        TaskSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        await using var connection = await _connectionFactory
            .CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        return await connection.QuerySingleAsync<long>(new CommandDefinition(
            $"""
            SELECT COUNT(1)
            FROM tasks
            {SearchWhere};
            """,
            SearchParameters(filter, offset: null, limit: null),
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static void EnsureSearchPage(long offset, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
    }

    private static object SearchParameters(
        TaskSearchFilter filter,
        long? offset,
        int? limit)
    {
        return new
        {
            Keyword = EscapeLikePattern(filter.Keyword),
            CategoryId = filter.CategoryId.HasValue
                ? SqliteValueConverter.ToGuid(filter.CategoryId.Value)
                : null,
            Priority = filter.Priority.HasValue ? (int?)filter.Priority.Value : null,
            WorkflowStatus = filter.WorkflowStatus.HasValue ? (int?)filter.WorkflowStatus.Value : null,
            IsOverdue = filter.IsOverdue.HasValue
                ? (int?)(filter.IsOverdue.Value ? 1 : 0)
                : null,
            Completed = (int)WorkflowStatus.Completed,
            NowUtc = SqliteValueConverter.ToUtc(filter.NowUtc),
            Offset = offset,
            Limit = limit
        };
    }

    private static string? EscapeLikePattern(string? keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            return null;
        }

        var escaped = keyword
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
