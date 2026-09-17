using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Domain.Tests;

public sealed class StatusAndDeadlineTests
{
    [Fact]
    public void DisplayStatus_ShouldApplyTheSpecifiedPrecedenceOrder()
    {
        var today = new DateOnly(2026, 1, 10);
        var now = DomainTestData.AtUtc(2026, 1, 10, 12, 0);

        var completedOverdue = DomainTestData.CreateTask(deadline: DomainTestData.CreateDeadline(now.AddHours(-1)));
        completedOverdue.Complete(now);
        var overdue = DomainTestData.CreateTask(deadline: DomainTestData.CreateDeadline(now.AddHours(-1)));
        var inProgress = DomainTestData.CreateTask();
        inProgress.StartProcessing(now);
        var plannedPast = DomainTestData.CreateTask(plannedDate: new DateOnly(2026, 1, 9));
        var notStarted = DomainTestData.CreateTask(plannedDate: today);

        Assert.Equal(DisplayStatus.Completed, DisplayStatusCalculator.Calculate(completedOverdue, now, today));
        Assert.Equal(DisplayStatus.Overdue, DisplayStatusCalculator.Calculate(overdue, now, today));
        Assert.Equal(DisplayStatus.InProgress, DisplayStatusCalculator.Calculate(inProgress, now, today));
        Assert.Equal(DisplayStatus.PlannedPast, DisplayStatusCalculator.Calculate(plannedPast, now, today));
        Assert.Equal(DisplayStatus.NotStarted, DisplayStatusCalculator.Calculate(notStarted, now, today));
    }

    [Fact]
    public void DisplayStatus_WhenOverdueInProgress_ShouldPreferOverdue()
    {
        var now = DomainTestData.AtUtc(2026, 1, 10, 12, 0);
        var task = DomainTestData.CreateTask(deadline: DomainTestData.CreateDeadline(now.AddMinutes(-1)));
        task.StartProcessing(now);

        Assert.Equal(DisplayStatus.Overdue, DisplayStatusCalculator.Calculate(task, now, new DateOnly(2026, 1, 10)));
    }

    [Fact]
    public void DisplayStatus_WhenDeadlineEqualsNow_ShouldNotBeOverdue()
    {
        var now = DomainTestData.AtUtc(2026, 1, 10, 12, 0);
        var task = DomainTestData.CreateTask(deadline: DomainTestData.CreateDeadline(now));

        Assert.Equal(DisplayStatus.NotStarted, DisplayStatusCalculator.Calculate(task, now, new DateOnly(2026, 1, 10)));
    }

    [Fact]
    public void DisplayStatus_WhenTodayLocalDiffersFromUtcDate_ShouldUseExplicitLocalDate()
    {
        var nowUtc = DomainTestData.AtUtc(2026, 1, 10, 1, 0);
        var task = DomainTestData.CreateTask(plannedDate: new DateOnly(2026, 1, 10));

        Assert.Equal(DisplayStatus.NotStarted, DisplayStatusCalculator.Calculate(task, nowUtc, new DateOnly(2026, 1, 10)));
        Assert.Equal(DisplayStatus.PlannedPast, DisplayStatusCalculator.Calculate(task, nowUtc, new DateOnly(2026, 1, 11)));
    }

    [Fact]
    public void DisplayStatus_WhenCompletedTaskIsCancelledAfterDeadline_ShouldRecalculateAsOverdue()
    {
        var deadline = DomainTestData.AtUtc(2026, 1, 2);
        var task = DomainTestData.CreateTask(deadline: DomainTestData.CreateDeadline(deadline));
        task.Complete(DomainTestData.AtUtc(2026, 1, 1));

        Assert.Equal(DisplayStatus.Completed, DisplayStatusCalculator.Calculate(task, deadline.AddMinutes(1), new DateOnly(2026, 1, 2)));

        task.CancelCompletion(DomainTestData.AtUtc(2026, 1, 2, 1, 0));

        Assert.Equal(DisplayStatus.Overdue, DisplayStatusCalculator.Calculate(task, deadline.AddMinutes(1), new DateOnly(2026, 1, 2)));
    }

    [Theory]
    [InlineData(8, DeadlineUrgencyLevel.Neutral)]
    [InlineData(7, DeadlineUrgencyLevel.MoreThanThreeDays)]
    [InlineData(3, DeadlineUrgencyLevel.OneToThreeDays)]
    [InlineData(1, DeadlineUrgencyLevel.OneToThreeDays)]
    [InlineData(0, DeadlineUrgencyLevel.LessThanOneDay)]
    [InlineData(-1, DeadlineUrgencyLevel.Overdue)]
    public void DeadlineUrgency_WhenAtWholeDayBoundary_ShouldUseSpecifiedLevel(int remainingDays, DeadlineUrgencyLevel expected)
    {
        var now = DomainTestData.AtUtc(2026, 1, 1);
        var task = DomainTestData.CreateTask(deadline: DomainTestData.CreateDeadline(now.AddDays(remainingDays)));

        Assert.Equal(expected, DeadlineUrgencyCalculator.Calculate(task, now));
    }

    [Fact]
    public void DeadlineUrgency_WhenJustAcrossBoundaries_ShouldUseStrictComparisons()
    {
        var now = DomainTestData.AtUtc(2026, 1, 1);

        Assert.Equal(
            DeadlineUrgencyLevel.MoreThanThreeDays,
            GetUrgency(now, TimeSpan.FromDays(7).Subtract(TimeSpan.FromTicks(1))));
        Assert.Equal(
            DeadlineUrgencyLevel.OneToThreeDays,
            GetUrgency(now, TimeSpan.FromDays(3).Subtract(TimeSpan.FromTicks(1))));
        Assert.Equal(
            DeadlineUrgencyLevel.LessThanOneDay,
            GetUrgency(now, TimeSpan.FromDays(1).Subtract(TimeSpan.FromTicks(1))));
        Assert.Equal(
            DeadlineUrgencyLevel.Overdue,
            GetUrgency(now, TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void DeadlineUrgency_WhenTaskHasNoDeadlineOrIsCompleted_ShouldReturnNone()
    {
        var now = DomainTestData.AtUtc(2026, 1, 1);
        var noDeadline = DomainTestData.CreateTask();
        var completed = DomainTestData.CreateTask(deadline: DomainTestData.CreateDeadline(now.AddDays(2)));
        completed.Complete(now);

        Assert.Equal(DeadlineUrgencyLevel.None, DeadlineUrgencyCalculator.Calculate(noDeadline, now));
        Assert.Equal(DeadlineUrgencyLevel.None, DeadlineUrgencyCalculator.Calculate(completed, now));
        Assert.NotEqual(DeadlineUrgencyLevel.Neutral, DeadlineUrgencyCalculator.Calculate(noDeadline, now));
    }

    private static DeadlineUrgencyLevel GetUrgency(DateTimeOffset now, TimeSpan remaining)
    {
        var task = DomainTestData.CreateTask(deadline: DomainTestData.CreateDeadline(now + remaining));
        return DeadlineUrgencyCalculator.Calculate(task, now);
    }
}
