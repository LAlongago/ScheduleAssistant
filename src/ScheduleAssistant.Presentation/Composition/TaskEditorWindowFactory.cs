using ScheduleAssistant.Presentation.ViewModels;
using ScheduleAssistant.Presentation.Views;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Creates a task-editor window without making the editor service a service locator.</summary>
public interface ITaskEditorWindowFactory
{
    /// <summary>Creates a window bound to the supplied editor state.</summary>
    TaskEditorWindow Create(TaskEditorViewModel viewModel);
}

/// <summary>Default WPF task-editor window factory.</summary>
public sealed class TaskEditorWindowFactory : ITaskEditorWindowFactory
{
    /// <inheritdoc />
    public TaskEditorWindow Create(TaskEditorViewModel viewModel)
    {
        return new TaskEditorWindow(viewModel);
    }
}
