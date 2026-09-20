using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Infrastructure.Composition;
using ScheduleAssistant.Presentation;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.Controls;
using ScheduleAssistant.Presentation.ViewModels;
using Xunit;

namespace ScheduleAssistant.Presentation.Tests;

public sealed class CompositionRegistrationTests
{
    [Fact]
    public void AddPresentation_WhenResolvingApplicationPorts_ShouldUseOneTaskUseCasesAndEventBusInstance()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "ScheduleAssistant-DEV042-" + Guid.NewGuid().ToString("N"));
        using var provider = new ServiceCollection()
            .AddInfrastructure(rootDirectory)
            .AddPresentation()
            .BuildServiceProvider();

        var useCases = provider.GetRequiredService<ITaskUseCases>();
        var queries = provider.GetRequiredService<ITaskQueries>();
        var publisher = provider.GetRequiredService<IApplicationEventPublisher>();
        var bus = provider.GetRequiredService<InProcessEventBus>();

        Assert.Same(useCases, queries);
        Assert.Same(bus, publisher);
    }

    [Fact]
    public void AddPresentation_WhenResolvingMainWindow_ShouldResolveCompleteConstructorGraph()
    {
        Exception? failure = null;
        var resolved = false;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new System.Windows.Application();
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/ScheduleAssistant;component/Resources/Theme.Light.xaml",
                        UriKind.Absolute)
                });
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/ScheduleAssistant;component/Resources/Controls.xaml",
                        UriKind.Absolute)
                });
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/ScheduleAssistant;component/Resources/SelectionControls.xaml",
                        UriKind.Absolute)
                });

                var rootDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "ScheduleAssistant-INTEGRATION008-" + Guid.NewGuid().ToString("N"));
                using var provider = new ServiceCollection()
                    .AddInfrastructure(rootDirectory)
                    .AddPresentation()
                    .BuildServiceProvider();

                var window = provider.GetRequiredService<MainWindow>();

                Assert.NotNull(window.DataContext);
                Assert.IsType<MainWindowViewModel>(window.DataContext);
                Assert.Same(provider.GetRequiredService<ITaskQueries>(), provider.GetRequiredService<ITaskUseCases>());
                Assert.Same(
                    provider.GetRequiredService<InProcessEventBus>(),
                    provider.GetRequiredService<IApplicationEventPublisher>());
                var editorViewModel = new TaskEditorViewModel(
                    provider.GetRequiredService<ITaskUseCases>(),
                    provider.GetRequiredService<TimeProvider>(),
                    new ConfirmingInteractionService(),
                    TaskEditorRequest.Create());
                var editorWindow = provider
                    .GetRequiredService<ITaskEditorWindowFactory>()
                    .Create(editorViewModel);
                editorWindow.ShowActivated = false;
                editorWindow.Opacity = 0;
                editorWindow.Left = -10_000;
                editorWindow.Top = -10_000;
                editorWindow.Show();
                var categoryComboBox = Assert.IsType<ComboBox>(editorWindow.FindName("CategoryComboBox"));
                var categorySelectionPresenter = Assert.IsType<ContentPresenter>(
                    categoryComboBox.Template.FindName("SelectionContentPresenter", categoryComboBox));
                Assert.Same(categoryComboBox.ItemTemplateSelector, categorySelectionPresenter.ContentTemplateSelector);
                Assert.NotNull(categorySelectionPresenter.ContentTemplateSelector);
                var priorityComboBox = Assert.IsType<ComboBox>(editorWindow.FindName("PriorityComboBox"));
                var prioritySelectionPresenter = Assert.IsType<ContentPresenter>(
                    priorityComboBox.Template.FindName("SelectionContentPresenter", priorityComboBox));
                Assert.Same(priorityComboBox.ItemTemplateSelector, prioritySelectionPresenter.ContentTemplateSelector);
                Assert.NotNull(prioritySelectionPresenter.ContentTemplateSelector);
                var plannedStartPicker = Assert.IsType<DigitalTimePicker>(
                    editorWindow.FindName("PlannedStartPicker"));
                Assert.Same(editorViewModel, plannedStartPicker.DataContext);
                var timeBinding = Assert.IsType<System.Windows.Data.BindingExpression>(
                    plannedStartPicker.GetBindingExpression(DigitalTimePicker.TimeTextProperty));
                Assert.Equal(System.Windows.Data.BindingStatus.Active, timeBinding.Status);
                Assert.IsType<DigitalTimePicker>(editorWindow.FindName("PlannedEndPicker"));
                Assert.IsType<DigitalTimePicker>(editorWindow.FindName("DeadlineTimePicker"));
                var plannedDatePicker = Assert.IsType<DatePicker>(editorWindow.FindName("PlannedDatePicker"));
                Assert.IsType<DatePickerTextBox>(
                    plannedDatePicker.Template.FindName("PART_TextBox", plannedDatePicker));
                var calendarButton = Assert.IsType<Button>(
                    plannedDatePicker.Template.FindName("PART_Button", plannedDatePicker));
                calendarButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(plannedDatePicker.IsDropDownOpen);
                plannedDatePicker.IsDropDownOpen = false;
                var deadlineSection = Assert.IsType<Expander>(editorWindow.FindName("DeadlineSection"));
                var contentSection = Assert.IsType<Expander>(editorWindow.FindName("ContentSection"));
                Assert.False(deadlineSection.IsExpanded);
                Assert.False(contentSection.IsExpanded);
                var deadlineHeaderToggle = Assert.IsType<ToggleButton>(
                    deadlineSection.Template.FindName("HeaderToggle", deadlineSection));
                deadlineHeaderToggle.IsChecked = true;
                Assert.True(deadlineSection.IsExpanded);
                var contentHeaderToggle = Assert.IsType<ToggleButton>(
                    contentSection.Template.FindName("HeaderToggle", contentSection));
                contentHeaderToggle.IsChecked = true;
                Assert.True(contentSection.IsExpanded);

                var hourTens = Assert.IsType<ComboBox>(plannedStartPicker.FindName("HourTensSelector"));
                var hourOnes = Assert.IsType<ComboBox>(plannedStartPicker.FindName("HourOnesSelector"));
                var minuteTens = Assert.IsType<ComboBox>(plannedStartPicker.FindName("MinuteTensSelector"));
                var minuteOnes = Assert.IsType<ComboBox>(plannedStartPicker.FindName("MinuteOnesSelector"));
                hourTens.SelectedItem = "2";
                hourOnes.SelectedItem = "3";
                minuteTens.SelectedItem = "5";
                minuteOnes.SelectedItem = "9";
                Assert.Equal("23:59", plannedStartPicker.TimeText);
                Assert.Equal("23:59", editorViewModel.PlannedStartText);
                plannedDatePicker.SelectedDate = new DateTime(2026, 9, 21);
                Assert.Equal(new DateTime(2026, 9, 21), editorViewModel.PlannedDateValue);
                editorWindow.Close();
                window.Close();
                resolved = true;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        Assert.True(resolved);
    }

    private sealed class ConfirmingInteractionService : ITaskEditorInteractionService
    {
        public bool ConfirmDeadlineBeforePlannedDate(DateOnly plannedDate, DateOnly deadlineDate) => true;

        public DateTimeOffset? ChooseAmbiguousDeadline(
            DateOnly localDate,
            TimeOnly localTime,
            string timeZoneId,
            IReadOnlyList<DateTimeOffset> candidates)
        {
            return candidates.Count == 0 ? null : candidates[0];
        }

        public bool ConfirmDiscardChanges() => true;
    }
}
