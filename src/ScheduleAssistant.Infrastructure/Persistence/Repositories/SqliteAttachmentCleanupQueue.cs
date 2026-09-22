using System.Diagnostics.CodeAnalysis;
using Dapper;
using ScheduleAssistant.Application.Abstractions.Attachments;
using ScheduleAssistant.Application.Abstractions.Persistence;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

/// <summary>SQLite adapter for the existing cleanup_queue table.</summary>
[SuppressMessage(
    "Naming",
    "CA1711",
    Justification = "The adapter deliberately names the durable cleanup queue it implements.")]
public sealed class SqliteAttachmentCleanupQueue : SqliteRepositoryBase, IAttachmentCleanupQueue
{
    private readonly SqliteConnectionFactory _connectionFactory;

    /// <summary>Initializes the cleanup queue adapter.</summary>
    public SqliteAttachmentCleanupQueue(
        SqliteConnectionFactory connectionFactory,
        SqlitePersistenceTransactionFactory transactionFactory)
        : base(transactionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public Task EnqueueAsync(
        AttachmentCleanupItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return PersistenceExceptionMapper.ExecuteAsync(
            "AttachmentCleanup.Enqueue",
            () => InTransactionAsync(
                transaction => transaction.Connection.ExecuteAsync(Command(
                    """
                    INSERT INTO cleanup_queue (
                        id, item_type, item_id, managed_path, queued_at_utc, attempt_count, last_error)
                    VALUES (
                        @Id, @ItemType, @ItemId, @ManagedPath, @QueuedAtUtc, @AttemptCount, @LastError);
                    """,
                    new
                    {
                        Id = item.Id.ToString("D"),
                        item.ItemType,
                        ItemId = item.ItemId.ToString("D"),
                        ManagedPath = item.ManagedRelativePath,
                        QueuedAtUtc = SqliteValueConverter.ToUtc(item.QueuedAtUtc),
                        item.AttemptCount,
                        LastError = NormalizeError(item.LastErrorCode)
                    },
                    transaction,
                    cancellationToken)),
                cancellationToken));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttachmentCleanupItem>> GetPendingAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumItems, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumItems, 1_000);
        return await PersistenceExceptionMapper.ExecuteAsync(
                "AttachmentCleanup.GetPending",
                async () =>
                {
                    await using var connection = await _connectionFactory
                        .CreateOpenConnectionAsync(cancellationToken)
                        .ConfigureAwait(false);
                    var rows = await connection.QueryAsync<CleanupQueueRow>(new CommandDefinition(
                        """
                        SELECT id AS Id,
                               item_type AS ItemType,
                               item_id AS ItemId,
                               managed_path AS ManagedPath,
                               queued_at_utc AS QueuedAtUtc,
                               attempt_count AS AttemptCount,
                               last_error AS LastErrorCode
                        FROM cleanup_queue
                        ORDER BY queued_at_utc, id
                        LIMIT @Limit;
                        """,
                        new { Limit = maximumItems },
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
                    return rows.Select(Map).ToArray();
                })
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return PersistenceExceptionMapper.ExecuteAsync(
            "AttachmentCleanup.Remove",
            () => InTransactionAsync(
                transaction => transaction.Connection.ExecuteAsync(Command(
                    "DELETE FROM cleanup_queue WHERE id = @Id;",
                    new { Id = id.ToString("D") },
                    transaction,
                    cancellationToken)),
                cancellationToken));
    }

    /// <inheritdoc />
    public Task RecordFailureAsync(
        Guid id,
        string errorCode,
        CancellationToken cancellationToken = default)
    {
        return PersistenceExceptionMapper.ExecuteAsync(
            "AttachmentCleanup.RecordFailure",
            () => InTransactionAsync(
                transaction => transaction.Connection.ExecuteAsync(Command(
                    """
                    UPDATE cleanup_queue
                    SET attempt_count = attempt_count + 1,
                        last_error = @LastError
                    WHERE id = @Id;
                    """,
                    new
                    {
                        Id = id.ToString("D"),
                        LastError = NormalizeError(errorCode)
                    },
                    transaction,
                    cancellationToken)),
                cancellationToken));
    }

    private static AttachmentCleanupItem Map(CleanupQueueRow row)
    {
        return new AttachmentCleanupItem(
            Guid.Parse(row.Id),
            row.ItemType,
            Guid.Parse(row.ItemId),
            row.ManagedPath,
            SqliteValueConverter.ToUtc(row.QueuedAtUtc),
            row.AttemptCount,
            row.LastErrorCode);
    }

    private static string? NormalizeError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= 128 ? normalized : normalized[..128];
    }

    private sealed class CleanupQueueRow
    {
        public string Id { get; init; } = string.Empty;
        public string ItemType { get; init; } = string.Empty;
        public string ItemId { get; init; } = string.Empty;
        public string ManagedPath { get; init; } = string.Empty;
        public string QueuedAtUtc { get; init; } = string.Empty;
        public int AttemptCount { get; init; }
        public string? LastErrorCode { get; init; }
    }
}
