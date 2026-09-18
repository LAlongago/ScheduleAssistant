namespace ScheduleAssistant.Application.Common;

/// <summary>A stable one-based page returned by an Application query.</summary>
/// <typeparam name="T">The page item type.</typeparam>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    long TotalCount,
    int PageNumber,
    int PageSize)
{
    /// <summary>Gets the total number of pages for the requested page size.</summary>
    public long TotalPages => TotalCount == 0
        ? 0
        : (TotalCount + PageSize - 1) / PageSize;
}
