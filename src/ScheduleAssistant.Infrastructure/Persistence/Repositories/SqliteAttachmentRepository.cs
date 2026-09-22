using Dapper;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

/// <summary>SQLite repository for attachment metadata only.</summary>
public sealed class SqliteAttachmentRepository : SqliteRepositoryBase, IAttachmentRepository
{
    private const string Columns = """
        id AS Id,
        task_id AS TaskId,
        display_name AS DisplayName,
        managed_relative_path AS ManagedRelativePath,
        extension AS Extension,
        mime_type AS MimeType,
        size_bytes AS SizeBytes,
        sha256 AS Sha256,
        imported_at_utc AS ImportedAtUtc
        """;

    private readonly SqliteConnectionFactory _connectionFactory;

    /// <summary>Initializes an attachment repository.</summary>
    public SqliteAttachmentRepository(
        SqliteConnectionFactory connectionFactory,
        SqlitePersistenceTransactionFactory transactionFactory)
        : base(transactionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<Attachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<AttachmentRow>(new CommandDefinition(
            $"SELECT {Columns} FROM attachments WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Attachment>> GetByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<AttachmentRow>(new CommandDefinition(
            $"SELECT {Columns} FROM attachments WHERE task_id = @TaskId ORDER BY imported_at_utc, id;",
            new { TaskId = SqliteValueConverter.ToGuid(taskId) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Attachment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<AttachmentRow>(new CommandDefinition(
            $"SELECT {Columns} FROM attachments ORDER BY imported_at_utc, id;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public Task AddAsync(Attachment attachment, CancellationToken cancellationToken = default)
    {
        return InTransactionAsync(transaction => AddCoreAsync(attachment, transaction, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task AddAsync(Attachment attachment, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        return AddCoreAsync(attachment, RequireTransaction(transaction), cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateDisplayNameAsync(Attachment attachment, CancellationToken cancellationToken = default)
    {
        return InTransactionAsync(transaction => UpdateDisplayNameCoreAsync(attachment, transaction, cancellationToken), cancellationToken);
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
        Attachment attachment,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        await transaction.Connection.ExecuteAsync(Command(
            """
            INSERT INTO attachments (
                id, task_id, display_name, managed_relative_path, extension, mime_type,
                size_bytes, sha256, imported_at_utc)
            VALUES (
                @Id, @TaskId, @DisplayName, @ManagedRelativePath, @Extension, @MimeType,
                @SizeBytes, @Sha256, @ImportedAtUtc);
            """,
            Parameters(attachment),
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private static async Task DeleteCoreAsync(
        Guid id,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.Connection.ExecuteAsync(Command(
            "DELETE FROM attachments WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private static async Task UpdateDisplayNameCoreAsync(
        Attachment attachment,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        var affected = await transaction.Connection.ExecuteAsync(Command(
            "UPDATE attachments SET display_name = @DisplayName WHERE id = @Id;",
            new
            {
                Id = SqliteValueConverter.ToGuid(attachment.Id),
                attachment.DisplayName
            },
            transaction,
            cancellationToken)).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new PersistenceNotFoundException(nameof(Attachment), attachment.Id);
        }
    }

    private static object Parameters(Attachment attachment)
    {
        return new
        {
            Id = SqliteValueConverter.ToGuid(attachment.Id),
            TaskId = SqliteValueConverter.ToGuid(attachment.TaskId),
            attachment.DisplayName,
            attachment.ManagedRelativePath,
            attachment.Extension,
            attachment.MimeType,
            attachment.SizeBytes,
            attachment.Sha256,
            ImportedAtUtc = SqliteValueConverter.ToUtc(attachment.ImportedAtUtc)
        };
    }

    private static Attachment Map(AttachmentRow row)
    {
        return Attachment.Rehydrate(
            SqliteValueConverter.ToGuid(row.Id),
            SqliteValueConverter.ToGuid(row.TaskId),
            row.DisplayName,
            row.ManagedRelativePath,
            row.SizeBytes,
            row.Extension,
            row.MimeType,
            row.Sha256,
            SqliteValueConverter.ToUtc(row.ImportedAtUtc));
    }

    private sealed class AttachmentRow
    {
        public string Id { get; init; } = string.Empty;
        public string TaskId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string ManagedRelativePath { get; init; } = string.Empty;
        public string? Extension { get; init; }
        public string? MimeType { get; init; }
        public long SizeBytes { get; init; }
        public string? Sha256 { get; init; }
        public string ImportedAtUtc { get; init; } = string.Empty;
    }
}
