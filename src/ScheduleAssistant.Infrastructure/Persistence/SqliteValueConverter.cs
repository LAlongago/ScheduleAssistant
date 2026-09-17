using System.Globalization;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Persistence;

internal static class SqliteValueConverter
{
    private const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm:ss.fffffff";
    private const string LocalDateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff";

    public static string ToGuid(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    public static Guid ToGuid(string value) => Guid.ParseExact(value, "D");

    public static string ToDate(DateOnly value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static DateOnly ToDate(string value) => DateOnly.ParseExact(value, DateFormat, CultureInfo.InvariantCulture);

    public static string ToTime(TimeOnly value) => value.ToString(TimeFormat, CultureInfo.InvariantCulture);

    public static TimeOnly ToTime(string value) => TimeOnly.ParseExact(value, TimeFormat, CultureInfo.InvariantCulture);

    public static string ToUtc(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString(UtcFormat, CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset ToUtc(string value)
    {
        return DateTimeOffset.ParseExact(
            value,
            UtcFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    public static string ToDeadlineLocal(ZonedDeadline deadline)
    {
        var dateTime = deadline.LocalDate.ToDateTime(deadline.LocalTime, DateTimeKind.Unspecified);
        return dateTime.ToString(LocalDateTimeFormat, CultureInfo.InvariantCulture);
    }

    public static (DateOnly Date, TimeOnly Time) ToDeadlineLocal(string value)
    {
        var dateTime = DateTime.ParseExact(value, LocalDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);
        return (DateOnly.FromDateTime(dateTime), TimeOnly.FromDateTime(dateTime));
    }
}
