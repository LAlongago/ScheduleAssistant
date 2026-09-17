namespace ScheduleAssistant.Domain;

/// <summary>
/// A deadline preserving the user's local wall-clock input alongside an already-resolved UTC instant.
/// </summary>
public sealed class ZonedDeadline : IEquatable<ZonedDeadline>
{
    /// <summary>
    /// Initializes a deadline from fields resolved by an outer time-conversion boundary.
    /// This constructor never converts the local fields and never consults the machine time zone.
    /// </summary>
    public ZonedDeadline(DateOnly localDate, TimeOnly localTime, string timeZoneId, DateTimeOffset utc)
    {
        LocalDate = localDate;
        LocalTime = localTime;
        TimeZoneId = DomainValidation.NormalizeTimeZoneId(timeZoneId);
        Utc = DomainValidation.NormalizeUtc(utc, nameof(utc));
    }

    /// <summary>
    /// Creates a deadline whose UTC instant has already been resolved by the caller.
    /// </summary>
    public static ZonedDeadline CreateResolvedUtc(
        DateOnly localDate,
        TimeOnly localTime,
        string timeZoneId,
        DateTimeOffset utc)
    {
        return new ZonedDeadline(localDate, localTime, timeZoneId, utc);
    }

    /// <summary>
    /// Gets the local date entered by the user.
    /// </summary>
    public DateOnly LocalDate { get; }

    /// <summary>
    /// Gets the local wall-clock time entered by the user.
    /// </summary>
    public TimeOnly LocalTime { get; }

    /// <summary>
    /// Gets the Windows time-zone identifier supplied by the caller.
    /// </summary>
    public string TimeZoneId { get; }

    /// <summary>
    /// Gets the already-resolved UTC instant, always with a zero offset.
    /// </summary>
    public DateTimeOffset Utc { get; }

    /// <summary>
    /// Gets the UTC instant under a name useful to scheduling callers.
    /// </summary>
    public DateTimeOffset UtcDateTime => Utc;

    /// <inheritdoc />
    public bool Equals(ZonedDeadline? other)
    {
        return other is not null
            && LocalDate == other.LocalDate
            && LocalTime == other.LocalTime
            && string.Equals(TimeZoneId, other.TimeZoneId, StringComparison.Ordinal)
            && Utc == other.Utc;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as ZonedDeadline);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(LocalDate, LocalTime, StringComparer.Ordinal.GetHashCode(TimeZoneId), Utc);
    }

    /// <summary>
    /// Compares two deadlines by value.
    /// </summary>
    public static bool operator ==(ZonedDeadline? left, ZonedDeadline? right)
    {
        return ReferenceEquals(left, right) || left is not null && left.Equals(right);
    }

    /// <summary>
    /// Compares two deadlines by value.
    /// </summary>
    public static bool operator !=(ZonedDeadline? left, ZonedDeadline? right)
    {
        return !(left == right);
    }
}
