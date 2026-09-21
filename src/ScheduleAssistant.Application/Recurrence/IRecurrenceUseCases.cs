using ScheduleAssistant.Application.Common;

namespace ScheduleAssistant.Application.Recurrence;

/// <summary>Application boundary for recurrence series and occurrence lifecycle operations.</summary>
public interface IRecurrenceUseCases
{
    /// <summary>Creates a series and materializes the normal default window atomically.</summary>
    Task<ApplicationResult<RecurrenceSeriesDto>> CreateAsync(
        CreateRecurrenceSeriesCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Reads one series.</summary>
    Task<ApplicationResult<RecurrenceSeriesDto>> GetAsync(
        GetRecurrenceSeriesQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Reads all series in stable identity order.</summary>
    Task<ApplicationResult<IReadOnlyList<RecurrenceSeriesDto>>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Updates shared fields and rebuilds only ordinary unfinished instances from a date onward.</summary>
    Task<ApplicationResult<RecurrenceSeriesDto>> UpdateAsync(
        UpdateRecurrenceSeriesCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Disables future materialization while preserving existing tasks and history.</summary>
    Task<ApplicationResult<RecurrenceSeriesDto>> DeactivateAsync(
        DeactivateRecurrenceSeriesCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Writes an exclusion and deletes one occurrence in the same transaction.</summary>
    Task<ApplicationResult<DeletedRecurrenceInstanceDto>> DeleteInstanceAsync(
        DeleteRecurrenceInstanceCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Truncates a series and removes only future unfinished ordinary instances.</summary>
    Task<ApplicationResult<DeletedFutureRecurrenceDto>> DeleteFutureAsync(
        DeleteFutureRecurrenceCommand command,
        CancellationToken cancellationToken = default);
}
