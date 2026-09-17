namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>A persisted occurrence date excluded from a recurrence series.</summary>
public sealed record RecurrenceExclusion(Guid SeriesId, DateOnly OccurrenceDate);
