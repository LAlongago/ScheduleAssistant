using ScheduleAssistant.Application.Common;

namespace ScheduleAssistant.Application.Tasks;

/// <summary>Application boundary for ordinary task commands and basic queries.</summary>
public interface ITaskUseCases : ITaskQueries
{
    /// <summary>Creates an ordinary task and its applicable reminder metadata.</summary>
    Task<ApplicationResult<TaskDto>> CreateAsync(
        CreateTaskCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Updates editable fields while preserving identity and recurrence identity.</summary>
    Task<ApplicationResult<TaskDto>> UpdateAsync(
        UpdateTaskCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Completes a task and cancels pending reminders atomically.</summary>
    Task<ApplicationResult<TaskDto>> CompleteAsync(
        ChangeTaskStateCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels completion and restores an applicable reminder plan.</summary>
    Task<ApplicationResult<TaskDto>> CancelCompletionAsync(
        ChangeTaskStateCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Moves a pending task to in-progress.</summary>
    Task<ApplicationResult<TaskDto>> StartProcessingAsync(
        ChangeTaskStateCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes an ordinary task with an optimistic version check.</summary>
    Task<ApplicationResult<DeletedTaskDto>> DeleteAsync(
        DeleteTaskCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Gets one task by identity.</summary>
    Task<ApplicationResult<TaskDto>> GetAsync(
        GetTaskQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Gets tasks planned on one local date.</summary>
    Task<ApplicationResult<IReadOnlyList<TaskDto>>> GetPlannedByDateAsync(
        GetTasksByPlannedDateQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Gets incomplete tasks with future deadlines.</summary>
    Task<ApplicationResult<IReadOnlyList<TaskDto>>> GetUpcomingDeadlinesAsync(
        GetUpcomingDeadlinesQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Gets non-archived categories suitable for selection.</summary>
    Task<ApplicationResult<IReadOnlyList<CategoryOptionDto>>> GetCategoryOptionsAsync(
        GetCategoryOptionsQuery query,
        CancellationToken cancellationToken = default);
}
