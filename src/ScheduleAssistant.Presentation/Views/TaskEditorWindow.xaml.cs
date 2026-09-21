using System.ComponentModel;
using System.Windows;
using ScheduleAssistant.Presentation.ViewModels;

namespace ScheduleAssistant.Presentation.Views;

/// <summary>WPF host for the ordinary task editor ViewModel.</summary>
public partial class TaskEditorWindow : Window
{
    private readonly TaskEditorViewModel _viewModel;

    /// <summary>Initializes a task editor window.</summary>
    public TaskEditorWindow(TaskEditorViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        _viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = !_viewModel.TryCloseFromWindow();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        base.OnClosed(e);
    }
}
