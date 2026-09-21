using System.Windows;
using ScheduleAssistant.Presentation.ViewModels;

namespace ScheduleAssistant.Presentation;

/// <summary>
/// Main window for the integrated ScheduleAssistant application shell.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes the main window through constructor-injected shell state.
    /// </summary>
    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
    }
}
