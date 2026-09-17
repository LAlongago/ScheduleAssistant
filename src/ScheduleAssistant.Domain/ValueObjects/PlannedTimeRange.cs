namespace ScheduleAssistant.Domain;

/// <summary>
/// Optional planned start and end wall-clock times. A date is validated by <see cref="TaskItem"/>.
/// </summary>
public sealed class PlannedTimeRange : IEquatable<PlannedTimeRange>
{
    /// <summary>
    /// Initializes a planned time range. Either endpoint may be absent.
    /// </summary>
    public PlannedTimeRange(TimeOnly? start, TimeOnly? end)
    {
        if (start.HasValue && end.HasValue && end.Value < start.Value)
        {
            throw new DomainValidationException("Planned end time must not be earlier than planned start time.", nameof(end));
        }

        Start = start;
        End = end;
    }

    /// <summary>
    /// Gets the optional start time.
    /// </summary>
    public TimeOnly? Start { get; }

    /// <summary>
    /// Gets the optional end time.
    /// </summary>
    public TimeOnly? End { get; }

    /// <summary>
    /// Gets whether either endpoint is present.
    /// </summary>
    public bool HasValue => Start.HasValue || End.HasValue;

    /// <inheritdoc />
    public bool Equals(PlannedTimeRange? other)
    {
        return other is not null && Start == other.Start && End == other.End;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as PlannedTimeRange);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Start, End);
    }

    /// <summary>
    /// Compares two planned time ranges by value.
    /// </summary>
    public static bool operator ==(PlannedTimeRange? left, PlannedTimeRange? right)
    {
        return ReferenceEquals(left, right) || left is not null && left.Equals(right);
    }

    /// <summary>
    /// Compares two planned time ranges by value.
    /// </summary>
    public static bool operator !=(PlannedTimeRange? left, PlannedTimeRange? right)
    {
        return !(left == right);
    }
}
