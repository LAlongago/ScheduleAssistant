using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Recurrence;

/// <summary>Ensures ordinary task instances exist for a local-date range.</summary>
public interface IRecurrenceMaterializer
{
    /// <summary>
    /// Materializes the inclusive range, defaulting to today minus 31 days through today plus
    /// 400 days. The operation is idempotent and creates no deadlines or reminders.
    /// </summary>
    Task MaterializeAsync(
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Transaction-aware materialization used when a series command must commit its template and
/// newly created instances as one unit.
/// </summary>
public interface ITransactionalRecurrenceMaterializer : IRecurrenceMaterializer
{
    /// <summary>Materializes one series through a caller-owned transaction without committing it.</summary>
    Task<IReadOnlyList<MaterializedRecurrenceTask>> MaterializeSeriesAsync(
        RecurrenceSeries series,
        DateOnly fromDate,
        DateOnly toDate,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);
}
