using ScheduleAssistant.Application.Attachments;
using ScheduleAssistant.Application.Recurrence;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Presentation.ViewModels;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>
/// Constructs and displays ordinary-task editors for all current and future entry points.
/// </summary>
public sealed class TaskEditorService : ITaskEditorService
{
    private readonly ITaskUseCases _taskUseCases;
    private readonly TimeProvider _timeProvider;
    private readonly TaskDeadlineResolver _deadlineResolver;
    private readonly ITaskEditorInteractionService _interactionService;
    private readonly ITaskEditorWindowFactory _windowFactory;
    private readonly IAttachmentUseCases? _attachmentUseCases;
    private readonly IRecurrenceUseCases? _recurrenceUseCases;

    /// <summary>Initializes the reusable task-editor service.</summary>
    public TaskEditorService(
        ITaskUseCases taskUseCases,
        TimeProvider timeProvider,
        TaskDeadlineResolver deadlineResolver,
        ITaskEditorInteractionService interactionService,
        ITaskEditorWindowFactory windowFactory,
        IAttachmentUseCases? attachmentUseCases = null,
        IRecurrenceUseCases? recurrenceUseCases = null)
    {
        _taskUseCases = taskUseCases ?? throw new ArgumentNullException(nameof(taskUseCases));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _deadlineResolver = deadlineResolver ?? throw new ArgumentNullException(nameof(deadlineResolver));
        _interactionService = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        _attachmentUseCases = attachmentUseCases;
        _recurrenceUseCases = recurrenceUseCases;
    }

    /// <inheritdoc />
    public event EventHandler<TaskEditorSavedEventArgs>? TaskSaved;

    /// <inheritdoc />
    public async Task OpenCreateAsync(
        DateOnly? prefilledPlannedDate = null,
        CancellationToken cancellationToken = default)
    {
        var viewModel = CreateViewModel(
            TaskEditorRequest.Create(prefilledPlannedDate));
        await OpenAsync(viewModel, cancellationToken).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public async Task OpenEditAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var viewModel = CreateViewModel(TaskEditorRequest.Edit(taskId));
        await OpenAsync(viewModel, cancellationToken).ConfigureAwait(true);
    }

    private TaskEditorViewModel CreateViewModel(TaskEditorRequest request)
    {
        return new TaskEditorViewModel(
            _taskUseCases,
            _timeProvider,
            _interactionService,
            request,
            _deadlineResolver,
            attachmentUseCases: _attachmentUseCases,
            recurrenceUseCases: _recurrenceUseCases);
    }

    private async Task OpenAsync(
        TaskEditorViewModel viewModel,
        CancellationToken cancellationToken)
    {
        viewModel.Saved += OnViewModelSaved;
        try
        {
            await viewModel.InitializeAsync(cancellationToken).ConfigureAwait(true);
            var window = _windowFactory.Create(viewModel);
            window.ShowDialog();
        }
        finally
        {
            viewModel.Saved -= OnViewModelSaved;
        }
    }

    private void OnViewModelSaved(object? sender, TaskEditorSavedEventArgs eventArgs)
    {
        TaskSaved?.Invoke(this, eventArgs);
    }
}
