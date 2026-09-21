using System.Diagnostics.CodeAnalysis;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Tasks;

/// <summary>
/// Converts an explicit Windows local date/time to a resolved UTC deadline.
/// Invalid and ambiguous DST values are never silently shifted or selected.
/// </summary>
public sealed class TaskDeadlineResolver
{
    /// <summary>Resolves one deadline input.</summary>
    [SuppressMessage(
        "Performance",
        "CA1822",
        Justification = "The instance is an injectable boundary so callers can replace the resolver in deterministic tests.")]
    public DeadlineResolution Resolve(DeadlineInput input)
    {
        var resolved = ResolveDomain(input);
        var deadline = resolved.Deadline;
        return new DeadlineResolution(
            resolved.Status,
            deadline is null
                ? null
                : new DeadlineDto(
                    deadline.LocalDate,
                    deadline.LocalTime,
                    deadline.TimeZoneId,
                    deadline.Utc),
            resolved.Error);
    }

    [SuppressMessage(
        "Performance",
        "CA1822",
        Justification = "The resolver remains an injectable Application boundary for deterministic callers.")]
    internal DomainDeadlineResolution ResolveDomain(DeadlineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return Failure(
                DeadlineResolutionStatus.RequiresModification,
                "Deadline.TimeZoneNotFound",
                "The selected Windows time zone is not available.");
        }
        catch (InvalidTimeZoneException)
        {
            return Failure(
                DeadlineResolutionStatus.RequiresModification,
                "Deadline.TimeZoneInvalid",
                "The selected Windows time zone is invalid.");
        }
        catch (ArgumentException)
        {
            return Failure(
                DeadlineResolutionStatus.RequiresModification,
                "Deadline.TimeZoneInvalid",
                "The selected Windows time zone is invalid.");
        }

        var local = input.LocalDate.ToDateTime(input.LocalTime, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
        {
            return Failure(
                DeadlineResolutionStatus.RequiresModification,
                "Deadline.InvalidLocalTime",
                "This local deadline does not exist in the selected time zone. Choose another time.");
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            var offsets = timeZone.GetAmbiguousTimeOffsets(local);
            if (!input.ConfirmedUtc.HasValue)
            {
                return Failure(
                    DeadlineResolutionStatus.RequiresConfirmation,
                    "Deadline.AmbiguousLocalTime",
                    "This local deadline occurs twice in the selected time zone. Confirm which UTC instant to use.");
            }

            var confirmedUtc = input.ConfirmedUtc.Value.ToUniversalTime();
            if (!offsets.Any(offset => new DateTimeOffset(local, offset).ToUniversalTime() == confirmedUtc))
            {
                return Failure(
                    DeadlineResolutionStatus.RequiresConfirmation,
                    "Deadline.AmbiguousConfirmationInvalid",
                    "The confirmed UTC instant does not match either valid interpretation of this local deadline.");
            }

            return Resolved(input, confirmedUtc);
        }

        var resolvedUtc = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(local, timeZone),
            TimeSpan.Zero);
        if (input.ConfirmedUtc.HasValue
            && input.ConfirmedUtc.Value.ToUniversalTime() != resolvedUtc)
        {
            return Failure(
                DeadlineResolutionStatus.RequiresConfirmation,
                "Deadline.ConfirmationMismatch",
                "The confirmed UTC instant does not match this local deadline.");
        }

        return Resolved(input, resolvedUtc);
    }

    private static DomainDeadlineResolution Resolved(DeadlineInput input, DateTimeOffset utc)
    {
        return new DomainDeadlineResolution(
            DeadlineResolutionStatus.Resolved,
            ZonedDeadline.CreateResolvedUtc(input.LocalDate, input.LocalTime, input.TimeZoneId, utc),
            Error: null);
    }

    private static DomainDeadlineResolution Failure(
        DeadlineResolutionStatus status,
        string code,
        string message)
    {
        return new DomainDeadlineResolution(
            status,
            Deadline: null,
            ApplicationErrorMapper.Validation(code, message));
    }
}

internal sealed record DomainDeadlineResolution(
    DeadlineResolutionStatus Status,
    ZonedDeadline? Deadline,
    ApplicationError? Error);
