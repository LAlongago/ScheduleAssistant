using System.Windows;
using System.Windows.Controls;
using ScheduleAssistant.Presentation.ViewModels;

namespace ScheduleAssistant.Presentation.Views;

/// <summary>Six-row month calendar view with in-page date details.</summary>
public partial class MonthView : UserControl
{
    private bool _loadRequested;

    /// <summary>Initializes the month page view.</summary>
    public MonthView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loadRequested || DataContext is not MonthPageViewModel viewModel)
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
