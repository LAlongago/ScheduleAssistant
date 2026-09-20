using System.Windows;
using System.Windows.Controls;
using ScheduleAssistant.Presentation.ViewModels;

namespace ScheduleAssistant.Presentation.Views;

/// <summary>Upcoming-deadlines view for the DEV-042 shell.</summary>
public partial class UpcomingDeadlinesView : UserControl
{
    private bool _loadRequested;

    /// <summary>Initializes the upcoming-deadlines page view.</summary>
    public UpcomingDeadlinesView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loadRequested || DataContext is not UpcomingDeadlinesPageViewModel viewModel)
        {
            return;
        }

        _loadRequested = true;
        try
        {
            await viewModel.LoadAsync();
        }
        catch
        {
            // The ViewModel owns safe error-state conversion; the UI event must not escape.
        }
    }
}
