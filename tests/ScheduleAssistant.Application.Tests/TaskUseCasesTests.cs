using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Application.Tests;

public sealed class TaskUseCasesTests
{
    [Fact]
    public async Task CreateAsync_WhenCategoryIsMissing_ShouldReturnNotFoundWithoutWriting()
    {
        await using var context = new TaskUseCaseTestContext();

        var result = await context.UseCases.CreateAsync(new CreateTaskCommand(
            new TaskDraft("Missing category", Guid.NewGuid())));

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal("Category.NotFound", result.Error.Code);
        Assert.Empty(context.Store.Tasks);
        Assert.Empty(context.Publisher.Events);
    }

    [Fact]
    public async Task CreateAsync_WhenCategoryIsArchived_ShouldReturnValidationWithoutWriting()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory(archived: true);

        var result = await context.UseCases.CreateAsync(new CreateTaskCommand(
            new TaskDraft("Archived category", category.Id)));

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrorKind.Validation, result.Error!.Kind);
        Assert.Equal("Category.Archived", result.Error.Code);
        Assert.Empty(context.Store.Tasks);
    }

    [Fact]
    public async Task CreateAsync_WhenDeadlinePrecedesPlannedDate_ShouldSaveWithNonBlockingWarning()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();

        var result = await context.UseCases.CreateAsync(new CreateTaskCommand(new TaskDraft(
            "Deadline warning",
            category.Id,
            PlannedDate: new DateOnly(2026, 1, 3),
            Deadline: new DeadlineInput(
                new DateOnly(2026, 1, 2),
                new TimeOnly(12, 0),
                "China Standard Time"))));

        Assert.True(result.IsSuccess);
        Assert.Equal("Deadline.BeforePlannedDate", Assert.Single(result.Warnings).Code);
        Assert.Equal(
            new DateTimeOffset(2026, 1, 2, 4, 0, 0, TimeSpan.Zero),
            result.Value!.Deadline!.Utc);
        Assert.Single(context.Store.RemindersFor(result.Value.Id));
    }

    [Fact]
    public async Task CreateAsync_WhenDeadlineIsAmbiguousWithoutConfirmation_ShouldRequireConfirmation()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();

        var result = await context.UseCases.CreateAsync(new CreateTaskCommand(new TaskDraft(
            "Ambiguous deadline",
            category.Id,
            Deadline: new DeadlineInput(
                new DateOnly(2026, 11, 1),
                new TimeOnly(1, 30),
                "Pacific Standard Time"))));

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrorKind.Validation, result.Error!.Kind);
        Assert.Equal("Deadline.AmbiguousLocalTime", result.Error.Code);
        Assert.Empty(context.Store.Tasks);
    }

    [Fact]
    public async Task CreateAsync_WhenDeadlineIsInvalid_ShouldRequireModification()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();

        var result = await context.UseCases.CreateAsync(new CreateTaskCommand(new TaskDraft(
            "Invalid deadline",
            category.Id,
            Deadline: new DeadlineInput(
                new DateOnly(2026, 3, 8),
                new TimeOnly(2, 30),
                "Pacific Standard Time"))));

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrorKind.Validation, result.Error!.Kind);
        Assert.Equal("Deadline.InvalidLocalTime", result.Error.Code);
        Assert.Empty(context.Store.Tasks);
    }

    [Fact]
    public async Task CompleteAsync_WhenPendingAndDeliveredRemindersExist_ShouldCancelOnlyPendingInSameTransaction()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        var created = await context.CreateTaskAsync(category.Id);
        context.Store.AddDeliveredReminder(created.Value!.Id);

        var result = await context.UseCases.CompleteAsync(
            new ChangeTaskStateCommand(created.Value.Id, created.Value.Version));

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowStatus.Completed, result.Value!.WorkflowStatus);
        var reminders = context.Store.RemindersFor(created.Value.Id);
        Assert.Contains(reminders, reminder => reminder.Status == ReminderStatus.Cancelled);
        Assert.Contains(reminders, reminder => reminder.Status == ReminderStatus.Delivered);
        Assert.Contains(context.Publisher.Events, applicationEvent => applicationEvent is TaskCompletedChanged);
        Assert.Contains(context.Publisher.Events, applicationEvent => applicationEvent is ReminderPlanChanged);
    }

    [Fact]
    public async Task CreateAsync_WhenCommitFails_ShouldReturnStorageUnavailableAndPublishNothing()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        context.TransactionFactory.CommitFailure = new PersistenceFailureException(
            PersistenceFailureKind.Unavailable,
            "commit");

        var result = await context.UseCases.CreateAsync(new CreateTaskCommand(new TaskDraft(
            "Rollback task",
            category.Id,
            Deadline: new DeadlineInput(
                new DateOnly(2026, 1, 3),
                new TimeOnly(12, 0),
                "China Standard Time"))));

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrorKind.StorageUnavailable, result.Error!.Kind);
        Assert.Empty(context.Store.Tasks);
        Assert.Empty(context.Publisher.Events);
        Assert.True(context.TransactionFactory.LastTransaction!.RollbackCalled);
    }

    [Fact]
    public async Task CreateAsync_WhenPostCommitEventFails_ShouldKeepSuccessAndRequestRefresh()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        context.Publisher.ExceptionToThrow = new InvalidOperationException("subscriber failure");

        var result = await context.CreateTaskAsync(category.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.RequiresRefresh);
        Assert.Equal(PostCommitEventStatus.RefreshRequired, result.PostCommitEventStatus);
        Assert.Single(context.Store.Tasks);
    }

    [Fact]
    public async Task UpdateAsync_WhenExpectedVersionIsStale_ShouldReturnConflictWithoutOverwriting()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        var created = await context.CreateTaskAsync(category.Id);
        var firstUpdate = await context.UseCases.UpdateAsync(new UpdateTaskCommand(
            created.Value!.Id,
            created.Value.Version,
            new TaskDraft("First update", category.Id)));
        Assert.True(firstUpdate.IsSuccess);

        var stale = await context.UseCases.UpdateAsync(new UpdateTaskCommand(
            created.Value.Id,
            created.Value.Version,
            new TaskDraft("Stale update", category.Id)));

        Assert.False(stale.IsSuccess);
        Assert.Equal(ApplicationErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal("First update", context.Store.Tasks[created.Value.Id].Title);
    }

    [Fact]
    public async Task UpdateAsync_WhenDeadlineChanges_ShouldReplacePendingReminderWithoutDuplicateNodes()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        var created = await context.CreateTaskAsync(category.Id);

        var result = await context.UseCases.UpdateAsync(new UpdateTaskCommand(
            created.Value!.Id,
            created.Value.Version,
            new TaskDraft(
                "Test task",
                category.Id,
                Deadline: new DeadlineInput(
                    new DateOnly(2026, 1, 5),
                    new TimeOnly(12, 0),
                    "China Standard Time"))));

        Assert.True(result.IsSuccess);
        var reminders = context.Store.RemindersFor(created.Value.Id);
        Assert.Single(reminders, reminder => reminder.Status == ReminderStatus.Pending);
        Assert.Single(reminders, reminder => reminder.Status == ReminderStatus.Cancelled);
        Assert.Equal(
            new DateTimeOffset(2026, 1, 4, 4, 0, 0, TimeSpan.Zero),
            Assert.Single(reminders, reminder => reminder.Status == ReminderStatus.Pending).ScheduledAtUtc);
    }

    [Fact]
    public async Task CancelCompletion_AfterDeadlineEdit_ShouldRestoreOnlyTheCurrentPlan()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        var created = await context.CreateTaskAsync(category.Id);

        var edited = await context.UseCases.UpdateAsync(new UpdateTaskCommand(
            created.Value!.Id,
            created.Value.Version,
            new TaskDraft(
                "Test task",
                category.Id,
                Deadline: new DeadlineInput(
                    new DateOnly(2026, 1, 5),
                    new TimeOnly(12, 0),
                    "China Standard Time"))));
        var completed = await context.UseCases.CompleteAsync(
            new ChangeTaskStateCommand(edited.Value!.Id, edited.Value.Version));

        var restored = await context.UseCases.CancelCompletionAsync(
            new ChangeTaskStateCommand(completed.Value!.Id, completed.Value.Version));

        Assert.True(restored.IsSuccess);
        var pending = Assert.Single(
            context.Store.RemindersFor(created.Value.Id),
            reminder => reminder.Status == ReminderStatus.Pending);
        Assert.Equal(
            new DateTimeOffset(2026, 1, 4, 4, 0, 0, TimeSpan.Zero),
            pending.ScheduledAtUtc);
    }

    [Fact]
    public async Task CancelCompletion_AfterReminderWasDisabled_ShouldNotRestoreAPlan()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        var created = await context.CreateTaskAsync(category.Id);
        var deadline = created.Value!.Deadline!;

        var disabled = await context.UseCases.UpdateAsync(new UpdateTaskCommand(
            created.Value.Id,
            created.Value.Version,
            new TaskDraft(
                "Test task",
                category.Id,
                Deadline: new DeadlineInput(
                    deadline.LocalDate,
                    deadline.LocalTime,
                    deadline.TimeZoneId,
                    deadline.Utc),
                ReminderPlan: new ReminderPlanInput(Enabled: false))));
        var completed = await context.UseCases.CompleteAsync(
            new ChangeTaskStateCommand(disabled.Value!.Id, disabled.Value.Version));

        var restored = await context.UseCases.CancelCompletionAsync(
            new ChangeTaskStateCommand(completed.Value!.Id, completed.Value.Version));

        Assert.True(restored.IsSuccess);
        Assert.DoesNotContain(
            context.Store.RemindersFor(created.Value.Id),
            reminder => reminder.Status == ReminderStatus.Pending);
    }

    [Fact]
    public async Task StateTransitions_WhenRepeated_ShouldPreserveDomainIdempotencyAndVersion()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        var created = await context.CreateTaskAsync(category.Id);

        var started = await context.UseCases.StartProcessingAsync(
            new ChangeTaskStateCommand(created.Value!.Id, created.Value.Version));
        var repeatedStart = await context.UseCases.StartProcessingAsync(
            new ChangeTaskStateCommand(created.Value.Id, started.Value!.Version));
        var completed = await context.UseCases.CompleteAsync(
            new ChangeTaskStateCommand(created.Value.Id, repeatedStart.Value!.Version));
        var repeatedComplete = await context.UseCases.CompleteAsync(
            new ChangeTaskStateCommand(created.Value.Id, completed.Value!.Version));

        Assert.Equal(2, started.Value!.Version);
        Assert.Equal(2, repeatedStart.Value!.Version);
        Assert.Equal(3, completed.Value!.Version);
        Assert.Equal(3, repeatedComplete.Value!.Version);
        Assert.Equal(WorkflowStatus.Completed, repeatedComplete.Value.WorkflowStatus);
    }

    [Fact]
    public async Task CancelCompletion_WhenReminderWasCancelled_ShouldRestoreOnePendingPlanWithoutDuplicates()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        var created = await context.CreateTaskAsync(category.Id);
        var completed = await context.UseCases.CompleteAsync(
            new ChangeTaskStateCommand(created.Value!.Id, created.Value.Version));

        var restored = await context.UseCases.CancelCompletionAsync(
            new ChangeTaskStateCommand(created.Value.Id, completed.Value!.Version));
        var repeated = await context.UseCases.CancelCompletionAsync(
            new ChangeTaskStateCommand(created.Value.Id, restored.Value!.Version));

        Assert.True(restored.IsSuccess);
        Assert.Equal(WorkflowStatus.Pending, restored.Value!.WorkflowStatus);
        Assert.Equal(3, repeated.Value!.Version);
        Assert.Single(context.Store.RemindersFor(created.Value.Id), reminder => reminder.Status == ReminderStatus.Pending);
    }

    [Fact]
    public async Task GetAsync_WhenRepositoryCancels_ShouldPropagateCancellationInsteadOfUnexpected()
    {
        await using var context = new TaskUseCaseTestContext();
        context.TaskRepository.ExceptionToThrow = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => context.UseCases.GetAsync(new GetTaskQuery(Guid.NewGuid())));
    }

    [Fact]
    public void DeadlineResolver_WhenAmbiguousUtcIsExplicitlyConfirmed_ShouldUseTheConfirmedOffset()
    {
        var resolver = new TaskDeadlineResolver();
        var date = new DateOnly(2026, 11, 1);
        var time = new TimeOnly(1, 30);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        var confirmedUtc = new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local)[0]).ToUniversalTime();

        var result = resolver.Resolve(new DeadlineInput(date, time, zone.Id, confirmedUtc));

        Assert.Equal(DeadlineResolutionStatus.Resolved, result.Status);
        Assert.Equal(confirmedUtc, result.Deadline!.Utc);
    }
}
