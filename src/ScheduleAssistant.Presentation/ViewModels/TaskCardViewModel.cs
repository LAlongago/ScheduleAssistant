using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Design-time category accents available to the reusable task card.</summary>
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
/// Presentation-only task card data. It deliberately does not mirror a Domain entity or execute commands.
/// </summary>
public sealed class TaskCardViewModel : ObservableObject
{
    private bool _isCompleted;
    private readonly bool _isCompletionEnabled;
    private readonly ICommand? _completionCommand;

    /// <summary>
    /// Initializes a task card with temporary shell demonstration data.
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
        StatusText = statusText;
        CategoryAccent = categoryAccent;
        Mode = mode;
        _isCompleted = isCompleted;
        _isCompletionEnabled = false;
        _completionCommand = null;
    }

    /// <summary>Gets the temporary display title.</summary>
    public string Title { get; }

    /// <summary>Gets the display category label.</summary>
    public string CategoryText { get; }

    /// <summary>Gets the planned date/time display.</summary>
    public string PlannedTimeText { get; }

    /// <summary>Gets the deadline display, including its semantic label.</summary>
    public string DeadlineText { get; }

    /// <summary>Gets the priority display text.</summary>
    public string PriorityText { get; }

    /// <summary>Gets the optional location display text.</summary>
    public string LocationText { get; }

    /// <summary>Gets the workflow/status display text.</summary>
    public string StatusText { get; }

    /// <summary>Gets the accent used for the category stripe.</summary>
    public CategoryAccent CategoryAccent { get; }

    /// <summary>Gets the requested card density.</summary>
    public TaskCardMode Mode { get; }

    /// <summary>Gets whether the temporary completion state is checked.</summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        private set => SetProperty(ref _isCompleted, value);
    }

    /// <summary>
    /// Completion is intentionally disabled until DEV-042 connects real Application commands.
    /// </summary>
    public bool IsCompletionEnabled => _isCompletionEnabled;

    /// <summary>Gets the future command binding point for task completion.</summary>
    public ICommand? CompletionCommand => _completionCommand;
}
