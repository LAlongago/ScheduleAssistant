using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.Controls;
using ScheduleAssistant.Presentation.ViewModels;
using Xunit;

namespace ScheduleAssistant.Presentation.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public void Navigate_WhenSelectingSettings_ShouldUpdateCurrentTitleAndSelection()
    {
        var navigation = CreateNavigation();
        var viewModel = new MainWindowViewModel(navigation, new FixedTimeProvider());

        viewModel.NavigateCommand.Execute(NavigationPage.Settings);

        Assert.Equal("设置", viewModel.CurrentTitle);
        Assert.Equal("主题、关闭行为和提醒偏好的集中入口", viewModel.CurrentSubtitle);
        var selected = Assert.Single(viewModel.NavigationItems, item => item.IsSelected);
        Assert.Equal(NavigationPage.Settings, selected.Page);
    }

    [Fact]
    public void SetContentState_WhenPageStateChanges_ShouldExposeTextAndState()
    {
        var page = new TodayPageViewModel();

        page.SetContentState(
            PageContentState.Error,
            "加载失败",
            "这是可操作的占位错误文本。");

        Assert.Equal(PageContentState.Error, page.ContentState);
        Assert.Equal("加载失败", page.StatusTitle);
        Assert.Equal("这是可操作的占位错误文本。", page.StatusMessage);
    }

    [Fact]
    public void DateSearchAndCreateCommands_WhenShellIsUnconnected_ShouldBeDisabled()
    {
        var viewModel = new MainWindowViewModel(CreateNavigation(), new FixedTimeProvider());

        Assert.False(viewModel.IsDateNavigationEnabled);
        Assert.False(viewModel.IsSearchEnabled);
        Assert.False(viewModel.IsCreateTaskEnabled);
        Assert.False(viewModel.PreviousDateCommand.CanExecute(null));
        Assert.False(viewModel.CurrentDateCommand.CanExecute(null));
        Assert.False(viewModel.NextDateCommand.CanExecute(null));
        Assert.False(viewModel.CreateTaskCommand.CanExecute(null));
    }

    [Fact]
    public async Task CreateCommand_WhenEditorIsConnected_ShouldOpenCreateWithDisplayDate()
    {
        var editor = new RecordingTaskEditorService();
        var viewModel = new MainWindowViewModel(CreateNavigation(), new FixedTimeProvider(), editor);

        Assert.True(viewModel.IsCreateTaskEnabled);
        Assert.True(viewModel.CreateTaskCommand.CanExecute(null));

        await viewModel.CreateTaskCommand.ExecuteAsync(null);

        Assert.Equal(viewModel.DisplayDate, editor.PrefilledPlannedDate);
    }

    private static NavigationService CreateNavigation()
    {
        return new NavigationService(
            new TodayPageViewModel(),
            new WeekPageViewModel(),
            new MonthPageViewModel(),
            new UpcomingDeadlinesPageViewModel(),
            new AllTasksPageViewModel(),
            new SettingsPageViewModel());
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private static readonly DateTimeOffset LocalNow = new(2026, 9, 19, 9, 30, 0, TimeSpan.FromHours(8));

        public override DateTimeOffset GetUtcNow() => LocalNow.ToUniversalTime();
    }

    private sealed class RecordingTaskEditorService : ITaskEditorService
    {
        public DateOnly? PrefilledPlannedDate { get; private set; }

        public event EventHandler<TaskEditorSavedEventArgs>? TaskSaved
        {
            add { }
            remove { }
        }

        public Task OpenCreateAsync(
            DateOnly? prefilledPlannedDate = null,
            CancellationToken cancellationToken = default)
        {
            PrefilledPlannedDate = prefilledPlannedDate;
            return Task.CompletedTask;
        }

        public Task OpenEditAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> TryOpenEditAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }
    }
}
