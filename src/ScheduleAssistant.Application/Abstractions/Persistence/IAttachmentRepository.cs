using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>Persists attachment metadata without touching attachment files.</summary>
public interface IAttachmentRepository
{
    /// <summary>Finds attachment metadata by identity.</summary>
    Task<Attachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets attachment metadata belonging to a task.</summary>
    Task<IReadOnlyList<Attachment>> GetByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Gets all attachment metadata for one bounded startup orphan comparison.</summary>
    Task<IReadOnlyList<Attachment>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Inserts attachment metadata.</summary>
    Task AddAsync(Attachment attachment, CancellationToken cancellationToken = default);

    /// <summary>Inserts attachment metadata in an existing transaction.</summary>
    Task AddAsync(Attachment attachment, IPersistenceTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Updates the user-visible display name without moving the managed file.</summary>
    Task UpdateDisplayNameAsync(Attachment attachment, CancellationToken cancellationToken = default);

    /// <summary>Deletes attachment metadata.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes attachment metadata in an existing transaction.</summary>
    Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default);
}
