using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>
/// Opens the ordinary-task editor through one reusable Presentation entry point.
/// </summary>
public interface ITaskEditorService
{
    /// <summary>Raised after a task has been committed by the editor.</summary>
    event EventHandler<TaskEditorSavedEventArgs>? TaskSaved;

    /// <summary>Opens a new ordinary-task editor.</summary>
    Task OpenCreateAsync(
        DateOnly? prefilledPlannedDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>Opens an editor for an existing ordinary task.</summary>
    Task OpenEditAsync(Guid taskId, CancellationToken cancellationToken = default);
}

/// <summary>Describes a task-editor save and preserves the Application refresh signal.</summary>
public sealed class TaskEditorSavedEventArgs : EventArgs
{
    /// <summary>Initializes a save notification.</summary>
    public TaskEditorSavedEventArgs(TaskDto task, PostCommitEventStatus postCommitEventStatus)
    {
        Task = task ?? throw new ArgumentNullException(nameof(task));
        PostCommitEventStatus = postCommitEventStatus;
    }

    /// <summary>Gets the task returned by the successful Application operation.</summary>
    public TaskDto Task { get; }

    /// <summary>Gets the post-commit event delivery outcome.</summary>
    public PostCommitEventStatus PostCommitEventStatus { get; }

    /// <summary>Gets whether consumers should perform a lightweight local refresh.</summary>
    public bool RequiresRefresh => PostCommitEventStatus == PostCommitEventStatus.RefreshRequired;
}
