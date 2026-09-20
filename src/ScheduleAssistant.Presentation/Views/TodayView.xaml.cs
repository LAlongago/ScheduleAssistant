using System.Windows;
using System.Windows.Controls;
using ScheduleAssistant.Presentation.ViewModels;

namespace ScheduleAssistant.Presentation.Views;

/// <summary>Today page view for the DEV-040 shell.</summary>
public partial class TodayView : UserControl
{
    private bool _loadRequested;

    /// <summary>Initializes the Today page view.</summary>
    public TodayView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loadRequested || DataContext is not TodayPageViewModel viewModel)
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
