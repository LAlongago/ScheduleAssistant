using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>
/// Provider-neutral search criteria. Keyword is the literal user text; Infrastructure owns
/// escaping provider-specific pattern characters before executing LIKE.
/// </summary>
public sealed record TaskSearchFilter(
    string? Keyword,
    Guid? CategoryId,
    TaskPriority? Priority,
    WorkflowStatus? WorkflowStatus,
    bool? IsOverdue,
    DateTimeOffset NowUtc);
