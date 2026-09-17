using Dapper;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

/// <summary>SQLite repository for reminder delivery state.</summary>
public sealed class SqliteReminderRepository : SqliteRepositoryBase, IReminderRepository
{
    private const string Columns = """
        id AS Id,
        task_id AS TaskId,
        relative_offset_minutes AS RelativeOffsetMinutes,
        scheduled_at_utc AS ScheduledAtUtc,
        delivered_at_utc AS DeliveredAtUtc,
        status AS Status,
        deduplication_key AS DeduplicationKey,
        error_code AS ErrorCode
        """;

    private readonly SqliteConnectionFactory _connectionFactory;

    /// <summary>Initializes a reminder repository.</summary>
    public SqliteReminderRepository(
        SqliteConnectionFactory connectionFactory,
        SqlitePersistenceTransactionFactory transactionFactory)
        : base(transactionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<Reminder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<ReminderRow>(new CommandDefinition(
            $"SELECT {Columns} FROM reminders WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Reminder>> GetByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ReminderRow>(new CommandDefinition(
            $"SELECT {Columns} FROM reminders WHERE task_id = @TaskId ORDER BY scheduled_at_utc, id;",
            new { TaskId = SqliteValueConverter.ToGuid(taskId) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public async Task<Reminder?> GetNextPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<ReminderRow>(new CommandDefinition(
            $"SELECT {Columns} FROM reminders WHERE status = @Status ORDER BY scheduled_at_utc, id LIMIT 1;",
            new { Status = (int)ReminderStatus.Pending },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    /// <inheritdoc />
    public Task AddAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        return InTransactionAsync(transaction => AddCoreAsync(reminder, transaction, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task AddAsync(Reminder reminder, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        return AddCoreAsync(reminder, RequireTransaction(transaction), cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        return InTransactionAsync(transaction => UpdateCoreAsync(reminder, transaction, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateAsync(Reminder reminder, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        return UpdateCoreAsync(reminder, RequireTransaction(transaction), cancellationToken);
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

    private static async Task AddCoreAsync(
        Reminder reminder,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        await transaction.Connection.ExecuteAsync(Command(
            """
            INSERT INTO reminders (
                id, task_id, relative_offset_minutes, scheduled_at_utc, delivered_at_utc,
                status, deduplication_key, error_code)
            VALUES (
                @Id, @TaskId, @RelativeOffsetMinutes, @ScheduledAtUtc, @DeliveredAtUtc,
                @Status, @DeduplicationKey, @ErrorCode);
            """,
            Parameters(reminder),
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private static async Task UpdateCoreAsync(
        Reminder reminder,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        var affected = await transaction.Connection.ExecuteAsync(Command(
            """
            UPDATE reminders SET
                task_id = @TaskId,
                relative_offset_minutes = @RelativeOffsetMinutes,
                scheduled_at_utc = @ScheduledAtUtc,
                delivered_at_utc = @DeliveredAtUtc,
                status = @Status,
                deduplication_key = @DeduplicationKey,
                error_code = @ErrorCode
            WHERE id = @Id;
            """,
            Parameters(reminder),
            transaction,
            cancellationToken)).ConfigureAwait(false);
        if (affected != 1)
        {
            throw new KeyNotFoundException($"Reminder '{reminder.Id}' was not found.");
        }
    }

    private static async Task DeleteCoreAsync(
        Guid id,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.Connection.ExecuteAsync(Command(
            "DELETE FROM reminders WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private static object Parameters(Reminder reminder)
    {
        return new
        {
            Id = SqliteValueConverter.ToGuid(reminder.Id),
            TaskId = SqliteValueConverter.ToGuid(reminder.TaskId),
            reminder.RelativeOffsetMinutes,
            ScheduledAtUtc = SqliteValueConverter.ToUtc(reminder.ScheduledAtUtc),
            DeliveredAtUtc = reminder.DeliveredAtUtc.HasValue ? SqliteValueConverter.ToUtc(reminder.DeliveredAtUtc.Value) : null,
            Status = (int)reminder.Status,
            reminder.DeduplicationKey,
            reminder.ErrorCode
        };
    }

    private static Reminder Map(ReminderRow row)
    {
        return Reminder.Rehydrate(
            SqliteValueConverter.ToGuid(row.Id),
            SqliteValueConverter.ToGuid(row.TaskId),
            row.RelativeOffsetMinutes,
            SqliteValueConverter.ToUtc(row.ScheduledAtUtc),
            row.DeliveredAtUtc is null ? null : SqliteValueConverter.ToUtc(row.DeliveredAtUtc),
            (ReminderStatus)row.Status,
            row.DeduplicationKey,
            row.ErrorCode);
    }

    private sealed class ReminderRow
    {
        public string Id { get; init; } = string.Empty;
        public string TaskId { get; init; } = string.Empty;
        public int RelativeOffsetMinutes { get; init; }
        public string ScheduledAtUtc { get; init; } = string.Empty;
        public string? DeliveredAtUtc { get; init; }
        public int Status { get; init; }
        public string DeduplicationKey { get; init; } = string.Empty;
        public string? ErrorCode { get; init; }
    }
}
