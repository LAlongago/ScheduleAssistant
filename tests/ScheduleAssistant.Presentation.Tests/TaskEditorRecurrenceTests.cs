using ScheduleAssistant.Application.Attachments;
using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Recurrence;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.ViewModels;
using Xunit;

namespace ScheduleAssistant.Presentation.Tests;

public sealed class TaskEditorRecurrenceTests
{
    private static readonly Guid CategoryId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SeriesId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid TaskId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly DateOnly EffectiveDate = new(2026, 9, 20);

    [Theory]
    [InlineData(RecurrenceFrequencyCode.Daily)]
    [InlineData(RecurrenceFrequencyCode.Weekly)]
    [InlineData(RecurrenceFrequencyCode.Monthly)]
    [InlineData(RecurrenceFrequencyCode.Yearly)]
    public async Task Save_WhenCreatingRecurrence_ShouldMapRuleAndAvoidOrdinaryTaskCopy(
        RecurrenceFrequencyCode frequency)
    {
        var fixture = await CreateFixtureAsync();
        PrepareRecurringTask(fixture.ViewModel, frequency);

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        var command = Assert.Single(fixture.Recurrence.CreateCommands);
        Assert.Empty(fixture.Tasks.CreatedCommands);
        Assert.Equal("每周任务", command.Draft.Title);
        Assert.Equal(1, command.Draft.Rule.Interval);
        Assert.Equal(frequency, command.Draft.Rule.Frequency);
        Assert.Equal(EffectiveDate, command.Draft.Rule.EffectiveDate);
        Assert.Equal("UTC", command.Draft.Rule.TimeZoneId);
        Assert.Equal(frequency == RecurrenceFrequencyCode.Weekly
            ? RecurrenceWeekdayCode.Monday | RecurrenceWeekdayCode.Friday
            : RecurrenceWeekdayCode.None, command.Draft.Rule.Weekdays);
        Assert.Equal(frequency == RecurrenceFrequencyCode.Monthly ? 31 : null, command.Draft.Rule.MonthDay);
        Assert.Equal(frequency == RecurrenceFrequencyCode.Yearly ? 2 : null, command.Draft.Rule.YearMonth);
        Assert.Equal(frequency == RecurrenceFrequencyCode.Yearly ? 29 : null, command.Draft.Rule.YearDay);
        Assert.False(fixture.ViewModel.IsTaskOnlyDataEnabled);
        Assert.False(fixture.ViewModel.CanManageAttachments);
    }

    [Fact]
    public async Task Save_WhenWeeklyRuleHasNoWeekday_ShouldShowValidationAndNotCreateSeries()
    {
        var fixture = await CreateFixtureAsync();
        PrepareRecurringTask(fixture.ViewModel, RecurrenceFrequencyCode.Weekly);
        foreach (var weekday in fixture.ViewModel.RecurrenceWeekdayOptions)
        {
            weekday.IsSelected = false;
        }

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(fixture.Recurrence.CreateCommands);
        Assert.Empty(fixture.Tasks.CreatedCommands);
        Assert.Contains("每周规则至少选择一天。", fixture.ViewModel.ValidationMessages);
        Assert.True(fixture.ViewModel.ShowValidationErrors);
    }

    [Fact]
    public async Task Save_WhenEndDatePrecedesEffectiveDate_ShouldKeepEditorOpen()
    {
        var fixture = await CreateFixtureAsync();
        PrepareRecurringTask(fixture.ViewModel, RecurrenceFrequencyCode.Daily);
        fixture.ViewModel.RecurrenceEndDateValue = EffectiveDate.AddDays(-1).ToDateTime(TimeOnly.MinValue);
        var closeCount = 0;
        fixture.ViewModel.CloseRequested += (_, _) => closeCount++;

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(fixture.Recurrence.CreateCommands);
        Assert.Contains("结束日期不能早于生效日期。", fixture.ViewModel.ValidationMessages);
        Assert.Equal(0, closeCount);
    }

    [Fact]
    public async Task RecurrenceMode_WhenMonthEndRuleIsSelected_ShouldExplainClampPolicy()
    {
        var fixture = await CreateFixtureAsync();
        fixture.ViewModel.Title = "每月末日";
        fixture.ViewModel.IsRecurrenceEnabled = true;
        fixture.ViewModel.SelectedRecurrenceFrequency = RecurrenceFrequencyCode.Monthly;
        fixture.ViewModel.MonthlyDay = 29;

        Assert.Equal("每月 29、30、31 日在较短月份按当月最后一天处理。", fixture.ViewModel.RecurrencePolicyNotice);

        fixture.ViewModel.SelectedRecurrenceFrequency = RecurrenceFrequencyCode.Yearly;
        fixture.ViewModel.YearMonth = 2;
        fixture.ViewModel.YearDay = 29;
        Assert.Equal("每年 2 月 29 日在非闰年按 2 月最后一天处理。", fixture.ViewModel.RecurrencePolicyNotice);
    }

    [Fact]
    public async Task EnableRecurrence_WhenDeadlineWasSelected_ShouldKeepSelectionAndRejectSeriesMode()
    {
        var fixture = await CreateFixtureAsync();
        fixture.ViewModel.Title = "保留截止设置";
        fixture.ViewModel.HasDeadline = true;
        fixture.ViewModel.DeadlineDateValue = new DateTime(2026, 9, 22);
        fixture.ViewModel.DeadlineTimeText = "17:00";

        fixture.ViewModel.IsRecurrenceEnabled = true;

        Assert.False(fixture.ViewModel.IsRecurrenceEnabled);
        Assert.True(fixture.ViewModel.HasDeadline);
        Assert.Equal(new DateTime(2026, 9, 22), fixture.ViewModel.DeadlineDateValue);
        Assert.Contains("不支持 Deadline", fixture.ViewModel.RecurrenceModeError);
    }

    [Fact]
    public async Task EnableRecurrence_WhenAttachmentWasSelected_ShouldKeepAttachmentAndRejectSeriesMode()
    {
        var interaction = new RecordingInteractionService
        {
            SelectedAttachments = [new AttachmentFileSelection("C:\\source\\file.pdf", "file.pdf")]
        };
        var fixture = await CreateFixtureAsync(interaction, new NoOpAttachmentUseCases());
        fixture.ViewModel.Title = "保留附件选择";
        await fixture.ViewModel.AddAttachmentCommand.ExecuteAsync(null);

        fixture.ViewModel.IsRecurrenceEnabled = true;

        Assert.False(fixture.ViewModel.IsRecurrenceEnabled);
        Assert.True(fixture.ViewModel.HasAttachments);
        Assert.Contains("附件", fixture.ViewModel.RecurrenceModeError);
    }

    [Fact]
    public async Task Save_WhenEditingCurrentOccurrence_ShouldUseOrdinaryTaskUpdateFlow()
    {
        var task = CreateTask("当前实例", seriesId: SeriesId, occurrenceDate: EffectiveDate, version: 7);
        var series = CreateSeries(version: 3);
        var recurrence = new RecordingRecurrenceUseCases { SeriesToGet = series };
        var fixture = await CreateFixtureAsync(
            request: TaskEditorRequest.Edit(TaskId),
            taskToLoad: task,
            recurrenceUseCases: recurrence);
        fixture.ViewModel.Title = "当前实例覆盖";

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        var update = Assert.Single(fixture.Tasks.UpdatedCommands);
        Assert.Equal(TaskId, update.TaskId);
        Assert.Equal(7, update.ExpectedVersion);
        Assert.Equal("当前实例覆盖", update.Draft.Title);
        Assert.Empty(recurrence.UpdateCommands);
    }

    [Fact]
    public async Task Save_WhenEditingSeries_ShouldConfirmAndPassExplicitApplyFromDate()
    {
        var task = CreateTask("当前实例", seriesId: SeriesId, occurrenceDate: EffectiveDate);
        var recurrence = new RecordingRecurrenceUseCases { SeriesToGet = CreateSeries(version: 4) };
        var interaction = new RecordingInteractionService { ConfirmRecurrenceResult = false };
        var fixture = await CreateFixtureAsync(
            request: TaskEditorRequest.Edit(TaskId),
            taskToLoad: task,
            recurrenceUseCases: recurrence,
            interaction: interaction);
        await fixture.ViewModel.EditRecurrenceSeriesCommand.ExecuteAsync(null);
        fixture.ViewModel.ApplyFromDateValue = new DateTime(2026, 9, 25);
        fixture.ViewModel.Title = "此后系列标题";

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(recurrence.UpdateCommands);
        Assert.Equal(1, interaction.RecurrenceConfirmationCalls);

        interaction.ConfirmRecurrenceResult = true;
        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        var command = Assert.Single(recurrence.UpdateCommands);
        Assert.Equal(SeriesId, command.SeriesId);
        Assert.Equal(4, command.ExpectedVersion);
        Assert.Equal(new DateOnly(2026, 9, 25), command.ApplyFromDate);
        Assert.Equal(new DateOnly(2026, 9, 25), command.Draft.Rule.EffectiveDate);
        Assert.Equal("此后系列标题", command.Draft.Title);
        Assert.Equal(2, interaction.RecurrenceConfirmationCalls);
        Assert.Empty(fixture.Tasks.UpdatedCommands);
    }

    [Fact]
    public async Task DeleteOccurrence_WhenConfirmed_ShouldCallInstanceExclusionUseCase()
    {
        var task = CreateTask("当前实例", seriesId: SeriesId, occurrenceDate: EffectiveDate, version: 6);
        var recurrence = new RecordingRecurrenceUseCases { SeriesToGet = CreateSeries() };
        var interaction = new RecordingInteractionService { ConfirmRecurrenceResult = false };
        var fixture = await CreateFixtureAsync(
            request: TaskEditorRequest.Edit(TaskId),
            taskToLoad: task,
            recurrenceUseCases: recurrence,
            interaction: interaction);

        await fixture.ViewModel.DeleteOccurrenceCommand.ExecuteAsync(null);
        Assert.Empty(recurrence.DeleteInstanceCommands);

        interaction.ConfirmRecurrenceResult = true;
        await fixture.ViewModel.DeleteOccurrenceCommand.ExecuteAsync(null);

        var command = Assert.Single(recurrence.DeleteInstanceCommands);
        Assert.Equal(TaskId, command.TaskId);
        Assert.Equal(6, command.ExpectedVersion);
        Assert.Equal(2, interaction.RecurrenceConfirmationCalls);
    }

    [Fact]
    public async Task DeleteFutureOccurrences_WhenConfirmed_ShouldPassSelectedStartDateAndSeriesVersion()
    {
        var task = CreateTask("当前实例", seriesId: SeriesId, occurrenceDate: EffectiveDate);
        var recurrence = new RecordingRecurrenceUseCases { SeriesToGet = CreateSeries(version: 5) };
        var interaction = new RecordingInteractionService { ConfirmRecurrenceResult = true };
        var fixture = await CreateFixtureAsync(
            request: TaskEditorRequest.Edit(TaskId),
            taskToLoad: task,
            recurrenceUseCases: recurrence,
            interaction: interaction);
        fixture.ViewModel.DeleteFromDateValue = new DateTime(2026, 10, 1);

        await fixture.ViewModel.DeleteFutureOccurrencesCommand.ExecuteAsync(null);

        var command = Assert.Single(recurrence.DeleteFutureCommands);
        Assert.Equal(SeriesId, command.SeriesId);
        Assert.Equal(5, command.ExpectedVersion);
        Assert.Equal(new DateOnly(2026, 10, 1), command.FromDate);
        Assert.Equal(1, interaction.RecurrenceConfirmationCalls);
        Assert.Empty(fixture.Tasks.DeletedCommands);
    }

    private static void PrepareRecurringTask(
        TaskEditorViewModel viewModel,
        RecurrenceFrequencyCode frequency)
    {
        viewModel.Title = "每周任务";
        viewModel.IsRecurrenceEnabled = true;
        viewModel.SelectedRecurrenceFrequency = frequency;
        viewModel.RecurrenceEffectiveDateValue = EffectiveDate.ToDateTime(TimeOnly.MinValue);
        viewModel.RecurrenceTimeZoneId = "UTC";
        viewModel.RecurrenceEndDateValue = null;
        if (frequency == RecurrenceFrequencyCode.Weekly)
        {
            viewModel.RecurrenceWeekdayOptions.Single(option => option.Value == RecurrenceWeekdayCode.Monday).IsSelected = true;
            viewModel.RecurrenceWeekdayOptions.Single(option => option.Value == RecurrenceWeekdayCode.Friday).IsSelected = true;
        }

        if (frequency == RecurrenceFrequencyCode.Monthly)
        {
            viewModel.MonthlyDay = 31;
        }

        if (frequency == RecurrenceFrequencyCode.Yearly)
        {
            viewModel.YearMonth = 2;
            viewModel.YearDay = 29;
        }
    }

    private static TaskDto CreateTask(
        string title,
        Guid? seriesId = null,
        DateOnly? occurrenceDate = null,
        long version = 1)
    {
        var now = new DateTimeOffset(2026, 9, 19, 1, 30, 0, TimeSpan.Zero);
        return new TaskDto(
            TaskId,
            title,
            CategoryId,
            TaskPriorityCode.Normal,
            WorkflowStatusCode.Pending,
            occurrenceDate ?? EffectiveDate,
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            null,
            null,
            null,
            null,
            null,
            seriesId,
            occurrenceDate,
            false,
            now,
            now,
            null,
            version);
    }

    private static RecurrenceSeriesDto CreateSeries(long version = 1)
    {
        var now = new DateTimeOffset(2026, 9, 19, 1, 30, 0, TimeSpan.Zero);
        return new RecurrenceSeriesDto(
            SeriesId,
            "系列标题",
            CategoryId,
            TaskPriorityCode.Normal,
            new RecurrenceRuleDto(
                RecurrenceFrequencyCode.Daily,
                new DateOnly(2026, 9, 1),
                "UTC",
                null,
                RecurrenceWeekdayCode.None,
                null,
                null,
                null,
                1),
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            null,
            null,
            null,
            null,
            true,
            now,
            now,
            version);
    }

    private static async Task<EditorFixture> CreateFixtureAsync(
        RecordingInteractionService? interaction = null,
        IAttachmentUseCases? attachmentUseCases = null,
        TaskEditorRequest? request = null,
        TaskDto? taskToLoad = null,
        RecordingRecurrenceUseCases? recurrenceUseCases = null)
    {
        var tasks = new RecordingTaskUseCases(taskToLoad);
        interaction ??= new RecordingInteractionService();
        recurrenceUseCases ??= new RecordingRecurrenceUseCases();
        var viewModel = new TaskEditorViewModel(
            tasks,
            new FixedTimeProvider(),
            interaction,
            request ?? TaskEditorRequest.Create(),
            localTimeZoneId: "UTC",
            attachmentUseCases: attachmentUseCases,
            recurrenceUseCases: recurrenceUseCases);
        await viewModel.InitializeAsync();
        Assert.True(viewModel.IsInitialized);
        return new EditorFixture(viewModel, tasks, recurrenceUseCases, interaction);
    }

    private sealed record EditorFixture(
        TaskEditorViewModel ViewModel,
        RecordingTaskUseCases Tasks,
        RecordingRecurrenceUseCases Recurrence,
        RecordingInteractionService Interaction);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 19, 1, 30, 0, TimeSpan.Zero);
    }

    private sealed class RecordingInteractionService : ITaskEditorInteractionService
    {
        public IReadOnlyList<AttachmentFileSelection> SelectedAttachments { get; init; } =
            Array.Empty<AttachmentFileSelection>();

        public bool ConfirmRecurrenceResult { get; set; } = true;

        public int RecurrenceConfirmationCalls { get; private set; }

        public bool ConfirmDeadlineBeforePlannedDate(DateOnly plannedDate, DateOnly deadlineDate) => true;

        public DateTimeOffset? ChooseAmbiguousDeadline(
            DateOnly localDate,
            TimeOnly localTime,
            string timeZoneId,
            IReadOnlyList<DateTimeOffset> candidates) => candidates.Count == 0 ? null : candidates[0];

        public bool ConfirmDiscardChanges() => true;

        public IReadOnlyList<AttachmentFileSelection> SelectAttachmentFiles() => SelectedAttachments;

        public string? PromptAttachmentDisplayName(string currentDisplayName) => currentDisplayName;

        public bool ConfirmRemoveAttachment(string displayName) => true;

        public bool ConfirmRecurrenceOperation(string title, string message, string confirmLabel)
        {
            RecurrenceConfirmationCalls++;
            return ConfirmRecurrenceResult;
        }
    }

    private sealed class NoOpAttachmentUseCases : IAttachmentUseCases
    {
        public Task<ApplicationResult<IReadOnlyList<AttachmentDto>>> GetByTaskIdAsync(
            GetAttachmentsByTaskQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ApplicationResult<IReadOnlyList<AttachmentDto>>.Success(Array.Empty<AttachmentDto>()));

        public Task<ApplicationResult<AttachmentDto>> ImportAsync(
            ImportAttachmentCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ApplicationResult<AttachmentDto>.Failure(new ApplicationError(
                ApplicationErrorKind.Unexpected,
                "Test.Unused",
                "未配置附件导入。")));

        public Task<ApplicationResult<AttachmentDto>> OpenAsync(
            OpenAttachmentCommand command,
            CancellationToken cancellationToken = default) => NotConfigured<AttachmentDto>();

        public Task<ApplicationResult<AttachmentDto>> RevealAsync(
            RevealAttachmentCommand command,
            CancellationToken cancellationToken = default) => NotConfigured<AttachmentDto>();

        public Task<ApplicationResult<AttachmentDto>> RenameAsync(
            RenameAttachmentCommand command,
            CancellationToken cancellationToken = default) => NotConfigured<AttachmentDto>();

        public Task<ApplicationResult<AttachmentRemovalResult>> RemoveAsync(
            RemoveAttachmentCommand command,
            CancellationToken cancellationToken = default) => NotConfigured<AttachmentRemovalResult>();

        private static Task<ApplicationResult<T>> NotConfigured<T>() =>
            Task.FromResult(ApplicationResult<T>.Failure(new ApplicationError(
                ApplicationErrorKind.Unexpected,
                "Test.Unused",
                "该操作未配置。")));
    }

    private sealed class RecordingRecurrenceUseCases : IRecurrenceUseCases
    {
        public RecurrenceSeriesDto? SeriesToGet { get; set; }
        public List<CreateRecurrenceSeriesCommand> CreateCommands { get; } = [];
        public List<UpdateRecurrenceSeriesCommand> UpdateCommands { get; } = [];
        public List<DeleteRecurrenceInstanceCommand> DeleteInstanceCommands { get; } = [];
        public List<DeleteFutureRecurrenceCommand> DeleteFutureCommands { get; } = [];

        public Task<ApplicationResult<RecurrenceSeriesDto>> CreateAsync(
            CreateRecurrenceSeriesCommand command,
            CancellationToken cancellationToken = default)
        {
            CreateCommands.Add(command);
            return Task.FromResult(ApplicationResult<RecurrenceSeriesDto>.Success(CreateSeries()));
        }

        public Task<ApplicationResult<RecurrenceSeriesDto>> GetAsync(
            GetRecurrenceSeriesQuery query,
            CancellationToken cancellationToken = default) => SeriesToGet is null
            ? Task.FromResult(ApplicationResult<RecurrenceSeriesDto>.Failure(new ApplicationError(
                ApplicationErrorKind.NotFound,
                "Test.SeriesNotFound",
                "测试系列不存在。")))
            : Task.FromResult(ApplicationResult<RecurrenceSeriesDto>.Success(SeriesToGet));

        public Task<ApplicationResult<IReadOnlyList<RecurrenceSeriesDto>>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ApplicationResult<IReadOnlyList<RecurrenceSeriesDto>>.Success(
                Array.Empty<RecurrenceSeriesDto>()));

        public Task<ApplicationResult<RecurrenceSeriesDto>> UpdateAsync(
            UpdateRecurrenceSeriesCommand command,
            CancellationToken cancellationToken = default)
        {
            UpdateCommands.Add(command);
            return Task.FromResult(ApplicationResult<RecurrenceSeriesDto>.Success(CreateSeries(command.ExpectedVersion + 1)));
        }

        public Task<ApplicationResult<RecurrenceSeriesDto>> DeactivateAsync(
            DeactivateRecurrenceSeriesCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ApplicationResult<RecurrenceSeriesDto>.Failure(new ApplicationError(
                ApplicationErrorKind.Unexpected,
                "Test.Unused",
                "该操作未配置。")));

        public Task<ApplicationResult<DeletedRecurrenceInstanceDto>> DeleteInstanceAsync(
            DeleteRecurrenceInstanceCommand command,
            CancellationToken cancellationToken = default)
        {
            DeleteInstanceCommands.Add(command);
            return Task.FromResult(ApplicationResult<DeletedRecurrenceInstanceDto>.Success(
                new DeletedRecurrenceInstanceDto(command.TaskId, SeriesId, EffectiveDate, [EffectiveDate])));
        }

        public Task<ApplicationResult<DeletedFutureRecurrenceDto>> DeleteFutureAsync(
            DeleteFutureRecurrenceCommand command,
            CancellationToken cancellationToken = default)
        {
            DeleteFutureCommands.Add(command);
            return Task.FromResult(ApplicationResult<DeletedFutureRecurrenceDto>.Success(
                new DeletedFutureRecurrenceDto(SeriesId, command.FromDate, Array.Empty<Guid>(), command.ExpectedVersion + 1, [])));
        }
    }

    private sealed class RecordingTaskUseCases : ITaskUseCases
    {
        private readonly TaskDto? _taskToGet;

        public RecordingTaskUseCases(TaskDto? taskToGet) => _taskToGet = taskToGet;

        public List<CreateTaskCommand> CreatedCommands { get; } = [];
        public List<UpdateTaskCommand> UpdatedCommands { get; } = [];
        public List<DeleteTaskCommand> DeletedCommands { get; } = [];

        public Task<ApplicationResult<TaskDto>> CreateAsync(
            CreateTaskCommand command,
            CancellationToken cancellationToken = default)
        {
            CreatedCommands.Add(command);
            return Task.FromResult(ApplicationResult<TaskDto>.Success(CreateTask(command.Draft.Title)));
        }

        public Task<ApplicationResult<TaskDto>> UpdateAsync(
            UpdateTaskCommand command,
            CancellationToken cancellationToken = default)
        {
            UpdatedCommands.Add(command);
            return Task.FromResult(ApplicationResult<TaskDto>.Success(CreateTask(command.Draft.Title, version: command.ExpectedVersion + 1)));
        }

        public Task<ApplicationResult<TaskDto>> CompleteAsync(ChangeTaskStateCommand command, CancellationToken cancellationToken = default) => NotConfigured<TaskDto>();
        public Task<ApplicationResult<TaskDto>> CancelCompletionAsync(ChangeTaskStateCommand command, CancellationToken cancellationToken = default) => NotConfigured<TaskDto>();
        public Task<ApplicationResult<TaskDto>> StartProcessingAsync(ChangeTaskStateCommand command, CancellationToken cancellationToken = default) => NotConfigured<TaskDto>();

        public Task<ApplicationResult<DeletedTaskDto>> DeleteAsync(
            DeleteTaskCommand command,
            CancellationToken cancellationToken = default)
        {
            DeletedCommands.Add(command);
            return NotConfigured<DeletedTaskDto>();
        }

        public Task<ApplicationResult<TaskDto>> GetAsync(GetTaskQuery query, CancellationToken cancellationToken = default) =>
            _taskToGet is null
                ? NotConfigured<TaskDto>()
                : Task.FromResult(ApplicationResult<TaskDto>.Success(_taskToGet));

        public Task<ApplicationResult<IReadOnlyList<TaskDto>>> GetPlannedByDateAsync(GetTasksByPlannedDateQuery query, CancellationToken cancellationToken = default) => NotConfigured<IReadOnlyList<TaskDto>>();
        public Task<ApplicationResult<IReadOnlyList<TaskDto>>> GetUpcomingDeadlinesAsync(GetUpcomingDeadlinesQuery query, CancellationToken cancellationToken = default) => NotConfigured<IReadOnlyList<TaskDto>>();

        public Task<ApplicationResult<IReadOnlyList<CategoryOptionDto>>> GetCategoryOptionsAsync(
            GetCategoryOptionsQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ApplicationResult<IReadOnlyList<CategoryOptionDto>>.Success(
            [
                new CategoryOptionDto(CategoryId, "科研", "#4F7CAC", 10)
            ]));

        public Task<ApplicationResult<CalendarDayDto>> GetCalendarDateAsync(GetCalendarDateQuery query, CancellationToken cancellationToken = default) => NotConfigured<CalendarDayDto>();
        public Task<ApplicationResult<TodayPendingDto>> GetTodayPendingAsync(GetTodayPendingQuery query, CancellationToken cancellationToken = default) => NotConfigured<TodayPendingDto>();
        public Task<ApplicationResult<WeekCalendarDto>> GetWeekCalendarAsync(GetWeekCalendarQuery query, CancellationToken cancellationToken = default) => NotConfigured<WeekCalendarDto>();
        public Task<ApplicationResult<MonthCalendarDto>> GetMonthCalendarAsync(GetMonthCalendarQuery query, CancellationToken cancellationToken = default) => NotConfigured<MonthCalendarDto>();
        public Task<ApplicationResult<DeadlineQueryResult>> GetDeadlinesAsync(GetDeadlinesQuery query, CancellationToken cancellationToken = default) => NotConfigured<DeadlineQueryResult>();
        public Task<ApplicationResult<PagedResult<TaskDto>>> SearchAsync(SearchTasksQuery query, CancellationToken cancellationToken = default) => NotConfigured<PagedResult<TaskDto>>();

        private static Task<ApplicationResult<T>> NotConfigured<T>() =>
            Task.FromResult(ApplicationResult<T>.Failure(new ApplicationError(
                ApplicationErrorKind.Unexpected,
                "Test.NotConfigured",
                "该测试未配置此用例。")));
    }
}
