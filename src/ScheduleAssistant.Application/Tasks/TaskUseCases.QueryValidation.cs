using ScheduleAssistant.Application.Common;
namespace ScheduleAssistant.Application.Tasks;

public sealed partial class TaskUseCases
{
    private QueryTimeSnapshot CaptureQueryTime()
    {
        var nowUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, TimeZoneInfo.Local);
        return new QueryTimeSnapshot(
            nowUtc,
            DateOnly.FromDateTime(localNow.DateTime));
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private static ApplicationError? ValidateDeadlineRange(DeadlineQueryRange range)
    {
        return Enum.IsDefined(range)
            ? null
            : ApplicationErrorMapper.Validation(
                "Validation.DeadlineRange",
                "Select a supported deadline range.");
    }

    private static ApplicationError? ValidateSearchQuery(
        SearchTasksQuery query,
        out long offset)
    {
        offset = 0;
        if (query.PageNumber < 1)
        {
            return ApplicationErrorMapper.Validation(
                "Validation.PageNumber",
                "The page number must be at least 1.");
        }

        if (query.PageSize is < 1 or > MaximumSearchPageSize)
        {
            return ApplicationErrorMapper.Validation(
                "Validation.PageSize",
                $"The page size must be between 1 and {MaximumSearchPageSize}.");
        }

        if (query.CategoryId is Guid categoryId && categoryId == Guid.Empty)
        {
            return ApplicationErrorMapper.Validation(
                "Validation.CategoryId",
                "The category filter is invalid.");
        }

        if (query.Priority.HasValue
            && !Enum.IsDefined(query.Priority.Value))
        {
            return ApplicationErrorMapper.Validation(
                "Validation.Priority",
                "The priority filter is invalid.");
        }

        if (query.WorkflowStatus.HasValue
            && !Enum.IsDefined(query.WorkflowStatus.Value))
        {
            return ApplicationErrorMapper.Validation(
                "Validation.WorkflowStatus",
                "The workflow status filter is invalid.");
        }

        try
        {
            offset = checked((long)(query.PageNumber - 1) * query.PageSize);
        }
        catch (OverflowException)
        {
            return ApplicationErrorMapper.Validation(
                "Validation.PageNumber",
                "The requested page is outside the supported range.");
        }

        return null;
    }

    private static string? NormalizeKeyword(string? keyword)
    {
        return string.IsNullOrWhiteSpace(keyword) ? null : keyword;
    }

    private readonly record struct QueryTimeSnapshot(
        DateTimeOffset NowUtc,
        DateOnly TodayLocal);
}
