using System.Windows;
using System.Windows.Controls;
using ScheduleAssistant.Presentation.ViewModels;

namespace ScheduleAssistant.Presentation.Views;

/// <summary>Seven-column week calendar view.</summary>
public partial class WeekView : UserControl
{
    private bool _loadRequested;

    /// <summary>Initializes the week calendar view.</summary>
    public WeekView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loadRequested || DataContext is not WeekPageViewModel viewModel)
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
