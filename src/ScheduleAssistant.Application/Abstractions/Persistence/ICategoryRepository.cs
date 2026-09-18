using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>Persists task categories.</summary>
public interface ICategoryRepository
{
    /// <summary>Finds a category by identity.</summary>
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Reads a category through a caller-owned transaction.</summary>
    Task<Category?> GetByIdAsync(
        Guid id,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Gets categories ordered by configured sort order and name.</summary>
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Inserts a category.</summary>
    Task<PersistenceCommitResult<Category>> AddAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>Inserts a category in an existing transaction.</summary>
    Task<PersistenceCommitResult<Category>> AddAsync(
        Category category,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Updates a category only when its stored version equals the expected version.</summary>
    Task<PersistenceCommitResult<Category>> UpdateAsync(
        Category category,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>Updates a category in an existing transaction.</summary>
    Task<PersistenceCommitResult<Category>> UpdateAsync(
        Category category,
        long expectedVersion,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a category; SQLite rejects deletion while tasks reference it.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a category in an existing transaction.</summary>
    Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default);
}
