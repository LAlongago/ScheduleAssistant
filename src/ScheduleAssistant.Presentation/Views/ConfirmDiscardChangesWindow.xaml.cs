using System.Windows;

namespace ScheduleAssistant.Presentation.Views;

/// <summary>Consistent themed confirmation window for discarding dirty editor changes.</summary>
public partial class ConfirmDiscardChangesWindow : Window
{
    /// <summary>Initializes the discard confirmation window.</summary>
    public ConfirmDiscardChangesWindow()
    {
        InitializeComponent();
    }

    private void OnDiscardClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
