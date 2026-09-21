using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Application.Tests;

public sealed class TaskContractMapperTests
{
    [Fact]
    public void TaskPriority_WhenMappedInBothDirections_ShouldPreserveEveryLegalMember()
    {
        Assert.All(
            Enum.GetValues<TaskPriority>(),
            value => Assert.Equal(value, TaskContractMapper.ToDomain(TaskContractMapper.ToCode(value))));
        Assert.All(
            Enum.GetValues<TaskPriorityCode>(),
            value => Assert.Equal(value, TaskContractMapper.ToCode(TaskContractMapper.ToDomain(value))));
    }

    [Fact]
    public void WorkflowStatus_WhenMappedInBothDirections_ShouldPreserveEveryLegalMember()
    {
        Assert.All(
            Enum.GetValues<WorkflowStatus>(),
            value => Assert.Equal(value, TaskContractMapper.ToDomain(TaskContractMapper.ToCode(value))));
        Assert.All(
            Enum.GetValues<WorkflowStatusCode>(),
            value => Assert.Equal(value, TaskContractMapper.ToCode(TaskContractMapper.ToDomain(value))));
    }

    [Fact]
    public void DisplayStatus_WhenMappedInBothDirections_ShouldPreserveEveryLegalMember()
    {
        Assert.All(
            Enum.GetValues<DisplayStatus>(),
            value => Assert.Equal(value, TaskContractMapper.ToDomain(TaskContractMapper.ToCode(value))));
        Assert.All(
            Enum.GetValues<DisplayStatusCode>(),
            value => Assert.Equal(value, TaskContractMapper.ToCode(TaskContractMapper.ToDomain(value))));
    }

    [Fact]
    public void DeadlineUrgency_WhenMappedInBothDirections_ShouldPreserveEveryLegalMember()
    {
        Assert.All(
            Enum.GetValues<DeadlineUrgencyLevel>(),
            value => Assert.Equal(value, TaskContractMapper.ToDomain(TaskContractMapper.ToCode(value))));
        Assert.All(
            Enum.GetValues<DeadlineUrgencyCode>(),
            value => Assert.Equal(value, TaskContractMapper.ToCode(TaskContractMapper.ToDomain(value))));
    }
}
