namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>Begins short-lived persistence transactions without exposing a database provider.</summary>
public interface IPersistenceTransactionFactory
{
    /// <summary>Begins a transaction that can be passed to more than one repository.</summary>
    Task<IPersistenceTransaction> BeginAsync(CancellationToken cancellationToken = default);
}
