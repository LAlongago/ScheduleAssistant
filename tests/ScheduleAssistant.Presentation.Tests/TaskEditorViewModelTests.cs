using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.ViewModels;
using Xunit;

namespace ScheduleAssistant.Presentation.Tests;

public sealed class TaskEditorViewModelTests
{
    private static readonly Guid CategoryId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static readonly IReadOnlyList<CategoryOptionDto> Categories =
    [
        new CategoryOptionDto(CategoryId, "科研", "#4F7CAC", 10),
        new CategoryOptionDto(
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            "课程",
            "#7A6FB1",
            20)
    ];

    [Fact]
    public async Task Initialize_WhenTitleIsBlank_ShouldHideValidationUntilSaveAttempt()
    {
        var fixture = await CreateFixtureAsync();
        fixture.ViewModel.Title = " ";

        Assert.True(fixture.ViewModel.HasErrors);
        Assert.Contains("请输入标题。", fixture.ViewModel.ValidationMessages);
        Assert.False(fixture.ViewModel.ShowValidationErrors);
        Assert.False(fixture.ViewModel.IsFormPromptVisible);
        Assert.True(fixture.ViewModel.SaveCommand.CanExecute(null));

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.ShowValidationErrors);
        Assert.True(fixture.ViewModel.IsFormPromptVisible);
        Assert.Empty(fixture.UseCases.CreatedCommands);
    }

    [Fact]
    public async Task EnableDeadline_WhenTimeIsNotSelected_ShouldRequireDeadlineTime()
    {
        var fixture = await CreateFixtureAsync();
        fixture.ViewModel.Title = "选择截止时间";
        fixture.ViewModel.HasDeadline = true;
        fixture.ViewModel.DeadlineDateValue = new DateTime(2026, 9, 21);

        Assert.Contains("请选择 Deadline 时间。", fixture.ViewModel.ValidationMessages);
        Assert.True(fixture.ViewModel.SaveCommand.CanExecute(null));

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.ShowValidationErrors);
        Assert.Empty(fixture.UseCases.CreatedCommands);
    }

    [Fact]
    public async Task Save_WhenApplicationSucceeds_ShouldCallCreateAndPublishRefreshSignal()
    {
        var fixture = await CreateFixtureAsync(
            prefilledPlannedDate: new DateOnly(2026, 9, 21));
        var savedTask = CreateTaskDto("新任务");
        fixture.UseCases.CreateHandler = command =>
        {
            Assert.Equal("新任务", command.Draft.Title);
            Assert.Equal(CategoryId, command.Draft.CategoryId);
            Assert.Equal(TaskPriorityCode.Important, command.Draft.Priority);
            Assert.Equal(new DateOnly(2026, 9, 21), command.Draft.PlannedDate);
            return ApplicationResult<TaskDto>.Success(
                savedTask,
                [new ApplicationWarning("Deadline.BeforePlannedDate", "请检查 Deadline。")],
                PostCommitEventStatus.RefreshRequired);
        };
        TaskEditorSavedEventArgs? savedEvent = null;
        var closeRequested = 0;
        fixture.ViewModel.Saved += (_, eventArgs) => savedEvent = eventArgs;
        fixture.ViewModel.CloseRequested += (_, _) => closeRequested++;

        fixture.ViewModel.Title = "新任务";
        fixture.ViewModel.SelectedPriority = TaskPriorityCode.Important;
        fixture.ViewModel.Description = "具体事务";

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        var command = Assert.Single(fixture.UseCases.CreatedCommands);
        Assert.Equal("新任务", command.Draft.Title);
        Assert.NotNull(savedEvent);
        Assert.Same(savedTask, savedEvent!.Task);
        Assert.True(savedEvent.RequiresRefresh);
        Assert.Equal("请检查 Deadline。", fixture.ViewModel.WarningMessage);
        Assert.False(fixture.ViewModel.IsDirty);
        Assert.Equal(1, closeRequested);
    }

    [Fact]
    public async Task Save_WhenApplicationFails_ShouldKeepAllInputAndLeaveEditorOpen()
    {
        var fixture = await CreateFixtureAsync();
        fixture.UseCases.CreateHandler = _ => ApplicationResult<TaskDto>.Failure(
            new ApplicationError(
                ApplicationErrorKind.StorageUnavailable,
                "Storage.Unavailable",
                "数据库暂时不可用。"));
        var closeRequested = 0;
        fixture.ViewModel.CloseRequested += (_, _) => closeRequested++;

        fixture.ViewModel.Title = "保留输入";
        fixture.ViewModel.Location = "会议室";
        fixture.ViewModel.Description = "详细内容";
        fixture.ViewModel.Materials = "材料";
        fixture.ViewModel.Notes = "备注";
        fixture.ViewModel.PlannedDateValue = new DateTime(2026, 9, 22);
        fixture.ViewModel.PlannedStartText = "09:30";
        fixture.ViewModel.PlannedEndText = "10:30";

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("保留输入", fixture.ViewModel.Title);
        Assert.Equal("会议室", fixture.ViewModel.Location);
        Assert.Equal("详细内容", fixture.ViewModel.Description);
        Assert.Equal("材料", fixture.ViewModel.Materials);
        Assert.Equal("备注", fixture.ViewModel.Notes);
        Assert.Equal(new DateOnly(2026, 9, 22), fixture.ViewModel.PlannedDate);
        Assert.Equal("09:30", fixture.ViewModel.PlannedStartText);
        Assert.Equal("10:30", fixture.ViewModel.PlannedEndText);
        Assert.Contains("数据库暂时不可用。", fixture.ViewModel.ErrorMessage);
        Assert.True(fixture.ViewModel.IsDirty);
        Assert.Equal(0, closeRequested);
    }

    [Fact]
    public async Task Save_WhenApplicationReportsConflict_ShouldKeepInputAndOfferReload()
    {
        var taskId = Guid.NewGuid();
        var fixture = await CreateFixtureAsync(
            TaskEditorRequest.Edit(taskId),
            taskToLoad: CreateTaskDto("数据库标题", taskId, version: 4));
        fixture.UseCases.UpdateHandler = command =>
        {
            Assert.Equal(taskId, command.TaskId);
            Assert.Equal(4, command.ExpectedVersion);
            return ApplicationResult<TaskDto>.Failure(
                new ApplicationError(
                    ApplicationErrorKind.Conflict,
                    "Persistence.Conflict",
                    "任务已被其他窗口修改，请重新加载。"));
        };

        fixture.ViewModel.Title = "本窗口修改";
        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.HasConflict);
        Assert.True(fixture.ViewModel.CanReload);
        Assert.Contains("重新加载", fixture.ViewModel.ErrorMessage);
        Assert.Equal("本窗口修改", fixture.ViewModel.Title);
        Assert.True(fixture.ViewModel.IsDirty);
    }

    [Fact]
    public async Task Save_WhenDeadlineIsBeforePlannedDate_ShouldRequireExplicitConfirmation()
    {
        var fixture = await CreateFixtureAsync();
        fixture.Interaction.ConfirmDeadlineResult = false;

        fixture.ViewModel.Title = "Deadline 确认";
        fixture.ViewModel.PlannedDateValue = new DateTime(2026, 9, 20);
        fixture.ViewModel.HasDeadline = true;
        fixture.ViewModel.DeadlineDateValue = new DateTime(2026, 9, 19);
        fixture.ViewModel.DeadlineTimeText = "08:30";

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.Interaction.DeadlineConfirmationCalls);
        Assert.Empty(fixture.UseCases.CreatedCommands);
        Assert.Contains("早于计划日期", fixture.ViewModel.ErrorMessage);
        Assert.True(fixture.ViewModel.IsDirty);
    }

    [Fact]
    public async Task Save_WhenDeadlineIsAmbiguous_ShouldSubmitOnlyTheExplicitlyChosenUtcInstant()
    {
        var fixture = await CreateFixtureAsync(
            localTimeZoneId: "Pacific Standard Time");
        fixture.Interaction.AmbiguousResultSelector = candidates => candidates[1];

        fixture.ViewModel.Title = "DST Deadline";
        fixture.ViewModel.HasDeadline = true;
        fixture.ViewModel.DeadlineDateValue = new DateTime(2026, 11, 1);
        fixture.ViewModel.DeadlineTimeText = "01:30";

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        var command = Assert.Single(fixture.UseCases.CreatedCommands);
        Assert.NotNull(command.Draft.Deadline);
        var deadline = command.Draft.Deadline!;
        Assert.Equal(1, fixture.Interaction.AmbiguousDeadlineCalls);
        Assert.Equal(2, fixture.Interaction.LastAmbiguousCandidates!.Count);
        Assert.Equal(
            fixture.Interaction.LastAmbiguousCandidates[1],
            deadline.ConfirmedUtc);
    }

    [Fact]
    public async Task Save_WhenDeadlineIsInvalidInSelectedTimeZone_ShouldRequireModification()
    {
        var fixture = await CreateFixtureAsync(
            localTimeZoneId: "Pacific Standard Time");

        fixture.ViewModel.Title = "无效 DST Deadline";
        fixture.ViewModel.HasDeadline = true;
        fixture.ViewModel.DeadlineDateValue = new DateTime(2026, 3, 8);
        fixture.ViewModel.DeadlineTimeText = "02:30";

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(fixture.UseCases.CreatedCommands);
        Assert.Contains("does not exist", fixture.ViewModel.ErrorMessage);
        Assert.Contains("Choose another time", fixture.ViewModel.ErrorMessage);
        Assert.True(fixture.ViewModel.HasErrors);
    }

    [Fact]
    public async Task Cancel_WhenFormIsDirty_ShouldAskBeforeDiscardingChanges()
    {
        var fixture = await CreateFixtureAsync();
        var closeRequested = 0;
        fixture.ViewModel.CloseRequested += (_, _) => closeRequested++;
        fixture.ViewModel.Title = "尚未保存";

        fixture.Interaction.DiscardChangesResult = false;
        fixture.ViewModel.CancelCommand.Execute(null);

        Assert.Equal(1, fixture.Interaction.DiscardConfirmationCalls);
        Assert.Equal(0, closeRequested);
        Assert.True(fixture.ViewModel.IsDirty);

        fixture.Interaction.DiscardChangesResult = true;
        fixture.ViewModel.CancelCommand.Execute(null);

        Assert.Equal(2, fixture.Interaction.DiscardConfirmationCalls);
        Assert.Equal(1, closeRequested);
        Assert.True(fixture.ViewModel.TryCloseFromWindow());
    }

    private static async Task<EditorFixture> CreateFixtureAsync(
        TaskEditorRequest? request = null,
        DateOnly? prefilledPlannedDate = null,
        TaskDto? taskToLoad = null,
        string localTimeZoneId = "UTC")
    {
        var useCases = new RecordingTaskUseCases
        {
            Categories = Categories,
            TaskToGet = taskToLoad
        };
        var interaction = new RecordingInteractionService();
        var viewModel = new TaskEditorViewModel(
            useCases,
            new FixedTimeProvider(),
            interaction,
            request ?? TaskEditorRequest.Create(prefilledPlannedDate),
            localTimeZoneId: localTimeZoneId);
        await viewModel.InitializeAsync();
        Assert.True(viewModel.IsInitialized);
        return new EditorFixture(viewModel, useCases, interaction);
    }

    private static TaskDto CreateTaskDto(
        string title,
        Guid? taskId = null,
        long version = 1)
    {
        var now = new DateTimeOffset(2026, 9, 19, 1, 30, 0, TimeSpan.Zero);
        return new TaskDto(
            taskId ?? Guid.NewGuid(),
            title,
            CategoryId,
            TaskPriorityCode.Normal,
            WorkflowStatusCode.Pending,
            new DateOnly(2026, 9, 20),
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            null,
            "地点",
            "具体事务",
            "材料",
            "备注",
            null,
            null,
            false,
            now,
            now,
            null,
            version);
    }

    private sealed record EditorFixture(
        TaskEditorViewModel ViewModel,
        RecordingTaskUseCases UseCases,
        RecordingInteractionService Interaction);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private static readonly DateTimeOffset UtcNow =
            new(2026, 9, 19, 1, 30, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class RecordingInteractionService : ITaskEditorInteractionService
    {
        public bool ConfirmDeadlineResult { get; set; } = true;

        public bool DiscardChangesResult { get; set; }

        public int DeadlineConfirmationCalls { get; private set; }

        public int AmbiguousDeadlineCalls { get; private set; }

        public int DiscardConfirmationCalls { get; private set; }

        public IReadOnlyList<DateTimeOffset>? LastAmbiguousCandidates { get; private set; }

        public Func<IReadOnlyList<DateTimeOffset>, DateTimeOffset?>? AmbiguousResultSelector { get; set; }

        public bool ConfirmDeadlineBeforePlannedDate(DateOnly plannedDate, DateOnly deadlineDate)
        {
            DeadlineConfirmationCalls++;
            return ConfirmDeadlineResult;
        }

        public DateTimeOffset? ChooseAmbiguousDeadline(
            DateOnly localDate,
            TimeOnly localTime,
            string timeZoneId,
            IReadOnlyList<DateTimeOffset> candidates)
        {
            AmbiguousDeadlineCalls++;
            LastAmbiguousCandidates = candidates;
            return AmbiguousResultSelector?.Invoke(candidates);
        }

        public bool ConfirmDiscardChanges()
        {
            DiscardConfirmationCalls++;
            return DiscardChangesResult;
        }
    }

    private sealed class RecordingTaskUseCases : ITaskUseCases
    {
        public IReadOnlyList<CategoryOptionDto> Categories { get; init; } = Array.Empty<CategoryOptionDto>();

        public TaskDto? TaskToGet { get; init; }

        public Func<CreateTaskCommand, ApplicationResult<TaskDto>> CreateHandler { get; set; } =
            _ => ApplicationResult<TaskDto>.Success(CreateTaskDto("创建结果"));

        public Func<UpdateTaskCommand, ApplicationResult<TaskDto>> UpdateHandler { get; set; } =
            _ => ApplicationResult<TaskDto>.Success(CreateTaskDto("更新结果"));

        public List<CreateTaskCommand> CreatedCommands { get; } = [];

        public List<UpdateTaskCommand> UpdatedCommands { get; } = [];

        public Task<ApplicationResult<TaskDto>> CreateAsync(
            CreateTaskCommand command,
            CancellationToken cancellationToken = default)
        {
            CreatedCommands.Add(command);
            return Task.FromResult(CreateHandler(command));
        }

        public Task<ApplicationResult<TaskDto>> UpdateAsync(
            UpdateTaskCommand command,
            CancellationToken cancellationToken = default)
        {
            UpdatedCommands.Add(command);
            return Task.FromResult(UpdateHandler(command));
        }

        public Task<ApplicationResult<TaskDto>> CompleteAsync(
            ChangeTaskStateCommand command,
            CancellationToken cancellationToken = default) => NotConfigured<TaskDto>();

        public Task<ApplicationResult<TaskDto>> CancelCompletionAsync(
            ChangeTaskStateCommand command,
            CancellationToken cancellationToken = default) => NotConfigured<TaskDto>();

        public Task<ApplicationResult<TaskDto>> StartProcessingAsync(
            ChangeTaskStateCommand command,
            CancellationToken cancellationToken = default) => NotConfigured<TaskDto>();

        public Task<ApplicationResult<DeletedTaskDto>> DeleteAsync(
            DeleteTaskCommand command,
            CancellationToken cancellationToken = default) => NotConfigured<DeletedTaskDto>();

        public Task<ApplicationResult<TaskDto>> GetAsync(
            GetTaskQuery query,
            CancellationToken cancellationToken = default)
        {
            return TaskToGet is null
                ? NotConfigured<TaskDto>()
                : Task.FromResult(ApplicationResult<TaskDto>.Success(TaskToGet));
        }

        public Task<ApplicationResult<IReadOnlyList<TaskDto>>> GetPlannedByDateAsync(
            GetTasksByPlannedDateQuery query,
            CancellationToken cancellationToken = default) =>
            NotConfigured<IReadOnlyList<TaskDto>>();

        public Task<ApplicationResult<IReadOnlyList<TaskDto>>> GetUpcomingDeadlinesAsync(
            GetUpcomingDeadlinesQuery query,
            CancellationToken cancellationToken = default) =>
            NotConfigured<IReadOnlyList<TaskDto>>();

        public Task<ApplicationResult<IReadOnlyList<CategoryOptionDto>>> GetCategoryOptionsAsync(
            GetCategoryOptionsQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ApplicationResult<IReadOnlyList<CategoryOptionDto>>.Success(Categories));

        public Task<ApplicationResult<CalendarDayDto>> GetCalendarDateAsync(
            GetCalendarDateQuery query,
            CancellationToken cancellationToken = default) => NotConfigured<CalendarDayDto>();

        public Task<ApplicationResult<TodayPendingDto>> GetTodayPendingAsync(
            GetTodayPendingQuery query,
            CancellationToken cancellationToken = default) => NotConfigured<TodayPendingDto>();

        public Task<ApplicationResult<WeekCalendarDto>> GetWeekCalendarAsync(
            GetWeekCalendarQuery query,
            CancellationToken cancellationToken = default) => NotConfigured<WeekCalendarDto>();

        public Task<ApplicationResult<MonthCalendarDto>> GetMonthCalendarAsync(
            GetMonthCalendarQuery query,
            CancellationToken cancellationToken = default) => NotConfigured<MonthCalendarDto>();

        public Task<ApplicationResult<DeadlineQueryResult>> GetDeadlinesAsync(
            GetDeadlinesQuery query,
            CancellationToken cancellationToken = default) => NotConfigured<DeadlineQueryResult>();

        public Task<ApplicationResult<PagedResult<TaskDto>>> SearchAsync(
            SearchTasksQuery query,
            CancellationToken cancellationToken = default) => NotConfigured<PagedResult<TaskDto>>();

        private static Task<ApplicationResult<T>> NotConfigured<T>()
        {
            return Task.FromResult(ApplicationResult<T>.Failure(
                new ApplicationError(
                    ApplicationErrorKind.Unexpected,
                    "Test.NotConfigured",
                    "该测试未配置此用例。")));
        }
    }
}
