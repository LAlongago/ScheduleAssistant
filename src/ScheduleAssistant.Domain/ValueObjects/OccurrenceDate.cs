namespace ScheduleAssistant.Domain;

/// <summary>
/// A recurrence occurrence represented only by a local calendar date.
/// </summary>
public sealed class OccurrenceDate : IEquatable<OccurrenceDate>, IComparable<OccurrenceDate>
{
    /// <summary>
    /// Initializes an occurrence date.
    /// </summary>
    public OccurrenceDate(DateOnly value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the local calendar date.
    /// </summary>
    public DateOnly Value { get; }

    /// <summary>
    /// Gets the local calendar date under the shorter domain-facing name.
    /// </summary>
    public DateOnly Date => Value;

    /// <inheritdoc />
    public int CompareTo(OccurrenceDate? other)
    {
        return other is null ? 1 : Value.CompareTo(other.Value);
    }

    /// <inheritdoc />
    public bool Equals(OccurrenceDate? other)
    {
        return other is not null && Value == other.Value;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as OccurrenceDate);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    /// <summary>
    /// Compares two occurrence dates by value.
    /// </summary>
    public static bool operator ==(OccurrenceDate? left, OccurrenceDate? right)
    {
        return ReferenceEquals(left, right) || left is not null && left.Equals(right);
    }

    /// <summary>
    /// Compares two occurrence dates by value.
    /// </summary>
    public static bool operator !=(OccurrenceDate? left, OccurrenceDate? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Compares two occurrence dates by calendar order.
    /// </summary>
    public static bool operator <(OccurrenceDate left, OccurrenceDate right)
    {
        return left.Value < right.Value;
    }

    /// <summary>
    /// Compares two occurrence dates by calendar order.
    /// </summary>
    public static bool operator >(OccurrenceDate left, OccurrenceDate right)
    {
        return left.Value > right.Value;
    }

    /// <summary>
    /// Compares two occurrence dates by calendar order.
    /// </summary>
    public static bool operator <=(OccurrenceDate left, OccurrenceDate right)
    {
        return left.Value <= right.Value;
    }

    /// <summary>
    /// Compares two occurrence dates by calendar order.
    /// </summary>
    public static bool operator >=(OccurrenceDate left, OccurrenceDate right)
    {
        return left.Value >= right.Value;
    }
}
