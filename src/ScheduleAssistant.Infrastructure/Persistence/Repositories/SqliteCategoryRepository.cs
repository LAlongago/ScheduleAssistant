using Dapper;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

/// <summary>SQLite repository for category metadata.</summary>
public sealed class SqliteCategoryRepository : SqliteRepositoryBase, ICategoryRepository
{
    private const string Columns = """
        id AS Id,
        name AS Name,
        color_hex AS ColorHex,
        sort_order AS SortOrder,
        is_built_in AS IsBuiltIn,
        is_archived AS IsArchived,
        created_at_utc AS CreatedAtUtc,
        updated_at_utc AS UpdatedAtUtc,
        version AS Version
        """;

    private readonly SqliteConnectionFactory _connectionFactory;

    /// <summary>Initializes a category repository.</summary>
    public SqliteCategoryRepository(
        SqliteConnectionFactory connectionFactory,
        SqlitePersistenceTransactionFactory transactionFactory)
        : base(transactionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<CategoryRow>(new CommandDefinition(
            $"SELECT {Columns} FROM categories WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<CategoryRow>(new CommandDefinition(
            $"SELECT {Columns} FROM categories ORDER BY sort_order, name, id;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public Task<PersistenceCommitResult<Category>> AddAsync(
        Category category,
        CancellationToken cancellationToken = default)
    {
        return InTransactionAsync(transaction => AddCoreAsync(category, transaction, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task<PersistenceCommitResult<Category>> AddAsync(
        Category category,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return AddCoreAsync(category, RequireTransaction(transaction), cancellationToken);
    }

    /// <inheritdoc />
    public Task<PersistenceCommitResult<Category>> UpdateAsync(
        Category category,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        return InTransactionAsync(
            transaction => UpdateCoreAsync(category, expectedVersion, transaction, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<PersistenceCommitResult<Category>> UpdateAsync(
        Category category,
        long expectedVersion,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return UpdateCoreAsync(category, expectedVersion, RequireTransaction(transaction), cancellationToken);
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

    private static async Task<PersistenceCommitResult<Category>> AddCoreAsync(
        Category category,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(category);
        await transaction.Connection.ExecuteAsync(Command(
            """
            INSERT INTO categories (
                id, name, color_hex, sort_order, is_built_in, is_archived,
                created_at_utc, updated_at_utc, version)
            VALUES (
                @Id, @Name, @ColorHex, @SortOrder, @IsBuiltIn, @IsArchived,
                @CreatedAtUtc, @UpdatedAtUtc, @Version);
            """,
            Parameters(category),
            transaction,
            cancellationToken)).ConfigureAwait(false);
        var row = await GetRowAsync(category.Id, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The inserted category could not be reloaded.");
        return new PersistenceCommitResult<Category>(Map(row), row.Version);
    }

    private static async Task<PersistenceCommitResult<Category>> UpdateCoreAsync(
        Category category,
        long expectedVersion,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (category.Version != expectedVersion)
        {
            throw Conflict(nameof(Category), category.Id, expectedVersion, category.Version);
        }

        var newVersion = checked(expectedVersion + 1);
        var affected = await transaction.Connection.ExecuteAsync(Command(
            """
            UPDATE categories SET
                name = @Name,
                color_hex = @ColorHex,
                sort_order = @SortOrder,
                is_archived = @IsArchived,
                updated_at_utc = @UpdatedAtUtc,
                version = @NewVersion
            WHERE id = @Id AND version = @ExpectedVersion;
            """,
            Parameters(category, expectedVersion, newVersion),
            transaction,
            cancellationToken)).ConfigureAwait(false);
        if (affected != 1)
        {
            var actualVersion = await GetVersionAsync(category.Id, transaction, cancellationToken).ConfigureAwait(false);
            throw Conflict(nameof(Category), category.Id, expectedVersion, actualVersion);
        }

        var row = await GetRowAsync(category.Id, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The updated category could not be reloaded.");
        return new PersistenceCommitResult<Category>(Map(row), row.Version);
    }

    private static async Task DeleteCoreAsync(
        Guid id,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.Connection.ExecuteAsync(Command(
            "DELETE FROM categories WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<CategoryRow?> GetRowAsync(
        Guid id,
        SqlitePersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        return await transaction.Connection.QuerySingleOrDefaultAsync<CategoryRow>(Command(
            $"SELECT {Columns} FROM categories WHERE id = @Id;",
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
            "SELECT version FROM categories WHERE id = @Id;",
            new { Id = SqliteValueConverter.ToGuid(id) },
            transaction,
            cancellationToken)).ConfigureAwait(false);
    }

    private static object Parameters(Category category, long? expectedVersion = null, long? newVersion = null)
    {
        return new
        {
            Id = SqliteValueConverter.ToGuid(category.Id),
            category.Name,
            category.ColorHex,
            category.SortOrder,
            IsBuiltIn = category.IsBuiltIn ? 1 : 0,
            IsArchived = category.IsArchived ? 1 : 0,
            CreatedAtUtc = SqliteValueConverter.ToUtc(category.CreatedAtUtc),
            UpdatedAtUtc = SqliteValueConverter.ToUtc(category.UpdatedAtUtc),
            Version = category.Version,
            ExpectedVersion = expectedVersion,
            NewVersion = newVersion
        };
    }

    private static Category Map(CategoryRow row)
    {
        return Category.Rehydrate(
            SqliteValueConverter.ToGuid(row.Id),
            row.Name,
            row.ColorHex,
            row.SortOrder,
            row.IsBuiltIn != 0,
            row.IsArchived != 0,
            SqliteValueConverter.ToUtc(row.CreatedAtUtc),
            SqliteValueConverter.ToUtc(row.UpdatedAtUtc),
            row.Version);
    }

    private sealed class CategoryRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string ColorHex { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public int IsBuiltIn { get; init; }
        public int IsArchived { get; init; }
        public string CreatedAtUtc { get; init; } = string.Empty;
        public string UpdatedAtUtc { get; init; } = string.Empty;
        public long Version { get; init; }
    }
}
