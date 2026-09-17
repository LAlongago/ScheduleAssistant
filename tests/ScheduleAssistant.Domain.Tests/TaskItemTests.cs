using System.Reflection;
using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Domain.Tests;

public sealed class TaskItemTests
{
    [Fact]
    public void Create_WhenTitleHasOuterWhitespace_ShouldStoreTrimmedTitle()
    {
        var task = TaskItem.Create(
            DomainTestData.TaskId,
            "  Read specification  ",
            DomainTestData.CategoryId,
            TaskPriority.Normal,
            DomainTestData.CreatedAtUtc);

        Assert.Equal("Read specification", task.Title);
        Assert.Equal(WorkflowStatus.Pending, task.WorkflowStatus);
        Assert.Equal(1, task.Version);
        Assert.Equal(DomainTestData.CreatedAtUtc, task.CreatedAtUtc);
        Assert.Equal(DomainTestData.CreatedAtUtc, task.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public void Create_WhenTitleIsWithinBounds_ShouldSucceed(int length)
    {
        var task = TaskItem.Create(
            DomainTestData.TaskId,
            new string('x', length),
            DomainTestData.CategoryId,
            DomainTestData.CreatedAtUtc);

        Assert.Equal(length, task.Title.Length);
    }

    [Fact]
    public void Create_WhenTitleIsEmptyOrWhitespace_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => DomainTestData.CreateTaskWithTitle(string.Empty));
        Assert.Throws<DomainValidationException>(() => DomainTestData.CreateTaskWithTitle("   \r\n\t  "));
    }

    [Fact]
    public void Create_WhenTrimmedTitleIsEmpty_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => DomainTestData.CreateTaskWithTitle("  \t  "));
    }

    [Fact]
    public void Create_WhenTitleIsTooLong_ShouldRejectItWithoutTruncating()
    {
        var title = new string('x', 201);

        var exception = Assert.Throws<DomainValidationException>(() => DomainTestData.CreateTaskWithTitle(title));

        Assert.Contains("200", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(301)]
    public void Create_WhenLocationExceedsMaximum_ShouldRejectIt(int length)
    {
        if (length == 300)
        {
            var task = DomainTestData.CreateTaskWith(location: new string('l', length));
            Assert.Equal(length, task.Location!.Length);
            return;
        }

        Assert.Throws<DomainValidationException>(() => DomainTestData.CreateTaskWith(location: new string('l', length)));
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(10_001)]
    public void Create_WhenDescriptionIsAtBoundary_ShouldApplyMaximum(int length)
    {
        if (length == 10_000)
        {
            var task = DomainTestData.CreateTaskWith(description: new string('d', length));
            Assert.Equal(length, task.Description!.Length);
            return;
        }

        Assert.Throws<DomainValidationException>(() => DomainTestData.CreateTaskWith(description: new string('d', length)));
    }

    [Fact]
    public void Create_WhenMaterialsOrNotesExceedMaximum_ShouldRejectEachField()
    {
        var tooLong = new string('m', 10_001);

        Assert.Throws<DomainValidationException>(() => DomainTestData.CreateTaskWith(materials: tooLong));
        Assert.Throws<DomainValidationException>(() => DomainTestData.CreateTaskWith(notes: tooLong));
    }

    [Fact]
    public void Create_WhenMaterialsAndNotesAreAtMaximum_ShouldPreserveBothFields()
    {
        var materials = new string('m', 10_000);
        var notes = new string('n', 10_000);

        var task = DomainTestData.CreateTaskWith(materials: materials, notes: notes);

        Assert.Equal(materials, task.Materials);
        Assert.Equal(notes, task.Notes);
    }

    [Fact]
    public void Create_WhenOptionalTextIsBlank_ShouldStoreNullAndPreserveMultilineText()
    {
        const string multiline = "line one\r\nline two\n中文";

        var task = DomainTestData.CreateTaskWith(
            location: " ",
            description: "\t",
            materials: string.Empty,
            notes: multiline);

        Assert.Null(task.Location);
        Assert.Null(task.Description);
        Assert.Null(task.Materials);
        Assert.Equal(multiline, task.Notes);
    }

    [Fact]
    public void Create_WhenScheduleTimeHasNoDate_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => DomainTestData.CreateTaskWith(plannedStart: new TimeOnly(9, 0)));
        Assert.Throws<DomainValidationException>(() => DomainTestData.CreateTaskWith(plannedEnd: new TimeOnly(10, 0)));
    }

    [Fact]
    public void Create_WhenEndEqualsStart_ShouldAllowIt()
    {
        var task = DomainTestData.CreateTaskWith(
            plannedDate: new DateOnly(2026, 1, 2),
            plannedStart: new TimeOnly(9, 0),
            plannedEnd: new TimeOnly(9, 0));

        Assert.Equal(task.PlannedStart, task.PlannedEnd);
    }

    [Fact]
    public void Create_WhenOnlyOneScheduleEndpointExists_ShouldAllowIt()
    {
        var withStart = DomainTestData.CreateTaskWith(
            plannedDate: new DateOnly(2026, 1, 2),
            plannedStart: new TimeOnly(9, 0));
        var withEnd = DomainTestData.CreateTaskWith(
            plannedDate: new DateOnly(2026, 1, 2),
            plannedEnd: new TimeOnly(10, 0));

        Assert.Null(withStart.PlannedEnd);
        Assert.Null(withEnd.PlannedStart);
    }

    [Fact]
    public void Create_WhenEndIsEarlierThanStart_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => DomainTestData.CreateTaskWith(
            plannedDate: new DateOnly(2026, 1, 2),
            plannedStart: new TimeOnly(10, 0),
            plannedEnd: new TimeOnly(9, 0)));
    }

    [Fact]
    public void Create_WhenDeadlineDateDiffersFromPlannedDate_ShouldKeepBothConceptsIndependent()
    {
        var deadline = DomainTestData.CreateDeadline(DomainTestData.AtUtc(2026, 1, 5, 18, 0));
        var task = DomainTestData.CreateTaskWith(
            plannedDate: new DateOnly(2026, 1, 2),
            deadline: deadline);

        Assert.Equal(new DateOnly(2026, 1, 2), task.PlannedDate);
        Assert.Equal(deadline, task.Deadline);
        Assert.Equal(DomainTestData.AtUtc(2026, 1, 5, 18, 0), task.DeadlineUtc);
    }

    [Fact]
    public void Create_WhenRequiredIdIsEmpty_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => TaskItem.Create(
            Guid.Empty,
            "Task",
            DomainTestData.CategoryId,
            DomainTestData.CreatedAtUtc));
        Assert.Throws<DomainValidationException>(() => TaskItem.Create(
            DomainTestData.TaskId,
            "Task",
            Guid.Empty,
            DomainTestData.CreatedAtUtc));
    }

    [Fact]
    public void Create_WhenPriorityIsUndefined_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => TaskItem.Create(
            DomainTestData.TaskId,
            "Task",
            DomainTestData.CategoryId,
            (TaskPriority)99,
            DomainTestData.CreatedAtUtc));
    }

    [Fact]
    public void Rehydrate_WhenWorkflowAndCompletionTimestampAreInconsistent_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => DomainTestData.RehydrateTask(
            workflowStatus: WorkflowStatus.Completed,
            completedAtUtc: null));
        Assert.Throws<DomainValidationException>(() => DomainTestData.RehydrateTask(
            workflowStatus: WorkflowStatus.Pending,
            completedAtUtc: DomainTestData.AtUtc(2026, 1, 2)));
        Assert.Throws<DomainValidationException>(() => DomainTestData.RehydrateTask(workflowStatus: (WorkflowStatus)99));
    }

    [Fact]
    public void Rehydrate_WhenVersionIsZero_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => DomainTestData.RehydrateTask(version: 0));
    }

    [Fact]
    public void Rehydrate_WhenRecurrenceIdentityIsPartial_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => DomainTestData.RehydrateTask(seriesId: Guid.NewGuid()));
        Assert.Throws<DomainValidationException>(() => DomainTestData.RehydrateTask(occurrenceDate: new OccurrenceDate(new DateOnly(2026, 1, 2))));
        Assert.Throws<DomainValidationException>(() => DomainTestData.RehydrateTask(isOccurrenceOverride: true));
    }

    [Fact]
    public void MarkAsOccurrenceOverride_WhenTaskIsARecurrenceInstance_ShouldMarkOnlyThatTask()
    {
        var seriesId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var task = TaskItem.Create(
            DomainTestData.TaskId,
            "Instance",
            DomainTestData.CategoryId,
            TaskPriority.Normal,
            DomainTestData.CreatedAtUtc,
            seriesId: seriesId,
            occurrenceDate: new OccurrenceDate(new DateOnly(2026, 1, 2)));

        task.MarkAsOccurrenceOverride(DomainTestData.AtUtc(2026, 1, 2));

        Assert.True(task.IsOccurrenceOverride);
        Assert.Equal(seriesId, task.SeriesId);
        Assert.Equal(new OccurrenceDate(new DateOnly(2026, 1, 2)), task.OccurrenceDate);
    }

    [Fact]
    public void Rehydrate_WhenCompleted_ShouldRestoreStateWithoutReplayingCompletion()
    {
        var completedAt = DomainTestData.AtUtc(2026, 1, 2, 10, 0);
        var updatedAt = DomainTestData.AtUtc(2026, 1, 2, 11, 0);

        var task = DomainTestData.RehydrateTask(
            workflowStatus: WorkflowStatus.Completed,
            completedAtUtc: completedAt,
            updatedAtUtc: updatedAt,
            version: 7);

        Assert.Equal(WorkflowStatus.Completed, task.WorkflowStatus);
        Assert.Equal(completedAt, task.CompletedAtUtc);
        Assert.Equal(updatedAt, task.UpdatedAtUtc);
        Assert.Equal(7, task.Version);
    }

    [Fact]
    public void StartProcessing_WhenPending_ShouldChangeOnlyWorkflowAndUpdateTimestamp()
    {
        var deadline = DomainTestData.CreateDeadline(DomainTestData.AtUtc(2026, 1, 5));
        var task = DomainTestData.CreateTaskWith(new DateOnly(2026, 1, 2), deadline: deadline);
        var updatedAt = DomainTestData.AtUtc(2026, 1, 1, 1, 0);

        task.StartProcessing(updatedAt);

        Assert.Equal(WorkflowStatus.InProgress, task.WorkflowStatus);
        Assert.Null(task.CompletedAtUtc);
        Assert.Equal(updatedAt, task.UpdatedAtUtc);
        Assert.Equal(new DateOnly(2026, 1, 2), task.PlannedDate);
        Assert.Equal(deadline, task.Deadline);
    }

    [Fact]
    public void StartProcessing_WhenRepeatedInProgress_ShouldBeIdempotent()
    {
        var task = DomainTestData.CreateTask();
        var firstUpdate = DomainTestData.AtUtc(2026, 1, 1, 1, 0);
        task.StartProcessing(firstUpdate);

        task.StartProcessing(DomainTestData.AtUtc(2026, 1, 1, 2, 0));

        Assert.Equal(WorkflowStatus.InProgress, task.WorkflowStatus);
        Assert.Equal(firstUpdate, task.UpdatedAtUtc);
    }

    [Fact]
    public void StartProcessing_WhenCompleted_ShouldRequireCancellationFirst()
    {
        var task = DomainTestData.CreateTask();
        task.Complete(DomainTestData.AtUtc(2026, 1, 2));

        Assert.Throws<InvalidOperationException>(() => task.StartProcessing(DomainTestData.AtUtc(2026, 1, 3)));
        Assert.Equal(WorkflowStatus.Completed, task.WorkflowStatus);
    }

    [Fact]
    public void Complete_WhenPendingOrInProgress_ShouldRecordCallerTimestamp()
    {
        var pending = DomainTestData.CreateTask();
        var completedAt = new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.FromHours(8));
        pending.Complete(completedAt);

        var inProgress = DomainTestData.CreateTask();
        inProgress.StartProcessing(DomainTestData.AtUtc(2026, 1, 1, 1, 0));
        inProgress.Complete(DomainTestData.AtUtc(2026, 1, 2, 10, 0), DomainTestData.AtUtc(2026, 1, 2, 11, 0));

        Assert.Equal(WorkflowStatus.Completed, pending.WorkflowStatus);
        Assert.Equal(completedAt.ToUniversalTime(), pending.CompletedAtUtc);
        Assert.Equal(WorkflowStatus.Completed, inProgress.WorkflowStatus);
        Assert.Equal(DomainTestData.AtUtc(2026, 1, 2, 10, 0), inProgress.CompletedAtUtc);
    }

    [Fact]
    public void Complete_WhenRepeated_ShouldPreserveFirstCompletionTimestamp()
    {
        var task = DomainTestData.CreateTask();
        var first = DomainTestData.AtUtc(2026, 1, 2, 10, 0);
        task.Complete(first);

        task.Complete(DomainTestData.AtUtc(2026, 1, 3, 10, 0));

        Assert.Equal(first, task.CompletedAtUtc);
        Assert.Equal(first, task.UpdatedAtUtc);
    }

    [Fact]
    public void CancelCompletion_WhenCompleted_ShouldRestorePendingAndClearTimestamp()
    {
        var task = DomainTestData.CreateTask(
            plannedDate: new DateOnly(2026, 1, 2),
            deadline: DomainTestData.CreateDeadline(DomainTestData.AtUtc(2026, 1, 3)));
        task.StartProcessing(DomainTestData.AtUtc(2026, 1, 1, 1, 0));
        task.Complete(DomainTestData.AtUtc(2026, 1, 2), DomainTestData.AtUtc(2026, 1, 2, 1, 0));

        task.CancelCompletion(DomainTestData.AtUtc(2026, 1, 3));

        Assert.Equal(WorkflowStatus.Pending, task.WorkflowStatus);
        Assert.Null(task.CompletedAtUtc);
        Assert.Equal(DomainTestData.AtUtc(2026, 1, 3), task.UpdatedAtUtc);
        Assert.Equal(new DateOnly(2026, 1, 2), task.PlannedDate);
    }

    [Fact]
    public void CancelCompletion_WhenRepeatedForNonCompletedTask_ShouldBeIdempotent()
    {
        var task = DomainTestData.CreateTask();

        task.CancelCompletion(DomainTestData.AtUtc(2026, 1, 2));

        Assert.Equal(WorkflowStatus.Pending, task.WorkflowStatus);
        Assert.Equal(DomainTestData.CreatedAtUtc, task.UpdatedAtUtc);
    }

    [Fact]
    public void UpdateDetails_WhenCandidateIsInvalid_ShouldLeaveAllExistingFieldsUnchanged()
    {
        var deadline = DomainTestData.CreateDeadline(DomainTestData.AtUtc(2026, 1, 5));
        var task = TaskItem.Create(
            DomainTestData.TaskId,
            "Original",
            DomainTestData.CategoryId,
            TaskPriority.Important,
            DomainTestData.CreatedAtUtc,
            new DateOnly(2026, 1, 2),
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            deadline,
            "Office",
            "Description",
            "Materials",
            "Notes");

        Assert.Throws<DomainValidationException>(() => task.UpdateDetails(
            "Changed",
            DomainTestData.CategoryId,
            TaskPriority.Low,
            new DateOnly(2026, 1, 3),
            new TimeOnly(11, 0),
            new TimeOnly(10, 0),
            null,
            null,
            null,
            null,
            null,
            DomainTestData.AtUtc(2026, 1, 2)));

        Assert.Equal("Original", task.Title);
        Assert.Equal(TaskPriority.Important, task.Priority);
        Assert.Equal(new DateOnly(2026, 1, 2), task.PlannedDate);
        Assert.Equal(new TimeOnly(9, 0), task.PlannedStart);
        Assert.Equal(new TimeOnly(10, 0), task.PlannedEnd);
        Assert.Equal(deadline, task.Deadline);
        Assert.Equal("Office", task.Location);
    }

    [Fact]
    public void UpdateDetails_WhenValid_ShouldUpdateFieldsWithoutChangingIdentityCreationOrVersion()
    {
        var task = DomainTestData.CreateTask();
        var updatedAt = DomainTestData.AtUtc(2026, 1, 2);
        var deadline = DomainTestData.CreateDeadline(DomainTestData.AtUtc(2026, 1, 4));

        task.UpdateDetails(
            "  Updated  ",
            DomainTestData.CategoryId,
            TaskPriority.UrgentAndImportant,
            new DateOnly(2026, 1, 3),
            new TimeOnly(8, 0),
            new TimeOnly(9, 0),
            deadline,
            "Home",
            "Details",
            "Bring notes",
            "Review",
            updatedAt);

        Assert.Equal("Updated", task.Title);
        Assert.Equal(TaskPriority.UrgentAndImportant, task.Priority);
        Assert.Equal(new DateOnly(2026, 1, 3), task.PlannedDate);
        Assert.Equal(deadline, task.Deadline);
        Assert.Equal(updatedAt, task.UpdatedAtUtc);
        Assert.Equal(DomainTestData.TaskId, task.Id);
        Assert.Equal(DomainTestData.CreatedAtUtc, task.CreatedAtUtc);
        Assert.Equal(1, task.Version);
    }

    [Fact]
    public void PublicIdentityAndCreationProperties_ShouldNotExposeSetters()
    {
        Assert.Null(typeof(TaskItem).GetProperty(nameof(TaskItem.Id))!.GetSetMethod());
        Assert.Null(typeof(TaskItem).GetProperty(nameof(TaskItem.CreatedAtUtc))!.GetSetMethod());
        Assert.Null(typeof(TaskItem).GetProperty(nameof(TaskItem.Version))!.GetSetMethod());
        Assert.Null(typeof(TaskItem).GetProperty(nameof(TaskItem.SeriesId))!.GetSetMethod());
        Assert.Null(typeof(TaskItem).GetProperty(nameof(TaskItem.OccurrenceDate))!.GetSetMethod());
    }
}
