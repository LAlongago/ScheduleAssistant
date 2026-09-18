using System.IO;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Common;

internal static class ApplicationErrorMapper
{
    public static ApplicationError Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            PersistenceConflictException => new ApplicationError(
                ApplicationErrorKind.Conflict,
                "Persistence.Conflict",
                "The item was changed elsewhere. Reload it and try again."),
            PersistenceNotFoundException notFound => new ApplicationError(
                ApplicationErrorKind.NotFound,
                "Persistence.NotFound",
                notFound.EntityName.Equals("Category", StringComparison.Ordinal)
                    ? "The selected category is no longer available."
                    : "The requested item could not be found."),
            PersistenceFailureException failure when failure.Kind == PersistenceFailureKind.Unavailable => new ApplicationError(
                ApplicationErrorKind.StorageUnavailable,
                "Persistence.Unavailable",
                "The local data store is unavailable. Try again."),
            PersistenceFailureException failure when failure.Kind == PersistenceFailureKind.Constraint => new ApplicationError(
                ApplicationErrorKind.Validation,
                "Persistence.Constraint",
                "The requested change conflicts with existing data."),
            DomainValidationException domainValidation => Validation(domainValidation),
            KeyNotFoundException => new ApplicationError(
                ApplicationErrorKind.NotFound,
                "Persistence.NotFound",
                "The requested item could not be found."),
            ArgumentException argumentException => new ApplicationError(
                ApplicationErrorKind.Validation,
                "Validation.InvalidRequest",
                SafeArgumentMessage(argumentException)),
            InvalidOperationException => new ApplicationError(
                ApplicationErrorKind.Validation,
                "Validation.InvalidOperation",
                "The requested operation is not valid for the current item state."),
            IOException or UnauthorizedAccessException or TimeoutException => new ApplicationError(
                ApplicationErrorKind.StorageUnavailable,
                "Persistence.Unavailable",
                "The local data store is unavailable. Try again."),
            _ => new ApplicationError(
                ApplicationErrorKind.Unexpected,
                "Application.Unexpected",
                "The operation could not be completed. Refresh and try again.")
        };
    }

    public static ApplicationError Validation(string code, string message)
    {
        return new ApplicationError(ApplicationErrorKind.Validation, code, message);
    }

    private static ApplicationError Validation(DomainValidationException exception)
    {
        var field = exception.ParamName;
        var message = field switch
        {
            "title" => "Enter a title between 1 and 200 characters.",
            "plannedDate" => "A planned date is required when a planned time is supplied.",
            "plannedStart" or "plannedEnd" => "The planned time range is invalid.",
            "categoryId" => "Select a valid category.",
            _ => "One or more task fields are invalid."
        };
        return Validation("Validation.Domain", message);
    }

    private static string SafeArgumentMessage(ArgumentException exception)
    {
        return exception.ParamName switch
        {
            "rangeEnd" => "The query end date must not be earlier than its start date.",
            _ => "The request is invalid."
        };
    }
}
