namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>
/// Describes an entity rebuilt from the row written by a repository operation.
/// When the caller supplies an external transaction, the result is tentative until
/// that transaction is committed and <see cref="IsCommitted"/> remains <see langword="false"/>.
/// </summary>
/// <typeparam name="TEntity">The domain entity type.</typeparam>
/// <param name="Entity">The entity rebuilt through the Domain <c>Rehydrate</c> entry point.</param>
/// <param name="NewVersion">The version written by the operation.</param>
/// <param name="IsCommitted">
/// <see langword="true"/> only when the repository owned and completed the transaction;
/// <see langword="false"/> for a caller-owned transaction or a result not yet committed.
/// </param>
public sealed record PersistenceCommitResult<TEntity>(
    TEntity Entity,
    long NewVersion,
    bool IsCommitted = false);
