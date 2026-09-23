using System.Windows;

namespace ScheduleAssistant.Presentation.Views;

/// <summary>Theme-consistent second confirmation for a scoped recurrence operation.</summary>
public partial class ConfirmRecurrenceActionWindow : Window
{
    /// <summary>Initializes a recurrence scope confirmation.</summary>
    public ConfirmRecurrenceActionWindow(string title, string message, string confirmLabel)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs eventArgs)
    {
        DialogResult = true;
    }
}
