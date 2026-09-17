namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>The entity rebuilt from the row that was successfully committed.</summary>
/// <typeparam name="TEntity">The domain entity type.</typeparam>
public sealed record PersistenceCommitResult<TEntity>(TEntity Entity, long NewVersion);
