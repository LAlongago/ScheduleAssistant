using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.Sqlite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application.Reminders;
using ScheduleAssistant.Domain;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Infrastructure.Composition;
using ScheduleAssistant.Infrastructure.Notifications;
using ScheduleAssistant.Presentation;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.Controls;
using ScheduleAssistant.Presentation.ViewModels;
using Xunit;

namespace ScheduleAssistant.Presentation.Tests;

public sealed class CompositionRegistrationTests
{
    [Fact]
    public void AddPresentation_WhenHostedServicesResolve_ShouldInitializeThenMaintainThenStartReminderScheduler()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "ScheduleAssistant-DEV080-startup-order-" + Guid.NewGuid().ToString("N"));
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(rootDirectory)
            .AddPresentation()
            .BuildServiceProvider();

        var hostedServiceTypes = provider.GetServices<IHostedService>()
            .Select(service => service.GetType())
            .ToArray();

        Assert.Equal(
            new[]
            {
                typeof(DatabaseInitializationHostedService),
                typeof(AttachmentMaintenanceHostedService),
                typeof(NotificationActivationHostedService),
                typeof(WindowsNotificationService),
                typeof(ReminderSchedulerHostedService)
            },
            hostedServiceTypes);
    }

    [Fact]
    public async Task AddPresentation_WhenNotificationAdapterHasNotStarted_ShouldReportNotRegistered()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "ScheduleAssistant-DEV080-notifications-" + Guid.NewGuid().ToString("N"));
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(rootDirectory)
            .AddPresentation()
            .BuildServiceProvider();

        var capability = await provider.GetRequiredService<INotificationService>().GetCapabilityAsync();

        Assert.False(capability.IsAvailable);
        Assert.Equal("notification.not-registered", capability.ErrorCode);
    }

    [Fact]
    public async Task StartAsync_WhenCompositionRootHasNoNotificationAdapter_ShouldPreserveDueReminderAndNotCreateTimer()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "ScheduleAssistant-DEV080-pending-reminder-" + Guid.NewGuid().ToString("N"));
        var timerFactory = new CountingOneShotTimerFactory();
        try
        {
            using var provider = new ServiceCollection()
                .AddLogging()
                .AddInfrastructure(rootDirectory)
                .AddPresentation()
                .AddSingleton<IOneShotTimerFactory>(timerFactory)
                .BuildServiceProvider();

            await provider.GetRequiredService<IDatabaseInitialization>().EnsureInitializedAsync();

            var nowUtc = DateTimeOffset.UtcNow;
            var deadlineUtc = nowUtc.AddDays(3);
            var deadlineLocal = deadlineUtc.UtcDateTime;
            var deadline = ZonedDeadline.CreateResolvedUtc(
                DateOnly.FromDateTime(deadlineLocal),
                TimeOnly.FromDateTime(deadlineLocal),
                "UTC",
                deadlineUtc);
            var categories = await provider.GetRequiredService<ICategoryRepository>().GetAllAsync();
            Assert.NotEmpty(categories);
            var category = categories[0];
            var task = TaskItem.Create(
                Guid.NewGuid(),
                "Startup reminder regression",
                category.Id,
                TaskPriority.Normal,
                nowUtc,
                deadline: deadline);
            await provider.GetRequiredService<ITaskRepository>().AddAsync(task);

            var reminder = Reminder.Create(
                Guid.NewGuid(),
                task.Id,
                -1440,
                nowUtc.AddDays(-2),
                "integration-011-unavailable-provider");
            var reminderRepository = provider.GetRequiredService<IReminderRepository>();
            await reminderRepository.AddAsync(reminder);

            var schedulerHostedService = provider.GetServices<IHostedService>()
                .OfType<ReminderSchedulerHostedService>()
                .Single();
            await schedulerHostedService.StartAsync(CancellationToken.None);

            var persisted = await reminderRepository.GetByIdAsync(reminder.Id);
            Assert.Equal(ReminderStatus.Pending, persisted!.Status);
            Assert.Equal(0, timerFactory.CreateCount);

            await schedulerHostedService.StopAsync(CancellationToken.None);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    private sealed class CountingOneShotTimerFactory : IOneShotTimerFactory
    {
        public int CreateCount { get; private set; }

        public IOneShotTimer Create(Func<Task> callback)
        {
            CreateCount++;
            return new UnusedOneShotTimer();
        }
    }

    private sealed class UnusedOneShotTimer : IOneShotTimer
    {
        public void Schedule(TimeSpan delay) => throw new InvalidOperationException("No timer should be scheduled.");

        public void CancelScheduledCallback()
        {
        }

        public void Dispose()
        {
        }
    }

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
                var formPrompt = Assert.IsType<Border>(editorWindow.FindName("FormPrompt"));
                var editorScrollViewer = Assert.IsType<ScrollViewer>(editorWindow.FindName("EditorScrollViewer"));
                Assert.Equal(ScrollBarVisibility.Hidden, editorScrollViewer.VerticalScrollBarVisibility);
                editorWindow.ShowActivated = false;
                editorWindow.Opacity = 0;
                editorWindow.Left = -10_000;
                editorWindow.Top = -10_000;
                editorWindow.Show();
                Assert.Equal(Visibility.Collapsed, formPrompt.Visibility);
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
                Assert.Equal(VerticalAlignment.Top, deadlineSection.VerticalAlignment);
                Assert.Equal(VerticalAlignment.Top, contentSection.VerticalAlignment);
                var deadlineHeaderToggle = Assert.IsType<ToggleButton>(
                    deadlineSection.Template.FindName("HeaderToggle", deadlineSection));
                deadlineHeaderToggle.IsChecked = true;
                Assert.True(deadlineSection.IsExpanded);
                Assert.False(contentSection.IsExpanded);
                Assert.Equal(VerticalAlignment.Stretch, deadlineSection.VerticalAlignment);
                Assert.Equal(VerticalAlignment.Top, contentSection.VerticalAlignment);
                Assert.Equal(370, deadlineSection.MinHeight);
                Assert.Equal(0, contentSection.MinHeight);
                var contentHeaderToggle = Assert.IsType<ToggleButton>(
                    contentSection.Template.FindName("HeaderToggle", contentSection));
                contentHeaderToggle.IsChecked = true;
                Assert.True(contentSection.IsExpanded);
                Assert.True(deadlineSection.IsExpanded);
                Assert.Equal(370, contentSection.MinHeight);
                editorWindow.UpdateLayout();
                Assert.Equal(VerticalAlignment.Stretch, deadlineSection.VerticalAlignment);
                Assert.Equal(VerticalAlignment.Stretch, contentSection.VerticalAlignment);
                Assert.InRange(Math.Abs(deadlineSection.ActualHeight - contentSection.ActualHeight), 0, 0.1);
                contentHeaderToggle.IsChecked = false;
                Assert.True(deadlineSection.IsExpanded);
                Assert.False(contentSection.IsExpanded);
                Assert.Equal(VerticalAlignment.Stretch, deadlineSection.VerticalAlignment);
                Assert.Equal(VerticalAlignment.Top, contentSection.VerticalAlignment);
                Assert.Equal(370, deadlineSection.MinHeight);
                Assert.Equal(0, contentSection.MinHeight);
                deadlineHeaderToggle.IsChecked = false;

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

        public IReadOnlyList<AttachmentFileSelection> SelectAttachmentFiles() =>
            Array.Empty<AttachmentFileSelection>();

        public string? PromptAttachmentDisplayName(string currentDisplayName) => currentDisplayName;

        public bool ConfirmRemoveAttachment(string displayName) => true;

        public bool ConfirmRecurrenceOperation(string title, string message, string confirmLabel) => true;
    }
}
