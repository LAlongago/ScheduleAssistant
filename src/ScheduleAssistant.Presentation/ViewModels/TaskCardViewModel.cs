using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Domain;
using ScheduleAssistant.Presentation.Composition;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Legacy category accents retained for future compact-card rendering.</summary>
public enum CategoryAccent
{
    Research,
    Course,
    Meeting,
    Administration,
    Life,
    Other
}

/// <summary>Controls the density of a task card without changing its data contract.</summary>
public enum TaskCardMode
{
    Standard,
    Compact
}

/// <summary>
/// Displays one Application task projection and owns the optimistic completion interaction.
/// </summary>
public sealed class TaskCardViewModel : ObservableObject
{
    private readonly ITaskUseCases? _taskUseCases;
    private readonly IUiDispatcher? _dispatcher;
    private readonly AsyncRelayCommand<object?>? _completionCommand;
    private TaskDto? _task;
    private bool _isCompleted;
    private bool _isCompletionInProgress;
    private string _statusText = string.Empty;
    private string _errorText = string.Empty;

    /// <summary>
    /// Initializes a legacy non-persistent card. It remains completion-disabled and has no
    /// persistence identity, so it cannot be mistaken for a real task.
    /// </summary>
    public TaskCardViewModel(
        string title,
        string categoryText,
        string plannedTimeText,
        string deadlineText,
        string priorityText,
        string locationText,
        string statusText,
        CategoryAccent categoryAccent,
        TaskCardMode mode = TaskCardMode.Standard,
        bool isCompleted = false)
    {
        Title = title;
        CategoryText = categoryText;
        PlannedTimeText = plannedTimeText;
        DeadlineText = deadlineText;
        PriorityText = priorityText;
        LocationText = locationText;
        _statusText = statusText;
        CategoryAccent = categoryAccent;
        Mode = mode;
        _isCompleted = isCompleted;
    }

    /// <summary>Initializes a real task card from mapped Application display data.</summary>
    public TaskCardViewModel(
        TaskCardPresentation presentation,
        ITaskUseCases taskUseCases,
        IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _taskUseCases = taskUseCases ?? throw new ArgumentNullException(nameof(taskUseCases));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _completionCommand = new AsyncRelayCommand<object?>(CompleteAsync, _ => IsCompletionEnabled);

        ApplyPresentation(presentation);
    }

    /// <summary>Gets the current Application task returned by the latest query or command.</summary>
    public TaskDto? Task => _task;

    /// <summary>Gets the stable task identity used by completion commands.</summary>
    public Guid TaskId { get; private set; }

    /// <summary>Gets the optimistic version sent with the latest completion command.</summary>
    public long Version { get; private set; }

    /// <summary>Gets the display title.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets the display category label.</summary>
    public string CategoryText { get; private set; } = string.Empty;

    /// <summary>Gets the persisted category color in hexadecimal form.</summary>
    public string? CategoryColorHex { get; private set; }

    /// <summary>Gets the planned date/time display.</summary>
    public string PlannedTimeText { get; private set; } = string.Empty;

    /// <summary>Gets the Deadline display, including its date marker.</summary>
    public string DeadlineText { get; private set; } = string.Empty;

    /// <summary>Gets the explicit plan marker for the card's display date.</summary>
    public string PlanMarkerText { get; private set; } = string.Empty;

    /// <summary>Gets the explicit Deadline marker for the card's display date.</summary>
    public string DeadlineMarkerText { get; private set; } = string.Empty;

    /// <summary>Gets the priority display text.</summary>
    public string PriorityText { get; private set; } = string.Empty;

    /// <summary>Gets the optional location display text.</summary>
    public string LocationText { get; private set; } = string.Empty;

    /// <summary>Gets the derived display status text supplied by Application data.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Gets the cached Deadline urgency text supplied by Application data.</summary>
    public string UrgencyText { get; private set; } = string.Empty;

    /// <summary>Gets the derived status value from the current calendar entry.</summary>
    public DisplayStatus DisplayStatus { get; private set; }

    /// <summary>Gets the derived Deadline urgency value from the current calendar entry.</summary>
    public DeadlineUrgencyLevel DeadlineUrgency { get; private set; }

    /// <summary>Gets the accent reserved for future compact-card rendering.</summary>
    public CategoryAccent CategoryAccent { get; private set; }

    /// <summary>Gets the requested card density.</summary>
    public TaskCardMode Mode { get; private set; }

    /// <summary>Gets whether the current task is completed.</summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        private set => SetProperty(ref _isCompleted, value);
    }

    /// <summary>Gets whether a completion request is currently in flight.</summary>
    public bool IsCompletionInProgress
    {
        get => _isCompletionInProgress;
        private set => SetProperty(ref _isCompletionInProgress, value);
    }

    /// <summary>Gets whether the card can send a completion request.</summary>
    public bool IsCompletionEnabled => _completionCommand is not null && !IsCompletionInProgress;

    /// <summary>Gets the safe user-facing error from the latest completion attempt.</summary>
    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    /// <summary>Gets the asynchronous completion command, when this is a real task card.</summary>
    public ICommand? CompletionCommand => _completionCommand;

    private async Task CompleteAsync(object? parameter)
    {
        if (_taskUseCases is null || _dispatcher is null || _task is null)
        {
            return;
        }

        var requestedCompleted = parameter is bool value ? value : !IsCompleted;
        if (requestedCompleted == IsCompleted)
        {
            return;
        }

        var previousCompleted = IsCompleted;
        IsCompletionInProgress = true;
        ErrorText = string.Empty;
        _completionCommand?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsCompletionEnabled));

        ApplicationResult<TaskDto>? result = null;
        string? failureMessage = null;
        try
        {
            var command = new ChangeTaskStateCommand(TaskId, Version);
            result = requestedCompleted
                ? await _taskUseCases.CompleteAsync(command).ConfigureAwait(false)
                : await _taskUseCases.CancelCompletionAsync(command).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            failureMessage = "操作已取消，请重试。";
        }
        catch
        {
            failureMessage = "保存完成状态失败，请重试。";
        }

        await _dispatcher.InvokeAsync(() =>
        {
            if (result is { IsSuccess: true, Value: not null })
            {
                _task = result.Value;
                TaskId = result.Value.Id;
                Version = result.Value.Version;
                IsCompleted = result.Value.WorkflowStatus == WorkflowStatus.Completed;
                DisplayStatus = IsCompleted ? DisplayStatus.Completed : DisplayStatus;
                if (IsCompleted)
                {
                    DeadlineUrgency = DeadlineUrgencyLevel.None;
                    UrgencyText = string.Empty;
                    OnPropertyChanged(nameof(DisplayStatus));
                    OnPropertyChanged(nameof(DeadlineUrgency));
                    OnPropertyChanged(nameof(UrgencyText));
                }
                StatusText = IsCompleted ? "已完成" : "已更新";
                OnPropertyChanged(nameof(Task));
                OnPropertyChanged(nameof(TaskId));
                OnPropertyChanged(nameof(Version));
                ErrorText = string.Empty;
            }
            else
            {
                IsCompleted = previousCompleted;
                OnPropertyChanged(nameof(IsCompleted));
                ErrorText = failureMessage
                    ?? result?.Error?.Message
                    ?? "保存完成状态失败，请重试。";
            }

            IsCompletionInProgress = false;
            _completionCommand?.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(IsCompletionEnabled));
        }).ConfigureAwait(false);
    }

    private void ApplyPresentation(TaskCardPresentation presentation)
    {
        _task = presentation.Task;
        TaskId = presentation.Task.Id;
        Version = presentation.Task.Version;
        Title = presentation.Task.Title;
        CategoryText = presentation.CategoryText;
        CategoryColorHex = presentation.CategoryColorHex;
        PlannedTimeText = presentation.PlannedTimeText;
        DeadlineText = presentation.DeadlineText;
        PlanMarkerText = presentation.PlanMarkerText;
        DeadlineMarkerText = presentation.DeadlineMarkerText;
        PriorityText = presentation.PriorityText;
        LocationText = presentation.LocationText;
        _statusText = presentation.StatusText;
        UrgencyText = presentation.UrgencyText;
        DisplayStatus = presentation.DisplayStatus;
        DeadlineUrgency = presentation.DeadlineUrgency;
        Mode = presentation.Mode;
        _isCompleted = presentation.Task.WorkflowStatus == WorkflowStatus.Completed;
    }
}
